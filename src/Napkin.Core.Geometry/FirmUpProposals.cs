using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Which of the two kinds of relationship Firm up proposes (<c>docs/design/sketch-mode.md</c> &#xA7;3.2).</summary>
public enum FirmUpKind
{
    /// <summary>Two faces facing each other, touching over an area: one part set against the other.</summary>
    Against,

    /// <summary>Two faces of touching parts facing the same way, in one plane: their fronts flush.</summary>
    Alongside,
}

/// <summary>One relationship Firm up proposes, and the sentence the popover lists it with.</summary>
/// <param name="Relationship">The <see cref="Flush"/> to add; it holds as the parts stand, so adding it moves nothing.</param>
/// <param name="Kind">Against or alongside.</param>
/// <param name="Sentence">What it says, in the words a person reads: "Top's south face against Leg 1's north face".</param>
public sealed record FirmUpProposal(Relationship Relationship, FirmUpKind Kind, string Sentence);

/// <summary>
/// The relationships a rough sketch's touching parts imply and it does not yet hold
/// (<c>docs/design/sketch-mode.md</c> &#xA7;3.2). Pure: the parts as they stand in, a list out.
/// </summary>
/// <remarks>
/// <para>
/// For each unordered pair of the given parts (both boxes with a <see cref="Part"/>; a wall is never
/// firmed), older id first: one <see cref="Flush"/> per pair of faces facing each other with a
/// contact of non-zero area (<see cref="FirmUpKind.Against"/>); and, only for a pair that has such
/// a contact, one per pair of faces facing the same way along X or Y at exactly one coordinate whose
/// extents on the other two axes overlap or abut (<see cref="FirmUpKind.Alongside"/>). Z is left out
/// of alongside on purpose: parts drawn in the plan all lie on the datum at one depth, and "their
/// undersides are coplanar" says nothing a person meant.
/// </para>
/// <para>
/// A proposal the sketch already holds — the same two references in either order — is skipped.
/// Order: by pair (lower id, then higher), against before alongside, then the world axis X, Y, Z,
/// then the first part's face South, East, North, West, Bottom, Top, then the second's.
/// </para>
/// </remarks>
public static class FirmUpProposals
{
    private static readonly BoxFace[] FaceOrder = [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];

    /// <summary>Every relationship the touching parts imply and the sketch does not yet hold, in the stated order.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="parts">The boxes to consider; anything that is not a part-carrying box is ignored.</param>
    public static ImmutableArray<FirmUpProposal> For(Sketch sketch, IReadOnlyCollection<EntityId> parts)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(parts);

        Box[] boxes = [.. parts
            .Distinct()
            .Order()
            .Select(sketch.Find<Box>)
            .OfType<Box>()
            .Where(box => box.Part is not null && box.Orientation.IsExact)];

        ImmutableArray<FirmUpProposal>.Builder proposals = ImmutableArray.CreateBuilder<FirmUpProposal>();
        for (int i = 0; i < boxes.Length; i++)
        {
            for (int j = i + 1; j < boxes.Length; j++)
            {
                AddPair(sketch, boxes[i], boxes[j], proposals);
            }
        }

        return proposals.ToImmutable();
    }

    private static void AddPair(Sketch sketch, Box a, Box b, ImmutableArray<FirmUpProposal>.Builder proposals)
    {
        List<(BoxFace OfA, BoxFace OfB, Axis Axis)> against = [];
        foreach (TouchingFaces touching in JointGeometry.TouchingFaces(sketch, a.Id, b.Id))
        {
            (Axis axisA, bool positiveA) = a.Orientation.Normal(touching.OfFirst);
            (_, bool positiveB) = b.Orientation.Normal(touching.OfSecond);
            if (positiveA != positiveB)
            {
                against.Add((touching.OfFirst, touching.OfSecond, axisA));
            }
        }

        if (against.Count == 0)
        {
            return;
        }

        foreach ((BoxFace ofA, BoxFace ofB, _) in Ordered(against))
        {
            Offer(sketch, a, ofA, b, ofB, FirmUpKind.Against, proposals);
        }

        List<(BoxFace OfA, BoxFace OfB, Axis Axis)> alongside = [];
        (Point3 lowA, Point3 highA) = JointGeometry.Extent(a);
        (Point3 lowB, Point3 highB) = JointGeometry.Extent(b);
        foreach (BoxFace ofA in FaceOrder)
        {
            (Axis axis, bool positive) = a.Orientation.Normal(ofA);
            if (axis == Axis.Z)
            {
                continue;
            }

            foreach (BoxFace ofB in FaceOrder)
            {
                if (b.Orientation.Normal(ofB) != (axis, positive))
                {
                    continue;
                }

                Length planeA = positive ? highA.Component(axis) : lowA.Component(axis);
                Length planeB = positive ? highB.Component(axis) : lowB.Component(axis);
                if (planeA == planeB && MeetAcross(axis, lowA, highA, lowB, highB))
                {
                    alongside.Add((ofA, ofB, axis));
                }
            }
        }

        foreach ((BoxFace ofA, BoxFace ofB, _) in Ordered(alongside))
        {
            Offer(sketch, a, ofA, b, ofB, FirmUpKind.Alongside, proposals);
        }
    }

    private static IEnumerable<(BoxFace OfA, BoxFace OfB, Axis Axis)> Ordered(List<(BoxFace OfA, BoxFace OfB, Axis Axis)> faces)
        => faces
            .OrderBy(pair => pair.Axis)
            .ThenBy(pair => Array.IndexOf(FaceOrder, pair.OfA))
            .ThenBy(pair => Array.IndexOf(FaceOrder, pair.OfB));

    // The two boxes' extents overlap or abut on both axes other than the faces' normal: closed intervals meet.
    private static bool MeetAcross(Axis normal, Point3 lowA, Point3 highA, Point3 lowB, Point3 highB)
    {
        foreach (Axis axis in (Axis[])[Axis.X, Axis.Y, Axis.Z])
        {
            if (axis != normal
                && Length.Max(lowA.Component(axis), lowB.Component(axis)) > Length.Min(highA.Component(axis), highB.Component(axis)))
            {
                return false;
            }
        }

        return true;
    }

    private static void Offer(Sketch sketch, Box a, BoxFace ofA, Box b, BoxFace ofB, FirmUpKind kind, ImmutableArray<FirmUpProposal>.Builder proposals)
    {
        FeatureRef first = new(a.Id, BoxFeature.Face(ofA));
        FeatureRef second = new(b.Id, BoxFeature.Face(ofB));
        if (AlreadyHeld(sketch, first, second))
        {
            return;
        }

        string joining = kind == FirmUpKind.Against ? "against" : "flush with";
        string sentence = $"{Name(a)}'s {FaceWord(a, ofA)} {joining} {Name(b)}'s {FaceWord(b, ofB)}";
        proposals.Add(new FirmUpProposal(new Flush(RelationshipId.New(), first, second), kind, sentence));
    }

    private static bool AlreadyHeld(Sketch sketch, FeatureRef first, FeatureRef second)
        => sketch.RelationshipsInOrder.OfType<Flush>().Any(held =>
            (held.A == first && held.B == second) || (held.A == second && held.B == first));

    private static string Name(Box box) => string.IsNullOrWhiteSpace(box.Name) ? "a part" : box.Name;

    // The way the face faces now, in the world (the plan draws north up): "south face", "top face".
    private static string FaceWord(Box box, BoxFace face) => box.Orientation.Normal(face) switch
    {
        (Axis.X, true) => "east face",
        (Axis.X, false) => "west face",
        (Axis.Y, true) => "north face",
        (Axis.Y, false) => "south face",
        (Axis.Z, true) => "top face",
        _ => "bottom face",
    };
}
