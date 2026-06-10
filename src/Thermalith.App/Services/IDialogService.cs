using Thermalith.Core.Catalog;

namespace Thermalith.App.Services;

/// <summary>App-level prompts the view-model can't show itself (kept UI-free, §4.1). Implemented in the window.</summary>
public interface IDialogService
{
    /// <summary>Ask whether to discard unsaved changes. Returns true to proceed (discard), false to cancel.</summary>
    Task<bool> ConfirmDiscardAsync();

    /// <summary>Show the roll/label definition dialog seeded with <paramref name="seed"/>. Returns the roll, or null on cancel.</summary>
    Task<RollDefinition?> DefineRollAsync(RollDefinition seed, string title);
}
