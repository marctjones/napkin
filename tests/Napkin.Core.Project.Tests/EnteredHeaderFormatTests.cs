using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Scene format 17 (docs/design/manual-code-values.md §7.1, #246): an opening's header row entered by
/// hand, strict both ways. Every value here is synthetic and chosen to look unlike any real row.
/// </summary>
public class EnteredHeaderFormatTests
{
    const string Row = """
        { "plies": 9, "lumber": "2x99", "jackStuds": 9, "kingStuds": 9,
          "citation": { "code": "SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099", "table": "Table T-99", "location": "p. 99, row 9", "notes": null },
          "enteredBy": "A. Person", "enteredOn": "2026-09-27",
          "for": { "pack": "us-zz-test", "side": "exterior", "supports": "test-roof", "span": 36864,
                   "groundSnowLoad": 99, "ultimateWindSpeed": null, "seismicDesignCategory": null,
                   "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null } }
        """;

    // The site carries the same six names, so every substitution is made in the row's own text.
    static readonly string WithRow = WithRowText(Row);

    static string WithRowText(string row, string fill = "\"glass\"")
        => Scenes.OneBox.With("\"opening\": null", "\"opening\": { \"fill\": " + fill + ", \"enteredHeader\": " + row + " }");

    static readonly EnteredHeader Expected = new(
        9,
        "2x99",
        9,
        9,
        new EnteredCitation("SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099", "Table T-99", "p. 99, row 9", null),
        "A. Person",
        new DateOnly(2026, 9, 27),
        new EnteredHeaderInputs("us-zz-test", WallSide.Exterior, "test-roof", Length.Inches(36), 99, null, null, null, null, null));

    static Box Shelf(Sketch sketch) => sketch.Find<Box>(new EntityId(new Guid(Scenes.BoxId)))!;

    [Fact]
    public void A_row_loads_as_written_and_round_trips()
    {
        Sketch sketch = Scenes.Accept(WithRow);
        Box box = Shelf(sketch);
        Assert.Equal(Expected, box.EnteredHeader);
        Assert.Equal(OpeningFill.Glass, box.Opening);

        string written = SceneWriter.WriteToText(sketch);
        Assert.Equal(sketch, Scenes.Accept(written));
        Assert.Equal(written, SceneWriter.WriteToText(Scenes.Accept(written)));
        Assert.Null(Shelf(Scenes.Accept(Scenes.OneBox)).EnteredHeader);
    }

    [Fact]
    public void Every_optional_field_round_trips_filled_and_a_row_needs_no_fill()
    {
        // The opening's fill is said separately, and a window in an existing wall need not say it.
        string filled = WithRowText(
            Row
            .With("\"notes\": null", "\"notes\": \"footnote applied by hand\"")
            .With("\"ultimateWindSpeed\": null", "\"ultimateWindSpeed\": 99")
            .With("\"seismicDesignCategory\": null", "\"seismicDesignCategory\": \"Z\"")
            .With("\"frostDepth\": null", "\"frostDepth\": 1024")
            .With("\"buildingWidth\": null", "\"buildingWidth\": 2048")
            .With("\"roofLiveLoad\": null", "\"roofLiveLoad\": 99"),
            fill: "null");
        Sketch sketch = Scenes.Accept(filled);
        Box box = Shelf(sketch);
        Assert.Null(box.Opening);
        Assert.Equal(
            Expected with
            {
                Citation = Expected.Citation with { Notes = "footnote applied by hand" },
                For = Expected.For with
                {
                    UltimateWindSpeedMph = 99, SeismicDesignCategory = "Z", FrostDepth = Length.Inches(1), BuildingWidth = Length.Inches(2), RoofLiveLoadPsf = 99,
                },
            },
            box.EnteredHeader);
        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));
    }

    [Fact]
    public void An_opening_with_neither_a_fill_nor_a_row_has_one_spelling()
    {
        Scenes.RefuseWith(Scenes.OneBox.With("\"opening\": null", "\"opening\": { \"fill\": null, \"enteredHeader\": null }"), LoadProblemKind.InvalidValue, "\"opening\": null");
        Scenes.RefuseWith(Scenes.OneBox.With("\"opening\": null", "\"opening\": { \"fill\": \"glass\" }"), LoadProblemKind.MissingField, "enteredHeader");
    }

    [Theory]
    [InlineData("\"plies\": 9", "\"plies\": 0", LoadProblemKind.InvalidValue, "plies")]
    [InlineData("\"jackStuds\": 9", "\"jackStuds\": -1", LoadProblemKind.InvalidValue, "jackStuds")]
    [InlineData("\"kingStuds\": 9", "\"kingStuds\": -1", LoadProblemKind.InvalidValue, "kingStuds")]
    [InlineData("\"lumber\": \"2x99\"", "\"lumber\": \" \"", LoadProblemKind.InvalidValue, "lumber")]
    [InlineData("\"code\": \"SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099\"", "\"code\": \"\"", LoadProblemKind.InvalidValue, "code")]
    [InlineData("\"table\": \"Table T-99\"", "\"table\": \"\"", LoadProblemKind.InvalidValue, "table")]
    [InlineData("\"location\": \"p. 99, row 9\"", "\"location\": \" \"", LoadProblemKind.InvalidValue, "location")]
    [InlineData("\"notes\": null", "\"notes\": \"\"", LoadProblemKind.InvalidValue, "notes")]
    [InlineData("\"enteredBy\": \"A. Person\"", "\"enteredBy\": \"\"", LoadProblemKind.InvalidValue, "enteredBy")]
    [InlineData("\"enteredOn\": \"2026-09-27\"", "\"enteredOn\": \"27/09/2026\"", LoadProblemKind.InvalidValue, "enteredOn")]
    [InlineData("\"enteredOn\": \"2026-09-27\"", "\"enteredOn\": null", LoadProblemKind.InvalidValue, "enteredOn")]
    [InlineData("\"pack\": \"us-zz-test\"", "\"pack\": \"US ZZ\"", LoadProblemKind.InvalidValue, "pack")]
    [InlineData("\"side\": \"exterior\"", "\"side\": \"outside\"", LoadProblemKind.UnknownValue, "side")]
    [InlineData("\"supports\": \"test-roof\"", "\"supports\": \"\"", LoadProblemKind.InvalidValue, "supports")]
    [InlineData("\"span\": 36864", "\"span\": 0", LoadProblemKind.InvalidValue, "span")]
    [InlineData("\"groundSnowLoad\": 99", "\"groundSnowLoad\": -1", LoadProblemKind.InvalidValue, "groundSnowLoad")]
    [InlineData("\"ultimateWindSpeed\": null", "\"ultimateWindSpeed\": -1", LoadProblemKind.InvalidValue, "ultimateWindSpeed")]
    [InlineData("\"seismicDesignCategory\": null", "\"seismicDesignCategory\": \"\"", LoadProblemKind.InvalidValue, "seismicDesignCategory")]
    [InlineData("\"frostDepth\": null", "\"frostDepth\": -1", LoadProblemKind.InvalidValue, "frostDepth")]
    [InlineData("\"buildingWidth\": null", "\"buildingWidth\": 0", LoadProblemKind.InvalidValue, "buildingWidth")]
    [InlineData("\"roofLiveLoad\": null", "\"roofLiveLoad\": -1", LoadProblemKind.InvalidValue, "roofLiveLoad")]
    [InlineData("\"plies\": 9, ", "", LoadProblemKind.MissingField, "plies")]
    [InlineData("\"roofLiveLoad\": null }", "\"roofLiveLoad\": null, \"bearing\": true }", LoadProblemKind.UnknownField, "bearing")]
    public void Each_refusal_names_the_field(string original, string replacement, LoadProblemKind kind, string named)
        => Scenes.RefuseWith(WithRowText(Row.With(original, replacement)), kind, named);

    [Fact]
    public void A_box_with_a_row_is_not_also_a_wall_a_deck_or_a_roof()
    {
        string onAWall = WithRowText(Row, fill: "null")
            .With("\"wall\": null", "\"wall\": { \"supports\": \"test-roof\", \"studSpacing\": null, \"bracing\": null, \"side\": null, \"bearing\": null, \"header\": null }");
        Scenes.RefuseWith(onAWall, LoadProblemKind.InvalidValue, "opening");
    }
}
