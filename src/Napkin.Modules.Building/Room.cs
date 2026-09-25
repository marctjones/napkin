using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>Which side of a room, by the compass of the plan (north up).</summary>
public enum RoomSide
{
    /// <summary>The south edge, the room's least Y.</summary>
    South,

    /// <summary>The east edge, the room's greatest X.</summary>
    East,

    /// <summary>The north edge, the room's greatest Y.</summary>
    North,

    /// <summary>The west edge, the room's least X.</summary>
    West,
}

/// <summary>
/// A room (docs/design/renovation-sketches.md §4.2): a box on the layer Room (or called "Room"),
/// whose plan width and height are its inside length and width and whose depth is its ceiling
/// height. Never framed, cut or bought as a box; it carries the finishes (§5). A room is a rectangle
/// the person draws, never something napkin finds from the walls around it.
/// </summary>
/// <param name="Box">The box the room is.</param>
public sealed record Room(Box Box)
{
    /// <summary>The room's id.</summary>
    public EntityId Id => Box.Id;

    /// <summary>What the room is called on screen.</summary>
    public string Name => Box.Name.Length == 0 ? "Room" : Box.Name;

    /// <summary>The ceiling height: the box's depth.</summary>
    public Length Height => Box.Depth;

    /// <summary>Whether a box reads as a room in this sketch.</summary>
    public static bool Is(Sketch sketch, Box box) => Wall.Reads(sketch, box, BuildingLayers.Room, "Room");

    /// <summary>Every room in a sketch, in id order.</summary>
    public static ImmutableArray<Room> All(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return [.. Wall.Boxes(sketch).Where(box => Is(sketch, box) && !Wall.Is(sketch, box)).Select(box => new Room(box))];
    }

    /// <summary>The room's plan rectangle, or null for a room not lying as drawn at a right angle.</summary>
    public (Length X0, Length X1, Length Y0, Length Y1)? Plan
    {
        get
        {
            if (!Box.Orientation.IsExact || Box.FaceUp != BoxFace.Top)
            {
                return null;
            }

            Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(Box.Corner)];
            return (corners.Min(c => c.X), corners.Max(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.Y));
        }
    }

    /// <summary>The inside length, east–west in the plan.</summary>
    public Length Length => Plan is { } p ? p.X1 - p.X0 : Box.Width;

    /// <summary>The inside width, north–south in the plan.</summary>
    public Length Width => Plan is { } p ? p.Y1 - p.Y0 : Box.Height;

    /// <summary>The perimeter, 2(L + W).</summary>
    public Length Perimeter => 2 * (Length + Width);
}

/// <summary>A wall's long face in the plan: a vertical line at X = <see cref="At"/>, or a horizontal one at Y, from <see cref="Lo"/> to <see cref="Hi"/>.</summary>
/// <param name="Vertical">Whether the face runs north–south.</param>
/// <param name="At">Its X when vertical, its Y otherwise.</param>
/// <param name="Lo">Where it starts along its line.</param>
/// <param name="Hi">Where it ends.</param>
public readonly record struct WallFace(bool Vertical, Length At, Length Lo, Length Hi);

/// <summary>A wall that bounds a room: one of its long faces lies on a room edge, overlapping it.</summary>
/// <param name="Wall">The wall.</param>
/// <param name="Side">Which edge of the room it lies on.</param>
/// <param name="From">Where the overlap starts along that edge (X for south and north, Y for east and west).</param>
/// <param name="To">Where it ends.</param>
public sealed record BoundingWall(Wall Wall, RoomSide Side, Length From, Length To)
{
    /// <summary>How much of the room's edge the wall covers.</summary>
    public Length Along => To - From;
}

/// <summary>
/// Which walls bound a room and which openings are the room's (renovation-sketches §4.2), from the
/// exact faces with no tolerance: a wall bounds a room when one of its long faces lies on the line
/// of a room edge and its extent along that edge overlaps the edge's.
/// </summary>
public static class RoomBounds
{
    /// <summary>The walls bounding a room, in wall id order.</summary>
    public static ImmutableArray<BoundingWall> Of(Sketch sketch, Room room)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(room);
        if (room.Plan is not { } r)
        {
            return [];
        }

        List<BoundingWall> found = [];
        foreach (Wall wall in Wall.All(sketch))
        {
            if (Faces(wall) is not { } faces)
            {
                continue;
            }

            foreach ((bool vertical, Length at, Length lo, Length hi) in faces)
            {
                foreach ((RoomSide side, bool edgeVertical, Length edgeAt, Length edgeLo, Length edgeHi) in Edges(r))
                {
                    Length from = Length.Max(lo, edgeLo), to = Length.Min(hi, edgeHi);
                    if (vertical == edgeVertical && at == edgeAt && from < to)
                    {
                        found.Add(new BoundingWall(wall, side, from, to));
                    }
                }
            }
        }

        return [.. found];
    }

    /// <summary>
    /// The room's openings: the openings of its bounding walls whose extent along the edge lies
    /// within the room's edge. One straddling the edge's end is not the room's.
    /// </summary>
    public static ImmutableArray<Opening> Openings(Sketch sketch, Room room)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(room);
        if (room.Plan is not { } r)
        {
            return [];
        }

        List<Opening> found = [];
        foreach (BoundingWall bounding in Of(sketch, room))
        {
            (Length edgeLo, Length edgeHi) = bounding.Side is RoomSide.South or RoomSide.North ? (r.X0, r.X1) : (r.Y0, r.Y1);
            foreach (Opening opening in Opening.In(sketch, bounding.Wall))
            {
                (Length lo, Length hi) = Span(opening, bounding.Side);
                if (lo >= edgeLo && hi <= edgeHi && found.All(o => o.Id != opening.Id))
                {
                    found.Add(opening);
                }
            }
        }

        return [.. found];
    }

    /// <summary>
    /// Walls whose long face runs along a room edge, overlapping it, but not on its line: "not
    /// bounded by Wall 2 — snap it to the wall" (§4.2). Only walls within a foot of the edge are
    /// named: a hint to snap, not a rule.
    /// </summary>
    public static ImmutableArray<string> NearMisses(Sketch sketch, Room room)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(room);
        if (room.Plan is not { } r)
        {
            return [];
        }

        HashSet<EntityId> bounding = [.. Of(sketch, room).Select(b => b.Wall.Id)];
        List<string> said = [];
        foreach (Wall wall in Wall.All(sketch).Where(wall => !bounding.Contains(wall.Id)))
        {
            if (Faces(wall) is not { } faces)
            {
                continue;
            }

            bool near = faces.Any(face => Edges(r).Any(edge =>
                face.Vertical == edge.Vertical
                && Length.Max(face.Lo, edge.Lo) < Length.Min(face.Hi, edge.Hi)
                && Length.Abs(face.At - edge.At) is var gap && gap > Length.Zero && gap <= Length.Inches(12)));
            if (near)
            {
                said.Add($"not bounded by {wall.Name} — snap it to the wall");
            }
        }

        return [.. said];
    }

    /// <summary>An opening's extent along a room edge: X for south and north, Y for east and west.</summary>
    static (Length Lo, Length Hi) Span(Opening opening, RoomSide side)
    {
        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(opening.Box.Corner)];
        return side is RoomSide.South or RoomSide.North
            ? (corners.Min(c => c.X), corners.Max(c => c.X))
            : (corners.Min(c => c.Y), corners.Max(c => c.Y));
    }

    static (RoomSide Side, bool Vertical, Length At, Length Lo, Length Hi)[] Edges((Length X0, Length X1, Length Y0, Length Y1) r) =>
    [
        (RoomSide.South, false, r.Y0, r.X0, r.X1),
        (RoomSide.East, true, r.X1, r.Y0, r.Y1),
        (RoomSide.North, false, r.Y1, r.X0, r.X1),
        (RoomSide.West, true, r.X0, r.Y0, r.Y1),
    ];

    /// <summary>A wall's two long faces in the plan; none for a wall not standing as drawn at a right angle.</summary>
    public static ImmutableArray<WallFace> FacesOf(Wall wall)
    {
        ArgumentNullException.ThrowIfNull(wall);
        return Faces(wall) is { } faces ? [.. faces.Select(face => new WallFace(face.Vertical, face.At, face.Lo, face.Hi))] : [];
    }

    static (bool Vertical, Length At, Length Lo, Length Hi)[]? Faces(Wall wall)
    {
        Box box = wall.Box;
        if (!box.Orientation.IsExact || box.FaceUp != BoxFace.Top)
        {
            return null;
        }

        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(box.Corner)];
        Length x0 = corners.Min(c => c.X), x1 = corners.Max(c => c.X), y0 = corners.Min(c => c.Y), y1 = corners.Max(c => c.Y);
        return box.Rotation.QuarterTurns % 2 == 0
            ? [(false, y0, x0, x1), (false, y1, x0, x1)]
            : [(true, x0, y0, y1), (true, x1, y0, y1)];
    }
}
