using Niimbot.Net.Commands;

namespace Niimbot.Net;

/// <summary>Horizontal placement of a label-width raster within the full printhead.</summary>
public enum PrintAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>Options for a print job. Defaults match the B1's sensible starting point.</summary>
public sealed record PrintOptions
{
    /// <summary>Label stock type. Defaults to gap-separated die-cut labels.</summary>
    public LabelType LabelType { get; init; } = LabelType.WithGaps;

    /// <summary>Print density / darkness. When null, the profile's default is used.</summary>
    public int? Density { get; init; }

    /// <summary>Number of copies of the bitmap to print.</summary>
    public int Copies { get; init; } = 1;

    /// <summary>Multicolor-paper print color (0 = default/black). B1-only.</summary>
    public int PageColor { get; init; } = 0;

    /// <summary>How often to poll print status while waiting for completion.</summary>
    public TimeSpan StatusPollInterval { get; init; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Overall timeout for a single page to finish printing.</summary>
    public TimeSpan PageTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Use the compact indexed-row packet for rows with ≤6 black pixels. Defaults to true; set
    /// false to force every row through the dense bitmap-row packet (a diagnostic for the
    /// less-verified indexed path).
    /// </summary>
    public bool UseIndexedRows { get; init; } = true;

    /// <summary>
    /// Horizontal placement of a label-width raster within the printhead. The protocol has no
    /// horizontal-offset command — every row is laid down from printhead pixel 0 — so the client
    /// pads the bitmap to the full head width and positions the content. Defaults to
    /// <see cref="PrintAlignment.Center"/>. See build spec §5 and the NIIMBOT protocol notes.
    /// </summary>
    public PrintAlignment HorizontalAlign { get; init; } = PrintAlignment.Center;

    /// <summary>Calibration nudge in pixels added to the aligned X position (+ moves right).</summary>
    public int OffsetXPx { get; init; } = 0;

    /// <summary>Calibration nudge in pixels prepended as blank feed rows (+ moves content down).</summary>
    public int OffsetYPx { get; init; } = 0;
}

/// <summary>Progress for an in-flight print, reported via <c>IProgress</c>.</summary>
public readonly record struct PrintProgress(int Page, int TotalPages, int PagePrintPercent, int PageFeedPercent);

/// <summary>
/// Friendly, model-agnostic readiness snapshot derived from a heartbeat. Null fields mean the
/// device did not report that signal.
/// </summary>
public readonly record struct PrinterStatus
{
    public bool? CoverOpen { get; init; }
    public bool? PaperPresent { get; init; }
    public BatteryChargeLevel? Battery { get; init; }
    public int? Temperature { get; init; }
}
