using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

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
        Vector3 offset,
        Func<EntityId, string>? nameOf = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(boxes);

        ImmutableDictionary<EntityId, EntityId> copies = boxes.ToImmutableDictionary(box => box.Id, _ => EntityId.New());
        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        HashSet<string> taken = Names(sketch, nameOf);
        foreach (Box box in boxes.OrderBy(box => box.Id))
        {
            requests.Add(new AddEntity(box with
            {
                Id = copies[box.Id],
                Anchor = box.Anchor + offset,
                Name = CopyName(NameOf(box, nameOf), taken),
                WallInputs = CopiedWallInputs(box, copies, mirror: null),
            }));
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
        out Box? refused,
        Func<EntityId, string>? nameOf = null)
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
        HashSet<string> taken = Names(sketch, nameOf);
        foreach (Box box in boxes.OrderBy(box => box.Id))
        {
            // The reflection of [low, high] about the plane is [2p - high, 2p - low]: the copy's low
            // corner is there on the mirror axis, and where it was on the other two.
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
            Length reflectedLow = plane + plane - high.Component(axis);
            Vector3 shift = Vector3.Along(axis, reflectedLow - low.Component(axis));
            requests.Add(new AddEntity(box with
            {
                Id = copies[box.Id],
                Anchor = box.Anchor + shift,
                Name = CopyName(NameOf(box, nameOf), taken),
                WallInputs = CopiedWallInputs(box, copies, axis),
            }));
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

    /// <summary>
    /// A copied wall's inputs (#39): its bracing assignments name the copies of the openings copied
    /// with it (an opening left behind keeps its id, which the copy's wall line treats as gone:
    /// segments either side merge, keeping a method they shared). A mirror across the wall's own
    /// length turns it end for end, so each segment's start and end boundaries swap.
    /// </summary>
    public static WallInputs? CopiedWallInputs(Box box, IReadOnlyDictionary<EntityId, EntityId> copies, Axis? mirror)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(copies);
        if (box.WallInputs is not { } inputs || inputs.Bracing.IsEmpty)
        {
            return box.WallInputs;
        }

        bool reversed = mirror is { } axis
            && box.Orientation.Apply(Vector3.Along(Axis.X, Length.Inches(1))).Component(axis) != Length.Zero;
        EntityId? Map(EntityId? id) => id is { } value && copies.TryGetValue(value, out EntityId copy) ? copy : id;
        return inputs with
        {
            Bracing =
            [
                .. inputs.Bracing.Select(a => reversed
                    ? new BracingAssignment(Map(a.To), Map(a.From), a.Method)
                    : new BracingAssignment(Map(a.From), Map(a.To), a.Method)),
            ],
        };
    }

    /// <summary>
    /// A copy's name (#91): its original's with the next free number — "Leg, south-west (2)", then
    /// "(3)" — so that the list, the messages and the Part panel can tell a copy from the part it
    /// came from. A name already ending in a number continues it rather than growing another. An
    /// unnamed part's copy stays unnamed, and the editor gives it a "Part N" label as it does any
    /// part added without one. The cut list groups by size, not by name, so the two still count as
    /// one row.
    /// </summary>
    /// <param name="name">The original's name.</param>
    /// <param name="taken">Every name in use, which the new one is added to.</param>
    public static string CopyName(string name, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(taken);

        if (name.Length == 0)
        {
            return name;
        }

        System.Text.RegularExpressions.Match numbered = System.Text.RegularExpressions.Regex.Match(name, @"^(.*) \((\d+)\)$");
        string stem = numbered.Success ? numbered.Groups[1].Value : name;
        int next = numbered.Success ? int.Parse(numbered.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) + 1 : 2;
        string candidate;
        do
        {
            candidate = $"{stem} ({next++})";
        }
        while (!taken.Add(candidate));

        return candidate;
    }

    // What the part is called on screen: its own name, or what the drawing calls it for want of one.
    static string NameOf(Box box, Func<EntityId, string>? nameOf) =>
        box.Name.Length > 0 || nameOf is null ? box.Name : nameOf(box.Id);

    static HashSet<string> Names(Sketch sketch, Func<EntityId, string>? nameOf) =>
        [.. sketch.Entities.Values.OfType<Box>().Select(box => NameOf(box, nameOf)).Where(name => name.Length > 0)];

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

            // A joint among the copied parts comes too, glue and fastening and all (joinery note
            // §4.3); one to a part that was not copied is not. Its pocket-hole face is a face of
            // the inserted part, so a mirror turns it like any other.
            Joint j when Place(j.Receiving) is FeatureRef receiving && Place(j.Inserted) is FeatureRef inserted =>
                new Joint(id, receiving, inserted, j.Type, j.Depth, j.Fastening with { PocketFace = PocketFace(j) }, j.Glue),
            _ => null,
        };

        BoxFace? PocketFace(Joint joint) =>
            joint.Fastening.PocketFace is { } face && mirror is { } axis && sketch.Find<Box>(joint.Inserted.Box) is { } box
                ? Mirrored(box, BoxFeature.Face(face), axis).Faces[0]
                : joint.Fastening.PocketFace;

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
