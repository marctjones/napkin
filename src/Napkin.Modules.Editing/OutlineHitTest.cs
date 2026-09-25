using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// Whether a point on the paper is on what is left of a blank after its cuts
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.4).
/// </summary>
/// <remarks>
/// <para>
/// <strong>A click in a corner that has been cut off is a click on nothing.</strong> That is the
/// whole point of this file: selection asks the outline, not the blank, so a leg drawn under a
/// table top's rounded corner is what a click there picks.
/// </para>
/// <para>
/// <strong>How exact.</strong> The boundary is a polygon through the segments' start points —
/// every arc contributing its chord — plus one circular correction per arc. The polygon test and
/// the correction for a <see cref="ArcByCenter"/> are exact, in <see cref="Int128"/>, because a
/// rounded corner's centre and radius are exact stored quantities (&#xA7;1.5). The correction for
/// an <see cref="ArcThrough"/> fits a circle through three points in <see cref="double"/>, which
/// &#xA7;2.4 allows in as many words: "that is a canvas decision about a click, and nothing stored
/// depends on it".
/// </para>
/// <para>
/// <strong>Why a correction rather than a finer polygon.</strong> Flattening an arc into chords
/// would make the answer depend on how finely it was flattened, and would be wrong by a hair
/// everywhere rather than right everywhere. Invariant 8 (&#xA7;1.6) keeps outline segments from
/// crossing, so the region an arc adds and the region an arc removes are disjoint and can be
/// applied in any order.
/// </para>
/// </remarks>
public static class OutlineHitTest
{
    /// <summary>Whether a point is on the shape an outline bounds, boundary included.</summary>
    /// <param name="outline">The boundary, counter-clockwise, as <see cref="Box.Outline"/> gives it.</param>
    /// <param name="point">The point asked about.</param>
    public static bool Contains(Outline outline, Point2 point)
    {
        ArgumentNullException.ThrowIfNull(outline);

        ImmutableArray<OutlineSegment> segments = outline.Segments;
        if (segments.Length < 3)
        {
            return false;
        }

        // The chord polygon: every segment's start, so an arc is represented by the straight line
        // between its ends and corrected below.
        ImmutableArray<Point2> chords = [.. segments.Select(segment => segment.From)];
        bool inside = PolygonContains(chords, point);
        bool added = false;
        bool removed = false;

        foreach (OutlineSegment segment in segments)
        {
            switch (segment)
            {
                case ArcByCenter rounded when InRoundedBulge(rounded, point):
                    // A rounded corner's arc always bulges away from its centre, towards the
                    // virtual corner, so its circular segment is material the chord left out.
                    added = true;
                    break;

                case ArcThrough curve when InFittedBulge(curve, point):
                    if (BowsInward(curve))
                    {
                        removed = true;
                    }
                    else
                    {
                        added = true;
                    }

                    break;
            }
        }

        return (inside || added) && !removed;
    }

    /// <summary>
    /// Whether a curved edge dips into the blank rather than bulging out of it: its middle point
    /// is on the interior side of its own chord.
    /// </summary>
    /// <remarks>
    /// "Interior side" is the left of the chord, because <see cref="Box.Outline"/> walks
    /// counter-clockwise and a right-angle rotation cannot turn that around (&#xA7;1.5).
    /// </remarks>
    static bool BowsInward(ArcThrough curve) =>
        LeftOf(curve.From, curve.To, curve.Through) > Int128.Zero;

    /// <summary>
    /// Whether a point is in the crescent a rounded corner's arc adds back to the chord: on the
    /// far side of the chord from the centre, and no further from the centre than the radius.
    /// </summary>
    /// <remarks>Exact: both tests are products of stored lengths, in <see cref="Int128"/>.</remarks>
    static bool InRoundedBulge(ArcByCenter arc, Point2 point)
    {
        Int128 centreSide = LeftOf(arc.From, arc.To, arc.Center);
        Int128 pointSide = LeftOf(arc.From, arc.To, point);
        if (centreSide == Int128.Zero || Sign(pointSide) == Sign(centreSide))
        {
            return false;
        }

        return SquaredDistance(point, arc.Center) <= SquaredDistance(arc.From, arc.Center);
    }

    /// <summary>
    /// Whether a point is in the crescent between a curved edge's chord and its arc: on the same
    /// side of the chord as the point the arc passes through, and inside the fitted circle.
    /// </summary>
    /// <remarks>
    /// The circle is fitted in <see cref="double"/> from the three exact points (&#xA7;2.4). Three
    /// points that turn out to be collinear describe no circle and no crescent, so the chord is
    /// the answer.
    /// </remarks>
    static bool InFittedBulge(ArcThrough arc, Point2 point)
    {
        Int128 throughSide = LeftOf(arc.From, arc.To, arc.Through);
        Int128 pointSide = LeftOf(arc.From, arc.To, point);
        if (throughSide == Int128.Zero || Sign(pointSide) != Sign(throughSide))
        {
            return false;
        }

        if (!FittedCircle(arc, out double centreX, out double centreY, out double radius))
        {
            return false;
        }

        double dx = point.X.ToInches() - centreX;
        double dy = point.Y.ToInches() - centreY;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    /// <summary>
    /// The circle through a curved edge's three points, in inches.
    /// </summary>
    /// <returns><see langword="false"/> when the three points are collinear.</returns>
    static bool FittedCircle(
        ArcThrough arc,
        out double centreX,
        out double centreY,
        out double radius)
    {
        ArgumentNullException.ThrowIfNull(arc);

        double ax = arc.From.X.ToInches();
        double ay = arc.From.Y.ToInches();
        double bx = arc.Through.X.ToInches();
        double by = arc.Through.Y.ToInches();
        double cx = arc.To.X.ToInches();
        double cy = arc.To.Y.ToInches();

        double twiceArea = 2 * (((bx - ax) * (cy - ay)) - ((by - ay) * (cx - ax)));
        if (Math.Abs(twiceArea) < 1e-12)
        {
            centreX = centreY = radius = 0;
            return false;
        }

        double aSquared = (ax * ax) + (ay * ay);
        double bSquared = (bx * bx) + (by * by);
        double cSquared = (cx * cx) + (cy * cy);

        centreX = ((aSquared * (by - cy)) + (bSquared * (cy - ay)) + (cSquared * (ay - by))) / twiceArea;
        centreY = ((aSquared * (cx - bx)) + (bSquared * (ax - cx)) + (cSquared * (bx - ax))) / twiceArea;
        radius = Math.Sqrt(((ax - centreX) * (ax - centreX)) + ((ay - centreY) * (ay - centreY)));
        return true;
    }

    /// <summary>
    /// Twice the signed area of the triangle <c>from &#x2192; to &#x2192; point</c>: positive when
    /// the point is to the left of the directed line, which — for a counter-clockwise outline — is
    /// its inside.
    /// </summary>
    static Int128 LeftOf(Point2 from, Point2 to, Point2 point) =>
        (Area.Of(to.X - from.X, point.Y - from.Y)) - (Area.Of(to.Y - from.Y, point.X - from.X));

    static Int128 SquaredDistance(Point2 a, Point2 b) =>
        Area.Of(a.X - b.X, a.X - b.X) + Area.Of(a.Y - b.Y, a.Y - b.Y);

    static int Sign(Int128 value) => value > Int128.Zero ? 1 : value < Int128.Zero ? -1 : 0;

    /// <summary>
    /// Whether a point is inside a closed polygon, exactly: the crossing-number test, with the
    /// division that decides which side of an edge the point falls cross-multiplied away.
    /// </summary>
    static bool PolygonContains(ImmutableArray<Point2> polygon, Point2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Point2 a = polygon[j];
            Point2 b = polygon[i];

            if (a.Y > point.Y == b.Y > point.Y)
            {
                continue;
            }

            // Where the edge crosses the ray, compared with the point, without dividing: the
            // comparison turns around when the edge runs downwards.
            Int128 left = Area.Of(point.X - a.X, b.Y - a.Y);
            Int128 right = Area.Of(point.Y - a.Y, b.X - a.X);
            bool crossesToTheRight = b.Y > a.Y ? left < right : left > right;

            if (crossesToTheRight)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
