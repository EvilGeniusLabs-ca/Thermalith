using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Thermalith.App.Views;

/// <summary>
/// A mm scale strip drawn along the top or left edge of the canvas (build spec §7 / rulers). It maps
/// screen pixels to label mm via two inputs the view keeps in sync: <see cref="Origin"/> = where the
/// label's 0 mm sits in this strip's own pixel space, and <see cref="PixelsPerMm"/> = the on-screen
/// scale (dpi/25.4 × zoom). Negative readings before the origin are the surrounding work margin.
/// </summary>
public sealed class Ruler : Control
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Ruler, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public static readonly StyledProperty<double> OriginProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(Origin));

    public static readonly StyledProperty<double> PixelsPerMmProperty =
        AvaloniaProperty.Register<Ruler, double>(nameof(PixelsPerMm), 1);

    private static readonly double[] StepsMm = [1, 2, 5, 10, 20, 50, 100, 200, 500];

    private readonly Typeface _face = new("Segoe UI");

    static Ruler() => AffectsRender<Ruler>(OrientationProperty, OriginProperty, PixelsPerMmProperty);

    public Ruler() => IsHitTestVisible = false;

    // Repaint when the effective theme flips so the strip re-resolves its chrome brushes.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == nameof(ActualThemeVariant)) InvalidateVisual();
    }

    /// <summary>Resolve a theme brush by key for the current variant; fall back to a fixed colour.</summary>
    private IBrush ThemeBrush(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : new SolidColorBrush(fallback);

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Origin { get => GetValue(OriginProperty); set => SetValue(OriginProperty, value); }
    public double PixelsPerMm { get => GetValue(PixelsPerMmProperty); set => SetValue(PixelsPerMmProperty, value); }

    public override void Render(DrawingContext context)
    {
        var bg = ThemeBrush("RulerBg", Color.FromRgb(0x2A, 0x2A, 0x2A));
        var tick = new Pen(ThemeBrush("RulerTick", Color.FromRgb(0x77, 0x77, 0x77)));
        var textBrush = ThemeBrush("RulerText", Color.FromRgb(0xAA, 0xAA, 0xAA));

        var horizontal = Orientation == Orientation.Horizontal;
        var length = horizontal ? Bounds.Width : Bounds.Height;
        var thickness = horizontal ? Bounds.Height : Bounds.Width;
        context.FillRectangle(bg, new Rect(Bounds.Size));

        var s = PixelsPerMm;
        if (s <= 0 || length <= 0) return;

        // Pick a major-tick step so majors are at least ~52px apart.
        var step = StepsMm[^1];
        foreach (var cand in StepsMm)
            if (cand * s >= 52) { step = cand; break; }
        var minor = step / 5.0;

        var origin = Origin;
        var mmStart = (0 - origin) / s;
        var mmEnd = (length - origin) / s;

        // Minor ticks (short, no label).
        var firstMinor = Math.Ceiling(mmStart / minor) * minor;
        for (var mm = firstMinor; mm <= mmEnd; mm += minor)
        {
            var p = origin + mm * s;
            if (horizontal) context.DrawLine(tick, new Point(p, thickness - 4), new Point(p, thickness));
            else context.DrawLine(tick, new Point(thickness - 4, p), new Point(thickness, p));
        }

        // Major ticks + labels.
        var firstMajor = Math.Ceiling(mmStart / step) * step;
        for (var mm = firstMajor; mm <= mmEnd; mm += step)
        {
            var p = origin + mm * s;
            var label = ((int)Math.Round(mm)).ToString();
            var ft = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _face, 9, textBrush);

            if (horizontal)
            {
                context.DrawLine(tick, new Point(p, thickness - 9), new Point(p, thickness));
                context.DrawText(ft, new Point(p + 2, 1));
            }
            else
            {
                context.DrawLine(tick, new Point(thickness - 9, p), new Point(thickness, p));
                context.DrawText(ft, new Point(Math.Max(1, thickness - ft.Width - 4), p + 1));
            }
        }
    }
}
