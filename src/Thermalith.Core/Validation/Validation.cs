using Thermalith.Core.Fonts;
using Thermalith.Core.Model;

namespace Thermalith.Core.Validation;

public enum ValidationSeverity { Info, Warning, Error }

/// <summary>A single coded diagnostic (build spec §6.7): stable <see cref="Code"/>, severity, message, and JSON path.</summary>
public sealed record ValidationDiagnostic(
    string Code,
    ValidationSeverity Severity,
    string Message,
    string JsonPath,
    int? Line = null);

/// <summary>The outcome of validating a document. <b>Errors block print; warnings do not</b> (§6.7).</summary>
public sealed record ValidationResult(IReadOnlyList<ValidationDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == ValidationSeverity.Error);
    public IEnumerable<ValidationDiagnostic> Errors => Diagnostics.Where(d => d.Severity == ValidationSeverity.Error);
    public IEnumerable<ValidationDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == ValidationSeverity.Warning);

    public static readonly ValidationResult Ok = new([]);
}

/// <summary>Optional inputs that let the validator check resolution, assets and fonts against a concrete print context.</summary>
public sealed record ValidationContext
{
    public IReadOnlyDictionary<string, object?>? Data { get; init; }
    public IReadOnlyDictionary<string, byte[]>? Assets { get; init; }
    public FontService? Fonts { get; init; }
}

/// <summary>Validates a label document, returning coded diagnostics surfaced in the editor and via the API/MCP (§6.7).</summary>
public interface ILabelValidator
{
    ValidationResult Validate(LabelDocument document, ValidationContext? context = null);
}

/// <summary>Stable diagnostic codes.</summary>
public static class ValidationCodes
{
    public const string DuplicateId = "duplicate-id";
    public const string UndeclaredToken = "undeclared-token";
    public const string RequiredTokenUnresolved = "required-token-unresolved";
    public const string BarcodeModuleTooSmall = "barcode-module-too-small";
    public const string BarcodeInvalidValue = "barcode-invalid-value";
    public const string QrModuleTooSmall = "qr-module-too-small";
    public const string QrInvalidValue = "qr-invalid-value";
    public const string TextOverflow = "text-overflow";
    public const string ElementOutsideCanvas = "element-outside-canvas";
    public const string ElementOutsideSafeArea = "element-outside-safe-area";
    public const string MissingAsset = "missing-asset";
    public const string MissingFont = "missing-font";
}
