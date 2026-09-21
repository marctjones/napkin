using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The samples are drawings, not test data: they have to be sketches the kernel itself would
/// accept.
/// </summary>
/// <remarks>
/// When the reader (#6) and the sample files (#37) land, these same assertions are what a file has
/// to satisfy to be opened at all — a sketch that passes <see cref="Sketch.Validate"/> and whose
/// stored geometry satisfies its stored relationships. Holding the built-in samples to it now means
/// the viewer is never shown something the reader would have refused.
/// </remarks>
public class BuiltInDesignTests
{
    public static TheoryData<string> Samples =>
        [.. BuiltInDesigns.All.Select(source => source.Name)];

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_sample_is_a_valid_sketch(string name)
    {
        Design design = Load(name);

        ValidationResult validation = design.Sketch.Validate();

        Assert.True(validation.IsValid, validation.ToString());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_sample_satisfies_its_own_relationships(string name)
    {
        Design design = Load(name);

        CheckReport report = RelationshipChecker.Check(design.Sketch);

        Assert.True(
            report.Violations.IsEmpty,
            string.Join(", ", report.Violations.Select(v => $"{v.Relationship} off by {v.Residual}")));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_sample_is_the_same_drawing_every_time_it_is_opened(string name)
    {
        // Ids are derived from the part names, not generated, so two loads are the same value.
        // Without that, "nothing moved while I panned" could not be asserted against a fresh load.
        Design first = Load(name);
        Design second = Load(name);

        Assert.Equal(first.Sketch, second.Sketch);
        Assert.Equal(first.Labels, second.Labels);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void A_sample_has_parts_and_dimensions_to_look_at(string name)
    {
        Design design = Load(name);

        Assert.True(design.Sketch.Entities.Values.OfType<Box>().Count() >= 2);
        Assert.True(DimensionLayout.Measure(design.Sketch).Count() >= 3);
        Assert.False(SketchExtents.Of(design.Sketch).IsEmpty);
    }

    [Fact]
    public void Every_part_of_the_coffee_table_is_where_the_arithmetic_says()
    {
        Design design = BuiltInDesigns.CoffeeTable();

        Assert.Equal(9, design.Sketch.Entities.Values.OfType<Box>().Count());
        AssertBox(design, "Top", 0, 0, 48, 20);
        AssertBox(design, "Leg, front left", 1, 1, 2.5, 2.5);
        AssertBox(design, "Leg, back right", 44.5, 16.5, 2.5, 2.5);
        AssertBox(design, "Apron, front", 3.5, 1.75, 41, 0.75);
        AssertBox(design, "Apron, right", 45.5, 3.5, 0.75, 13);
    }

    [Fact]
    public void Every_part_of_the_wall_is_where_the_arithmetic_says()
    {
        Design design = BuiltInDesigns.WallWithWindow();

        Assert.Equal(6, design.Sketch.Entities.Values.OfType<Box>().Count());
        AssertBox(design, "Wall", 0, 0, 144, 3.5);
        AssertBox(design, "Window", 50.5, 0, 36, 3.5);
        AssertBox(design, "Jack, left", 49, 0, 1.5, 3.5);
        AssertBox(design, "King, right", 88, 0, 1.5, 3.5);
    }

    [Fact]
    public void The_extents_a_fit_frames_include_the_dimension_lines()
    {
        // The dimensions sit 8" off the top's south and west edges, so a fit that framed only the
        // parts would cut them off.
        WorldBounds extents = SketchExtents.Of(BuiltInDesigns.CoffeeTable().Sketch);

        Assert.Equal(Length.Inches(-8), extents.MinX);
        Assert.Equal(Length.Inches(-8), extents.MinY);
        Assert.Equal(Length.Inches(48), extents.MaxX);
        Assert.Equal(Length.Inches(20), extents.MaxY);
    }

    [Fact]
    public void Two_parts_cannot_share_a_key()
    {
        DesignBuilder builder = new("Clashing");
        LayerId layer = builder.AddLayer(DesignLayers.Parts);
        builder.AddBox("Leg", "Leg", layer, Point2.Origin, Length.Inches(2), Length.Inches(2));

        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            builder.AddBox("Leg", "Leg", layer, Point2.Inches(10, 0), Length.Inches(2), Length.Inches(2)));

        Assert.Contains("Leg", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_sample_says_what_it_is()
    {
        foreach (IDesignSource source in BuiltInDesigns.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(source.Name));
            Assert.False(string.IsNullOrWhiteSpace(source.Description));
            Assert.Equal(source.Name, source.Load().Name);
        }
    }

    static Design Load(string name) =>
        BuiltInDesigns.All.Single(source => source.Name == name).Load();

    static void AssertBox(Design design, string key, double x, double y, double width, double height)
    {
        Box box = design.Sketch.Find<Box>(DesignBuilder.IdFor(design.Name, key))
            ?? throw new InvalidOperationException($"{design.Name} has no part called {key}.");

        Assert.Equal(x, box.Anchor.X.ToInches());
        Assert.Equal(y, box.Anchor.Y.ToInches());
        Assert.Equal(width, box.Width.ToInches());
        Assert.Equal(height, box.Height.ToInches());
    }
}
