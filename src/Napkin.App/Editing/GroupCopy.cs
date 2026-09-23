using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// Copies of several parts at once, and the relationships among them (#87): what
/// <see cref="SelectionCommands.Duplicate"/> and <see cref="SelectionCommands.Mirror"/> build.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The relationships among the copied parts come too</strong> — a flush between a leg and
/// the apron beside it, a coincidence of two corners, a width stated equal to another — renamed onto
/// the copies, with new ids. The ones that hold a copied part to something that was not copied do
/// not, and neither do the ones about one part alone — its typed sizes, its pin — exactly as a copy
/// of one part has no relationships (shaped-parts §2.6): a copy is free until it is set down.
/// </para>
/// <para>
/// <strong>A mirror copy</strong> keeps each part's orientation — a mirror is not a turn, and a
/// plain block is its own mirror image — and lands at the reflected place. Its relationships are
/// mirrored with it: a face that faced along the mirror axis faces the other way, so it is the
/// opposite face of the copy, and a distance along that axis changes sign. A part with cuts is not
/// its own mirror image, and is refused rather than copied the wrong way round.
/// </para>
/// </remarks>
public static class GroupCopy
{
    static readonly BoxFace[] AllFaces = [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];

    /// <summary>The least and greatest corner of several boxes' extents together.</summary>
    public static (Point3 Low, Point3 High) Extent(IEnumerable<Box> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        Point3? low = null, high = null;
        foreach (Box box in boxes)
        {
            (Point3 l, Point3 h) = SpaceSnapResolver.Extent(box);
            low = low is { } a ? new Point3(Length.Min(a.X, l.X), Length.Min(a.Y, l.Y), Length.Min(a.Z, l.Z)) : l;
            high = high is { } b ? new Point3(Length.Max(b.X, h.X), Length.Max(b.Y, h.Y), Length.Max(b.Z, h.Z)) : h;
        }

        return low is { } lo && high is { } hi
            ? (lo, hi)
            : throw new ArgumentException("There are no boxes to measure.", nameof(boxes));
    }

    /// <summary>
    /// The requests that add copies of <paramref name="boxes"/> moved by <paramref name="offset"/>,
    /// with the relationships among them, and the copies' ids by original.
    /// </summary>
    public static (ImmutableList<Request> Requests, ImmutableDictionary<EntityId, EntityId> Copies) Duplicate(
        Sketch sketch,
        IReadOnlyCollection<Box> boxes,
        Vector3 offset)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(boxes);

        ImmutableDictionary<EntityId, EntityId> copies = boxes.ToImmutableDictionary(box => box.Id, _ => EntityId.New());
        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        foreach (Box box in boxes.OrderBy(box => box.Id))
        {
            requests.Add(new AddEntity(box with { Id = copies[box.Id], Anchor = box.Anchor + offset }));
        }

        foreach (Relationship relationship in Among(sketch, copies.Keys))
        {
            if (Renamed(relationship, copies, sketch, mirror: null) is { } copy)
            {
                requests.Add(new AddRelationship(copy));
            }
        }

        return (requests.ToImmutable(), copies);
    }

    /// <summary>
    /// The requests that add mirror copies of <paramref name="boxes"/> across the plane perpendicular
    /// to <paramref name="axis"/> at <paramref name="plane"/>, with the relationships among them
    /// mirrored too — or null, with the name of a part that has cuts, which a mirror would have to
    /// turn the wrong way round.
    /// </summary>
    public static (ImmutableList<Request> Requests, ImmutableDictionary<EntityId, EntityId> Copies)? Mirror(
        Sketch sketch,
        IReadOnlyCollection<Box> boxes,
        Axis axis,
        Length plane,
        out Box? refused)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(boxes);

        refused = boxes.OrderBy(box => box.Id).FirstOrDefault(box => !box.Cuts.IsEmpty || !box.Orientation.IsExact);
        if (refused is not null)
        {
            return null;
        }

        ImmutableDictionary<EntityId, EntityId> copies = boxes.ToImmutableDictionary(box => box.Id, _ => EntityId.New());
        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        foreach (Box box in boxes.OrderBy(box => box.Id))
        {
            // The reflection of [low, high] about the plane is [2p - high, 2p - low]: the copy's low
            // corner is there on the mirror axis, and where it was on the other two.
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
            Length reflectedLow = plane + plane - high.Component(axis);
            Vector3 shift = Vector3.Along(axis, reflectedLow - low.Component(axis));
            requests.Add(new AddEntity(box with { Id = copies[box.Id], Anchor = box.Anchor + shift }));
        }

        foreach (Relationship relationship in Among(sketch, copies.Keys))
        {
            if (Renamed(relationship, copies, sketch, axis) is { } copy)
            {
                requests.Add(new AddRelationship(copy));
            }
        }

        return (requests.ToImmutable(), copies);
    }

    /// <summary>The relationships among some parts: naming two or more of them, and nothing else.</summary>
    public static IEnumerable<Relationship> Among(Sketch sketch, IEnumerable<EntityId> parts)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(parts);

        HashSet<EntityId> set = [.. parts];
        return sketch.RelationshipsInOrder.Where(relationship =>
        {
            EntityId[] named = [.. relationship.References.Distinct()];
            return named.Length >= 2 && named.All(set.Contains);
        });
    }

    static Relationship? Renamed(Relationship relationship, IReadOnlyDictionary<EntityId, EntityId> copies, Sketch sketch, Axis? mirror)
    {
        RelationshipId id = RelationshipId.New();
        return relationship switch
        {
            Coincident c when Place(c.A) is { } a && Place(c.B) is { } b => new Coincident(id, a, b),
            Flush f when Place(f.A) is { } a && Place(f.B) is { } b => new Flush(id, a, b),
            AxisDistance d when Place(d.From) is { } from && Place(d.To) is { } to =>
                new AxisDistance(id, from, to, d.Axis, d.Axis == mirror ? -d.Distance : d.Distance),
            Centered c when Place(c.Middle) is { } middle && Place(c.A) is { } a && Place(c.B) is { } b =>
                new Centered(id, middle, a, b, c.Axis),
            EqualParam e when Param(e.A) is { } a && Param(e.B) is { } b => new EqualParam(id, a, b),
            _ => null,
        };

        PlaceRef? Place(PlaceRef reference) => reference switch
        {
            FeatureRef feature when copies.TryGetValue(feature.Box, out EntityId copy) =>
                new FeatureRef(copy, mirror is { } axis && sketch.Find<Box>(feature.Box) is { } box
                    ? Mirrored(box, feature.Feature, axis)
                    : feature.Feature),
            CenterRef centre when copies.TryGetValue(centre.Box, out EntityId copy) => new CenterRef(copy),
            _ => null,
        };

        ParamRef? Param(ParamRef reference) => reference switch
        {
            BoxWidthRef width when copies.TryGetValue(width.Box, out EntityId copy) => new BoxWidthRef(copy),
            BoxHeightRef height when copies.TryGetValue(height.Box, out EntityId copy) => new BoxHeightRef(copy),
            BoxDepthRef depth when copies.TryGetValue(depth.Box, out EntityId copy) => new BoxDepthRef(copy),
            _ => null,
        };
    }

    /// <summary>
    /// A feature of a box as its mirror copy has it: each face that faces along the mirror axis
    /// is the opposite face, because the copy is turned the same way and the reflection sends that
    /// face's place to the other side.
    /// </summary>
    public static BoxFeature Mirrored(Box box, BoxFeature feature, Axis axis)
    {
        ArgumentNullException.ThrowIfNull(box);

        BoxFace[] faces = [.. feature.Faces.Select(face => box.Orientation.Normal(face).Axis == axis ? Opposite(box, face) : face)];
        return faces.Length switch
        {
            1 => BoxFeature.Face(faces[0]),
            2 => BoxFeature.Edge(faces[0], faces[1]),
            _ => BoxFeature.Vertex(faces[0], faces[1], faces[2]),
        };
    }

    static BoxFace Opposite(Box box, BoxFace face)
    {
        (Axis axis, bool positive) = box.Orientation.Normal(face);
        return AllFaces.Single(other => box.Orientation.Normal(other) == (axis, !positive));
    }
}
