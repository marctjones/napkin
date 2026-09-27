using System.Diagnostics.CodeAnalysis;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;

namespace Napkin.Modules.Editing;

/// <summary>
/// The wall tool's state machine (#18): press, drag the wall's length, release. The wall is as thick
/// as its stud stock is deep and as tall as <see cref="Height"/>; the frame it implies is
/// <see cref="FramingList"/>'s to work out, never drawn.
/// </summary>
/// <remarks>
/// A drag mostly east–west makes a wall standing as drawn; one mostly north–south makes the same wall
/// turned a quarter, so the wall's own length is always its width and an opening can be measured
/// along it. The thickness goes off the press point on the side the pointer leaned, as a stock
/// part's width does.
/// </remarks>
public sealed class WallTool
{
    Point2 _from;
    Point2 _to;

    /// <summary>
    /// The height a new wall starts at, 8'-0": a starting value to type over in the panel's Depth,
    /// not a standard (the <c>wall-with-window</c> sample's height, Marc, 2026-09-23).
    /// </summary>
    public static readonly Length StartingHeight = Length.Inches(96);

    /// <summary>The stud stock new walls are framed from: its dressed width is their thickness.</summary>
    public LumberStock? Member { get; set; }

    /// <summary>How tall a new wall is.</summary>
    public Length Height { get; set; } = StartingHeight;

    /// <summary>Whether a drag is under way.</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>Starts a drag.</summary>
    public void Begin(Point2 at)
    {
        if (Member is null)
        {
            return;
        }

        _from = at;
        _to = at;
        IsDrawing = true;
    }

    /// <summary>Follows the pointer.</summary>
    public void MoveTo(Point2 at)
    {
        if (IsDrawing)
        {
            _to = at;
        }
    }

    /// <summary>Abandons the drag. Nothing is made.</summary>
    public void Cancel() => IsDrawing = false;

    /// <summary>The wall a release would make now, as a box; false while the drag has no length.</summary>
    public bool TryShape(LayerId layer, EntityId id, string name, [NotNullWhen(true)] out Box? wall)
    {
        wall = null;
        if (!IsDrawing || Member is not { } member)
        {
            return false;
        }

        Length thickness = member.Width;
        Length dx = _to.X - _from.X;
        Length dy = _to.Y - _from.Y;
        bool alongX = Length.Abs(dx) >= Length.Abs(dy);
        Length run = alongX ? Length.Abs(dx) : Length.Abs(dy);
        if (run <= Length.Zero)
        {
            return false;
        }

        // Turned a quarter, the wall's length runs north and its thickness west of its anchor.
        Point2 anchor = alongX
            ? new Point2(Length.Min(_from.X, _to.X), dy >= Length.Zero ? _from.Y : _from.Y - thickness)
            : new Point2(dx >= Length.Zero ? _from.X + thickness : _from.X, Length.Min(_from.Y, _to.Y));

        wall = Box.AsDrawn(id, layer, anchor, run, thickness, Height, alongX ? Angle.Zero : Angle.Right) with { Name = name };
        return true;
    }

    /// <summary>
    /// Ends the drag, and gives back the request that adds the wall (and its layer, if new). Given the
    /// sketch, a wall drawn within a deck's outline stands on its decking (deck-and-porch §5.1).
    /// </summary>
    public bool TryComplete(LayerId layer, Request? addLayer, EntityId id, string name, [NotNullWhen(true)] out Request? request, Sketch? sketch = null)
    {
        request = null;
        bool shaped = TryShape(layer, id, name, out Box? wall);
        IsDrawing = false;
        if (!shaped)
        {
            return false;
        }

        if (sketch is not null)
        {
            wall = OnDecking(sketch, wall!);
        }

        request = addLayer is null ? new AddEntity(wall!) : Batch.Of(addLayer, new AddEntity(wall!));
        return true;
    }

    /// <summary>
    /// The wall standing on the decking of the deck whose outline holds its footprint — its anchor's z
    /// the deck's top — or the wall as drawn when no deck holds it.
    /// </summary>
    public static Box OnDecking(Sketch sketch, Box wall)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(wall);
        if (Deck.Bounds(wall) is not { } w)
        {
            return wall;
        }

        Deck? under = Deck.All(sketch).FirstOrDefault(deck => deck.Box.Phase != Phase.Demolish && deck.Outline is { } o
            && o.West <= w.West && w.East <= o.East && o.South <= w.South && w.North <= o.North);
        return under is null ? wall : wall with { Anchor = wall.Anchor with { Z = under.Box.Anchor.Z + under.Height } };
    }
}
