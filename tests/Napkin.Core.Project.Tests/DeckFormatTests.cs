using System.Reflection;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Scene format 13 (<c>docs/design/deck-and-porch.md</c> §7, #195): a deck, a shed roof, an
/// opening's fill and the site's soil bearing value, each written every time and read back equal;
/// every refusal of §7 hit by one malformed file naming the field; a version-12 file refused naming
/// both versions.
/// </summary>
public class DeckFormatTests
{
    const string Layer = "00000000-0000-0000-0000-000000000001";
    const string DeckId = "0192f1a0-0000-4000-8000-0000000000d1";
    const string WallId = "0192f1a0-0000-4000-8000-0000000000d2";
    const string WindowId = "0192f1a0-0000-4000-8000-0000000000d3";
    const string RoofId = "0192f1a0-0000-4000-8000-0000000000d4";

    const string Deck = """
        { "joistDirection": "out", "joistSpacing": 16384, "joist": "2x8", "beam": { "plies": 2, "lumber": "2x10" },
          "post": "4x4", "postCount": 3, "cantilever": 12288, "decking": "5/4x6", "deckingGap": 128, "blocking": true,
          "supports": null, "species": null, "footingDepth": 43008,
          "hardware": [ { "name": "Joist hanger, 2x8", "quantity": 10 } ],
          "guard": { "height": 36864, "postSpacing": 73728, "balusterGap": 3584, "bottomClearance": 3584, "post": "4x4", "rail": "2x4", "cap": "2x6", "baluster": "2x2" },
          "stair": { "edge": "south", "at": 12288, "width": 36864, "run": 10240, "risers": null, "stringers": 3, "stringer": "2x12", "treadBoards": 2 } }
        """;

    const string Roof = $$"""
        { "rafterSpacing": 16384, "rafter": "2x8", "ledger": "2x8", "overhang": 12288, "blocking": true, "sheathing": "1/2 plywood",
          "roofing": { "name": "Asphalt shingles", "coverage": 33, "waste": 10 },
          "lowEnd": { "kind": "wall", "wall": "{{WallId}}" } }
        """;

    static string Box(string id, string name, string anchorZ, string width, string height, string depth, string deck = "null", string roof = "null", string opening = "null", string wall = "null")
        => $$"""
            { "id": "{{id}}", "type": "box", "layer": "{{Layer}}", "name": "{{name}}", "phase": "new",
              "anchor": { "x": 0, "y": 0, "z": {{anchorZ}} }, "width": {{width}}, "height": {{height}}, "depth": {{depth}}, "faceUp": "top", "rotation": 0,
              "part": null, "wall": {{wall}}, "room": null, "deck": {{deck}}, "roof": {{roof}}, "opening": {{opening}}, "cuts": [] }
            """;

    static readonly string Porch = $$"""
        {
          "formatVersion": 13,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "{{Layer}}", "name": "Default" } ],
          "entities": [
            {{Box(DeckId, "Deck 1", "0", "147456", "122880", "36864", deck: Deck)}},
            {{Box(WallId, "Front wall", "36864", "147456", "3584", "98304", wall: "{ \"supports\": null, \"studSpacing\": null, \"bracing\": null, \"side\": \"exterior\", \"bearing\": true, \"header\": null }")}},
            {{Box(WindowId, "Screen 1", "36864", "36864", "3584", "61440", opening: "{ \"fill\": \"screen\" }")}},
            {{Box(RoofId, "Roof", "135168", "147456", "122880", "51200", roof: Roof)}}
          ],
          "fastenerChoices": [], "supplies": [], "code": null,
          "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "soilBearing": 1500, "source": null },
          "relationships": []
        }
        """;

    static Box Find(Sketch sketch, string id) => sketch.Find<Box>(new EntityId(new Guid(id)))!;

    [Fact]
    [Trait("Feature", "DECK-001")]
    public void A_deck_a_roof_a_screen_and_the_soil_bearing_load_as_written()
    {
        Sketch sketch = Scenes.Accept(Porch);

        DeckInputs deck = Find(sketch, DeckId).Deck!;
        Assert.Equal(
            new DeckInputs(
                JoistDirection.Out, Length.Inches(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Inches(12), "5/4x6", Length.Inches(0, 1, 8), true, null, null, Length.Inches(42),
                new GuardInputs(Length.Inches(36), Length.Inches(72), Length.Inches(3, 1, 2), Length.Inches(3, 1, 2), "4x4", "2x4", "2x6", "2x2"),
                new StairInputs(DeckEdge.South, Length.Inches(12), Length.Inches(36), Length.Inches(10), null, 3, "2x12", 2))
            {
                Hardware = [new HardwareItem("Joist hanger, 2x8", 10)],
            },
            deck);

        RoofInputs roof = Find(sketch, RoofId).Roof!;
        Assert.Equal(new WallLowEnd(new EntityId(new Guid(WallId))), roof.LowEnd);
        Assert.Equal(new Roofing("Asphalt shingles", 33, 10), roof.Roofing);
        Assert.Equal((Length.Inches(16), "1/2 plywood"), (roof.RafterSpacing, roof.Sheathing));

        Assert.Equal(OpeningFill.Screen, Find(sketch, WindowId).Opening);
        Assert.Null(Find(sketch, WallId).Opening);
        Assert.Equal(1500, sketch.Site.SoilBearingPsf);
    }

    [Fact]
    [Trait("Feature", "DECK-001")]
    public void Everything_round_trips_and_a_beam_low_end_does_too()
    {
        string beamRoof = Porch.With($"\"lowEnd\": {{ \"kind\": \"wall\", \"wall\": \"{WallId}\" }}", "\"lowEnd\": { \"kind\": \"beam\", \"beam\": { \"plies\": 2, \"lumber\": \"2x10\" }, \"post\": \"4x4\", \"postCount\": 2 }");
        foreach (string scene in new[] { Porch, beamRoof })
        {
            Sketch sketch = Scenes.Accept(scene);
            Sketch again = Scenes.Accept(SceneWriter.WriteToText(sketch));
            Assert.Equal(sketch.Entities.Values.OrderBy(entity => entity.Id), again.Entities.Values.OrderBy(entity => entity.Id));
            Assert.Equal(sketch.Site, again.Site);
        }

        Assert.Equal(new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2), Find(Scenes.Accept(beamRoof), RoofId).Roof!.LowEnd);
    }

    [Fact]
    public void A_box_with_nothing_to_say_writes_deck_roof_and_opening_as_null()
    {
        string text = SceneWriter.WriteToText(Scenes.Accept(Scenes.OneBox));

        Assert.Contains("\"deck\": null,", text, StringComparison.Ordinal);
        Assert.Contains("\"roof\": null,", text, StringComparison.Ordinal);
        Assert.Contains("\"opening\": null,", text, StringComparison.Ordinal);
        Assert.Contains("\"soilBearing\": null,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_property_a_box_carries_survives_a_round_trip()
    {
        // The reflection list: every public settable property of Box is set here, so a new one that
        // the writer forgets fails this test rather than vanishing from saved files.
        string[] covered = ["Name", "Phase", "Part", "WallInputs", "Room", "Deck", "Roof", "Opening", "Cuts"];
        string[] settable = [.. typeof(Box).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null && property.DeclaringType != typeof(object))
            .Where(property => property.Name is not ("Id" or "Layer" or "Anchor" or "Width" or "Height" or "Depth" or "FaceUp" or "Rotation" or "EqualityContract"))
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)];

        Assert.Equal(covered.Order(StringComparer.Ordinal), settable);
    }

    [Theory]
    [InlineData("\"joistDirection\": \"out\"", "\"joistDirection\": \"along\"", LoadProblemKind.InvalidValue, "joistDirection")]
    [InlineData("\"joistSpacing\": 16384", "\"joistSpacing\": 0", LoadProblemKind.InvalidValue, "joistSpacing")]
    [InlineData("\"joist\": \"2x8\"", "\"joist\": \"\"", LoadProblemKind.InvalidValue, "joist")]
    [InlineData("\"plies\": 2, \"lumber\": \"2x10\" },\n  \"post\"", "\"plies\": 4, \"lumber\": \"2x10\" },\n  \"post\"", LoadProblemKind.InvalidValue, "plies")]
    [InlineData("\"postCount\": 3", "\"postCount\": 1", LoadProblemKind.InvalidValue, "postCount")]
    [InlineData("\"cantilever\": 12288", "\"cantilever\": -1", LoadProblemKind.InvalidValue, "cantilever")]
    [InlineData("\"deckingGap\": 128", "\"deckingGap\": -1", LoadProblemKind.InvalidValue, "deckingGap")]
    [InlineData("\"footingDepth\": 43008", "\"footingDepth\": -1", LoadProblemKind.InvalidValue, "footingDepth")]
    [InlineData("\"quantity\": 10", "\"quantity\": 0", LoadProblemKind.InvalidValue, "quantity")]
    [InlineData("\"height\": 36864, \"postSpacing\"", "\"height\": 0, \"postSpacing\"", LoadProblemKind.InvalidValue, "height")]
    [InlineData("\"postSpacing\": 73728", "\"postSpacing\": 0", LoadProblemKind.InvalidValue, "postSpacing")]
    [InlineData("\"baluster\": \"2x2\"", "\"baluster\": \"\"", LoadProblemKind.InvalidValue, "baluster")]
    [InlineData("\"edge\": \"south\"", "\"edge\": \"up\"", LoadProblemKind.UnknownValue, "edge")]
    [InlineData("\"width\": 36864, \"run\"", "\"width\": 0, \"run\"", LoadProblemKind.InvalidValue, "width")]
    [InlineData("\"run\": 10240", "\"run\": 0", LoadProblemKind.InvalidValue, "run")]
    [InlineData("\"risers\": null", "\"risers\": 1", LoadProblemKind.InvalidValue, "risers")]
    [InlineData("\"stringers\": 3", "\"stringers\": 1", LoadProblemKind.InvalidValue, "stringers")]
    [InlineData("\"treadBoards\": 2", "\"treadBoards\": 0", LoadProblemKind.InvalidValue, "treadBoards")]
    [InlineData("\"rafterSpacing\": 16384", "\"rafterSpacing\": 0", LoadProblemKind.InvalidValue, "rafterSpacing")]
    [InlineData("\"overhang\": 12288", "\"overhang\": -1", LoadProblemKind.InvalidValue, "overhang")]
    [InlineData("\"coverage\": 33", "\"coverage\": 0", LoadProblemKind.InvalidValue, "coverage")]
    [InlineData("\"waste\": 10", "\"waste\": -1", LoadProblemKind.InvalidValue, "waste")]
    [InlineData("\"kind\": \"wall\"", "\"kind\": \"cloud\"", LoadProblemKind.InvalidValue, "kind")]
    [InlineData("\"fill\": \"screen\"", "\"fill\": \"mesh\"", LoadProblemKind.UnknownValue, "fill")]
    [InlineData("\"soilBearing\": 1500", "\"soilBearing\": -1", LoadProblemKind.InvalidValue, "soilBearing")]
    [InlineData("\"soilBearing\": 1500, ", "", LoadProblemKind.MissingField, "soilBearing")]
    [InlineData("\"deck\": null, \"roof\": null, \"opening\": { \"fill\"", "\"roof\": null, \"opening\": { \"fill\"", LoadProblemKind.MissingField, "deck")]
    public void Each_refusal_names_the_field(string original, string replacement, LoadProblemKind kind, string named)
        => Scenes.RefuseWith(Porch.With(original, replacement), kind, named);

    [Fact]
    public void A_roof_whose_low_end_names_no_box_is_refused()
        => Scenes.RefuseWith(Porch.With($"\"wall\": \"{WallId}\" }}", "\"wall\": \"0192f1a0-0000-4000-8000-0000000000ff\" }"), LoadProblemKind.DanglingReference, "lowEnd");

    [Theory]
    // A deck that is also a part, a roof that is also a deck, an opening that is also a wall.
    [InlineData("\"part\": null, \"wall\": null, \"room\": null, \"deck\": {", "\"part\": { \"stock\": null, \"species\": null, \"quantity\": 1, \"planAxes\": { \"x\": \"length\", \"y\": \"width\" }, \"hardware\": [], \"rough\": false, \"grain\": null, \"showFace\": null }, \"wall\": null, \"room\": null, \"deck\": {", "deck")]
    [InlineData("\"deck\": null, \"roof\": { \"rafterSpacing\"", "\"deck\": " + "{ \"joistDirection\": \"out\", \"joistSpacing\": 16384, \"joist\": \"2x8\", \"beam\": { \"plies\": 1, \"lumber\": \"2x8\" }, \"post\": \"4x4\", \"postCount\": 2, \"cantilever\": 0, \"decking\": \"5/4x6\", \"deckingGap\": 0, \"blocking\": false, \"supports\": null, \"species\": null, \"footingDepth\": null, \"hardware\": [], \"guard\": null, \"stair\": null }" + ", \"roof\": { \"rafterSpacing\"", "deck")]
    [InlineData("\"header\": null }, \"room\": null, \"deck\": null, \"roof\": null, \"opening\": null", "\"header\": null }, \"room\": null, \"deck\": null, \"roof\": null, \"opening\": { \"fill\": \"glass\" }", "opening")]
    public void A_box_that_is_two_things_at_once_is_refused(string original, string replacement, string named)
        => Scenes.RefuseWith(Porch.With(original, replacement), LoadProblemKind.InvalidValue, named);

    [Fact]
    public void A_version_12_file_is_refused_naming_both_versions()
        => Scenes.RefuseWith(Scenes.OneBox.With("\"formatVersion\": 13", "\"formatVersion\": 12"), LoadProblemKind.UnsupportedFormatVersion, "format version 12", "format version 13");
}
