using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// J on an angled part and what it stands on or against (<c>docs/design/angled-parts.md</c> &#xA7;5):
/// the one joint a strut's end can make, a butt, found by where its end already sits.
/// </summary>
/// <remarks>
/// A strut's end is always the inserted face. It is joined to a face of a box that lies in the very
/// plane the end is cut to and overlaps it, or to another strut's end cut to the same plane — an
/// A-frame's apex. The fastening proposed is the classic splayed-leg attachment: pocket screws from
/// the strut's bottom face, glued. Nothing else needs asking, because nothing else is possible.
/// </remarks>
public static class StrutJoining
{
    /// <summary>The joint two parts make, one of them a strut, or <see langword="null"/> when no end of a strut sits on the other.</summary>
    public static StrutJoint? Propose(Sketch sketch, EntityId first, EntityId second)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return From(sketch, first, second) ?? From(sketch, second, first);
    }

    /// <summary>What to say when two parts with a strut among them make no joint.</summary>
    public const string NothingSits =
        "Neither end of the angled part sits on the other part: put its end on a face of it — its top under a seat, its foot on a rail — then press J.";

    static StrutJoint? From(Sketch sketch, EntityId strutId, EntityId otherId)
    {
        if (sketch.Find<Strut>(strutId) is not { } strut || sketch.Find(otherId) is not { } other)
        {
            return null;
        }

        foreach (StrutEnd end in new[] { StrutEnd.To, StrutEnd.From })
        {
            StrutEndFaceRef inserted = new(strut.Id, end);
            IEnumerable<PlaceRef> faces = other switch
            {
                Box box => Enum.GetValues<BoxFace>().Select(face => (PlaceRef)new FeatureRef(box.Id, BoxFeature.Face(face))),
                Strut => [new StrutEndFaceRef(other.Id, StrutEnd.To), new StrutEndFaceRef(other.Id, StrutEnd.From)],
                _ => [],
            };

            foreach (PlaceRef face in faces)
            {
                StrutJoint joint = new(
                    new RelationshipId(Guid.NewGuid()),
                    face,
                    inserted,
                    new Fastening(FasteningKind.PocketScrews, null, null),
                    Glue: true,
                    StrutFace.Bottom);
                if (StrutJointGeometry.Contact(sketch, joint) is not null)
                {
                    return joint;
                }
            }
        }

        return null;
    }
}
