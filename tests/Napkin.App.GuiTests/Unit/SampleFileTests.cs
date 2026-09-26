using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The samples the application ships: that they are there at all, that they are the drawings the
/// committed expectations describe, and that they are sketches the kernel itself accepts.
/// </summary>
/// <remarks>
/// <para>
/// These replace the tests that held the viewer's <em>in-code</em> samples to the same bar. The
/// samples are files now — the very files <c>tests/Napkin.Core.Project.Tests</c> checks the reader
/// against — so there is one drawing per fixture rather than two hand-computed copies of it, and
/// what this suite adds on top of the reader's own tests is that the files <em>shipped</em> and
/// that the viewer can open them.
/// </para>
/// <para>
/// A failure of the first test here almost certainly means the <c>Content</c> item in
/// <c>src/Napkin.App/Napkin.App.csproj</c> did not put <c>samples/</c> in the build output, not
/// that a drawing is wrong.
/// </para>
/// </remarks>
public class SampleFileTests
{
    public static TheoryData<string> Fixtures => [.. SampleExpectations.Fixtures];

    [Fact]
    public void The_samples_ship_beside_the_application()
    {
        Assert.True(
            Directory.Exists(SampleFiles.SampleDirectory),
            $"No samples directory beside the application at {SampleFiles.SampleDirectory}. "
            + "The Content item in Napkin.App.csproj is what puts it there.");

        foreach (string fixture in SampleExpectations.Fixtures)
        {
            Assert.True(
                File.Exists(SampleExpectations.SceneFile(fixture)),
                $"{fixture}.scene.json did not ship beside the application.");
        }

        Assert.Equal(
            ["Coffee table", "Rounded-corner table", "Wall with window", "Bookcase", "Bench", "Lying beam", "Chain of five", "Fraction stress", "Scale extremes", "Framing at 16\" o.c.", "L-bracket", "Overlap", "Picture frame", "Stocked bench", "DIY coffee table with drawers", "Window in an existing wall", "Basement room", "Splayed bench", "Splayed footstool"],
            SampleFiles.All.Select(sample => sample.Name));
    }

    [Fact]
    public void Every_sample_says_what_it_is_and_names_its_file()
    {
        Assert.NotEmpty(SampleFiles.All);

        foreach (FileDesignSource sample in SampleFiles.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.Name));
            Assert.False(string.IsNullOrWhiteSpace(sample.Description));

            // The status line is built from the description, so the file on screen is always
            // named there even when the menu title is a friendly one.
            Assert.Contains(sample.FileName, sample.Description, StringComparison.Ordinal);
            Assert.Equal(sample.Name, sample.Load().Name);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_sample_is_a_valid_sketch(string fixture)
    {
        ValidationResult validation = Load(fixture).Sketch.Validate();

        Assert.True(validation.IsValid, validation.ToString());
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_sample_satisfies_its_own_relationships(string fixture)
    {
        CheckReport report = RelationshipChecker.Check(Load(fixture).Sketch);

        Assert.True(
            report.Violations.IsEmpty,
            string.Join(", ", report.Violations.Select(v => $"{v.Relationship} off by {v.Residual}")));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_sample_is_the_same_drawing_every_time_it_is_opened(string fixture)
    {
        // Ids come from the file rather than being generated, so two reads are the same value.
        // Without that, "nothing moved while I panned" could not be asserted against a fresh load.
        Design first = Load(fixture);
        Design second = Load(fixture);

        Assert.Equal(first.Sketch, second.Sketch);
        Assert.Equal(first.Name, second.Name);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_sample_carries_the_part_names_the_file_states(string fixture)
    {
        // Scene format version 2 put a name on every entity (#8), so a design read from a file has
        // something to draw on its parts and Design.Labels is filled from the file itself.
        Design design = Load(fixture);

        Assert.NotEmpty(design.Labels);
        Assert.All(design.Labels.Values, label => Assert.NotEmpty(label));
        Assert.Equal(design.Sketch.Entities.Count, design.Labels.Count);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void A_sample_has_parts_and_dimensions_to_look_at(string fixture)
    {
        Design design = Load(fixture);

        Assert.True(design.Sketch.Entities.Values.OfType<Box>().Count() >= 2);
        Assert.True(DimensionLayout.Measure(design.Sketch).Count() >= 3);
        Assert.False(SketchExtents.Of(design.Sketch).IsEmpty);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Every_part_is_where_the_committed_arithmetic_says(string fixture)
    {
        Design design = Load(fixture);
        SampleExpectations expected = SampleExpectations.For(fixture);

        Assert.Equal(expected.Counts.Boxes, design.Sketch.Entities.Values.OfType<Box>().Count());
        Assert.Equal(
            expected.Counts.Dimensions,
            design.Sketch.Entities.Values.OfType<Dimension>().Count());

        foreach (ExpectedBox part in expected.Boxes)
        {
            Box box = Assert.IsType<Box>(design.Sketch.Find(part.EntityId));

            // Integer units throughout: compared exactly, never as inches and never with a
            // tolerance.
            Assert.Equal(part.AnchorXUnits, box.Anchor.X.Units);
            Assert.Equal(part.AnchorYUnits, box.Anchor.Y.Units);
            Assert.Equal(part.WidthUnits, box.Width.Units);
            Assert.Equal(part.HeightUnits, box.Height.Units);
            Assert.Equal(part.RotationArcseconds, box.Rotation.Arcseconds);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void The_extents_a_fit_frames_include_the_dimension_lines(string fixture)
    {
        Design design = Load(fixture);
        SampleExpectations expected = SampleExpectations.For(fixture);
        WorldBounds extents = SketchExtents.Of(design.Sketch);

        long partsWest = expected.Boxes.Min(box => box.AnchorXUnits);
        long partsSouth = expected.Boxes.Min(box => box.AnchorYUnits);
        long partsEast = expected.Boxes.Max(box => box.AnchorXUnits + box.WidthUnits);
        long partsNorth = expected.Boxes.Max(box => box.AnchorYUnits + box.HeightUnits);

        // Both fixtures carry a dimension below the parts and one to their left, so a fit that
        // framed only the parts would cut them off.
        Assert.True(
            extents.MinX.Units < partsWest,
            $"{fixture}: the extents stop at x {extents.MinX.Units} with a dimension further west.");
        Assert.True(
            extents.MinY.Units < partsSouth,
            $"{fixture}: the extents stop at y {extents.MinY.Units} with a dimension further south.");
        Assert.True(extents.MaxX.Units >= partsEast);
        Assert.True(extents.MaxY.Units >= partsNorth);

        // And the parts themselves measure what the expectations say the whole drawing does.
        Assert.Equal(expected.Overall.WidthUnits, partsEast - partsWest);
        Assert.Equal(expected.Overall.DepthUnits, partsNorth - partsSouth);
    }

    [Theory]
    [InlineData("garden-gate.scene.json", "Garden gate")]
    [InlineData("shed_roof.json", "Shed roof")]
    [InlineData("coffee-table.scene.json", "Coffee table")]
    public void A_sample_nobody_catalogued_still_gets_a_readable_title(string file, string expected) =>
        Assert.Equal(expected, SampleFiles.TitleFrom(file));

    static Design Load(string fixture) => SampleExpectations.Sample(fixture).Load();
}
