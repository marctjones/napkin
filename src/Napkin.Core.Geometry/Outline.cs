using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>One piece of a blank's boundary.</summary>
/// <param name="From">Where the piece starts.</param>
/// <param name="To">Where the piece ends.</param>
public abstract record OutlineSegment(Point2 From, Point2 To);

/// <summary>A straight run of the boundary.</summary>
/// <param name="From">Where the run starts.</param>
/// <param name="To">Where the run ends.</param>
public sealed record StraightSegment(Point2 From, Point2 To) : OutlineSegment(From, To);

/// <summary>
/// A circular arc whose centre is on the grid: a rounded corner. The radius is
/// |<paramref name="From"/> &#x2212; <paramref name="Center"/>|.
/// </summary>
/// <param name="From">Where the arc starts — a tangent point on the incoming edge.</param>
/// <param name="To">Where the arc ends — a tangent point on the outgoing edge.</param>
/// <param name="Center">The centre, exact: the corner offset by the radius along both axes.</param>
public sealed record ArcByCenter(Point2 From, Point2 To, Point2 Center) : OutlineSegment(From, To);

/// <summary>
/// A circular arc through three grid points: a curved edge. The centre and radius are derived in
/// <see cref="double"/> by whoever draws it; nothing stored depends on them.
/// </summary>
/// <param name="From">Where the arc starts.</param>
/// <param name="Through">The third point the arc passes through, between the two ends.</param>
/// <param name="To">Where the arc ends.</param>
public sealed record ArcThrough(Point2 From, Point2 Through, Point2 To) : OutlineSegment(From, To);

/// <summary>
/// The closed boundary of what is left of a blank, in world coordinates, counter-clockwise in the
/// box's local frame (<c>docs/design/shaped-parts-model.md</c> §1.5).
/// </summary>
/// <remarks>
/// The walk starts along the south edge, at the south-west corner or at whatever a cut there left
/// of it, and each corner's own segment is emitted when the walk <em>arrives</em> at that corner —
/// so a cut at the south-west corner is the last segment, not the first, and the boundary closes
/// on the first segment's start.
/// </remarks>
/// <param name="Segments">The boundary in order; the last segment ends where the first begins.</param>
public sealed record Outline(ImmutableArray<OutlineSegment> Segments)
{
    /// <summary>
    /// The polygon the boundary's points describe: every segment's start, plus the third point of
    /// each arc through three points. A rounded corner contributes its chord, which understates
    /// the area rather than overstating it.
    /// </summary>
    public ImmutableArray<Point2> Vertices
    {
        get
        {
            ImmutableArray<Point2>.Builder vertices = ImmutableArray.CreateBuilder<Point2>();
            foreach (OutlineSegment segment in Segments)
            {
                vertices.Add(segment.From);
                if (segment is ArcThrough arc)
                {
                    vertices.Add(arc.Through);
                }
            }

            return vertices.ToImmutable();
        }
    }

    /// <summary>Equality by value: <see cref="ImmutableArray{T}"/> compares by identity.</summary>
    public bool Equals(Outline? other) => other is not null && Segments.SequenceEqual(other.Segments);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (OutlineSegment segment in Segments)
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Walks a blank's four edges and its cuts into an <see cref="Outline"/>
/// (<c>docs/design/shaped-parts-model.md</c> §1.5).
/// </summary>
/// <remarks>
/// <para>
/// Total by construction: a box whose cuts break §1.6's invariants still produces an outline, so
/// that <see cref="Sketch.Validate"/> can compute invariant 9's area without first knowing that
/// invariants 5 and 6 passed. Where two cuts claim one site the first in site order wins, and a
/// corner's own cut wins over a curve that claims the corner.
/// </para>
/// <para>
/// Every point is exact under right-angle rotations: a setback point is a corner offset by a
/// stored length along one axis, an arc centre is a corner offset along both, and the only
/// rounding is an edge's middle, which — like <see cref="Box.Center"/> — rounds by half a unit
/// when the edge is an odd number of units long.
/// </para>
/// </remarks>
internal static class OutlineBuilder
{
    /// <summary>The four edges in walk order, each with the corner it runs from and to.</summary>
    private static readonly (BoxEdge Edge, BoxCorner From, BoxCorner To)[] Walk =
    [
        (BoxEdge.South, BoxCorner.SouthWest, BoxCorner.SouthEast),
        (BoxEdge.East, BoxCorner.SouthEast, BoxCorner.NorthEast),
        (BoxEdge.North, BoxCorner.NorthEast, BoxCorner.NorthWest),
        (BoxEdge.West, BoxCorner.NorthWest, BoxCorner.SouthWest),
    ];

    /// <summary>The boundary of a blank, in world coordinates or in its own local frame.</summary>
    internal static Outline Build(Box box, bool world)
    {
        Dictionary<CutSite, Cut> bySite = CutsBySite(box);
        ImmutableArray<OutlineSegment>.Builder segments = ImmutableArray.CreateBuilder<OutlineSegment>();

        Point2 Place(Vector2 local) => world ? box.Anchor + local.Rotate(box.Rotation) : new Point2(local.Dx, local.Dy);

        foreach ((BoxEdge edge, BoxCorner from, BoxCorner to) in Walk)
        {
            Vector2 leaves = CornerPoint(box, bySite, from, arriving: false);
            Vector2 meets = CornerPoint(box, bySite, to, arriving: true);

            if (bySite.GetValueOrDefault(CutSite.Edge(edge)) is CurvedEdge curve)
            {
                segments.Add(new ArcThrough(Place(leaves), Place(Through(box, curve)), Place(meets)));
            }
            else if (leaves != meets)
            {
                segments.Add(new StraightSegment(Place(leaves), Place(meets)));
            }

            switch (bySite.GetValueOrDefault(CutSite.Corner(to)))
            {
                case CornerCut:
                    segments.Add(new StraightSegment(
                        Place(meets),
                        Place(CornerPoint(box, bySite, to, arriving: false))));
                    break;

                case RoundedCorner rounded:
                    segments.Add(new ArcByCenter(
                        Place(meets),
                        Place(CornerPoint(box, bySite, to, arriving: false)),
                        Place(Center(box, to, rounded.Radius))));
                    break;
            }
        }

        return new Outline(segments.ToImmutable());
    }

    /// <summary>The cuts by site, first in site order winning when a site is claimed twice.</summary>
    private static Dictionary<CutSite, Cut> CutsBySite(Box box)
    {
        Dictionary<CutSite, Cut> bySite = [];
        foreach (Cut cut in box.Cuts)
        {
            bySite.TryAdd(cut.Site, cut);
        }

        return bySite;
    }

    /// <summary>
    /// Where the boundary arrives at a corner, or leaves it: the corner itself, the cut's point on
    /// the edge in question, or — when an adjacent edge bows outward — the point that curve starts
    /// from, which is <see cref="CurvedEdge.Depth"/> along the corner's <em>other</em> edge.
    /// </summary>
    private static Vector2 CornerPoint(Box box, Dictionary<CutSite, Cut> bySite, BoxCorner corner, bool arriving)
    {
        BoxEdge incoming = IncomingEdge(corner);
        BoxEdge outgoing = OutgoingEdge(corner);
        BoxEdge edge = arriving ? incoming : outgoing;

        switch (bySite.GetValueOrDefault(CutSite.Corner(corner)))
        {
            case CornerCut cut:
                return Along(box, corner, edge, RunsAlongX(edge) ? cut.AlongX : cut.AlongY);

            case RoundedCorner rounded:
                return Along(box, corner, edge, rounded.Radius);
        }

        if (bySite.GetValueOrDefault(CutSite.Edge(incoming)) is CurvedEdge { Bow: Bow.Outward } arrivingCurve)
        {
            return Along(box, corner, outgoing, arrivingCurve.Depth);
        }

        if (bySite.GetValueOrDefault(CutSite.Edge(outgoing)) is CurvedEdge { Bow: Bow.Outward } leavingCurve)
        {
            return Along(box, corner, incoming, leavingCurve.Depth);
        }

        return box.LocalOffset(corner);
    }

    /// <summary>The third point of a curved edge's arc.</summary>
    private static Vector2 Through(Box box, CurvedEdge curve)
    {
        Vector2 middle = Middle(box, curve.Edge);
        if (curve.Bow == Bow.Outward)
        {
            return middle;
        }

        // Inward: the middle moves into the blank, perpendicular to the edge.
        Axis across = RunsAlongX(curve.Edge) ? Axis.Y : Axis.X;
        bool towardsTheOrigin = curve.Edge is BoxEdge.North or BoxEdge.East;
        return middle + Vector2.Along(across, towardsTheOrigin ? -curve.Depth : curve.Depth);
    }

    /// <summary>The middle of an edge, the one place the outline rounds — by half a unit.</summary>
    private static Vector2 Middle(Box box, BoxEdge edge)
    {
        (BoxCorner from, BoxCorner to) = Box.Ends(edge);
        Vector2 a = box.LocalOffset(from);
        Vector2 b = box.LocalOffset(to);
        return new Vector2(
            (a.Dx + b.Dx).Divide(2, Rounding.HalfToEven),
            (a.Dy + b.Dy).Divide(2, Rounding.HalfToEven));
    }

    /// <summary>The centre of a rounded corner: the corner offset by the radius along both edges.</summary>
    private static Vector2 Center(Box box, BoxCorner corner, Length radius)
    {
        Vector2 at = box.LocalOffset(corner);
        return Along(box, corner, IncomingEdge(corner), radius)
               + (Along(box, corner, OutgoingEdge(corner), radius) - at);
    }

    /// <summary>The point <paramref name="distance"/> from a corner, into the edge.</summary>
    internal static Vector2 Along(Box box, BoxCorner corner, BoxEdge edge, Length distance)
    {
        Axis axis = RunsAlongX(edge) ? Axis.X : Axis.Y;
        (BoxCorner from, BoxCorner to) = Box.Ends(edge);
        BoxCorner other = corner == from ? to : from;

        Length towards = box.LocalOffset(other).Component(axis) - box.LocalOffset(corner).Component(axis);
        return box.LocalOffset(corner) + Vector2.Along(axis, towards >= Length.Zero ? distance : -distance);
    }

    /// <summary>Whether an edge runs along local X.</summary>
    internal static bool RunsAlongX(BoxEdge edge) => edge is BoxEdge.South or BoxEdge.North;

    /// <summary>The edge the counter-clockwise walk reaches a corner along.</summary>
    internal static BoxEdge IncomingEdge(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => BoxEdge.West,
        BoxCorner.SouthEast => BoxEdge.South,
        BoxCorner.NorthEast => BoxEdge.East,
        _ => BoxEdge.North,
    };

    /// <summary>The edge the counter-clockwise walk leaves a corner along.</summary>
    internal static BoxEdge OutgoingEdge(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => BoxEdge.South,
        BoxCorner.SouthEast => BoxEdge.East,
        BoxCorner.NorthEast => BoxEdge.North,
        _ => BoxEdge.West,
    };
}
