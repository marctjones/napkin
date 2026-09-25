using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The grid ladder and the snap that rounds to it, worked by hand: the step chosen at a zoom is the
/// finest one at least <see cref="SnapGrid.MinimumSpacingPixels"/> apart on screen, and a snap
/// rounds half away from zero (docs/design/geometry-model.md &#xA7;1.4).
/// </summary>
public class SnapGridTests
{
    static Length Units(long units) => new(units);

    [Theory]
    // 0.25" x 100 px/in = 25 px, already >= 14.
    [InlineData(100, 0.25)]
    // At 10 px/in: 1" is 10 px (too close), 3" is 30 px.
    [InlineData(10, 3)]
    // At 1 px/in: 12" is 12 px, 24" is 24 px.
    [InlineData(1, 24)]
    // So far out that no step reaches 14 px: the coarsest step there is.
    [InlineData(0.0001, 12000)]
    public void The_step_is_the_finest_on_the_ladder_that_is_far_enough_apart(double pixelsPerInch, double expected) =>
        Assert.Equal(expected, SnapGrid.StepInches(pixelsPerInch));

    [Fact]
    public void The_heavy_step_is_at_least_four_minor_steps_and_four_times_as_far_apart()
    {
        // Minor 3" at 10 px/in: heavy must be >= 12" and >= 56 px, so 12" (120 px).
        Assert.Equal(12, SnapGrid.CoarserStepInches(3, 10));

        // Minor 0.25" at 100 px/in: >= 1" and >= 56 px, so 1" (100 px).
        Assert.Equal(1, SnapGrid.CoarserStepInches(0.25, 100));
    }

    [Fact]
    public void There_is_no_heavy_step_past_the_top_of_the_ladder()
    {
        Assert.Equal(0, SnapGrid.CoarserStepInches(12000, 1));
    }

    [Fact]
    public void A_step_is_a_whole_number_of_units_and_never_less_than_one()
    {
        Assert.Equal(Length.UnitsPerInch, SnapGrid.UnitsPerStep(1));
        Assert.Equal(Length.UnitsPerInch / 4, SnapGrid.UnitsPerStep(0.25));
        Assert.Equal(1, SnapGrid.UnitsPerStep(0));
    }

    [Theory]
    // On the grid already.
    [InlineData(3072, 3072)]
    [InlineData(-3072, -3072)]
    // Just under half a step rounds toward zero; exactly half rounds away from it, either sign.
    [InlineData(1535, 1024)]
    [InlineData(1536, 2048)]
    [InlineData(-1535, -1024)]
    [InlineData(-1536, -2048)]
    // Past half a step rounds away from zero.
    [InlineData(1900, 2048)]
    [InlineData(-1900, -2048)]
    [InlineData(-100, 0)]
    public void A_length_snaps_to_the_nearest_inch_half_away_from_zero(long units, long expected) =>
        Assert.Equal(Units(expected), SnapGrid.Snap(Units(units), 1));

    [Fact]
    public void A_point_snaps_each_coordinate_on_its_own()
    {
        Point2 snapped = SnapGrid.Snap(new Point2(Units(1536), Units(-1535)), 1);

        Assert.Equal(new Point2(Length.Inches(2), Length.Inches(-1)), snapped);
    }
}

/// <summary>Every box feature a relationship names, for the 3D view's markers.</summary>
public class RelationshipFeatureTests
{
    static readonly EntityId A = EntityId.New();
    static readonly EntityId B = EntityId.New();
    static readonly FeatureRef AEast = new(A, BoxFeature.Face(BoxFace.East));
    static readonly FeatureRef BWest = new(B, BoxFeature.Face(BoxFace.West));
    static readonly FeatureRef AEdge = new(A, BoxFeature.Edge(BoxFace.North, BoxFace.Top));
    static readonly NodeRef Node = new(EntityId.New());
    static readonly SegmentRef Edge = new(EntityId.New());

    public static TheoryData<Relationship, FeatureRef[]> Cases => new()
    {
        { new Coincident(RelationshipId.New(), AEast, BWest), [AEast, BWest] },
        { new Flush(RelationshipId.New(), AEast, BWest), [AEast, BWest] },
        { new AxisDistance(RelationshipId.New(), AEast, BWest, Axis.X, Length.Inches(1)), [AEast, BWest] },
        { new Centered(RelationshipId.New(), new CenterRef(A), AEast, BWest, Axis.X), [AEast, BWest] },
        { new Horizontal(RelationshipId.New(), AEdge), [AEdge] },
        { new Vertical(RelationshipId.New(), AEdge), [AEdge] },
        { new Distance(RelationshipId.New(), AEast, Node, Length.Inches(1)), [AEast] },
        { new PointOnEdge(RelationshipId.New(), Node, AEdge), [AEdge] },
        { new Symmetric(RelationshipId.New(), AEast, BWest, Edge), [AEast, BWest] },
        { new Napkin.Core.Geometry.Parallel(RelationshipId.New(), AEdge, Edge), [AEdge] },
        { new Perpendicular(RelationshipId.New(), Edge, AEdge), [AEdge] },
        { new AngleBetween(RelationshipId.New(), AEdge, Edge, Angle.Degrees(30)), [AEdge] },
        { new Tangent(RelationshipId.New(), AEdge, Edge), [AEdge] },
        { new Anchored(RelationshipId.New(), A), [] },
        { new EqualParam(RelationshipId.New(), new BoxWidthRef(A), new BoxWidthRef(B)), [] },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Only_box_features_are_listed_in_the_order_the_relationship_names_them(Relationship relationship, FeatureRef[] expected) =>
        Assert.Equal(expected, RelationshipSites.FeaturesOf(relationship));

    [Fact]
    public void A_relationship_is_needed()
    {
        Assert.Throws<ArgumentNullException>(() => RelationshipSites.FeaturesOf(null!).ToList());
    }
}
