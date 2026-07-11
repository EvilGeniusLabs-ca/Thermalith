using Thermalith.Core.Catalog;

namespace Thermalith.App.Services;

/// <summary>App-level prompts the view-model can't show itself (kept UI-free, §4.1). Implemented in the window.</summary>
public interface IDialogService
{
    /// <summary>Ask whether to discard unsaved changes. Returns true to proceed (discard), false to cancel.</summary>
    Task<bool> ConfirmDiscardAsync();

    /// <summary>Show the roll/label definition dialog seeded with <paramref name="seed"/>. Returns the roll, or null on cancel.</summary>
    Task<RollDefinition?> DefineRollAsync(RollDefinition seed, string title);

    /// <summary>Show the Help → About dialog (identity, version, donate link).</summary>
    Task ShowAboutAsync();

    /// <summary>Generic confirm with a custom message + primary-button label. Returns true to proceed.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>Show an informational message with a single dismiss button.</summary>
    Task MessageAsync(string title, string message);

    /// <summary>Open a modeless progress window for a data-merge run (GitHub #7), with a live count and a
    /// Cancel button. The caller reports progress, reads <see cref="IMergeProgress.IsCancelled"/> between
    /// rows, and closes it when done.</summary>
    IMergeProgress BeginMergeProgress(string title, int total);
}

/// <summary>A live batch-print progress window (GitHub #7): shows "Printing k / total" and a Cancel button.</summary>
public interface IMergeProgress
{
    /// <summary>True once the user clicks Cancel — the print loop stops after the current label finishes.</summary>
    bool IsCancelled { get; }

    /// <summary>Update the printed-rows count shown to the user.</summary>
    void Report(int done);

    /// <summary>Close the progress window.</summary>
    void Close();
}
