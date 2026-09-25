using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Corners and joins (docs/design/renovation-sketches.md §4.1, test 3): decided from the boxes'
/// exact corners, no tolerance. Worked in whole inches from the drawings in the comments.
/// </summary>
public class WallJoinTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>A 2x4 wall, 96 in tall, of <paramref name="length"/>, its start at (x, y), turned by quarter turns.</summary>
    static Box WallBox(string name, Length x, Length y, Length length, int quarterTurns = 0)
        => new Box(EntityId.New(), WallLayer, new Point3(x, y, Length.Zero), length, In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero.Rotate90(quarterTurns)) { Name = name };

    static Sketch Of(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening));
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
        }

        return sketch;
    }

    [Fact]
    public void Example_1s_walls_join_at_all_four_corners_where_the_short_walls_butt_between()
    {
        // South and north run full, 175 in (x 0..175); west and east are 144 in between them,
        // turned a quarter, from y 3 1/2 to 147 1/2. A quarter turn about the anchor puts local x
        // up the page and local y to the west, so the west wall's anchor is at x 3 1/2.
        Box south = WallBox("South", In(0), In(0), In(175));
        Box north = WallBox("North", In(0), In(147, 1, 2), In(175));
        Box west = WallBox("West", In(3, 1, 2), In(3, 1, 2), In(144), quarterTurns: 1);
        Box east = WallBox("East", In(175), In(3, 1, 2), In(144), quarterTurns: 1);
        Sketch sketch = Of(south, north, west, east);

        WallJoin[] southJoins = [.. WallJoins.Of(sketch, new Wall(south))];
        Assert.Equal(["East", "West"], southJoins.Select(join => join.Other.Name).Order(StringComparer.Ordinal));

        // The west wall covers x 0..3 1/2 along the south wall; the east wall x 171 1/2..175.
        WallJoin atWest = southJoins.Single(join => join.Other.Name == "West");
        Assert.Equal(WallJoinKind.EndOnFace, atWest.Kind);
        Assert.Equal((In(0), In(3, 1, 2)), (atWest.From, atWest.To));
        Assert.Equal((new Point2(In(0), In(3, 1, 2)), new Point2(In(3, 1, 2), In(3, 1, 2))), atWest.Seam);
        WallJoin atEast = southJoins.Single(join => join.Other.Name == "East");
        Assert.Equal((In(171, 1, 2), In(175)), (atEast.From, atEast.To));

        // From the west wall's side, its south end lies on the south wall's face: at its start, 0..3 1/2 across.
        WallJoin fromWest = WallJoins.Of(sketch, new Wall(west)).Single(join => join.Other.Name == "South");
        Assert.Equal(WallJoinKind.FaceOnEnd, fromWest.Kind);
        Assert.Equal(Length.Zero, fromWest.From);
        Assert.Equal(Length.Zero, fromWest.To);
        Assert.Equal(2, WallJoins.Of(sketch, new Wall(west)).Length);
    }

    [Fact]
    public void Offset_by_one_unit_they_do_not_join()
    {
        Box south = WallBox("South", In(0), In(0), In(175));
        Box west = WallBox("West", In(3, 1, 2), In(3, 1, 2) + new Length(1), In(144), quarterTurns: 1);

        Assert.Empty(WallJoins.Of(Of(south, west), new Wall(south)));
        Assert.Null(WallJoins.Between(new Wall(south), new Wall(west)));
    }

    [Fact]
    public void Two_ends_meeting_in_a_line_join_and_two_walls_overlapping_at_a_corner_join()
    {
        // End to end: 0..96 and 96..192 on one line.
        Box a = WallBox("A", In(0), In(0), In(96));
        Box b = WallBox("B", In(96), In(0), In(96));
        WallJoin line = WallJoins.Between(new Wall(a), new Wall(b))!;
        Assert.Equal(WallJoinKind.EndToEnd, line.Kind);
        Assert.Equal((In(96), In(96)), (line.From, line.To));
        Assert.Equal(WallJoinKind.EndToEnd, WallJoins.Between(new Wall(b), new Wall(a))!.Kind);
        Assert.Equal((Length.Zero, Length.Zero), (WallJoins.Between(new Wall(b), new Wall(a))!.From, WallJoins.Between(new Wall(b), new Wall(a))!.To));

        // An L drawn with both walls running into the corner square: x 0..3 1/2, y 0..3 1/2 shared.
        Box c = WallBox("C", In(3, 1, 2), In(0), In(96), quarterTurns: 1);
        WallJoin corner = WallJoins.Between(new Wall(a), new Wall(c))!;
        Assert.Equal(WallJoinKind.Overlap, corner.Kind);
        Assert.Equal((In(0), In(3, 1, 2)), (corner.From, corner.To));
        Assert.Null(corner.Seam);

        // A wall crossing the middle of another reaches neither's end: not a join.
        Box crossing = WallBox("X", In(50), In(-20), In(40), quarterTurns: 1);
        Assert.Null(WallJoins.Between(new Wall(a), new Wall(crossing)));

        // A partition running into the middle of a wall's thickness reaches its own end but not the
        // wall's: an overlap, not a corner, so not a join (its end is not on the wall's face).
        Box into = WallBox("T", In(43, 1, 2), In(0), In(50), quarterTurns: 1);
        Assert.Null(WallJoins.Between(new Wall(a), new Wall(into)));
        Assert.Null(WallJoins.Between(new Wall(into), new Wall(a)));

        // Two walls face to face, side by side, are not joined either.
        Box beside = WallBox("Beside", In(0), In(3, 1, 2), In(96));
        Assert.Null(WallJoins.Between(new Wall(a), new Wall(beside)));

        // A wall standing on its end is not read.
        Box tipped = a with { FaceUp = BoxFace.North };
        Assert.Null(WallJoins.Between(new Wall(tipped), new Wall(b)));
    }

    [Fact]
    public void An_opening_reaching_a_join_is_refused_naming_the_other_wall()
    {
        // A partition butts the 175 in south wall's north face at x 100..103 1/2 (a T). A 36 in
        // window at 90 has its extent 90..126 across it; its kings and jacks, 87..129, fit the wall.
        Box south = WallBox("Wall 1", In(0), In(0), In(175));
        Box partition = WallBox("Wall 2", In(103, 1, 2), In(3, 1, 2), In(96), quarterTurns: 1);
        Box window = new Box(EntityId.New(), OpeningLayer, new Point3(In(90), Length.Zero, In(36)), In(36), In(3, 1, 2), In(48), BoxFace.Top, Angle.Zero) { Name = "Window 1" };
        Sketch sketch = Of(south, partition, window);

        WallFraming framing = FramingList.Frame(sketch, new Wall(south), MaterialsLibrary.Shipped);

        Assert.Contains("Window 1: it reaches the corner with Wall 2", framing.Problems);

        // Moved to 40, its extent 40..76 clears the partition: framed.
        Sketch moved = sketch.WithEntity(window with { Anchor = new Point3(In(40), Length.Zero, In(36)) });
        Assert.Empty(FramingList.Frame(moved, new Wall(south), MaterialsLibrary.Shipped).Problems);
    }

    [Fact]
    public void The_plan_strokes_an_edge_less_its_seams()
    {
        Point2 from = Point2.Inches(0, 0), to = Point2.Inches(10, 0);

        Assert.Equal([(from, to)], WallJoins.Strokes(from, to, []));
        Assert.Equal(
            [(Point2.Inches(0, 0), Point2.Inches(2, 0)), (Point2.Inches(5, 0), Point2.Inches(10, 0))],
            WallJoins.Strokes(from, to, [(Point2.Inches(2, 0), Point2.Inches(5, 0))]));
        Assert.Empty(WallJoins.Strokes(from, to, [(Point2.Inches(-1, 0), Point2.Inches(11, 0))]));
        Assert.Equal([(from, Point2.Inches(9, 0))], WallJoins.Strokes(from, to, [(Point2.Inches(9, 0), Point2.Inches(12, 0))]));
        Assert.Equal([(Point2.Inches(2, 0), to)], WallJoins.Strokes(from, to, [(from, Point2.Inches(2, 0))]));
        Assert.Equal([(from, Point2.Inches(8, 0))], WallJoins.Strokes(from, to, [(Point2.Inches(8, 0), to)]));

        // A seam on another line, or across the edge, leaves it whole.
        Assert.Equal([(from, to)], WallJoins.Strokes(from, to, [(Point2.Inches(2, 1), Point2.Inches(5, 1)), (Point2.Inches(3, -1), Point2.Inches(3, 1))]));

        // A vertical edge, and a slanted one, which no seam can lie on.
        Point2 up = Point2.Inches(0, 10);
        Assert.Equal([(Point2.Inches(0, 4), up)], WallJoins.Strokes(from, up, [(Point2.Inches(0, -2), Point2.Inches(0, 4))]));
        Assert.Equal([(from, Point2.Inches(3, 4))], WallJoins.Strokes(from, Point2.Inches(3, 4), [(from, Point2.Inches(3, 4))]));
    }
}
