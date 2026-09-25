using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// The solid a blank and its cuts leave: a prism whose caps are the outline. Derived, never stored
/// (<c>docs/design/assembly-model.md</c> §4.1).
/// </summary>
/// <remarks>
/// The faces come in one order: the <see cref="BoxFace.Bottom"/> cap, the <see cref="BoxFace.Top"/>
/// cap, then one side per segment of <see cref="Box.Outline"/>, in the outline's own order — so a
/// plain rectangle's sides are south, east, north, west.
/// </remarks>
/// <param name="Faces">Every face of the solid, caps first.</param>
public sealed record Solid(ImmutableArray<SolidFace> Faces)
{
    /// <summary>Equality by value: <see cref="ImmutableArray{T}"/> compares by identity.</summary>
    public bool Equals(Solid? other) => other is not null && Faces.SequenceEqual(other.Faces);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (SolidFace face in Faces)
        {
            hash.Add(face);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// One planar or singly-curved face of the solid, in world coordinates.
/// </summary>
/// <remarks>
/// The boundary is closed — each segment ends where the next begins and the last ends where the
/// first begins — and winds counter-clockwise seen from outside the solid, so that the right-hand
/// rule gives the outward normal. A curved side is bounded by its two arcs, one in each cap plane,
/// and the two straight rulings between their ends.
/// </remarks>
/// <param name="Of">
/// The face of the box this lies on: <see cref="BoxFace.Bottom"/> and <see cref="BoxFace.Top"/> for
/// the caps, a side face for an uncut run of an edge, and <see langword="null"/> for a face a cut made —
/// a mitre, a chamfer, a rounded corner, a curved edge.
/// </param>
/// <param name="Boundary">The closed boundary, winding outward.</param>
public sealed record SolidFace(BoxFace? Of, ImmutableArray<SolidSegment> Boundary)
{
    /// <summary>Equality by value: <see cref="ImmutableArray{T}"/> compares by identity.</summary>
    public bool Equals(SolidFace? other) => other is not null && Of == other.Of && Boundary.SequenceEqual(other.Boundary);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Of);
        foreach (SolidSegment segment in Boundary)
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }
}

/// <summary>One piece of a face's boundary, in world coordinates.</summary>
/// <param name="From">Where the piece starts.</param>
/// <param name="To">Where the piece ends.</param>
public abstract record SolidSegment(Point3 From, Point3 To);

/// <summary>A straight run: an edge of a cap, or a ruling between the caps.</summary>
/// <param name="From">Where the run starts.</param>
/// <param name="To">Where the run ends.</param>
public sealed record StraightSegment3(Point3 From, Point3 To) : SolidSegment(From, To);

/// <summary>
/// A rounded corner's arc, lying in a cap plane, with its exact centre: <see cref="ArcByCenter"/>
/// placed in space.
/// </summary>
/// <param name="From">Where the arc starts.</param>
/// <param name="To">Where the arc ends.</param>
/// <param name="Center">The centre, in the same cap plane.</param>
public sealed record ArcByCenter3(Point3 From, Point3 To, Point3 Center) : SolidSegment(From, To);

/// <summary>
/// A curved edge's arc, lying in a cap plane, through three exact points: <see cref="ArcThrough"/>
/// placed in space.
/// </summary>
/// <param name="From">Where the arc starts.</param>
/// <param name="Through">The third point the arc passes through, between the two ends.</param>
/// <param name="To">Where the arc ends.</param>
public sealed record ArcThrough3(Point3 From, Point3 Through, Point3 To) : SolidSegment(From, To);

/// <summary>
/// Extrudes a box's local outline along local Z and places it (<c>docs/design/assembly-model.md</c>
/// §4.1).
/// </summary>
/// <remarks>
/// <para>
/// Every point is <see cref="Box.World"/> of an outline point lifted to local z = 0 or
/// <see cref="Box.Depth"/>: an anchor coordinate plus or minus an outline coordinate or the depth,
/// exact for all 24 orientations. The only rounding is the one the outline already has, an odd
/// edge's middle. A rotation that is not a quarter turn — reachable only from the solver — rounds X
/// and Y point by point, as <see cref="Box.Vertex"/> does.
/// </para>
/// <para>
/// One path for every box, as <see cref="OutlineBuilder"/> has one path: a box with no cuts has a
/// four-segment outline, so its solid is the six quads of the box's six faces, each carrying its
/// <see cref="BoxFace"/>, and nothing special-cases it.
/// </para>
/// <para>
/// A cut is square through the cap (§4.2): each side is one outline segment swept straight along
/// local Z, so every side is perpendicular to the caps, and a cut turns with the box because
/// every point goes through the same orientation.
/// </para>
/// </remarks>
internal static class SolidBuilder
{
    /// <summary>The solid of a box: its outline extruded along local Z from 0 to its depth, then oriented.</summary>
    internal static Solid Build(Box box)
    {
        ImmutableArray<OutlineSegment> outline = box.Outline().Segments;
        ImmutableArray<SolidFace>.Builder faces = ImmutableArray.CreateBuilder<SolidFace>(outline.Length + 2);

        // Bottom: walked backwards, each segment reversed, so it winds outward — downward.
        ImmutableArray<SolidSegment>.Builder bottom = ImmutableArray.CreateBuilder<SolidSegment>(outline.Length);
        for (int i = outline.Length - 1; i >= 0; i--)
        {
            bottom.Add(Reversed(Lift(box, outline[i], Length.Zero)));
        }

        faces.Add(new SolidFace(BoxFace.Bottom, bottom.MoveToImmutable()));

        // Top: walked as is. The outline is counter-clockwise seen from local +Z, which is outward.
        ImmutableArray<SolidSegment>.Builder top = ImmutableArray.CreateBuilder<SolidSegment>(outline.Length);
        foreach (OutlineSegment segment in outline)
        {
            top.Add(Lift(box, segment, box.Depth));
        }

        faces.Add(new SolidFace(BoxFace.Top, top.MoveToImmutable()));

        // One side per segment, a₀ b₀ b_D a_D: along the segment at the bottom, up, back along it at
        // the top, down. For a counter-clockwise outline that winds outward — the segment's
        // direction crossed with local +Z points to its right, out of the blank.
        foreach (OutlineSegment segment in outline)
        {
            SolidSegment low = Lift(box, segment, Length.Zero);
            SolidSegment high = Lift(box, segment, box.Depth);
            faces.Add(new SolidFace(
                SideOf(box, segment),
                [
                    low,
                    new StraightSegment3(low.To, high.To),
                    Reversed(high),
                    new StraightSegment3(high.From, low.From),
                ]));
        }

        return new Solid(faces.MoveToImmutable());
    }

    /// <summary>An outline segment lifted to local height <paramref name="z"/> and placed in the world.</summary>
    internal static SolidSegment Lift(Box box, OutlineSegment segment, Length z)
    {
        Point3 At(Point2 local) => box.World(new Vector3(local.X, local.Y, z));

        return segment switch
        {
            StraightSegment straight => new StraightSegment3(At(straight.From), At(straight.To)),
            ArcByCenter arc => new ArcByCenter3(At(arc.From), At(arc.To), At(arc.Center)),
            ArcThrough arc => new ArcThrough3(At(arc.From), At(arc.Through), At(arc.To)),
            _ => throw new InvalidOperationException($"{segment.GetType().Name} is not an outline segment the solid knows how to extrude."),
        };
    }

    /// <summary>The same piece of boundary, walked the other way.</summary>
    internal static SolidSegment Reversed(SolidSegment segment) => segment switch
    {
        StraightSegment3 straight => new StraightSegment3(straight.To, straight.From),
        ArcByCenter3 arc => new ArcByCenter3(arc.To, arc.From, arc.Center),
        ArcThrough3 arc => new ArcThrough3(arc.To, arc.Through, arc.From),
        _ => throw new InvalidOperationException($"{segment.GetType().Name} is not a solid segment the solid knows how to reverse."),
    };

    /// <summary>
    /// The side face an outline segment lies on — a straight run along one of the blank's four
    /// edge lines — or <see langword="null"/> for anything a cut made.
    /// </summary>
    /// <remarks>
    /// Read from where the segment is, not from which cut made it: a corner cut's setbacks are both
    /// positive (shaped-parts §1.6 invariant 7), so its segment is never along an edge line, and an
    /// edge's run — shortened by a cut or an outward curve at either end, or not — always is.
    /// </remarks>
    private static BoxFace? SideOf(Box box, OutlineSegment segment)
    {
        if (segment is not StraightSegment { From: var a, To: var b })
        {
            return null;
        }

        if (a.Y == Length.Zero && b.Y == Length.Zero)
        {
            return BoxFace.South;
        }

        if (a.X == box.Width && b.X == box.Width)
        {
            return BoxFace.East;
        }

        if (a.Y == box.Height && b.Y == box.Height)
        {
            return BoxFace.North;
        }

        if (a.X == Length.Zero && b.X == Length.Zero)
        {
            return BoxFace.West;
        }

        return null;
    }
}
