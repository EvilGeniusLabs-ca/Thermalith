namespace Thermalith.Core.Rendering;

/// <summary>Knobs for the render path (kept tiny so the path stays pure given these + the resolved label).</summary>
public sealed record RenderOptions
{
    /// <summary>Luminance cutoff for the stage-4 threshold safety net: a pixel darker than this burns (§6.3.3).</summary>
    public int TextThreshold { get; init; } = 128;

    public static readonly RenderOptions Default = new();
}
