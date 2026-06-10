namespace Thermalith.Core.Rendering;

/// <summary>Table cell geometry in mm — column/row sizes and edge positions — so the renderer and the
/// editor (hit-testing, in-place edit overlay) agree on where each cell sits.</summary>
public static class TableMetrics
{
    /// <summary>Per-axis sizes (mm): the explicit widths/heights when supplied and the count matches,
    /// otherwise an even split of <paramref name="totalMm"/> across <paramref name="count"/> tracks.</summary>
    public static double[] AxisMm(double[]? explicitMm, int count, double totalMm)
    {
        count = Math.Max(0, count);
        var sizes = new double[count];
        if (explicitMm is not null && explicitMm.Length == count)
            Array.Copy(explicitMm, sizes, count);
        else if (count > 0)
        {
            var each = totalMm / count;
            for (var i = 0; i < count; i++) sizes[i] = each;
        }
        return sizes;
    }

    /// <summary>Edge offsets (mm), length <c>sizes.Length + 1</c>: <c>edges[i]</c> = sum of sizes[0..i-1],
    /// so a track spanning columns c..c+span occupies <c>[edges[c], edges[c+span]]</c>.</summary>
    public static double[] Edges(double[] sizes)
    {
        var edges = new double[sizes.Length + 1];
        for (var i = 0; i < sizes.Length; i++) edges[i + 1] = edges[i] + sizes[i];
        return edges;
    }
}
