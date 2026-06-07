using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Thermalith.App.Views;

/// <summary>Draws a dot grid over the canvas at a given device-pixel spacing (build spec §7 grid/snap).</summary>
public sealed class GridOverlay : Control
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<GridOverlay, double>(nameof(Spacing), 16);

    static GridOverlay()
    {
        AffectsRender<GridOverlay>(SpacingProperty);
    }

    public GridOverlay() => IsHitTestVisible = false;

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var sp = Spacing;
        if (sp < 4 || !IsVisible) return; // too dense to be useful

        var dot = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        for (var x = 0.0; x <= Bounds.Width; x += sp)
            for (var y = 0.0; y <= Bounds.Height; y += sp)
                context.FillRectangle(dot, new Rect(x - 0.5, y - 0.5, 1, 1));
    }
}
