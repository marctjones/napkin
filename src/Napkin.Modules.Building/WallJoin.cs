using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>How two walls meet (docs/design/renovation-sketches.md §4.1).</summary>
public enum WallJoinKind
{
    /// <summary>An end of the other wall lies on a long face of this one: a butt, as at a T or a corner drawn "running past".</summary>
    EndOnFace,

    /// <summary>An end of this wall lies on a long face of the other.</summary>
    FaceOnEnd,

    /// <summary>The two walls' ends meet face to face.</summary>
    EndToEnd,

    /// <summary>The two walls overlap at a corner, each running into the other's thickness.</summary>
    Overlap,
}

/// <summary>
/// Where another wall joins this one: the stretch along this wall's length that the other covers,
/// and — for a contact rather than an overlap — the shared edge the plan does not stroke.
/// </summary>
/// <param name="Wall">This wall.</param>
/// <param name="Other">The wall joining it.</param>
/// <param name="Kind">How they meet.</param>
/// <param name="From">Where the joined stretch starts, along this wall from its start.</param>
/// <param name="To">Where it ends.</param>
/// <param name="Seam">The shared edge in the plan, or null for an overlap.</param>
public sealed record WallJoin(Wall Wall, Wall Other, WallJoinKind Kind, Length From, Length To, (Point2 From, Point2 To)? Seam);

/// <summary>
/// Corners and joins, decided from the boxes' exact corners with no tolerance (renovation-sketches
/// §4.1): two walls join when an end of one lies on a long face of the other, or two ends meet, with
/// their thicknesses overlapping. Nothing joins walls into a graph; each wall is still framed alone
/// and no corner studs are counted (§1.1).
/// </summary>
public static class WallJoins
{
    /// <summary>Every join of <paramref name="wall"/> with another wall of the sketch, in the other wall's id order.</summary>
    public static ImmutableArray<WallJoin> Of(Sketch sketch, Wall wall)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(wall);
        return [.. Wall.All(sketch).Where(other => other.Id != wall.Id).Select(other => Between(wall, other)).OfType<WallJoin>()];
    }

    /// <summary>How <paramref name="other"/> joins <paramref name="wall"/>, or null when it does not.</summary>
    public static WallJoin? Between(Wall wall, Wall other)
    {
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(other);
        if (Plan.Of(wall) is not { } a || Plan.Of(other) is not { } b)
        {
            return null;
        }

        bool overlapping = a.X0 < b.X1 && b.X0 < a.X1 && a.Y0 < b.Y1 && b.Y0 < a.Y1;
        if (!overlapping)
        {
            foreach ((WallJoinKind kind, Edge[] mine, Edge[] theirs) in new[]
                     {
                         (WallJoinKind.EndOnFace, a.Faces, b.Ends),
                         (WallJoinKind.FaceOnEnd, a.Ends, b.Faces),
                         (WallJoinKind.EndToEnd, a.Ends, b.Ends),
                     })
            {
                foreach (Edge m in mine)
                {
                    foreach (Edge t in theirs)
                    {
                        if (m.Shared(t) is { } seam)
                        {
                            return Along(wall, other, kind, seam.From, seam.To, seam);
                        }
                    }
                }
            }

            return null;
        }

        // An overlap is a corner only where it reaches an end of each wall: two walls crossing, or one
        // lying along the other, are not joined.
        Plan shared = new(Length.Max(a.X0, b.X0), Length.Min(a.X1, b.X1), Length.Max(a.Y0, b.Y0), Length.Min(a.Y1, b.Y1), a.AlongX);
        return a.ReachesAnEnd(shared) && b.ReachesAnEnd(shared)
            ? Along(wall, other, WallJoinKind.Overlap, new Point2(shared.X0, shared.Y0), new Point2(shared.X1, shared.Y1), null)
            : null;
    }

    /// <summary>
    /// The pieces of a straight edge from <paramref name="from"/> to <paramref name="to"/> that are
    /// not on any seam: what the plan strokes, so a joined pair shows no line between them.
    /// </summary>
    public static ImmutableArray<(Point2 From, Point2 To)> Strokes(Point2 from, Point2 to, IEnumerable<(Point2 From, Point2 To)> seams)
    {
        ArgumentNullException.ThrowIfNull(seams);
        bool vertical = from.X == to.X;
        if (!vertical && from.Y != to.Y)
        {
            return [(from, to)];
        }

        Length at = vertical ? from.X : from.Y;
        Length lo = vertical ? Length.Min(from.Y, to.Y) : Length.Min(from.X, to.X);
        Length hi = vertical ? Length.Max(from.Y, to.Y) : Length.Max(from.X, to.X);
        List<(Length Lo, Length Hi)> left = [(lo, hi)];
        foreach ((Point2 a, Point2 b) in seams)
        {
            bool seamVertical = a.X == b.X;
            if (seamVertical != vertical || (seamVertical ? a.X : a.Y) != at || (a.X != b.X && a.Y != b.Y))
            {
                continue;
            }

            Length cutLo = seamVertical ? Length.Min(a.Y, b.Y) : Length.Min(a.X, b.X);
            Length cutHi = seamVertical ? Length.Max(a.Y, b.Y) : Length.Max(a.X, b.X);
            left = [.. left.SelectMany(piece => Cut(piece, cutLo, cutHi))];
        }

        return [.. left.Select(piece => vertical ? (new Point2(at, piece.Lo), new Point2(at, piece.Hi)) : (new Point2(piece.Lo, at), new Point2(piece.Hi, at)))];

        static IEnumerable<(Length Lo, Length Hi)> Cut((Length Lo, Length Hi) piece, Length lo, Length hi)
        {
            if (hi <= piece.Lo || lo >= piece.Hi)
            {
                yield return piece;
                yield break;
            }

            if (lo > piece.Lo)
            {
                yield return (piece.Lo, lo);
            }

            if (hi < piece.Hi)
            {
                yield return (hi, piece.Hi);
            }
        }
    }

    static WallJoin Along(Wall wall, Wall other, WallJoinKind kind, Point2 p, Point2 q, (Point2 From, Point2 To)? seam)
    {
        Length a = Offset(wall, p), b = Offset(wall, q);
        return new WallJoin(wall, other, kind, Length.Min(a, b), Length.Max(a, b), seam);
    }

    /// <summary>How far along the wall, from its start, a plan point lies.</summary>
    static Length Offset(Wall wall, Point2 point)
        => wall.Box.Orientation.Unapply(new Vector3(point.X - wall.Box.Anchor.X, point.Y - wall.Box.Anchor.Y, Length.Zero)).Dx;

    /// <summary>An axis-aligned edge in the plan: a vertical one at X = <see cref="At"/>, or a horizontal one at Y.</summary>
    readonly record struct Edge(bool Vertical, Length At, Length Lo, Length Hi)
    {
        /// <summary>The stretch two edges share on one line, when it has length.</summary>
        public (Point2 From, Point2 To)? Shared(Edge other)
        {
            if (other.Vertical != Vertical || other.At != At)
            {
                return null;
            }

            Length lo = Length.Max(Lo, other.Lo), hi = Length.Min(Hi, other.Hi);
            return lo >= hi ? null : Vertical ? (new Point2(At, lo), new Point2(At, hi)) : (new Point2(lo, At), new Point2(hi, At));
        }
    }

    /// <summary>A wall standing as drawn, in the plan: its rectangle and which way it runs.</summary>
    sealed record Plan(Length X0, Length X1, Length Y0, Length Y1, bool AlongX)
    {
        public static Plan? Of(Wall wall)
        {
            Box box = wall.Box;
            if (!box.Orientation.IsExact || box.FaceUp != BoxFace.Top)
            {
                return null;
            }

            Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(box.Corner)];
            return new Plan(
                corners.Min(c => c.X), corners.Max(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.Y),
                box.Rotation.QuarterTurns % 2 == 0);
        }

        /// <summary>The two short faces a wall ends at.</summary>
        public Edge[] Ends => AlongX
            ? [new Edge(true, X0, Y0, Y1), new Edge(true, X1, Y0, Y1)]
            : [new Edge(false, Y0, X0, X1), new Edge(false, Y1, X0, X1)];

        /// <summary>The two long faces.</summary>
        public Edge[] Faces => AlongX
            ? [new Edge(false, Y0, X0, X1), new Edge(false, Y1, X0, X1)]
            : [new Edge(true, X0, Y0, Y1), new Edge(true, X1, Y0, Y1)];

        /// <summary>Whether a region inside this wall reaches one of its ends.</summary>
        public bool ReachesAnEnd(Plan region) => AlongX ? region.X0 == X0 || region.X1 == X1 : region.Y0 == Y0 || region.Y1 == Y1;
    }
}
