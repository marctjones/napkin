using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The site plan's distances (docs/design/permit-set.md §5.3, #223), worked by hand on a 100' × 50' lot
/// with a 40' house wall 30' in from the west line and 25' in from the front (south) line.
/// </summary>
public class SitePlanTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId SiteLayer = LayerId.New();

    static Course C(NorthSouth from, long degrees, EastWest toward, long feet, Setback? setback = null)
        => new(new Bearing(from, Angle.Degrees(degrees), toward), Length.Feet(feet), setback);

    static ImmutableArray<Course> Lot() =>
    [
        C(NorthSouth.North, 0, EastWest.East, 50, new Setback(Length.Feet(10), SetbackKind.Side)),
        C(NorthSouth.North, 90, EastWest.East, 100, new Setback(Length.Feet(30), SetbackKind.Rear)),
        C(NorthSouth.South, 0, EastWest.East, 50),
        C(NorthSouth.North, 90, EastWest.West, 100, new Setback(Length.Feet(25), SetbackKind.Front)),
    ];

    static Box House(Phase phase = Phase.Existing) =>
        new(EntityId.New(), WallLayer, new Point3(Length.Inches(360), Length.Inches(300), Length.Zero), Length.Inches(480), Length.Inches(5, 1, 2), Length.Inches(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = phase,
        };

    static Sketch Sketch(ImmutableArray<Course> courses, params Box[] boxes) => boxes.Aggregate(
        Napkin.Core.Geometry.Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(SiteLayer, BuildingLayers.Site))
            .WithEntity(new Boundary(EntityId.New(), SiteLayer, Point2.Origin, courses) { Name = "Lot", Phase = Phase.Existing }),
        (with, box) => with.WithEntity(box));

    [Fact]
    [Trait("Feature", "SITE-002")]
    public void Each_line_says_the_structures_distance_against_its_setback()
    {
        SitePlanMeasure measure = SitePlan.Of(Sketch(Lot(), House()))!;
        Assert.Equal(Length.Zero, measure.Closure);
        Assert.Equal("The courses close on the point of beginning.", measure.ClosureText);
        Assert.Equal(
            [
                "House to side line 30'-0\" (setback 10'-0\": clear)",
                "House to rear line 24'-6 1/2\" (setback 30'-0\": short by 5'-5 1/2\")",
                "House to line 3 30'-0\"",
                "House to front line 25'-0\" (setback 25'-0\": clear)",
            ],
            measure.Lines.Select(line => line.Text));
        Assert.Equal([true, false, null, true], measure.Lines.Select(line => line.Clear));
        Assert.All(measure.Lines, line => Assert.True(line.Exact));
    }

    [Fact]
    [Trait("Feature", "SITE-003")]
    public void Turned_north_makes_every_distance_approximate()
    {
        Sketch turned = Sketch(Lot(), House()) with { Site = SiteValues.NotEntered with { North = Angle.Degrees(1) } };
        SitePlanMeasure measure = SitePlan.Of(turned)!;
        Assert.All(measure.Lines, line => Assert.False(line.Exact));
        Assert.All(measure.Lines, line => Assert.Contains(" ≈ ", line.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_structure_or_only_a_demolished_one_there_is_nothing_to_measure_from()
    {
        SitePlanMeasure empty = SitePlan.Of(Sketch(Lot()))!;
        Assert.Equal("Side line: no wall, deck or roof is drawn to measure from.", empty.Lines[0].Text);
        Assert.Null(empty.Lines[0].Clear);
        Assert.Null(SitePlan.Of(Sketch(Lot(), House(Phase.Demolish)))!.Lines[0].Distance);
        Assert.Null(SitePlan.Of(Napkin.Core.Geometry.Sketch.Empty));
        Assert.Equal("The structure to side line 30'-0\" (setback 10'-0\": clear)", SitePlan.Of(Sketch(Lot(), House() with { Name = string.Empty }))!.Lines[0].Text);
    }

    [Fact]
    [Trait("Feature", "SITE-004")]
    public void A_traverse_that_does_not_close_says_so_and_is_not_adjusted()
    {
        ImmutableArray<Course> open = [C(NorthSouth.North, 0, EastWest.East, 100), C(NorthSouth.North, 90, EastWest.East, 100), C(NorthSouth.South, 45, EastWest.West, 100)];
        SitePlanMeasure measure = SitePlan.Of(Sketch(open, House()))!;
        Assert.StartsWith("The courses do not close: the last corner is 41'-5 1/16\" from the point of beginning.", measure.ClosureText, StringComparison.Ordinal);
        Assert.Contains("never adjusts it", measure.ClosureText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(NorthSouth.North, 12, 34, 56, EastWest.East, "N 12°34'56\" E")]
    [InlineData(NorthSouth.South, 0, 0, 5, EastWest.West, "S 0°00'05\" W")]
    public void A_bearing_reads_as_a_survey_prints_it(NorthSouth from, long d, long m, long s, EastWest toward, string words)
        => Assert.Equal(words, SitePlan.Words(new Bearing(from, Angle.Degrees(d, m, s), toward)));
}
