using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What a dimension says, and where its graphics go.
/// </summary>
/// <remarks>
/// A dimension never stores a length of its own (docs/design/geometry-model.md &#xA7;3.3), so every
/// test here changes geometry and expects the text to follow. The formatting itself belongs to
/// <c>Core.Geometry</c> and is tested there; what these tests hold down is that the canvas asks it
/// the right question about the right two points.
/// </remarks>
public class DimensionLabelTests
{
    static readonly LengthFormat AtSixteenths = new FeetInchesFormat(16);

    [Theory]
    [InlineData("Overall width", "4'-0\"")]
    [InlineData("Overall depth", "1'-8\"")]
    [InlineData("Leg inset", "1\"")]
    [InlineData("Leg size", "2 1/2\"")]
    [InlineData("Apron length", "3'-5\"")]
    [Trait("Feature", "CVS-004")]
    public void The_coffee_table_reads_the_numbers_it_was_built_from(string dimension, string expected) =>
        Assert.Equal(expected, LabelOf(BuiltInDesigns.CoffeeTable(), dimension));

    [Theory]
    [InlineData("Wall length", "12'-0\"")]
    [InlineData("Opening width", "3'-0\"")]
    [InlineData("To opening", "4'-2 1/2\"")]
    [InlineData("Past opening", "4'-9 1/2\"")]
    [InlineData("Wall thickness", "3 1/2\"")]
    [Trait("Feature", "CVS-004")]
    public void The_wall_reads_the_numbers_it_was_built_from(string dimension, string expected) =>
        Assert.Equal(expected, LabelOf(BuiltInDesigns.WallWithWindow(), dimension));

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_label_follows_the_geometry_it_measures()
    {
        Design design = BuiltInDesigns.WallWithWindow();
        EntityId wallId = DesignBuilder.IdFor(design.Name, "Wall");
        Box wall = design.Sketch.Find<Box>(wallId)!;

        Assert.Equal("12'-0\"", LabelOf(design, "Wall length"));

        // The same dimension, over a wall a foot and a half longer. Nothing about the dimension
        // entity changed; the only thing that changed is what it measures.
        Sketch widened = design.Sketch.WithEntity(wall with { Width = Length.FeetInches(13, 6) });
        Design after = design with { Sketch = widened };

        Assert.Equal("13'-6\"", LabelOf(after, "Wall length"));
    }

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_distance_label_follows_the_part_it_measures_from()
    {
        Design design = BuiltInDesigns.WallWithWindow();
        EntityId windowId = DesignBuilder.IdFor(design.Name, "Window");
        Box window = design.Sketch.Find<Box>(windowId)!;

        Assert.Equal("4'-2 1/2\"", LabelOf(design, "To opening"));

        Sketch moved = design.Sketch.WithEntity(
            window with { Anchor = new Point2(Length.Feet(6), window.Anchor.Y) });

        Assert.Equal("6'-0\"", LabelOf(design with { Sketch = moved }, "To opening"));
    }

    [Fact]
    [Trait("Feature", "CVS-004")]
    public void A_value_that_is_not_on_the_displayed_grid_is_marked_as_approximate()
    {
        // 1/32" cannot be shown at 1/16", so the text is not the stored value and says so
        // (docs/design/geometry-model.md §1.4).
        DesignBuilder builder = new("Off grid");
        LayerId layer = builder.AddLayer(DesignLayers.Parts);
        EntityId part = builder.AddBox(
            "Part", "Part", layer, Point2.Origin, Length.Inches(3, 1, 32), Length.Inches(2));
        builder.AddSizeDimension(
            "Width", layer, new BoxWidthRef(part), DimensionSide.South, Length.Inches(2));
        Design design = builder.Build();

        DimensionMeasurement measurement = Single(design, "Width");

        Assert.False(measurement.Format(AtSixteenths).IsExact);
        Assert.Equal("≈3 1/16\"", measurement.Label(AtSixteenths));
    }

    [Fact]
    public void A_dimension_line_sits_off_the_side_its_placement_names()
    {
        Design design = BuiltInDesigns.CoffeeTable();

        DimensionMeasurement below = Single(design, "Overall width");
        DimensionMeasurement left = Single(design, "Overall depth");

        // South of a top whose bottom edge is y = 0, by the 8" the placement asks for.
        Assert.Equal(Length.Inches(-8), below.LineFrom.Y);
        Assert.Equal(Length.Inches(-8), below.LineTo.Y);
        Assert.Equal(Axis.X, below.Axis);

        // West of a top whose left edge is x = 0.
        Assert.Equal(Length.Inches(-8), left.LineFrom.X);
        Assert.Equal(Length.Inches(-8), left.LineTo.X);
        Assert.Equal(Axis.Y, left.Axis);
    }

    [Fact]
    public void A_dimension_spans_exactly_what_it_measures()
    {
        Design design = BuiltInDesigns.WallWithWindow();

        DimensionMeasurement opening = Single(design, "Opening width");

        Assert.Equal(Length.FeetInches(4, 2, 1, 2), opening.From.X);
        Assert.Equal(Length.FeetInches(7, 2, 1, 2), opening.To.X);
        Assert.Equal(opening.From.X, opening.LineFrom.X);
        Assert.Equal(opening.To.X, opening.LineTo.X);

        // North of a wall 3 1/2" thick, by the 8" the placement asks for.
        Assert.Equal(Length.Inches(11, 1, 2), opening.LineFrom.Y);
    }

    [Fact]
    public void A_dimension_whose_measurand_is_gone_is_skipped_rather_than_thrown_over()
    {
        Design design = BuiltInDesigns.CoffeeTable();
        Sketch withoutTop = design.Sketch.WithoutEntity(DesignBuilder.IdFor(design.Name, "Top"));

        List<DimensionMeasurement> measured = [.. DimensionLayout.Measure(withoutTop)];

        Assert.DoesNotContain(measured, m => m.Value == Length.Inches(48));
        Assert.Contains(measured, m => m.Value == Length.Inches(2, 1, 2));
    }

    static string LabelOf(Design design, string key) => Single(design, key).Label(AtSixteenths);

    static DimensionMeasurement Single(Design design, string key)
    {
        EntityId id = DesignBuilder.IdFor(design.Name, key);
        Dimension dimension = design.Sketch.Find<Dimension>(id)
            ?? throw new InvalidOperationException($"{design.Name} has no dimension called {key}.");

        Assert.True(
            DimensionLayout.TryMeasure(design.Sketch, dimension, out DimensionMeasurement? measurement),
            $"{key} could not be measured.");
        return measurement!;
    }
}
