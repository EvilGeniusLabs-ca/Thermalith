using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Thermalith.App.Views;

/// <summary>
/// Draws the printable-area boundary as four full-span dashed lines across the canvas viewport (the two
/// vertical lines mark the left/right print edges, the two horizontal lines the top/bottom), instead of
/// a small box hugging the label. <see cref="OriginX"/>/<see cref="OriginY"/> = the label's top-left in
/// this overlay's pixel space (kept in sync with scroll/zoom by the view, same as the rulers);
/// <see cref="PixelsPerMm"/> = dpi/25.4 × zoom. Lines sit <see cref="InsetMm"/> in from each label edge.
/// </summary>
public sealed class PrintGuideOverlay : Control
{
    public static readonly StyledProperty<double> OriginXProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(OriginX));
    public static readonly StyledProperty<double> OriginYProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(OriginY));
    public static readonly StyledProperty<double> PixelsPerMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(PixelsPerMm), 1);
    public static readonly StyledProperty<double> InsetMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(InsetMm));
    public static readonly StyledProperty<double> LabelWidthMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(LabelWidthMm));
    public static readonly StyledProperty<double> LabelHeightMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(LabelHeightMm));

    static PrintGuideOverlay() => AffectsRender<PrintGuideOverlay>(
        OriginXProperty, OriginYProperty, PixelsPerMmProperty, InsetMmProperty, LabelWidthMmProperty, LabelHeightMmProperty);

    public PrintGuideOverlay() => IsHitTestVisible = false;

    public double OriginX { get => GetValue(OriginXProperty); set => SetValue(OriginXProperty, value); }
    public double OriginY { get => GetValue(OriginYProperty); set => SetValue(OriginYProperty, value); }
    public double PixelsPerMm { get => GetValue(PixelsPerMmProperty); set => SetValue(PixelsPerMmProperty, value); }
    public double InsetMm { get => GetValue(InsetMmProperty); set => SetValue(InsetMmProperty, value); }
    public double LabelWidthMm { get => GetValue(LabelWidthMmProperty); set => SetValue(LabelWidthMmProperty, value); }
    public double LabelHeightMm { get => GetValue(LabelHeightMmProperty); set => SetValue(LabelHeightMmProperty, value); }

    public override void Render(DrawingContext context)
    {
        var s = PixelsPerMm;
        if (s <= 0 || InsetMm <= 0 || LabelWidthMm <= 0 || LabelHeightMm <= 0 || !IsVisible) return;

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x30)), 1, new DashStyle([4, 3], 0));
        var lx = OriginX + InsetMm * s;
        var rx = OriginX + (LabelWidthMm - InsetMm) * s;
        var ty = OriginY + InsetMm * s;
        var by = OriginY + (LabelHeightMm - InsetMm) * s;
        var w = Bounds.Width;
        var h = Bounds.Height;

        context.DrawLine(pen, new Point(lx, 0), new Point(lx, h)); // left print edge, full height
        context.DrawLine(pen, new Point(rx, 0), new Point(rx, h)); // right print edge
        context.DrawLine(pen, new Point(0, ty), new Point(w, ty)); // top print edge, full width
        context.DrawLine(pen, new Point(0, by), new Point(w, by)); // bottom print edge
    }
}
