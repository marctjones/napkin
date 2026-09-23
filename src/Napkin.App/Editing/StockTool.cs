using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.App.Editing;

/// <summary>
/// The stock tool's state machine: a stock item picked from the toolbox, then press, drag,
/// release — and what that makes is a part that already <em>is</em> that stock.
/// </summary>
/// <remarks>
/// <para>
/// Issue #7's picker, decided: "Placing an item sets the part's stock reference; the part's
/// footprint then follows the stock's cross-section." So a 2x4 is not drawn as a rectangle and then
/// assigned; the drag makes a box already 3 1/2&#x2033; across, and the request that adds it is
/// one <see cref="Batch"/> of the <see cref="AddEntity"/> and
/// <see cref="StockAssignment.RequestsFor"/>'s own requests — the same function the properties
/// panel calls, so which dimensions the yard fixes is said in one place for both paths.
/// </para>
/// <para>
/// <strong>What the drag decides, per stock kind</strong>, read off
/// <see cref="StockAssignment.Fixes"/> rather than restated here:
/// </para>
/// <list type="bullet">
/// <item>
/// A stock that fixes the <em>width</em> as well as the thickness — lumber and decking, a 2x4, a
/// 5/4x6 — leaves one free plan dimension, its length. The drag's longer direction is the length;
/// the other direction is the stock's width, whatever the pointer did across it. The part lies
/// flat: its thickness is the one the plan cannot show.
/// </item>
/// <item>
/// A stock that fixes only the thickness — a sheet good, a hardwood board sold in random widths —
/// leaves both plan dimensions free, so the drag is the whole rectangle, as the rectangle tool's
/// is. The longer side is named the length.
/// </item>
/// <item>
/// A stock that fixes nothing — a fastener — cannot be placed at all (<see cref="CanPlace"/>):
/// napkin has no fastener-as-a-part, and a nail drawn as a rectangle would be a lie on the cut
/// list.
/// </item>
/// </list>
/// <para>
/// A click with no drag makes nothing, as with the rectangle tool. A 4x4 leg seen from above —
/// the cross-section as the footprint, both plan dimensions fixed — would be a click-to-place, but
/// its length is then the one dimension the plan cannot show and nothing in a click states it, so
/// that placement is not offered here; a part placed flat can be turned on end in the properties
/// panel, which already works.
/// </para>
/// </remarks>
public sealed class StockTool
{
    Point2 _from;
    Point2 _to;

    /// <summary>The stock item the tool is holding, or null when nothing is picked.</summary>
    public StockItem? Stock { get; private set; }

    /// <summary>Whether a part is being dragged out now.</summary>
    public bool IsDrawing { get; private set; }

    /// <summary>
    /// Whether this item can be placed on the drawing as a part: whether the yard fixes any of its
    /// dimensions at all.
    /// </summary>
    public static bool CanPlace(StockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return !StockAssignment.Fixes(item).IsEmpty;
    }

    /// <summary>Picks up a stock item, or puts it down with null.</summary>
    /// <returns><see langword="false"/> when the item cannot be placed and nothing was picked up.</returns>
    public bool Arm(StockItem? item)
    {
        IsDrawing = false;
        if (item is not null && !CanPlace(item))
        {
            return false;
        }

        Stock = item;
        return true;
    }

    /// <summary>Starts a part at a point. Does nothing when no stock is picked up.</summary>
    public void Begin(Point2 at)
    {
        if (Stock is null)
        {
            return;
        }

        _from = at;
        _to = at;
        IsDrawing = true;
    }

    /// <summary>Moves the far end.</summary>
    public void MoveTo(Point2 at)
    {
        if (IsDrawing)
        {
            _to = at;
        }
    }

    /// <summary>Abandons the part. Nothing is made; the stock stays picked up.</summary>
    public void Cancel() => IsDrawing = false;

    /// <summary>
    /// The part as it stands: where its box is, its two plan sizes, and which of the three finished
    /// dimensions each of them is.
    /// </summary>
    /// <returns><see langword="false"/> when nothing has been dragged along a free dimension yet.</returns>
    public bool TryShape(out Point2 anchor, out Length width, out Length height, out PlanAxes axes)
    {
        anchor = default;
        width = Length.Zero;
        height = Length.Zero;
        axes = default;

        if (!IsDrawing || Stock is null)
        {
            return false;
        }

        ImmutableArray<FixedDimension> fixes = StockAssignment.Fixes(Stock);
        Length dx = _to.X - _from.X;
        Length dy = _to.Y - _from.Y;
        bool alongX = Length.Abs(dx) >= Length.Abs(dy);

        if (ValueFor(fixes, PartDimension.Width) is { } stockWidth)
        {
            // One free dimension: the length runs the way the drag mostly went, and the width
            // goes off the press point on whichever side the pointer leaned, so the board lies
            // where it was drawn rather than centred on nothing in particular.
            Length run = alongX ? Length.Abs(dx) : Length.Abs(dy);
            if (run <= Length.Zero)
            {
                return false;
            }

            if (alongX)
            {
                anchor = new Point2(
                    Length.Min(_from.X, _to.X),
                    dy >= Length.Zero ? _from.Y : _from.Y - stockWidth);
                width = run;
                height = stockWidth;
                axes = new PlanAxes(PartDimension.Length, PartDimension.Width);
            }
            else
            {
                anchor = new Point2(
                    dx >= Length.Zero ? _from.X : _from.X - stockWidth,
                    Length.Min(_from.Y, _to.Y));
                width = stockWidth;
                height = run;
                axes = new PlanAxes(PartDimension.Width, PartDimension.Length);
            }

            return true;
        }

        // Both plan dimensions free: the drag is the rectangle, and the longer side is the length.
        anchor = new Point2(Length.Min(_from.X, _to.X), Length.Min(_from.Y, _to.Y));
        width = Length.Abs(dx);
        height = Length.Abs(dy);
        axes = alongX
            ? new PlanAxes(PartDimension.Length, PartDimension.Width)
            : new PlanAxes(PartDimension.Width, PartDimension.Length);
        return width > Length.Zero && height > Length.Zero;
    }

    /// <summary>
    /// Ends the drag, and gives back the one request that adds a part already cut from the stock.
    /// </summary>
    /// <param name="sketch">The design the part goes into, which the stock assignment reads.</param>
    /// <param name="layer">The layer the new part goes on.</param>
    /// <param name="id">The id the new part will have.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="request">
    /// A <see cref="Batch"/>: the <see cref="AddEntity"/>, then
    /// <see cref="StockAssignment.RequestsFor"/>'s <see cref="SetPart"/> and the stated size of each
    /// plan dimension the stock fixes — atomic, so a part is never on the drawing without its stock.
    /// </param>
    /// <returns><see langword="false"/> when the gesture made nothing.</returns>
    public bool TryComplete(
        Sketch sketch,
        LayerId layer,
        EntityId id,
        string name,
        [NotNullWhen(true)] out Request? request)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        request = null;

        bool shaped = TryShape(out Point2 anchor, out Length width, out Length height, out PlanAxes axes);
        IsDrawing = false;
        if (!shaped || Stock is not { } stock)
        {
            return false;
        }

        // Every placeable stock fixes the thickness, and a part lying flat has its thickness out
        // of the plan — its depth — so this is the value the assignment will state for it too.
        Length thickness = ValueFor(StockAssignment.Fixes(stock), PartDimension.Thickness)
                           ?? throw new InvalidOperationException(
                               $"{stock.Name} fixes no thickness, so it cannot lie flat.");

        Box box = Box.AsDrawn(id, layer, anchor, width, height, thickness, Angle.Zero) with { Name = name };
        Part part = new(stock.Name, Species: null, Quantity: 1, axes);

        request = Batch.Of(new AddEntity(box), StockAssignment.RequestsFor(sketch, box, part, stock));
        return true;
    }

    static Length? ValueFor(ImmutableArray<FixedDimension> fixes, PartDimension dimension)
    {
        foreach (FixedDimension fixed_ in fixes)
        {
            if (fixed_.Dimension == dimension)
            {
                return fixed_.Value;
            }
        }

        return null;
    }
}
