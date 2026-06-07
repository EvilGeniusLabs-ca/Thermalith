using System.Diagnostics;

namespace Thermalith.PrintHarness;

/// <summary>
/// Renders the TX/RX byte capture to the console (dimmed, optionally colored) and, when a path is
/// given, appends it to a file. The file capture is the artifact to diff against the reference
/// implementations and to flip the 4 PENDING-HARDWARE-VERIFICATION tests from skipped to asserted.
/// </summary>
public sealed class CaptureLog : IDisposable
{
    private readonly StreamWriter? _file;
    private readonly bool _color;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();

    public CaptureLog(string? path, bool color)
    {
        _color = color && !Console.IsOutputRedirected;
        if (path is not null)
        {
            _file = new StreamWriter(path, append: true) { AutoFlush = true };
            _file.WriteLine($"# capture started (ticks={Stopwatch.GetTimestamp()})");
        }
    }

    /// <summary>The sink to hand to <see cref="CaptureTransport"/>: 'T' = transmit, 'R' = receive.</summary>
    public void Sink(char direction, ReadOnlyMemory<byte> data)
    {
        var arrow = direction == 'T' ? "→ TX" : "← RX";
        var hex = Convert.ToHexString(data.Span);
        var spaced = string.Join(' ', Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2)));
        var line = $"[{_clock.ElapsedMilliseconds,6} ms] {arrow}  {spaced}";

        lock (_gate)
        {
            if (_color)
                Console.ForegroundColor = direction == 'T' ? ConsoleColor.Cyan : ConsoleColor.DarkYellow;
            Console.WriteLine(line);
            if (_color)
                Console.ResetColor();

            _file?.WriteLine(line);
        }
    }

    public void Dispose() => _file?.Dispose();
}
