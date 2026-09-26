using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 6 (issues #18, #19): the root's adopted <c>code</c> and <c>site</c> values and a
/// box's <c>wall</c> inputs; version 8 (#39) its segments' <c>bracing</c> methods. The scene is written out by hand, in inches x 1024; the pack id and the
/// "supports" word are made up (they are not read against any pack), and nothing comes from
/// napkin's own output.
/// </summary>
public class BuildingFormatTests
{
    // A 12 ft wall (147456 = 144 x 1024), 3 1/2 in thick (3584), 8 ft tall (98304), stud spacing
    // 24 in (24576), carrying "test-roof". Site: snow 30 psf, wind 115 mph, category "B", frost
    // 42 in (43008), building width 24 ft (294912 = 288 x 1024). One bracing assignment: the segment
    // from the wall's start to an opening ...ff (which is not in this file: the ids are not references).
    private const string Bracing = "[ { \"from\": null, \"to\": \"0192f1a0-0000-4000-8000-0000000000ff\", \"method\": \"zz-test-method\" } ]";

    private const string Filled = $$"""
        {
          "formatVersion": 11,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Wall", "phase": "new",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 147456, "height": 3584, "depth": 98304, "faceUp": "top", "rotation": 0,
              "part": null, "wall": { "supports": "test-roof", "studSpacing": 24576, "bracing": {{Bracing}}, "side": null, "bearing": null, "header": null }, "room": null, "cuts": [] }
          ],
          "relationships": [],
          "fastenerChoices": [],
          "supplies": [],
          "code": { "pack": "us-zz-test", "revision": 2, "mode": "locked", "lockedOn": "2026-09-25" },
          "site": { "groundSnowLoad": 30, "ultimateWindSpeed": 115, "seismicDesignCategory": "B", "frostDepth": 43008,
                    "buildingWidth": 294912, "roofLiveLoad": 20, "source": { "text": "Town office, by phone", "on": "2026-09-24" } }
        }
        """;

    private static Box TheWall(Sketch sketch) => Assert.IsType<Box>(Assert.Single(sketch.Entities.Values));

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void The_code_the_site_and_a_walls_inputs_load_as_written()
    {
        Sketch sketch = Scenes.Accept(Filled);

        Assert.Equal(new CodeChoice("us-zz-test", 2, CodeMode.Locked, new DateOnly(2026, 9, 25)), sketch.Code);
        Assert.Equal(
            new SiteValues(30, 115, "B", new Length(43008), new Length(294912), 20, new SiteSource("Town office, by phone", new DateOnly(2026, 9, 24))),
            sketch.Site);
        EntityId opening = new(new Guid("0192f1a0-0000-4000-8000-0000000000ff"));
        Assert.Equal(
            new WallInputs("test-roof", new Length(24576), [new BracingAssignment(null, opening, "zz-test-method")]),
            TheWall(sketch).WallInputs);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void The_building_inputs_round_trip_and_a_following_code_has_no_date()
    {
        Sketch sketch = Scenes.Accept(Filled.With(
            "\"mode\": \"locked\", \"lockedOn\": \"2026-09-25\"", "\"mode\": \"following\", \"lockedOn\": null"));
        Assert.Equal(CodeMode.Following, sketch.Code!.Mode);
        Assert.Null(sketch.Code.LockedOn);

        string text = SceneWriter.WriteToText(sketch);
        Sketch again = Scenes.Accept(text);

        Assert.Equal(sketch, again);
        Assert.Equal(text, SceneWriter.WriteToText(again));
        Assert.Contains("\"mode\": \"following\"", text, StringComparison.Ordinal);
        Assert.Contains("\"studSpacing\": 24576", text, StringComparison.Ordinal);
        Assert.Contains("\"bracing\": [\n", text, StringComparison.Ordinal);
        Assert.Contains("\"to\": \"0192f1a0-0000-4000-8000-0000000000ff\"", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_wall_with_only_bracing_round_trips_and_without_it_writes_null()
    {
        Sketch sketch = Scenes.Accept(Filled.With("\"supports\": \"test-roof\", \"studSpacing\": 24576", "\"supports\": null, \"studSpacing\": null"));
        Assert.Equal("zz-test-method", Assert.Single(TheWall(sketch).WallInputs!.Bracing).Method);
        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));

        Sketch plain = Scenes.Accept(Filled.With("\"bracing\": " + Bracing, "\"bracing\": null"));
        Assert.Empty(TheWall(plain).WallInputs!.Bracing);
        Assert.Contains("\"bracing\": null", SceneWriter.WriteToText(plain), StringComparison.Ordinal);
        Assert.NotEqual(sketch, plain);
    }

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void Nothing_entered_is_written_as_nulls_and_reads_back_as_not_entered()
    {
        Sketch sketch = Scenes.Accept(Scenes.OneBox);
        Assert.Null(sketch.Code);
        Assert.Equal(SiteValues.NotEntered, sketch.Site);
        Assert.Null(TheWall(sketch).WallInputs);

        string text = SceneWriter.WriteToText(sketch);
        Assert.Contains("\"code\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"groundSnowLoad\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"wall\": null", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_site_value_or_a_code_choice_differs_the_sketch()
    {
        Sketch sketch = Scenes.Accept(Filled);
        Assert.NotEqual(sketch, sketch with { Site = sketch.Site with { GroundSnowLoadPsf = 31 } });
        Assert.NotEqual(sketch, sketch with { Code = null });
    }

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_version_5_file_is_refused_with_the_unsupported_version_message()
    {
        // A version-5 file had no code, site or wall fields; it is refused for its version alone.
        string version5 = Scenes.OneBox
            .With("\"formatVersion\": 11", "\"formatVersion\": 5")
            .With("\"wall\": null, ", string.Empty);

        LoadProblem problem = Scenes.RefuseWith(version5, LoadProblemKind.UnsupportedFormatVersion, "format version 5", "format version 11");
        Assert.Contains("no migration", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_version_7_file_is_refused_with_the_unsupported_version_message()
    {
        // A version-7 file had no bracing on a wall; it is refused for its version alone, no converter.
        string version7 = Scenes.OneBox.With("\"formatVersion\": 11", "\"formatVersion\": 7");

        LoadProblem problem = Scenes.RefuseWith(version7, LoadProblemKind.UnsupportedFormatVersion, "format version 7", "format version 11");
        Assert.Contains("no migration", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Feature", "PRJ-002")]
    [InlineData("\"code\": { \"pack\": \"us-zz-test\"", "\"code\": { \"pack\": \"US ZZ\"", LoadProblemKind.InvalidValue, "pack")]
    [InlineData("\"revision\": 2", "\"revision\": 0", LoadProblemKind.InvalidValue, "revision")]
    [InlineData("\"mode\": \"locked\"", "\"mode\": \"frozen\"", LoadProblemKind.UnknownValue, "mode")]
    [InlineData("\"lockedOn\": \"2026-09-25\"", "\"lockedOn\": null", LoadProblemKind.InvalidValue, "lockedOn")]
    [InlineData("\"lockedOn\": \"2026-09-25\"", "\"lockedOn\": \"25/09/2026\"", LoadProblemKind.InvalidValue, "lockedOn")]
    [InlineData("\"groundSnowLoad\": 30", "\"groundSnowLoad\": -1", LoadProblemKind.InvalidValue, "groundSnowLoad")]
    [InlineData("\"ultimateWindSpeed\": 115", "\"ultimateWindSpeed\": -5", LoadProblemKind.InvalidValue, "ultimateWindSpeed")]
    [InlineData("\"seismicDesignCategory\": \"B\"", "\"seismicDesignCategory\": \"\"", LoadProblemKind.InvalidValue, "seismicDesignCategory")]
    [InlineData("\"frostDepth\": 43008", "\"frostDepth\": -1", LoadProblemKind.InvalidValue, "frostDepth")]
    [InlineData("\"buildingWidth\": 294912", "\"buildingWidth\": 0", LoadProblemKind.InvalidValue, "buildingWidth")]
    [InlineData("\"roofLiveLoad\": 20", "\"roofLiveLoad\": -1", LoadProblemKind.InvalidValue, "roofLiveLoad")]
    [InlineData("\"supports\": \"test-roof\", \"studSpacing\": 24576, \"bracing\": " + Bracing, "\"supports\": null, \"studSpacing\": null, \"bracing\": null", LoadProblemKind.InvalidValue, "wall")]
    [InlineData("\"bracing\": " + Bracing, "\"bracing\": []", LoadProblemKind.InvalidValue, "bracing")]
    [InlineData("\"bracing\": " + Bracing, "\"bracing\": 3", LoadProblemKind.Malformed, "bracing")]
    [InlineData("\"method\": \"zz-test-method\"", "\"method\": \"\"", LoadProblemKind.InvalidValue, "method")]
    [InlineData("\"method\": \"zz-test-method\"", "\"method\": \"zz-test-method\", \"length\": 1", LoadProblemKind.UnknownField, "length")]
    [InlineData("\"from\": null", "\"from\": \"0192f1a0-0000-4000-8000-0000000000ff\"", LoadProblemKind.InvalidValue, "bracing/0")]
    [InlineData("\"from\": null", "\"from\": \"start\"", LoadProblemKind.NotAnId, "from")]
    [InlineData("\"bracing\": [ {", "\"bracing\": [ { \"from\": null, \"to\": \"0192f1a0-0000-4000-8000-0000000000ff\", \"method\": \"other\" }, {", LoadProblemKind.InvalidValue, "bracing/1")]
    [InlineData("\"studSpacing\": 24576", "\"studSpacing\": 0", LoadProblemKind.InvalidValue, "studSpacing")]
    [InlineData("\"supports\": \"test-roof\"", "\"supports\": \"\"", LoadProblemKind.InvalidValue, "supports")]
    [InlineData("\"groundSnowLoad\": 30", "\"groundSnowLoad\": 30.0", LoadProblemKind.NotAnInteger, "groundSnowLoad")]
    [InlineData("\"supplies\": [],", "\"supplies\": [], \"wind\": 5,", LoadProblemKind.UnknownField, "wind")]
    [InlineData("\"site\": {", "\"site\": { \"snow\": 1,", LoadProblemKind.UnknownField, "snow")]
    [InlineData("\"code\": { \"pack\": \"us-zz-test\", \"revision\": 2, \"mode\": \"locked\", \"lockedOn\": \"2026-09-25\" },", "", LoadProblemKind.MissingField, "code")]
    [InlineData("\"part\": null, \"wall\": {", "\"part\": null, \"wal\": {", LoadProblemKind.MissingField, "wall")]
    public void A_malformed_building_input_is_refused_naming_the_field(string original, string replacement, LoadProblemKind kind, string named)
    {
        Scenes.RefuseWith(Filled.With(original, replacement), kind, named);
    }
}
