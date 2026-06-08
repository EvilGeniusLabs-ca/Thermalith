using Avalonia.Media;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>Enum value lists backing the inspector dropdowns (mirrors the schema, label-json-spec §11).</summary>
public static class EditorOptions
{
    /// <summary>Sentinel for "use the app's bundled font" — maps to a null FontFamily on the element.</summary>
    public const string BundledFont = "(bundled)";

    private static string[]? _fonts;

    /// <summary>Bundled sentinel followed by installed system font families (cross-platform via Avalonia's
    /// <see cref="FontManager"/>). Built once on first access, after Avalonia is initialised.</summary>
    public static string[] Fonts => _fonts ??= BuildFonts();

    private static string[] BuildFonts()
    {
        try
        {
            var system = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            return new[] { BundledFont }.Concat(system).ToArray();
        }
        catch
        {
            return [BundledFont]; // headless / FontManager unavailable
        }
    }

    public static string[] JustifyH { get; } = ["left", "center", "right", "justify"];
    public static string[] JustifyV { get; } = ["top", "middle", "bottom"];
    public static string[] Wrap { get; } = ["none", "word"];
    public static string[] FontSizing { get; } = ["fixed", "shrink", "fill"];
    public static string[] Symbology { get; } = ["code128", "code39", "ean13", "ean8", "upca", "upce", "itf", "codabar"];
    public static string[] TextPosition { get; } = ["above", "below", "none"];
    public static string[] QrEncoding { get; } = ["text", "hex"];
    public static string[] EcLevel { get; } = ["L", "M", "Q", "H"];
    public static string[] ShapeType { get; } = ["rect", "roundedRect", "ellipse", "line"];
    public static string[] Fill { get; } = ["none", "solid"];
    public static string[] Fit { get; } = ["fill", "fit", "stretch", "center"];
    public static string[] Dither { get; } = ["threshold", "floydSteinberg", "atkinson", "ordered", "none"];
    public static string[] DateKind { get; } = ["date", "time", "datetime"];
    public static string[] DateSource { get; } = ["printNow", "fixed"];
    public static string[] CanvasShape { get; } = ["rectangle", "rounded", "circle", "dieCut"];
    public static int[] Orientation { get; } = [0, 90, 180, 270];
    public static string[] PaperType { get; } = ["gap", "black", "continuous", "transparent", "perforated"];
}
