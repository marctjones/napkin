using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Where a dragged part lands, and what the drawing would then be able to say about it.
/// </summary>
/// <remarks>
/// CVS-006's rule runs through every test here: a snap produces a <em>candidate relationship</em>,
/// and the relationship is what makes the alignment real. Nothing is ever inferred from where two
/// parts happen to sit (docs/design/geometry-model.md &#xA7;3.2) — which is why the resolver hands
/// back statements to store rather than just a position.
/// </remarks>
public class SnapResolverTests
{
    const double OneInchGrid = 1;

    static readonly Length Radius = Length.Inches(1);

    [Fact]
    [Trait("Feature", "CVS-006")]
    public void An_edge_within_the_radius_wins_over_the_grid_and_states_a_Flush()
    {
        // The target's right-hand edge is at 10 1/2", which the one-inch grid cannot reach.
        Design design = EditingBuilder.Design(
            (Point2.Inches(0, 0), Length.Inches(10, 1, 2), Length.Inches(12)),
            EditingBuilder.At(14, 4, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(1),
            new Point2(Length.Inches(10, 3, 4), Length.Inches(4)),
            OneInchGrid,
            Radius);

        Assert.True(plan.CaughtSomething);
        Assert.Equal(Length.Inches(10, 1, 2).Units, plan.Anchor.X.Units);

        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(LocalFeatures.Edge(EditingBuilder.Id(0), BoxEdge.East), flush.A);
        Assert.Equal(LocalFeatures.Edge(EditingBuilder.Id(1), BoxEdge.West), flush.B);

        SnapHit hit = Assert.Single(plan.Hits, candidate => candidate.Kind == SnapKind.Edge);
        Assert.Equal(Axis.X, hit.Axis);
        Assert.Equal(EditingBuilder.Id(0), hit.Target);
    }

    [Fact]
    public void The_moving_part_is_the_one_that_follows()
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(14, 4, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(1),
            Point2.Inches(10, 4),
            OneInchGrid,
            Radius);

        // Flush(a, b) reads "b follows a", so the part being dragged has to be b — otherwise
        // dropping one part would move the one it was dropped against.
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(EditingBuilder.Id(1), flush.B.Owner);
    }

    [Fact]
    [Trait("Feature", "CVS-006")]
    public void Both_axes_onto_one_part_is_a_corner_and_states_one_Coincident()
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(20, 20, 10, 12));

        // Dropped a fraction away from the target's bottom-right corner, on both axes at once.
        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(1),
            new Point2(Length.Inches(10, 1, 4), Length.Inches(0, 1, 4)),
            OneInchGrid,
            Radius);

        Coincident coincident = Assert.IsType<Coincident>(Assert.Single(plan.Relationships));
        Assert.Equal(LocalFeatures.Corner(EditingBuilder.Id(0), BoxCorner.SouthEast), coincident.A);
        Assert.Equal(LocalFeatures.Corner(EditingBuilder.Id(1), BoxCorner.SouthWest), coincident.B);
        Assert.All(plan.Hits, hit => Assert.Equal(SnapKind.Corner, hit.Kind));
    }

    [Fact]
    public void Nothing_near_enough_lands_on_the_grid_and_states_nothing()
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(40, 40, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(1),
            new Point2(Length.Inches(40, 3, 8), Length.Inches(39, 5, 8)),
            OneInchGrid,
            Radius);

        Assert.False(plan.CaughtSomething);
        Assert.Empty(plan.Relationships);
        Assert.Equal(Length.Inches(40).Units, plan.Anchor.X.Units);
        Assert.Equal(Length.Inches(40).Units, plan.Anchor.Y.Units);
        Assert.All(plan.Hits, hit => Assert.Equal(SnapKind.Grid, hit.Kind));
    }

    [Fact]
    public void An_edge_that_does_not_overlap_is_not_a_target()
    {
        // The same X, but three feet up the page: two parts that share a coordinate and nothing
        // else are not lined up with each other in any sense a person means.
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(10, 100, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(1),
            new Point2(Length.Inches(10, 1, 4), Length.Inches(100)),
            OneInchGrid,
            Radius);

        Assert.False(plan.CaughtSomething);
        Assert.Empty(plan.Relationships);
    }

    [Fact]
    public void The_nearest_of_several_edges_is_the_one_caught()
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            (Point2.Inches(12, 0), Length.Inches(10, 1, 4), Length.Inches(12)),
            EditingBuilder.At(40, 4, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(2),
            new Point2(Length.Inches(22, 1, 8), Length.Inches(4)),
            OneInchGrid,
            Radius);

        // 22 1/4" (the middle part's right-hand edge) is an eighth away; 22" (the grid) is an
        // eighth the other way, and the edge wins ties and near-ties alike.
        Assert.Equal(Length.Inches(22, 1, 4).Units, plan.Anchor.X.Units);
        Assert.Equal(Length.Inches(4).Units, plan.Anchor.Y.Units);
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(EditingBuilder.Id(1), flush.A.Owner);
        Assert.Equal(BoxFeature.Face(BoxFace.East), Assert.IsType<FeatureRef>(flush.A).Feature);
    }

    [Fact]
    public void A_part_never_snaps_to_itself()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 12));

        SnapPlan plan = SnapResolver.Resolve(
            design.Sketch,
            design.Box(0),
            new Point2(Length.Inches(0, 1, 8), Length.Inches(0, 1, 8)),
            OneInchGrid,
            Radius);

        Assert.False(plan.CaughtSomething);
        Assert.Empty(plan.Relationships);
    }

    [Fact]
    public void Two_perpendicular_edges_of_one_box_share_exactly_one_corner()
    {
        Assert.Equal(BoxCorner.SouthEast, SnapResolver.SharedCorner(BoxEdge.South, BoxEdge.East));
        Assert.Equal(BoxCorner.NorthWest, SnapResolver.SharedCorner(BoxEdge.North, BoxEdge.West));
        Assert.Null(SnapResolver.SharedCorner(BoxEdge.North, BoxEdge.South));
    }

    [Fact]
    [Trait("Feature", "CVS-006")]
    public void The_canvas_only_offers_what_this_updater_can_hold_for_these_references()
    {
        // The updater's SupportedRelationships lists kinds; its real support is by (kind,
        // reference kind). A Horizontal is supported — on a segment. On a box edge it is not,
        // and the editor has to find that out by asking rather than by reading the list
        // (#10, Fable's review of #35, finding 10).
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 12)));
        EntityId box = EditingBuilder.Id(0);

        Assert.Contains(typeof(Horizontal), DirectUpdater.Instance.SupportedRelationships);
        Assert.False(editor.CanHold(new Horizontal(RelationshipId.New(), LocalFeatures.Edge(box, BoxEdge.South))));

        Assert.True(editor.CanHold(new ParamValue(RelationshipId.New(), new BoxWidthRef(box), Length.Inches(4))));
        Assert.True(editor.CanHold(new Anchored(RelationshipId.New(), box)));

        // And a kind reserved for the solver is refused whatever it names.
        Assert.False(editor.CanHold(new Napkin.Core.Geometry.Parallel(
            RelationshipId.New(),
            LocalFeatures.Edge(box, BoxEdge.South),
            LocalFeatures.Edge(box, BoxEdge.North))));

        // A flush between a south face (Y) and an east face (X) shares no axis: it could never
        // hold, and the refusal says so (docs/design/assembly-model.md §2.3).
        Flush crossed = new(RelationshipId.New(), LocalFeatures.Edge(box, BoxEdge.South), LocalFeatures.Edge(box, BoxEdge.East));
        Assert.False(editor.CanHold(crossed));
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(editor.Design.Sketch, new AddRelationship(crossed)));
        Assert.Equal(RejectionReason.PlacesNotComparable, rejected.Reason);
        Assert.Contains("could never hold", EditMessages.Refusal(rejected.Reason), StringComparison.Ordinal);
    }

    [Fact]
    public void A_flush_written_the_other_way_round_is_the_same_statement()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(10, 0, 10, 12)));

        Flush stated = new(
            RelationshipId.New(),
            LocalFeatures.Edge(EditingBuilder.Id(0), BoxEdge.East),
            LocalFeatures.Edge(EditingBuilder.Id(1), BoxEdge.West));

        editor.Apply(new AddRelationship(stated), "Snapped");

        // The updater's own duplicate check compares records field for field and would not catch
        // this; the drawing would then say the same thing twice, in two list entries.
        Assert.True(editor.AlreadyStates(new Flush(
            RelationshipId.New(),
            LocalFeatures.Edge(EditingBuilder.Id(1), BoxEdge.West),
            LocalFeatures.Edge(EditingBuilder.Id(0), BoxEdge.East))));
    }
}
