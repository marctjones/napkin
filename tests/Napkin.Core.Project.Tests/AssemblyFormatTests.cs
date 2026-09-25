using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Scene format version 4, docs/design/assembly-model.md &#xA7;10 step 5 and &#xA7;9.1 case 21: a
/// box in space — <c>anchor.z</c>, <c>depth</c>, <c>faceUp</c> — and the <c>feature</c> reference
/// that replaced <c>corner</c> and <c>boxEdge</c>. The round trip over every face-up and every
/// reference shape, and each refusal on its own, named.
/// </summary>
public sealed class AssemblyFormatTests
{
    private const string Left = "0192f1a0-0000-4000-8000-00000000000a";
    private const string Right = "0192f1a0-0000-4000-8000-00000000000b";

    /// <summary>
    /// Two boxes in space and the relationships §2.3 allows between them. Left is 10" × 4" × 3",
    /// top up, 2" off the floor: its top face is at z = 2048 + 3072 = 5120. Right stands on that
    /// face — its anchor z is 5120 — and runs on east from Left's east face at x = 10240. So Left's
    /// east face and Right's west face share x = 10240, Left's top and Right's bottom share
    /// z = 5120, and Right's top south-west corner, (10240, 0, 5120 + 1024 = 6144), is 1024 above
    /// Left's top face.
    /// </summary>
    private const string TwoBoxesInSpace = """
        {
          "formatVersion": 9,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Left",
              "anchor": { "x": 0, "y": 0, "z": 2048 }, "width": 10240, "height": 4096, "depth": 3072, "faceUp": "top", "rotation": 0,
              "part": null, "wall": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Right",
              "anchor": { "x": 10240, "y": 0, "z": 5120 }, "width": 10240, "height": 4096, "depth": 1024, "faceUp": "top", "rotation": 0,
              "part": { "stock": null, "species": null, "quantity": 1, "planAxes": { "x": "length", "y": "width" }, "hardware": [], "rough": false },
              "wall": null, "cuts": [] },
            { "id": "0192f1a0-0000-4000-8000-00000000000d", "type": "dimension", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Left width",
              "measures": { "kind": "boxWidth", "box": "0192f1a0-0000-4000-8000-00000000000a" },
              "drives": null,
              "placement": { "offset": 2048, "side": "south" } }
          ],
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
          "relationships": [
            { "id": "0192f1a0-0000-4000-8000-00000000001a", "kind": "flush",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["east"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["west"] } },
            { "id": "0192f1a0-0000-4000-8000-00000000001b", "kind": "flush",
              "a": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["top"] },
              "b": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["bottom"] } },
            { "id": "0192f1a0-0000-4000-8000-00000000001c", "kind": "axisDistance",
              "from": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000a", "faces": ["top"] },
              "to": { "kind": "feature", "box": "0192f1a0-0000-4000-8000-00000000000b", "faces": ["south", "west", "top"] },
              "axis": "z", "distance": 1024 },
            { "id": "0192f1a0-0000-4000-8000-00000000001d", "kind": "paramValue",
              "param": { "kind": "boxDepth", "box": "0192f1a0-0000-4000-8000-00000000000b" }, "value": 1024 }
          ]
        }
        """;

    private static EntityId IdOf(string id) => new(Guid.Parse(id));

    // -----------------------------------------------------------------------------------------
    // What the format says, read both ways
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void A_box_in_space_and_its_features_are_read_as_the_file_spells_them()
    {
        Sketch sketch = Scenes.Accept(TwoBoxesInSpace);

        Box left = sketch.Find<Box>(IdOf(Left))!;
        Assert.Equal(new Point3(Length.Zero, Length.Zero, new Length(2048)), left.Anchor);
        Assert.Equal(new Length(3072), left.Depth);
        Assert.Equal(BoxFace.Top, left.FaceUp);

        Box right = sketch.Find<Box>(IdOf(Right))!;
        Assert.Equal(new Length(5120), right.Anchor.Z);
        Assert.Equal(new Length(1024), right.Depth);

        // The part is three sizes of the box, the third its depth: nothing is stored twice.
        Assert.Equal(new Length(1024), right.Part!.SizeOn(right).Thickness);

        AxisDistance rise = Assert.Single(sketch.Relationships.Values.OfType<AxisDistance>());
        Assert.Equal(Axis.Z, rise.Axis);
        Assert.Equal(new FeatureRef(IdOf(Left), BoxFeature.Face(BoxFace.Top)), rise.From);
        Assert.Equal(
            new FeatureRef(IdOf(Right), BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top)),
            rise.To);

        Assert.Contains(
            new Flush(
                new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-00000000001b")),
                new FeatureRef(IdOf(Left), BoxFeature.Face(BoxFace.Top)),
                new FeatureRef(IdOf(Right), BoxFeature.Face(BoxFace.Bottom))),
            sketch.Relationships.Values);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void The_file_spells_a_box_in_space_the_way_it_is_read()
    {
        Sketch sketch = Scenes.Accept(TwoBoxesInSpace);
        string written = SceneWriter.WriteToText(sketch);

        Assert.Contains("\"z\": 5120", written, StringComparison.Ordinal);
        Assert.Contains("\"depth\": 1024,\n      \"faceUp\": \"top\",\n      \"rotation\": 0", written, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"feature\"", written, StringComparison.Ordinal);
        Assert.Contains("\"faces\": [\n          \"south\",\n          \"west\",\n          \"top\"\n        ]", written, StringComparison.Ordinal);
        Assert.Contains("\"axis\": \"z\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("outOfPlane", written, StringComparison.Ordinal);

        Assert.Equal(sketch, Scenes.Accept(written));
    }

    /// <summary>
    /// Case 21's round trip, built by hand rather than generated: a box for every face-up, each at
    /// its own height and depth, and every reference shape of §2.2 — a node, a segment, a centre,
    /// and a feature of one, two and three faces — with a span along Z among them.
    /// </summary>
    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Load_of_save_is_the_sketch_for_every_face_up_and_every_reference_shape()
    {
        Sketch sketch = Sketch.Empty;
        List<Box> boxes = [];
        int index = 0;
        foreach (BoxFace up in Enum.GetValues<BoxFace>())
        {
            Box box = new(
                EntityIdAt(++index),
                LayerId.Default,
                new Point3(new Length(20480 * index), new Length(-1024 * index), new Length(768 * index - 2048)),
                new Length(4096 + index),
                new Length(2048 + index),
                new Length(1024 + (256 * index)),
                up,
                new Angle(Angle.RightAngleArcseconds * (index % 4)))
            {
                Name = $"Box {index}",
                Part = index % 2 == 0 ? null : new Part("2x4", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
            };

            boxes.Add(box);
            sketch = sketch.WithEntity(box);
        }

        // A node meets a local upright of the top-up box, which stands along world Z and so fixes
        // X and Y, as a plan corner always has. (On the south-up box the same upright lies along
        // world Y and fixes X and Z, which no node can meet.)
        Box topUp = boxes.Single(box => box.FaceUp == BoxFace.Top);
        FeatureRef upright = new(topUp.Id, BoxFeature.LocalUpright(BoxCorner.NorthEast));
        Place uprightPlace = sketch.PlaceOf(upright);
        Node node = new(EntityIdAt(20), LayerId.Default, new Point2(uprightPlace[Axis.X], uprightPlace[Axis.Y]));

        // A horizontal segment along the top-up box's south face.
        Place south = sketch.PlaceOf(new FeatureRef(topUp.Id, BoxFeature.Face(BoxFace.South)));
        Node west = new(EntityIdAt(21), LayerId.Default, new Point2(new Length(-4096), south[Axis.Y]));
        Node east = new(EntityIdAt(22), LayerId.Default, new Point2(new Length(4096), south[Axis.Y]));
        Segment rail = new(EntityIdAt(23), LayerId.Default, west.Id, east.Id);
        sketch = sketch.WithEntity(node).WithEntity(west).WithEntity(east).WithEntity(rail);

        Box first = boxes[0];
        Box second = boxes[1];
        Box last = boxes[^1];
        PlaceRef face = new FeatureRef(first.Id, BoxFeature.Face(first.FaceUp));
        PlaceRef edge = new FeatureRef(second.Id, BoxFeature.Edge(second.FaceUp, BoxFace.Bottom));
        PlaceRef vertex = new FeatureRef(last.Id, BoxFeature.Vertex(last.FaceUp, BoxFace.South, BoxFace.West));
        PlaceRef centre = new CenterRef(second.Id);

        sketch = sketch
            .WithRelationship(new Coincident(RelationshipIdAt(1), new NodeRef(node.Id), upright))
            .WithRelationship(new Flush(RelationshipIdAt(2), new SegmentRef(rail.Id), new FeatureRef(topUp.Id, BoxFeature.Face(BoxFace.South))))
            .WithRelationship(new AxisDistance(RelationshipIdAt(3), face, vertex, Axis.Z, sketch.PlaceOf(vertex)[Axis.Z] - sketch.PlaceOf(face)[Axis.Z]))
            .WithRelationship(new AxisDistance(RelationshipIdAt(4), edge, centre, Axis.Z, sketch.PlaceOf(centre)[Axis.Z] - sketch.PlaceOf(edge)[Axis.Z]))
            .WithRelationship(new ParamValue(RelationshipIdAt(5), new BoxDepthRef(last.Id), last.Depth))
            .WithRelationship(new Anchored(RelationshipIdAt(6), first.Id));

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.True(RelationshipChecker.Check(sketch).AllHold, RelationshipChecker.Check(sketch).ToString());

        byte[] written = SceneWriter.WriteToBytes(sketch);
        using MemoryStream stream = new(written);
        Sketch read = Assert.IsType<Loaded>(SceneReader.Read(stream)).Sketch;

        Assert.Equal(sketch, read);
        Assert.Equal(written, SceneWriter.WriteToBytes(read));
        Assert.Equal(6, read.Entities.Values.OfType<Box>().Select(box => box.FaceUp).Distinct().Count());
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_box_turned_off_the_plan_saves_in_a_project_and_opens_again()
    {
        // Until version 4 this was refused at save: version 3 had nowhere to say it.
        Sketch sketch = Scenes.Accept(TwoBoxesInSpace);
        Box right = sketch.Find<Box>(IdOf(Right))!;
        Sketch turned = sketch
            .WithoutRelationship(new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-00000000001a")))
            .WithoutRelationship(new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-00000000001b")))
            .WithoutRelationship(new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-00000000001c")))
            .WithEntity(right with { FaceUp = BoxFace.North, Anchor = right.Anchor with { Z = new Length(-3072) } });

        using MemoryStream stream = new();
        Assert.IsType<Saved>(ProjectFile.Save(stream, turned));
        stream.Position = 0;

        Assert.Equal(turned, Assert.IsType<LoadedProject>(ProjectFile.Load(stream)).Contents.Sketch);
    }

    // -----------------------------------------------------------------------------------------
    // Refusals, each on its own
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_version_4_file_is_now_too_old_to_open()
    {
        LoadProblem problem = Scenes.RefuseWith(
            Scenes.OneBox.With("\"formatVersion\": 9", "\"formatVersion\": 4"),
            LoadProblemKind.UnsupportedFormatVersion,
            "format version 4",
            "format version 9");

        Assert.Contains("no migration", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"depth\": 768, ", "depth")]
    [InlineData("\"faceUp\": \"top\", ", "faceUp")]
    [InlineData(", \"z\": 0", "z")]
    [Trait("Feature", "PRJ-002")]
    public void A_box_without_its_depth_its_face_up_or_its_height_is_refused(string removed, string named)
        => Scenes.RefuseWith(Scenes.OneBox.With(removed, string.Empty), LoadProblemKind.MissingField, named);

    [Theory]
    [InlineData("\"depth\": 0,", LoadProblemKind.InvalidValue, "depth")]
    [InlineData("\"depth\": -768,", LoadProblemKind.InvalidValue, "depth")]
    [InlineData("\"depth\": 768.5,", LoadProblemKind.NotAnInteger, "depth")]
    [InlineData("\"depth\": \"768\",", LoadProblemKind.Malformed, "depth")]
    [Trait("Feature", "PRJ-002")]
    public void A_depth_that_is_not_a_positive_integer_is_refused(string written, LoadProblemKind kind, string named)
        => Scenes.RefuseWith(Scenes.OneBox.With("\"depth\": 768,", written), kind, named);

    [Theory]
    [InlineData("\"z\": 0.5", LoadProblemKind.NotAnInteger)]
    [InlineData("\"z\": \"0\"", LoadProblemKind.Malformed)]
    [Trait("Feature", "PRJ-002")]
    public void A_height_that_is_not_an_integer_is_refused(string written, LoadProblemKind kind)
        => Scenes.RefuseWith(Scenes.OneBox.With("\"z\": 0", written), kind, "z");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_face_up_that_is_not_one_of_the_six_is_refused_naming_them()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"faceUp\": \"top\"", "\"faceUp\": \"up\""),
            LoadProblemKind.UnknownValue,
            "\"up\"",
            "south, east, north, west, bottom, top");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_part_still_carrying_its_out_of_plane_dimension_is_refused()
    {
        // The third dimension is the box's depth now; a second copy on the part is an unknown
        // field, not a value to reconcile.
        Scenes.RefuseWith(
            Scenes.OneBox.With("\"quantity\": 1,", "\"quantity\": 1, \"outOfPlane\": 768,"),
            LoadProblemKind.UnknownField,
            "outOfPlane");
    }

    [Theory]
    [InlineData("[\"south\", \"north\"]", "opposite")]
    [InlineData("[\"top\", \"bottom\"]", "opposite")]
    [InlineData("[\"east\", \"west\", \"top\"]", "opposite")]
    [InlineData("[\"west\", \"south\"]", "after")]
    [InlineData("[\"south\", \"top\", \"west\"]", "after")]
    [InlineData("[\"south\", \"south\"]", "twice")]
    [InlineData("[\"south\", \"east\", \"bottom\", \"top\"]", "names 4")]
    [InlineData("[]", "names 0")]
    [Trait("Feature", "PRJ-002")]
    public void A_feature_that_is_not_one_two_or_three_faces_in_order_is_refused(string faces, string says)
        => Scenes.RefuseWith(
            Scenes.TurnedBoxAndNode.With("\"faces\": [\"south\", \"east\"]", $"\"faces\": {faces}"),
            LoadProblemKind.InvalidValue,
            "faces",
            says);

    [Theory]
    [InlineData("\"faces\": [\"south\", \"east\"]", "\"faces\": \"south\"", LoadProblemKind.Malformed, "array")]
    [InlineData("\"faces\": [\"south\", \"east\"]", "\"faces\": [\"south\", 2]", LoadProblemKind.Malformed, "found a number")]
    [InlineData(", \"faces\": [\"south\", \"east\"]", "", LoadProblemKind.MissingField, "faces")]
    [Trait("Feature", "PRJ-002")]
    public void A_feature_whose_faces_are_not_a_list_of_names_is_refused(
        string original, string replacement, LoadProblemKind kind, string says)
        => Scenes.RefuseWith(Scenes.TurnedBoxAndNode.With(original, replacement), kind, says);

    [Theory]
    [InlineData(
        "{ \"kind\": \"feature\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"south\", \"east\"] }",
        "{ \"kind\": \"corner\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"corner\": \"southEast\" }",
        "\"corner\"")]
    [InlineData(
        "{ \"kind\": \"feature\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"south\", \"east\"] }",
        "{ \"kind\": \"boxEdge\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"edge\": \"south\" }",
        "\"boxEdge\"")]
    [Trait("Feature", "PRJ-002")]
    public void The_version_3_reference_kinds_are_refused_saying_what_replaced_them(
        string original, string replacement, string named)
        => Scenes.RefuseWith(
            Scenes.TurnedBoxAndNode.With(original, replacement),
            LoadProblemKind.UnknownValue,
            named,
            "\"feature\"",
            "version 4");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_flush_between_faces_on_different_axes_is_refused_naming_both_places()
        => Scenes.RefuseWith(
            TwoBoxesInSpace.With("\"faces\": [\"west\"]", "\"faces\": [\"north\"]"),
            LoadProblemKind.InvalidValue,
            "Left's east face fixes X",
            "Right's north face fixes Y");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_flush_between_a_face_and_an_edge_is_refused_because_an_edge_is_not_a_plane()
        => Scenes.RefuseWith(
            TwoBoxesInSpace.With("\"faces\": [\"bottom\"]", "\"faces\": [\"west\", \"bottom\"]"),
            LoadProblemKind.InvalidValue,
            "Right's bottom west edge fixes X and Z",
            "two planes");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_span_along_z_between_places_that_do_not_fix_z_is_refused()
        => Scenes.RefuseWith(
            TwoBoxesInSpace.With("\"faces\": [\"south\", \"west\", \"top\"]", "\"faces\": [\"south\", \"west\"]"),
            LoadProblemKind.InvalidValue,
            "Right's south-west edge fixes X and Y",
            "along Z");

    [Theory]
    [InlineData("\"kind\": \"boxWidth\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\"",
                "\"kind\": \"boxDepth\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\"",
                "depth of Left")]
    [Trait("Feature", "PRJ-002")]
    public void A_dimension_on_the_depth_of_a_box_lying_as_drawn_is_refused_as_leaving_the_plan(
        string original, string replacement, string says)
        => Scenes.RefuseWith(
            TwoBoxesInSpace.With(original, replacement),
            LoadProblemKind.InvalidValue,
            "Dimension ",
            says,
            "world Z");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_dimension_on_the_width_of_a_box_standing_on_its_east_face_is_refused()
    {
        // Left tipped over westward stands its width along world Z (§1.3's table). The flushes and
        // the span name Left's faces, so they go, and the dimension alone is what is refused.
        string standing = TwoBoxesInSpace
            .With(
                "\"depth\": 3072, \"faceUp\": \"top\"",
                "\"depth\": 3072, \"faceUp\": \"east\"")
            .With(
                RelationshipsOf(TwoBoxesInSpace),
                "\"relationships\": []");

        LoadProblem problem = Scenes.RefuseWith(standing, LoadProblemKind.InvalidValue, "Dimension ", "width of Left", "east face up");
        Assert.Contains("world Z", problem.Message, StringComparison.Ordinal);

        // Its height still lies in the plan on an east-up box, so a dimension on that loads.
        Scenes.Accept(standing.With("\"kind\": \"boxWidth\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\"", "\"kind\": \"boxHeight\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\""));
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_dimension_spanning_along_z_is_refused_as_leaving_the_plan()
        => Scenes.RefuseWith(
            TwoBoxesInSpace.With(
                "\"measures\": { \"kind\": \"boxWidth\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\" }",
                "\"measures\": { \"kind\": \"axis\", "
                + "\"from\": { \"kind\": \"feature\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"bottom\"] }, "
                + "\"to\": { \"kind\": \"feature\", \"box\": \"0192f1a0-0000-4000-8000-00000000000a\", \"faces\": [\"top\"] }, "
                + "\"axis\": \"z\" }"),
            LoadProblemKind.InvalidValue,
            "Dimension ",
            "measures along Z");

    [Fact]
    [Trait("Feature", "PRJ-003")]
    public void A_box_whose_height_breaks_its_own_relationships_along_z_is_refused()
    {
        // One unit up breaks both things that hold Right in Z: the flush under Left's top face,
        // and the span to Right's top corner, which is now 1025.
        Refused refused = Scenes.Refuse(TwoBoxesInSpace.With("\"z\": 5120", "\"z\": 5121"));

        Assert.All(refused.Problems, problem => Assert.Equal(LoadProblemKind.RelationshipViolated, problem.Kind));
        Assert.Equal(["axisDistance", "flush"], refused.Problems.Select(KindNamed).Order());
    }

    private static string KindNamed(LoadProblem problem)
        => problem.Message.Contains("own flush", StringComparison.Ordinal) ? "flush"
            : problem.Message.Contains("own axisDistance", StringComparison.Ordinal) ? "axisDistance"
            : problem.Message;

    private static string RelationshipsOf(string scene)
    {
        int start = scene.IndexOf("\"relationships\": [", StringComparison.Ordinal);
        int end = scene.LastIndexOf(']');
        return scene[start..(end + 1)];
    }

    private static EntityId EntityIdAt(int n) => new(Guid.Parse($"0192f1a0-0000-4000-8000-{n:x12}"));

    private static RelationshipId RelationshipIdAt(int n) => new(Guid.Parse($"0192f1a0-0000-4000-9000-{n:x12}"));
}
