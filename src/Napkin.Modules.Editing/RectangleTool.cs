using System.Diagnostics.CodeAnalysis;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>Which tool the pointer is holding.</summary>
public enum EditTool
{
    /// <summary>Pick things, move them, and drag their handles.</summary>
    Select,

    /// <summary>Drag out a new rectangular part.</summary>
    Rectangle,

    /// <summary>
    /// Drag out a part already cut from a stock item picked in the toolbox (<see cref="StockTool"/>).
    /// </summary>
    Stock,

    /// <summary>Drag out a wall (<see cref="WallTool"/>, #18).</summary>
    Wall,

    /// <summary>Click on a wall to put a window or a door in it (#18).</summary>
    Opening,

    /// <summary>Drag out a room, or click inside walls for their inside faces (renovation-sketches §8).</summary>
    Room,

    /// <summary>Click to put a note (renovation-sketches §8).</summary>
    Note,
}

/// <summary>
/// The rectangle tool's state machine: press, drag, release, and what that makes.
/// </summary>
/// <remarks>
/// <para>
/// It holds two model points and nothing else — no pixels, no control, no sketch. The canvas
/// feeds it snapped model points and asks what to draw; on release it asks for the request, and
/// the request is an <see cref="AddEntity"/> like any other. Drawing a box is therefore the same
/// kind of thing as every other edit: something put to the updater (CVS-005).
/// </para>
/// <para>
/// A press and release with no drag between them is a click, not a rectangle, so it makes
/// nothing. There is no minimum size beyond that: a quarter-inch part is a real thing to want at
/// a high zoom, and the grid step already stops a drag from landing between two positions.
/// </para>
/// </remarks>
public sealed class RectangleTool
{
    Point2 _from;
    Point2 _to;

    /// <summary>Whether a rectangle is being dragged out now.</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>The corner the drag started at.</summary>
    public Point2 From => _from;

    /// <summary>The corner the pointer is at now.</summary>
    public Point2 To => _to;

    /// <summary>Starts a rectangle at a corner.</summary>
    public void Begin(Point2 corner)
    {
        _from = corner;
        _to = corner;
        IsDrawing = true;
    }

    /// <summary>Moves the far corner.</summary>
    public void MoveTo(Point2 corner)
    {
        if (IsDrawing)
        {
            _to = corner;
        }
    }

    /// <summary>Abandons the rectangle. Nothing is made.</summary>
    public void Cancel() => IsDrawing = false;

    /// <summary>
    /// The rectangle as it stands: the anchor corner and the two sizes, whichever way it was
    /// dragged.
    /// </summary>
    /// <returns><see langword="false"/> when it has no area yet.</returns>
    public bool TryRectangle(out Point2 anchor, out Length width, out Length height)
    {
        anchor = new Point2(Length.Min(_from.X, _to.X), Length.Min(_from.Y, _to.Y));
        width = Length.Abs(_to.X - _from.X);
        height = Length.Abs(_to.Y - _from.Y);
        return width > Length.Zero && height > Length.Zero;
    }

    /// <summary>
    /// Ends the drag, and gives back the request that adds what was drawn.
    /// </summary>
    /// <param name="layer">The layer the new part goes on.</param>
    /// <param name="id">The id the new part will have — stable from here on.</param>
    /// <param name="name">
    /// What to call it, so that nothing on screen is a GUID and a part drawn today keeps the name
    /// it was drawn with when the file is saved.
    /// </param>
    /// <param name="request">The request to put to the updater.</param>
    /// <returns><see langword="false"/> when the gesture was a click and made nothing.</returns>
    public bool TryComplete(
        LayerId layer,
        EntityId id,
        string name,
        [NotNullWhen(true)] out Request? request,
        EntryMode mode = EntryMode.Precise)
    {
        request = null;
        bool wasDrawing = IsDrawing;
        IsDrawing = false;

        if (!wasDrawing
            || !TryRectangle(out Point2 anchor, out Length width, out Length height))
        {
            return false;
        }

        // A bare rectangle lies as drawn at the plan datum, and every box now has a depth: the
        // visible, editable 3/4" default of docs/design/assembly-model.md §11 decision 7.
        // In Rough mode the rectangle is a plank: a part with no stock, marked rough, so it is on
        // the cut list from the start (docs/design/sketch-mode.md §2.3).
        request = new AddEntity(Box.AsDrawn(id, layer, anchor, width, height, Box.DefaultDepth, Angle.Zero) with
        {
            Name = name,
            Part = mode == EntryMode.Rough ? RoughEntry.Plank(width, height) : null,
        });
        return true;
    }
}
