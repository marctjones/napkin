using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>How a segment's bracing method was found (docs/building.md, "Wall bracing").</summary>
public enum AssignmentOrigin
{
    /// <summary>No method: the segment is not braced. Never defaulted or inferred.</summary>
    None,

    /// <summary>Assigned to exactly this segment (the same opening, or wall end, on each side).</summary>
    Assigned,

    /// <summary>
    /// The segment is two or more assigned segments merged when the openings between them left the
    /// wall (deleted, or moved to another wall), and every one of them had this same method.
    /// </summary>
    Merged,

    /// <summary>The segment is merged from assigned segments that had different methods: unassigned, and said so.</summary>
    MergedConflict,
}

/// <summary>
/// One solid stretch of a wall between its openings and ends. Its identity is what bounds it: the
/// opening it starts after (null: the wall's start) and the opening it ends before (null: the
/// wall's end). Its length comes from the drawing.
/// </summary>
/// <param name="Index">Its place along the wall, from 0 at the start.</param>
/// <param name="From">The opening it starts after, or null for the wall's start.</param>
/// <param name="To">The opening it ends before, or null for the wall's end.</param>
/// <param name="Start">How far along the wall it starts.</param>
/// <param name="Length">Its length.</param>
/// <param name="Label">"wall start to Window 1".</param>
/// <param name="Method">The bracing method id, or null when none applies.</param>
/// <param name="Origin">How the method was found.</param>
public sealed record WallSegment(
    int Index,
    EntityId? From,
    EntityId? To,
    Length Start,
    Length Length,
    string Label,
    string? Method,
    AssignmentOrigin Origin)
{
    /// <summary>Where it ends along the wall.</summary>
    public Length End => Start + Length;
}

/// <summary>
/// A wall line (issue #39): one wall's solid segments between its openings and ends, each with the
/// bracing method a person assigned to it, if any. Derived from the drawing every time; only the
/// assignments are stored (<see cref="WallInputs.Bracing"/>).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Which assignment a segment keeps</strong> (docs/building.md): the one stored for exactly
/// its two boundaries. Resizing or moving an opening keeps its neighbours' boundaries, so their
/// methods stay. When openings leave the wall (deleted, or moved to another wall) the segments either
/// side merge: the merged segment keeps their method if every one of them had the same one, and is
/// unassigned otherwise (<see cref="AssignmentOrigin.MergedConflict"/>). An opening moved past
/// another, or a new opening splitting a segment, makes segments with new boundaries: unassigned.
/// </para>
/// <para>
/// Assignments whose boundaries no longer bound a segment stay stored until a method is next chosen
/// on the wall, when <see cref="Assign"/> rewrites the list for the current segments only; that is
/// what lets undo put an opening back with its neighbours' methods.
/// </para>
/// </remarks>
public sealed record WallLine(Wall Wall, ImmutableArray<Opening> Openings, ImmutableArray<WallSegment> Segments)
{
    /// <summary>The wall line of a wall in a sketch.</summary>
    public static WallLine Of(Sketch sketch, Wall wall)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(wall);
        ImmutableArray<Opening> openings = Opening.In(sketch, wall);
        Dictionary<EntityId, string> names = openings.ToDictionary(o => o.Id, o => o.Name);

        List<(EntityId? From, EntityId? To, Length Start, Length End)> spans = [];
        Length cursor = Length.Zero;
        EntityId? left = null;
        foreach (Opening opening in openings)
        {
            Length start = Length.Max(Length.Zero, opening.Offset);
            Length end = Length.Min(wall.Length, opening.Offset + opening.Width);
            if (start > cursor)
            {
                spans.Add((left, opening.Id, cursor, start));
            }

            // Overlapping or touching openings bound no segment between them; the one reaching
            // furthest along bounds the next segment's start.
            if (end >= cursor)
            {
                cursor = end;
                left = opening.Id;
            }
        }

        if (cursor < wall.Length)
        {
            spans.Add((left, null, cursor, wall.Length));
        }

        HashSet<EntityId> boundaries = [.. spans.SelectMany(s => new[] { s.From, s.To }).OfType<EntityId>()];
        ImmutableArray<BracingAssignment> stored = wall.Box.WallInputs?.Bracing ?? [];
        ImmutableArray<WallSegment> segments =
        [
            .. spans.Select((s, i) =>
            {
                (string? method, AssignmentOrigin origin) = Resolve(stored, s.From, s.To, boundaries);
                string label = $"{(s.From is { } f ? names[f] : "wall start")} to {(s.To is { } t ? names[t] : "wall end")}";
                return new WallSegment(i, s.From, s.To, s.Start, s.End - s.Start, label, method, origin);
            }),
        ];
        return new WallLine(wall, openings, segments);
    }

    /// <summary>Every wall's line in a sketch, in wall order.</summary>
    public static ImmutableArray<WallLine> All(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return [.. Wall.All(sketch).Select(wall => Of(sketch, wall))];
    }

    /// <summary>
    /// The wall's assignments with segment <paramref name="index"/> set to <paramref name="method"/>
    /// (null: not braced): every current segment's method, start to end, and nothing stale.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">No such segment.</exception>
    public ImmutableArray<BracingAssignment> Assign(int index, string? method)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Segments.Length);
        return
        [
            .. Segments
                .Select(s => (Segment: s, Method: s.Index == index ? method : s.Method))
                .Where(p => p.Method is not null)
                .Select(p => new BracingAssignment(p.Segment.From, p.Segment.To, p.Method!)),
        ];
    }

    /// <summary>The method stored for exactly these boundaries, or one shared across a merge; see the remarks on the type.</summary>
    private static (string? Method, AssignmentOrigin Origin) Resolve(
        ImmutableArray<BracingAssignment> stored, EntityId? from, EntityId? to, HashSet<EntityId> boundaries)
    {
        if (stored.FirstOrDefault(a => a.From == from && a.To == to) is { } exact)
        {
            return (exact.Method, AssignmentOrigin.Assigned);
        }

        // A chain from → x → … → to through boundaries that no longer bound any segment of the wall.
        List<BracingAssignment>? chain = Chain(stored, from, to, boundaries, []);
        if (chain is null)
        {
            return (null, AssignmentOrigin.None);
        }

        return chain.All(a => a.Method == chain[0].Method)
            ? (chain[0].Method, AssignmentOrigin.Merged)
            : (null, AssignmentOrigin.MergedConflict);
    }

    private static List<BracingAssignment>? Chain(
        ImmutableArray<BracingAssignment> stored, EntityId? at, EntityId? to, HashSet<EntityId> boundaries, HashSet<EntityId> visited)
    {
        foreach (BracingAssignment link in stored.Where(a => a.From == at))
        {
            if (link.To == to)
            {
                return [link];
            }

            if (link.To is not { } next || boundaries.Contains(next) || !visited.Add(next))
            {
                continue;
            }

            if (Chain(stored, next, to, boundaries, visited) is { } rest)
            {
                return [link, .. rest];
            }
        }

        return null;
    }
}
