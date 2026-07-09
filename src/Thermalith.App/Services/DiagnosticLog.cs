using System.Diagnostics;
using System.Runtime.InteropServices;
using Niimbot.Net.Diagnostics;

namespace Thermalith.App.Services;

/// <summary>
/// Bridges <see cref="NiimbotTrace"/> to a timestamped log file (and stderr) so a "printer not
/// detected" report becomes an attachable connection log — port enumeration, serial open, bytes
/// sent, and whether anything came back. Off by default; enable via Help ▸ Connection Logging, the
/// <c>THERMALITH_DEBUG=1</c> env var, or the <c>--debug</c> launch flag.
/// </summary>
public static class DiagnosticLog
{
    private static readonly object Gate = new();
    private static StreamWriter? _writer;

    /// <summary>Whether tracing is currently capturing to a file.</summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>The folder holding session log files.</summary>
    public static string LogDirectory => Path.Combine(PlatformDirectories.AppData(), "logs");

    /// <summary>The file the current session is writing to, or null when disabled.</summary>
    public static string? CurrentLogPath { get; private set; }

    /// <summary>Turn on file+stderr tracing (idempotent). A fresh, timestamped file per enable.</summary>
    public static void Enable()
    {
        lock (Gate)
        {
            if (IsEnabled) return;
            Directory.CreateDirectory(LogDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            CurrentLogPath = Path.Combine(LogDirectory, $"thermalith-{stamp}.log");
            _writer = new StreamWriter(CurrentLogPath, append: true) { AutoFlush = true };
            NiimbotTrace.Sink = Write;
            IsEnabled = true;
            WriteHeader();
        }
    }

    /// <summary>Stop tracing and close the file (idempotent).</summary>
    public static void Disable()
    {
        lock (Gate)
        {
            if (!IsEnabled) return;
            NiimbotTrace.Sink = null;
            _writer?.Dispose();
            _writer = null;
            IsEnabled = false;
        }
    }

    /// <summary>Open the log folder in the OS file manager (best-effort; creates it first).</summary>
    public static void OpenLogFolder()
    {
        Directory.CreateDirectory(LogDirectory);
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", LogDirectory) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", LogDirectory);
            else
                Process.Start("xdg-open", LogDirectory);
        }
        catch
        {
            // No file manager available (headless, unusual desktop) — nothing we can do; swallow.
        }
    }

    private static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        lock (Gate)
            _writer?.WriteLine(line);
        Console.Error.WriteLine(line);
    }

    private static void WriteHeader()
    {
        var version = typeof(DiagnosticLog).Assembly.GetName().Version?.ToString() ?? "?";
        Write($"=== Thermalith connection log ===");
        Write($"app {version} · {RuntimeInformation.FrameworkDescription} · " +
              $"{RuntimeInformation.OSDescription} · {RuntimeInformation.OSArchitecture}");
        Write($"file {CurrentLogPath}");
    }
}
