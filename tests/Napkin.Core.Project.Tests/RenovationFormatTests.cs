using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 10 (docs/design/renovation-sketches.md §7, test 1): a phase on every entity, a
/// wall's side, bearing and typed header, a room on every box and the note entity round-trip; each
/// refusal of §7 is hit by one malformed file naming the field; a version-9 file is refused naming
/// both versions.
/// </summary>
public class RenovationFormatTests
{
    private const string Layer = "00000000-0000-0000-0000-000000000001";

    private const string Wall = """
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Wall 1", "phase": "existing",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 147456, "height": 3584, "depth": 98304, "faceUp": "top", "rotation": 0,
              "part": null,
              "wall": { "supports": null, "studSpacing": null, "bracing": null, "side": "exterior", "bearing": false,
                        "header": { "plies": 2, "lumber": "2x6" } },
              "room": null, "cuts": [] }
        """;

    private const string Room = """
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Room", "phase": "new",
              "anchor": { "x": 3584, "y": 3584, "z": 0 }, "width": 172032, "height": 147456, "depth": 98304, "faceUp": "top", "rotation": 0,
              "part": null, "wall": null,
              "room": { "drywall": "walls-and-ceiling", "sheet": { "width": 49152, "length": 98304 },
                        "insulation": "exterior", "insulationBy": "area", "insulationCoverage": 40,
                        "paint": "walls", "paintCoats": 2, "paintCoverage": 350,
                        "flooring": true, "flooringWaste": 10, "flooringBox": 20,
                        "baseboard": true, "baseboardStick": 98304,
                        "measured": { "south": 172032, "north": 173056, "east": null, "west": null, "diagonal1": 225280, "diagonal2": null } },
              "cuts": [] }
        """;

    private const string NoteEntity = """
            { "id": "0192f1a0-0000-4000-8000-00000000000c", "type": "note", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "", "phase": "demolish", "position": { "x": 1024, "y": 2048 }, "text": "outlet", "symbol": "outlet" }
        """;

    private static readonly string Scene = $$"""
        {
          "formatVersion": 11,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "{{Layer}}", "name": "Default" } ],
          "entities": [
        {{Wall}},
        {{Room}},
        {{NoteEntity}}
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
          "relationships": []
        }
        """;

    private static readonly EntityId WallId = new(new Guid("0192f1a0-0000-4000-8000-00000000000a"));
    private static readonly EntityId RoomId = new(new Guid("0192f1a0-0000-4000-8000-00000000000b"));
    private static readonly EntityId NoteId = new(new Guid("0192f1a0-0000-4000-8000-00000000000c"));

    [Fact]
    public void Phase_side_bearing_header_room_and_note_load_as_written()
    {
        Sketch sketch = Scenes.Accept(Scene);

        Box wall = sketch.Find<Box>(WallId)!;
        Assert.Equal(Phase.Existing, wall.Phase);
        Assert.Equal(new WallInputs(null, null) { Side = WallSide.Exterior, Bearing = false, Header = new TypedHeader(2, "2x6") }, wall.WallInputs);
        Assert.Null(wall.Room);

        Box room = sketch.Find<Box>(RoomId)!;
        Assert.Equal(Phase.New, room.Phase);
        Assert.Equal(
            new RoomInputs(
                RoomSurfaces.WallsAndCeiling, new SheetSize(Length.Inches(48), Length.Inches(96)),
                InsulatedWalls.Exterior, InsulationBy.Area, 40, RoomSurfaces.Walls, 2, 350, true, 10, 20, true, Length.Inches(96),
                new MeasuredRoom(Length.Inches(168), Length.Inches(169), null, null, Length.Inches(220), null)),
            room.Room);

        Note note = sketch.Find<Note>(NoteId)!;
        Assert.Equal(new Note(NoteId, new LayerId(new Guid(Layer)), Point2.Inches(1, 2), "outlet", NoteSymbol.Outlet) { Phase = Phase.Demolish }, note);
    }

    [Fact]
    public void Every_renovation_field_survives_a_round_trip_and_is_written_every_time()
    {
        Sketch sketch = Scenes.Accept(Scene);

        string text = SceneWriter.WriteToText(sketch);

        Assert.Equal(sketch, Scenes.Accept(text));
        Assert.Contains("\"phase\": \"existing\"", text, StringComparison.Ordinal);
        Assert.Contains("\"phase\": \"demolish\"", text, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"note\"", text, StringComparison.Ordinal);
        Assert.Contains("\"insulationBy\": \"area\"", text, StringComparison.Ordinal);
        Assert.Contains("\"east\": null", text, StringComparison.Ordinal);

        // Every symbol, side, surface and insulation choice has one spelling each way.
        foreach (NoteSymbol symbol in Enum.GetValues<NoteSymbol>())
        {
            Sketch with = sketch.WithEntity(sketch.Find<Note>(NoteId)! with { Symbol = symbol, Text = "x" });
            Assert.Equal(with, Scenes.Accept(SceneWriter.WriteToText(with)));
        }

        foreach ((RoomSurfaces surfaces, InsulatedWalls insulated, InsulationBy by) in new[]
                 {
                     (RoomSurfaces.None, InsulatedWalls.All, InsulationBy.Bays),
                     (RoomSurfaces.Walls, InsulatedWalls.None, InsulationBy.Area),
                 })
        {
            Box room = sketch.Find<Box>(RoomId)!;
            Sketch with = sketch.WithEntity(room with { Room = RoomInputs.None with { Drywall = surfaces, Paint = surfaces, Insulation = insulated, InsulationBy = by } });
            Assert.Equal(with, Scenes.Accept(SceneWriter.WriteToText(with)));
        }

        Box wall = sketch.Find<Box>(WallId)!;
        Sketch interior = sketch.WithEntity(wall with { WallInputs = new WallInputs("x", null) { Side = WallSide.Interior, Bearing = true } });
        Assert.Equal(interior, Scenes.Accept(SceneWriter.WriteToText(interior)));
        Assert.Contains("\"header\": null", SceneWriter.WriteToText(interior), StringComparison.Ordinal);
        Assert.Contains("\"bearing\": true", SceneWriter.WriteToText(interior), StringComparison.Ordinal);
        Sketch unsaid = sketch.WithEntity(wall with { WallInputs = new WallInputs("x", null) });
        Assert.Contains("\"side\": null", SceneWriter.WriteToText(unsaid), StringComparison.Ordinal);
        Assert.Equal(unsaid, Scenes.Accept(SceneWriter.WriteToText(unsaid)));
    }

    [Theory]
    [InlineData("\"phase\": \"existing\"", "\"phase\": \"changed\"", LoadProblemKind.UnknownValue, "changed")]
    [InlineData("\"phase\": \"existing\",", "", LoadProblemKind.MissingField, "phase")]
    [InlineData("\"side\": \"exterior\"", "\"side\": \"outside\"", LoadProblemKind.UnknownValue, "outside")]
    [InlineData("\"bearing\": false", "\"bearing\": \"no\"", LoadProblemKind.Malformed, "bearing")]
    [InlineData("\"plies\": 2", "\"plies\": 4", LoadProblemKind.InvalidValue, "plies")]
    [InlineData("\"plies\": 2", "\"plies\": 0", LoadProblemKind.InvalidValue, "plies")]
    [InlineData("\"lumber\": \"2x6\"", "\"lumber\": \"\"", LoadProblemKind.InvalidValue, "lumber")]
    [InlineData("\"drywall\": \"walls-and-ceiling\"", "\"drywall\": \"ceiling\"", LoadProblemKind.UnknownValue, "ceiling")]
    [InlineData("\"paint\": \"walls\"", "\"paint\": \"trim\"", LoadProblemKind.UnknownValue, "trim")]
    [InlineData("\"insulation\": \"exterior\"", "\"insulation\": \"attic\"", LoadProblemKind.UnknownValue, "attic")]
    [InlineData("\"insulationBy\": \"area\"", "\"insulationBy\": \"bags\"", LoadProblemKind.UnknownValue, "bags")]
    [InlineData("\"insulationCoverage\": 40", "\"insulationCoverage\": 0", LoadProblemKind.InvalidValue, "insulationCoverage")]
    [InlineData("\"paintCoverage\": 350", "\"paintCoverage\": -350", LoadProblemKind.InvalidValue, "paintCoverage")]
    [InlineData("\"paintCoats\": 2", "\"paintCoats\": 0", LoadProblemKind.InvalidValue, "paintCoats")]
    [InlineData("\"flooringBox\": 20", "\"flooringBox\": 0", LoadProblemKind.InvalidValue, "flooringBox")]
    [InlineData("\"flooringWaste\": 10", "\"flooringWaste\": -1", LoadProblemKind.InvalidValue, "flooringWaste")]
    [InlineData("\"flooring\": true", "\"flooring\": 1", LoadProblemKind.Malformed, "flooring")]
    [InlineData("\"baseboardStick\": 98304", "\"baseboardStick\": 0", LoadProblemKind.InvalidValue, "baseboardStick")]
    [InlineData("\"sheet\": { \"width\": 49152", "\"sheet\": { \"width\": 0", LoadProblemKind.InvalidValue, "width")]
    [InlineData("\"length\": 98304 }", "\"length\": 0 }", LoadProblemKind.InvalidValue, "length")]
    [InlineData("\"south\": 172032", "\"south\": 0", LoadProblemKind.InvalidValue, "south")]
    [InlineData("\"diagonal2\": null }", "\"diagonal2\": null, \"diagonal3\": 5 }", LoadProblemKind.UnknownField, "diagonal3")]
    [InlineData("\"flooringBox\": 20,", "\"flooringBox\": 20, \"colour\": \"grey\",", LoadProblemKind.UnknownField, "colour")]
    [InlineData("\"lumber\": \"2x6\" }", "\"lumber\": \"2x6\", \"span\": 1 }", LoadProblemKind.UnknownField, "span")]
    [InlineData("\"symbol\": \"outlet\"", "\"symbol\": \"socket\"", LoadProblemKind.UnknownValue, "socket")]
    [InlineData("\"text\": \"outlet\", \"symbol\": \"outlet\"", "\"text\": \"\", \"symbol\": \"none\"", LoadProblemKind.InvalidValue, "text")]
    [InlineData("\"text\": \"outlet\", ", "", LoadProblemKind.MissingField, "text")]
    [InlineData("\"part\": null, \"wall\": null,", "\"part\": null, \"wall\": null, \"extra\": 1,", LoadProblemKind.UnknownField, "extra")]
    public void A_malformed_renovation_field_is_refused_naming_it(string original, string replacement, LoadProblemKind kind, string named)
        => Scenes.RefuseWith(Scene.With(original, replacement), kind, named);

    [Fact]
    public void A_box_that_is_a_room_is_not_also_a_part_or_a_wall()
    {
        Scenes.RefuseWith(
            Scene.With("\"part\": null, \"wall\": null,", "\"part\": null, \"wall\": { \"supports\": \"x\", \"studSpacing\": null, \"bracing\": null, \"side\": null, \"bearing\": null, \"header\": null },"),
            LoadProblemKind.InvalidValue,
            "room",
            "not also a part or a wall");

        Scenes.RefuseWith(
            Scene.With("\"part\": null, \"wall\": null,", "\"part\": { \"stock\": null, \"species\": null, \"quantity\": 1, \"planAxes\": { \"x\": \"length\", \"y\": \"width\" }, \"hardware\": [], \"rough\": false }, \"wall\": null,"),
            LoadProblemKind.InvalidValue,
            "room");
    }

    [Fact]
    public void A_wall_with_nothing_said_writes_null_and_an_object_of_nulls_is_refused()
    {
        Scenes.RefuseWith(
            Scene.With("\"side\": \"exterior\", \"bearing\": false,", "\"side\": null, \"bearing\": null,")
                .With("\"header\": { \"plies\": 2, \"lumber\": \"2x6\" }", "\"header\": null"),
            LoadProblemKind.InvalidValue,
            "wall");
    }

    [Fact]
    public void A_symbol_note_may_say_nothing()
    {
        Sketch sketch = Scenes.Accept(Scene.With("\"text\": \"outlet\"", "\"text\": \"\""));
        Assert.Equal(string.Empty, sketch.Find<Note>(NoteId)!.Text);
    }

    [Fact]
    public void A_version_9_file_is_refused_naming_both_versions()
    {
        Scenes.RefuseWith(
            Scene.With("\"formatVersion\": 11", "\"formatVersion\": 9"),
            LoadProblemKind.UnsupportedFormatVersion,
            "format version 9",
            "format version 11");
    }
}
