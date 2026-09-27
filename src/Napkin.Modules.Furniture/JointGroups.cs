using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>A drawer (docs/design/furniture-checks.md §4.2): the marked part and every part joined to it.</summary>
/// <param name="Marked">The part carrying the mark.</param>
/// <param name="Members">The drawer's parts: its jointed group, the marked part included, in id order.</param>
/// <param name="Extension">How far it opens, as typed.</param>
/// <param name="Note">Said when another part of the same group is marked too; null otherwise.</param>
public sealed record Drawer(Box Marked, ImmutableArray<Box> Members, Length Extension, string? Note);

/// <summary>
/// Parts joined to one another, derived from the joints and never stored: a drawer box is one group,
/// the carcass another, because slides are hardware, not joints.
/// </summary>
public static class JointGroups
{
    /// <summary>Every box's group: the connected parts over the sketch's joints, each group in id order, groups by their first id.</summary>
    public static ImmutableArray<ImmutableArray<Box>> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        Box[] boxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        Dictionary<EntityId, EntityId> parent = boxes.ToDictionary(box => box.Id, box => box.Id);

        EntityId Root(EntityId id)
        {
            while (parent[id] != id)
            {
                id = parent[id] = parent[parent[id]];
            }

            return id;
        }

        foreach (Joint joint in sketch.RelationshipsInOrder.OfType<Joint>())
        {
            parent[Root(joint.Receiving.Box)] = Root(joint.Inserted.Box);
        }

        return [.. boxes.GroupBy(box => Root(box.Id)).Select(group => group.ToImmutableArray()).OrderBy(group => group[0].Id)];
    }

    /// <summary>
    /// The drawers: one per group holding a part marked a drawer. When two parts of a group are marked,
    /// the first by id is the drawer's, and the note says the other was not read.
    /// </summary>
    public static ImmutableArray<Drawer> Drawers(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        List<Drawer> drawers = [];
        foreach (ImmutableArray<Box> group in Of(sketch))
        {
            Box[] marked = [.. group.Where(box => box.Part?.Drawer is not null)];
            if (marked.Length == 0)
            {
                continue;
            }

            string? note = marked.Length > 1
                ? $"{marked[1].Name} is marked a drawer too, in {marked[0].Name}'s drawer: napkin reads {marked[0].Name}'s mark only."
                : null;
            drawers.Add(new Drawer(marked[0], group, marked[0].Part!.Drawer!.Extension, note));
        }

        return [.. drawers];
    }
}
