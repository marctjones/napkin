using System.Collections.Immutable;

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

/// <summary>Which way a groove runs in its receiving part (&#xA7;4.2): along its length, or across its width, which is a dado.</summary>
public enum GrooveDirection
{
    /// <summary>Along the receiving part's length: a groove (a drawer bottom in a drawer side).</summary>
    AlongLength,

    /// <summary>Across the receiving part's width: a dado (a shelf into a side).</summary>
    AcrossWidth,
}

/// <summary>A groove or dado as the two boxes place it (&#xA7;4.2). Faces are the receiving part's own, in its local frame.</summary>
/// <param name="Direction">Along the length (a groove) or across the width (a dado).</param>
/// <param name="Width">How wide the slot is: the inserted part's thickness.</param>
/// <param name="Depth">How deep it is: the joint's depth.</param>
/// <param name="Offset">The distance from the slot to the nearer edge of the receiving face that runs parallel to it.</param>
/// <param name="OffsetFrom">The receiving part's face that edge is shared with; the offset is "from the <c>OffsetFrom</c> edge".</param>
public sealed record GrooveShape(GrooveDirection Direction, Length Width, Length Depth, Length Offset, BoxFace OffsetFrom);

/// <summary>A rabbet at the end of the receiving part (&#xA7;4.2).</summary>
/// <param name="Width">How wide the step is, along the receiving part's length: the inserted part's thickness.</param>
/// <param name="Depth">How deep it is: the joint's depth.</param>
/// <param name="End">The receiving part's end face the step is cut at, in its local frame.</param>
public sealed record RabbetShape(Length Width, Length Depth, BoxFace End);

/// <summary>How far one part of a half-lap is lapped along its own length.</summary>
/// <param name="Long">The overlap's extent along the part's length.</param>
/// <param name="Offset">The distance from the overlap to the nearer end of the part: zero when the lap is at an end.</param>
/// <param name="Nearest">The part's end face (local frame) the offset is measured from.</param>
public sealed record LapPart(Length Long, Length Offset, BoxFace Nearest);

/// <summary>A half-lap: the overlap box of the two drawn boxes, and how it sits on each part (&#xA7;4.2).</summary>
/// <param name="Low">The overlap box's least corner, in the world.</param>
/// <param name="High">The overlap box's greatest corner.</param>
/// <param name="Receiving">The lap on the receiving part.</param>
/// <param name="Inserted">The lap on the inserted part.</param>
/// <param name="Depth">How much is cut from each part: half its thickness.</param>
public sealed record LapShape(Point3 Low, Point3 High, LapPart Receiving, LapPart Inserted, Length Depth);

/// <summary>Everything a joint's two boxes imply, derived and never stored (&#xA7;4.2).</summary>
/// <param name="Contact">The contact rectangle.</param>
/// <param name="Groove">The groove or dado, for a <see cref="JointType.Groove"/>.</param>
/// <param name="Rabbet">The rabbet, for a <see cref="JointType.Rabbet"/>.</param>
/// <param name="Lap">The half-lap, for a <see cref="JointType.HalfLap"/>.</param>
/// <param name="PocketEnd">For pocket screws: the end face of the inserted part that is in the contact; null when its contact is a long face.</param>
public sealed record JointShape(JointContact Contact, GrooveShape? Groove, RabbetShape? Rabbet, LapShape? Lap, BoxFace? PocketEnd)
{
    /// <summary>The joint length the count rules read: the contact's long side.</summary>
    public Length JointLength => Contact.JointLength;

    /// <summary>The contact's short side.</summary>
    public Length JointWidth => Contact.JointWidth;
}

/// <summary>Which part receives and which is inserted, as the tool proposes them (&#xA7;4.1).</summary>
/// <param name="Receiving">The face of the receiving part.</param>
/// <param name="Inserted">The face of the inserted part.</param>
/// <param name="Ambiguous">Whether the rule could not decide and the tool should ask, defaulting to the larger part as receiving.</param>
public sealed record JointRoles(FeatureRef Receiving, FeatureRef Inserted, bool Ambiguous);

/// <summary>A pair of faces, one of each of two parts, that share a rectangle.</summary>
/// <param name="OfFirst">The first part's face.</param>
/// <param name="OfSecond">The second part's face.</param>
/// <param name="Contact">The rectangle they share.</param>
public sealed record TouchingFaces(BoxFace OfFirst, BoxFace OfSecond, JointContact Contact);

/// <summary>What a joint's two boxes say about it, derived (&#xA7;4.2, &#xA7;4.3).</summary>
public static class JointGeometry
{
    private static readonly BoxFace[] AllFaces = [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];

    private static readonly Axis[] AllAxes = [Axis.X, Axis.Y, Axis.Z];

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

        return sketch.Find<Box>(joint.Receiving.Box) is { } receiving
               && sketch.Find<Box>(joint.Inserted.Box) is { } inserted
               && joint.Receiving.Feature.Faces is [var receivingFace]
               && joint.Inserted.Feature.Faces is [var insertedFace]
            ? ContactOf(receiving, receivingFace, inserted, insertedFace)
            : null;
    }

    /// <summary>
    /// Every pair of faces, one of each of two parts, that share a rectangle: the ways the two
    /// could be joined. One pair means the tool can open its popover at once; none means the parts
    /// do not touch (&#xA7;5.1).
    /// </summary>
    public static ImmutableArray<TouchingFaces> TouchingFaces(Sketch sketch, EntityId first, EntityId second)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        if (first == second || sketch.Find<Box>(first) is not { } a || sketch.Find<Box>(second) is not { } b)
        {
            return [];
        }

        ImmutableArray<TouchingFaces>.Builder pairs = ImmutableArray.CreateBuilder<TouchingFaces>();
        foreach (BoxFace ofA in AllFaces)
        {
            foreach (BoxFace ofB in AllFaces)
            {
                if (ContactOf(a, ofA, b, ofB) is { } contact)
                {
                    pairs.Add(new TouchingFaces(ofA, ofB, contact));
                }
            }
        }

        return pairs.ToImmutable();
    }

    /// <summary>
    /// Whether the joint's two parts still touch and its numbers still make sense: a non-empty
    /// contact, a depth less than the receiving part's thickness, and (for a half-lap) two parts of
    /// the same thickness whose overlap has volume (&#xA7;4.3).
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
               || (receivingThickness == ThicknessAcross(inserted, joint.Inserted.Feature.Faces[0])
                   && Overlap(receiving, inserted) is not null);
    }

    /// <summary>
    /// Everything a joint's two boxes imply, or null when they do not touch there. Derived every time
    /// from the drawn boxes and never stored: resize the drawer bottom and the groove offset follows
    /// (&#xA7;4.2). Faces are named in each part's own frame; lengths are measured in the world.
    /// </summary>
    public static JointShape? Of(Sketch sketch, Joint joint)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        if (Contact(sketch, joint) is not { } contact
            || sketch.Find<Box>(joint.Receiving.Box) is not { } receiving
            || sketch.Find<Box>(joint.Inserted.Box) is not { } inserted
            || joint.Receiving.Feature.Faces is not [var receivingFace]
            || joint.Inserted.Feature.Faces is not [var insertedFace])
        {
            return null;
        }

        return new JointShape(
            contact,
            joint.Type == JointType.Groove ? GrooveOf(contact, receiving, joint) : null,
            joint.Type == JointType.Rabbet ? RabbetOf(contact, receiving, joint) : null,
            joint.Type == JointType.HalfLap ? LapOf(receiving, inserted, receivingFace) : null,
            joint.Fastening.Kind == FasteningKind.PocketScrews && IsEnd(inserted, insertedFace) ? insertedFace : null);
    }

    /// <summary>
    /// Which part receives and which is inserted, as the tool proposes them for two faces the person
    /// has touching (&#xA7;4.1). For a butt or a rabbet the inserted part is the one whose contact face
    /// is an end; for a groove the thinner one; a half-lap does not care and stores the lower id
    /// first; for a tabletop the receiving part is the top, whose bottom face is in the contact.
    /// When the rule cannot decide (both ends or neither, equal thickness, both or neither a bottom
    /// face) the larger part receives and <see cref="JointRoles.Ambiguous"/> says to ask.
    /// </summary>
    /// <returns>Null when either face is not one face of a box in the sketch.</returns>
    public static JointRoles? ProposeRoles(Sketch sketch, JointType type, FeatureRef first, FeatureRef second)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (sketch.Find<Box>(first.Box) is not { } a
            || sketch.Find<Box>(second.Box) is not { } b
            || first.Feature.Faces is not [var faceA]
            || second.Feature.Faces is not [var faceB])
        {
            return null;
        }

        // The rule's verdict: whether the first (true) or the second (false) is inserted, or undecided (null).
        bool? firstIsInserted = type switch
        {
            JointType.Butt or JointType.Rabbet => IsEnd(a, faceA) != IsEnd(b, faceB) ? IsEnd(a, faceA) : null,
            JointType.Groove => Thickness(a) != Thickness(b) ? Thickness(a) < Thickness(b) : null,
            JointType.Tabletop => (faceA == BoxFace.Bottom) != (faceB == BoxFace.Bottom) ? faceB == BoxFace.Bottom : null,
            _ => first.Box.CompareTo(second.Box) > 0,
        };

        bool insertFirst = firstIsInserted
            ?? (Volume(a) < Volume(b) || (Volume(a) == Volume(b) && first.Box.CompareTo(second.Box) > 0));
        return insertFirst
            ? new JointRoles(second, first, firstIsInserted is null)
            : new JointRoles(first, second, firstIsInserted is null);
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

    private static JointContact? ContactOf(Box receiving, BoxFace receivingFace, Box inserted, BoxFace insertedFace)
    {
        if (!receiving.Orientation.IsExact || !inserted.Orientation.IsExact)
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
        foreach (Axis axis in AllAxes)
        {
            if (axis != axisA && high.Component(axis) <= low.Component(axis))
            {
                return null;
            }
        }

        return new JointContact(axisA, low, high);
    }

    // The overlap of the two boxes' world extents, or null unless it has volume: what a half-lap is.
    private static (Point3 Low, Point3 High)? Overlap(Box first, Box second)
    {
        if (!first.Orientation.IsExact || !second.Orientation.IsExact)
        {
            return null;
        }

        (Point3 lowA, Point3 highA) = Extent(first);
        (Point3 lowB, Point3 highB) = Extent(second);
        Point3 low = new(Length.Max(lowA.X, lowB.X), Length.Max(lowA.Y, lowB.Y), Length.Max(lowA.Z, lowB.Z));
        Point3 high = new(Length.Min(highA.X, highB.X), Length.Min(highA.Y, highB.Y), Length.Min(highA.Z, highB.Z));
        return AllAxes.All(axis => high.Component(axis) > low.Component(axis)) ? (low, high) : null;
    }

    private static GrooveShape GrooveOf(JointContact contact, Box receiving, Joint joint)
    {
        Axis[] flat = [.. AllAxes.Where(axis => axis != contact.Normal)];
        Axis lengthWorld = WorldAxis(receiving, LengthAxis(receiving));

        // The slot runs along the contact's long side; a tie runs along the receiving part's length.
        Axis along = contact.Extent(flat[0]) == contact.Extent(flat[1])
            ? (flat.Contains(lengthWorld) ? lengthWorld : flat[0])
            : (contact.Extent(flat[0]) > contact.Extent(flat[1]) ? flat[0] : flat[1]);
        Axis across = flat[0] == along ? flat[1] : flat[0];

        (Point3 low, Point3 high) = Extent(receiving);
        Length fromLow = contact.Low.Component(across) - low.Component(across);
        Length fromHigh = high.Component(across) - contact.High.Component(across);
        bool nearerLow = fromLow <= fromHigh;

        return new GrooveShape(
            along == lengthWorld ? GrooveDirection.AlongLength : GrooveDirection.AcrossWidth,
            contact.Extent(across),
            joint.Depth ?? Length.Zero,
            nearerLow ? fromLow : fromHigh,
            FaceFacing(receiving, across, positive: !nearerLow));
    }

    private static RabbetShape? RabbetOf(JointContact contact, Box receiving, Joint joint)
    {
        Axis lengthWorld = WorldAxis(receiving, LengthAxis(receiving));
        if (lengthWorld == contact.Normal)
        {
            return null;    // the face is an end, so there is no end for the step to be at
        }

        (Point3 low, Point3 high) = Extent(receiving);
        Length fromLow = contact.Low.Component(lengthWorld) - low.Component(lengthWorld);
        Length fromHigh = high.Component(lengthWorld) - contact.High.Component(lengthWorld);

        return new RabbetShape(
            contact.Extent(lengthWorld),
            joint.Depth ?? Length.Zero,
            FaceFacing(receiving, lengthWorld, positive: fromLow > fromHigh));
    }

    private static LapShape? LapOf(Box receiving, Box inserted, BoxFace receivingFace)
    {
        if (Overlap(receiving, inserted) is not { } overlap)
        {
            return null;
        }

        return new LapShape(
            overlap.Low,
            overlap.High,
            LapOn(receiving, overlap),
            LapOn(inserted, overlap),
            RelationshipChecker.Midpoint(Length.Zero, ThicknessAcross(receiving, receivingFace)));
    }

    private static LapPart LapOn(Box box, (Point3 Low, Point3 High) overlap)
    {
        Axis lengthWorld = WorldAxis(box, LengthAxis(box));
        (Point3 low, Point3 high) = Extent(box);
        Length fromLow = overlap.Low.Component(lengthWorld) - low.Component(lengthWorld);
        Length fromHigh = high.Component(lengthWorld) - overlap.High.Component(lengthWorld);
        bool nearerLow = fromLow <= fromHigh;
        return new LapPart(
            overlap.High.Component(lengthWorld) - overlap.Low.Component(lengthWorld),
            nearerLow ? fromLow : fromHigh,
            FaceFacing(box, lengthWorld, positive: !nearerLow));
    }

    // The local axis a part's length lies along: from its part when it has one, else its longest size (X before Y before Z).
    private static Axis LengthAxis(Box box)
    {
        if (box.Part is { } part)
        {
            return part.PlanAxes.X == PartDimension.Length ? Axis.X
                : part.PlanAxes.Y == PartDimension.Length ? Axis.Y
                : Axis.Z;
        }

        return box.Width >= box.Height && box.Width >= box.Depth ? Axis.X
            : box.Height >= box.Depth ? Axis.Y
            : Axis.Z;
    }

    private static Axis LocalAxisOf(BoxFace face) => face switch
    {
        BoxFace.South or BoxFace.North => Axis.Y,
        BoxFace.East or BoxFace.West => Axis.X,
        _ => Axis.Z,
    };

    // A face is an end when it is perpendicular to the part's length.
    private static bool IsEnd(Box box, BoxFace face) => LocalAxisOf(face) == LengthAxis(box);

    private static Axis WorldAxis(Box box, Axis local) => box.Orientation.Image(local).Axis;

    private static Length Thickness(Box box)
        => box.Part is { } part ? part.SizeOn(box).Thickness : Length.Min(box.Width, Length.Min(box.Height, box.Depth));

    private static Int128 Volume(Box box) => (Int128)box.Width.Units * box.Height.Units * box.Depth.Units;

    // The face of the box that faces along a world axis, the given way.
    private static BoxFace FaceFacing(Box box, Axis axis, bool positive)
        => AllFaces.Single(face => box.Orientation.Normal(face) == (axis, positive));

    // The box's world extent: for the 24 orientations its six faces lie on the six planes of it.
    private static (Point3 Low, Point3 High) Extent(Box box)
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

        return (low!.Value, high!.Value);
    }

    // A face of a box in the world: the axis it is perpendicular to, the coordinate of its plane, and the box's extent.
    private static (Axis Normal, Length Plane, Point3 Low, Point3 High) FaceRectangle(Box box, BoxFace face)
    {
        (Point3 low, Point3 high) = Extent(box);
        (Axis normal, bool positive) = box.Orientation.Normal(face);
        return (normal, positive ? high.Component(normal) : low.Component(normal), low, high);
    }
}
