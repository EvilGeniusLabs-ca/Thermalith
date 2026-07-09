using System.Text;

namespace Niimbot.Net.Diagnostics;

/// <summary>
/// Process-wide, opt-in diagnostic trace for connection/transport troubleshooting (spec §5.1).
/// Off by default: when <see cref="Sink"/> is null the calls are near-free, so instrumentation can
/// live on the hot path. A host (the app, a harness, a test) sets <see cref="Sink"/> to capture the
/// enumeration → open → probe → command conversation and attach it to a "printer not detected"
/// report — the whole point is to diagnose an unowned model from a user's log.
///
/// <para>Kept deliberately dependency-free (a single <see cref="Action{T}"/> hook) so the library
/// stays pure — no logging framework, no UI, no file I/O here; the host owns where lines land.</para>
/// </summary>
public static class NiimbotTrace
{
    /// <summary>Where trace lines go. Null (default) disables tracing. Set by the host.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>True when a sink is attached — guard expensive message building on this.</summary>
    public static bool IsEnabled => Sink is not null;

    /// <summary>Emit one line. Never throws (a broken sink must not break printing/discovery).</summary>
    public static void Log(string message)
    {
        var sink = Sink;
        if (sink is null) return;
        try { sink(message); }
        catch { /* logging is best-effort — swallow */ }
    }

    /// <summary>Emit a categorised line, e.g. <c>[serial] open OK</c>.</summary>
    public static void Log(string category, string message)
    {
        if (Sink is null) return;
        Log($"[{category}] {message}");
    }

    /// <summary>
    /// Emit a byte buffer as space-separated hex, capped so a full-raster write can't flood the log.
    /// </summary>
    public static void Bytes(string category, string label, ReadOnlySpan<byte> data, int max = 64)
    {
        if (Sink is null) return;
        Log(category, data.Length == 0
            ? $"{label} (0 bytes)"
            : $"{label} ({data.Length} bytes): {Hex(data, max)}");
    }

    private static string Hex(ReadOnlySpan<byte> data, int max)
    {
        var n = Math.Min(data.Length, max);
        var sb = new StringBuilder(n * 3 + 12);
        for (var i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(data[i].ToString("X2"));
        }
        if (data.Length > n) sb.Append($" …(+{data.Length - n} more)");
        return sb.ToString();
    }
}
