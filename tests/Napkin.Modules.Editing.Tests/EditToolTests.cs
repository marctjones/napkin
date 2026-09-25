using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The rectangle tool's state machine, the grid it lands on, and what a pointer has hold of.
/// </summary>
/// <remarks>
/// None of this needs a window: the tool holds two model points, the grid is a ladder of plain
/// numbers and a rounding rule, and hit-testing is arithmetic on a box. Keeping them out of the
/// control is what lets these be asserted a hundred times in a millisecond while the workflows
/// prove the wiring once.
/// </remarks>
public class EditToolTests
{
    [Fact]
    [Trait("Feature", "CVS-005")]
    public void A_drag_makes_an_AddEntity_for_a_box_of_the_dragged_size()
    {
        RectangleTool tool = new();
        tool.Begin(Point2.Inches(4, 5));
        tool.MoveTo(Point2.Inches(10, 6));
        tool.MoveTo(Point2.Inches(16, 17));

        EntityId id = EntityId.New();
        Assert.True(tool.TryComplete(LayerId.Default, id, "Part 1", out Request? request));

        AddEntity add = Assert.IsType<AddEntity>(request);
        Box box = Assert.IsType<Box>(add.Entity);
        Assert.Equal(id, box.Id);
        Assert.Equal(Length.Inches(4).Units, box.Anchor.X.Units);
        Assert.Equal(Length.Inches(5).Units, box.Anchor.Y.Units);
        Assert.Equal(Length.Inches(12).Units, box.Width.Units);
        Assert.Equal(Length.Inches(12).Units, box.Height.Units);
        Assert.False(tool.IsDrawing);
    }

    [Fact]
    public void A_rectangle_dragged_up_and_to_the_left_is_the_same_rectangle()
    {
        RectangleTool tool = new();
        tool.Begin(Point2.Inches(16, 17));
        tool.MoveTo(Point2.Inches(4, 5));

        Assert.True(tool.TryRectangle(out Point2 anchor, out Length width, out Length height));
        Assert.Equal(Length.Inches(4).Units, anchor.X.Units);
        Assert.Equal(Length.Inches(5).Units, anchor.Y.Units);
        Assert.Equal(Length.Inches(12).Units, width.Units);
        Assert.Equal(Length.Inches(12).Units, height.Units);
    }

    [Fact]
    public void A_click_with_no_drag_makes_nothing()
    {
        RectangleTool tool = new();
        tool.Begin(Point2.Inches(4, 5));

        Assert.False(tool.TryRectangle(out _, out _, out _));
        Assert.False(tool.TryComplete(LayerId.Default, EntityId.New(), "Part 1", out Request? request));
        Assert.Null(request);
    }

    [Fact]
    public void A_cancelled_rectangle_makes_nothing_even_after_a_real_drag()
    {
        RectangleTool tool = new();
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(10, 10));
        tool.Cancel();

        Assert.False(tool.IsDrawing);
        Assert.False(tool.TryComplete(LayerId.Default, EntityId.New(), "Part 1", out _));
    }

    [Fact]
    public void Moving_before_a_press_does_nothing()
    {
        RectangleTool tool = new();
        tool.MoveTo(Point2.Inches(10, 10));

        Assert.False(tool.IsDrawing);
        Assert.Equal(Point2.Origin, tool.To);
    }

    [Theory]
    // The finest step whose lines are at least 14 pixels apart, at each end of the zoom range.
    [InlineData(2400, 0.25)]
    [InlineData(96, 0.25)]
    [InlineData(48, 0.5)]
    [InlineData(16, 1)]
    [InlineData(12, 3)]
    [InlineData(4, 6)]
    [InlineData(1, 24)]
    public void The_grid_step_is_the_finest_one_still_far_enough_apart(double pixelsPerInch, double expected)
    {
        double step = SnapGrid.StepInches(pixelsPerInch);

        Assert.Equal(expected, step);
        Assert.True(
            step * pixelsPerInch >= SnapGrid.MinimumSpacingPixels,
            $"a {step}\" grid is only {step * pixelsPerInch} pixels apart at {pixelsPerInch} px/inch.");
    }

    [Fact]
    public void The_grid_drawn_and_the_grid_snapped_to_are_the_same_ladder()
    {
        // Every step in force at some zoom is on the ladder, which is what makes "it snapped to
        // the grid" mean "it snapped to the grid you can see".
        foreach (double pixelsPerInch in (double[])[0.02, 1, 4, 12, 16, 48, 96, 2400])
        {
            Assert.Contains(SnapGrid.StepInches(pixelsPerInch), SnapGrid.Ladder);
        }
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1100, 1, 1024)]
    [InlineData(1600, 1, 2048)]
    [InlineData(1536, 1, 2048)]
    [InlineData(-1100, 1, -1024)]
    [InlineData(-1536, 1, -2048)]
    [InlineData(300, 0.25, 256)]
    public void Snapping_a_length_lands_on_the_nearest_grid_multiple(long units, double step, long expected)
    {
        Assert.Equal(expected, SnapGrid.Snap(new Length(units), step).Units);
    }

    [Fact]
    public void A_snapped_length_is_always_a_whole_number_of_steps()
    {
        long perStep = SnapGrid.UnitsPerStep(0.5);
        for (long units = -5000; units <= 5000; units += 37)
        {
            Assert.Equal(0, SnapGrid.Snap(new Length(units), 0.5).Units % perStep);
        }
    }

    [Fact]
    public void Corner_handles_win_over_edge_handles_and_edges_over_the_body()
    {
        Box box = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(0, 0), Length.Inches(20), Length.Inches(10), Box.DefaultDepth, Angle.Zero);
        Length tolerance = Length.Inches(1);

        Assert.Equal(BoxGrip.SouthWest, BoxGeometry.GripAt(box, Point2.Inches(0, 0), tolerance));
        Assert.Equal(BoxGrip.NorthEast, BoxGeometry.GripAt(box, Point2.Inches(20, 10), tolerance));
        Assert.Equal(BoxGrip.East, BoxGeometry.GripAt(box, Point2.Inches(20, 5), tolerance));
        Assert.Equal(BoxGrip.Body, BoxGeometry.GripAt(box, Point2.Inches(10, 3), tolerance));
        Assert.Null(BoxGeometry.GripAt(box, Point2.Inches(30, 30), tolerance));
    }

    [Fact]
    public void A_corner_handle_drags_the_two_edges_that_meet_at_it()
    {
        Assert.Equal([BoxEdge.North, BoxEdge.East], BoxGeometry.EdgesOf(BoxGrip.NorthEast));
        Assert.Equal([BoxEdge.South, BoxEdge.West], BoxGeometry.EdgesOf(BoxGrip.SouthWest));
        Assert.Equal([BoxEdge.East], BoxGeometry.EdgesOf(BoxGrip.East));
        Assert.Empty(BoxGeometry.EdgesOf(BoxGrip.Body));
    }

    [Fact]
    public void An_edge_grows_when_the_pointer_moves_away_from_the_box()
    {
        Box box = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(0, 0), Length.Inches(20), Length.Inches(10), Box.DefaultDepth, Angle.Zero);
        Vector2 rightAndUp = new(Length.Inches(3), Length.Inches(2));

        Assert.Equal(Length.Inches(3).Units, BoxGeometry.OutwardDelta(box, BoxEdge.East, rightAndUp).Units);
        Assert.Equal(Length.Inches(-3).Units, BoxGeometry.OutwardDelta(box, BoxEdge.West, rightAndUp).Units);
        Assert.Equal(Length.Inches(2).Units, BoxGeometry.OutwardDelta(box, BoxEdge.North, rightAndUp).Units);
        Assert.Equal(Length.Inches(-2).Units, BoxGeometry.OutwardDelta(box, BoxEdge.South, rightAndUp).Units);
    }

    [Fact]
    public void A_quarter_turned_box_takes_its_handles_with_it()
    {
        // The edges are named in the box's own frame, so dragging the pointer right grows the
        // edge that is pointing right — which on a box turned a quarter turn is its north edge.
        Box turned = Box.AsDrawn(
            EntityId.New(),
            LayerId.Default,
            Point2.Inches(0, 0),
            Length.Inches(20),
            Length.Inches(10),
            Box.DefaultDepth,
            Angle.Right);

        Vector2 right = new(Length.Inches(3), Length.Zero);
        Assert.Equal(Length.Inches(-3).Units, BoxGeometry.OutwardDelta(turned, BoxEdge.North, right).Units);
        Assert.Equal(Length.Inches(3).Units, BoxGeometry.OutwardDelta(turned, BoxEdge.South, right).Units);
        Assert.Equal(Length.Zero.Units, BoxGeometry.OutwardDelta(turned, BoxEdge.East, right).Units);
    }

    [Fact]
    public void A_box_knows_its_four_axis_aligned_edges_and_where_they_sit()
    {
        Box box = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(2, 3), Length.Inches(20), Length.Inches(10), Box.DefaultDepth, Angle.Zero);
        List<EdgeLine> edges = [.. BoxGeometry.AxisAlignedEdges(box)];

        Assert.Equal(4, edges.Count);

        EdgeLine east = edges.Single(edge => edge.Edge == BoxEdge.East);
        Assert.Equal(Axis.X, east.NormalAxis);
        Assert.Equal(Length.Inches(22).Units, east.Coordinate.Units);
        Assert.Equal(Length.Inches(3).Units, east.Low.Units);
        Assert.Equal(Length.Inches(13).Units, east.High.Units);

        EdgeLine south = edges.Single(edge => edge.Edge == BoxEdge.South);
        Assert.Equal(Axis.Y, south.NormalAxis);
        Assert.Equal(Length.Inches(3).Units, south.Coordinate.Units);
    }
}
