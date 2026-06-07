using System.Buffers;
using Niimbot.Net.Capabilities;
using Niimbot.Net.Commands;
using Niimbot.Net.Encoding;
using Niimbot.Net.Framing;
using Niimbot.Net.Profiles;

namespace Niimbot.Net;

/// <summary>
/// The session API over a <see cref="INiimbotTransport"/>: connect, query capabilities/status,
/// configure density and label type, print a 1bpp bitmap with progress, and disconnect. The client
/// owns all framing and a single-in-flight command queue — the transport stays a dumb byte duplex
/// (spec §5.1). Async-first throughout; <see cref="IAsyncDisposable"/> end to end.
///
/// <para>A background read pump accumulates bytes into whole packets, fulfilling the one in-flight
/// request and raising <see cref="PacketReceived"/> for unsolicited packets (page-index, etc.).</para>
/// </summary>
public sealed class NiimbotClient : IAsyncDisposable
{
    private readonly INiimbotTransport _transport;
    private readonly bool _ownsTransport;

    // Serializes commands so only one request is in flight at a time.
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly PacketAccumulator _accumulator = new();
    private readonly object _pendingLock = new();

    private PendingRequest? _pending;
    private Task? _readPump;
    private CancellationTokenSource? _pumpCts;
    private int _protocolVersion;

    /// <summary>Models that report lid-closed with inverted polarity (carried from niimbluelib).</summary>
    private static readonly HashSet<int> InvertedLidModels =
        [512, 514, 513, 2304, 1792, 3584, 5120, 2560, 3840, 4352, 272, 273, 274];

    public NiimbotClient(INiimbotTransport transport, bool ownsTransport = false)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _ownsTransport = ownsTransport;
    }

    /// <summary>
    /// Convenience factory for the common case: build a <see cref="Transport.SerialTransport"/> from
    /// a port name. The returned client owns and disposes that transport (ownership follows creation,
    /// spec §5.1).
    /// </summary>
    public static NiimbotClient FromSerialPort(string portName) =>
        new(new Transport.SerialTransport(portName), ownsTransport: true);

    /// <summary>The active printer profile, resolved on connect.</summary>
    public PrinterProfile? Profile { get; private set; }

    /// <summary>Hardware-derived capabilities, available after <see cref="ConnectAsync"/>.</summary>
    public PrinterCapabilities? Capabilities { get; private set; }

    public bool IsConnected => _transport.IsConnected;

    /// <summary>Raised for packets the printer sends unsolicited (not a reply to the in-flight command).</summary>
    public event EventHandler<NiimbotPacket>? PacketReceived;

    /// <summary>
    /// Connect the transport, identify the device, resolve its profile, and read capabilities
    /// (including the loaded RFID label where supported). Optional info queries that a given model
    /// does not answer are tolerated.
    /// </summary>
    public async Task<PrinterCapabilities> ConnectAsync(CancellationToken ct = default)
    {
        await _transport.ConnectAsync(ct).ConfigureAwait(false);

        _pumpCts = new CancellationTokenSource();
        _readPump = Task.Run(() => ReadLoopAsync(_pumpCts.Token));

        // Handshake. Some firmwares respond, some are silent — don't fail the whole connect on it.
        await TrySendAsync(PacketGenerator.Connect(), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);

        // Protocol version drives heartbeat variant selection.
        _protocolVersion = await TryReadProtocolVersionAsync(ct).ConfigureAwait(false);

        var modelId = await ReadModelIdAsync(ct).ConfigureAwait(false);
        var profile = PrinterProfiles.FromModelId(modelId);
        Profile = profile;

        var caps = PrinterCapabilities.FromProfile(profile, modelId);

        caps = caps with
        {
            FirmwareVersion = await TryReadInfoStringAsync(PrinterInfoType.SoftwareVersion, ct).ConfigureAwait(false),
            SerialNumber = await TryReadSerialAsync(ct).ConfigureAwait(false),
        };

        if (profile.SupportsRfid)
        {
            var rfid = await TryReadRfidAsync(ct).ConfigureAwait(false);
            if (rfid is { TagPresent: true })
                caps = caps with { LoadedLabel = rfid };
        }

        Capabilities = caps;
        return caps;
    }

    /// <summary>Read the live readiness snapshot (cover / paper / battery) via a heartbeat.</summary>
    public async Task<PrinterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var type = _protocolVersion >= 3 ? HeartbeatType.Advanced2 : HeartbeatType.Advanced1;
        var packet = await SendAsync(PacketGenerator.Heartbeat(type), TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false)
            ?? throw new NiimbotTimeoutException("No heartbeat response.");

        var hb = type == HeartbeatType.Advanced2
            ? DecodeHeartbeatAdvanced2(packet)
            : DecodeHeartbeatAdvanced1(packet);

        return new PrinterStatus
        {
            CoverOpen = hb.LidClosed is { } closed ? !closed : null,
            PaperPresent = hb.PaperInserted,
            Battery = hb.ChargeLevel,
            Temperature = hb.Temperature,
        };
    }

    /// <summary>Read the loaded label's RFID tag (where the model supports it). Null when absent.</summary>
    public async Task<RfidInfo?> GetRfidInfoAsync(CancellationToken ct = default)
    {
        var packet = await SendAsync(PacketGenerator.RfidInfo(), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        return packet is null ? null : DecodeRfid(packet);
    }

    public async Task SetDensityAsync(int density, CancellationToken ct = default) =>
        await SendAsync(PacketGenerator.SetDensity(density), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);

    public async Task SetLabelTypeAsync(LabelType labelType, CancellationToken ct = default) =>
        await SendAsync(PacketGenerator.SetLabelType(labelType), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);

    /// <summary>
    /// Trigger the printer's built-in self-test page — a single command that exercises connect +
    /// paper feed without touching the bitmap encoder, so it is the safest first live print. Throws
    /// <see cref="PrintException"/> if the model reports the command unsupported.
    /// </summary>
    public async Task PrintTestPageAsync(CancellationToken ct = default) =>
        await SendAsync(PacketGenerator.PrintTestPage(), TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

    /// <summary>
    /// Print a 1bpp bitmap. Runs the model's print task (B1: 7-byte PrintStart + 6-byte SetPageSize),
    /// streams the RLE-encoded rows, then polls status to completion. <paramref name="progress"/> is
    /// reported during the wait.
    /// </summary>
    public async Task PrintAsync(
        MonochromeBitmap bitmap,
        PrintOptions? options = null,
        IProgress<PrintProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (Profile is not { } profile)
            throw new InvalidOperationException("Connect before printing.");

        options ??= new PrintOptions();
        var density = options.Density ?? profile.DensityDefault;
        var totalPages = Math.Max(1, options.Copies);

        if (bitmap.WidthPx > profile.PrintheadPixels)
            throw new ArgumentException(
                $"Bitmap width {bitmap.WidthPx}px exceeds the {profile.Model} printhead ({profile.PrintheadPixels}px).",
                nameof(bitmap));

        var rows = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = profile.PrintheadPixels });

        // Print init.
        await SendAsync(PacketGenerator.SetDensity(density), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        await SendAsync(PacketGenerator.SetLabelType(options.LabelType), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        await SendStartAsync(profile, totalPages, options.PageColor, ct).ConfigureAwait(false);

        // Page.
        await SendAsync(PacketGenerator.PageStart(), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        await SendPageSizeAsync(profile, bitmap, totalPages, ct).ConfigureAwait(false);

        foreach (var row in rows)
            await SendAsync(row, options.PageTimeout, ct).ConfigureAwait(false); // one-way

        await SendAsync(PacketGenerator.PageEnd(), options.PageTimeout, ct).ConfigureAwait(false);

        await WaitForPrintFinishedAsync(totalPages, options.StatusPollInterval, progress, ct).ConfigureAwait(false);

        await SendAsync(PacketGenerator.PrintEnd(), TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_pumpCts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            if (_readPump is { } pump)
            {
                try { await pump.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            cts.Dispose();
            _pumpCts = null;
            _readPump = null;
        }

        await _transport.DisconnectAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _commandLock.Dispose();
        if (_ownsTransport)
            await _transport.DisposeAsync().ConfigureAwait(false);
    }

    // --- print-task-version dispatch ---

    private Task SendStartAsync(PrinterProfile profile, int totalPages, int pageColor, CancellationToken ct) =>
        profile.PrintTaskVersion switch
        {
            PrintTaskVersion.B1 => SendAsync(PacketGenerator.PrintStart(totalPages, pageColor), TimeSpan.FromSeconds(2), ct),
            _ => SendAsync(PacketGenerator.PrintStart(), TimeSpan.FromSeconds(2), ct),
        };

    private Task SendPageSizeAsync(PrinterProfile profile, MonochromeBitmap bitmap, int copies, CancellationToken ct) =>
        profile.PrintTaskVersion switch
        {
            // B1 wants the 6-byte form; the 4-byte form misprints on the B1.
            PrintTaskVersion.B1 => SendAsync(PacketGenerator.SetPageSize(bitmap.HeightPx, bitmap.WidthPx, copies), TimeSpan.FromSeconds(1), ct),
            _ => SendAsync(PacketGenerator.SetPageSize(bitmap.HeightPx), TimeSpan.FromSeconds(1), ct),
        };

    private async Task WaitForPrintFinishedAsync(
        int totalPages, TimeSpan pollInterval, IProgress<PrintProgress>? progress, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await GetPrintStatusAsync(ct).ConfigureAwait(false);
            progress?.Report(new PrintProgress(status.Page, totalPages, status.PagePrintProgress, status.PageFeedProgress));

            if (status.Page >= totalPages && status.PagePrintProgress >= 100 && status.PageFeedProgress >= 100)
                return;

            await Task.Delay(pollInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<PrintStatus> GetPrintStatusAsync(CancellationToken ct)
    {
        var packet = await SendAsync(PacketGenerator.PrintStatus(), TimeSpan.FromSeconds(5), ct).ConfigureAwait(false)
            ?? throw new NiimbotTimeoutException("No print-status response.");

        var r = new SequentialDataReader(packet.Data);
        if (!r.CanRead(4))
            throw new NiimbotProtocolException("Print-status payload too short.");

        var page = r.ReadU16();
        var pagePrint = r.ReadU8();
        var pageFeed = r.ReadU8();

        if (packet.Data.Length == 10)
        {
            r.Skip(2);
            var error = r.ReadU8();
            if (error != 0)
                throw new PrintException($"Printer reported error code 0x{error:X2} during print.", (PrinterErrorCode)error);
        }

        return new PrintStatus(page, pagePrint, pageFeed);
    }

    // --- info / status decoding ---

    private async Task<int> ReadModelIdAsync(CancellationToken ct)
    {
        var packet = await SendAsync(PacketGenerator.GetPrinterInfo(PrinterInfoType.PrinterModelId), TimeSpan.FromSeconds(1), ct)
            .ConfigureAwait(false);

        if (packet is null || packet.Data.Length == 0)
            return 0;

        return packet.Data.Length == 1
            ? packet.Data[0] << 8
            : (packet.Data[0] << 8) | packet.Data[1];
    }

    private async Task<int> TryReadProtocolVersionAsync(CancellationToken ct)
    {
        var packet = await TrySendAsync(PacketGenerator.GetPrinterStatusData(), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        if (packet is null || packet.Data.Length <= 12)
            return 0;

        var n = packet.Data[11] * 100 + packet.Data[12];
        if (n >= 204 && n < 300) return 3;
        if (n >= 302) return 5;
        if (n == 300 || n == 301) return 4;
        return 0;
    }

    private async Task<string?> TryReadInfoStringAsync(PrinterInfoType type, CancellationToken ct)
    {
        var packet = await TrySendAsync(PacketGenerator.GetPrinterInfo(type), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        if (packet is null || packet.Data.Length == 0)
            return null;

        // Version fields are commonly a big-endian hundredths value.
        if (packet.Data.Length == 2)
            return ((packet.Data[0] * 256 + packet.Data[1]) / 100.0).ToString("0.00");

        return Convert.ToHexString(packet.Data);
    }

    private async Task<string?> TryReadSerialAsync(CancellationToken ct)
    {
        var packet = await TrySendAsync(PacketGenerator.GetPrinterInfo(PrinterInfoType.SerialNumber), TimeSpan.FromSeconds(1), ct)
            .ConfigureAwait(false);
        if (packet is null || packet.Data.Length < 4)
            return null;

        return packet.Data.Length >= 8
            ? System.Text.Encoding.UTF8.GetString(packet.Data)
            : Convert.ToHexString(packet.Data.AsSpan(0, 4));
    }

    private async Task<RfidInfo?> TryReadRfidAsync(CancellationToken ct)
    {
        var packet = await TrySendAsync(PacketGenerator.RfidInfo(), TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        return packet is null ? null : DecodeRfid(packet);
    }

    private static RfidInfo DecodeRfid(NiimbotPacket packet)
    {
        if (packet.Data.Length <= 1)
            return RfidInfo.Empty;

        var r = new SequentialDataReader(packet.Data);
        var uuid = Convert.ToHexString(r.ReadBytes(8));
        var barcode = r.ReadVString();
        var serial = r.ReadVString();
        var total = r.ReadU16();
        var used = r.ReadU16();
        var type = (LabelType)r.ReadU8();

        return new RfidInfo
        {
            TagPresent = true,
            Uuid = uuid,
            Barcode = barcode,
            SerialNumber = serial,
            TotalLabels = total,
            UsedLabels = used,
            ConsumablesType = type,
        };
    }

    private HeartbeatData DecodeHeartbeatAdvanced1(NiimbotPacket packet)
    {
        var len = packet.Data.Length;
        var r = new SequentialDataReader(packet.Data);
        bool? lid = null, paper = null, rfid = null;
        BatteryChargeLevel? charge = null;

        switch (len)
        {
            case 10: // D110
                r.Skip(8);
                lid = r.ReadU8() == 0;
                charge = (BatteryChargeLevel)r.ReadU8();
                break;
            case 13: // B1
                r.Skip(9);
                lid = r.ReadU8() == 0;
                charge = (BatteryChargeLevel)r.ReadU8();
                paper = r.ReadU8() == 0;
                rfid = r.ReadU8() != 0;
                break;
            case 19:
                r.Skip(15);
                lid = r.ReadU8() == 0;
                charge = (BatteryChargeLevel)r.ReadU8();
                paper = r.ReadU8() == 0;
                rfid = r.ReadU8() != 0;
                break;
            case 20:
                r.Skip(18);
                paper = r.ReadU8() == 0;
                rfid = r.ReadU8() != 0;
                break;
            default:
                throw new NiimbotProtocolException($"Unexpected heartbeat length {len}.");
        }

        if (lid is { } closed && Capabilities is { } caps && InvertedLidModels.Contains(caps.ModelId))
            lid = !closed;

        return new HeartbeatData { LidClosed = lid, ChargeLevel = charge, PaperInserted = paper, PaperRfidSuccess = rfid };
    }

    private static HeartbeatData DecodeHeartbeatAdvanced2(NiimbotPacket packet)
    {
        var r = new SequentialDataReader(packet.Data);
        if (!r.CanRead(9))
            throw new NiimbotProtocolException("Advanced2 heartbeat too short.");

        r.Skip(2);
        var charge = (BatteryChargeLevel)r.ReadU8();
        var temp = r.ReadU8();
        var lid = r.ReadU8() == 0;
        var paper = r.ReadU8() == 0;
        var paperRfid = r.ReadU8() != 0;
        var ribbonRfid = r.ReadU8() != 0;
        var ribbon = r.ReadU8() == 0;

        return new HeartbeatData
        {
            ChargeLevel = charge,
            Temperature = temp,
            LidClosed = lid,
            PaperInserted = paper,
            PaperRfidSuccess = paperRfid,
            RibbonRfidSuccess = ribbonRfid,
            RibbonInserted = ribbon,
        };
    }

    // --- command queue + read pump ---

    /// <summary>
    /// Send a packet and, unless it is one-way, await the correlated response within
    /// <paramref name="timeout"/>. Returns null for one-way packets. Throws
    /// <see cref="NiimbotTimeoutException"/> when a response is expected but none arrives.
    /// </summary>
    internal async Task<NiimbotPacket?> SendAsync(NiimbotPacket packet, TimeSpan timeout, CancellationToken ct)
    {
        await _commandLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (packet.OneWay)
            {
                await _transport.WriteAsync(packet.ToBytes(), ct).ConfigureAwait(false);
                return null;
            }

            var pending = new PendingRequest(packet.ValidResponseIds);
            lock (_pendingLock)
                _pending = pending;

            await _transport.WriteAsync(packet.ToBytes(), ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            try
            {
                return await pending.Tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new NiimbotTimeoutException(
                    $"No response to {(RequestCommandId)packet.Command} within {timeout.TotalMilliseconds:0} ms.");
            }
            finally
            {
                lock (_pendingLock)
                    _pending = null;
            }
        }
        finally
        {
            _commandLock.Release();
        }
    }

    /// <summary>Like <see cref="SendAsync"/> but swallows timeouts, returning null (optional queries).</summary>
    private async Task<NiimbotPacket?> TrySendAsync(NiimbotPacket packet, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            return await SendAsync(packet, timeout, ct).ConfigureAwait(false);
        }
        catch (NiimbotTimeoutException)
        {
            return null;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await _transport.ReadAsync(buffer, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (read <= 0)
                    continue;

                _accumulator.Append(buffer.AsSpan(0, read));

                while (_accumulator.TryRead() is { } packet)
                    Dispatch(packet);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void Dispatch(NiimbotPacket packet)
    {
        PendingRequest? pending;
        lock (_pendingLock)
            pending = _pending;

        if (pending is not null && pending.Matches(packet))
        {
            lock (_pendingLock)
                _pending = null;
            pending.Complete(packet);
            return;
        }

        PacketReceived?.Invoke(this, packet);
    }

    private sealed class PendingRequest(ResponseCommandId[] expected)
    {
        public TaskCompletionSource<NiimbotPacket> Tcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Matches(NiimbotPacket packet)
        {
            var cmd = (ResponseCommandId)packet.Command;
            if (cmd is ResponseCommandId.In_PrintError or ResponseCommandId.In_NotSupported)
                return true;
            return Array.IndexOf(expected, cmd) >= 0;
        }

        public void Complete(NiimbotPacket packet)
        {
            var cmd = (ResponseCommandId)packet.Command;
            if (cmd == ResponseCommandId.In_NotSupported)
                Tcs.TrySetException(new PrintException("Printer reported the command is not supported."));
            else if (cmd == ResponseCommandId.In_PrintError)
                Tcs.TrySetException(new PrintException(
                    packet.Data.Length > 0
                        ? $"Printer reported error code 0x{packet.Data[0]:X2}."
                        : "Printer reported a print error.",
                    packet.Data.Length > 0 ? (PrinterErrorCode)packet.Data[0] : null));
            else
                Tcs.TrySetResult(packet);
        }
    }
}
