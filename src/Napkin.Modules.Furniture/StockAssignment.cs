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
    /// What a stock field says back about what was typed into it: the library item it names, that
    /// nothing was chosen, or that the library does not carry it — never a guess.
    /// </summary>
    public static string Readout(string? typed, MaterialsLibrary library)
    {
        string name = (typed ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "No stock chosen. The cut list shows the size you typed.";
        }

        return library.TryFind(name, out StockItem item)
            ? item.HoverText
            : $"\"{name}\" is not in this build's materials library. "
              + "The cut list will say so rather than guess.";
    }

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
    /// <item>the <see cref="SetPart"/> that makes the box this part;</item>
    /// <item>
    /// a request for each dimension the stock fixes, on whichever of <see cref="BoxWidthRef"/>,
    /// <see cref="BoxHeightRef"/> and <see cref="BoxDepthRef"/> <see cref="PlanAxes"/> puts it:
    /// an <c>AddRelationship(ParamValue(…))</c> where nothing drives that size, because a fixed
    /// size is a stated fact and the yard is the one who states it — or a
    /// <see cref="SetParameter"/> on the <see cref="ParamValue"/> already driving it, so that a
    /// width typed before the stock was chosen is a change to that number rather than a second
    /// owner for it (<c>docs/design/geometry-model.md</c> §4.1; without this the assignment would
    /// be <see cref="RejectionReason.DuplicateRelationship"/>).
    /// </item>
    /// </list>
    /// <para>
    /// All three go through the updater alike. The out-of-plane dimension — a flat 1x6's
    /// ¾&#x2033; thickness — was once set on the part directly, "nothing else depending on it"; it
    /// is the box's <see cref="Box.Depth"/> now, and a <see cref="Flush"/> on a top or bottom face
    /// can depend on it, so it is a <see cref="ParamValue"/> on the depth like the other two and a
    /// conflict on it is reported like any other (<c>docs/design/assembly-model.md</c> §1.2).
    /// </para>
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

        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        requests.Add(new SetPart(box.Id, part));

        foreach ((PartDimension name, ParamRef size) in (ReadOnlySpan<(PartDimension, ParamRef)>)
                 [
                     (axes.X, new BoxWidthRef(box.Id)),
                     (axes.Y, new BoxHeightRef(box.Id)),
                     (axes.OutOfPlane, new BoxDepthRef(box.Id)),
                 ])
        {
            if (ValueFor(fixes, name) is { } value)
            {
                requests.Add(SizeRequest(sketch, size, value));
            }
        }

        return new Batch(requests.ToImmutable());
    }

    /// <summary>
    /// The requests that make <paramref name="strut"/> a piece of <paramref name="part"/> and land
    /// what the stock fixes on its cross-section, as one batch — the box's rule above, for an angled
    /// part (<c>docs/design/assembly-model.md</c> &#xA7;3a.5).
    /// </summary>
    /// <remarks>
    /// <see cref="PlanAxes.X"/> names the strut's derived dimension — its length, or an angled
    /// shelf's width — which comes from where its ends are and is never the yard's to state, so a
    /// stock that fixes it fixes nothing here. <see cref="PlanAxes.Y"/> lands on the strut's height and
    /// the dimension out of the plane on its depth, each as a <see cref="ParamValue"/> or a change
    /// to the one already driving it.
    /// </remarks>
    /// <param name="sketch">The design the strut is in, read for the relationship that drives a size.</param>
    /// <param name="strut">The strut the part is on.</param>
    /// <param name="part">What the strut is a piece of, with the stock name already on it.</param>
    /// <param name="stock">What that name resolved to, or <see langword="null"/>.</param>
    public static Batch RequestsFor(Sketch sketch, Strut strut, Part part, StockItem? stock)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(strut);
        ArgumentNullException.ThrowIfNull(part);

        ImmutableArray<FixedDimension> fixes = Fixes(stock);
        ImmutableList<Request>.Builder requests = ImmutableList.CreateBuilder<Request>();
        requests.Add(new SetPart(strut.Id, part));

        foreach ((PartDimension name, ParamRef size) in (ReadOnlySpan<(PartDimension, ParamRef)>)
                 [
                     (part.PlanAxes.Y, new StrutHeightRef(strut.Id)),
                     (part.PlanAxes.OutOfPlane, new StrutDepthRef(strut.Id)),
                 ])
        {
            if (ValueFor(fixes, name) is { } value)
            {
                requests.Add(SizeRequest(sketch, size, value));
            }
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
    /// Whether <paramref name="stock"/> fixes the dimension of <paramref name="part"/> that lies
    /// along the box's local Z — whether its depth is the yard's to state.
    /// </summary>
    /// <param name="part">The part, for which of its dimensions is out of the plan.</param>
    /// <param name="stock">The library item, or <see langword="null"/> for no stock.</param>
    public static bool FixesDepth(Part part, StockItem? stock)
    {
        ArgumentNullException.ThrowIfNull(part);
        return ValueFor(Fixes(stock), part.PlanAxes.OutOfPlane) is not null;
    }

    /// <summary>
    /// The request that puts a size at a value: one number, one owner (geometry model §4.1). A
    /// <see cref="SetParameter"/> on the <see cref="ParamValue"/> that already drives
    /// <paramref name="size"/>, or a new <see cref="ParamValue"/> where nothing does.
    /// </summary>
    /// <param name="sketch">The design, read for the relationship that drives the size.</param>
    /// <param name="size">The size to state.</param>
    /// <param name="value">What to state it as.</param>
    public static Request SizeRequest(Sketch sketch, ParamRef size, Length value)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(size);

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
