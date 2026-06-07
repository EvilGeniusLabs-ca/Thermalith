using Niimbot.Net.Commands;

namespace Niimbot.Net;

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
