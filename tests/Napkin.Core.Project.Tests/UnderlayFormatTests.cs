using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>Scene format 16 (docs/design/permit-set.md §5.4, #224): the survey underlay on the site, strict both ways.</summary>
public class UnderlayFormatTests
{
    const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    static readonly string WithUnderlay = Scenes.OneBox.With(
        "\"underlay\": null",
        "\"underlay\": { \"asset\": \"" + Hash + "\", \"name\": \"survey.png\", \"imageA\": { \"x\": 10, \"y\": 20 }, \"imageB\": { \"x\": 410, \"y\": 20 }, "
        + "\"worldA\": { \"x\": 0, \"y\": 0 }, \"worldB\": { \"x\": 1228800, \"y\": 0 }, \"distance\": 1228800 }");

    [Fact]
    public void An_underlay_loads_as_written_and_round_trips()
    {
        Sketch sketch = Scenes.Accept(WithUnderlay);
        Assert.Equal(
            new SurveyUnderlay(Hash, new Pixel(10, 20), new Pixel(410, 20), Point2.Origin, new Point2(Length.Feet(100), Length.Zero), Length.Feet(100), "survey.png"),
            sketch.Site.Underlay);
        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));
        Assert.Null(Scenes.Accept(Scenes.OneBox).Site.Underlay);
    }

    [Fact]
    public void A_bad_underlay_is_refused_by_name()
    {
        Scenes.RefuseWith(WithUnderlay.With(Hash, Hash.ToUpperInvariant()), LoadProblemKind.InvalidValue, "SHA-256");
        Scenes.RefuseWith(WithUnderlay.With("\"imageB\": { \"x\": 410, \"y\": 20 }", "\"imageB\": { \"x\": 10, \"y\": 20 }"), LoadProblemKind.InvalidValue, "two different points");
        Scenes.RefuseWith(WithUnderlay.With("\"distance\": 1228800 }", "\"distance\": 0 }"), LoadProblemKind.InvalidValue, "longer than zero");
        Scenes.RefuseWith(WithUnderlay.With("\"imageA\": { \"x\": 10, \"y\": 20 }", "\"imageA\": { \"x\": 10, \"y\": 20, \"z\": 1 }"), LoadProblemKind.UnknownField, "z");
        Scenes.RefuseWith(WithUnderlay.With("\"name\": \"survey.png\", ", string.Empty), LoadProblemKind.MissingField, "name");
        Scenes.RefuseWith(Scenes.OneBox.With("\"underlay\": null, ", string.Empty), LoadProblemKind.MissingField, "underlay");
    }
}
