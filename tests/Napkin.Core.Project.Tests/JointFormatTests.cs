using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Format version 5 (docs/design/joinery-and-fasteners.md &#xA7;4.4, &#xA7;7.3, &#xA7;7.5, &#xA7;8): the
/// <c>joint</c> relationship, a part's <c>hardware</c>, the root's <c>fastenerChoices</c> and
/// <c>supplies</c>. The scene below is written out by hand, in inches x 1024; nothing in it comes
/// from napkin's own output.
/// </summary>
public class JointFormatTests
{
    private const string Leg = "0192f1a0-0000-4000-8000-00000000000a";
    private const string Apron = "0192f1a0-0000-4000-8000-00000000000b";
    private const string JointId = "0192f1a0-0000-4000-8000-00000000001a";

    // Leg: 2 x 2 x 16 in at the origin, x 0..2. Apron: 10 x 2 x 4 in at (2, 0, 10), x 2..12. The leg's east
    // face and the apron's west face are the plane x = 2, and they share a 2 in by 4 in rectangle.
    private const string Joined = """
        {
          "formatVersion": 10,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Leg", "phase": "new",
              "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 2048, "height": 2048, "depth": 16384, "faceUp": "top", "rotation": 0,
              "part": { "stock": "2x2", "species": null, "quantity": 1,
                        "planAxes": { "x": "width", "y": "thickness" }, "hardware": [], "rough": false }, "wall": null, "room": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Apron", "phase": "new",
              "anchor": { "x": 2048, "y": 0, "z": 10240 }, "width": 10240, "height": 2048, "depth": 4096, "faceUp": "top", "rotation": 0,
              "part": { "stock": "1x6", "species": null, "quantity": 1,
                        "planAxes": { "x": "length", "y": "thickness" },
                        "hardware": [ { "name": "16 in side-mount drawer slide, pair", "quantity": 1 } ], "rough": false }, "wall": null, "room": null, "cuts": [] }
          ],
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "joint", "type": "butt",
              "receiving": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["east"] },
              "inserted": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["west"] },
              "depth": null,
              "fastening": { "kind": "pocketScrews", "count": 3, "pocketFace": "south" },
              "glue": true }
          ],
          "fastenerChoices": [
            { "kind": "pocketScrew", "thickness": 768, "size": "1-1/4 in coarse", "packSize": 100 },
            { "kind": "tabletopClip", "thickness": null, "size": "figure-8, with screws", "packSize": null }
          ],
          "supplies": [ { "item": "Wood glue", "note": "" } ],
          "code": null,
          "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null }
        }
        """;

    private static string Variant(params (string Original, string Replacement)[] changes)
    {
        string json = Joined;
        foreach ((string original, string replacement) in changes)
        {
            json = json.With(original, replacement);
        }

        return json;
    }

    private static Joint TheJoint(Sketch sketch) => Assert.IsType<Joint>(Assert.Single(sketch.Relationships.Values));

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_joint_and_the_joinery_lists_load_as_written()
    {
        Sketch sketch = Scenes.Accept(Joined);

        Joint joint = TheJoint(sketch);
        Assert.Equal(JointType.Butt, joint.Type);
        Assert.Equal(new FeatureRef(new EntityId(Guid.Parse(Leg)), BoxFeature.Face(BoxFace.East)), joint.Receiving);
        Assert.Equal(new FeatureRef(new EntityId(Guid.Parse(Apron)), BoxFeature.Face(BoxFace.West)), joint.Inserted);
        Assert.Null(joint.Depth);
        Assert.Equal(new Fastening(FasteningKind.PocketScrews, 3, BoxFace.South), joint.Fastening);
        Assert.True(joint.Glue);

        Part apron = sketch.Find<Box>(new EntityId(Guid.Parse(Apron)))!.Part!;
        Assert.Equal([new HardwareItem("16 in side-mount drawer slide, pair", 1)], apron.Hardware);
        Assert.Empty(sketch.Find<Box>(new EntityId(Guid.Parse(Leg)))!.Part!.Hardware);

        Assert.Equal(
            [
                new FastenerChoice(FastenerKind.PocketScrew, new Length(768), "1-1/4 in coarse", 100),
                new FastenerChoice(FastenerKind.TabletopClip, null, "figure-8, with screws", null),
            ],
            sketch.FastenerChoices);
        Assert.Equal([new SupplyLine("Wood glue", string.Empty)], sketch.Supplies);
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_joint_round_trips_byte_for_byte_and_the_writer_spells_it_the_way_the_note_does()
    {
        Sketch sketch = Scenes.Accept(Joined);

        string text = SceneWriter.WriteToText(sketch);
        Sketch again = Scenes.Accept(text);

        Assert.Equal(sketch, again);
        Assert.Equal(text, SceneWriter.WriteToText(again));
        Assert.Contains("\"kind\": \"joint\"", text, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"butt\"", text, StringComparison.Ordinal);
        Assert.Contains("\"depth\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"pocketScrews\"", text, StringComparison.Ordinal);
        Assert.Contains("\"count\": 3", text, StringComparison.Ordinal);
        Assert.Contains("\"pocketFace\": \"south\"", text, StringComparison.Ordinal);
        Assert.Contains("\"glue\": true", text, StringComparison.Ordinal);
        Assert.Contains("\"fastenerChoices\": [", text, StringComparison.Ordinal);
        Assert.Contains("\"thickness\": null", text, StringComparison.Ordinal);
        Assert.Contains("\"packSize\": null", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void Every_type_and_fastening_the_format_spells_round_trips()
    {
        foreach ((string type, string fastening, string depth) in new[]
        {
            ("groove", "\"none\", \"count\": null, \"pocketFace\": null", "256"),
            ("rabbet", "\"brads\", \"count\": 4, \"pocketFace\": null", "256"),
            ("halfLap", "\"dowels\", \"count\": null, \"pocketFace\": null", "null"),
            ("tabletop", "\"clips\", \"count\": 2, \"pocketFace\": null", "null"),
        })
        {
            string json = Variant(
                ("\"type\": \"butt\"", $"\"type\": \"{type}\""),
                ("\"depth\": null", $"\"depth\": {depth}"),
                ("\"pocketScrews\", \"count\": 3, \"pocketFace\": \"south\"", fastening));

            Sketch sketch = Scenes.Accept(json);

            Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));
        }
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_joint_whose_parts_have_drifted_apart_still_opens_and_is_unsatisfied()
    {
        // The apron at x 4..14: its west face is the plane x = 4, not x = 2. Position is geometry, not format.
        Sketch sketch = Scenes.Accept(Variant(("\"anchor\": { \"x\": 2048,", "\"anchor\": { \"x\": 4096,")));

        Assert.False(RelationshipChecker.IsSatisfied(sketch, TheJoint(sketch)));
    }

    [Theory]
    [Trait("Feature", "PRJ-008")]
    [InlineData("\"type\": \"butt\"", "\"type\": \"dado\"", LoadProblemKind.UnknownValue, "dado")]
    [InlineData("\"receiving\": { \"kind\": \"feature\"", "\"receiving\": { \"kind\": \"center\"", LoadProblemKind.UnknownValue, "receiving")]
    [InlineData("\"faces\": [\"east\"]", "\"faces\": [\"east\", \"north\"]", LoadProblemKind.InvalidValue, "\"receiving\" names 2 faces")]
    [InlineData("\"depth\": null", "\"depth\": 256", LoadProblemKind.InvalidValue, "\"depth\"")]
    [InlineData("\"depth\": null", "\"depth\": \"deep\"", LoadProblemKind.Malformed, "depth")]
    [InlineData("\"pocketScrews\", \"count\": 3", "\"glue\", \"count\": 3", LoadProblemKind.UnknownValue, "glue")]
    [InlineData("\"count\": 3", "\"count\": 0", LoadProblemKind.InvalidValue, "fastening.count")]
    [InlineData("\"count\": 3", "\"count\": -2", LoadProblemKind.InvalidValue, "fastening.count")]
    [InlineData("\"count\": 3", "\"count\": 2.5", LoadProblemKind.NotAnInteger, "count")]
    [InlineData("\"pocketFace\": \"south\"", "\"pocketFace\": \"west\"", LoadProblemKind.InvalidValue, "fastening.pocketFace")]
    [InlineData("\"pocketFace\": \"south\"", "\"pocketFace\": \"east\"", LoadProblemKind.InvalidValue, "fastening.pocketFace")]
    [InlineData("\"pocketFace\": \"south\"", "\"pocketFace\": \"up\"", LoadProblemKind.UnknownValue, "up")]
    [InlineData("\"glue\": true", "\"glue\": \"yes\"", LoadProblemKind.Malformed, "glue")]
    [InlineData("\"glue\": true", "\"glue\": true, \"colour\": \"red\"", LoadProblemKind.UnknownField, "colour")]
    [InlineData("\"depth\": null,", "", LoadProblemKind.MissingField, "depth")]
    [InlineData("\"glue\": true", "\"glued\": true", LoadProblemKind.MissingField, "glue")]
    [InlineData("\"planAxes\": { \"x\": \"width\", \"y\": \"thickness\" }, \"hardware\": [], ", "\"planAxes\": { \"x\": \"width\", \"y\": \"thickness\" }, ", LoadProblemKind.MissingField, "hardware")]
    [InlineData("\"supplies\": [ { \"item\": \"Wood glue\", \"note\": \"\" } ]", "\"supplyList\": []", LoadProblemKind.MissingField, "supplies")]
    [InlineData("\"fastenerChoices\": [", "\"fastenerChoice\": [", LoadProblemKind.MissingField, "fastenerChoices")]
    [InlineData("\"quantity\": 1 } ], \"rough\": false }, \"wall\"", "\"quantity\": 0 } ], \"rough\": false }, \"wall\"", LoadProblemKind.InvalidValue, "quantity")]
    [InlineData("\"name\": \"16 in side-mount drawer slide, pair\"", "\"name\": \"\"", LoadProblemKind.InvalidValue, "name")]
    [InlineData("\"item\": \"Wood glue\"", "\"item\": \"\"", LoadProblemKind.InvalidValue, "item")]
    [InlineData("\"kind\": \"pocketScrew\", \"thickness\": 768", "\"kind\": \"screwdriver\", \"thickness\": 768", LoadProblemKind.UnknownValue, "screwdriver")]
    [InlineData("\"thickness\": 768", "\"thickness\": 0", LoadProblemKind.InvalidValue, "thickness")]
    [InlineData("\"packSize\": 100", "\"packSize\": 0", LoadProblemKind.InvalidValue, "packSize")]
    [InlineData("\"kind\": \"tabletopClip\", \"thickness\": null", "\"kind\": \"pocketScrew\", \"thickness\": 768", LoadProblemKind.DuplicateId, "same kind")]
    public void A_malformed_joint_or_joinery_list_is_refused_naming_the_field(
        string original, string replacement, LoadProblemKind kind, string named)
    {
        Scenes.RefuseWith(Variant((original, replacement)), kind, named);
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_pair_outside_the_allowed_table_is_refused_naming_the_fastening()
    {
        // A groove held with pocket screws: not in section 3.3's table.
        string json = Variant(("\"type\": \"butt\"", "\"type\": \"groove\""), ("\"depth\": null", "\"depth\": 256"));

        Scenes.RefuseWith(json, LoadProblemKind.InvalidValue, "fastening.kind", "PocketScrews");
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_joint_between_faces_that_could_never_touch_is_refused_the_way_a_flush_is()
    {
        // The leg's east face fixes x; the apron's top face fixes z.
        string json = Variant(("\"faces\": [\"west\"]", "\"faces\": [\"top\"]"), ("\"pocketFace\": \"south\"", "\"pocketFace\": \"east\""));

        Scenes.RefuseWith(json, LoadProblemKind.InvalidValue, "fixing exactly one axis, the same one");
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_joint_that_joins_a_part_to_itself_is_refused()
    {
        string json = Variant(("\"box\": \"0192f1a0-0000-4000-8000-00000000000b\", \"faces\": [\"west\"]", "\"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"west\"]"));

        Scenes.RefuseWith(json, LoadProblemKind.InvalidValue, "joins a part to itself");
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_count_on_no_fastening_is_refused()
    {
        string json = Variant(("\"pocketScrews\", \"count\": 3, \"pocketFace\": \"south\"", "\"none\", \"count\": 3, \"pocketFace\": null"));

        Scenes.RefuseWith(json, LoadProblemKind.InvalidValue, "fastening.count");
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_pocket_face_on_anything_but_pocket_screws_is_refused()
    {
        string json = Variant(("\"pocketScrews\", \"count\": 3", "\"screws\", \"count\": 3"));

        Scenes.RefuseWith(json, LoadProblemKind.InvalidValue, "fastening.pocketFace");
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_version_4_file_is_refused_with_the_unsupported_version_message_and_no_converter()
    {
        LoadProblem problem = Scenes.RefuseWith(
            Variant(("\"formatVersion\": 10", "\"formatVersion\": 4")),
            LoadProblemKind.UnsupportedFormatVersion,
            "format version 4",
            "format version 10");

        Assert.Contains("no migration", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Feature", "PRJ-008")]
    [InlineData("\"hardware\": [ { \"name\": \"16 in side-mount drawer slide, pair\", \"quantity\": 1 } ]", "\"hardware\": [ 5 ]", "hardware")]
    [InlineData("{ \"kind\": \"pocketScrew\", \"thickness\": 768, \"size\": \"1-1/4 in coarse\", \"packSize\": 100 },", "\"screw\",", "fastenerChoices")]
    [InlineData("\"supplies\": [ { \"item\": \"Wood glue\", \"note\": \"\" } ]", "\"supplies\": [ [] ]", "supplies")]
    [InlineData("\"receiving\": { \"kind\": \"feature\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"east\"] }", "\"receiving\": 5", "receiving")]
    [InlineData("\"fastening\": { \"kind\": \"pocketScrews\", \"count\": 3, \"pocketFace\": \"south\" }", "\"fastening\": \"brads\"", "fastening")]
    public void A_joinery_field_of_the_wrong_shape_is_refused_as_malformed(string original, string replacement, string named)
    {
        Scenes.RefuseWith(Variant((original, replacement)), LoadProblemKind.Malformed, named);
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void A_build_whose_updater_cannot_hold_a_joint_refuses_the_file_naming_the_kind()
    {
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(Joined));

        Refused refused = Assert.IsType<Refused>(SceneReader.Read(stream, new NoJoints()));

        Assert.Contains("\"joint\"", refused.Summary, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void Every_type_fastening_and_fastener_kind_has_one_spelling_each_way_and_no_other()
    {
        foreach (JointType type in Enum.GetValues<JointType>())
        {
            Assert.True(SceneNames.TryJointType(SceneNames.Of(type), out JointType back));
            Assert.Equal(type, back);
        }

        foreach (FasteningKind kind in Enum.GetValues<FasteningKind>())
        {
            Assert.True(SceneNames.TryFasteningKind(SceneNames.Of(kind), out FasteningKind back));
            Assert.Equal(kind, back);
        }

        foreach (FastenerKind kind in Enum.GetValues<FastenerKind>())
        {
            Assert.True(SceneNames.TryFastenerKind(SceneNames.Of(kind), out FastenerKind back));
            Assert.Equal(kind, back);
        }

        Assert.Equal(Enum.GetValues<JointType>().Length, SceneNames.JointTypes.Length);
        Assert.Equal(Enum.GetValues<FasteningKind>().Length, SceneNames.FasteningKinds.Length);
        Assert.Equal(Enum.GetValues<FastenerKind>().Length, SceneNames.FastenerKinds.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneNames.Of((JointType)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneNames.Of((FasteningKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneNames.Of((FastenerKind)99));
        Assert.False(SceneNames.TryJointType("dado", out _));
        Assert.False(SceneNames.TryFasteningKind("glue", out _));
        Assert.False(SceneNames.TryFastenerKind("screwdriver", out _));
    }

    [Fact]
    [Trait("Feature", "PRJ-008")]
    public void Every_kind_of_fastener_choice_round_trips()
    {
        Sketch sketch = Scenes.Accept(Joined) with
        {
            FastenerChoices =
            [
                .. Enum.GetValues<FastenerKind>().Select((kind, index) => new FastenerChoice(kind, new Length(256 * (index + 1)), $"size {index}", index + 1)),
            ],
        };

        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));
    }

    /// <summary>An updater that holds no joint, standing in for a build older than joinery.</summary>
    private sealed class NoJoints : IGeometryUpdater
    {
        public System.Collections.Immutable.ImmutableHashSet<Type> SupportedRelationships { get; }
            = System.Collections.Immutable.ImmutableHashSet<Type>.Empty;

        public UpdateResult Apply(Sketch sketch, Request request) => throw new NotSupportedException();
    }
}
