using Napkin.App.Editing;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What the plan canvas sees of a box in space, docs/design/assembly-model.md &#xA7;7.2: the
/// footprint for grips, snaps and hit-testing, and one three-way rule for drawing a cut part.
/// </summary>
public class PlanShapeTests
{
    // 48" x 24" x 3/4", anchored at (10, 20) in the plan.
    static Box Blank(BoxFace faceUp, int quarterTurns = 0, params Cut[] cuts) => new(
        EntityId.New(),
        LayerId.Default,
        Point3.Inches(10, 20, 0),
        Length.Inches(48),
        Length.Inches(24),
        new Length(768),
        faceUp,
        Angle.Right * quarterTurns)
    {
        Cuts = [.. cuts],
    };

    [Fact]
    public void A_box_as_drawn_draws_its_own_outline_moved_to_the_anchor()
    {
        Box box = Blank(BoxFace.Top, 0, new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(4)));

        Outline plan = PlanShape.Outline(box);
        Assert.Equal(
            box.Outline().Vertices.Select(point => point + new Vector2(Length.Inches(10), Length.Inches(20))),
            plan.Vertices);
        Assert.True(PlanShape.ShowsCap(box));

        // In the cut-away corner there is nothing to pick; just inside it there is.
        Assert.False(PlanShape.Contains(box, Point2.Inches(57, 43)));
        Assert.True(PlanShape.Contains(box, Point2.Inches(50, 40)));
    }

    [Fact]
    public void A_box_turned_over_draws_its_outline_mirrored_north_to_south()
    {
        // Bottom up: local (x, y) lands at (x, -y), so the cut north-east corner of the blank is
        // at the plan's south-east, below the anchor.
        Box box = Blank(BoxFace.Bottom, 0, new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(4)));

        Outline plan = PlanShape.Outline(box);
        Assert.Equal(
            box.Outline().Vertices.Select(point => new Point2(point.X + Length.Inches(10), Length.Inches(20) - point.Y)),
            plan.Vertices);

        Assert.False(PlanShape.Contains(box, Point2.Inches(57, -3)));
        Assert.True(PlanShape.Contains(box, Point2.Inches(50, 0)));
        Assert.True(PlanShape.Contains(box, Point2.Inches(12, 18)));
        Assert.False(PlanShape.Contains(box, Point2.Inches(12, 22)));
    }

    [Fact]
    public void An_inward_curve_on_a_box_turned_over_still_takes_material_away()
    {
        // The hit test runs in the blank's own frame, so the counter-clockwise walk the curve
        // tests read is kept even though the plan sees the outline mirrored.
        Box box = Blank(BoxFace.Bottom, 0, new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(3)));

        // The blank's north edge is the plan's south side (y = 20 - 24 = -4); the curve's middle
        // dips 3" into the blank there.
        Assert.False(PlanShape.Contains(box, Point2.Inches(34, -3)));
        Assert.True(PlanShape.Contains(box, Point2.Inches(34, 0)));
        Assert.True(BoxGeometry.ContainsShape(box, Point2.Inches(34, 0)));
    }

    [Fact]
    public void A_box_on_its_side_draws_and_picks_its_footprint_and_no_cut()
    {
        // East up: the plan sees the blank's depth across and its height up, from x = 10 - 3/4"
        // to 10, and the corner cut runs along the depth, where the plan cannot see it.
        Box box = Blank(BoxFace.East, 0, new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(4)));
        Footprint footprint = box.Footprint();

        Assert.False(PlanShape.ShowsCap(box));
        Assert.Equal(
            [
                footprint.Corner(BoxCorner.SouthWest),
                footprint.Corner(BoxCorner.SouthEast),
                footprint.Corner(BoxCorner.NorthEast),
                footprint.Corner(BoxCorner.NorthWest),
            ],
            PlanShape.Outline(box).Vertices);
        Assert.Equal(new Point2(Length.Inches(10) - new Length(768), Length.Inches(20)), footprint.Corner(BoxCorner.SouthWest));

        Point2 nearTheCut = new(Length.Inches(10) - new Length(384), Length.Inches(43));
        Assert.True(PlanShape.Contains(box, nearTheCut));
        Assert.True(BoxGeometry.IsWithinShape(box, nearTheCut, Length.Zero));
        Assert.False(PlanShape.Contains(box, Point2.Inches(11, 30)));
    }

    [Fact]
    public void Grips_and_edges_are_the_footprints()
    {
        Box box = Blank(BoxFace.North, 1);
        Footprint footprint = box.Footprint();

        Assert.Equal(footprint.Corner(BoxCorner.NorthEast), BoxGeometry.GripPoint(box, BoxGrip.NorthEast));
        Assert.Equal(footprint.Center, BoxGeometry.GripPoint(box, BoxGrip.Body));
        Assert.Equal(new Length(768), BoxGeometry.SizeAcross(box, BoxEdge.North));
        Assert.Equal(Length.Inches(48), BoxGeometry.SizeAcross(box, BoxEdge.East));
        Assert.Equal(footprint.PlanWidth.Units * (double)footprint.PlanHeight.Units, BoxGeometry.Area(box));

        List<EdgeLine> edges = [.. BoxGeometry.AxisAlignedEdges(box)];
        Assert.Equal(4, edges.Count);

        // Spun a quarter turn, the footprint's south side runs up the plan at its low X... which
        // is the anchor's X minus nothing, and its plan height (3/4") is now along -X.
        EdgeLine south = Assert.Single(edges, edge => edge.Edge == BoxEdge.South);
        Assert.Equal(Axis.X, south.NormalAxis);
    }

    [Fact]
    public void A_snap_onto_a_box_on_its_side_lines_the_part_up_and_states_the_face_the_plan_sees()
    {
        // The target stands East up: its plan west side is the blank's top face, and since #70 the
        // snap names it through the footprint rather than landing the part and saying nothing.
        Box standing = Blank(BoxFace.East) with { Id = EditingBuilder.Id(0) };
        Box moving = Box.AsDrawn(EditingBuilder.Id(1), LayerId.Default, Point2.Inches(0, 25), Length.Inches(4), Length.Inches(4), Box.DefaultDepth, Angle.Zero);
        Sketch sketch = Sketch.Empty.WithEntity(standing).WithEntity(moving);

        // The standing box's plan west side is at x = 10 - 3/4"; drop the mover's east side near it.
        SnapPlan plan = SnapResolver.Resolve(sketch, moving, new Point2(Length.Inches(5), Length.Inches(25)), 1, Length.Inches(1));

        Assert.True(plan.CaughtSomething);
        Assert.Equal((Length.Inches(10) - new Length(768) - Length.Inches(4)).Units, plan.Anchor.X.Units);
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(standing.Id, BoxFeature.Face(BoxFace.Top)), flush.A);
        Assert.Equal(new FeatureRef(moving.Id, BoxFeature.Face(BoxFace.East)), flush.B);
    }

    [Fact]
    public void A_width_that_stands_vertical_is_not_drawn_as_a_plan_dimension()
    {
        Box standing = Blank(BoxFace.East) with { Id = EditingBuilder.Id(0) };
        Sketch sketch = Sketch.Empty.WithEntity(standing);

        Dimension width = new(EntityId.New(), LayerId.Default, new ParamMeasurand(new BoxWidthRef(standing.Id)), null, new DimensionPlacement(Length.Inches(1), DimensionSide.South));
        Dimension height = new(EntityId.New(), LayerId.Default, new ParamMeasurand(new BoxHeightRef(standing.Id)), null, new DimensionPlacement(Length.Inches(1), DimensionSide.West));

        Assert.False(DimensionLayout.TryMeasure(sketch, width, out _));
        Assert.True(DimensionLayout.TryMeasure(sketch, height, out DimensionMeasurement? measured));
        Assert.Equal(Length.Inches(24), measured.Value);
        Assert.Equal(standing.Footprint().Corner(BoxCorner.SouthWest), measured.From);
    }
}
