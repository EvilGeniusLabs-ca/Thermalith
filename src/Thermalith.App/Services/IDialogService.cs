using Thermalith.Core.Catalog;

namespace Thermalith.App.Services;

/// <summary>
/// The outcome of the new/define-roll dialog: the roll plus the chosen <i>design-target</i> printer
/// geometry (printable width + dpi → the print-crop guide). The target fields are null when no target
/// was chosen (offline with no catalog).
/// </summary>
public sealed record NewLabelChoice(RollDefinition Roll, double? TargetPrintableWidthMm, int? TargetDpi);

/// <summary>App-level prompts the view-model can't show itself (kept UI-free, §4.1). Implemented in the window.</summary>
public interface IDialogService
{
    /// <summary>Ask whether to discard unsaved changes. Returns true to proceed (discard), false to cancel.</summary>
    Task<bool> ConfirmDiscardAsync();

    /// <summary>
    /// Show the roll/label definition dialog seeded with <paramref name="seed"/>. <paramref name="printers"/>
    /// populates the design-target dropdown (catalog models, incl. ones not physically connected) and
    /// <paramref name="defaultTarget"/> pre-selects it. Returns the roll + chosen target, or null on cancel.
    /// </summary>
    Task<NewLabelChoice?> DefineRollAsync(RollDefinition seed, string title, IReadOnlyList<PrinterEntry> printers, PrinterEntry? defaultTarget);
}
