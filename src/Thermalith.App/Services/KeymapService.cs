using Avalonia.Input;

namespace Thermalith.App.Services;

/// <summary>The logical, platform-neutral editor actions that carry an accelerator (build spec §7.2).</summary>
public enum EditorAction
{
    New, Open, Save, SaveAs,
    Undo, Redo, Delete,
    ZoomIn, ZoomOut, ZoomFit,
    Quit,
}

/// <summary>
/// Central keymap (§7.2): maps logical actions to <see cref="KeyGesture"/>s bound to the platform
/// command key, so the same accelerator resolves to ⌘ on macOS and Ctrl on Windows/Linux. Built
/// once from <c>TopLevel.PlatformSettings.HotkeyConfiguration.CommandModifiers</c>.
/// </summary>
public sealed class KeymapService
{
    private readonly KeyModifiers _cmd;
    private readonly bool _isMac;

    public KeymapService(KeyModifiers commandModifiers)
    {
        _cmd = commandModifiers == KeyModifiers.None ? KeyModifiers.Control : commandModifiers;
        _isMac = OperatingSystem.IsMacOS();
    }

    public KeyGesture Gesture(EditorAction action) => action switch
    {
        EditorAction.New => new KeyGesture(Key.N, _cmd),
        EditorAction.Open => new KeyGesture(Key.O, _cmd),
        EditorAction.Save => new KeyGesture(Key.S, _cmd),
        EditorAction.SaveAs => new KeyGesture(Key.S, _cmd | KeyModifiers.Shift),
        EditorAction.Undo => new KeyGesture(Key.Z, _cmd),
        EditorAction.Redo => _isMac ? new KeyGesture(Key.Z, _cmd | KeyModifiers.Shift) : new KeyGesture(Key.Y, _cmd),
        EditorAction.Delete => new KeyGesture(Key.Delete),
        EditorAction.ZoomIn => new KeyGesture(Key.OemPlus, _cmd),
        EditorAction.ZoomOut => new KeyGesture(Key.OemMinus, _cmd),
        EditorAction.ZoomFit => new KeyGesture(Key.D0, _cmd),
        EditorAction.Quit => _isMac ? new KeyGesture(Key.Q, _cmd) : new KeyGesture(Key.F4, KeyModifiers.Alt),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    public string Display(EditorAction action) => Gesture(action).ToString();
}
