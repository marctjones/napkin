using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The writer against the reader: the round trip, and the promises about the bytes that make a
/// saved project diff cleanly and a save reproducible.
/// </summary>
public sealed class SceneWriterTests
{
    /// <summary>
    /// The seeds every round-trip theory runs. A failure prints the seed, and adding it here is
    /// how a failure becomes a permanent regression test.
    /// </summary>
    public static TheoryData<int> Seeds
    {
        get
        {
            TheoryData<int> data = [];
            for (int seed = 1; seed <= 60; seed++)
            {
                data.Add(seed);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    [Trait("Feature", "PRJ-007")]
    public void Load_of_save_is_the_sketch_it_started_from(int seed)
    {
        Sketch sketch = SketchGenerator.Generate(seed);

        // The generator's own contract first, so that a bad seed is diagnosed as a bad seed
        // rather than blamed on the writer.
        Assert.True(sketch.Validate().IsValid, $"seed {SketchGenerator.Describe(seed)}: {sketch.Validate()}");
        Assert.True(
            RelationshipChecker.Check(sketch).AllHold,
            $"seed {SketchGenerator.Describe(seed)}: {RelationshipChecker.Check(sketch)}");

        byte[] written = SceneWriter.WriteToBytes(sketch);
        Sketch read = Reload(written, seed);

        Assert.Equal(sketch, read);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    [Trait("Feature", "PRJ-007")]
    public void Bytes_survive_a_round_trip_unchanged(int seed)
    {
        // The other half of the identity: reading a file this build wrote and writing it again
        // produces the same file. Without this, a save after an open could churn the file in git
        // while still "round tripping" by value.
        byte[] written = SceneWriter.WriteToBytes(SketchGenerator.Generate(seed));
        byte[] again = SceneWriter.WriteToBytes(Reload(written, seed));

        Assert.Equal(written, again);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    [Trait("Feature", "PRJ-007")]
    public void Writing_the_same_sketch_twice_gives_the_same_bytes(int seed)
    {
        Sketch sketch = SketchGenerator.Generate(seed);

        Assert.Equal(SceneWriter.WriteToBytes(sketch), SceneWriter.WriteToBytes(sketch));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    [Trait("Feature", "PRJ-007")]
    public void The_file_is_in_id_order_and_not_in_the_dictionary_s_order(int seed)
    {
        // A sketch holds entities and relationships in an ImmutableDictionary, which enumerates
        // in an order derived from the keys' hash codes — stable within a run, unrelated to id
        // order, and not something the file should inherit. Comparing two sketches' bytes would
        // *not* catch a writer that skipped sorting, because two sketches holding the same keys
        // enumerate the same way; so the order in the document is read back and checked directly.
        Sketch sketch = SketchGenerator.Generate(seed);

        Guid[] entityIds = IdsUnder(sketch, "entities");
        Guid[] relationshipIds = IdsUnder(sketch, "relationships");

        Assert.Equal([.. sketch.Entities.Keys.Order().Select(id => id.Value)], entityIds);
        Assert.Equal([.. sketch.Relationships.Keys.Order().Select(id => id.Value)], relationshipIds);

        // And the check is worth making: the dictionary's own order is not the sorted one, so a
        // writer that just enumerated would fail the assertions above rather than pass them by
        // luck.
        Assert.NotEqual([.. sketch.Entities.Values.Select(entity => entity.Id.Value)], entityIds);
    }

    /// <summary>The ids, in the order the written document lists them.</summary>
    private static Guid[] IdsUnder(Sketch sketch, string array)
    {
        using JsonDocument document = JsonDocument.Parse(SceneWriter.WriteToBytes(sketch));
        return [.. document.RootElement.GetProperty(array).EnumerateArray()
            .Select(element => Guid.Parse(element.GetProperty("id").GetString()!))];
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [InlineData("rounded-corner-table")]
    [Trait("Feature", "PRJ-007")]
    public void A_hand_written_sample_reads_back_equal_after_being_rewritten(string fixture)
    {
        // The samples are laid out by hand — several fields to a line, blank lines between
        // groups — so the writer cannot reproduce their bytes and does not try. What it must do
        // is write a document that means exactly the same thing, and be stable from then on.
        Sketch original = ReadSample(fixture);

        byte[] written = SceneWriter.WriteToBytes(original);
        Sketch reread = Reload(written, seed: 0);

        Assert.Equal(original, reread);
        Assert.Equal(written, SceneWriter.WriteToBytes(reread));
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Every_relationship_kind_the_model_can_hold_is_generated()
    {
        // A round-trip suite that never generated a centered or a symmetric would pass while the
        // writer got them wrong, so what the generator reaches is itself asserted.
        HashSet<Type> seen = [];
        bool negativeCoordinate = false;
        bool turnedBox = false;
        bool severalLayers = false;
        bool referenceDimension = false;
        bool drivingDimension = false;

        foreach (int seed in Enumerable.Range(1, 60))
        {
            Sketch sketch = SketchGenerator.Generate(seed);
            foreach (Relationship relationship in sketch.RelationshipsInOrder)
            {
                seen.Add(relationship.GetType());
            }

            severalLayers |= sketch.Layers.Count > 1;

            foreach (Entity entity in sketch.Entities.Values)
            {
                switch (entity)
                {
                    case Box box:
                        negativeCoordinate |= box.Anchor.X.Units < 0 || box.Anchor.Y.Units < 0;
                        turnedBox |= box.Rotation != Angle.Zero;
                        break;

                    case Node node:
                        negativeCoordinate |= node.Position.X.Units < 0 || node.Position.Y.Units < 0;
                        break;

                    case Dimension dimension:
                        referenceDimension |= dimension.Drives is null;
                        drivingDimension |= dimension.Drives is not null;
                        break;
                }
            }
        }

        Type[] holdable =
        [
            typeof(Anchored), typeof(Coincident), typeof(Horizontal), typeof(Vertical), typeof(Flush),
            typeof(AxisDistance), typeof(ParamValue), typeof(EqualParam), typeof(Centered),
            typeof(Geometry.Parallel), typeof(Perpendicular), typeof(AngleBetween), typeof(Distance),
            typeof(PointOnEdge), typeof(Symmetric),
        ];

        Assert.Empty(holdable.Where(kind => !seen.Contains(kind)));
        Assert.True(negativeCoordinate, "no generated sketch had a negative coordinate.");
        Assert.True(turnedBox, "no generated sketch had a rotated box.");
        Assert.True(severalLayers, "no generated sketch had more than one layer.");
        Assert.True(referenceDimension, "no generated sketch had a reference dimension.");
        Assert.True(drivingDimension, "no generated sketch had a driving dimension.");
    }

    [Theory]
    [InlineData("tangent")]
    [InlineData("radius")]
    [Trait("Feature", "PRJ-002")]
    public void The_two_kinds_that_are_about_arcs_are_still_written_in_the_shape_the_reader_parses(string kind)
    {
        // Tangent and Radius cannot round trip: there are no arcs, so the checker cannot evaluate
        // them and the reader refuses any file holding one (geometry design §10). The writer still
        // has to spell them the way the format says, and the proof is that the reader's binder
        // consumes the writer's bytes all the way to the checker and stops there — one problem,
        // naming the kind, and nothing about a field.
        Box box = Box.AsDrawn(
            new EntityId(Guid.Parse("0192f1a0-0000-4000-8000-00000000000a")),
            LayerId.Default,
            Point2.Origin,
            new Length(8192),
            new Length(8192),
            Box.DefaultDepth,
            Angle.Zero);

        RelationshipId id = new(Guid.Parse("0192f1a0-0000-4000-8000-000000000001"));
        Relationship relationship = kind == "tangent"
            ? new Tangent(id, TestRefs.Edge(box.Id, BoxEdge.North), TestRefs.Edge(box.Id, BoxEdge.South))
            : new Radius(id, box.Id, new Length(4096));

        Sketch sketch = Sketch.Empty.WithEntity(box).WithRelationship(relationship);
        byte[] written = SceneWriter.WriteToBytes(sketch);

        Assert.Contains($"\"kind\": \"{kind}\"", Encoding.UTF8.GetString(written), StringComparison.Ordinal);

        using MemoryStream stream = new(written);
        Refused refused = Assert.IsType<Refused>(SceneReader.Read(stream, new EveryKindUpdater()));

        LoadProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(LoadProblemKind.RelationshipViolated, problem.Kind);
        Assert.Contains(kind, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void The_document_carries_the_stamp_the_reader_judges_it_by()
    {
        string text = SceneWriter.WriteToText(Sketch.Empty);

        Assert.StartsWith("{\n  \"formatVersion\": 8,\n", text, StringComparison.Ordinal);
        Assert.Contains("\"units\": {\n    \"length\": \"inch/1024\",\n    \"angle\": \"arcsecond\"\n  }", text, StringComparison.Ordinal);
        Assert.Equal(FormatStamp.CurrentVersion, SceneWriter.FormatVersion);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Lines_end_in_a_newline_on_every_platform_and_the_file_does_too()
    {
        // The bytes are what a diff and a checksum see, so they cannot depend on which machine
        // saved the file.
        string text = SceneWriter.WriteToText(SketchGenerator.Generate(3));

        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Ids_are_written_in_the_canonical_form_the_reader_demands()
    {
        Guid id = Guid.Parse("0192f1a0-1234-4abc-8def-00000000000a");
        Sketch sketch = Sketch.Empty.WithEntity(new Node(new EntityId(id), LayerId.Default, Point2.Origin));

        Assert.Contains(
            $"\"id\": \"{id.ToString("D", CultureInfo.InvariantCulture)}\"",
            SceneWriter.WriteToText(sketch),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void A_reference_dimension_writes_a_null_rather_than_leaving_the_field_out()
    {
        // The format has no optional fields: the writer and the reader agree on the shape, so a
        // reference dimension says so (docs/file-format.md).
        Node node = new(new EntityId(Guid.Parse("0192f1a0-0000-4000-8000-00000000000b")), LayerId.Default, Point2.Origin);
        Node other = new(new EntityId(Guid.Parse("0192f1a0-0000-4000-8000-00000000000e")), LayerId.Default, new Point2(new Length(1024), Length.Zero));
        Dimension dimension = new(
            new EntityId(Guid.Parse("0192f1a0-0000-4000-8000-00000000000d")),
            LayerId.Default,
            new AxisMeasurand(new NodeRef(node.Id), new NodeRef(other.Id), Axis.X),
            null,
            new DimensionPlacement(new Length(2048), DimensionSide.South));

        Sketch sketch = Sketch.Empty.WithEntity(node).WithEntity(other).WithEntity(dimension);

        Assert.Contains("\"drives\": null", SceneWriter.WriteToText(sketch), StringComparison.Ordinal);
        Assert.Equal(sketch, Reload(SceneWriter.WriteToBytes(sketch), seed: 0));
    }

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void An_empty_sketch_is_a_document_the_reader_accepts()
    {
        Assert.Equal(Sketch.Empty, Reload(SceneWriter.WriteToBytes(Sketch.Empty), seed: 0));
    }

    internal static Sketch ReadSample(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(
            Path.Combine(SampleFixtureTests.SampleDirectory, $"{fixture}.scene.json"));
        return Assert.IsType<Loaded>(result).Sketch;
    }

    private static Sketch Reload(byte[] written, int seed)
    {
        using MemoryStream stream = new(written);
        LoadResult result = SceneReader.Read(stream, new EveryKindUpdater());

        if (result is Refused refused)
        {
            Assert.Fail(
                $"seed {SketchGenerator.Describe(seed)}: the writer produced a file the reader refused."
                + Environment.NewLine + refused.Summary
                + Environment.NewLine + Encoding.UTF8.GetString(written));
        }

        return Assert.IsType<Loaded>(result).Sketch;
    }

    /// <summary>
    /// An updater that holds every kind, standing in for a build with the constraint solver. The
    /// round trip is about the format, not about what this build can edit, so the reader is told
    /// to hold everything the model can express.
    /// </summary>
    internal sealed class EveryKindUpdater : IGeometryUpdater
    {
        public ImmutableHashSet<Type> SupportedRelationships { get; } =
        [
            typeof(Anchored), typeof(Coincident), typeof(Horizontal), typeof(Vertical), typeof(Flush),
            typeof(AxisDistance), typeof(ParamValue), typeof(EqualParam), typeof(Centered),
            typeof(Geometry.Parallel), typeof(Perpendicular), typeof(AngleBetween), typeof(Distance),
            typeof(PointOnEdge), typeof(Symmetric), typeof(Tangent), typeof(Radius),
        ];

        public UpdateResult Apply(Sketch sketch, Request request)
            => throw new NotSupportedException("This updater is only here for its list of kinds.");
    }
}
