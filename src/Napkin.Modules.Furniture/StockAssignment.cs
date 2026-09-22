using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One of a part's three finished dimensions, fixed at a value by the stock it is cut from.
/// </summary>
/// <param name="Dimension">Which of the three the yard fixes.</param>
/// <param name="Value">What it fixes it at, read from the materials library.</param>
public readonly record struct FixedDimension(PartDimension Dimension, Length Value);

/// <summary>
/// Assigning a stock item to a part: which of its dimensions the yard fixes, and the requests that
/// make the blank really be that stock.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A blank that claims to be a 1x6 but is 4&#x2033; wide is exactly the silent error the
/// cut list exists to prevent.</strong> So a stock name is not just stored: a stock item fixes some
/// of a part's three named dimensions, <see cref="PlanAxes"/> says which of those land in the plan,
/// and on assignment the box's fixed parameters are set <em>through the updater</em> so that
/// relationships propagate and a conflict is reported like any other
/// (<c>docs/design/parts-and-cut-list.md</c> §1.2, <c>docs/design/shaped-parts-model.md</c> §1.2).
/// </para>
/// <para>
/// <strong>A pure function, here rather than in the canvas.</strong> "Which dimensions does this
/// stock fix, given <c>planAxes</c>" is a fact about parts and lumber, not about a window, so it
/// lives beside <see cref="CutList"/> in the furniture module and the properties panel only calls
/// it — the same reason the cut list lives here (<c>Napkin.App</c> is outside the coverage
/// ratchet). It builds requests and applies none of them: the caller puts the batch through its own
/// editor, which is what makes a conflict its ordinary self.
/// </para>
/// </remarks>
public static class StockAssignment
{
    /// <summary>
    /// Which of the three finished dimensions this stock item fixes, and at what.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table of <c>docs/design/parts-and-cut-list.md</c> §1.2, by stock kind:
    /// <see cref="LumberStock"/> — a 2x4, a 1x6 — fixes the whole cross-section, its width and its
    /// thickness, and leaves the length to the design. <see cref="PanelStock"/> fixes the
    /// thickness; a plywood shelf is any size you like at the panel's thickness.
    /// <see cref="HardwoodStock"/> fixes the thickness too, and it is the <em>surfaced</em>
    /// thickness — the rough one is what you pay for, not what the piece finishes at — because
    /// hardwood comes in random widths, so a part cut from it has two free plan dimensions.
    /// </para>
    /// <para>
    /// Anything else — a fastener, or no stock at all — fixes nothing. A part whose stock is
    /// <see langword="null"/> or whose name the library does not carry is legal and is not an
    /// error: its finished dimensions are its own (§1.2, open decision §11.10).
    /// </para>
    /// </remarks>
    /// <param name="stock">The library item, or <see langword="null"/> for no stock.</param>
    public static ImmutableArray<FixedDimension> Fixes(StockItem? stock) => stock switch
    {
        LumberStock lumber =>
        [
            new FixedDimension(PartDimension.Width, lumber.Width),
            new FixedDimension(PartDimension.Thickness, lumber.Thickness),
        ],

        PanelStock panel => [new FixedDimension(PartDimension.Thickness, panel.Thickness)],

        HardwoodStock hardwood =>
            [new FixedDimension(PartDimension.Thickness, hardwood.SurfacedTwoSides)],

        _ => [],
    };

    /// <summary>
    /// The requests that assign <paramref name="stock"/> to <paramref name="box"/> as
    /// <paramref name="part"/>, as one batch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>One <see cref="Batch"/>, so that nothing is half-assigned.</strong> If any of it
    /// conflicts — the part is pinned to something that cannot move — the whole assignment is
    /// refused and the part keeps both its old size and its old stock (§1.2 step 2). The batch is,
    /// in order:
    /// </para>
    /// <list type="number">
    /// <item>
    /// the <see cref="SetPart"/> that makes the box this part, carrying the out-of-plane dimension
    /// from the stock when the fixed dimension is the out-of-plane one — a flat 1x6's ¾&#x2033;
    /// thickness — since §1.2 sets that value on the part directly, nothing else depending on it;
    /// </item>
    /// <item>
    /// a request for each <em>in-plan</em> dimension the stock fixes: an
    /// <c>AddRelationship(ParamValue(…))</c> where nothing drives that size, because a fixed size
    /// is a stated fact and the yard is the one who states it — or a <see cref="SetParameter"/> on
    /// the <see cref="ParamValue"/> already driving it, so that a width typed before the stock was
    /// chosen is a change to that number rather than a second owner for it
    /// (<c>docs/design/geometry-model.md</c> §4.1; without this the assignment would be
    /// <see cref="RejectionReason.DuplicateRelationship"/>).
    /// </item>
    /// </list>
    /// <para>
    /// A dimension the stock fixes is driven from then on, so a drag on that edge is
    /// <see cref="RejectionReason.DrivenSize"/> — not because cuts or stock are special, but
    /// because the yard now owns that number the way a typed dimension does. Changing the stock
    /// (1x6 → 1x4) is this same function run again, which is a resize through the updater.
    /// </para>
    /// <para>
    /// <strong>Nothing is ever un-fixed.</strong> Assigning no stock, or a name the library does
    /// not carry, fixes nothing and removes nothing: the batch is the <see cref="SetPart"/> alone,
    /// and a size a previous stock stated stays stated until someone says otherwise. A stated size
    /// is a stated fact (§2.3's reasoning, applied to the other direction).
    /// </para>
    /// </remarks>
    /// <param name="sketch">The design the box is in, read for the relationship that drives a size.</param>
    /// <param name="box">The box the part is on. Its dimensions are not read: they are what this changes.</param>
    /// <param name="part">What the box is a piece of, with the stock name already on it.</param>
    /// <param name="stock">
    /// What that name resolved to in the materials library, or <see langword="null"/> when the part
    /// has no stock or the library does not carry the name.
    /// </param>
    public static Batch RequestsFor(Sketch sketch, Box box, Part part, StockItem? stock)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(part);

        ImmutableArray<FixedDimension> fixes = Fixes(stock);
        PlanAxes axes = part.PlanAxes;

        // The one name neither axis claims is the out-of-plane one, and a stock that fixes it sets
        // it on the part rather than on the box: a plan view has two axes, and this is the third.
        Length outOfPlane = ValueFor(fixes, axes.OutOfPlane) ?? part.OutOfPlane;

        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        requests.Add(new SetPart(box.Id, part with { OutOfPlane = outOfPlane }));

        if (ValueFor(fixes, axes.X) is { } width)
        {
            requests.Add(SizeRequest(sketch, new BoxWidthRef(box.Id), width));
        }

        if (ValueFor(fixes, axes.Y) is { } height)
        {
            requests.Add(SizeRequest(sketch, new BoxHeightRef(box.Id), height));
        }

        return new Batch(requests.ToImmutable());
    }

    /// <summary>What the stock fixes this dimension at, or null when it leaves it free.</summary>
    private static Length? ValueFor(ImmutableArray<FixedDimension> fixes, PartDimension dimension)
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

    /// <summary>
    /// The request that puts a size at a value: one number, one owner (geometry model §4.1).
    /// </summary>
    private static Request SizeRequest(Sketch sketch, ParamRef size, Length value)
    {
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (relationship is ParamValue driving && driving.Param == size)
            {
                return new SetParameter(driving.Id, value);
            }
        }

        return new AddRelationship(new ParamValue(RelationshipId.New(), size, value));
    }
}
