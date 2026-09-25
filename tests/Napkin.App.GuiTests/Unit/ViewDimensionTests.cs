using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Dimensions per standard view (docs/design/standard-views.md §3, §7.2), on the coffee table, whose
/// six dimensions the note tables by hand: all six in Top and Bottom, the four along X in Front and
/// Back, the two along Y in Left and Right. Placements are worked from coffee-table.expected.json's
/// derivations: the top is z 16¼–17, a short apron y 4–20 and z 12¾–16¼, a leg z 0–16¼.
/// </summary>
public class ViewDimensionTests
{
    const double Tolerance = 1e-9;
    static readonly LengthFormat AtSixteenths = new FeetInchesFormat(16);

    static Sketch Table() => SampleExpectations.Sample("coffee-table").Load().Sketch;

    /// <summary>The name coffee-table.expected.json gives a dimension.</summary>
    static string NameOf(ViewDimension dimension) =>
        SampleExpectations.For("coffee-table").DimensionLabels.Single(label => label.EntityId == dimension.Measurement.Dimension.Id).Name;

    static ViewDimension Named(Sketch sketch, StandardView view, string name) =>
        Assert.Single(DimensionLayout.Measure(sketch, view), dimension => NameOf(dimension) == name);

    static readonly string[] AlongX = ["Leg inset from the top's west edge", "Leg width", "Long apron length", "Top width"];
    static readonly string[] AlongY = ["Short apron length", "Top depth"];

    public static TheoryData<StandardView, string[]> Shown => new()
    {
        { StandardView.Top, [.. AlongX, .. AlongY] },
        { StandardView.Bottom, [.. AlongX, .. AlongY] },
        { StandardView.Front, AlongX },
        { StandardView.Back, AlongX },
        { StandardView.Left, AlongY },
        { StandardView.Right, AlongY },
    };

    [Theory]
    [Trait("Feature", "VIEW-010")]
    [MemberData(nameof(Shown))]
    public void Each_view_shows_the_dimensions_along_its_two_screen_axes(StandardView view, string[] names)
    {
        Sketch sketch = Table();
        Assert.Equal(names.Order(), DimensionLayout.Measure(sketch, view).Select(dimension => NameOf(dimension)).Order());
    }

    [Theory]
    [Trait("Feature", "VIEW-010")]
    [InlineData(StandardView.Bottom)]
    [InlineData(StandardView.Front)]
    [InlineData(StandardView.Back)]
    [InlineData(StandardView.Left)]
    [InlineData(StandardView.Right)]
    public void Every_label_reads_what_it_reads_in_Top(StandardView view)
    {
        Sketch sketch = Table();
        Dictionary<string, string> top = DimensionLayout.Measure(sketch, StandardView.Top).ToDictionary(d => NameOf(d), d => d.Label(AtSixteenths));
        Assert.All(DimensionLayout.Measure(sketch, view), dimension => Assert.Equal(top[NameOf(dimension)], dimension.Label(AtSixteenths)));
    }

    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void Top_is_the_plans_own_layout_x_along_and_y_across()
    {
        Sketch sketch = Table();
        foreach (ViewDimension dimension in DimensionLayout.Measure(sketch, StandardView.Top))
        {
            DimensionMeasurement plan = dimension.Measurement;
            Assert.Equal(plan.LineFrom.X.ToInches(), dimension.LineFrom.Along, Tolerance);
            Assert.Equal(plan.LineFrom.Y.ToInches(), dimension.LineFrom.Across, Tolerance);
            Assert.Equal(plan.To.X.ToInches(), dimension.To.Along, Tolerance);
            Assert.Equal(plan.To.Y.ToInches(), dimension.To.Across, Tolerance);
        }
    }

    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void In_Front_the_top_width_placed_south_4_inches_lies_4_inches_below_the_top()
    {
        // The top is z 16¼–17; South is the low side; so the line is at 16¼ − 4 = 12¼, and its
        // extension lines start at the top's underside, 16¼. Along is x: 0 to 48.
        ViewDimension width = Named(Table(), StandardView.Front, "Top width");
        Assert.Equal((12.25, 12.25), (width.LineFrom.Across, width.LineTo.Across));
        Assert.Equal((16.25, 16.25), (width.From.Across, width.To.Across));
        Assert.Equal(48, Math.Abs(width.LineTo.Along - width.LineFrom.Along), Tolerance);
        Assert.Equal(0, Math.Min(width.LineFrom.Along, width.LineTo.Along), Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void In_Back_the_same_line_runs_right_to_left_at_the_same_height()
    {
        // Screen right is west: along is −x, so 0 to −48, and the height is Front's.
        ViewDimension width = Named(Table(), StandardView.Back, "Top width");
        Assert.Equal(12.25, width.LineFrom.Across, Tolerance);
        Assert.Equal(-48, Math.Min(width.LineFrom.Along, width.LineTo.Along), Tolerance);
        Assert.Equal(0, Math.Max(width.LineFrom.Along, width.LineTo.Along), Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void In_Bottom_south_is_up_so_the_south_dimension_is_on_the_screen_up_side_of_the_top()
    {
        // Screen up is −y. The top spans y 0–24, across −24 to 0; its South line is at y −4, across +4.
        ViewDimension width = Named(Table(), StandardView.Bottom, "Top width");
        Assert.Equal(4, width.LineFrom.Across, Tolerance);
        Assert.True(width.LineFrom.Across > 0, "the south dimension is not above the top on screen");
    }

    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void An_inset_placed_north_lies_above_both_places_and_a_short_apron_west_lies_below_it()
    {
        Sketch sketch = Table();

        // From the top's upright south-west edge (no z of its own: the top's 16¼–17) to the leg's
        // (the leg's 0–16¼), North by 1": above the higher, 17 + 1 = 18.
        ViewDimension inset = Named(sketch, StandardView.Front, "Leg inset from the top's west edge");
        Assert.Equal(18, inset.LineFrom.Across, Tolerance);
        Assert.Equal(1.5, Math.Abs(inset.LineTo.Along - inset.LineFrom.Along), Tolerance);

        // The west short apron, y 4–20 and z 12¾–16¼, West by 2": below it, 10¾. In Left screen right
        // is south (−y), so along runs −4 to −20; in Right it is +y, 4 to 20.
        ViewDimension left = Named(sketch, StandardView.Left, "Short apron length");
        Assert.Equal(10.75, left.LineFrom.Across, Tolerance);
        Assert.Equal((-20.0, -4.0), (Math.Min(left.LineFrom.Along, left.LineTo.Along), Math.Max(left.LineFrom.Along, left.LineTo.Along)));
        ViewDimension right = Named(sketch, StandardView.Right, "Short apron length");
        Assert.Equal((4.0, 20.0), (Math.Min(right.LineFrom.Along, right.LineTo.Along), Math.Max(right.LineFrom.Along, right.LineTo.Along)));

        // And in the world a view dimension's point is where the camera projects it.
        Assert.Equal(new Vector3d(0, 4, 10.75), right.InWorld(new ViewPoint(4, 10.75)));
        Assert.Equal(new Vector3d(0, 4, 10.75), left.InWorld(new ViewPoint(-4, 10.75)));
    }
    [Fact]
    [Trait("Feature", "VIEW-010")]
    public void A_distance_between_two_centres_is_placed_from_the_centres_heights()
    {
        // Measure the inset between the top's centre (z 16⅝) and the south-west leg's (z 8⅛) instead,
        // South by 1": below the lower centre, 7⅛ — not below the leg's foot, since a centre fixes its z.
        Sketch sketch = Table();
        EntityId inset = SampleExpectations.For("coffee-table").Label("Leg inset from the top's west edge").EntityId;
        EntityId top = SampleExpectations.For("coffee-table").Box("Top").EntityId;
        EntityId leg = SampleExpectations.For("coffee-table").Box("Leg, south-west").EntityId;
        Dimension was = sketch.Find<Dimension>(inset)!;
        sketch = sketch.WithEntity(was with
        {
            Measures = new AxisMeasurand(new CenterRef(top), new CenterRef(leg), Axis.X),
            Placement = was.Placement with { Side = DimensionSide.South },
        });

        ViewDimension centres = Named(sketch, StandardView.Front, "Leg inset from the top's west edge");
        Assert.Equal(7.125, centres.LineFrom.Across, Tolerance);
        Assert.Equal(8.125, centres.From.Across, Tolerance);
        Assert.Equal(24 - 2.75, Math.Abs(centres.LineTo.Along - centres.LineFrom.Along), Tolerance);
    }
}
