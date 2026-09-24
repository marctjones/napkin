namespace Napkin.Core.Geometry;

/// <summary>
/// Where two parts touch: the rectangle their two joined faces share (<c>docs/design/joinery-and-fasteners.md</c>
/// &#xA7;4.2). Derived from the two boxes each time; never stored.
/// </summary>
/// <param name="Normal">The world axis the two faces are perpendicular to.</param>
/// <param name="Low">The rectangle's least corner; on <paramref name="Normal"/> it is the plane both faces lie in.</param>
/// <param name="High">The rectangle's greatest corner; equal to <paramref name="Low"/> on <paramref name="Normal"/>.</param>
public sealed record JointContact(Axis Normal, Point3 Low, Point3 High)
{
    /// <summary>The rectangle's size along one world axis; zero along <see cref="Normal"/>.</summary>
    public Length Extent(Axis axis) => High.Component(axis) - Low.Component(axis);

    /// <summary>The joint length: the rectangle's long side (&#xA7;4.2).</summary>
    public Length JointLength => Length.Max(Extent(Axis.X), Length.Max(Extent(Axis.Y), Extent(Axis.Z)));

    /// <summary>The rectangle's short side.</summary>
    public Length JointWidth
    {
        get
        {
            Length[] sides = [.. new[] { Axis.X, Axis.Y, Axis.Z }.Where(axis => axis != Normal).Select(Extent).Order()];
            return sides[0];
        }
    }

    /// <summary>The rectangle's centre, where a joint's marker is drawn (&#xA7;5.2).</summary>
    public Point3 Centre => new(
        RelationshipChecker.Midpoint(Low.X, High.X),
        RelationshipChecker.Midpoint(Low.Y, High.Y),
        RelationshipChecker.Midpoint(Low.Z, High.Z));
}

/// <summary>What a joint's two boxes say about it, derived (&#xA7;4.2, &#xA7;4.3).</summary>
public static class JointGeometry
{
    /// <summary>
    /// The contact rectangle of a joint's two faces: non-empty (positive area) and coplanar. Null
    /// when the parts do not touch there — the faces are in different planes, or the same plane
    /// with no overlap, or a box is off the quarter turns — which is what makes a joint
    /// <em>unsatisfied</em> rather than refused.
    /// </summary>
    public static JointContact? Contact(Sketch sketch, Joint joint)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        if (sketch.Find<Box>(joint.Receiving.Box) is not { } receiving
            || sketch.Find<Box>(joint.Inserted.Box) is not { } inserted
            || joint.Receiving.Feature.Faces is not [var receivingFace]
            || joint.Inserted.Feature.Faces is not [var insertedFace]
            || !receiving.Orientation.IsExact
            || !inserted.Orientation.IsExact)
        {
            return null;
        }

        (Axis axisA, Length planeA, Point3 lowA, Point3 highA) = FaceRectangle(receiving, receivingFace);
        (Axis axisB, Length planeB, Point3 lowB, Point3 highB) = FaceRectangle(inserted, insertedFace);
        if (axisA != axisB || planeA != planeB)
        {
            return null;
        }

        Point3 low = new(Length.Max(lowA.X, lowB.X), Length.Max(lowA.Y, lowB.Y), Length.Max(lowA.Z, lowB.Z));
        Point3 high = new(Length.Min(highA.X, highB.X), Length.Min(highA.Y, highB.Y), Length.Min(highA.Z, highB.Z));
        foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
        {
            if (axis != axisA && high.Component(axis) <= low.Component(axis))
            {
                return null;
            }
        }

        return new JointContact(axisA, low, high);
    }

    /// <summary>
    /// Whether the joint's two parts still touch and its numbers still make sense: a non-empty
    /// contact, a depth less than the receiving part's thickness, and (for a half-lap) two parts of
    /// the same thickness (&#xA7;4.3).
    /// </summary>
    public static bool IsSatisfied(Sketch sketch, Joint joint)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        if (Contact(sketch, joint) is null)
        {
            return false;
        }

        Box receiving = sketch.Find<Box>(joint.Receiving.Box)!;
        Box inserted = sketch.Find<Box>(joint.Inserted.Box)!;
        Length receivingThickness = ThicknessAcross(receiving, joint.Receiving.Feature.Faces[0]);

        if (joint.Depth is { } depth && depth >= receivingThickness)
        {
            return false;
        }

        return joint.Type != JointType.HalfLap
               || receivingThickness == ThicknessAcross(inserted, joint.Inserted.Feature.Faces[0]);
    }

    /// <summary>How thick a box is across one of its faces: its own size along that face's normal.</summary>
    public static Length ThicknessAcross(Box box, BoxFace face)
    {
        ArgumentNullException.ThrowIfNull(box);

        return face switch
        {
            BoxFace.South or BoxFace.North => box.Height,
            BoxFace.East or BoxFace.West => box.Width,
            _ => box.Depth,
        };
    }

    // A face of a box in the world: the axis it is perpendicular to, the coordinate of its plane,
    // and the rectangle it covers. For the 24 orientations the six faces lie on the six planes of
    // the box's world extent.
    private static (Axis Normal, Length Plane, Point3 Low, Point3 High) FaceRectangle(Box box, BoxFace face)
    {
        Point3? low = null, high = null;
        foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            foreach (BoxLevel level in (BoxLevel[])[BoxLevel.Bottom, BoxLevel.Top])
            {
                Point3 vertex = box.Vertex(corner, level);
                low = low is { } l ? new Point3(Length.Min(l.X, vertex.X), Length.Min(l.Y, vertex.Y), Length.Min(l.Z, vertex.Z)) : vertex;
                high = high is { } h ? new Point3(Length.Max(h.X, vertex.X), Length.Max(h.Y, vertex.Y), Length.Max(h.Z, vertex.Z)) : vertex;
            }
        }

        (Axis normal, bool positive) = box.Orientation.Normal(face);
        Length plane = positive ? high!.Value.Component(normal) : low!.Value.Component(normal);
        return (normal, plane, low!.Value, high!.Value);
    }
}
