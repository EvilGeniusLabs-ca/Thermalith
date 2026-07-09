using Niimbot.Net.Commands;
using Niimbot.Net.Diagnostics;
using Niimbot.Net.Framing;
using Niimbot.Net.Profiles;

namespace Niimbot.Net.Transport;

/// <summary>The outcome of probing a port: which model answered, and its raw id.</summary>
public sealed record ProbeResult(string PortName, PrinterModel Model, int ModelId, PrinterProfile Profile)
{
    /// <summary>The catalogue display name (real name even for models outside the <see cref="PrinterModel"/> enum).</summary>
    public string ModelName => Profile.ModelName;
}

/// <summary>
/// Confirms whether a real NIIMBOT printer is on a given port and identifies the model (spec §5.1).
/// Opens the candidate, sends a single lightweight model-id query, and waits briefly for a
/// well-formed response. A non-NIIMBOT device fails fast (no valid framed reply within the timeout)
/// rather than silently hanging. Powers the App's printer panel and the future MCP
/// <c>list_printers</c> / <c>printer_status</c>.
/// </summary>
public static class PrinterProbe
{
    /// <summary>
    /// Probe a single named port. Returns null if no NIIMBOT answered in time. The whole probe is
    /// raced against a hard wall-clock deadline on a worker task: <see cref="System.IO.Ports.SerialPort.Open"/>
    /// can block indefinitely on some Windows virtual/Bluetooth COM ports (and the port stream
    /// ignores cancellation tokens), so we abandon a stuck port rather than hang discovery.
    /// </summary>
    public static async Task<ProbeResult?> ProbeAsync(
        string portName, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = timeout ?? TimeSpan.FromMilliseconds(800);
        var work = Task.Run(() => ProbeCoreAsync(portName, deadline, ct), ct);
        var finished = await Task.WhenAny(work, Task.Delay(deadline + TimeSpan.FromMilliseconds(500), ct)).ConfigureAwait(false);
        if (finished != work)
        {
            NiimbotTrace.Log("probe", $"{portName} abandoned — port stuck (Open() blocked past deadline)");
            return null; // port stuck (e.g. Open() blocked) — abandon it
        }
        return await work.ConfigureAwait(false);
    }

    private static async Task<ProbeResult?> ProbeCoreAsync(string portName, TimeSpan deadline, CancellationToken ct)
    {
        NiimbotTrace.Log("probe", $"{portName} — probing (deadline {deadline.TotalMilliseconds:0} ms)");

        // Short write timeout so a powered-off printer (port opens, write never drains) fails fast
        // within the probe deadline instead of blocking the full default write timeout.
        await using var transport = new SerialTransport(portName, writeTimeoutMs: 400);

        try
        {
            await transport.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            NiimbotTrace.Log("probe", $"{portName} — not a printer (port busy or unopenable)");
            return null; // port busy or unopenable
        }

        var query = PacketGenerator.GetPrinterInfo(PrinterInfoType.PrinterModelId);
        var accumulator = new PacketAccumulator();
        var buffer = new byte[256];

        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadlineCts.CancelAfter(deadline);

        try
        {
            await transport.WriteAsync(query.ToBytes(), deadlineCts.Token).ConfigureAwait(false);
            NiimbotTrace.Log("probe", $"{portName} — sent GetPrinterInfo(PrinterModelId), awaiting reply");

            while (!deadlineCts.Token.IsCancellationRequested)
            {
                var read = await transport.ReadAsync(buffer, deadlineCts.Token).ConfigureAwait(false);
                if (read <= 0)
                    continue;

                accumulator.Append(buffer.AsSpan(0, read));
                while (accumulator.TryRead() is { } packet)
                {
                    if (packet.Command != (byte)ResponseCommandId.In_PrinterInfoPrinterCode || packet.Data.Length == 0)
                    {
                        NiimbotTrace.Log("probe", $"{portName} — ignoring packet cmd 0x{packet.Command:X2} " +
                            $"({packet.Data.Length} data bytes), not the model-id reply");
                        continue;
                    }

                    var modelId = packet.Data.Length == 1
                        ? packet.Data[0] << 8
                        : (packet.Data[0] << 8) | packet.Data[1];
                    var profile = PrinterProfiles.FromModelId(modelId);
                    NiimbotTrace.Log("probe", $"{portName} — NIIMBOT model id {modelId} → {profile.ModelName}");
                    return new ProbeResult(portName, profile.Model, modelId, profile);
                }
            }

            NiimbotTrace.Log("probe", $"{portName} — no model-id reply within {deadline.TotalMilliseconds:0} ms " +
                "(port opened + query sent, but the device stayed silent)");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // deadline hit — not a NIIMBOT (or too slow)
            NiimbotTrace.Log("probe", $"{portName} — deadline hit ({deadline.TotalMilliseconds:0} ms), no valid reply");
        }
        catch (Exception ex)
        {
            // Port opened but the write/read failed — e.g. the printer is powered off, or the port
            // belongs to some other device. Treat as "no NIIMBOT here" rather than surfacing the
            // TimeoutException to the caller.
            NiimbotTrace.Log("probe", $"{portName} — probe I/O failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }

        return null;
    }

    /// <summary>Probe every enumerated port and return the printers that answered.</summary>
    public static async Task<IReadOnlyList<ProbeResult>> ProbeAllAsync(
        TimeSpan? perPortTimeout = null, CancellationToken ct = default)
    {
        var results = new List<ProbeResult>();
        foreach (var port in SerialPortEnumerator.Enumerate())
        {
            var result = await ProbeAsync(port.PortName, perPortTimeout, ct).ConfigureAwait(false);
            if (result is not null)
                results.Add(result);
        }
        return results;
    }
}
