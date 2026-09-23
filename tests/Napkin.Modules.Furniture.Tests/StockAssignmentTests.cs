using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Stock makes the blank: assigning a stock item to a part fixes the dimensions the yard fixes,
/// through the updater, as one batch.
/// </summary>
/// <remarks>
/// <para>
/// The rule is <c>docs/design/parts-and-cut-list.md</c> §1.2 and the reason it matters is
/// <c>docs/design/shaped-parts-model.md</c> §1.2: a blank that claims to be a 1x6 but is 4&#x2033;
/// wide is exactly the silent error the cut list exists to prevent. These are
/// <c>shaped-parts-model.md</c> §9.1's test 11c minus its cut clause, which §10 step 2 scopes out
/// of this step, plus one case per row of §1.2's table.
/// </para>
/// <para>
/// <strong>No dressed size is written down here.</strong> Every expected number is read from
/// <see cref="MaterialsLibrary.Shipped"/>, which carries its own citation, so this suite cannot
/// pass against a wrong number that both it and the library agree on (CLAUDE.md, "Data and
/// citations").
/// </para>
/// </remarks>
public sealed class StockAssignmentTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static readonly IGeometryUpdater Updater = new DirectUpdater();

    /// <summary>A part lying flat: the plan shows its length and its width.</summary>
    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);

    /// <summary>A part drawn as its footprint, standing up: the plan shows its cross-section.</summary>
    private static readonly PlanAxes Footprint = new(PartDimension.Width, PartDimension.Thickness);

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_flat_part_assigned_a_1x6_is_really_five_and_a_half_inches_wide()
    {
        // §9.1 test 11c: assigning "1x6" to a flat part sets its width to 5 1/2" through the
        // updater, exactly, and a drag on that edge is then refused.
        LumberStock lumber = Lumber("1x6");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);

        // Drawn 48" long and 8" across: the length is the person's, the width is the yard's.
        (Sketch sketch, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(sketch, StockAssignment.RequestsFor(sketch, box, part, lumber)));

        Box assigned = result.Sketch.Find<Box>(box.Id)!;
        Assert.Equal(Length.Inches(48), assigned.Width);
        Assert.Equal(lumber.Width, assigned.Height);
        Assert.Equal(5632, assigned.Height.Units);

        // The thickness is the one dimension the plan cannot hold: the box's depth, which the yard
        // states through the updater like the width (assembly-model §1.2).
        Assert.Equal(lumber.Thickness, assigned.Depth);
        Assert.Equal(
            lumber.Thickness,
            Assert.Single(
                result.Sketch.RelationshipsInOrder.OfType<ParamValue>(),
                value => value.Param.Equals(new BoxDepthRef(box.Id))).Value);

        // And the cut list now says a 1x6's real size, which is the whole point of the step.
        CutListRow row = Assert.Single(CutList.Of(result.Sketch, Library));
        Assert.Equal(Length.Inches(48), row.Length);
        Assert.Equal(lumber.Width, row.Width);
        Assert.Equal(lumber.Thickness, row.Thickness);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_dimension_the_yard_fixes_cannot_then_be_dragged()
    {
        // Not new behaviour and not re-implemented here: the width is driven by a ParamValue like
        // any typed dimension, and a drag never silently overrides a number somebody stated
        // (geometry-model §4.1). The yard is the one who stated this one.
        LumberStock lumber = Lumber("1x6");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        (Sketch sketch, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        Sketch assigned = Assert.IsType<Solved>(
            Updater.Apply(sketch, StockAssignment.RequestsFor(sketch, box, part, lumber))).Sketch;

        Rejected refused = Assert.IsType<Rejected>(
            Updater.Apply(assigned, new DragFace(box.Id, BoxFace.North, Length.Inches(2))));
        Assert.Equal(RejectionReason.DrivenSize, refused.Reason);

        // The free dimension is still free: dragging the end of the board still lengthens it.
        Assert.IsType<Solved>(
            Updater.Apply(assigned, new DragFace(box.Id, BoxFace.East, Length.Inches(2))));
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_footprint_part_assigned_a_2x4_has_both_plan_dimensions_set()
    {
        // §1.2: a LumberStock drawn as a footprint fixes both plan dimensions — the blank is the
        // stock's cross-section — and leaves only the length, which is out of plane, free.
        LumberStock lumber = Lumber("2x4");
        Part part = new("2x4", Species: null, Quantity: 1, Footprint);
        (Sketch sketch, Box box) = OneBox(Length.Inches(4), Length.Inches(2), part, depth: Length.Inches(30));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(sketch, StockAssignment.RequestsFor(sketch, box, part, lumber)));

        Box assigned = result.Sketch.Find<Box>(box.Id)!;
        Assert.Equal(lumber.Width, assigned.Width);
        Assert.Equal(lumber.Thickness, assigned.Height);

        // The length is the free one, so it is left exactly as the design stated it.
        Assert.Equal(Length.Inches(30), assigned.Depth);

        // Both are driven now, so neither edge of the footprint can be dragged.
        foreach (BoxFace face in new[] { BoxFace.North, BoxFace.East })
        {
            Rejected refused = Assert.IsType<Rejected>(
                Updater.Apply(result.Sketch, new DragFace(box.Id, face, Length.Inches(1))));
            Assert.Equal(RejectionReason.DrivenSize, refused.Reason);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_panel_fixes_the_thickness_and_leaves_both_plan_dimensions_free()
    {
        // §1.2: a plywood shelf is any size you like, at the panel's thickness.
        StockItem panel = Find("3/4 plywood");
        Part part = new("3/4 plywood", Species: null, Quantity: 1, Flat);
        (Sketch sketch, Box box) = OneBox(Length.Inches(36), Length.Inches(11), part, depth: Length.Inches(1));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(sketch, StockAssignment.RequestsFor(sketch, box, part, panel)));

        Box assigned = result.Sketch.Find<Box>(box.Id)!;
        Assert.Equal(Length.Inches(36), assigned.Width);
        Assert.Equal(Length.Inches(11), assigned.Height);
        Assert.Equal(((PanelStock)panel).Thickness, assigned.Depth);

        // Nothing drives either plan dimension, so a shelf can still be dragged to size.
        Assert.IsType<Solved>(
            Updater.Apply(result.Sketch, new DragFace(box.Id, BoxFace.North, Length.Inches(1))));
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Hardwood_fixes_the_surfaced_thickness_not_the_rough_one()
    {
        // §1.2: the surfaced thickness is what the piece finishes at; the rough one is what you
        // pay for. And hardwood comes in random widths, so both plan dimensions stay free.
        HardwoodStock hardwood = (HardwoodStock)Find("4/4");
        Part part = new("4/4", Species: "walnut", Quantity: 1, Flat);
        (Sketch sketch, Box box) = OneBox(Length.Inches(24), Length.Inches(6), part, depth: Length.Inches(1));

        Solved result = Assert.IsType<Solved>(
            Updater.Apply(sketch, StockAssignment.RequestsFor(sketch, box, part, hardwood)));

        Box assigned = result.Sketch.Find<Box>(box.Id)!;
        Assert.Equal(Length.Inches(24), assigned.Width);
        Assert.Equal(Length.Inches(6), assigned.Height);
        Assert.Equal(hardwood.SurfacedTwoSides, assigned.Depth);
        Assert.NotEqual(hardwood.RoughThickness, assigned.Depth);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void No_stock_and_a_fastener_fix_nothing()
    {
        // A part whose stock is null is legal and is not an error: its finished dimensions are its
        // own (§1.2, open decision §11.10). A nail is not something you cut a part from at all.
        Assert.Empty(StockAssignment.Fixes(null));
        Assert.Empty(StockAssignment.Fixes(Find("16d")));

        Part part = new(Stock: null, Species: null, Quantity: 1, Flat);
        (Sketch sketch, Box box) = OneBox(Length.Inches(40), Length.Inches(9), part, depth: Length.Inches(1));

        Batch batch = StockAssignment.RequestsFor(sketch, box, part, stock: null);
        Assert.IsType<SetPart>(Assert.Single(batch.Requests));

        Box assigned = Assert.IsType<Solved>(Updater.Apply(sketch, batch)).Sketch.Find<Box>(box.Id)!;
        Assert.Equal(Length.Inches(40), assigned.Width);
        Assert.Equal(Length.Inches(9), assigned.Height);
        Assert.Equal(Length.Inches(1), assigned.Depth);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Each_stock_kind_fixes_the_dimensions_its_row_of_the_table_states()
    {
        // §1.2's table, read directly rather than through a request: a LumberStock fixes the whole
        // cross-section, a panel and hardwood fix the thickness alone.
        LumberStock lumber = Lumber("1x6");
        Assert.Equal(
            [
                new FixedDimension(PartDimension.Width, lumber.Width),
                new FixedDimension(PartDimension.Thickness, lumber.Thickness),
            ],
            StockAssignment.Fixes(lumber));

        PanelStock panel = (PanelStock)Find("23/32 plywood");
        Assert.Equal(
            [new FixedDimension(PartDimension.Thickness, panel.Thickness)],
            StockAssignment.Fixes(panel));

        HardwoodStock hardwood = (HardwoodStock)Find("8/4");
        Assert.Equal(
            [new FixedDimension(PartDimension.Thickness, hardwood.SurfacedTwoSides)],
            StockAssignment.Fixes(hardwood));

        // No stock fixes a length: that is what the yard does not decide for you.
        Assert.DoesNotContain(
            StockAssignment.Fixes(lumber),
            fixed_ => fixed_.Dimension == PartDimension.Length);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_width_typed_before_the_stock_was_chosen_is_changed_rather_than_duplicated()
    {
        // geometry-model §4.1: one number, one owner. Typing 8" first and then choosing a 1x6 is a
        // change to that number, not a second ParamValue on the same size — which would be
        // Rejected(DuplicateRelationship) and would refuse the assignment for no honest reason.
        LumberStock lumber = Lumber("1x6");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        (Sketch drawn, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        RelationshipId typed = RelationshipId.New();
        Sketch sketch = Assert.IsType<Solved>(Updater.Apply(
            drawn,
            new AddRelationship(new ParamValue(typed, new BoxHeightRef(box.Id), Length.Inches(8)))))
            .Sketch;

        Batch batch = StockAssignment.RequestsFor(sketch, box, part, lumber);
        Assert.Contains(batch.Requests, request => request is SetParameter parameter && parameter.Driving == typed);
        Assert.DoesNotContain(
            batch.Requests,
            request => request is AddRelationship { Relationship: ParamValue { Param: BoxHeightRef } });

        Solved result = Assert.IsType<Solved>(Updater.Apply(sketch, batch));

        Assert.Equal(lumber.Width, result.Sketch.Find<Box>(box.Id)!.Height);
        Assert.Equal(
            Length.Inches(8),
            ((ParamValue)sketch.Find(typed)!).Value);
        Assert.Equal(lumber.Width, ((ParamValue)result.Sketch.Find(typed)!).Value);

        // Still exactly one owner for that number afterwards.
        Assert.Single(
            result.Sketch.RelationshipsInOrder.OfType<ParamValue>(),
            value => value.Param.Equals(new BoxHeightRef(box.Id)));
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Changing_the_stock_resizes_the_blank()
    {
        // §1.2: changing the stock (1x6 -> 1x4) is this same function run again, which is a resize
        // through the updater. The case where that resize no longer fits a cut is the test below.
        LumberStock wider = Lumber("1x6");
        LumberStock narrower = Lumber("1x4");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        (Sketch drawn, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        Sketch onA1x6 = Assert.IsType<Solved>(
            Updater.Apply(drawn, StockAssignment.RequestsFor(drawn, box, part, wider))).Sketch;
        Assert.Equal(wider.Width, onA1x6.Find<Box>(box.Id)!.Height);

        Part rebought = part with { Stock = "1x4" };
        Sketch onA1x4 = Assert.IsType<Solved>(Updater.Apply(
            onA1x6,
            StockAssignment.RequestsFor(onA1x6, onA1x6.Find<Box>(box.Id)!, rebought, narrower)))
            .Sketch;

        Box assigned = onA1x4.Find<Box>(box.Id)!;
        Assert.Equal(narrower.Width, assigned.Height);
        Assert.True(narrower.Width < wider.Width);
        Assert.Equal(Length.Inches(48), assigned.Width);
        Assert.Equal("1x4", assigned.Part!.Stock);

        // The second assignment re-used the first's ParamValue rather than adding a second owner.
        Assert.Single(
            onA1x4.RelationshipsInOrder.OfType<ParamValue>(),
            value => value.Param.Equals(new BoxHeightRef(box.Id)));
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Rebuying_a_part_on_narrower_stock_is_refused_when_a_cut_no_longer_fits()
    {
        // The other half of §9.1's test 11c, and the reason
        // docs/design/shaped-parts-model.md §2.3 puts its fit check in the updater rather than in
        // any one caller: StockAssignment builds an ordinary ParamValue resize and knows nothing
        // about cuts, so nothing here had to change for this to be refused.
        //
        // §1.2: "changing the stock (1x6 -> 1x4) is a resize through the updater, and §2.3's
        // check refuses it if a cut no longer fits the narrower blank". Both widths are read from
        // the library, which carries their citation; the setback is 4", chosen between them.
        LumberStock wider = Lumber("1x6");
        LumberStock narrower = Lumber("1x4");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        (Sketch drawn, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        Sketch onA1x6 = Assert.IsType<Solved>(
            Updater.Apply(drawn, StockAssignment.RequestsFor(drawn, box, part, wider))).Sketch;

        // A 4" setback across the width: it fits the 1x6 and cannot fit the 1x4.
        Length setback = Length.Inches(4);
        Assert.True(setback < wider.Width, "the setback has to fit the wider stock");
        Assert.True(setback > narrower.Width, "and not fit the narrower one");

        Sketch shaped = Assert.IsType<Solved>(Updater.Apply(
            onA1x6,
            new SetCut(box.Id, new CornerCut(BoxCorner.SouthEast, Length.Inches(6), setback)))).Sketch;

        Part rebought = part with { Stock = "1x4" };
        Rejected refused = Assert.IsType<Rejected>(Updater.Apply(
            shaped,
            StockAssignment.RequestsFor(shaped, shaped.Find<Box>(box.Id)!, rebought, narrower)));

        Assert.Equal(RejectionReason.CutDoesNotFit, refused.Reason);
        Assert.Contains("SouthEast corner", refused.Detail!.Message, StringComparison.Ordinal);

        // Nothing is half-assigned: the batch sets the part before it resizes, so without the
        // atomicity the box would now claim to be a 1x4 at the 1x6's width.
        Box unchanged = shaped.Find<Box>(box.Id)!;
        Assert.Equal("1x6", unchanged.Part!.Stock);
        Assert.Equal(wider.Width, unchanged.Height);
        Assert.Equal(setback, unchanged.Cuts.OfType<CornerCut>().Single().AlongY);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Nothing_is_half_assigned_when_one_of_the_dimensions_conflicts()
    {
        // §1.2 step 2: if any of the assignment conflicts — the part is pinned to something that
        // cannot move — the whole thing is refused, and the part keeps both its old size and its
        // old stock. The batch sets the width first and the height second, so this pins the
        // height: if the batch were not atomic the width would already be the 2x4's.
        LumberStock lumber = Lumber("2x4");
        Part leg = new("2x4", Species: null, Quantity: 1, Footprint);
        Piece plain = new(Stock: null, Species: null, Quantity: 1, Length.Inches(30), Footprint);

        Sketch drawn = Design.WithParts(
            ("Leg", Length.Inches(4).Units, Length.Inches(2).Units, plain),
            ("Stretcher", Length.Inches(4).Units, Length.Inches(2).Units, plain));

        Box[] boxes = [.. drawn.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        Box leg_ = boxes[0];
        Box other = boxes[1];

        // The leg's height follows the stretcher's, and the stretcher's is stated at 2".
        Sketch pinned = Assert.IsType<Solved>(Updater.Apply(drawn, Batch.Of(
            new AddRelationship(new EqualParam(
                RelationshipId.New(), new BoxHeightRef(other.Id), new BoxHeightRef(leg_.Id))),
            new AddRelationship(new ParamValue(
                RelationshipId.New(), new BoxHeightRef(other.Id), Length.Inches(2)))))).Sketch;

        Batch batch = StockAssignment.RequestsFor(pinned, leg_, leg, lumber);

        // The premise of this test, said out loud: the width is set before the height, and on its
        // own it would have gone through.
        Assert.Equal(3, batch.Requests.Count);
        Assert.Equal(
            new BoxWidthRef(leg_.Id),
            ((ParamValue)((AddRelationship)batch.Requests[1]).Relationship).Param);
        Assert.IsType<Solved>(Updater.Apply(pinned, batch.Requests[1]));

        Assert.IsType<OverConstrained>(Updater.Apply(pinned, batch));

        // What "nothing is half-assigned" means here: OverConstrained carries no sketch, so the
        // caller still holds this one, in which the width the batch's first request would have
        // set was never set and the part is still what it was.
        Box unchanged = pinned.Find<Box>(leg_.Id)!;
        Assert.Equal(Length.Inches(4), unchanged.Width);
        Assert.Equal(Length.Inches(2), unchanged.Height);
        Assert.Null(unchanged.Part!.Stock);
        Assert.NotEqual(lumber.Width, unchanged.Width);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_depth_typed_before_the_stock_was_chosen_is_changed_rather_than_duplicated()
    {
        // assembly-model §1.2: the thickness of a flat part is its depth, stated through the
        // updater exactly as a width is — so a depth somebody typed is the number the stock changes.
        LumberStock lumber = Lumber("1x6");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        (Sketch drawn, Box box) = OneBox(Length.Inches(48), Length.Inches(8), part, depth: Length.Inches(1));

        RelationshipId typed = RelationshipId.New();
        Sketch sketch = Assert.IsType<Solved>(Updater.Apply(
            drawn,
            new AddRelationship(new ParamValue(typed, new BoxDepthRef(box.Id), Length.Inches(1)))))
            .Sketch;

        Batch batch = StockAssignment.RequestsFor(sketch, box, part, lumber);
        Assert.Contains(batch.Requests, request => request is SetParameter parameter && parameter.Driving == typed);

        Solved result = Assert.IsType<Solved>(Updater.Apply(sketch, batch));
        Assert.Equal(lumber.Thickness, result.Sketch.Find<Box>(box.Id)!.Depth);
        Assert.Equal(lumber.Thickness, ((ParamValue)result.Sketch.Find(typed)!).Value);
        Assert.Contains(box.Id, result.Changes.Resized);
        Assert.True(StockAssignment.FixesDepth(part, lumber));
        Assert.False(StockAssignment.FixesDepth(part with { PlanAxes = Footprint }, lumber));
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_depth_the_stock_cannot_have_is_a_conflict_like_any_other()
    {
        // The special case is gone: the out-of-plane value used to be set on the part directly,
        // "nothing else depending on it". Now a depth held by something else conflicts, and the
        // whole assignment is refused — the part keeps its old depth and its old stock.
        LumberStock lumber = Lumber("1x6");
        Part part = new("1x6", Species: null, Quantity: 1, Flat);
        Piece plain = new(Stock: null, Species: null, Quantity: 1, Length.Inches(1), Flat);

        Sketch drawn = Design.WithParts(
            ("Shelf", Length.Inches(48).Units, Length.Inches(8).Units, plain),
            ("Stop", Length.Inches(48).Units, Length.Inches(8).Units, plain));
        Box[] boxes = [.. drawn.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];

        Sketch pinned = Assert.IsType<Solved>(Updater.Apply(drawn, Batch.Of(
            new AddRelationship(new EqualParam(
                RelationshipId.New(), new BoxDepthRef(boxes[1].Id), new BoxDepthRef(boxes[0].Id))),
            new AddRelationship(new ParamValue(
                RelationshipId.New(), new BoxDepthRef(boxes[1].Id), Length.Inches(1)))))).Sketch;

        OverConstrained refused = Assert.IsType<OverConstrained>(
            Updater.Apply(pinned, StockAssignment.RequestsFor(pinned, boxes[0], part, lumber)));
        Assert.Contains("depth", refused.Conflict.Summary, StringComparison.Ordinal);

        Box unchanged = pinned.Find<Box>(boxes[0].Id)!;
        Assert.Equal(Length.Inches(1), unchanged.Depth);
        Assert.Null(unchanged.Part!.Stock);
    }

    /// <summary>One box with one part on it, drawn at the given plan size and depth.</summary>
    private static (Sketch Sketch, Box Box) OneBox(Length width, Length height, Part part, Length depth)
    {
        Sketch sketch = Design.WithParts(
            ("Part", width.Units, height.Units, new Piece(part.Stock, part.Species, part.Quantity, depth, part.PlanAxes)));
        return (sketch, sketch.Entities.Values.OfType<Box>().Single());
    }

    private static LumberStock Lumber(string name)
    {
        Assert.True(Library.TryFindLumber(name, out LumberStock lumber), $"The library carries {name}.");
        return lumber;
    }

    private static StockItem Find(string name)
    {
        Assert.True(Library.TryFind(name, out StockItem item), $"The library carries {name}.");
        return item;
    }
}
