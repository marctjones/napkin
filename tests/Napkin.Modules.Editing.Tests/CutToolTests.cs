using System.Collections.Immutable;

using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The shape workshop's tool, and the angle entry beside it — both pure, neither needing a window.
/// </summary>
/// <remarks>
/// <see cref="CutTool"/> is the state machine <c>docs/design/shaped-parts-model.md</c> &#xA7;7.2
/// asks for: two model points and a target, no pixels. So what a gesture produces can be asserted
/// here exactly, at every corner and every edge, and the workflow only has to prove the wiring
/// once.
/// </remarks>
public class CutToolTests
{
    /// <summary>A 48&#x2033; &#xD7; 24&#x2033; blank in its own frame, the size &#xA7;9.1 works in.</summary>
    static Box Blank() => CutTool.Local(Box.AsDrawn(
        EntityId.New(),
        LayerId.Default,
        Point2.Inches(30, 40),
        Length.Inches(48),
        Length.Inches(24),
        Box.DefaultDepth,
        Angle.Zero));

    [Fact]
    public void A_blank_in_its_own_frame_is_at_the_origin_and_unrotated_with_its_cuts_intact()
    {
        Box drawn = Box.AsDrawn(
            EntityId.New(),
            LayerId.Default,
            Point2.Inches(30, 40),
            Length.Inches(48),
            Length.Inches(24),
            Box.DefaultDepth,
            Angle.Right) with
        {
            Cuts = [new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1))],
        };

        Box local = CutTool.Local(drawn);

        Assert.Equal(Point3.Origin, local.Anchor);
        Assert.Equal(Angle.Zero, local.Rotation);
        Assert.Equal(drawn.Cuts, local.Cuts);
        Assert.Equal(drawn.Id, local.Id);
    }

    [Fact]
    public void The_targets_are_the_four_corners_and_the_four_edge_midpoints()
    {
        Box blank = Blank();
        ImmutableArray<CutSite> targets = [.. CutTool.Targets()];

        Assert.Equal(8, targets.Length);
        Assert.Equal(Point2.Origin, CutTool.TargetPoint(blank, CutSite.Corner(BoxCorner.SouthWest)));
        Assert.Equal(
            Point2.Inches(48, 24),
            CutTool.TargetPoint(blank, CutSite.Corner(BoxCorner.NorthEast)));
        Assert.Equal(Point2.Inches(24, 0), CutTool.TargetPoint(blank, CutSite.Edge(BoxEdge.South)));
        Assert.Equal(Point2.Inches(0, 12), CutTool.TargetPoint(blank, CutSite.Edge(BoxEdge.West)));
    }

    [Fact]
    public void A_press_near_a_corner_finds_that_corner_and_a_press_on_the_paper_finds_nothing()
    {
        Box blank = Blank();
        Length tolerance = Length.Inches(1);

        Assert.Equal(
            CutSite.Corner(BoxCorner.SouthEast),
            CutTool.TargetAt(blank, new Point2(Length.Inches(47, 1, 2), Length.Inches(0, 1, 2)), tolerance));
        Assert.Equal(
            CutSite.Edge(BoxEdge.North),
            CutTool.TargetAt(blank, Point2.Inches(24, 24), tolerance));
        Assert.Null(CutTool.TargetAt(blank, Point2.Inches(20, 12), tolerance));
    }

    [Fact]
    public void Dragging_inward_from_a_corner_clips_it_and_the_setbacks_follow_the_pointer()
    {
        Box blank = Blank();

        Cut? candidate = CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.SouthWest),
            Point2.Inches(6, 3),
            CutModifiers.None,
            gridStepInches: 1);

        CornerCut clip = Assert.IsType<CornerCut>(candidate);
        Assert.Equal(BoxCorner.SouthWest, clip.Corner);
        Assert.Equal(Length.Inches(6).Units, clip.AlongX.Units);
        Assert.Equal(Length.Inches(3).Units, clip.AlongY.Units);
    }

    [Fact]
    public void A_clip_is_measured_from_the_corner_it_was_pressed_at_whichever_one_that_is()
    {
        Box blank = Blank();

        CornerCut clip = Assert.IsType<CornerCut>(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.NorthEast),
            Point2.Inches(44, 22),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(BoxCorner.NorthEast, clip.Corner);
        Assert.Equal(Length.Inches(4).Units, clip.AlongX.Units);
        Assert.Equal(Length.Inches(2).Units, clip.AlongY.Units);
    }

    [Fact]
    public void The_setbacks_land_on_the_grid_step_in_force()
    {
        Box blank = Blank();

        CornerCut clip = Assert.IsType<CornerCut>(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.SouthWest),
            new Point2(Length.Inches(5, 5, 8), Length.Inches(2, 3, 8)),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(Length.Inches(6).Units, clip.AlongX.Units);
        Assert.Equal(Length.Inches(2).Units, clip.AlongY.Units);
    }

    [Fact]
    public void The_equal_modifier_holds_the_two_setbacks_the_same_which_is_45_degrees()
    {
        Box blank = Blank();

        CornerCut clip = Assert.IsType<CornerCut>(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.SouthWest),
            Point2.Inches(6, 2),
            CutModifiers.Equal,
            gridStepInches: 1));

        Assert.Equal(clip.AlongX, clip.AlongY);
        Assert.Equal(Length.Inches(4).Units, clip.AlongX.Units);
        Assert.Equal("45°", CutAngle.Text(clip.AlongY, clip.AlongX));
    }

    [Fact]
    public void The_round_modifier_makes_a_roundover_instead_of_a_clip()
    {
        Box blank = Blank();

        RoundedCorner rounded = Assert.IsType<RoundedCorner>(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.NorthWest),
            Point2.Inches(3, 21),
            CutModifiers.Round,
            gridStepInches: 1));

        Assert.Equal(BoxCorner.NorthWest, rounded.Corner);
        Assert.Equal(Length.Inches(3).Units, rounded.Radius.Units);
    }

    [Fact]
    public void A_setback_dragged_past_the_far_edge_stops_at_it_rather_than_asking_for_a_cut_that_cannot_fit()
    {
        Box blank = Blank();

        CornerCut clip = Assert.IsType<CornerCut>(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.SouthWest),
            Point2.Inches(300, 300),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(blank.Width.Units, clip.AlongX.Units);
        Assert.Equal(blank.Height.Units, clip.AlongY.Units);
    }

    [Fact]
    public void A_press_that_has_gone_nowhere_asks_for_nothing()
    {
        Box blank = Blank();

        Assert.Null(CutTool.CandidateFor(
            blank,
            CutSite.Corner(BoxCorner.SouthWest),
            new Point2(Length.Inches(0, 1, 8), Length.Inches(0, 1, 8)),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Null(CutTool.CandidateFor(
            blank,
            CutSite.Edge(BoxEdge.South),
            new Point2(Length.Inches(24), Length.Inches(0, 1, 8)),
            CutModifiers.None,
            gridStepInches: 1));
    }

    [Fact]
    public void Dragging_an_edge_midpoint_into_the_part_scallops_it_and_out_of_it_bows_it()
    {
        Box blank = Blank();

        CurvedEdge scallop = Assert.IsType<CurvedEdge>(CutTool.CandidateFor(
            blank,
            CutSite.Edge(BoxEdge.South),
            Point2.Inches(24, 4),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(BoxEdge.South, scallop.Edge);
        Assert.Equal(Bow.Inward, scallop.Bow);
        Assert.Equal(Length.Inches(4).Units, scallop.Depth.Units);

        CurvedEdge bow = Assert.IsType<CurvedEdge>(CutTool.CandidateFor(
            blank,
            CutSite.Edge(BoxEdge.South),
            Point2.Inches(24, -4),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(Bow.Outward, bow.Bow);
        Assert.Equal(Length.Inches(4).Units, bow.Depth.Units);
    }

    [Fact]
    public void An_edge_bows_the_way_the_pointer_went_whichever_edge_it_is()
    {
        Box blank = Blank();

        CurvedEdge east = Assert.IsType<CurvedEdge>(CutTool.CandidateFor(
            blank,
            CutSite.Edge(BoxEdge.East),
            Point2.Inches(45, 12),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(BoxEdge.East, east.Edge);
        Assert.Equal(Bow.Inward, east.Bow);
        Assert.Equal(Length.Inches(3).Units, east.Depth.Units);
    }

    [Fact]
    public void A_scallop_stops_short_of_the_far_side_so_something_is_always_left()
    {
        Box blank = Blank();

        CurvedEdge scallop = Assert.IsType<CurvedEdge>(CutTool.CandidateFor(
            blank,
            CutSite.Edge(BoxEdge.South),
            Point2.Inches(24, 400),
            CutModifiers.None,
            gridStepInches: 1));

        Assert.Equal(Bow.Inward, scallop.Bow);
        Assert.True(scallop.Depth < blank.Height);
    }

    [Fact]
    public void A_whole_gesture_is_one_SetCut_at_the_site_that_was_pressed()
    {
        Box blank = Blank();
        CutTool tool = new();

        tool.Begin(blank, CutSite.Corner(BoxCorner.SouthEast), gridStepInches: 1);
        Assert.True(tool.IsCutting);

        tool.MoveTo(Point2.Inches(46, 1), CutModifiers.None);
        tool.MoveTo(Point2.Inches(42, 4), CutModifiers.None);

        Assert.True(tool.TryComplete(out SetCut? request));
        Assert.False(tool.IsCutting);
        Assert.Equal(blank.Id, request!.Box);

        CornerCut clip = Assert.IsType<CornerCut>(request.Cut);
        Assert.Equal(CutSite.Corner(BoxCorner.SouthEast), clip.Site);
        Assert.Equal(Length.Inches(6).Units, clip.AlongX.Units);
        Assert.Equal(Length.Inches(4).Units, clip.AlongY.Units);
    }

    [Fact]
    public void A_press_and_release_with_no_drag_between_them_completes_nothing()
    {
        CutTool tool = new();
        tool.Begin(Blank(), CutSite.Corner(BoxCorner.SouthWest), gridStepInches: 1);

        Assert.False(tool.TryComplete(out SetCut? request));
        Assert.Null(request);
    }

    [Fact]
    public void A_cancelled_gesture_makes_nothing()
    {
        CutTool tool = new();
        tool.Begin(Blank(), CutSite.Corner(BoxCorner.SouthWest), gridStepInches: 1);
        tool.MoveTo(Point2.Inches(6, 6), CutModifiers.None);
        tool.Cancel();

        Assert.False(tool.IsCutting);
        Assert.False(tool.TryComplete(out _));
    }

    [Fact]
    public void Hovering_a_target_says_what_a_press_would_do()
    {
        Assert.Equal(
            CutGesture.Clip,
            CutTool.GestureFor(CutSite.Corner(BoxCorner.SouthWest), CutModifiers.None));
        Assert.Equal(
            CutGesture.Clip,
            CutTool.GestureFor(CutSite.Corner(BoxCorner.SouthWest), CutModifiers.Equal));
        Assert.Equal(
            CutGesture.Round,
            CutTool.GestureFor(CutSite.Corner(BoxCorner.SouthWest), CutModifiers.Round));
        Assert.Equal(
            CutGesture.Curve,
            CutTool.GestureFor(CutSite.Edge(BoxEdge.North), CutModifiers.None));
    }

    // ---- Angle entry ---------------------------------------------------------------------

    [Fact]
    public void An_angle_typed_for_a_mitre_becomes_the_setback_it_implies_with_one_rounding()
    {
        Length whole = Length.Inches(5);

        Length? converted = CutAngle.SetbackFor(whole, 30);
        Assert.NotNull(converted);
        Length setback = converted.Value;

        // 5" x tan 30 degrees is 2.8867..", which is 2956.0.. of the 1/1024" units: one rounding,
        // and the field says so by reading back approximately.
        Assert.Equal(2956, setback.Units);
        Assert.False(CutAngle.IsExact(setback, whole));
        Assert.StartsWith(CutAngle.Approximately, CutAngle.Text(setback, whole), StringComparison.Ordinal);
    }

    [Fact]
    public void Forty_five_degrees_is_the_one_angle_that_is_exact()
    {
        Length whole = Length.Inches(5);

        Length? converted = CutAngle.SetbackFor(whole, 45);
        Assert.NotNull(converted);
        Length setback = converted.Value;

        Assert.Equal(whole.Units, setback.Units);
        Assert.True(CutAngle.IsExact(setback, whole));
        Assert.Equal("45°", CutAngle.Text(setback, whole));
    }

    [Fact]
    public void An_angle_a_cut_cannot_be_made_at_is_refused_rather_than_rounded_to_something_else()
    {
        Length whole = Length.Inches(5);

        Assert.Null(CutAngle.SetbackFor(whole, 0));
        Assert.Null(CutAngle.SetbackFor(whole, 90));
        Assert.Null(CutAngle.SetbackFor(whole, -12));
        Assert.Null(CutAngle.SetbackFor(Length.Zero, 30));
    }

    [Theory]
    [InlineData("31", 31)]
    [InlineData(" 31° ", 31)]
    [InlineData("22.5", 22.5)]
    public void An_angle_field_reads_a_plain_number_of_degrees(string typed, double expected)
    {
        Assert.True(CutAngle.TryParseDegrees(typed, out double degrees));
        Assert.Equal(expected, degrees);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("square")]
    [InlineData("3/4\"")]
    public void Text_that_is_not_an_angle_is_refused(string typed)
    {
        Assert.False(CutAngle.TryParseDegrees(typed, out _));
    }

    [Fact]
    public void The_workshop_and_the_cut_list_agree_about_what_angle_a_mitre_is_cut_at()
    {
        // A rail 5" wide with a mitre laid out 3" in along its edge: the field the workshop shows
        // and the sentence the cut list prints have to be the same number, or one of them is
        // lying about the saw setting.
        Length setback = Length.Inches(3);
        Length whole = Length.Inches(5);
        string field = CutAngle.Text(setback, whole);

        ImmutableArray<string> sentences = CutDescription.Describe(
            [new CornerCut(BoxCorner.SouthWest, whole, setback)],
            new FinishedSize(whole, Length.Inches(4), Length.Inches(0, 3, 4)),
            new PlanAxes(PartDimension.Length, PartDimension.Width));

        Assert.Equal("≈31°", field);
        Assert.Contains(field, Assert.Single(sentences), StringComparison.Ordinal);
    }

    [Fact]
    public void A_full_mitre_sets_both_setbacks_to_the_narrow_dimension_in_one_cut()
    {
        Box rail = CutTool.Local(Box.AsDrawn(
            EntityId.New(),
            LayerId.Default,
            Point2.Origin,
            Length.Inches(36),
            Length.Inches(2),
            Box.DefaultDepth,
            Angle.Zero));

        CornerCut mitre = CutAngle.FullMitre(rail, BoxCorner.SouthEast);

        Assert.Equal(BoxCorner.SouthEast, mitre.Corner);
        Assert.Equal(rail.Height.Units, mitre.AlongX.Units);
        Assert.Equal(rail.Height.Units, mitre.AlongY.Units);
        Assert.Equal("45°", CutAngle.Text(mitre.AlongY, mitre.AlongX));
    }
}
