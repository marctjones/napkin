using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 9 (<c>docs/design/sketch-mode.md</c> &#xA7;4.1, &#xA7;7.3): every part carries a
/// required <c>rough</c> boolean, written and read as the part's own field; a version-8 file is
/// refused with the unsupported-version message, never converted.
/// </summary>
public class RoughFormatTests
{
    private const string FirmPart = "\"hardware\": [], \"rough\": false, \"grain\": null, \"showFace\": null }";
    private const string RoughPart = "\"hardware\": [], \"rough\": true, \"grain\": null, \"showFace\": null }";

    [Fact]
    public void ARoughPartIsReadAsRough()
    {
        Sketch sketch = Scenes.Accept(Scenes.OneBox.With(FirmPart, RoughPart));

        Box box = Assert.Single(sketch.Entities.Values.OfType<Box>());
        Assert.True(box.Part!.Rough);
    }

    [Fact]
    public void AFirmPartIsReadAsFirm()
    {
        Sketch sketch = Scenes.Accept(Scenes.OneBox);

        Box box = Assert.Single(sketch.Entities.Values.OfType<Box>());
        Assert.False(box.Part!.Rough);
    }

    [Fact]
    public void TheRoughMarkIsWrittenAfterTheHardwareAndSurvivesARoundTrip()
    {
        Sketch sketch = Scenes.Accept(Scenes.OneBox.With(FirmPart, RoughPart));

        string text = SceneWriter.WriteToText(sketch);

        Assert.Contains("\"hardware\": [],\n        \"rough\": true, \"grain\": null, \"showFace\": null\n", text, StringComparison.Ordinal);
        Sketch again = Scenes.Accept(text);
        Assert.True(Assert.Single(again.Entities.Values.OfType<Box>()).Part!.Rough);
    }

    [Fact]
    public void AFirmPartWritesRoughFalseBecauseTheFormatHasNoOptionalFields()
    {
        string text = SceneWriter.WriteToText(Scenes.Accept(Scenes.OneBox));

        Assert.Contains("\"rough\": false, \"grain\": null, \"showFace\": null", text, StringComparison.Ordinal);
    }

    [Fact]
    public void APartWithoutRoughIsRefusedAsAMissingField()
    {
        Scenes.RefuseWith(
            Scenes.OneBox.With(FirmPart, "\"hardware\": [] }"),
            LoadProblemKind.MissingField,
            "rough");
    }

    [Theory]
    [InlineData("\"yes\"", "text")]
    [InlineData("1", "a number")]
    [InlineData("null", "null")]
    public void ARoughThatIsNotABooleanIsRefused(string value, string described)
    {
        LoadProblem problem = Scenes.RefuseWith(
            Scenes.OneBox.With(FirmPart, $"\"hardware\": [], \"rough\": {value} }}"),
            LoadProblemKind.Malformed,
            "rough");
        Assert.Contains(described, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AVersionEightFileIsRefusedWithTheUnsupportedVersionMessage()
    {
        string version8 = Scenes.OneBox
            .With("\"formatVersion\": 12", "\"formatVersion\": 8")
            .With(", \"rough\": false, \"grain\": null, \"showFace\": null", string.Empty);

        Scenes.RefuseWith(version8, LoadProblemKind.UnsupportedFormatVersion, "format version 8", "format version 12");
    }

    [Fact]
    public void RoughIsPartOfAPartsValueEquality()
    {
        var firm = new Part("2x4", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));
        Part rough = firm with { Rough = true };

        Assert.NotEqual(firm, rough);
        Assert.NotEqual(firm.GetHashCode(), rough.GetHashCode());
        Assert.Equal(rough, firm with { Rough = true });
        Assert.Equal(rough.GetHashCode(), (firm with { Rough = true }).GetHashCode());
    }
}
