using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Thermalith.App.Views;

/// <summary>
/// Draws the printable-area boundary as full-span dashed lines across the canvas viewport. The printable
/// area = the label inset by <see cref="InsetXMm"/> left/right and <see cref="InsetYMm"/> top/bottom; an
/// axis with 0 inset (label fits the printhead on that axis) draws no line — so a label narrower than the
/// head shows no margin. <see cref="OriginX"/>/<see cref="OriginY"/> = the label's top-left in this
/// overlay's pixel space (synced to scroll/zoom like the rulers); <see cref="PixelsPerMm"/> = dpi/25.4 × zoom.
/// </summary>
public sealed class PrintGuideOverlay : Control
{
    public static readonly StyledProperty<double> OriginXProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(OriginX));
    public static readonly StyledProperty<double> OriginYProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(OriginY));
    public static readonly StyledProperty<double> PixelsPerMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(PixelsPerMm), 1);
    public static readonly StyledProperty<double> InsetXMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(InsetXMm));
    public static readonly StyledProperty<double> InsetYMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(InsetYMm));
    public static readonly StyledProperty<double> LabelWidthMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(LabelWidthMm));
    public static readonly StyledProperty<double> LabelHeightMmProperty = AvaloniaProperty.Register<PrintGuideOverlay, double>(nameof(LabelHeightMm));

    static PrintGuideOverlay() => AffectsRender<PrintGuideOverlay>(
        OriginXProperty, OriginYProperty, PixelsPerMmProperty, InsetXMmProperty, InsetYMmProperty, LabelWidthMmProperty, LabelHeightMmProperty);

    public PrintGuideOverlay() => IsHitTestVisible = false;

    public double OriginX { get => GetValue(OriginXProperty); set => SetValue(OriginXProperty, value); }
    public double OriginY { get => GetValue(OriginYProperty); set => SetValue(OriginYProperty, value); }
    public double PixelsPerMm { get => GetValue(PixelsPerMmProperty); set => SetValue(PixelsPerMmProperty, value); }
    public double InsetXMm { get => GetValue(InsetXMmProperty); set => SetValue(InsetXMmProperty, value); }
    public double InsetYMm { get => GetValue(InsetYMmProperty); set => SetValue(InsetYMmProperty, value); }
    public double LabelWidthMm { get => GetValue(LabelWidthMmProperty); set => SetValue(LabelWidthMmProperty, value); }
    public double LabelHeightMm { get => GetValue(LabelHeightMmProperty); set => SetValue(LabelHeightMmProperty, value); }

    public override void Render(DrawingContext context)
    {
        var s = PixelsPerMm;
        if (s <= 0 || LabelWidthMm <= 0 || LabelHeightMm <= 0 || !IsVisible) return;
        if (InsetXMm <= 0 && InsetYMm <= 0) return; // label fits the head on both axes → no margin to draw

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x30)), 1, new DashStyle([4, 3], 0));
        var w = Bounds.Width;
        var h = Bounds.Height;

        if (InsetXMm > 0) // left/right printable edges, full height
        {
            context.DrawLine(pen, new Point(OriginX + InsetXMm * s, 0), new Point(OriginX + InsetXMm * s, h));
            context.DrawLine(pen, new Point(OriginX + (LabelWidthMm - InsetXMm) * s, 0), new Point(OriginX + (LabelWidthMm - InsetXMm) * s, h));
        }
        if (InsetYMm > 0) // top/bottom printable edges, full width
        {
            context.DrawLine(pen, new Point(0, OriginY + InsetYMm * s), new Point(w, OriginY + InsetYMm * s));
            context.DrawLine(pen, new Point(0, OriginY + (LabelHeightMm - InsetYMm) * s), new Point(w, OriginY + (LabelHeightMm - InsetYMm) * s));
        }
    }
}
