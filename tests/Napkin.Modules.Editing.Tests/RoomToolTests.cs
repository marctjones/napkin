using Napkin.Core.Geometry;
using Napkin.Modules.Building;

using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Draw → Room (docs/design/renovation-sketches.md §4.2, §8): a click inside four walls takes their
/// inside faces; a drag snaps its edges to the faces within reach; a room starts 8'-0" tall.
/// Worked in whole inches on example 1's walls.
/// </summary>
public class RoomToolTests
{
    static readonly LayerId WallLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static Box WallBox(Length x, Length y, Length length, int quarterTurns = 0, Phase phase = Phase.New)
        => new Box(EntityId.New(), WallLayer, new Point3(x, y, Length.Zero), length, In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero.Rotate90(quarterTurns)) { Phase = phase };

    /// <summary>Example 1's walls: south and north 175 run full, west and east 144 butt between.</summary>
    static Sketch FourWalls(Phase east = Phase.New)
        => Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithEntity(WallBox(In(0), In(0), In(175)))
            .WithEntity(WallBox(In(0), In(147, 1, 2), In(175)))
            .WithEntity(WallBox(In(3, 1, 2), In(3, 1, 2), In(144), 1))
            .WithEntity(WallBox(In(175), In(3, 1, 2), In(144), 1, east));

    [Fact]
    public void A_click_inside_four_walls_takes_their_inside_faces()
    {
        (Point2 anchor, Length length, Length width) = RoomTool.Enclosure(FourWalls(), Point2.Inches(80, 70))!.Value;

        // Inside faces: x 3 1/2..171 1/2 (14'-0"), y 3 1/2..147 1/2 (12'-0").
        Assert.Equal(new Point2(In(3, 1, 2), In(3, 1, 2)), anchor);
        Assert.Equal((In(168), In(144)), (length, width));
    }

    [Fact]
    public void A_click_with_a_side_open_or_a_wall_coming_out_finds_no_enclosure()
    {
        Assert.Null(RoomTool.Enclosure(Sketch.Empty, Point2.Origin));
        Assert.Null(RoomTool.Enclosure(FourWalls(east: Phase.Demolish), Point2.Inches(80, 70)));

        // Outside the walls, to the south: nothing south of the point.
        Assert.Null(RoomTool.Enclosure(FourWalls(), Point2.Inches(80, -20)));
    }

    [Fact]
    public void A_drag_lands_on_the_faces_within_reach_and_stays_where_none_is()
    {
        // Dragged 6..170 by 6..146: every edge within 3 in of an inside face (2 1/2 and 1 1/2 away) snaps to it.
        (Point2 anchor, Length length, Length width) = RoomTool.SnapToWalls(FourWalls(), Point2.Inches(6, 6), In(164), In(140), In(3));
        Assert.Equal((new Point2(In(3, 1, 2), In(3, 1, 2)), In(168), In(144)), (anchor, length, width));

        // With a 1 in reach nothing is close enough: the drag stands.
        Assert.Equal((Point2.Inches(6, 6), In(164), In(140)), RoomTool.SnapToWalls(FourWalls(), Point2.Inches(6, 6), In(164), In(140), In(1)));

        // A snap that would turn the rectangle inside out is refused.
        Assert.Equal((Point2.Inches(2, 2), In(1), In(1)), RoomTool.SnapToWalls(FourWalls(), Point2.Inches(2, 2), In(1), In(1), In(3)));
    }

    [Fact]
    public void The_request_adds_a_room_box_8_feet_tall_and_its_layer()
    {
        LayerId layer = LayerId.New();
        EntityId id = EntityId.New();
        Request plain = RoomTool.Request(layer, null, id, "Room 1", Point2.Origin, In(168), In(144));
        Box box = (Box)Assert.IsType<AddEntity>(plain).Entity;
        Assert.Equal(("Room 1", In(168), In(144), In(96)), (box.Name, box.Width, box.Height, box.Depth));
        Assert.Null(box.Room);
        Assert.Equal(In(120), RoomTool.StartingSide);

        AddLayer add = new(new Layer(layer, BuildingLayers.Room));
        Assert.Equal(2, Assert.IsType<Batch>(RoomTool.Request(layer, add, id, "Room 1", Point2.Origin, In(1), In(1))).Requests.Count);
    }
}
