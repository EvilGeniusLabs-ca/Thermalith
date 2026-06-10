using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Niimbot.Net;
using Niimbot.Net.Capabilities;
using Niimbot.Net.Commands;
using Niimbot.Net.Transport;

namespace Thermalith.App.ViewModels;

/// <summary>A discovered serial port and whether a NIIMBOT answered a probe on it (build spec §5.1).</summary>
public sealed record PortItem(string Port, string Label, bool IsNiimbot);

/// <summary>
/// The printer panel (build spec §7): discover/probe serial ports, connect, show paper/cover/battery
/// status, set density/copies/label-type + a calibration offset, and print the Core-rendered raster
/// over <c>Niimbot.Net</c>. Connection lifetime is owned here; the client is disposed on disconnect.
/// </summary>
public sealed partial class PrinterViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private NiimbotClient? _client;
    private PrinterCapabilities? _caps;

    public PrinterViewModel(EditorViewModel editor) => _editor = editor;

    public ObservableCollection<PortItem> Ports { get; } = [];
    public ObservableCollection<LabelType> LabelTypes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private PortItem? _selectedPort;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshPortsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeLabelsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeLabelsCommand))]
    private bool _isConnected;

    [ObservableProperty] private string _connectionInfo = "Not connected.";
    [ObservableProperty] private string _statusInfo = "";

    /// <summary>Concise loaded-roll summary for the main interface, e.g. "B1 · 59/96 labels".</summary>
    [ObservableProperty] private string _loadedRollText = "No printer";

    /// <summary>The RFID of the currently-loaded roll (set on connect/refresh), or null. Used by the shell to resolve/prompt.</summary>
    public RfidInfo? LoadedRfid { get; private set; }

    /// <summary>DPI of the connected printer, or null.</summary>
    public int? ConnectedDpi => _caps?.Dpi;

    /// <summary>
    /// The connected printer's printable width in mm — the printhead pixel limit converted to mm and
    /// rounded down to 0.1 mm so a canvas at this width never exceeds the printhead (worklist §A6).
    /// Null when disconnected. The B1 reports 384 px @ 203 dpi → 48.0 mm (its 50 mm stock has ~1 mm
    /// unprintable margins each side).
    /// </summary>
    public double? ConnectedPrintableWidthMm => _caps is { Dpi: > 0, PrintheadPixels: > 0 } c
        ? Math.Floor(c.PrintheadPixels / (c.Dpi / 25.4) * 10) / 10
        : null;

    /// <summary>Raised after a connect/refresh so the shell can look up or prompt for the loaded roll.</summary>
    public event EventHandler? RollDetected;

    /// <summary>Raised after a successful connect so the shell can persist the last printer (port + model).</summary>
    public event EventHandler? Connected;

    /// <summary>The port we connected on / the connected model — for persistence + startup reconnect.</summary>
    public string? ConnectedPort { get; private set; }
    public string? ConnectedModel => _caps?.Model.ToString();
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private LabelType _selectedLabelType = LabelType.WithGaps;
    [ObservableProperty] private int _density = 3;
    [ObservableProperty] private int _densityMin = 1;
    [ObservableProperty] private int _densityMax = 5;
    [ObservableProperty] private int _copies = 1;
    [ObservableProperty] private int _offsetX;
    [ObservableProperty] private int _offsetY;

    // ── Discovery ────────────────────────────────────────────────────────────────────────────

    // Greys while any operation is in flight (the busy cursor + spinner give the visible feedback).
    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task RefreshPortsAsync()
    {
        IsBusy = true;
        Message = "Scanning ports…";
        Ports.Clear();
        try
        {
            var found = 0;
            foreach (var info in SerialPortEnumerator.Enumerate())
            {
                Message = $"Probing {info.PortName}…";
                var probe = await PrinterProbe.ProbeAsync(info.PortName);
                if (probe is not null) found++;
                Ports.Add(new PortItem(
                    info.PortName,
                    probe is null ? $"{info.PortName} — no printer" : $"{info.PortName} — {probe.Model}",
                    probe is not null));
            }
            SelectedPort = Ports.FirstOrDefault(p => p.IsNiimbot) ?? Ports.FirstOrDefault();
            Message = Ports.Count == 0
                ? "No serial ports found."
                : found > 0
                    ? $"Found {found} printer(s) across {Ports.Count} port(s)."
                    : $"Scanned {Ports.Count} port(s); no NIIMBOT found.";
        }
        catch (Exception ex)
        {
            Message = "Scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Connection ───────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedPort is null) return;
        IsBusy = true;
        Message = $"Connecting to {SelectedPort.Port}…";
        try
        {
            _client = NiimbotClient.FromSerialPort(SelectedPort.Port);
            _caps = await _client.ConnectAsync();
            IsConnected = true;

            DensityMin = _caps.DensityMin;
            DensityMax = _caps.DensityMax;
            Density = _caps.DensityDefault;
            LabelTypes.Clear();
            foreach (var t in _caps.SupportedLabelTypes) LabelTypes.Add(t);
            SelectedLabelType = LabelTypes.Count > 0 ? LabelTypes[0] : LabelType.WithGaps;

            ConnectionInfo = $"{_caps.Model} · {_caps.Dpi} dpi · head {_caps.PrintheadPixels}px"
                + (_caps.FirmwareVersion is { Length: > 0 } fw ? $" · fw {fw}" : "");
            Message = "Connected.";
            ConnectedPort = SelectedPort.Port;
            Connected?.Invoke(this, EventArgs.Empty); // shell persists this printer for startup reconnect
            await RefreshStatusCoreAsync();
            RollDetected?.Invoke(this, EventArgs.Empty); // let the shell resolve/prompt the loaded roll
        }
        catch (Exception ex)
        {
            Message = "Connect failed: " + ex.Message;
            await SafeDisposeAsync();
            IsConnected = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Startup background scan + reconnect: pre-fills the port list, then connects if the last
    /// printer is found (same port, or the single NIIMBOT matching its model). Otherwise just pre-selects.
    /// Runs off the UI thread (fire-and-forget) so the user can keep working while it scans.</summary>
    public async Task AutoConnectAsync(string? lastPort, string? lastModel)
    {
        if (IsConnected || IsBusy) return;
        await RefreshPortsAsync();
        if (IsConnected || IsBusy) return; // user connected meanwhile
        var target = Ports.FirstOrDefault(p => p.IsNiimbot && p.Port == lastPort)
            ?? (lastModel is { Length: > 0 } && Ports.Count(p => p.IsNiimbot && p.Label.Contains(lastModel)) == 1
                ? Ports.First(p => p.IsNiimbot && p.Label.Contains(lastModel))
                : Ports.Count(p => p.IsNiimbot) == 1 ? Ports.First(p => p.IsNiimbot) : null);
        if (target is null) return; // ambiguous or none — the list is pre-filled for a manual Connect
        SelectedPort = target;
        await ConnectAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            if (_client is not null) await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Message = "Disconnect: " + ex.Message;
        }
        finally
        {
            await SafeDisposeAsync();
            IsConnected = false;
            ConnectionInfo = "Not connected.";
            StatusInfo = "";
            LoadedRollText = "No printer";
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RefreshStatusAsync()
    {
        IsBusy = true;
        try { await RefreshStatusCoreAsync(); }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Re-read the loaded roll after the user swaps labels (the protocol gives no change event, so this is
    /// explicit). Re-reads RFID, then raises <see cref="RollDetected"/> so the shell resolves/prompts as on connect.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ChangeLabelsAsync()
    {
        IsBusy = true;
        Message = "Reading new roll…";
        try
        {
            await RefreshStatusCoreAsync();
            RollDetected?.Invoke(this, EventArgs.Empty);
            Message = "Roll updated.";
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshStatusCoreAsync()
    {
        if (_client is null) return;
        try
        {
            var s = await _client.GetStatusAsync();
            StatusInfo = string.Join("   ",
                $"cover: {Describe(s.CoverOpen, "open", "closed")}",
                $"paper: {Describe(s.PaperPresent, "present", "out")}",
                $"battery: {(s.Battery is { } b ? b.ToString() : "—")}",
                s.Temperature is { } t ? $"temp: {t}°C" : "");

            // Refresh the loaded-roll label count (consumed labels change as you print).
            try { UpdateLoadedRoll(await _client.GetRfidInfoAsync()); }
            catch { UpdateLoadedRoll(null); }
        }
        catch (Exception ex)
        {
            StatusInfo = "status unavailable: " + ex.Message;
        }
    }

    /// <summary>Set the selected print label-type from a roll's paper-type string (drives SetLabelType at print).</summary>
    public void ApplyPaperType(string? paperType)
    {
        SelectedLabelType = paperType?.ToLowerInvariant() switch
        {
            "black" => LabelType.Black,
            "continuous" => LabelType.Continuous,
            "transparent" => LabelType.Transparent,
            _ => LabelType.WithGaps,
        };
    }

    private void UpdateLoadedRoll(RfidInfo? rfid)
    {
        LoadedRfid = rfid;
        if (!IsConnected || _caps is null)
        {
            LoadedRollText = "No printer";
            return;
        }
        var model = _caps.Model.ToString();
        if (rfid is { TagPresent: true } r && r.TotalLabels > 0)
        {
            var used = r.UsedLabels < 0 ? 0 : r.UsedLabels;
            LoadedRollText = $"{model} · {r.TotalLabels - used}/{r.TotalLabels} labels";
        }
        else
        {
            LoadedRollText = $"{model} connected";
        }
    }

    // ── Print ────────────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task PrintAsync()
    {
        if (_client is null || !IsConnected) return;
        IsBusy = true;
        Message = "Rendering…";
        try
        {
            var mono = _editor.RenderForPrint();
            if (_caps is { } caps && mono.WidthPx > caps.PrintheadPixels)
                // Canvas wider than the printhead → crop the centred printable strip (the ~1mm/side the
                // head can't reach); content there is mechanically unprintable (crop-don't-block, §F).
                mono = CropCentered(mono, caps.PrintheadPixels);

            var options = new PrintOptions
            {
                Density = Density,
                Copies = Math.Max(1, Copies),
                LabelType = SelectedLabelType,
                HorizontalAlign = PrintAlignment.Center,
                OffsetXPx = OffsetX,
                OffsetYPx = OffsetY,
            };
            var progress = new Progress<PrintProgress>(p =>
                Message = $"Printing {p.Page}/{p.TotalPages} — {p.PagePrintPercent}%");

            await _client.PrintAsync(mono, options, progress);
            Message = "Printed.";
            await RefreshStatusCoreAsync();
        }
        catch (Exception ex)
        {
            Message = "Print failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Crop a 1bpp raster to a centred target width (drop the equal margins each side).</summary>
    private static Niimbot.Net.Encoding.MonochromeBitmap CropCentered(Niimbot.Net.Encoding.MonochromeBitmap src, int targetWidthPx)
    {
        if (targetWidthPx >= src.WidthPx) return src;
        var h = src.HeightPx;
        var x0 = (src.WidthPx - targetWidthPx) / 2;
        var bpr = (targetWidthPx + 7) / 8;
        var packed = new byte[bpr * h];
        for (var y = 0; y < h; y++)
        {
            var row = src.Row(y);
            for (var x = 0; x < targetWidthPx; x++)
            {
                var sx = x0 + x;
                if ((row[sx >> 3] & (0x80 >> (sx & 7))) != 0)
                    packed[y * bpr + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            }
        }
        return new Niimbot.Net.Encoding.MonochromeBitmap(targetWidthPx, h, packed);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────────

    /// <summary>Best-effort teardown on window close (fire-and-forget; the OS reclaims the port regardless).</summary>
    public void Shutdown() => _ = SafeDisposeAsync();

    private async Task SafeDisposeAsync()
    {
        if (_client is null) return;
        try { await _client.DisposeAsync(); }
        catch { /* already gone */ }
        _client = null;
    }

    // ── CanExecute ───────────────────────────────────────────────────────────────────────────

    private bool NotBusy() => !IsBusy;
    private bool CanConnect() => !IsBusy && !IsConnected && SelectedPort is not null;
    private bool CanOperate() => !IsBusy && IsConnected;

    private static string Describe(bool? value, string whenTrue, string whenFalse) =>
        value is null ? "—" : value.Value ? whenTrue : whenFalse;
}
