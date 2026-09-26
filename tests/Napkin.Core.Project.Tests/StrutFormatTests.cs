using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 11 (docs/design/angled-parts.md §7, §9.3 case 12): the strut entity and the
/// <c>strutEnd</c>, <c>strutFace</c> and <c>strutEndFace</c> references round-trip; each refusal
/// of §7 that slice A owns is hit by one malformed file; a version-10 file is refused naming both
/// versions. A <c>strutFace</c> in a <c>flush</c> and a <c>strutEndFace</c> in a <c>joint</c> are
/// slices B (#190) and E (#193).
/// </summary>
public class StrutFormatTests
{
    private const string Layer = "00000000-0000-0000-0000-000000000001";
    private const string SeatId = "0192f1a0-0000-4000-8000-00000000000a";
    private const string LegId = "0192f1a0-0000-4000-8000-00000000000b";
    private const string TwinId = "0192f1a0-0000-4000-8000-00000000000c";

    // The splayed bench's seat and its south-west leg (§9.1), and the leg's mirror across the seat's
    // width meeting it at the top: run 7″, rise 24″, 2x2 stock.
    private static readonly string Scene = $$"""
        {
          "formatVersion": 11,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "{{Layer}}", "name": "Default" } ],
          "entities": [
            { "id": "{{SeatId}}", "type": "box", "layer": "{{Layer}}", "name": "Seat", "phase": "new",
              "anchor": { "x": 0, "y": 0, "z": 24576 }, "width": 36864, "height": 12288, "depth": 768, "faceUp": "top", "rotation": 0,
              "part": null, "wall": null, "room": null, "cuts": [] },
            { "id": "{{LegId}}", "type": "strut", "layer": "{{Layer}}", "name": "Leg, south-west", "phase": "new",
              "from": { "x": 4096, "y": -4096, "z": 0 },
              "to": { "x": 4096, "y": 3072, "z": 24576 },
              "fromCut": "z", "toCut": "z",
              "reference": "z",
              "height": 1536, "depth": 1536,
              "part": { "stock": "2x2", "species": null, "quantity": 1, "planAxes": { "x": "length", "y": "width" }, "hardware": [], "rough": false } },
            { "id": "{{TwinId}}", "type": "strut", "layer": "{{Layer}}", "name": "", "phase": "existing",
              "from": { "x": 4096, "y": 10240, "z": 0 },
              "to": { "x": 4096, "y": 3072, "z": 24576 },
              "fromCut": "square", "toCut": "y",
              "reference": "x",
              "height": 1536, "depth": 768,
              "part": null }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "axisDistance",
              "from": { "kind": "feature", "box": "{{SeatId}}", "faces": ["bottom"] },
              "to": { "kind": "strutEnd", "strut": "{{LegId}}", "end": "to" },
              "axis": "z", "distance": 0 },
            { "id": "0192f1a0-0000-4000-8000-00000000001b", "kind": "coincident",
              "a": { "kind": "strutEnd", "strut": "{{LegId}}", "end": "to" },
              "b": { "kind": "strutEnd", "strut": "{{TwinId}}", "end": "to" } }
          ]
        }
        """;

    private static readonly EntityId Leg = new(new Guid(LegId));
    private static readonly EntityId Twin = new(new Guid(TwinId));

    [Fact]
    public void A_strut_loads_as_written()
    {
        Sketch sketch = Scenes.Accept(Scene);

        Strut leg = sketch.Find<Strut>(Leg)!;
        Assert.Equal(
            new Strut(
                Leg, new LayerId(new Guid(Layer)), new Point3(new Length(4096), new Length(-4096), Length.Zero),
                new Point3(new Length(4096), new Length(3072), new Length(24576)), EndCut.Z, EndCut.Z, Axis.Z, new Length(1536), new Length(1536))
            {
                Name = "Leg, south-west",
                Part = new Part("2x2", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
            },
            leg);
        Assert.Equal(new Length(26048), leg.Blank().Length.Value);
        Assert.Equal(Phase.Existing, sketch.Find<Strut>(Twin)!.Phase);
        Assert.Contains(sketch.Relationships.Values, r => r is Coincident { A: StrutEndRef { End: StrutEnd.To } });
    }

    [Fact]
    public void Every_strut_field_and_reference_survives_a_round_trip()
    {
        Sketch sketch = Scenes.Accept(Scene);
        string text = SceneWriter.WriteToText(sketch);

        Assert.Equal(sketch, Scenes.Accept(text));
        Assert.Contains("\"type\": \"strut\"", text, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"strutEnd\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("blank", text, StringComparison.OrdinalIgnoreCase);

        // Every cut, reference axis and end has one spelling each way.
        Strut twin = sketch.Find<Strut>(Twin)!;
        foreach (EndCut cut in Enum.GetValues<EndCut>())
        {
            foreach (Axis axis in Enum.GetValues<Axis>())
            {
                Sketch with = sketch.WithEntity(twin with { FromCut = cut, Reference = axis, Part = new Part(null, null, 2, new PlanAxes(PartDimension.Width, PartDimension.Thickness)) });
                if (with.Validate().IsValid)
                {
                    Assert.Equal(with, Scenes.Accept(SceneWriter.WriteToText(with)));
                }
            }
        }
    }

    [Fact]
    public void A_flush_to_a_strut_face_an_anchored_strut_and_its_sizes_round_trip()
    {
        // The leg leans one way (d = (0, 7168, 24576), reference Z), so its top face is x = 4096 + 768
        // = 4864; a stretcher's west face there is flush with it.
        const string StretcherId = "0192f1a0-0000-4000-8000-00000000000d";
        string held = Scene
            .With(
                "\"entities\": [",
                "\"entities\": [ "
                + $$"""{ "id": "{{StretcherId}}", "type": "box", "layer": "{{Layer}}", "name": "Stretcher", "phase": "new", "anchor": { "x": 4864, "y": 0, "z": 6144 }, "width": 6144, "height": 768, "depth": 3584, "faceUp": "top", "rotation": 0, "part": null, "wall": null, "room": null, "cuts": [] },""")
            .With(
                "\"relationships\": [",
                "\"relationships\": [ "
                + $$"""{ "id": "0192f1a0-0000-4000-8000-00000000001c", "kind": "flush", "a": { "kind": "feature", "box": "{{StretcherId}}", "faces": ["west"] }, "b": { "kind": "strutFace", "strut": "{{LegId}}", "face": "top" } },"""
                + $$"""{ "id": "0192f1a0-0000-4000-8000-00000000001d", "kind": "anchored", "entity": "{{TwinId}}" },"""
                + $$"""{ "id": "0192f1a0-0000-4000-8000-00000000001e", "kind": "paramValue", "param": { "kind": "strutHeight", "strut": "{{LegId}}" }, "value": 1536 },"""
                + $$"""{ "id": "0192f1a0-0000-4000-8000-00000000001f", "kind": "equalParam", "a": { "kind": "strutDepth", "strut": "{{LegId}}" }, "b": { "kind": "strutHeight", "strut": "{{TwinId}}" } },""");

        Sketch sketch = Scenes.Accept(held);
        string text = SceneWriter.WriteToText(sketch);

        Assert.Equal(sketch, Scenes.Accept(text));
        Assert.Contains(sketch.Relationships.Values, r => r is Flush { B: StrutFaceRef { Face: StrutFace.Top } });
        Assert.Contains("\"kind\": \"strutHeight\"", text, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"strutDepth\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_strut_face_outside_a_flush_and_an_end_face_are_refused_by_the_kernel()
    {
        string face = Scene.With(
            "\"b\": { \"kind\": \"strutEnd\", \"strut\": \"" + TwinId + "\", \"end\": \"to\" }",
            "\"b\": { \"kind\": \"strutFace\", \"strut\": \"" + TwinId + "\", \"face\": \"north\" }");
        LoadProblem problem = Scenes.RefuseWith(face, LoadProblemKind.InvalidValue, "only a flush can hold");
        Assert.Contains("north face", problem.Message, StringComparison.Ordinal);

        string endFace = Scene.With(
            "\"b\": { \"kind\": \"strutEnd\", \"strut\": \"" + TwinId + "\", \"end\": \"to\" }",
            "\"b\": { \"kind\": \"strutEndFace\", \"strut\": \"" + TwinId + "\", \"end\": \"to\" }");
        Scenes.RefuseWith(endFace, LoadProblemKind.InvalidValue, "joint");

        // The references themselves are written, whatever holds them.
        Sketch sketch = Sketch.Empty.WithEntity(Scenes.Accept(Scene).Find<Strut>(Leg)!);
        foreach (PlaceRef place in new PlaceRef[] { new StrutFaceRef(Leg, StrutFace.Bottom), new StrutEndFaceRef(Leg, StrutEnd.From) })
        {
            Sketch with = sketch.WithRelationship(new Coincident(new RelationshipId(Guid.NewGuid()), place, new StrutEndRef(Leg, StrutEnd.To)));
            string text = SceneWriter.WriteToText(with);
            Assert.Contains(place is StrutFaceRef ? "\"face\": \"bottom\"" : "\"kind\": \"strutEndFace\"", text, StringComparison.Ordinal);
        }
    }

    private static string WithJoint(string joint) => Scene.With("\"relationships\": [", "\"relationships\": [ " + joint + ",");

    private static string LegOnSeat(string type = "butt", string pocketFace = "bottom", string receiving = "{ \"kind\": \"feature\", \"box\": \"" + SeatId + "\", \"faces\": [\"bottom\"] }", string inserted = "{ \"kind\": \"strutEndFace\", \"strut\": \"" + LegId + "\", \"end\": \"to\" }")
        => "{ \"id\": \"0192f1a0-0000-4000-8000-00000000002a\", \"kind\": \"joint\", \"type\": \"" + type + "\", "
           + "\"receiving\": " + receiving + ", \"inserted\": " + inserted + ", \"depth\": null, "
           + "\"fastening\": { \"kind\": \"pocketScrews\", \"count\": null, \"pocketFace\": \"" + pocketFace + "\" }, \"glue\": true }";

    [Fact]
    public void A_leg_pocket_screwed_under_the_seat_round_trips()
    {
        // angled-parts §7: a joint whose inserted face is a strutEndFace; the pocket face is one of the
        // strut's long faces, spelled as a box's face of the same name.
        Sketch sketch = Scenes.Accept(WithJoint(LegOnSeat()));
        StrutJoint joint = Assert.Single(sketch.Relationships.Values.OfType<StrutJoint>());

        Assert.Equal((StrutFace.Bottom, FasteningKind.PocketScrews, true), (joint.PocketFrom!.Value, joint.Fastening.Kind, joint.Glue));
        Assert.Equal(new StrutEndFaceRef(Leg, StrutEnd.To), joint.Inserted);
        string text = SceneWriter.WriteToText(sketch);
        Assert.Equal(sketch, Scenes.Accept(text));
        Assert.Contains("\"pocketFace\": \"bottom\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("groove", "bottom", "butt with no depth")]
    [InlineData("butt", "east", "south, north, bottom or top")]
    public void A_strut_joint_that_is_not_a_butt_from_a_long_face_is_refused(string type, string face, string mustName)
        => Scenes.RefuseWith(WithJoint(LegOnSeat(type, face)), LoadProblemKind.InvalidValue, mustName);

    [Fact]
    public void A_strut_end_is_never_the_receiving_face_of_a_box()
    {
        string swapped = LegOnSeat(
            receiving: "{ \"kind\": \"strutEndFace\", \"strut\": \"" + LegId + "\", \"end\": \"to\" }",
            inserted: "{ \"kind\": \"feature\", \"box\": \"" + SeatId + "\", \"faces\": [\"bottom\"] }");
        Scenes.RefuseWith(WithJoint(swapped), LoadProblemKind.InvalidValue, "inserted face");
    }

    [Fact]
    public void A_joint_face_of_another_kind_is_refused_naming_both()
        => Scenes.RefuseWith(
            WithJoint(LegOnSeat(inserted: "{ \"kind\": \"strutEnd\", \"strut\": \"" + LegId + "\", \"end\": \"to\" }")),
            LoadProblemKind.UnknownValue,
            "strutEndFace");

    [Fact]
    public void A_strut_has_no_length_to_hold()
    {
        string length = Scene.With(
            "\"relationships\": [",
            "\"relationships\": [ "
            + $$"""{ "id": "0192f1a0-0000-4000-8000-00000000001c", "kind": "paramValue", "param": { "kind": "strutLength", "strut": "{{LegId}}" }, "value": 26048 },""");
        Scenes.RefuseWith(length, LoadProblemKind.UnknownValue, "strutLength");
    }

    [Fact]
    public void A_flush_naming_a_strut_end_is_refused()
    {
        string flush = Scene.With(
            "\"kind\": \"coincident\"",
            "\"kind\": \"flush\"");
        Scenes.RefuseWith(flush, LoadProblemKind.InvalidValue, "flush");
    }

    [Theory]
    [InlineData("\"fromCut\": \"z\"", "\"fromCut\": \"w\"", LoadProblemKind.UnknownValue, "end cut")]
    [InlineData("\"reference\": \"z\"", "\"reference\": \"square\"", LoadProblemKind.UnknownValue, "reference axis")]
    [InlineData("\"height\": 1536, \"depth\": 1536", "\"height\": 0, \"depth\": 1536", LoadProblemKind.InvalidValue, "height")]
    [InlineData("\"height\": 1536, \"depth\": 1536", "\"height\": 1536, \"depth\": -1", LoadProblemKind.InvalidValue, "depth")]
    [InlineData("\"reference\": \"z\",", "", LoadProblemKind.MissingField, "reference")]
    [InlineData("\"from\": { \"x\": 4096, \"y\": -4096, \"z\": 0 },", "", LoadProblemKind.MissingField, "from")]
    [InlineData("\"planAxes\": { \"x\": \"length\", \"y\": \"width\" }", "\"planAxes\": { \"x\": \"thickness\", \"y\": \"width\" }", LoadProblemKind.InvalidValue, "thickness")]
    [InlineData("\"from\": { \"x\": 4096, \"y\": -4096, \"z\": 0 }", "\"from\": { \"x\": 4096, \"y\": 3072, \"z\": 0 }", LoadProblemKind.InvalidValue, "axis")]
    public void Each_strut_refusal_names_what_is_wrong(string original, string replacement, LoadProblemKind kind, string mustName)
        => Scenes.RefuseWith(Scene.With(original, replacement), kind, mustName);

    [Fact]
    public void A_strut_too_short_for_its_cuts_is_refused()
    {
        // Assembly-model case 28: a 2x4 at 45° spanning 2″ each way between a floor and a wall.
        string stub = Scene
            .With("\"from\": { \"x\": 4096, \"y\": -4096, \"z\": 0 }", "\"from\": { \"x\": 4096, \"y\": 1024, \"z\": 22528 }")
            .With("\"fromCut\": \"z\", \"toCut\": \"z\"", "\"fromCut\": \"z\", \"toCut\": \"y\"")
            .With("\"height\": 1536, \"depth\": 1536", "\"height\": 3584, \"depth\": 1536");
        Scenes.RefuseWith(stub, LoadProblemKind.InvalidValue, "too short");
    }

    [Fact]
    public void A_version_10_file_is_refused_naming_both_versions()
    {
        Scenes.RefuseWith(
            Scene.With("\"formatVersion\": 11", "\"formatVersion\": 10"),
            LoadProblemKind.UnsupportedFormatVersion,
            "format version 10",
            "format version 11");
    }
}
