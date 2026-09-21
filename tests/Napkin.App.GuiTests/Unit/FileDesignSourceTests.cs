using Napkin.App.Designs;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What the viewer's one way of opening a drawing does with a file.
/// </summary>
/// <remarks>
/// The reader's own refusals are tested in <c>tests/Napkin.Core.Project.Tests</c>; what is under
/// test here is the App's side of the seam — that a refusal arrives as a
/// <see cref="DesignLoadException"/> carrying <em>every</em> problem, that nothing escapes as an
/// unhandled exception of another kind, and that a file that opens produces exactly the sketch the
/// reader produced.
/// </remarks>
public class FileDesignSourceTests
{
    [Fact]
    public void A_file_that_is_not_there_is_refused_and_named()
    {
        string missing = BadScenes.MissingFile("no-such-design.scene.json");

        DesignLoadException failure = Assert.Throws<DesignLoadException>(
            () => new FileDesignSource(missing).Load());

        Assert.NotEmpty(failure.Problems);
        Assert.Contains(missing, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("empty.scene.json", BadScenes.Empty)]
    [InlineData("garbled.scene.json", BadScenes.NotJson)]
    public void An_empty_or_garbled_file_is_refused_rather_than_crashing(string name, string text)
    {
        DesignLoadException failure = Refuse(name, text);

        Assert.NotEmpty(failure.Problems);
        Assert.Contains("could not be opened", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_file_from_another_format_version_is_refused_naming_the_version()
    {
        DesignLoadException failure = Refuse("from-the-future.scene.json", BadScenes.WrongVersion);

        Assert.Contains("2", Assert.Single(failure.Problems), StringComparison.Ordinal);
        Assert.Contains(
            SceneReader.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_dangling_reference_is_refused_rather_than_repaired()
    {
        DesignLoadException failure = Refuse("dangling.scene.json", BadScenes.DanglingReference);

        Assert.Single(failure.Problems);
        Assert.Contains("0192f1a0-0000-4000-8000-0000000000ff", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_relationship_kind_this_build_cannot_hold_is_refused_naming_the_kind()
    {
        DesignLoadException failure = Refuse(
            "solver-only.scene.json",
            BadScenes.UnsupportedRelationshipKind);

        Assert.Contains("distance", Assert.Single(failure.Problems), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_problem_the_reader_found_reaches_the_window_not_just_the_first()
    {
        string path = BadScenes.Write("several-faults.scene.json", BadScenes.SeveralFaults);
        Refused refused = Assert.IsType<Refused>(SceneReader.ReadFile(path));

        DesignLoadException failure = Assert.Throws<DesignLoadException>(
            () => new FileDesignSource(path).Load());

        // Exactly the reader's list, in the reader's order, one readable line each. This is the
        // assertion the refusal panel's "every problem, not the first" promise rests on.
        Assert.Equal(
            refused.Problems.Select(problem => problem.ToString()),
            failure.Problems);
        Assert.True(failure.Problems.Length > 1, "the fixture stopped producing several problems.");
    }

    [Fact]
    public void A_file_that_opens_produces_the_sketch_the_reader_produced()
    {
        string path = BadScenes.Write("one-box.scene.json", BadScenes.Good);
        Loaded loaded = Assert.IsType<Loaded>(SceneReader.ReadFile(path));

        Design design = new FileDesignSource(path).Load();

        Assert.Equal(loaded.Sketch, design.Sketch);
        Assert.Equal("one-box.scene.json", design.Name);
        Assert.Empty(design.Labels);
        Assert.Single(design.Sketch.Entities.Values.OfType<Box>());
    }

    [Fact]
    public void A_shipped_sample_opens_as_the_reader_reads_it()
    {
        foreach (string fixture in SampleExpectations.Fixtures)
        {
            FileDesignSource sample = SampleExpectations.Sample(fixture);
            Loaded loaded = Assert.IsType<Loaded>(SceneReader.ReadFile(sample.Path));

            Assert.Equal(loaded.Sketch, sample.Load().Sketch);
        }
    }

    [Fact]
    public void A_source_names_the_file_it_reads()
    {
        string path = BadScenes.Write("named.scene.json", BadScenes.Good);

        FileDesignSource titled = new(path, "Coffee table", "A table.");
        FileDesignSource plain = new(path);

        Assert.Equal("Coffee table", titled.Name);
        Assert.Equal("A table.  (named.scene.json)", titled.Description);
        Assert.Equal("named.scene.json", plain.Name);
        Assert.Equal(path, plain.Description);
        Assert.Equal("named.scene.json", plain.FileName);
        Assert.Equal(path, plain.Path);
    }

    [Fact]
    public void A_source_without_a_file_to_read_is_a_programming_error_not_a_refusal() =>
        Assert.Throws<ArgumentException>(() => new FileDesignSource("  "));

    static DesignLoadException Refuse(string name, string text)
    {
        string path = BadScenes.Write(name, text);
        return Assert.Throws<DesignLoadException>(() => new FileDesignSource(path).Load());
    }
}
