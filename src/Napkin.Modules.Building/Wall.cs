using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>The layer names a wall and an opening are drawn on (the app styles them by these names).</summary>
public static class BuildingLayers
{
    /// <summary>The layer a wall is drawn on.</summary>
    public const string Wall = "Wall";

    /// <summary>The layer a window or door opening is drawn on.</summary>
    public const string Opening = "Opening";

    /// <summary>The layer a room is drawn on (renovation-sketches §4.2).</summary>
    public const string Room = "Room";

    /// <summary>The layer notes are drawn on (renovation-sketches §7).</summary>
    public const string Notes = "Notes";
}

/// <summary>Whether an opening is a window or a door.</summary>
public enum OpeningKind
{
    /// <summary>An opening with a sill above the floor: framed with a rough sill and cripples under it.</summary>
    Window,

    /// <summary>An opening down to the floor (a sill height of zero): no rough sill, no cripples under it.</summary>
    Door,
}

/// <summary>
/// A wall: a box standing as drawn, whose plan width is its length, whose plan height is its
/// thickness (the depth of its studs) and whose depth is its height (docs/building.md).
/// </summary>
/// <remarks>
/// A wall is not a new kind of entity; it is a way of reading a <see cref="Box"/>. A box is a wall
/// when it is on the layer called "Wall", or when it is itself called "Wall" (the
/// <c>wall-with-window</c> sample predates the layer).
/// </remarks>
/// <param name="Box">The box the wall is.</param>
public sealed record Wall(Box Box)
{
    /// <summary>The wall's id.</summary>
    public EntityId Id => Box.Id;

    /// <summary>What the wall is called on screen.</summary>
    public string Name => Box.Name.Length == 0 ? "Wall" : Box.Name;

    /// <summary>How long the wall is, along its own width.</summary>
    public Length Length => Box.Width;

    /// <summary>How thick the wall is: the depth of its studs.</summary>
    public Length Thickness => Box.Height;

    /// <summary>How tall the wall is, bottom of the bottom plate to top of the top plates.</summary>
    public Length Height => Box.Depth;

    /// <summary>Whether a box reads as a wall in this sketch.</summary>
    public static bool Is(Sketch sketch, Box box) => Reads(sketch, box, BuildingLayers.Wall, "Wall");

    /// <summary>Every wall in a sketch, in id order.</summary>
    public static ImmutableArray<Wall> All(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return [.. Boxes(sketch).Where(box => Is(sketch, box)).Select(box => new Wall(box))];
    }

    /// <summary>
    /// Where a box sits in this wall's own frame — along its length, across its thickness, and up
    /// from its bottom — or <see langword="null"/> when the two do not stand the same way.
    /// </summary>
    public Vector3? Local(Box other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Orientation != Box.Orientation || !Box.Orientation.IsExact)
        {
            return null;
        }

        return Box.Orientation.Unapply(other.Anchor - Box.Anchor);
    }

    internal static bool Reads(Sketch sketch, Box box, string layerName, string boxName)
    {
        if (string.Equals(box.Name, boxName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Layer? layer = sketch.Layers.FirstOrDefault(candidate => candidate.Id == box.Layer);
        return layer is not null && string.Equals(layer.Name, layerName, StringComparison.OrdinalIgnoreCase);
    }

    internal static IEnumerable<Box> Boxes(Sketch sketch)
        => sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id);
}

/// <summary>
/// A rough opening — a window or a door — in a wall: a box across the wall's full thickness, flush
/// with both its faces.
/// </summary>
/// <param name="Box">The box the opening is.</param>
/// <param name="Wall">The wall it is in.</param>
/// <param name="Offset">How far along the wall its near side is, from the wall's start (its west end as drawn).</param>
/// <param name="Sill">How high its bottom is above the wall's bottom.</param>
public sealed record Opening(Box Box, Wall Wall, Length Offset, Length Sill)
{
    /// <summary>The opening's id.</summary>
    public EntityId Id => Box.Id;

    /// <summary>What the opening is called on screen.</summary>
    public string Name => Box.Name.Length == 0 ? (Kind == OpeningKind.Door ? "Door" : "Window") : Box.Name;

    /// <summary>The rough opening's width, along the wall.</summary>
    public Length Width => Box.Width;

    /// <summary>The rough opening's height.</summary>
    public Length Height => Box.Depth;

    /// <summary>The top of the rough opening above the wall's bottom: where the header's underside is.</summary>
    public Length Top => Sill + Height;

    /// <summary>A door when it comes down to the wall's bottom, and a window otherwise.</summary>
    public OpeningKind Kind => Sill == Length.Zero ? OpeningKind.Door : OpeningKind.Window;

    /// <summary>Whether a box reads as an opening in this sketch (on the layer "Opening", or called "Opening").</summary>
    public static bool Is(Sketch sketch, Box box) => Wall.Reads(sketch, box, BuildingLayers.Opening, "Opening");

    /// <summary>
    /// The openings in a wall, nearest its start first: every opening box standing the way the wall
    /// does, across its whole thickness, flush with both faces, and overlapping its length.
    /// </summary>
    public static ImmutableArray<Opening> In(Sketch sketch, Wall wall)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(wall);

        List<Opening> found = [];
        foreach (Box box in Wall.Boxes(sketch).Where(box => Is(sketch, box) && !Wall.Is(sketch, box)))
        {
            if (wall.Local(box) is not { } at
                || at.Dy != Length.Zero
                || box.Height != wall.Thickness
                || at.Dx >= wall.Length
                || at.Dx + box.Width <= Length.Zero)
            {
                continue;
            }

            found.Add(new Opening(box, wall, at.Dx, at.Dz));
        }

        return [.. found.OrderBy(opening => opening.Offset).ThenBy(opening => opening.Id)];
    }

    /// <summary>The wall an opening box is in, or <see langword="null"/> when it is in none.</summary>
    public static Opening? Find(Sketch sketch, EntityId id)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return Wall.All(sketch).SelectMany(wall => In(sketch, wall)).FirstOrDefault(opening => opening.Id == id);
    }
}
