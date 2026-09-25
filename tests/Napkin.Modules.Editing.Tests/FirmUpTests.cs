using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Firm up's plan and its acceptance (<c>docs/design/sketch-mode.md</c> &#xA7;3, &#xA7;6.2), on the quick
/// bench of &#xA7;7.2 drawn rough. Stock sizes are read from the library, never typed.
/// </summary>
public class FirmUpTests
{
    static readonly LengthFormat Format = new FeetInchesFormat(16);

    // The quick bench as four rough planks, 3/4" deep, drawn through the rectangle tool in Rough mode.
    static (DesignEditor Editor, EntityId Top, EntityId Leg1, EntityId Leg2, EntityId Stretcher) Bench()
    {
        DesignEditor editor = new();
        editor.EntryMode = EntryMode.Rough;
        EntityId Draw(long x, long y, long w, long h)
        {
            RectangleTool tool = new();
            tool.Begin(Point2.Inches(x, y));
            tool.MoveTo(Point2.Inches(x + w, y + h));
            EntityId id = EntityId.New();
            Assert.True(tool.TryComplete(LayerId.Default, id, editor.NextPartName(), out Request? request, editor.EntryMode));
            Assert.IsAssignableFrom<Succeeded>(editor.Apply(request, "Drew a plank"));
            return id;
        }

        EntityId top = Draw(0, 16, 48, 2);
        EntityId leg1 = Draw(2, 0, 4, 16);
        EntityId leg2 = Draw(42, 0, 4, 16);
        EntityId stretcher = Draw(6, 4, 36, 3);
        return (editor, top, leg1, leg2, stretcher);
    }

    static StockItem Item(string name)
    {
        Assert.True(MaterialsLibrary.Shipped.TryFind(name, out StockItem item));
        return item;
    }

    [Fact]
    public void With_nothing_selected_the_scope_is_every_part_and_with_a_selection_only_it()
    {
        (DesignEditor editor, EntityId top, EntityId leg1, EntityId leg2, EntityId stretcher) = Bench();
        Box wall = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(100, 0), Length.Inches(10), Length.Inches(4), Length.Inches(8), Angle.Zero);
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddEntity(wall), "wall"));

        Assert.Equal(new[] { top, leg1, leg2, stretcher }.Order(), FirmUp.Scope(editor.Sketch, new HashSet<EntityId>()));
        Assert.Equal([leg1], FirmUp.Scope(editor.Sketch, new HashSet<EntityId> { leg1, wall.Id }));
    }

    [Fact]
    public void The_bench_plan_has_four_relationships_four_stock_lines_and_four_size_lines()
    {
        (DesignEditor editor, EntityId top, EntityId leg1, EntityId leg2, EntityId stretcher) = Bench();

        FirmUpPlan plan = FirmUp.Plan(editor.Sketch, [top, leg1, leg2, stretcher], MaterialsLibrary.Shipped, editor.NameOf, Format);

        Assert.Equal(4, plan.Relationships.Length);
        // In id order, whatever order they were drawn in.
        Assert.Equal(new[] { top, leg1, leg2, stretcher }.Order(), plan.Stocks.Select(line => line.Part));
        Assert.Equal(new[] { top, leg1, leg2, stretcher }.Order(), plan.Sizes.Select(line => line.Part));
        Assert.False(plan.IsEmpty);

        // The leg is 16 long, 4 wide, 3/4 thick; the 1x4's width and thickness are each within an inch.
        FirmUpStockLine leg = plan.Stocks.Single(line => line.Part == leg1);
        Assert.Equal("Part 2, 1'-4\" × 4\" × 3/4\"", leg.Sentence);
        Assert.Contains(Item("1x4"), leg.Candidates);
        Assert.InRange(leg.Candidates.Length, 1, FirmUp.CandidateCount);
        Assert.Equal("Part 1, keep 4'-0\" × 2\" as drawn", plan.Sizes.Single(line => line.Part == top).Sentence);
    }

    [Fact]
    public void Accepting_everything_firms_every_part_in_one_undo_step()
    {
        (DesignEditor editor, EntityId top, EntityId leg1, EntityId leg2, EntityId stretcher) = Bench();
        EntityId[] all = [top, leg1, leg2, stretcher];
        FirmUpPlan plan = FirmUp.Plan(editor.Sketch, all, MaterialsLibrary.Shipped, editor.NameOf, Format);
        int undoBefore = editor.History.UndoCount;
        Sketch before = editor.Sketch;

        FirmUpOutcome outcome = FirmUp.Accept(
            editor,
            plan.Relationships,
            plan.Stocks.Where(line => !line.Candidates.IsEmpty).Select(line => (line.Part, line.Candidates[0])),
            plan.Sizes.Select(line => line.Part));

        Assert.Empty(outcome.Rejections);
        Assert.Equal(4, outcome.Relationships);
        Assert.Equal(4, outcome.Stocks);
        Assert.Equal(4, outcome.Sizes);
        Assert.Equal(4, outcome.PartsFirmed);
        Assert.Equal("Firmed up 4 parts: 4 relationships, 4 stocks, 4 sizes. Next: Join all touching.", outcome.Summary);
        Assert.Equal(outcome.Summary, editor.LastMessage!.Text);
        Assert.All(all, id => Assert.False(editor.Sketch.Find<Box>(id)!.Part!.Rough));
        Assert.Equal(undoBefore + 1, editor.History.UndoCount);
        Assert.Equal(FirmUp.What, editor.History.UndoWhat);

        // Each part's two plan sizes are stated now: by the stock where it fixes one, by the drawn value otherwise.
        foreach (EntityId id in all)
        {
            Assert.NotNull(DimensionEntry.DrivingRelationship(editor.Sketch, new BoxWidthRef(id)));
            Assert.NotNull(DimensionEntry.DrivingRelationship(editor.Sketch, new BoxHeightRef(id)));
        }

        Assert.True(RelationshipChecker.Check(editor.Sketch).AllHold);
        Assert.Empty(FirmUp.Plan(editor.Sketch, all, MaterialsLibrary.Shipped, editor.NameOf, Format).Stocks);

        Assert.True(editor.Undo());
        Assert.Equal(before, editor.Sketch);
    }

    [Fact]
    public void Unticked_lines_are_left_and_the_part_stays_rough_without_its_stock_or_sizes()
    {
        (DesignEditor editor, EntityId top, EntityId leg1, EntityId leg2, EntityId stretcher) = Bench();
        FirmUpPlan plan = FirmUp.Plan(editor.Sketch, [top, leg1, leg2, stretcher], MaterialsLibrary.Shipped, editor.NameOf, Format);

        FirmUpOutcome outcome = FirmUp.Accept(editor, plan.Relationships.Take(3), [], [top]);

        Assert.Equal(3, outcome.Relationships);
        Assert.Equal(0, outcome.Stocks);
        Assert.Equal(1, outcome.Sizes);
        Assert.Equal(1, outcome.PartsFirmed);
        Assert.Equal("Firmed up 1 part: 3 relationships, 0 stocks, 1 size. Next: Join all touching.", outcome.Summary);
        Assert.False(editor.Sketch.Find<Box>(top)!.Part!.Rough);
        Assert.True(editor.Sketch.Find<Box>(leg1)!.Part!.Rough);
        Assert.Equal(3, editor.Sketch.RelationshipsInOrder.OfType<Flush>().Count());

        // The top's drawn sizes are stated exactly as drawn.
        ParamValue width = editor.Sketch.RelationshipsInOrder.OfType<ParamValue>().Single(value => value.Param == new BoxWidthRef(top));
        Assert.Equal(Length.Inches(48), width.Value);
    }

    [Fact]
    public void A_stock_that_conflicts_is_reported_in_the_updaters_words_and_the_rest_still_land()
    {
        // Leg 1 and the stretcher both pinned, and held face to face: a 1x4 makes the leg 3 1/2" wide,
        // which would pull its east face off the pinned stretcher's west face — refused. The top's size still lands.
        (DesignEditor editor, EntityId top, EntityId leg1, _, EntityId stretcher) = Bench();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(new Anchored(RelationshipId.New(), leg1)), "pin"));
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddRelationship(new Anchored(RelationshipId.New(), stretcher)), "pin"));
        FirmUpPlan plan = FirmUp.Plan(editor.Sketch, [leg1, stretcher], MaterialsLibrary.Shipped, editor.NameOf, Format);
        FirmUpProposal against = Assert.Single(plan.Relationships);

        FirmUpOutcome outcome = FirmUp.Accept(editor, [against], [(leg1, Item("1x4"))], [top]);

        Assert.Equal(1, outcome.Relationships);
        Assert.Equal(0, outcome.Stocks);
        Assert.Equal(1, outcome.Sizes);
        string rejection = Assert.Single(outcome.Rejections);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(EditSeverity.Problem, editor.LastMessage!.Severity);
        Assert.True(editor.Sketch.Find<Box>(leg1)!.Part!.Rough);
        Assert.Null(editor.Sketch.Find<Box>(leg1)!.Part!.Stock);
    }

    [Fact]
    public void Accepting_a_stock_alone_clears_the_mark_and_states_only_what_the_yard_fixes()
    {
        (DesignEditor editor, _, EntityId leg1, _, _) = Bench();
        LumberStock oneByFour = (LumberStock)Item("1x4");

        FirmUpOutcome outcome = FirmUp.Accept(editor, [], [(leg1, oneByFour)], []);

        Box leg = editor.Sketch.Find<Box>(leg1)!;
        Assert.Equal(1, outcome.Stocks);
        Assert.Equal(1, outcome.PartsFirmed);
        Assert.False(leg.Part!.Rough);
        Assert.Equal("1x4", leg.Part.Stock);
        Assert.Equal(oneByFour.Width, leg.Width);

        // The free length keeps its rough value and nothing states it: that is the size line's job.
        Assert.Equal(Length.Inches(16), leg.Height);
        Assert.Null(DimensionEntry.DrivingRelationship(editor.Sketch, new BoxHeightRef(leg1)));
    }

    [Fact]
    public void A_rough_part_that_already_has_stock_gets_no_stock_line_but_a_size_line()
    {
        DesignEditor editor = new();
        editor.EntryMode = EntryMode.Rough;
        StockTool tool = new();
        Assert.True(tool.Arm(Item("2x4")));
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(36, 0));
        EntityId id = EntityId.New();
        Assert.True(tool.TryComplete(editor.Sketch, LayerId.Default, id, editor.NextPartName(), out Request? request, editor.EntryMode));
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(request, "Placed a 2x4"));

        FirmUpPlan plan = FirmUp.Plan(editor.Sketch, [id], MaterialsLibrary.Shipped, editor.NameOf, Format);

        Assert.Empty(plan.Stocks);
        FirmUpSizeLine line = Assert.Single(plan.Sizes);
        Assert.Equal(id, line.Part);
    }

    [Fact]
    public void A_part_that_is_gone_or_not_a_part_is_passed_over()
    {
        (DesignEditor editor, _, _, _, _) = Bench();

        FirmUpOutcome outcome = FirmUp.Accept(editor, [], [(EntityId.New(), Item("1x4"))], [EntityId.New()]);

        Assert.Equal(new FirmUpOutcome(0, 0, 0, 0, []).Summary, outcome.Summary);
    }

    [Fact]
    public void F_opens_firm_up_in_the_editing_keys()
        => Assert.Equal(EditCommand.FirmUp, KeyMaps.Edit.Find(new Keystroke(KeyName.F, KeyMods.None)));
}
