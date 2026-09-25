using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What a dimension says, and where its graphics go.
/// </summary>
/// <remarks>
/// <para>
/// A dimension never stores a length of its own (docs/design/geometry-model.md &#xA7;3.3), so every
/// test here changes geometry and expects the text to follow. The formatting itself belongs to
/// <c>Core.Geometry</c> and is tested there; what these tests hold down is that the canvas asks it
/// the right question about the right two points.
/// </para>
/// <para>
/// The strings come from each fixture's <c>*.expected.json</c> rather than from this file. They
/// used to be typed in here, because the viewer built its samples in code while the sample files
/// were being written (#37); one set of hand-derived numbers, asserted by the reader's tests and
/// the viewer's alike, is the point of that file existing.
/// </para>
/// </remarks>
public class DimensionLabelTests
{
    static readonly LengthFormat AtSixteenths = new FeetInchesFormat(16);

    public static TheoryData<string, string> EveryDimension
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (string fixture in SampleExpectations.Fixtures)
            {
                foreach (ExpectedLabel label in SampleExpectations.For(fixture).DimensionLabels)
                {
                    data.Add(fixture, label.Name);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryDimension))]
    [Trait("Feature", "CVS-004")]
    public void A_sample_reads_the_numbers_its_expectations_state(string fixture, string dimension)
    {
        ExpectedLabel expected = SampleExpectations.For(fixture).Label(dimension);
        DimensionMeasurement measured = Measure(Load(fixture), expected.EntityId);

        Assert.Equal(expected.ValueUnits, measured.Value.Units);
        Assert.Equal(expected.Text, measured.Label(AtSixteenths));
        Assert.True(measured.Format(AtSixteenths).IsExact, $"{dimension} does not display exactly.");
        Assert.Equal(expected.Driving, measured.Dimension.Drives is not null);
    }

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_label_follows_the_geometry_it_measures()
    {
        Design design = Load("wall-with-window");
        SampleExpectations expected = SampleExpectations.For("wall-with-window");
        ExpectedBox wallBox = expected.Box("Wall");
        ExpectedLabel wallLength = expected.Label("Wall length");
        Box wall = design.Sketch.Find<Box>(wallBox.EntityId)!;

        Assert.Equal(wallLength.Text, Measure(design, wallLength.EntityId).Label(AtSixteenths));

        // The same dimension, over a wall a foot and a half longer. Nothing about the dimension
        // entity changed; the only thing that changed is what it measures.
        Design after = design with
        {
            Sketch = design.Sketch.WithEntity(wall with { Width = Length.FeetInches(13, 6) }),
        };

        Assert.Equal("13'-6\"", Measure(after, wallLength.EntityId).Label(AtSixteenths));
    }

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_distance_label_follows_the_part_it_measures_from()
    {
        Design design = Load("wall-with-window");
        SampleExpectations expected = SampleExpectations.For("wall-with-window");
        ExpectedLabel toOpening = expected.Label("Wall west end to opening");
        Box opening = design.Sketch.Find<Box>(expected.Box("Opening").EntityId)!;

        Assert.Equal(toOpening.Text, Measure(design, toOpening.EntityId).Label(AtSixteenths));

        Design moved = design with
        {
            Sketch = design.Sketch.WithEntity(
                opening with { Anchor = opening.Anchor with { X = Length.Feet(6) } }),
        };

        Assert.Equal("6'-0\"", Measure(moved, toOpening.EntityId).Label(AtSixteenths));
    }

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_value_that_is_not_on_the_displayed_grid_is_marked_as_approximate()
    {
        // 1/32" cannot be shown at 1/16", so the text is not the stored value and says so
        // (docs/design/geometry-model.md §1.4). No fixture on disk carries a number like this, and
        // none should: it is a property of the display, not of a design anyone would build.
        DesignBuilder builder = new("Off grid");
        LayerId layer = builder.AddLayer(DesignLayers.Parts);
        EntityId part = builder.AddBox(
            "Part", "Part", layer, Point2.Origin, Length.Inches(3, 1, 32), Length.Inches(2));
        EntityId width = builder.AddSizeDimension(
            "Width", layer, new BoxWidthRef(part), DimensionSide.South, Length.Inches(2));
        Design design = builder.Build();

        DimensionMeasurement measurement = Measure(design, width);

        Assert.False(measurement.Format(AtSixteenths).IsExact);
        Assert.Equal("≈3 1/16\"", measurement.Label(AtSixteenths));
    }

    [Fact]
    public void A_dimension_line_sits_off_the_side_its_placement_names()
    {
        Design design = Load("coffee-table");
        SampleExpectations expected = SampleExpectations.For("coffee-table");

        DimensionMeasurement below = Measure(design, expected.Label("Top width").EntityId);
        DimensionMeasurement left = Measure(design, expected.Label("Top depth").EntityId);

        // South of a top whose bottom edge is y = 0, by the 4" the placement asks for.
        Assert.Equal(Length.Inches(-4), below.LineFrom.Y);
        Assert.Equal(Length.Inches(-4), below.LineTo.Y);
        Assert.Equal(Axis.X, below.Axis);

        // West of a top whose left edge is x = 0.
        Assert.Equal(Length.Inches(-4), left.LineFrom.X);
        Assert.Equal(Length.Inches(-4), left.LineTo.X);
        Assert.Equal(Axis.Y, left.Axis);
    }

    [Fact]
    public void A_dimension_spans_exactly_what_it_measures()
    {
        Design design = Load("wall-with-window");
        SampleExpectations expected = SampleExpectations.For("wall-with-window");
        ExpectedBox opening = expected.Box("Opening");

        DimensionMeasurement measured = Measure(design, expected.Label("Opening width").EntityId);

        Assert.Equal(new Length(opening.AnchorXUnits), measured.From.X);
        Assert.Equal(new Length(opening.AnchorXUnits + opening.WidthUnits), measured.To.X);
        Assert.Equal(measured.From.X, measured.LineFrom.X);
        Assert.Equal(measured.To.X, measured.LineTo.X);

        // North of a wall 5 1/2" thick, by the 1/2" the placement asks for.
        Assert.Equal(Length.Inches(6), measured.LineFrom.Y);
    }

    [Fact]
    public void A_dimension_whose_measurand_is_gone_is_skipped_rather_than_thrown_over()
    {
        Design design = Load("coffee-table");
        SampleExpectations expected = SampleExpectations.For("coffee-table");
        ExpectedLabel topWidth = expected.Label("Top width");
        ExpectedLabel legWidth = expected.Label("Leg width");

        Sketch withoutTop = design.Sketch.WithoutEntity(expected.Box("Top").EntityId);

        List<DimensionMeasurement> measured = [.. DimensionLayout.Measure(withoutTop)];

        Assert.DoesNotContain(measured, m => m.Dimension.Id == topWidth.EntityId);
        Assert.Contains(measured, m => m.Dimension.Id == legWidth.EntityId);
    }

    static Design Load(string fixture) => SampleExpectations.Sample(fixture).Load();

    static DimensionMeasurement Measure(Design design, EntityId id)
    {
        Dimension dimension = design.Sketch.Find<Dimension>(id)
            ?? throw new InvalidOperationException($"{design.Name} has no dimension {id}.");

        Assert.True(
            DimensionLayout.TryMeasure(design.Sketch, dimension, out DimensionMeasurement? measurement),
            $"{id} could not be measured.");
        return measurement!;
    }
}
