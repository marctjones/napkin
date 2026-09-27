using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Scene format 14 (docs/design/furniture-checks.md §4.2, §9.1, #218): a part's drawer mark and the
/// design's furniture marks, each written every time and read back equal; each refusal names its field.
/// </summary>
public class FurnitureFormatTests
{
    static readonly string Dresser = Scenes.OneBox
        .With("\"showFace\": null, \"drawer\": null", "\"showFace\": null, \"drawer\": { \"extension\": 14336 }")
        .With("\"furniture\": { \"kind\": \"none\", \"anchored\": false }", "\"furniture\": { \"kind\": \"clothingStorage\", \"anchored\": true }");

    [Fact]
    public void A_drawer_mark_and_the_furniture_marks_load_as_written_and_round_trip()
    {
        Sketch sketch = Scenes.Accept(Dresser);
        Assert.Equal(new DrawerMark(Length.Inches(14)), Assert.Single(sketch.Entities.Values.OfType<Box>()).Part!.Drawer);
        Assert.Equal(new FurnitureMarks(FurnitureKind.ClothingStorage, true), sketch.Furniture);
        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));

        Sketch bunk = Scenes.Accept(Scenes.OneBox.With("\"kind\": \"none\"", "\"kind\": \"bunkBed\""));
        Assert.Equal(FurnitureKind.BunkBed, bunk.Furniture.Kind);
        Assert.Equal(FurnitureMarks.None, Scenes.Accept(Scenes.OneBox).Furniture);
    }

    [Fact]
    public void A_bad_kind_a_drawer_that_opens_no_distance_and_a_missing_field_are_refused_by_name()
    {
        Scenes.RefuseWith(Scenes.OneBox.With("\"kind\": \"none\"", "\"kind\": \"dresser\""), LoadProblemKind.UnknownValue, "dresser");
        Scenes.RefuseWith(Dresser.With("\"extension\": 14336", "\"extension\": 0"), LoadProblemKind.InvalidValue, "extension");
        Scenes.RefuseWith(Dresser.With("\"extension\": 14336", "\"extension\": -1024"), LoadProblemKind.InvalidValue, "extension");
        Scenes.RefuseWith(Dresser.With("{ \"extension\": 14336 }", "{ }"), LoadProblemKind.MissingField, "extension");
        Scenes.RefuseWith(Dresser.With("\"extension\": 14336", "\"extension\": 14336, \"slides\": \"full\""), LoadProblemKind.UnknownField, "slides");
        Scenes.RefuseWith(Scenes.OneBox.With("\"anchored\": false", "\"anchored\": \"no\""), LoadProblemKind.Malformed, "anchored");
        Scenes.RefuseWith(Scenes.OneBox.With("\"furniture\": { \"kind\": \"none\", \"anchored\": false }, ", string.Empty), LoadProblemKind.MissingField, "furniture");
        Scenes.RefuseWith(Scenes.OneBox.With(", \"drawer\": null", string.Empty), LoadProblemKind.MissingField, "drawer");
    }
}
