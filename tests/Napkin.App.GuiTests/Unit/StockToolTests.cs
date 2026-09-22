using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The stock tool's state machine: which drag makes which part, per stock kind, and that the part
/// it makes is already the stock — through the updater, in one batch.
/// </summary>
/// <remarks>
/// Every expected size is read out of the shipped materials library, never written here: a test
/// that typed 3 1/2" for a 2x4 would be a second, uncited copy of PS 20's table.
/// </remarks>
public class StockToolTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;

    static StockItem Item(StockCategory category, string name)
    {
        Assert.True(Library.TryFind(category, name, out StockItem item), $"The library has no {name}.");
        return item;
    }

    static LumberStock TwoByFour => (LumberStock)Item(StockCategory.DimensionalLumber, "2x4");

    static PanelStock Plywood => (PanelStock)Item(StockCategory.SheetGood, "3/4 plywood");

    static HardwoodStock FourQuarter => (HardwoodStock)Item(StockCategory.HardwoodBoard, "4/4");

    static StockTool Armed(StockItem item)
    {
        StockTool tool = new();
        Assert.True(tool.Arm(item));
        return tool;
    }

    [Fact]
    public void A_fastener_cannot_be_picked_up_and_every_other_category_can()
    {
        foreach (StockItem item in Library.Items)
        {
            bool placeable = item.Category != StockCategory.Fastener;
            Assert.Equal(placeable, StockTool.CanPlace(item));
        }

        StockTool tool = Armed(TwoByFour);
        Assert.False(tool.Arm(Item(StockCategory.Fastener, "16d")));

        // Refusing the nail leaves the 2x4 in hand rather than leaving the tool holding nothing.
        Assert.Same(TwoByFour, tool.Stock);
    }

    [Fact]
    public void A_2x4_dragged_along_x_is_a_board_lying_flat_at_the_stock_width()
    {
        LumberStock lumber = TwoByFour;
        StockTool tool = Armed(lumber);
        tool.Begin(Point2.Inches(2, 10));

        // The pointer wanders an inch across the board; the board does not follow it.
        tool.MoveTo(Point2.Inches(38, 11));

        Assert.True(tool.TryShape(out Point2 anchor, out Length width, out Length height, out PlanAxes axes));
        Assert.Equal(Point2.Inches(2, 10), anchor);
        Assert.Equal(Length.Inches(36), width);
        Assert.Equal(lumber.Width, height);
        Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), axes);
    }

    [Fact]
    public void A_2x4_dragged_down_the_page_runs_along_y_and_hangs_off_the_side_it_leaned()
    {
        LumberStock lumber = TwoByFour;
        StockTool tool = Armed(lumber);
        tool.Begin(Point2.Inches(10, 40));
        tool.MoveTo(Point2.Inches(9, 4));

        Assert.True(tool.TryShape(out Point2 anchor, out Length width, out Length height, out PlanAxes axes));
        Assert.Equal(new Point2(Length.Inches(10) - lumber.Width, Length.Inches(4)), anchor);
        Assert.Equal(lumber.Width, width);
        Assert.Equal(Length.Inches(36), height);
        Assert.Equal(new PlanAxes(PartDimension.Width, PartDimension.Length), axes);
    }

    [Fact]
    public void A_2x4_dragged_to_the_left_and_down_lies_below_the_press_point()
    {
        LumberStock lumber = TwoByFour;
        StockTool tool = Armed(lumber);
        tool.Begin(Point2.Inches(40, 10));
        tool.MoveTo(Point2.Inches(4, 9));

        Assert.True(tool.TryShape(out Point2 anchor, out Length width, out _, out _));
        Assert.Equal(new Point2(Length.Inches(4), Length.Inches(10) - lumber.Width), anchor);
        Assert.Equal(Length.Inches(36), width);
    }

    [Fact]
    public void A_2x4_dragged_right_and_up_the_page_hangs_off_to_the_right()
    {
        LumberStock lumber = TwoByFour;
        StockTool tool = Armed(lumber);
        tool.Begin(Point2.Inches(10, 4));
        tool.MoveTo(Point2.Inches(11, 40));

        Assert.True(tool.TryShape(out Point2 anchor, out _, out Length height, out _));
        Assert.Equal(Point2.Inches(10, 4), anchor);
        Assert.Equal(Length.Inches(36), height);
    }

    [Fact]
    public void A_board_needs_a_run_but_not_a_rectangle()
    {
        // A perfectly level drag has no extent across it, and for a board that is fine: the stock
        // supplies the width. The rectangle tool would refuse this, rightly, and this tool must not.
        StockTool tool = Armed(TwoByFour);
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(24, 0));
        Assert.True(tool.TryShape(out _, out _, out _, out _));

        // And no run at all is a click, which makes nothing.
        StockTool clicked = Armed(TwoByFour);
        clicked.Begin(Point2.Inches(3, 3));
        Assert.False(clicked.TryShape(out _, out _, out _, out _));
        Assert.False(clicked.TryComplete(Sketch.Empty, LayerId.Default, EntityId.New(), "Part 1", out Request? request));
        Assert.Null(request);
    }

    [Fact]
    public void A_sheet_is_the_dragged_rectangle_and_its_longer_side_is_the_length()
    {
        StockTool tool = Armed(Plywood);
        tool.Begin(Point2.Inches(30, 20));
        tool.MoveTo(Point2.Inches(6, 2));

        Assert.True(tool.TryShape(out Point2 anchor, out Length width, out Length height, out PlanAxes axes));
        Assert.Equal(Point2.Inches(6, 2), anchor);
        Assert.Equal(Length.Inches(24), width);
        Assert.Equal(Length.Inches(18), height);
        Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), axes);

        tool.MoveTo(Point2.Inches(20, 60));
        Assert.True(tool.TryShape(out _, out _, out _, out PlanAxes tall));
        Assert.Equal(new PlanAxes(PartDimension.Width, PartDimension.Length), tall);

        // A sheet has two free dimensions, so a drag along one only is no part yet.
        tool.MoveTo(Point2.Inches(0, 20));
        Assert.False(tool.TryShape(out _, out _, out _, out _));
    }

    [Fact]
    public void Nothing_is_drawn_without_stock_in_hand_or_after_a_cancel()
    {
        StockTool empty = new();
        empty.Begin(Point2.Inches(0, 0));
        empty.MoveTo(Point2.Inches(10, 10));
        Assert.False(empty.IsDrawing);
        Assert.False(empty.TryShape(out _, out _, out _, out _));

        StockTool cancelled = Armed(Plywood);
        cancelled.Begin(Point2.Inches(0, 0));
        cancelled.MoveTo(Point2.Inches(10, 10));
        cancelled.Cancel();
        Assert.False(cancelled.TryComplete(Sketch.Empty, LayerId.Default, EntityId.New(), "Part 1", out _));
        Assert.Same(Plywood, cancelled.Stock);

        // Putting the stock down ends a drag in progress too.
        StockTool dropped = Armed(Plywood);
        dropped.Begin(Point2.Inches(0, 0));
        Assert.True(dropped.Arm(null));
        Assert.Null(dropped.Stock);
        Assert.False(dropped.IsDrawing);
    }

    [Fact]
    public void Placing_a_2x4_is_one_batch_whose_part_already_is_the_stock()
    {
        LumberStock lumber = TwoByFour;
        StockTool tool = Armed(lumber);
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(30, 0));

        EntityId id = EntityId.New();
        Assert.True(tool.TryComplete(Sketch.Empty, LayerId.Default, id, "Part 1", out Request? request));
        Assert.False(tool.IsDrawing);

        Batch batch = Assert.IsType<Batch>(request);
        Assert.IsType<AddEntity>(batch.Requests[0]);

        // Through the real updater, as the canvas puts it: the part exists, cut from a 2x4, with
        // the yard's width stated as a driving value so a drag on that edge is refused.
        DesignEditor editor = new();
        editor.Open(Design.Unlabelled("Untitled", Sketch.Empty));
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(batch, "Placed a 2x4"));

        Box placed = editor.Sketch.Find<Box>(id)!;
        Assert.Equal("Part 1", placed.Name);
        Assert.Equal(Length.Inches(30), placed.Width);
        Assert.Equal(lumber.Width, placed.Height);

        Part part = placed.Part!;
        Assert.Equal("2x4", part.Stock);
        Assert.Equal(lumber.Thickness, part.OutOfPlane);
        Assert.Equal(new FinishedSize(Length.Inches(30), lumber.Width, lumber.Thickness), part.SizeOn(placed));

        ParamValue driving = Assert.Single(editor.Sketch.RelationshipsInOrder.OfType<ParamValue>());
        Assert.Equal(new BoxHeightRef(id), driving.Param);
        Assert.Equal(lumber.Width, driving.Value);

        // And that is exactly what the properties panel's assignment would state for the same box
        // and part: one function decides it for both paths.
        Batch assignment = StockAssignment.RequestsFor(Sketch.Empty, placed, part, lumber);
        Batch stated = Assert.IsType<Batch>(Assert.Single(batch.Requests, inner => inner is Batch));
        Assert.Equal(assignment.Requests.Count, stated.Requests.Count);
        Assert.Equal(assignment.Requests[0], stated.Requests[0]);
    }

    [Fact]
    public void Placing_a_sheet_or_a_hardwood_board_fixes_only_its_thickness()
    {
        foreach (StockItem item in new StockItem[] { Plywood, FourQuarter })
        {
            StockTool tool = Armed(item);
            tool.Begin(Point2.Inches(0, 0));
            tool.MoveTo(Point2.Inches(20, 12));

            EntityId id = EntityId.New();
            Assert.True(tool.TryComplete(Sketch.Empty, LayerId.Default, id, "Shelf", out Request? request));

            DesignEditor editor = new();
            editor.Open(Design.Unlabelled("Untitled", Sketch.Empty));
            Assert.IsAssignableFrom<Succeeded>(editor.Apply(request, "Placed it"));

            Box placed = editor.Sketch.Find<Box>(id)!;
            Length thickness = Assert.Single(StockAssignment.Fixes(item)).Value;
            Assert.Equal(item.Name, placed.Part!.Stock);
            Assert.Equal(thickness, placed.Part.OutOfPlane);
            Assert.Equal(Length.Inches(20), placed.Width);
            Assert.Equal(Length.Inches(12), placed.Height);

            // Both plan dimensions are the design's own, so nothing drives either of them.
            Assert.Empty(editor.Sketch.RelationshipsInOrder.OfType<ParamValue>());
        }
    }
}
