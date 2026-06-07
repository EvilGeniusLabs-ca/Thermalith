// Phase 0 transport spike (build spec §12): open the serial port, push one hardcoded 1bpp bitmap,
// and get a physical label out of the printer. Built for incremental live verification — confirm
// identification before committing paper, and capture raw TX/RX bytes (the spec's tiebreaker, §5/§10).
//
//   print-harness probe                 Enumerate + probe ports for a NIIMBOT (safe; default)
//   print-harness info       [--port X] Connect, show capabilities + status, disconnect (no print)
//   print-harness status     [--port X] Connect and read printer status
//   print-harness test-page  [--port X] Print the printer's built-in self-test (safest first print)
//   print-harness print      [--port X] Print the hardcoded test bitmap
//   print-harness encode                Encode the test bitmap offline (no device)
//
// Options: --port <name> --baud <n> --density <1-5> --copies <n> --log <file> --yes --no-color

using Niimbot.Net;
using Niimbot.Net.Encoding;
using Niimbot.Net.Transport;
using Thermalith.PrintHarness;

var command = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "probe";
var port = GetOption(args, "--port");
var baud = int.TryParse(GetOption(args, "--baud"), out var b) ? b : SerialTransport.DefaultBaudRate;
var logFile = GetOption(args, "--log");
var assumeYes = args.Contains("--yes");
Console.WriteLine("Thermalith print-harness — Phase 0 transport spike.\n");

// Ctrl+C aborts cleanly so a hung print disconnects instead of leaving the port open.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); Console.WriteLine("\n(aborting…)"); };

using var log = new CaptureLog(logFile, !args.Contains("--no-color"));

try
{
    return command switch
    {
        "probe" => await ProbeCommand(cts.Token),
        "encode" => EncodeCommand(),
        "info" => await ConnectCommand(print: PrintMode.None, cts.Token),
        "status" => await ConnectCommand(print: PrintMode.None, cts.Token, statusOnly: true),
        "test-page" => await ConnectCommand(print: PrintMode.TestPage, cts.Token),
        "print" => await ConnectCommand(print: PrintMode.Bitmap, cts.Token),
        _ => Usage($"Unknown command '{command}'."),
    };
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled.");
    return 130;
}

async Task<int> ProbeCommand(CancellationToken ct)
{
    Console.WriteLine("Probing serial ports for a NIIMBOT printer…");
    var found = await PrinterProbe.ProbeAllAsync(ct: ct);
    if (found.Count == 0)
    {
        var names = SerialPortEnumerator.Enumerate();
        Console.WriteLine(names.Count == 0
            ? "No serial ports present."
            : $"Ports seen but none answered as a NIIMBOT: {string.Join(", ", names.Select(n => n.PortName))}");
        Console.WriteLine("\nTODO (PENDING-HARDWARE): plug in a B1 and re-run to get a physical label + byte capture.");
        return 0;
    }

    foreach (var p in found)
        Console.WriteLine($"  {p.PortName}: {p.Model} (id {p.ModelId})");
    Console.WriteLine($"\nNext: `print-harness info --port {found[0].PortName}` to verify, then `test-page` or `print`.");
    return 0;
}

int EncodeCommand()
{
    var bitmap = BuildTestPattern(320, 240);
    var rows = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });
    Console.WriteLine($"Test bitmap: {bitmap.WidthPx}×{bitmap.HeightPx}px, {bitmap.Packed.Length} packed bytes.");
    Console.WriteLine($"Row encoder produced {rows.Count} row packets. (offline — nothing sent)");
    return 0;
}

async Task<int> ConnectCommand(PrintMode print, CancellationToken ct, bool statusOnly = false)
{
    var target = port ?? await ResolvePort(ct);
    if (target is null)
        return 1;

    INiimbotTransport transport = new CaptureTransport(new SerialTransport(target, baud), log.Sink);
    await using var client = new NiimbotClient(transport, ownsTransport: true);

    Console.WriteLine($"Connecting to {target} @ {baud} baud…");
    var caps = await client.ConnectAsync(ct);

    if (!statusOnly)
        PrintCapabilities(client, caps);

    var status = await client.GetStatusAsync(ct);
    Console.WriteLine($"\nStatus: cover {Fmt(status.CoverOpen, "open", "closed")}, " +
                      $"paper {Fmt(status.PaperPresent, "present", "absent")}, battery {status.Battery?.ToString() ?? "?"}.");

    switch (print)
    {
        case PrintMode.None:
            break;

        case PrintMode.TestPage:
            if (!Confirm("Send the printer's built-in self-test page?"))
                return 0;
            Console.WriteLine("Sending test page…");
            await client.PrintTestPageAsync(ct);
            Console.WriteLine("Test page sent — paper should feed.");
            break;

        case PrintMode.Bitmap:
            var bitmap = BuildTestPattern(320, 240);
            Console.WriteLine($"\nBitmap: {bitmap.WidthPx}×{bitmap.HeightPx}px.");
            if (!Confirm("Print the hardcoded test bitmap?"))
                return 0;
            var density = int.TryParse(GetOption(args, "--density"), out var d) ? (int?)d : null;
            var copies = int.TryParse(GetOption(args, "--copies"), out var c) ? c : 1;
            var progress = new Progress<PrintProgress>(p =>
                Console.WriteLine($"  page {p.Page}/{p.TotalPages}  print {p.PagePrintPercent}%  feed {p.PageFeedPercent}%"));
            Console.WriteLine("Printing…");
            await client.PrintAsync(bitmap, new PrintOptions { Density = density, Copies = copies }, progress, ct);
            Console.WriteLine("Done. A label should have come out of the printer.");
            break;
    }

    return 0;
}

async Task<string?> ResolvePort(CancellationToken ct)
{
    Console.WriteLine("No --port given; probing…");
    var found = await PrinterProbe.ProbeAllAsync(ct: ct);
    if (found.Count == 0)
    {
        Console.WriteLine("No NIIMBOT found. Pass --port <name>, or run `print-harness probe`.");
        return null;
    }
    Console.WriteLine($"Using {found[0].PortName} ({found[0].Model}).");
    return found[0].PortName;
}

void PrintCapabilities(NiimbotClient client, Niimbot.Net.Capabilities.PrinterCapabilities caps)
{
    Console.WriteLine($"\nIdentified: {caps.Model} (id {caps.ModelId})");
    Console.WriteLine($"  resolution     {caps.Dpi} dpi ({caps.PixelsPerMm:0.###} px/mm)");
    Console.WriteLine($"  printhead      {caps.PrintheadPixels} px → max {caps.MaxPrintWidthMm:0.#} mm wide");
    Console.WriteLine($"  print task     {client.Profile?.PrintTaskVersion}");
    Console.WriteLine($"  density        {caps.DensityMin}–{caps.DensityMax} (default {caps.DensityDefault})");
    Console.WriteLine($"  label types    {string.Join(", ", caps.SupportedLabelTypes)}");
    Console.WriteLine($"  firmware       {caps.FirmwareVersion ?? "?"}");
    Console.WriteLine($"  serial         {caps.SerialNumber ?? "?"}");
    if (caps.LoadedLabel is { TagPresent: true } l)
        Console.WriteLine($"  RFID roll      {l.ConsumablesType}, {l.TotalLabels - l.UsedLabels}/{l.TotalLabels} labels left");
    else if (caps.SupportsRfid)
        Console.WriteLine("  RFID roll      none detected");
}

bool Confirm(string prompt)
{
    if (assumeYes || Console.IsInputRedirected)
        return true;
    Console.Write($"{prompt} [y/N] ");
    var answer = Console.ReadLine();
    return answer?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) ?? false;
}

static string Fmt(bool? v, string t, string f) => v is null ? "?" : v.Value ? t : f;

static int Usage(string? error)
{
    if (error is not null)
        Console.WriteLine($"{error}\n");
    Console.WriteLine("Commands: probe | info | status | test-page | print | encode");
    Console.WriteLine("Options:  --port <name> --baud <n> --density <1-5> --copies <n> --log <file> --yes --no-color");
    return error is null ? 0 : 2;
}

static MonochromeBitmap BuildTestPattern(int width, int height)
{
    var bytesPerRow = (width + 7) / 8;
    var packed = new byte[bytesPerRow * height];

    void Set(int x, int y)
    {
        if ((uint)x >= width || (uint)y >= height) return;
        packed[y * bytesPerRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
    }

    for (var x = 0; x < width; x++)
    {
        Set(x, 0); Set(x, 1);
        Set(x, height - 1); Set(x, height - 2);
    }
    for (var y = 0; y < height; y++)
    {
        Set(0, y); Set(1, y);
        Set(width - 1, y); Set(width - 2, y);
    }
    for (var i = 0; i < Math.Min(width, height); i++)
    {
        var x = i * width / Math.Min(width, height);
        Set(x, i);
        Set(width - 1 - x, i);
    }

    return new MonochromeBitmap(width, height, packed);
}

static string? GetOption(string[] argv, string name)
{
    var idx = Array.IndexOf(argv, name);
    return idx >= 0 && idx + 1 < argv.Length ? argv[idx + 1] : null;
}

internal enum PrintMode { None, TestPage, Bitmap }
