using System.Text.RegularExpressions;

namespace Thermalith.Core.Catalog;

/// <summary>
/// Parses dimensions out of a NIIMBOT part name / box label, e.g. <c>T40*20-320WHITE</c> → 40×20 mm
/// (worklist §A). The RFID itself doesn't carry the size, but the part name the user reads off the
/// box almost always encodes <c>W*H</c> — so we can pre-fill the roll form. Best-effort: returns null
/// when no <c>W*H</c> pattern is present.
/// </summary>
public static partial class RollNaming
{
    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*[*xX×]\s*(\d+(?:\.\d+)?)")]
    private static partial Regex SizePattern();

    /// <summary>Extract (widthMm, heightMm) from a part name, or null if none is found.</summary>
    public static (double WidthMm, double HeightMm)? ParseSize(string? partName)
    {
        if (string.IsNullOrWhiteSpace(partName)) return null;
        var m = SizePattern().Match(partName);
        if (!m.Success) return null;
        if (double.TryParse(m.Groups[1].Value, out var w) && double.TryParse(m.Groups[2].Value, out var h) && w > 0 && h > 0)
            return (w, h);
        return null;
    }
}
