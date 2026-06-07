namespace Thermalith.App.Services;

/// <summary>
/// Abstracts the platform file dialogs so the view-model stays UI-free (§4.1). Implemented in the
/// window code-behind over Avalonia's <c>StorageProvider</c>. Returns local filesystem paths.
/// </summary>
public interface IFilePicker
{
    Task<string?> OpenLabelAsync();
    Task<string?> SaveLabelAsync(string? suggestedName);
}
