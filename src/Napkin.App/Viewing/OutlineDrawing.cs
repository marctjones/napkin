using System.Collections.Immutable;

using Avalonia;
using Avalonia.Media;

using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// Draws a blank's derived <see cref="Outline"/> — the real boundary of a shaped part
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;1.5).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pixels only.</strong> Nothing here is stored and nothing compares against it. An
/// <see cref="ArcByCenter"/> is drawn from its two exact ends and its exact centre; an
/// <see cref="ArcThrough"/> has a circle fitted through its three exact points in
/// <see cref="double"/>, which &#xA7;1.5's last paragraph says in as many words is what whoever
/// draws it should do.
/// </para>
/// <para>
/// <strong>One builder, two callers.</strong> The canvas draws parts at the view's scale and the
/// cut-list thumbnail draws the same outline shrunk into a cell, so the projection arrives as a
/// function and the geometry is built the same way for both — a shaped part cannot look like one
/// thing on the drawing and another beside its row.
/// </para>
/// </remarks>
public static class OutlineDrawing
{
    /// <summary>
    /// The outline as a closed figure, in whatever coordinates the projection produces.
    /// </summary>
    /// <param name="outline">The boundary, as <see cref="Box.Outline"/> gives it.</param>
    /// <param name="toScreen">Where a model point is drawn. Assumed to preserve shape and scale.</param>
    public static StreamGeometry GeometryOf(Outline outline, Func<Point2, Point> toScreen)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(toScreen);

        ImmutableArray<OutlineSegment> segments = outline.Segments;
        StreamGeometry geometry = new();
        using (StreamGeometryContext figure = geometry.Open())
        {
            if (segments.Length == 0)
            {
                figure.BeginFigure(default, isFilled: true);
                figure.EndFigure(isClosed: true);
                return geometry;
            }

            figure.BeginFigure(toScreen(segments[0].From), isFilled: true);
            foreach (OutlineSegment segment in segments)
            {
                Point from = toScreen(segment.From);
                Point to = toScreen(segment.To);

                switch (segment)
                {
                    case ArcByCenter rounded:
                        DrawRounded(figure, from, to, toScreen(rounded.Center));
                        break;

                    case ArcThrough curve:
                        DrawCurved(figure, from, toScreen(curve.Through), to);
                        break;

                    default:
                        figure.LineTo(to);
                        break;
                }
            }

            figure.EndFigure(isClosed: true);
        }

        return geometry;
    }

    /// <summary>
    /// A rounded corner: the radius is the distance from either end to the exact centre, and a
    /// roundover is never more than a quarter turn, so it is never the long way round.
    /// </summary>
    static void DrawRounded(StreamGeometryContext figure, Point from, Point to, Point centre)
    {
        // Both ends are the same distance from the centre in the model; averaging is only
        // insurance against the half-pixel the projection can leave between them.
        double radius = (Distance(from, centre) + Distance(to, centre)) / 2;
        if (radius <= 0)
        {
            figure.LineTo(to);
            return;
        }

        figure.ArcTo(to, new Size(radius, radius), 0, isLargeArc: false, SweepFrom(centre, from, to));
    }

    /// <summary>
    /// A curved edge: a circle fitted through the three points, drawn the way round that passes
    /// through the middle one.
    /// </summary>
    static void DrawCurved(StreamGeometryContext figure, Point from, Point through, Point to)
    {
        // The circle is fitted in the projection's own coordinates rather than projected from the
        // model's, so that a scale and a flip leave a circle a circle. Three points on a line
        // describe no arc; nothing the updater accepts produces that, and the chord is the honest
        // answer if anything ever does.
        if (!Circumcentre(from, through, to, out Point centre))
        {
            figure.LineTo(to);
            return;
        }

        double radius = (Distance(from, centre) + Distance(through, centre) + Distance(to, centre)) / 3;

        // More than half a turn exactly when the centre lies on the same side of the chord as the
        // point the arc has to pass through.
        bool largeArc = Math.Sign(Side(from, to, centre)) == Math.Sign(Side(from, to, through));

        // The sweep follows the way the three points turn, not the centre: for a major arc the
        // centre is on the other side and would name the wrong direction.
        SweepDirection sweep = Side(from, through, to) > 0
            ? SweepDirection.Clockwise
            : SweepDirection.CounterClockwise;

        figure.ArcTo(to, new Size(radius, radius), 0, largeArc, sweep);
    }

    /// <summary>
    /// Which way an arc about a centre turns on screen, where y grows downwards: a positive cross
    /// product is a clockwise turn to look at.
    /// </summary>
    static SweepDirection SweepFrom(Point centre, Point from, Point to) =>
        ((from.X - centre.X) * (to.Y - centre.Y)) - ((from.Y - centre.Y) * (to.X - centre.X)) > 0
            ? SweepDirection.Clockwise
            : SweepDirection.CounterClockwise;

    /// <summary>Twice the signed area of a triangle: which side of <c>a &#x2192; b</c> <c>c</c> is.</summary>
    static double Side(Point a, Point b, Point c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    /// <summary>The centre of the circle through three points, in the points' own coordinates.</summary>
    static bool Circumcentre(Point a, Point b, Point c, out Point centre)
    {
        double twiceArea = 2 * Side(a, b, c);
        if (Math.Abs(twiceArea) < 1e-9)
        {
            centre = default;
            return false;
        }

        double aSquared = (a.X * a.X) + (a.Y * a.Y);
        double bSquared = (b.X * b.X) + (b.Y * b.Y);
        double cSquared = (c.X * c.X) + (c.Y * c.Y);

        centre = new Point(
            ((aSquared * (b.Y - c.Y)) + (bSquared * (c.Y - a.Y)) + (cSquared * (a.Y - b.Y))) / twiceArea,
            ((aSquared * (c.X - b.X)) + (bSquared * (a.X - c.X)) + (cSquared * (b.X - a.X))) / twiceArea);
        return true;
    }

    static double Distance(Point a, Point b) => Math.Sqrt(
        ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
