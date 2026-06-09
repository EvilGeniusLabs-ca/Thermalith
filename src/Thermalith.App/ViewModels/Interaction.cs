namespace Thermalith.App.ViewModels;

/// <summary>Which part of a selection a pointer gesture is manipulating (build spec §7 canvas interaction).</summary>
public enum DragMode { None, Move, Resize }

/// <summary>The eight resize handles around a selected element.</summary>
public enum Handle { TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

/// <summary>A resize handle's on-canvas rectangle (display coords) and which corner/edge it drives.</summary>
public sealed record HandleSpec(double Left, double Top, double Size, Handle Kind);

/// <summary>Element geometry in mm, captured at the start of a drag so deltas apply against a fixed origin.</summary>
public readonly record struct GeomMm(double X, double Y, double W, double H);

/// <summary>A selection outline rectangle in display coordinates (bindable for multi-select adorners).
/// <paramref name="Locked"/> drives the grey lock-indicator stroke.</summary>
public sealed record SelRect(double Left, double Top, double Width, double Height, bool Locked = false);
