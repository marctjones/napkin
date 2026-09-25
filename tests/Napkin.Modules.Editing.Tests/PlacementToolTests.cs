using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Placing a part in the 3D view on the face under the pointer (#74): where it lands, which way
/// it lies, and what holds it there.
/// </summary>
public class PlacementToolTests
{
    // The coffee table's top: 48" x 24" x 3/4", its underside at 16 1/4".
    static readonly Box Top = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0) with { Z = Length.Inches(16, 1, 4) }, Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
    static readonly Sketch Table = Sketch.Empty.WithEntity(Top);
    static readonly EntityId NewPart = EntityId.New();

    static StockItem TwoByFour()
    {
        Assert.True(MaterialsLibrary.Shipped.TryFind("2x4", out StockItem item));
        return item;
    }

    static PlacementPreview Place(PlacementTool tool, PlacementFace face, Point3 from, Point3 to) =>
        tool.Shape(Table, face, from, to, LayerId.Default, NewPart, "Part", 1, Length.Inches(0, 1, 4))
        ?? throw new InvalidOperationException("Nothing would be placed.");

    [Fact]
    public void A_point_on_a_north_facing_face_is_across_it_east_west_and_up()
    {
        PlacementFace north = PlacementFace.Of(Top, BoxFace.North);

        Assert.Equal((Axis.Y, true), (north.Normal, north.Positive));
        Assert.Equal(Length.Inches(24), north.Coordinate);
        Assert.Equal((Axis.X, Axis.Z), north.Plane);
        Assert.Equal(new Point2(Length.Inches(5), Length.Inches(16)), north.InPlane(Point3.Inches(5, 24, 16)));
    }

    [Theory]
    [InlineData(Axis.X, Axis.Y)]
    [InlineData(Axis.X, Axis.Z)]
    [InlineData(Axis.Y, Axis.Z)]
    public void Lying_on_a_plane_sends_local_x_and_y_along_it(Axis u, Axis v)
    {
        Orientation lying = PlacementTool.LyingOn(u, v);

        Assert.Equal((u, true), lying.Image(Axis.X));
        Assert.Equal((v, true), lying.Image(Axis.Y));
    }

    [Fact]
    public void A_click_on_the_tops_upper_face_lays_24_inches_of_2x4_flat_on_it_and_holds_it_there()
    {
        PlacementTool tool = new();
        Assert.True(tool.Arm(TwoByFour()));
        PlacementFace upper = PlacementFace.Of(Top, BoxFace.Top);
        Point3 at = Point3.Inches(20, 10, 17);

        PlacementPreview preview = Place(tool, upper, at, at);

        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
        Assert.Equal(Length.Inches(17), low.Z);
        Assert.Equal(Length.Inches(24), high.X - low.X);
        Assert.Equal(Length.Inches(3, 1, 2), high.Y - low.Y);
        Assert.Equal(Length.Inches(1, 1, 2), high.Z - low.Z);
        Assert.Equal("2x4", preview.Part!.Stock);
        Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), preview.Part.PlanAxes);

        (Request add, var holds) = PlacementTool.Requests(Table, preview);
        Assert.IsType<Batch>(add);
        Flush flush = Assert.IsType<Flush>(holds[0]);
        Assert.Equal(new FeatureRef(Top.Id, BoxFeature.Face(BoxFace.Top)), flush.A);
        Assert.Equal(new FeatureRef(NewPart, BoxFeature.Face(BoxFace.Bottom)), flush.B);
    }

    [Fact]
    public void A_drag_along_a_north_face_stands_the_board_against_it_for_the_length_dragged()
    {
        PlacementTool tool = new();
        tool.Arm(TwoByFour());
        PlacementFace north = PlacementFace.Of(Top, BoxFace.North);

        PlacementPreview preview = Place(tool, north, Point3.Inches(4, 24, 16), Point3.Inches(34, 24, 16));

        // Its thickness runs north from the face, its length east along the drag, its width up.
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
        Assert.Equal(Length.Inches(24), low.Y);
        Assert.Equal(Length.Inches(1, 1, 2), high.Y - low.Y);
        Assert.Equal((Length.Inches(4), Length.Inches(34)), (low.X, high.X));
        Assert.Equal(Length.Inches(3, 1, 2), high.Z - low.Z);

        Flush flush = Assert.IsType<Flush>(PlacementTool.Requests(Table, preview).Holds[0]);
        Assert.Equal(new FeatureRef(Top.Id, BoxFeature.Face(BoxFace.North)), flush.A);
        Assert.Equal(PlacementTool.Contact(preview.Box, north), ((FeatureRef)flush.B).Feature.Faces.Single());
        Assert.Equal((Axis.Y, false), preview.Box.Orientation.Normal(PlacementTool.Contact(preview.Box, north)));
    }

    [Fact]
    public void Under_a_face_that_faces_down_the_part_hangs_below_it()
    {
        PlacementTool tool = new();
        tool.ArmPlainBoard();
        PlacementFace underside = PlacementFace.Of(Top, BoxFace.Bottom);
        Point3 at = Point3.Inches(24, 12, 0) with { Z = Length.Inches(16, 1, 4) };

        PlacementPreview preview = Place(tool, underside, at, at);

        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
        Assert.Equal(Length.Inches(16, 1, 4), high.Z);
        Assert.Equal(Box.DefaultDepth, high.Z - low.Z);
        Assert.Equal((Length.Inches(24), Length.Inches(12)), (high.X - low.X, high.Y - low.Y));
        Assert.Null(preview.Part);
    }

    [Fact]
    public void On_the_floor_a_part_sits_at_zero_and_nothing_holds_it()
    {
        PlacementTool tool = new();
        tool.Arm(TwoByFour());

        PlacementPreview preview = Place(tool, PlacementFace.Floor, Point3.Inches(60, 0, 0), Point3.Inches(60, 30, 0));

        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
        Assert.Equal(Length.Zero, low.Z);
        Assert.Equal(Length.Inches(30), high.Y - low.Y);
        Assert.Empty(PlacementTool.Requests(Table, preview).Holds);
    }

    [Fact]
    public void A_2x4_turned_about_y_stands_on_end_on_the_top_for_its_default_length(/* #92 */)
    {
        PlacementTool tool = new();
        tool.Arm(TwoByFour());
        PlacementFace upper = PlacementFace.Of(Top, BoxFace.Top);
        tool.Turn(Axis.Y, 1, upper);
        Point3 at = Point3.Inches(20, 10, 17);

        PlacementPreview preview = Place(tool, upper, at, at);

        // Its length points up out of the face: 24", resting on the top.
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
        Assert.Equal(Length.Inches(17), low.Z);
        Assert.Equal(Length.Inches(24), high.Z - low.Z);
        Assert.Equal(Axis.Z, preview.Box.Orientation.Image(Axis.X).Axis);

        // And it is held by whichever of its faces now faces down.
        Flush flush = Assert.IsType<Flush>(PlacementTool.Requests(Table, preview).Holds[0]);
        BoxFace touching = ((FeatureRef)flush.B).Feature.Faces.Single();
        Assert.Equal((Axis.Z, false), preview.Box.Orientation.Normal(touching));
    }

    [Fact]
    public void A_drag_sets_a_turned_parts_length_only_along_the_axis_it_lies_on(/* #92 */)
    {
        // Turned about Z, a 2x4 lying on the floor runs north-south: a drag north states its length.
        PlacementTool tool = new();
        tool.Arm(TwoByFour());
        tool.Turn(Axis.Z, 1, PlacementFace.Floor);

        PlacementPreview north = Place(tool, PlacementFace.Floor, Point3.Inches(60, 0, 0), Point3.Inches(60, 30, 0));
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(north.Box);
        Assert.Equal(Length.Inches(30), high.Y - low.Y);
        Assert.Equal((Length.Zero, Length.Inches(30)), (low.Y, high.Y));

        // Stood on end instead, the drag cannot state a length that points out of the floor: the
        // length stays the default however far the drag goes.
        PlacementTool onEnd = new();
        onEnd.Arm(TwoByFour());
        onEnd.Turn(Axis.Y, 1, PlacementFace.Floor);
        PlacementPreview upright = Place(onEnd, PlacementFace.Floor, Point3.Inches(60, 0, 0), Point3.Inches(60, 30, 0));
        Assert.Equal(Axis.Z, upright.Box.Orientation.Image(Axis.X).Axis);
        (Point3 uprightLow, Point3 uprightHigh) = SpaceSnapResolver.Extent(upright.Box);
        Assert.Equal(PlacementTool.DefaultLength, uprightHigh.Z - uprightLow.Z);
        Assert.Equal(Length.Zero, uprightLow.Z);
    }

    [Fact]
    public void Picking_something_up_again_starts_it_flat()
    {
        PlacementTool tool = new();
        tool.Arm(TwoByFour());
        tool.Turn(Axis.Y, 1, PlacementFace.Floor);
        Assert.NotNull(tool.Turned);

        tool.Arm(TwoByFour());
        Assert.Null(tool.Turned);
    }

    [Fact]
    public void Nothing_held_places_nothing_and_a_fastener_cannot_be_held()
    {
        PlacementTool tool = new();
        Assert.Null(tool.Shape(Table, PlacementFace.Floor, Point3.Origin, Point3.Origin, LayerId.Default, NewPart, "Part", 1, Length.Zero));

        StockItem fastener = MaterialsLibrary.Shipped.InCategory(StockCategory.Fastener).First();
        Assert.False(tool.Arm(fastener));
        Assert.False(tool.IsArmed);
    }
}
