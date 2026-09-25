using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;

namespace Napkin.Modules.Editing;

/// <summary>
/// Draw → Room (docs/design/renovation-sketches.md §4.2, §8): a room is a rectangle the person
/// draws — dragged out, or made by one click and then typed — whose edges land on the walls' inside
/// faces. It starts 8'-0" tall, as a wall does: a starting value, not a standard.
/// </summary>
public static class RoomTool
{
    /// <summary>The ceiling height a new room starts at, the wall tool's 8'-0" (a starting value to type over).</summary>
    public static Length StartingHeight => WallTool.StartingHeight;

    /// <summary>
    /// The size a room made by a click with no walls around it starts at, 10'-0" square: a starting
    /// size to type over, not a standard.
    /// </summary>
    public static readonly Length StartingSide = Length.Inches(120);

    /// <summary>
    /// The rectangle the nearest wall face on each side of a point encloses — the room's inside
    /// faces — or null when a side has no wall face across from the point. Walls being demolished
    /// are not there.
    /// </summary>
    public static (Point2 Anchor, Length Length, Length Width)? Enclosure(Sketch sketch, Point2 at)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ImmutableArray<WallFace> faces = [.. Wall.All(sketch.After()).SelectMany(wall => RoomBounds.FacesOf(wall))];
        Length? west = faces.Where(f => f.Vertical && f.At < at.X && f.Lo <= at.Y && at.Y <= f.Hi).Select(f => (Length?)f.At).Max();
        Length? east = faces.Where(f => f.Vertical && f.At > at.X && f.Lo <= at.Y && at.Y <= f.Hi).Select(f => (Length?)f.At).Min();
        Length? south = faces.Where(f => !f.Vertical && f.At < at.Y && f.Lo <= at.X && at.X <= f.Hi).Select(f => (Length?)f.At).Max();
        Length? north = faces.Where(f => !f.Vertical && f.At > at.Y && f.Lo <= at.X && at.X <= f.Hi).Select(f => (Length?)f.At).Min();
        return west is { } x0 && east is { } x1 && south is { } y0 && north is { } y1
            ? (new Point2(x0, y0), x1 - x0, y1 - y0)
            : null;
    }

    /// <summary>
    /// A dragged rectangle with each edge moved onto a parallel wall face within
    /// <paramref name="reach"/> that runs along it — the snap that lands a room on the inside faces.
    /// </summary>
    public static (Point2 Anchor, Length Length, Length Width) SnapToWalls(Sketch sketch, Point2 anchor, Length length, Length width, Length reach)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ImmutableArray<WallFace> faces = [.. Wall.All(sketch.After()).SelectMany(wall => RoomBounds.FacesOf(wall))];
        Length x0 = anchor.X, x1 = anchor.X + length, y0 = anchor.Y, y1 = anchor.Y + width;

        Length Snap(Length edge, bool vertical, Length lo, Length hi)
            => faces
                .Where(f => f.Vertical == vertical && Length.Max(f.Lo, lo) < Length.Min(f.Hi, hi) && Length.Abs(f.At - edge) <= reach)
                .OrderBy(f => Length.Abs(f.At - edge))
                .Select(f => (Length?)f.At)
                .FirstOrDefault() ?? edge;

        Length west = Snap(x0, true, y0, y1), east = Snap(x1, true, y0, y1);
        Length south = Snap(y0, false, x0, x1), north = Snap(y1, false, x0, x1);
        return east > west && north > south ? (new Point2(west, south), east - west, north - south) : (anchor, length, width);
    }

    /// <summary>The request that adds a room — a box on the Room layer, lying as drawn, <see cref="StartingHeight"/> tall — and its layer, if new.</summary>
    public static Request Request(LayerId layer, Request? addLayer, EntityId id, string name, Point2 anchor, Length length, Length width)
    {
        AddEntity add = new(Box.AsDrawn(id, layer, anchor, length, width, StartingHeight, Angle.Zero) with { Name = name });
        return addLayer is null ? add : Batch.Of(addLayer, add);
    }
}
