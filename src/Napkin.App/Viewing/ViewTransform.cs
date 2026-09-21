using Avalonia;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// Where the drawing sits on the screen: what part of the model the viewport shows, and how large
/// an inch of it is drawn.
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is the only place the model meets the screen.</strong> Model coordinates are exact
/// integer <see cref="Length"/>s on a 1/1024&#x2033; grid; screen coordinates are device-independent
/// pixels and are inherently fractional. The conversion happens here, at the render edge, and
/// nowhere else: no drawing code multiplies a <see cref="Length"/> by a zoom factor, and no view
/// operation ever writes back to an entity (docs/design/geometry-model.md &#xA7;2.1).
/// </para>
/// <para>
/// <strong>Y is up in the model</strong> — the CAD, DXF and PDF convention — and down on the screen.
/// The flip lives in <see cref="ToScreen(double, double)"/> and its inverse, which is the whole of
/// the screen flip in napkin's drawing code.
/// </para>
/// <para>
/// The transform is a value: every navigation verb returns a new one. That makes zoom-about-the-
/// cursor a testable function rather than a sequence of mutations, and it means the canvas can hold
/// exactly one of them.
/// </para>
/// </remarks>
/// <param name="CenterXInches">The model X coordinate, in inches, at the centre of the viewport.</param>
/// <param name="CenterYInches">The model Y coordinate, in inches, at the centre of the viewport.</param>
/// <param name="PixelsPerInch">
/// How many device-independent pixels one model inch covers. Clamped to
/// [<see cref="MinPixelsPerInch"/>, <see cref="MaxPixelsPerInch"/>] by the constructor, so no
/// sequence of zooms can leave the view at a scale nothing is visible at.
/// </param>
/// <param name="Viewport">The size of the canvas the transform paints into, in pixels.</param>
public readonly record struct ViewTransform(
    double CenterXInches,
    double CenterYInches,
    double PixelsPerInch,
    Size Viewport)
{
    /// <summary>
    /// The scale at which the drawing is nominally life-size: Avalonia's device-independent pixel
    /// is 1/96&#x2033;, so 96 pixels to the inch is 100% zoom.
    /// </summary>
    public const double PixelsPerInchAt100Percent = 96.0;

    /// <summary>
    /// Furthest out: an inch is a fiftieth of a pixel, so a 1,500-foot site fits in a laptop
    /// window. Zooming out past this is never useful and invites divide-by-tiny arithmetic.
    /// </summary>
    public const double MinPixelsPerInch = 0.02;

    /// <summary>
    /// Furthest in: an inch covers 2,400 pixels, so the 1/1024&#x2033; grid is about two pixels
    /// wide. Nothing in the model is finer than that, so zooming further shows no more detail.
    /// </summary>
    public const double MaxPixelsPerInch = 2400.0;

    /// <summary>The fraction of the viewport left empty around the drawing by a zoom-to-fit.</summary>
    public const double FitMarginFraction = 0.06;

    /// <summary>
    /// The view a canvas starts with: no viewport yet, so nothing can be fitted into it until the
    /// first layout pass says how big it is.
    /// </summary>
    public static readonly ViewTransform Default =
        new(0, 0, PixelsPerInchAt100Percent / 4, default);

    readonly double _pixelsPerInch = ClampScale(PixelsPerInch);

    /// <inheritdoc cref="ViewTransform(double, double, double, Size)"/>
    /// <remarks>
    /// Declared explicitly, rather than taking the synthesised positional property, so that the
    /// limits are applied however the value arrives — the constructor, a <c>with</c> expression or
    /// a zoom — and no caller has to remember them.
    /// </remarks>
    public double PixelsPerInch
    {
        get => _pixelsPerInch;
        init => _pixelsPerInch = ClampScale(value);
    }

    /// <summary>The zoom as a percentage of life size, for the status bar.</summary>
    public double ZoomPercent => PixelsPerInch / PixelsPerInchAt100Percent * 100.0;

    /// <summary>Whether the viewport has a positive area — false before the first layout pass.</summary>
    public bool HasViewport => Viewport.Width > 0 && Viewport.Height > 0;

    /// <summary>Where a model point is drawn.</summary>
    public Point ToScreen(Point2 point) => ToScreen(point.X.ToInches(), point.Y.ToInches());

    /// <summary>Where a model point, in inches, is drawn. The screen flip is the minus sign.</summary>
    public Point ToScreen(double xInches, double yInches) => new(
        (Viewport.Width / 2.0) + ((xInches - CenterXInches) * PixelsPerInch),
        (Viewport.Height / 2.0) - ((yInches - CenterYInches) * PixelsPerInch));

    /// <summary>What model point, in inches, is drawn at a screen point.</summary>
    public (double X, double Y) ToWorldInches(Point screen) => (
        CenterXInches + ((screen.X - (Viewport.Width / 2.0)) / PixelsPerInch),
        CenterYInches - ((screen.Y - (Viewport.Height / 2.0)) / PixelsPerInch));

    /// <summary>
    /// What model point is under a screen point, rounded onto the 1/1024&#x2033; grid.
    /// </summary>
    /// <remarks>
    /// Rounding is half away from zero because this is a display value — the cursor readout — and
    /// that is the display rule (docs/design/geometry-model.md &#xA7;1.4). Nothing derived from this
    /// is stored; a read-only viewer has nothing to store it in.
    /// </remarks>
    public Point2 ToWorld(Point screen)
    {
        (double x, double y) = ToWorldInches(screen);
        return new Point2(
            Length.FromInches(x, Rounding.HalfAwayFromZero),
            Length.FromInches(y, Rounding.HalfAwayFromZero));
    }

    /// <summary>The same view in a viewport of another size. The model point at the centre stays there.</summary>
    public ViewTransform WithViewport(Size viewport) => this with { Viewport = viewport };

    /// <summary>
    /// The view after dragging the drawing by a screen displacement: the model point under the
    /// pointer stays under it, so the drawing follows the hand.
    /// </summary>
    public ViewTransform PanByPixels(Vector screenDelta) => this with
    {
        CenterXInches = CenterXInches - (screenDelta.X / PixelsPerInch),
        CenterYInches = CenterYInches + (screenDelta.Y / PixelsPerInch),
    };

    /// <summary>The view after panning by a fraction of the viewport — what an arrow key does.</summary>
    public ViewTransform PanByViewportFraction(double fractionX, double fractionY) =>
        PanByPixels(new Vector(-fractionX * Viewport.Width, -fractionY * Viewport.Height));

    /// <summary>
    /// The view after zooming about a screen point: the model point under that point is still under
    /// it afterwards, to within the rounding of a single pixel.
    /// </summary>
    /// <remarks>
    /// The anchor is honoured even when the requested factor is clipped by the scale limits: the
    /// centre is recomputed from the scale that was actually applied, so a wheel turn at the limit
    /// leaves the view exactly where it was instead of sliding.
    /// </remarks>
    public ViewTransform ZoomAt(Point anchor, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return this;
        }

        double scale = ClampScale(PixelsPerInch * factor);
        (double worldX, double worldY) = ToWorldInches(anchor);
        return this with
        {
            CenterXInches = worldX - ((anchor.X - (Viewport.Width / 2.0)) / scale),
            CenterYInches = worldY + ((anchor.Y - (Viewport.Height / 2.0)) / scale),
            PixelsPerInch = scale,
        };
    }

    /// <summary>The view after zooming about the centre of the viewport — what the +/- keys do.</summary>
    public ViewTransform ZoomAtCenter(double factor) =>
        ZoomAt(new Point(Viewport.Width / 2.0, Viewport.Height / 2.0), factor);

    /// <summary>
    /// The view that frames <paramref name="bounds"/> in <paramref name="viewport"/> with a margin
    /// all round, keeping this transform's scale when there is nothing to frame.
    /// </summary>
    /// <param name="bounds">What to frame.</param>
    /// <param name="viewport">The canvas size to frame it in.</param>
    /// <param name="marginFraction">
    /// How much of each edge to leave empty, as a fraction of the viewport.
    /// </param>
    public ViewTransform FitTo(WorldBounds bounds, Size viewport, double marginFraction = FitMarginFraction)
    {
        if (bounds.IsEmpty || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return WithViewport(viewport);
        }

        double usableWidth = Math.Max(viewport.Width * (1 - (2 * marginFraction)), 1);
        double usableHeight = Math.Max(viewport.Height * (1 - (2 * marginFraction)), 1);
        double width = bounds.WidthInches;
        double height = bounds.HeightInches;

        // A design with no extent along one axis — a single part drawn edge-on, a lone node —
        // is framed by the axis that has one; a design with neither keeps the current scale.
        double byWidth = width > 0 ? usableWidth / width : double.PositiveInfinity;
        double byHeight = height > 0 ? usableHeight / height : double.PositiveInfinity;
        double scale = Math.Min(byWidth, byHeight);
        if (!double.IsFinite(scale))
        {
            scale = PixelsPerInch;
        }

        return new ViewTransform(bounds.CenterXInches, bounds.CenterYInches, scale, viewport);
    }

    /// <summary>The scale limits, applied wherever a scale is set.</summary>
    static double ClampScale(double pixelsPerInch) => double.IsFinite(pixelsPerInch)
        ? Math.Clamp(pixelsPerInch, MinPixelsPerInch, MaxPixelsPerInch)
        : PixelsPerInchAt100Percent;
}
