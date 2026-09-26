using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// What the reader refuses, and what it says when it does. Every case starts from a scene that
/// loads and introduces exactly one fault, so the refusal is about that fault and nothing else.
/// </summary>
public sealed class SceneReaderRejectionTests
{
    /// <summary>A file whose scene body is nonsense, so that only the stamp can be judged.</summary>
    private const string BrokenBodyAtVersion = """
        {
          "formatVersion": {0},
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": 5,
          "entities": 5,
          "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
          "relationships": 5
        }
        """;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [Trait("Feature", "PRJ-004")]
    public void A_file_from_another_format_version_fails_before_the_scene_is_parsed(int version)
    {
        string json = BrokenBodyAtVersion.Replace("{0}", version.ToString(), StringComparison.Ordinal);

        Refused refused = Scenes.Refuse(json);

        // The scene body below the stamp is nonsense in every direction, and none of it is
        // reported: the version was judged first, which is the whole point of the stamp.
        LoadProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(LoadProblemKind.UnsupportedFormatVersion, problem.Kind);
        Assert.Contains($"format version {version}", problem.Message, StringComparison.Ordinal);
        Assert.Contains($"format version {SceneReader.FormatVersion}", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void The_version_the_reader_accepts_is_the_version_the_format_stamps()
    {
        Assert.Equal(FormatStamp.Current.FormatVersion, SceneReader.FormatVersion);

        Loaded loaded = Assert.IsType<Loaded>(Scenes.Read(Scenes.OneBox));
        Assert.Equal(FormatStamp.Current, loaded.Stamp);
    }

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_file_with_no_version_stamp_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"formatVersion\": 11,", string.Empty),
            LoadProblemKind.MissingField,
            "formatVersion");

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_version_written_as_text_is_not_a_version()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"formatVersion\": 11", "\"formatVersion\": \"4\""),
            LoadProblemKind.Malformed,
            "formatVersion");

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_version_written_as_a_decimal_is_not_a_version()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"formatVersion\": 11", "\"formatVersion\": 11.0"),
            LoadProblemKind.NotAnInteger,
            "formatVersion");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Units_the_app_does_not_store_in_are_refused()
    {
        Scenes.RefuseWith(
            Scenes.OneBox.With("\"length\": \"inch/1024\"", "\"length\": \"mm\""),
            LoadProblemKind.UnsupportedUnits,
            "mm",
            "inch/1024");

        Scenes.RefuseWith(
            Scenes.OneBox.With("\"angle\": \"arcsecond\"", "\"angle\": \"degree\""),
            LoadProblemKind.UnsupportedUnits,
            "degree",
            "arcsecond");
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void An_unknown_field_is_refused_and_named()
    {
        Scenes.RefuseWith(
            Scenes.OneBox.With("\"rotation\": 0,", "\"rotation\": 0, \"colour\": \"walnut\","),
            LoadProblemKind.UnknownField,
            "colour");

        Scenes.RefuseWith(
            Scenes.OneBox.With("\"formatVersion\": 11,", "\"formatVersion\": 11, \"author\": \"someone\","),
            LoadProblemKind.UnknownField,
            "author");

        // A part's own object is read as strictly as the entity around it.
        Scenes.RefuseWith(
            Scenes.OneBox.With("\"quantity\": 1,", "\"quantity\": 1, \"grain\": \"quartersawn\","),
            LoadProblemKind.UnknownField,
            "grain");
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_field_written_twice_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"width\": 30720,", "\"width\": 30720, \"width\": 30721,"),
            LoadProblemKind.DuplicateField,
            "width");

    [Theory]
    [InlineData("30720.5")]
    [InlineData("30720.0")]
    [InlineData("3.072e4")]
    [Trait("Feature", "PRJ-002")]
    public void A_length_that_is_not_a_whole_number_of_units_is_refused(string written)
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"width\": 30720,", $"\"width\": {written},"),
            LoadProblemKind.NotAnInteger,
            "width",
            written);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_length_written_as_text_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"width\": 30720,", "\"width\": \"30 inches\","),
            LoadProblemKind.Malformed,
            "width");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_number_too_large_for_the_model_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"width\": 30720,", "\"width\": 99999999999999999999,"),
            LoadProblemKind.OutOfRange,
            "width");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_dangling_reference_is_a_load_failure_and_not_a_repair()
        => Scenes.RefuseWith(
            Scenes.OneBox.With($"\"box\": \"{Scenes.BoxId}\"", $"\"box\": \"{Scenes.MissingId}\""),
            LoadProblemKind.DanglingReference,
            Scenes.MissingId);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_dimension_driven_by_a_relationship_the_file_does_not_have_is_refused()
        => Scenes.RefuseWith(
            Scenes.SegmentAndDimension.With("\"drives\": null", $"\"drives\": \"{Scenes.MissingId}\""),
            LoadProblemKind.DanglingReference,
            Scenes.MissingId);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void An_entity_on_a_layer_the_file_does_not_have_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With(
                $"\"type\": \"box\", \"layer\": \"{Scenes.LayerId}\"",
                $"\"type\": \"box\", \"layer\": \"{Scenes.MissingId}\""),
            LoadProblemKind.DanglingReference,
            Scenes.MissingId[..8]);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_reference_that_names_the_wrong_kind_of_entity_is_refused_and_not_evaluated()
    {
        // A feature of a node is not a thing. If this reached the relationship checker it would
        // throw rather than refuse, which is why kinds are resolved before any geometry is read.
        Scenes.RefuseWith(
            Scenes.TurnedBoxAndNode.With(
                $"\"kind\": \"feature\", \"box\": \"{Scenes.BoxId}\"",
                $"\"kind\": \"feature\", \"box\": \"{Scenes.NodeId}\""),
            LoadProblemKind.WrongReferenceKind,
            Scenes.NodeId,
            "box");

        Scenes.RefuseWith(
            Scenes.TurnedBoxAndNode.With(
                $"\"kind\": \"node\", \"node\": \"{Scenes.NodeId}\"",
                $"\"kind\": \"node\", \"node\": \"{Scenes.BoxId}\""),
            LoadProblemKind.WrongReferenceKind,
            Scenes.BoxId,
            "node");

        Scenes.RefuseWith(
            Scenes.SegmentAndDimension.With(
                $"\"kind\": \"segmentLength\", \"segment\": \"{Scenes.SegmentId}\"",
                $"\"kind\": \"segmentLength\", \"segment\": \"{Scenes.NodeId}\""),
            LoadProblemKind.WrongReferenceKind,
            "segment");
    }

    [Fact]
    [Trait("Feature", "PRJ-003")]
    public void A_file_whose_geometry_does_not_satisfy_its_own_relationships_is_refused()
    {
        // The box is 30720 units wide and the file says its width is held at 30721.
        LoadProblem problem = Scenes.RefuseWith(
            Scenes.OneBox.With("\"value\": 30720", "\"value\": 30721"),
            LoadProblemKind.RelationshipViolated,
            Scenes.RelationshipId[..8],
            "paramValue");

        Assert.Contains("does not satisfy", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_relationship_kind_this_build_cannot_hold_is_refused_naming_the_kind()
        => Scenes.RefuseWith(Scenes.TangentBetweenBoxEdges, LoadProblemKind.UnsupportedRelationship, "tangent");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not a project file")]
    [InlineData("{ \"formatVersion\": 1, }")]
    [InlineData("{ /* a comment */ }")]
    [Trait("Feature", "PRJ-002")]
    public void An_empty_or_garbled_file_is_refused_rather_than_crashing(string text)
    {
        Refused refused = Scenes.Refuse(text);
        Assert.Equal(LoadProblemKind.Malformed, Assert.Single(refused.Problems).Kind);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_document_that_is_not_an_object_is_refused()
        => Scenes.RefuseWith("[]", LoadProblemKind.Malformed, "object");

    [Theory]
    [InlineData("\"type\": \"box\"", "\"type\": \"cube\"", "cube")]
    [InlineData("\"kind\": \"paramValue\"", "\"kind\": \"almostEqual\"", "almostEqual")]
    [InlineData("\"kind\": \"boxWidth\"", "\"kind\": \"boxLength\"", "boxLength")]
    [Trait("Feature", "PRJ-002")]
    public void A_type_or_kind_this_build_does_not_know_is_refused_and_named(
        string original, string replacement, string named)
        => Scenes.RefuseWith(
            Scenes.OneBox.With(original, replacement),
            LoadProblemKind.UnknownValue,
            named);

    [Theory]
    [InlineData("\"faces\": [\"south\", \"east\"]", "\"faces\": [\"south\", \"middle\"]", "middle")]
    [Trait("Feature", "PRJ-002")]
    public void A_face_this_build_does_not_know_is_refused_and_named(
        string original, string replacement, string named)
        => Scenes.RefuseWith(
            Scenes.TurnedBoxAndNode.With(original, replacement),
            LoadProblemKind.UnknownValue,
            named);

    [Theory]
    [InlineData("\"axis\": \"x\"", "\"axis\": \"w\"", "w")]
    [InlineData("\"side\": \"south\"", "\"side\": \"below\"", "below")]
    [Trait("Feature", "PRJ-002")]
    public void An_axis_or_side_this_build_does_not_know_is_refused_and_named(
        string original, string replacement, string named)
        => Scenes.RefuseWith(
            Scenes.SegmentAndDimension.With(original, replacement),
            LoadProblemKind.UnknownValue,
            named);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void An_id_that_is_not_a_guid_is_refused()
        => Scenes.RefuseWith(
            Scenes.OneBox.With($"\"id\": \"{Scenes.BoxId}\"", "\"id\": \"the-table-top\""),
            LoadProblemKind.NotAnId,
            "the-table-top");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Two_entities_with_one_id_are_refused()
        => Scenes.RefuseWith(Scenes.TwoEntitiesUnderOneId, LoadProblemKind.DuplicateId, Scenes.BoxId);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Two_layers_with_one_id_are_refused()
        => Scenes.RefuseWith(Scenes.TwoLayersUnderOneId, LoadProblemKind.DuplicateId, Scenes.LayerId);

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Two_relationships_that_say_the_same_thing_are_refused()
        => Scenes.RefuseWith(
            Scenes.TwoRelationshipsSayingTheSameThing,
            LoadProblemKind.DuplicateRelationship,
            "say the same thing");

    [Theory]
    [InlineData("\"width\": 0,")]
    [InlineData("\"width\": -30720,")]
    [Trait("Feature", "PRJ-002")]
    public void A_box_with_a_size_that_is_not_positive_is_refused(string written)
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"width\": 30720,", written),
            LoadProblemKind.InvalidValue,
            "width");

    [Theory]
    [InlineData("1296000")]
    [InlineData("-324000")]
    [Trait("Feature", "PRJ-002")]
    public void A_rotation_that_is_not_stored_normalised_is_refused(string written)
    {
        // Angle normalises into [0, 360), so a file saying 1296000 would load as 0 and the sketch
        // would no longer equal the file. Refusing keeps "equal by value" true.
        Scenes.RefuseWith(
            Scenes.OneBox.With("\"rotation\": 0", $"\"rotation\": {written}"),
            LoadProblemKind.InvalidValue,
            "rotation");
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_required_field_that_is_missing_is_refused_and_named()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"height\": 4096, ", string.Empty),
            LoadProblemKind.MissingField,
            "height");

    [Theory]
    [InlineData("\"name\": \"Shelf\",", "name")]
    [InlineData("\"part\": ", "part")]
    [InlineData("\"cuts\": ", "cuts")]
    [Trait("Feature", "CUT-001")]
    public void A_name_a_part_and_a_cut_list_are_required_on_a_box(string original, string named)
        => Scenes.RefuseWith(
            Scenes.OneBox.With(original, original.Replace(named, $"{named}Of", StringComparison.Ordinal)),
            LoadProblemKind.MissingField,
            named);

    [Theory]
    [InlineData("\"name\": \"Shelf\"", "\"name\": 7")]
    [InlineData("\"stock\": \"1x6\"", "\"stock\": 24")]
    [InlineData("\"species\": null", "\"species\": 24")]
    [Trait("Feature", "CUT-001")]
    public void A_name_a_stock_and_a_species_are_text_or_nothing(string original, string replacement)
        => Scenes.RefuseWith(Scenes.OneBox.With(original, replacement), LoadProblemKind.Malformed, "found a number");

    [Theory]
    [InlineData("\"quantity\": 0")]
    [InlineData("\"quantity\": -1")]
    [Trait("Feature", "CUT-001")]
    public void A_part_counts_at_least_one_piece(string replacement)
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"quantity\": 1", replacement),
            LoadProblemKind.InvalidValue,
            "quantity");

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_plan_axis_this_build_does_not_know_is_refused_and_named()
        => Scenes.RefuseWith(
            Scenes.OneBox.With("\"y\": \"width\"", "\"y\": \"depth\""),
            LoadProblemKind.UnknownValue,
            "depth");

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void Two_plan_axes_naming_one_dimension_are_refused()
    {
        // Both axes claiming "length" would leave no name for the box's depth to carry, so the part
        // would have two dimensions and a spare number rather than three dimensions.
        LoadProblem problem = Scenes.RefuseWith(
            Scenes.OneBox.With("\"y\": \"width\"", "\"y\": \"length\""),
            LoadProblemKind.InvalidValue,
            "planAxes");

        Assert.Contains("length", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_stock_name_this_build_has_never_heard_of_still_loads()
    {
        // Deliberate: a file is refused for being malformed, never for naming something this
        // build's materials library does not carry. The cut list says so instead.
        Sketch sketch = Scenes.Accept(Scenes.OneBox.With("\"stock\": \"1x6\"", "\"stock\": \"9x17 unobtainium\""));

        Box box = Assert.IsType<Box>(sketch.Find(new EntityId(Guid.Parse(Scenes.BoxId))));
        Assert.Equal("9x17 unobtainium", box.Part?.Stock);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_file_with_several_faults_reports_them_all_and_returns_no_sketch()
    {
        Refused refused = Scenes.Refuse(
            Scenes.OneBox
                .With("\"rotation\": 0,", "\"rotation\": 0, \"colour\": \"walnut\",")
                .With("\"name\": \"Default\"", "\"name\": \"Default\", \"visible\": true"));

        Assert.Equal(2, refused.Problems.Count);
        Assert.All(refused.Problems, problem => Assert.Equal(LoadProblemKind.UnknownField, problem.Kind));
        Assert.False(refused.IsLoaded);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_refusal_is_the_same_every_time_and_leaves_nothing_behind()
    {
        string json = Scenes.OneBox.With("\"value\": 30720", "\"value\": 30721");

        Refused first = Scenes.Refuse(json);
        Refused second = Scenes.Refuse(json);

        Assert.Equal(first.Problems, second.Problems);

        // The file that is still good still loads: a refusal of one file changes nothing for the
        // next.
        Assert.IsType<Loaded>(Scenes.Read(Scenes.OneBox));
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_file_that_cannot_be_read_at_all_is_refused_rather_than_thrown()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"napkin-no-such-file-{Guid.NewGuid():N}.json");

        Refused refused = Assert.IsType<Refused>(SceneReader.ReadFile(missing));

        Assert.Equal(LoadProblemKind.Unreadable, Assert.Single(refused.Problems).Kind);
        Assert.Contains(missing, refused.Summary, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void The_reader_refuses_a_kind_the_updater_it_is_given_does_not_support()
    {
        Refused refused = Assert.IsType<Refused>(
            SceneReader.Read(
                new MemoryStream(Encoding.UTF8.GetBytes(Scenes.OneBox)),
                new NoRelationshipsUpdater()));

        Assert.Equal(LoadProblemKind.UnsupportedRelationship, Assert.Single(refused.Problems).Kind);
        Assert.Contains("paramValue", refused.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_reader_rejects_nonsense_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => SceneReader.Read((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => SceneReader.ReadFile("scene.json", null!));
        Assert.Throws<ArgumentException>(() => SceneReader.ReadFile(string.Empty));
    }

    /// <summary>An updater that holds nothing, standing in for a build without the solver.</summary>
    private sealed class NoRelationshipsUpdater : IGeometryUpdater
    {
        public System.Collections.Immutable.ImmutableHashSet<Type> SupportedRelationships { get; }
            = System.Collections.Immutable.ImmutableHashSet<Type>.Empty;

        public UpdateResult Apply(Sketch sketch, Request request)
            => throw new NotSupportedException("This updater is only here for its empty list of kinds.");
    }
}
