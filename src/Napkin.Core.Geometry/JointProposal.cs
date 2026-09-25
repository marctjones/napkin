using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>What the join tool proposes for two parts a person has selected (&#xA7;5.1).</summary>
/// <param name="Faces">The pair of faces the joint would be between: the largest contact, facing each other.</param>
/// <param name="Roles">Which part receives and which is inserted, and whether the rule could not decide.</param>
/// <param name="Type">The type the contact suggests.</param>
/// <param name="PocketFaces">The faces of the inserted part pocket holes could be drilled from, nearest the middle of everything first; two when they tie.</param>
public sealed record JointProposal(TouchingFaces Faces, JointRoles Roles, JointType Type, ImmutableArray<BoxFace> PocketFaces)
{
    /// <summary>Whether two pocket faces are equally near the middle, so the person is to pick (&#xA7;6.2).</summary>
    public bool PocketFaceTied => PocketFaces.Length > 1;

    /// <summary>The proposed pocket face: the nearest to the middle, or the first of a tie.</summary>
    public BoxFace PocketFace => PocketFaces[0];
}

/// <summary>
/// What napkin suggests when two parts are to be joined: which faces, which type, who receives and
/// where pocket holes would go. Only suggestions &#x2014; the person's popover changes any of it.
/// </summary>
public static class JointProposals
{
    /// <summary>
    /// The proposal for two parts, or null when they do not touch. A pair of faces facing each other is
    /// preferred, the largest contact first; failing that, overlapping parts of one thickness lap.
    /// </summary>
    /// <param name="sketch">The design.</param>
    /// <param name="first">One part.</param>
    /// <param name="second">The other.</param>
    public static JointProposal? For(Sketch sketch, EntityId first, EntityId second)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        ImmutableArray<TouchingFaces> pairs = JointGeometry.TouchingFaces(sketch, first, second);
        if (pairs.IsEmpty || sketch.Find<Box>(first) is not { } a || sketch.Find<Box>(second) is not { } b)
        {
            return null;
        }

        TouchingFaces? facing = pairs
            .Where(pair => a.Orientation.Normal(pair.OfFirst) is var na && b.Orientation.Normal(pair.OfSecond) is var nb
                           && na.Axis == nb.Axis && na.Positive != nb.Positive)
            .OrderByDescending(pair => Area(pair.Contact))
            .Select(pair => (TouchingFaces?)pair)
            .FirstOrDefault();

        TouchingFaces chosen = facing ?? pairs.OrderByDescending(pair => Area(pair.Contact)).First();
        JointType type = SuggestType(sketch, a, b, chosen, facing is null);

        FeatureRef ofFirst = new(first, BoxFeature.Face(chosen.OfFirst));
        FeatureRef ofSecond = new(second, BoxFeature.Face(chosen.OfSecond));
        JointRoles roles = JointGeometry.ProposeRoles(sketch, type, ofFirst, ofSecond)!;

        Box inserted = sketch.Find<Box>(roles.Inserted.Box)!;
        return new JointProposal(chosen, roles, type, PocketFacesFor(sketch, inserted, roles.Inserted.Feature.Faces[0]));
    }

    private static Int128 Area(JointContact contact) => (Int128)contact.JointLength.Units * contact.JointWidth.Units;

    /// <summary>
    /// The type a contact suggests (&#xA7;5.1): overlapping parts of one thickness lap; a bottom face on a long thin
    /// contact is a tabletop; a part thinner than the one it meets, meeting it away from at least
    /// one of the receiving face's edges on both sides, sits in a groove; otherwise a butt.
    /// </summary>
    private static JointType SuggestType(Sketch sketch, Box a, Box b, TouchingFaces pair, bool notFacing)
    {
        if (notFacing)
        {
            Joint lap = Trial(a, pair.OfFirst, b, pair.OfSecond, JointType.HalfLap);
            return JointGeometry.IsSatisfied(sketch, lap) ? JointType.HalfLap : JointType.Butt;
        }

        // A tabletop: one part's bottom face on the other's top, the contact long and thin like an apron's edge.
        // "Bottom" is the face pointing down in the drawing, not the part's own bottom: an apron turned on edge has one end that is its own.
        if ((a.Orientation.Normal(pair.OfFirst) == (Axis.Z, false) || b.Orientation.Normal(pair.OfSecond) == (Axis.Z, false))
            && pair.Contact.JointLength.Units >= 4 * pair.Contact.JointWidth.Units)
        {
            return JointType.Tabletop;
        }

        // A groove: the thinner part enters the thicker one's face with room to spare on both sides of it
        // in some direction (a bottom in a side), not butted to its edge (an apron's end on a leg).
        (Box Inserted, BoxFace Face, Box Receiving)[] ways = [(a, pair.OfFirst, b), (b, pair.OfSecond, a)];
        foreach ((Box inserted, BoxFace face, Box receiving) in ways)
        {
            // The thinner part's broad face on the other's is a butt (a drawer box front on a drawer front).
            if (Thickness(inserted) < Thickness(receiving)
                && JointGeometry.LocalAxisOf(face) != ThicknessAxis(inserted)
                && Inside(receiving, pair.Contact))
            {
                return JointType.Groove;
            }
        }

        return JointType.Butt;
    }

    // The local axis a part is thin along: from its part, else its smallest size.
    private static Axis ThicknessAxis(Box box)
    {
        if (box.Part is { } part)
        {
            return part.PlanAxes.X == PartDimension.Thickness ? Axis.X : part.PlanAxes.Y == PartDimension.Thickness ? Axis.Y : Axis.Z;
        }

        return box.Width <= box.Height && box.Width <= box.Depth ? Axis.X : box.Height <= box.Depth ? Axis.Y : Axis.Z;
    }

    private static Length Thickness(Box box)
        => box.Part is { } part ? part.SizeOn(box).Thickness : Length.Min(box.Width, Length.Min(box.Height, box.Depth));

    // The contact has margin on both sides of it, along at least one direction across the receiving face.
    private static bool Inside(Box receiving, JointContact contact)
    {
        (Point3 low, Point3 high) = JointGeometry.Extent(receiving);
        foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
        {
            if (axis != contact.Normal
                && contact.Low.Component(axis) > low.Component(axis)
                && contact.High.Component(axis) < high.Component(axis))
            {
                return true;
            }
        }

        return false;
    }

    private static Joint Trial(Box a, BoxFace faceA, Box b, BoxFace faceB, JointType type) => new(
        new RelationshipId(Guid.Empty),
        new FeatureRef(a.Id, BoxFeature.Face(faceA)),
        new FeatureRef(b.Id, BoxFeature.Face(faceB)),
        type,
        null,
        Fastening.None,
        false);

    /// <summary>
    /// The faces pocket holes could be drilled from, best first (&#xA7;6.2): the inserted part's two broad faces
    /// (those across its thickness) that are neither its contact face nor the opposite of it, the one
    /// nearer the middle of everything drawn first; both, in low-face-first order, when they tie.
    /// </summary>
    /// <param name="sketch">The design.</param>
    /// <param name="inserted">The inserted part.</param>
    /// <param name="contactFace">Its face in the joint.</param>
    public static ImmutableArray<BoxFace> PocketFacesFor(Sketch sketch, Box inserted, BoxFace contactFace)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(inserted);

        BoxFace[] all = [BoxFace.West, BoxFace.South, BoxFace.Bottom, BoxFace.East, BoxFace.North, BoxFace.Top];
        BoxFace opposite = JointRules.Opposite(contactFace);
        Axis thicknessAxis = ThicknessAxis(inserted);

        BoxFace[] candidates =
        [
            .. all.Where(face => face != contactFace && face != opposite
                                 && JointGeometry.LocalAxisOf(face) == thicknessAxis),
        ];
        if (candidates.Length == 0)
        {
            candidates = [.. all.Where(face => face != contactFace && face != opposite)];
        }

        Point3 middle = JointGeometry.CentreOfEverything(sketch, inserted);
        Length nearest = candidates.Min(face => JointGeometry.DistanceFromPoint(inserted, face, middle));
        return [.. candidates.Where(face => JointGeometry.DistanceFromPoint(inserted, face, middle) == nearest)];
    }
}
