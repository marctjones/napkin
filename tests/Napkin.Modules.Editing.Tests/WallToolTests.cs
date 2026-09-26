using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The wall tool's drag: which way the wall runs, where its anchor lands so that its thickness sits
/// on the side of the stroke the drag went toward, and what it asks the updater for.
/// </summary>
/// <remarks>
/// The thickness is the chosen member's actual width, read from the shipped library (a 2x4's
/// width is never typed here). Every other number is a drag coordinate, and the expected anchor
/// is worked from the rule in <see cref="WallTool.TryShape"/>'s comment: along X the wall's length
/// runs east of its anchor and its thickness north; turned a quarter, its length runs north and
/// its thickness west.
/// </remarks>
public class WallToolTests
{
    static readonly LayerId Walls = new(new Guid("00000000-0000-4000-8000-00000000aaaa"));

    static readonly EntityId WallId = EntityId.New();

    static LumberStock TwoByFour
    {
        get
        {
            Assert.True(MaterialsLibrary.Shipped.TryFind(StockCategory.DimensionalLumber, "2x4", out StockItem item));
            return (LumberStock)item;
        }
    }

    static WallTool Drawing(Point2 from, Point2 to)
    {
        WallTool tool = new() { Member = TwoByFour };
        tool.Begin(from);
        tool.MoveTo(to);
        return tool;
    }

    static Box Shape(WallTool tool)
    {
        Assert.True(tool.TryShape(Walls, WallId, "Wall 1", out Box? wall));
        return wall;
    }

    [Fact]
    public void Without_a_member_the_tool_does_not_start_and_shapes_nothing()
    {
        WallTool tool = new();
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(100, 0));

        Assert.False(tool.IsDrawing);
        Assert.False(tool.TryShape(Walls, WallId, "Wall 1", out Box? wall));
        Assert.Null(wall);
        Assert.False(tool.TryComplete(Walls, null, WallId, "Wall 1", out Request? request));
        Assert.Null(request);
    }

    [Fact]
    public void Moving_before_the_drag_begins_is_ignored()
    {
        WallTool tool = new() { Member = TwoByFour };
        tool.MoveTo(Point2.Inches(100, 0));
        Assert.False(tool.IsDrawing);

        // Beginning afresh starts from the press, not from the stray move: no run, no wall.
        tool.Begin(Point2.Inches(10, 10));
        Assert.True(tool.IsDrawing);
        Assert.False(tool.TryShape(Walls, WallId, "Wall 1", out _));
    }

    [Fact]
    public void Dragged_east_the_wall_runs_along_x_from_the_press_with_its_thickness_to_the_north()
    {
        Box wall = Shape(Drawing(Point2.Inches(10, 20), Point2.Inches(130, 25)));

        // |dx| = 120 beats |dy| = 5, so the wall runs along X for 120"; dy >= 0 keeps the
        // anchor on the stroke's own line.
        Assert.Equal(new Point3(Length.Inches(10), Length.Inches(20), Length.Zero), wall.Anchor);
        Assert.Equal(Length.Inches(120), wall.Width);
        Assert.Equal(TwoByFour.Width, wall.Height);
        Assert.Equal(WallTool.StartingHeight, wall.Depth);
        Assert.Equal(Angle.Zero, wall.Rotation);
        Assert.Equal(Walls, wall.Layer);
        Assert.Equal(WallId, wall.Id);
        Assert.Equal("Wall 1", wall.Name);
        Assert.Equal(BoxFace.Top, wall.FaceUp);
    }

    [Fact]
    public void Dragged_west_and_a_little_south_the_anchor_moves_to_the_far_end_and_one_thickness_south()
    {
        Box wall = Shape(Drawing(Point2.Inches(130, 20), Point2.Inches(10, 15)));

        // The run starts at the smaller X; with dy < 0 the thickness sits south of the stroke.
        Assert.Equal(Length.Inches(10), wall.Anchor.X);
        Assert.Equal(Length.Inches(20) - TwoByFour.Width, wall.Anchor.Y);
        Assert.Equal(Length.Inches(120), wall.Width);
        Assert.Equal(Angle.Zero, wall.Rotation);
    }

    [Fact]
    public void Dragged_north_the_wall_is_turned_a_quarter_and_its_anchor_is_one_thickness_east_of_the_stroke()
    {
        Box wall = Shape(Drawing(Point2.Inches(10, 10), Point2.Inches(12, 100)));

        // |dy| = 90 beats |dx| = 2: the run is 90" north. Turned a quarter the thickness lies
        // west of the anchor, so for dx >= 0 the anchor is one thickness east of the press.
        Assert.Equal(Length.Inches(10) + TwoByFour.Width, wall.Anchor.X);
        Assert.Equal(Length.Inches(10), wall.Anchor.Y);
        Assert.Equal(Length.Inches(90), wall.Width);
        Assert.Equal(TwoByFour.Width, wall.Height);
        Assert.Equal(Angle.Right, wall.Rotation);
    }

    [Fact]
    public void Dragged_south_and_a_little_west_the_anchor_stays_on_the_stroke_at_the_lower_end()
    {
        Box wall = Shape(Drawing(Point2.Inches(10, 100), Point2.Inches(8, 10)));

        Assert.Equal(Length.Inches(10), wall.Anchor.X);
        Assert.Equal(Length.Inches(10), wall.Anchor.Y);
        Assert.Equal(Length.Inches(90), wall.Width);
        Assert.Equal(Angle.Right, wall.Rotation);
    }

    [Fact]
    public void A_perfect_diagonal_is_read_as_running_along_x()
    {
        Box wall = Shape(Drawing(Point2.Inches(0, 0), Point2.Inches(48, 48)));

        Assert.Equal(Angle.Zero, wall.Rotation);
        Assert.Equal(Length.Inches(48), wall.Width);
    }

    [Fact]
    public void The_height_in_force_is_the_wall_s_depth()
    {
        WallTool tool = Drawing(Point2.Inches(0, 0), Point2.Inches(60, 0));
        tool.Height = Length.Inches(48);

        Assert.Equal(Length.Inches(48), Shape(tool).Depth);
    }

    [Fact]
    public void Cancelling_ends_the_drag_so_nothing_is_shaped()
    {
        WallTool tool = Drawing(Point2.Inches(0, 0), Point2.Inches(60, 0));
        tool.Cancel();

        Assert.False(tool.IsDrawing);
        Assert.False(tool.TryShape(Walls, WallId, "Wall 1", out _));
    }

    [Fact]
    public void Completing_asks_for_the_wall_alone_when_its_layer_already_exists()
    {
        WallTool tool = Drawing(Point2.Inches(0, 0), Point2.Inches(60, 0));
        Box expected = Shape(tool);

        Assert.True(tool.TryComplete(Walls, null, WallId, "Wall 1", out Request? request));

        Assert.Equal(new AddEntity(expected), request);
        Assert.False(tool.IsDrawing);
    }

    [Fact]
    public void Completing_asks_for_the_layer_first_and_the_wall_second_when_the_layer_is_new()
    {
        WallTool tool = Drawing(Point2.Inches(0, 0), Point2.Inches(60, 0));
        Box expected = Shape(tool);
        Request addLayer = new AddLayer(new Layer(Walls, "Walls"));

        Assert.True(tool.TryComplete(Walls, addLayer, WallId, "Wall 1", out Request? request));

        Batch batch = Assert.IsType<Batch>(request);
        Assert.Equal(2, batch.Requests.Count);
        Assert.Same(addLayer, batch.Requests[0]);
        Assert.Equal(new AddEntity(expected), batch.Requests[1]);
    }

    [Fact]
    public void A_wall_drawn_on_a_deck_stands_on_its_decking_and_one_beside_it_on_the_ground()
    {
        // A deck 144 × 120 south of the origin, 36″ up; a wall along its south edge, and one past its east edge.
        LayerId decks = LayerId.New();
        Box deck = new(EntityId.New(), decks, new Point3(Length.Zero, Length.Inches(-120), Length.Zero), Length.Inches(144), Length.Inches(120), Length.Inches(36), BoxFace.Top, Angle.Zero)
        {
            Deck = DeckTool.StartingInputs,
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(decks, Napkin.Modules.Building.BuildingLayers.Deck)).WithEntity(deck);

        WallTool front = Drawing(Point2.Inches(0, -120), Point2.Inches(144, -120));
        Box drawn = Shape(front);
        Assert.True(front.TryComplete(Walls, null, WallId, "Wall 1", out Request? request, sketch));
        Assert.Equal(new AddEntity(drawn with { Anchor = drawn.Anchor with { Z = Length.Inches(36) } }), request);

        WallTool beside = Drawing(Point2.Inches(150, -120), Point2.Inches(150, 0));
        Box outside = Shape(beside);
        Assert.True(beside.TryComplete(Walls, null, WallId, "Wall 1", out Request? ground, sketch));
        Assert.Equal(new AddEntity(outside), ground);

        // A demolished deck holds nothing up, and a tipped wall is left as drawn.
        Assert.Equal(Length.Zero, WallTool.OnDecking(sketch.WithEntity(deck with { Phase = Phase.Demolish }), drawn).Anchor.Z);
        Box tipped = drawn with { FaceUp = BoxFace.South };
        Assert.Equal(tipped, WallTool.OnDecking(sketch, tipped));
    }

    [Fact]
    public void A_click_without_a_drag_completes_nothing_and_still_ends_the_drag()
    {
        WallTool tool = new() { Member = TwoByFour };
        tool.Begin(Point2.Inches(5, 5));

        Assert.False(tool.TryComplete(Walls, null, WallId, "Wall 1", out Request? request));
        Assert.Null(request);
        Assert.False(tool.IsDrawing);
    }
}
