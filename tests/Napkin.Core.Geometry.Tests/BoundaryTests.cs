using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// A lot's boundary from its survey courses (docs/design/permit-set.md §5.2, §5.5, #223): corners
/// derived from the point of beginning, rounded once each; the closure error reported, never adjusted.
/// </summary>
public class BoundaryTests
{
    static readonly IGeometryUpdater Updater = DirectUpdater.Instance;
    static readonly LayerId Site = LayerId.New();

    static Course C(NorthSouth from, long degrees, EastWest toward, long feet, Setback? setback = null)
        => new(new Bearing(from, Angle.Degrees(degrees), toward), Length.Feet(feet), setback);

    /// <summary>A 100' × 50' lot from the origin: north 50', east 100', south 50', west 100'.</summary>
    static ImmutableArray<Course> Rectangle() =>
    [
        C(NorthSouth.North, 0, EastWest.East, 50, new Setback(Length.Feet(10), SetbackKind.Side)),
        C(NorthSouth.North, 90, EastWest.East, 100, new Setback(Length.Feet(30), SetbackKind.Rear)),
        C(NorthSouth.South, 0, EastWest.East, 50),
        C(NorthSouth.North, 90, EastWest.West, 100, new Setback(Length.Feet(25), SetbackKind.Front)),
    ];

    [Fact]
    public void A_bearings_azimuth_is_measured_clockwise_from_north_by_quadrant()
    {
        Angle a = Angle.Degrees(45);
        Assert.Equal(
            [Angle.Degrees(45), Angle.Degrees(135), Angle.Degrees(225), Angle.Degrees(315)],
            new[] { new Bearing(NorthSouth.North, a, EastWest.East), new Bearing(NorthSouth.South, a, EastWest.East), new Bearing(NorthSouth.South, a, EastWest.West), new Bearing(NorthSouth.North, a, EastWest.West) }.Select(b => b.Azimuth));
    }

    [Fact]
    [Trait("Feature", "SITE-004")]
    public void A_rectangular_lot_closes_exactly_on_the_grid()
    {
        Boundary lot = new(EntityId.New(), Site, Point2.Origin, Rectangle());
        Assert.Equal(
            [Point2.Origin, new Point2(Length.Zero, Length.Feet(50)), new Point2(Length.Feet(100), Length.Feet(50)), new Point2(Length.Feet(100), Length.Zero), Point2.Origin],
            lot.Corners(Angle.Zero));
        Assert.Equal(Length.Zero, lot.ClosureError(Angle.Zero));

        // North turned a quarter clockwise: "north" now points along +X, so the first course runs east.
        Assert.Equal(new Point2(Length.Feet(50), Length.Zero), lot.Corners(Angle.Right)[1]);
    }

    [Fact]
    [Trait("Feature", "SITE-004")]
    public void An_open_traverse_reports_its_closure_error_and_is_not_adjusted()
    {
        // 100' north, 100' east, 100' south-west: the end is 100' − 100' sin 45° from the start each way.
        Boundary open = new(EntityId.New(), Site, Point2.Origin, [C(NorthSouth.North, 0, EastWest.East, 100), C(NorthSouth.North, 90, EastWest.East, 100), C(NorthSouth.South, 45, EastWest.West, 100)]);
        Assert.Equal(new Point2(new Length(359907), new Length(359907)), open.Corners(Angle.Zero)[^1]);
        Assert.Equal(new Length(508985), open.ClosureError(Angle.Zero));
    }

    [Fact]
    public void Bad_courses_are_refused_by_the_rules_the_updater_and_validation()
    {
        ImmutableArray<Course> good = Rectangle();
        Assert.Null(BoundaryRules.Refusal(good));
        Assert.Equal("a lot's boundary has at least three courses", BoundaryRules.Refusal([.. good.Take(2)]));
        Assert.Equal("every course is longer than zero", BoundaryRules.Refusal(good.SetItem(0, good[0] with { Distance = Length.Zero })));
        Assert.Equal("a bearing's angle is from 0° to 90°", BoundaryRules.Refusal(good.SetItem(0, good[0] with { Bearing = good[0].Bearing with { Angle = Angle.Degrees(91) } })));
        Assert.Equal("a setback is longer than zero", BoundaryRules.Refusal(good.SetItem(0, good[0] with { Setback = new Setback(Length.Zero, SetbackKind.Side) })));

        SketchBuilder builder = new();
        Boundary lot = new(EntityId.New(), LayerId.Default, Point2.Origin, good);
        Sketch sketch = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new AddEntity(lot))).Sketch;
        Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new AddEntity(lot with { Courses = [.. good.Take(2)] })));

        Solved moved = Assert.IsType<Solved>(Updater.Apply(sketch, new SetBoundary(lot.Id, Point2.Inches(12, 0), good)));
        Assert.Equal(Point2.Inches(12, 0), moved.Sketch.Find<Boundary>(lot.Id)!.Start);
        Assert.IsType<Rejected>(Updater.Apply(sketch, new SetBoundary(lot.Id, Point2.Origin, [.. good.Take(2)])));
        Assert.Equal(RejectionReason.UnknownEntity, Assert.IsType<Rejected>(Updater.Apply(sketch, new SetBoundary(EntityId.New(), Point2.Origin, good))).Reason);
        EntityId node = builder.AddNode(0, 0);
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetBoundary(node, Point2.Origin, good))).Reason);

        // A sketch that somehow holds a bad boundary says so when validated.
        Sketch bad = builder.Sketch.WithEntity(lot with { Courses = [.. good.Take(2)] });
        Assert.Contains(bad.Validate().Errors, error => error.Message.Contains("at least three courses", StringComparison.Ordinal));
    }

    [Fact]
    public void A_boundary_is_equal_by_value_and_moves_layer()
    {
        Boundary a = new(EntityId.New(), Site, Point2.Origin, Rectangle());
        Boundary b = a with { Courses = [.. Rectangle()] };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { Start = Point2.Inches(1, 0) });
        LayerId other = LayerId.New();
        Assert.Equal(other, a.OnLayer(other).Layer);
        Assert.Equal(Angle.Zero, SiteValues.NotEntered.North);
    }
}
