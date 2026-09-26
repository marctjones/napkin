using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The plan's dimension layout (<see cref="PlanDimensions"/>), worked by hand on a 48″ × 24″ blank
/// anchored at (10, 20): what each kind of dimension measures, which side its line goes, and the
/// dimensions the plan cannot draw.
/// </summary>
public class PlanDimensionsTests
{
    static readonly LengthFormat Sixteenths = new FeetInchesFormat(16);
    static readonly Length Four = Length.Inches(4);

    static Box Blank(BoxFace faceUp = BoxFace.Top, int quarterTurns = 0) => new(
        EntityId.New(),
        LayerId.Default,
        Point3.Inches(10, 20, 0),
        Length.Inches(48),
        Length.Inches(24),
        new Length(768),
        faceUp,
        Angle.Right * quarterTurns);

    static Dimension Measuring(Measurand what, DimensionSide side) => new(EntityId.New(), LayerId.Default, what, null, new DimensionPlacement(Four, side));

    static DimensionMeasurement? Measure(Sketch sketch, Dimension dimension)
        => PlanDimensions.TryMeasure(sketch.WithEntity(dimension), dimension, out DimensionMeasurement? measured) ? measured : null;

    [Theory]
    // South: along the south edge, the line 4″ below it at y 16.
    [InlineData(DimensionSide.South, 20, 16)]
    // North: along the north edge (y 44), the line 4″ above it.
    [InlineData(DimensionSide.North, 44, 48)]
    public void A_width_runs_along_the_side_it_sits_beside(DimensionSide side, long edgeY, long lineY)
    {
        Box box = Blank();
        DimensionMeasurement measured = Measure(Sketch.Empty.WithEntity(box), Measuring(new ParamMeasurand(new BoxWidthRef(box.Id)), side))!;

        Assert.Equal((Point2.Inches(10, edgeY), Point2.Inches(58, edgeY)), (measured.From, measured.To));
        Assert.Equal((Point2.Inches(10, lineY), Point2.Inches(58, lineY)), (measured.LineFrom, measured.LineTo));
        Assert.Equal((Axis.X, Length.Inches(48)), (measured.Axis, measured.Value));
        Assert.Equal("4'-0\"", measured.Label(Sixteenths));
        Assert.Equal(Point2.Inches(34, lineY), measured.LabelAnchor);
    }

    [Theory]
    // West: along the west edge (x 10), the line 4″ to its left.
    [InlineData(DimensionSide.West, 10, 6)]
    [InlineData(DimensionSide.East, 58, 62)]
    public void A_height_runs_along_the_side_it_sits_beside(DimensionSide side, long edgeX, long lineX)
    {
        Box box = Blank();
        DimensionMeasurement measured = Measure(Sketch.Empty.WithEntity(box), Measuring(new ParamMeasurand(new BoxHeightRef(box.Id)), side))!;

        Assert.Equal((Point2.Inches(edgeX, 20), Point2.Inches(edgeX, 44)), (measured.From, measured.To));
        Assert.Equal((Point2.Inches(lineX, 20), Point2.Inches(lineX, 44)), (measured.LineFrom, measured.LineTo));
        Assert.Equal((Axis.Y, Length.Inches(24)), (measured.Axis, measured.Value));
    }

    [Fact]
    public void A_quarter_turned_box_gets_a_vertical_width_without_a_special_case()
    {
        Box box = Blank(quarterTurns: 1);
        DimensionMeasurement measured = Measure(Sketch.Empty.WithEntity(box), Measuring(new ParamMeasurand(new BoxWidthRef(box.Id)), DimensionSide.East))!;

        Assert.Equal(Axis.Y, measured.Axis);
        Assert.Equal(Length.Inches(48), measured.Value);
    }

    [Fact]
    public void A_width_or_height_standing_vertical_or_of_a_missing_box_is_not_drawn()
    {
        Box onEdge = Blank(BoxFace.South);
        Box onEnd = Blank(BoxFace.East);
        Sketch sketch = Sketch.Empty.WithEntity(onEdge).WithEntity(onEnd);

        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new BoxHeightRef(onEdge.Id)), DimensionSide.West)));
        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new BoxWidthRef(onEnd.Id)), DimensionSide.South)));
        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new BoxWidthRef(EntityId.New())), DimensionSide.South)));
        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new BoxHeightRef(EntityId.New())), DimensionSide.West)));
        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new BoxDepthRef(onEdge.Id)), DimensionSide.West)));
    }

    [Fact]
    public void A_segment_is_measured_end_to_end_and_a_dangling_one_is_not_drawn()
    {
        Node start = new(EntityId.New(), LayerId.Default, Point2.Inches(0, 0));
        Node end = new(EntityId.New(), LayerId.Default, Point2.Inches(30, 40));
        Segment segment = new(EntityId.New(), LayerId.Default, start.Id, end.Id);
        Sketch sketch = Sketch.Empty.WithEntity(start).WithEntity(end).WithEntity(segment);

        // 30-40-50: the length is the segment's, and it lies mostly along Y, so it is a Y dimension.
        DimensionMeasurement measured = Measure(sketch, Measuring(new ParamMeasurand(new SegmentLengthRef(segment.Id)), DimensionSide.East))!;
        Assert.Equal((Length.Inches(50), Axis.Y), (measured.Value, measured.Axis));

        Assert.Null(Measure(sketch, Measuring(new ParamMeasurand(new SegmentLengthRef(EntityId.New())), DimensionSide.East)));
        Segment toNowhere = new(EntityId.New(), LayerId.Default, start.Id, EntityId.New());
        Assert.Null(Measure(sketch.WithEntity(toNowhere), Measuring(new ParamMeasurand(new SegmentLengthRef(toNowhere.Id)), DimensionSide.East)));
        Segment fromNowhere = new(EntityId.New(), LayerId.Default, EntityId.New(), end.Id);
        Assert.Null(Measure(sketch.WithEntity(fromNowhere), Measuring(new ParamMeasurand(new SegmentLengthRef(fromNowhere.Id)), DimensionSide.East)));
    }

    [Fact]
    public void A_distance_between_two_places_is_measured_along_its_axis()
    {
        Box box = Blank();
        Node node = new(EntityId.New(), LayerId.Default, Point2.Inches(100, 0));
        Sketch sketch = Sketch.Empty.WithEntity(box).WithEntity(node);

        // The blank's centre (34, 32) to the node: 66″ along X.
        DimensionMeasurement measured = Measure(sketch, Measuring(new AxisMeasurand(new CenterRef(box.Id), new NodeRef(node.Id), Axis.X), DimensionSide.North))!;
        Assert.Equal((Length.Inches(66), Axis.X), (measured.Value, measured.Axis));

        // An upright edge of the blank, its south-west corner, to the node: 90″ along X.
        FeatureRef corner = new(box.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest));
        Assert.Equal(Length.Inches(90), Measure(sketch, Measuring(new AxisMeasurand(corner, new NodeRef(node.Id), Axis.X), DimensionSide.South))!.Value);
    }

    [Fact]
    public void A_distance_the_plan_cannot_place_or_of_nothing_is_not_drawn()
    {
        Box box = Blank();
        Node node = new(EntityId.New(), LayerId.Default, Point2.Inches(100, 0));
        Sketch sketch = Sketch.Empty.WithEntity(box).WithEntity(node);
        Dimension Between(PlaceRef from, PlaceRef to, Axis axis = Axis.X) => Measuring(new AxisMeasurand(from, to, axis), DimensionSide.North);

        // A face fixes only one coordinate; a feature with no faces is nothing; a missing node or box.
        Assert.Null(Measure(sketch, Between(new FeatureRef(box.Id, BoxFeature.Face(BoxFace.West)), new NodeRef(node.Id))));
        Assert.Null(Measure(sketch, Between(new FeatureRef(box.Id, default), new NodeRef(node.Id))));
        Assert.Null(Measure(sketch, Between(new NodeRef(node.Id), new NodeRef(EntityId.New()))));
        Assert.Null(Measure(sketch, Between(new CenterRef(EntityId.New()), new NodeRef(node.Id))));

        // An angled part's end is not a place the plan dimensions yet.
        Assert.Null(Measure(sketch, Between(new StrutEndRef(EntityId.New(), StrutEnd.From), new NodeRef(node.Id))));

        // No distance along the axis asked: nothing to draw.
        Assert.Null(Measure(sketch, Between(new NodeRef(node.Id), new NodeRef(node.Id))));
    }

    [Fact]
    public void A_distance_along_Z_has_nowhere_to_go_in_the_plan_and_says_so()
    {
        Box low = Blank();
        Box high = Blank() with { Anchor = Point3.Inches(10, 20, 30) };
        Sketch sketch = Sketch.Empty.WithEntity(low).WithEntity(high);
        Dimension up = Measuring(new AxisMeasurand(new CenterRef(low.Id), new CenterRef(high.Id), Axis.Z), DimensionSide.North);

        Assert.Throws<ArgumentOutOfRangeException>(() => Measure(sketch, up));
    }

    [Fact]
    public void Measure_lists_what_can_be_drawn_in_id_order_and_skips_the_rest()
    {
        Box box = Blank();
        Dimension first = Measuring(new ParamMeasurand(new BoxWidthRef(box.Id)), DimensionSide.South) with { Id = new EntityId(new Guid("00000000-0000-4000-8000-000000000001")) };
        Dimension dangling = Measuring(new ParamMeasurand(new BoxWidthRef(EntityId.New())), DimensionSide.South) with { Id = new EntityId(new Guid("00000000-0000-4000-8000-000000000002")) };
        Dimension second = Measuring(new ParamMeasurand(new BoxHeightRef(box.Id)), DimensionSide.West) with { Id = new EntityId(new Guid("00000000-0000-4000-8000-000000000003")) };
        Sketch sketch = Sketch.Empty.WithEntity(box).WithEntity(second).WithEntity(dangling).WithEntity(first);

        Assert.Equal([first.Id, second.Id], PlanDimensions.Measure(sketch).Select(measured => measured.Dimension.Id));
    }
}
