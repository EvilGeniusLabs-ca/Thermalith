// Phase 0 transport spike (build spec §12): open the serial port, push one hardcoded 1bpp
// bitmap, and get a physical label out of the printer — before anything else.
//
// Usage:
//   print-harness            → probe all serial ports for a NIIMBOT and print to the first found
//   print-harness --port COM5 → skip discovery and print to a specific port
//   print-harness --dry-run  → build + describe the bitmap and command stream, send nothing
//
// LIVE-RUN TODO (PENDING-HARDWARE): the end-to-end "bytes actually burn a label" run and the
// resulting byte capture still require a physical B1 on the bench. Everything up to WriteAsync is
// exercised here; flip --dry-run off with a printer attached to complete Phase 0.

using Niimbot.Net;
using Niimbot.Net.Encoding;
using Niimbot.Net.Transport;

var portArg = GetOption(args, "--port");
var dryRun = args.Contains("--dry-run");

Console.WriteLine("Thermalith print-harness — Phase 0 transport spike.");
Console.WriteLine();

// One hardcoded, deterministic bitmap: a 320×240 (≈40×30 mm @ 203 dpi) frame with a diagonal X.
var bitmap = BuildTestPattern(320, 240);
Console.WriteLine($"Built test bitmap: {bitmap.WidthPx}×{bitmap.HeightPx}px, {bitmap.Packed.Length} packed bytes.");

if (dryRun)
{
    var rows = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });
    Console.WriteLine($"[dry-run] Row encoder produced {rows.Count} row packets. Nothing sent.");
    return 0;
}

// Resolve a target port: explicit --port, or probe discovery.
string? port = portArg;
if (port is null)
{
    Console.WriteLine("Probing serial ports for a NIIMBOT printer...");
    var found = await PrinterProbe.ProbeAllAsync();
    if (found.Count == 0)
    {
        var names = SerialPortEnumerator.Enumerate();
        Console.WriteLine(names.Count == 0
            ? "No serial ports present."
            : $"Serial ports seen but none answered as a NIIMBOT: {string.Join(", ", names.Select(n => n.PortName))}");
        Console.WriteLine();
        Console.WriteLine("TODO (PENDING-HARDWARE): plug in a B1 and re-run to get a physical label + byte capture.");
        return 0;
    }

    var printer = found[0];
    port = printer.PortName;
    Console.WriteLine($"Found {printer.Model} (id {printer.ModelId}) on {port}.");
}

await using var client = NiimbotClient.FromSerialPort(port);
var caps = await client.ConnectAsync();
Console.WriteLine($"Connected: {caps.Model} @ {caps.Dpi} dpi, max width {caps.MaxPrintWidthMm:0.#} mm.");
if (caps.LoadedLabel is { TagPresent: true } label)
    Console.WriteLine($"Loaded RFID roll: {label.ConsumablesType}, {label.TotalLabels - label.UsedLabels} labels remaining.");

var progress = new Progress<PrintProgress>(p =>
    Console.WriteLine($"  page {p.Page}/{p.TotalPages}  print {p.PagePrintPercent}%  feed {p.PageFeedPercent}%"));

Console.WriteLine("Printing...");
await client.PrintAsync(bitmap, new PrintOptions { Copies = 1 }, progress);
Console.WriteLine("Done. A label should have come out of the printer.");
return 0;

static MonochromeBitmap BuildTestPattern(int width, int height)
{
    var bytesPerRow = (width + 7) / 8;
    var packed = new byte[bytesPerRow * height];

    void Set(int x, int y)
    {
        if ((uint)x >= width || (uint)y >= height) return;
        packed[y * bytesPerRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
    }

    // 2px border.
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

    // Diagonal X.
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
