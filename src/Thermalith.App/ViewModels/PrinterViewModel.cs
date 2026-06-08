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
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshStatusCommand))]
    private bool _isConnected;

    [ObservableProperty] private string _connectionInfo = "Not connected.";
    [ObservableProperty] private string _statusInfo = "";

    /// <summary>Concise loaded-roll summary for the main interface, e.g. "B1 · 59/96 labels".</summary>
    [ObservableProperty] private string _loadedRollText = "No printer";

    /// <summary>The RFID of the currently-loaded roll (set on connect/refresh), or null. Used by the shell to resolve/prompt.</summary>
    public RfidInfo? LoadedRfid { get; private set; }

    /// <summary>DPI of the connected printer, or null.</summary>
    public int? ConnectedDpi => _caps?.Dpi;

    /// <summary>Raised after a connect/refresh so the shell can look up or prompt for the loaded roll.</summary>
    public event EventHandler? RollDetected;
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
            {
                Message = $"Label is {mono.WidthPx}px wide — exceeds the {caps.Model} printhead ({caps.PrintheadPixels}px). Reduce canvas width.";
                return;
            }

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
