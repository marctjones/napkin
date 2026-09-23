namespace Napkin.Core.Geometry;

/// <summary>
/// What the plan view sees of a box: the rectangle its eight vertices project to, spun by
/// <see cref="Rotation"/> about <see cref="Anchor"/> (<c>docs/design/assembly-model.md</c> &#xA7;7.1).
/// </summary>
/// <remarks>
/// <para>
/// The plan canvas is the camera looking straight down world −Z, and for every one of the 24
/// orientations a box's shadow on the plan is a rectangle whose sides are the four faces whose
/// world normals are horizontal. The canvas draws, grips, snaps and hit-tests that rectangle
/// without knowing which way the box is turned; only <see cref="FaceAt"/> and
/// <see cref="UprightAt"/> translate a plan name back into the box's local frame, because for a
/// tipped box the plan's south-west corner is not the blank's.
/// </para>
/// <para>
/// Everything here is exact for a quarter-turn <see cref="Rotation"/>: the extents are stored sizes
/// selected by a signed permutation, and a plan corner is the anchor plus a swap-and-negate of
/// them. Plan names — <see cref="BoxEdge"/> sides, <see cref="BoxCorner"/> corners — are in the
/// footprint's own frame, before the spin, the same way a <see cref="Box"/>'s were before
/// assembly-model.
/// </para>
/// </remarks>
/// <param name="Anchor">The box's anchor's X and Y, verbatim.</param>
/// <param name="LowCorner">Where the rectangle's south-west corner is, relative to <paramref name="Anchor"/>, before the spin.</param>
/// <param name="PlanWidth">The rectangle's size along plan X, before the spin.</param>
/// <param name="PlanHeight">The rectangle's size along plan Y, before the spin.</param>
/// <param name="Rotation">The spin about world Z, the box's own.</param>
public sealed record Footprint(
    Point2 Anchor,
    Vector2 LowCorner,
    Length PlanWidth,
    Length PlanHeight,
    Angle Rotation)
{
    private static readonly BoxFace[] Faces =
        [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];

    /// <summary>
    /// Which local face of the box points up — what <see cref="FaceAt"/> and
    /// <see cref="UprightAt"/> need to name the box's own faces. <see cref="BoxFace.Top"/> unless
    /// said otherwise, so that a footprint written out by hand for a box as drawn is the one the box
    /// derives.
    /// </summary>
    public BoxFace FaceUp { get; init; } = BoxFace.Top;

    /// <summary>
    /// The footprint of <paramref name="box"/>: the bounding rectangle, before the spin, of its eight
    /// vertices tipped by <see cref="Box.FaceUp"/>. Derived from the orientation rather than written
    /// out as a table, so that the table in &#xA7;7.1 is something the tests check rather than
    /// something this could copy wrong.
    /// </summary>
    public static Footprint Of(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        Orientation tip = new(box.FaceUp, Angle.Zero);
        Length? lowX = null, highX = null, lowY = null, highY = null;

        foreach (Length x in (Length[])[Length.Zero, box.Width])
        {
            foreach (Length y in (Length[])[Length.Zero, box.Height])
            {
                foreach (Length z in (Length[])[Length.Zero, box.Depth])
                {
                    Vector3 world = tip.Apply(new Vector3(x, y, z));
                    lowX = lowX is { } lx ? Length.Min(lx, world.Dx) : world.Dx;
                    highX = highX is { } hx ? Length.Max(hx, world.Dx) : world.Dx;
                    lowY = lowY is { } ly ? Length.Min(ly, world.Dy) : world.Dy;
                    highY = highY is { } hy ? Length.Max(hy, world.Dy) : world.Dy;
                }
            }
        }

        return new Footprint(
            box.Anchor.XY,
            new Vector2(lowX!.Value, lowY!.Value),
            highX!.Value - lowX.Value,
            highY!.Value - lowY.Value,
            box.Rotation)
        {
            FaceUp = box.FaceUp,
        };
    }

    /// <summary>
    /// Which local face of the box the plan sees as <paramref name="planSide"/>: the face whose
    /// outward normal, once the box is tipped, points that way in the plan. For
    /// <see cref="BoxFace.Top"/> up it is the side of the same name; for
    /// <see cref="BoxFace.East"/> up the plan's west side is the blank's top.
    /// </summary>
    public BoxFace FaceAt(BoxEdge planSide)
    {
        (Axis axis, bool positive) = planSide switch
        {
            BoxEdge.South => (Axis.Y, false),
            BoxEdge.East => (Axis.X, true),
            BoxEdge.North => (Axis.Y, true),
            BoxEdge.West => (Axis.X, false),
            _ => throw new ArgumentOutOfRangeException(nameof(planSide), planSide, "Not a plan side."),
        };

        Orientation tip = new(FaceUp, Angle.Zero);
        foreach (BoxFace face in Faces)
        {
            if (tip.Normal(face) == (axis, positive))
            {
                return face;
            }
        }

        throw new InvalidOperationException($"No face of a box {FaceUp} up faces {planSide}; the tip table is not a set of rotations.");
    }

    /// <summary>
    /// The edge of the box that stands vertical at a corner of the footprint — a <em>plan</em>
    /// upright, which is the local-Z edge <see cref="BoxFeature.LocalUpright"/> only when
    /// <see cref="BoxFace.Top"/> is up.
    /// </summary>
    public BoxFeature UprightAt(BoxCorner planCorner)
    {
        (BoxEdge northSouth, BoxEdge eastWest) = planCorner switch
        {
            BoxCorner.SouthWest => (BoxEdge.South, BoxEdge.West),
            BoxCorner.SouthEast => (BoxEdge.South, BoxEdge.East),
            BoxCorner.NorthEast => (BoxEdge.North, BoxEdge.East),
            BoxCorner.NorthWest => (BoxEdge.North, BoxEdge.West),
            _ => throw new ArgumentOutOfRangeException(nameof(planCorner), planCorner, "Not a plan corner."),
        };

        return BoxFeature.Edge(FaceAt(northSouth), FaceAt(eastWest));
    }

    /// <summary>
    /// The side of the footprint a local face is seen as, or <see langword="null"/> when its normal
    /// is vertical — <see cref="BoxFace.Top"/> and <see cref="BoxFace.Bottom"/> of a box lying as
    /// drawn, which the plan sees face on rather than edge on.
    /// </summary>
    public BoxEdge? SideOf(BoxFace face)
    {
        foreach (BoxEdge side in (BoxEdge[])[BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West])
        {
            if (FaceAt(side) == face)
            {
                return side;
            }
        }

        return null;
    }

    /// <summary>The displacement from the anchor to a footprint corner, before the spin.</summary>
    public Vector2 LocalOffset(BoxCorner planCorner) => LowCorner + planCorner switch
    {
        BoxCorner.SouthWest => Vector2.Zero,
        BoxCorner.SouthEast => new Vector2(PlanWidth, Length.Zero),
        BoxCorner.NorthEast => new Vector2(PlanWidth, PlanHeight),
        BoxCorner.NorthWest => new Vector2(Length.Zero, PlanHeight),
        _ => throw new ArgumentOutOfRangeException(nameof(planCorner), planCorner, "Not a plan corner."),
    };

    /// <summary>
    /// Where a footprint corner is in the plan. Exact when <see cref="Rotation"/> is a right-angle
    /// multiple.
    /// </summary>
    public Point2 Corner(BoxCorner planCorner) => Anchor + LocalOffset(planCorner).Rotate(Rotation);

    /// <summary>
    /// The middle of the footprint. Rounds by at most half a unit when a side is an odd number of
    /// units.
    /// </summary>
    public Point2 Center => Anchor + (LowCorner + new Vector2(
        PlanWidth.Divide(2, Rounding.HalfToEven),
        PlanHeight.Divide(2, Rounding.HalfToEven))).Rotate(Rotation);

    /// <summary>
    /// A plan point in the footprint's own frame: measured from its south-west corner, before the
    /// spin, so that the rectangle is <c>[0, PlanWidth] × [0, PlanHeight]</c>.
    /// </summary>
    public Vector2 ToLocal(Point2 point) => (point - Anchor).Rotate(-Rotation) - LowCorner;

    /// <summary>A displacement in the plan, in the footprint's own frame: un-spun.</summary>
    public Vector2 ToLocal(Vector2 displacement) => displacement.Rotate(-Rotation);

    /// <summary>A point in the footprint's own frame, back in the plan.</summary>
    public Point2 FromLocal(Vector2 local) => Anchor + (LowCorner + local).Rotate(Rotation);
}
