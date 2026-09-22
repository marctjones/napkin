using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// A box's <c>cuts</c> (<c>docs/design/shaped-parts-model.md</c> §5, test 17): the round trip that
/// holds every kind, and each of the ways a file spelling one is refused. Every refusal starts
/// from the scene below, which loads, and introduces exactly one fault.
/// </summary>
public sealed class CutFormatTests
{
    private const string BlankId = "0192f1a0-0000-4000-8000-00000000000a";
    private const string BowedId = "0192f1a0-0000-4000-8000-00000000000b";

    /// <summary>
    /// Two 48&#x2033; &#xD7; 24&#x2033; blanks that between them carry every kind of cut and both
    /// bows. They cannot be one blank: a curved edge claims both of its corners (invariant 6), so
    /// an inward and an outward curve on one box would leave no corner free for the other kinds.
    /// </summary>
    private const string EveryKindOfCut = """
        {
          "formatVersion": 3,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Top",
              "anchor": { "x": 0, "y": 0 }, "width": 49152, "height": 24576, "rotation": 0,
              "part": null,
              "cuts": [
                { "kind": "roundedCorner", "corner": "southWest", "radius": 1024 },
                { "kind": "cornerCut", "corner": "southEast", "alongX": 3072, "alongY": 5120 },
                { "kind": "curvedEdge", "edge": "north", "bow": "inward", "depth": 2048 }
              ] },
            { "id": "0192f1a0-0000-4000-8000-00000000000b", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Shelf front",
              "anchor": { "x": 0, "y": 40960 }, "width": 49152, "height": 24576, "rotation": 0,
              "part": null,
              "cuts": [
                { "kind": "curvedEdge", "edge": "south", "bow": "outward", "depth": 2048 }
              ] }
          ],
          "relationships": []
        }
        """;

    /// <summary>
    /// A complete, well-formed file in the format version this build read until cuts existed:
    /// every box carries a part and no box carries cuts.
    /// </summary>
    private const string TheFormatBeforeCuts = """
        {
          "formatVersion": 2,
          "units": { "length": "inch/1024", "angle": "arcsecond" },
          "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
          "entities": [
            { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
              "name": "Top",
              "anchor": { "x": 0, "y": 0 }, "width": 49152, "height": 24576, "rotation": 0,
              "part": null }
          ],
          "relationships": []
        }
        """;

    /// <summary>The two blanks built by hand, so that the file above has something to be equal to.</summary>
    private static Sketch TheTwoBlanks => Sketch.Empty
        .WithEntity(new Box(
            new EntityId(Guid.Parse(BlankId)),
            LayerId.Default,
            new Point2(Length.Zero, Length.Zero),
            new Length(49152),
            new Length(24576),
            Angle.Zero)
        {
            Name = "Top",
            Cuts =
            [
                new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                new CornerCut(BoxCorner.SouthEast, new Length(3072), new Length(5120)),
                new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(2048)),
            ],
        })
        .WithEntity(new Box(
            new EntityId(Guid.Parse(BowedId)),
            LayerId.Default,
            new Point2(Length.Zero, new Length(40960)),
            new Length(49152),
            new Length(24576),
            Angle.Zero)
        {
            Name = "Shelf front",
            Cuts = [new CurvedEdge(BoxEdge.South, Bow.Outward, new Length(2048))],
        });

    [Fact]
    [Trait("Feature", "PRJ-007")]
    public void Load_of_save_is_the_sketch_it_started_from_for_every_kind_of_cut()
    {
        Sketch sketch = TheTwoBlanks;
        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());

        using MemoryStream stream = new(SceneWriter.WriteToBytes(sketch));
        Sketch read = Assert.IsType<Loaded>(SceneReader.Read(stream)).Sketch;

        Assert.Equal(sketch, read);
    }

    [Fact]
    [Trait("Feature", "PRJ-001")]
    public void The_file_spells_the_cuts_the_model_holds()
    {
        // The reader and the writer against the same hand-written document, so that §5's spelling
        // is pinned rather than merely self-consistent: a field renamed in SceneNames would pass
        // the round trip above and fail here.
        Assert.Equal(TheTwoBlanks, Scenes.Accept(EveryKindOfCut));

        string written = SceneWriter.WriteToText(TheTwoBlanks);
        Assert.Contains("\"cuts\": [", written, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"roundedCorner\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"corner\": \"southWest\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"radius\": 1024\n", written, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"cornerCut\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"alongX\": 3072,\n", written, StringComparison.Ordinal);
        Assert.Contains("\"alongY\": 5120\n", written, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"curvedEdge\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"edge\": \"north\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"bow\": \"inward\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"bow\": \"outward\",\n", written, StringComparison.Ordinal);
        Assert.Contains("\"depth\": 2048\n", written, StringComparison.Ordinal);

        // A plain rectangle writes the empty array, because the format has no optional fields.
        Assert.Contains("\"cuts\": []", SceneWriter.WriteToText(Scenes.Accept(Scenes.OneBox)), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_kind_of_cut_this_build_does_not_know_is_refused()
        => Scenes.RefuseWith(
            EveryKindOfCut.With("\"kind\": \"roundedCorner\"", "\"kind\": \"chamfer\""),
            LoadProblemKind.UnknownValue,
            "chamfer",
            "cornerCut",
            "curvedEdge",
            "roundedCorner");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Two_cuts_at_one_site_are_refused()
        => Scenes.RefuseWith(
            EveryKindOfCut.With(
                "\"kind\": \"cornerCut\", \"corner\": \"southEast\"",
                "\"kind\": \"cornerCut\", \"corner\": \"southWest\""),
            LoadProblemKind.InvalidValue,
            "more than one cut",
            "SouthWest corner");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_corner_a_curved_edge_claims_cannot_carry_its_own_cut()
        => Scenes.RefuseWith(
            EveryKindOfCut
                .With("{ \"kind\": \"roundedCorner\", \"corner\": \"southWest\", \"radius\": 1024 },", string.Empty)
                .With("\"corner\": \"southEast\", \"alongX\"", "\"corner\": \"northEast\", \"alongX\""),
            LoadProblemKind.InvalidValue,
            "NorthEast corner",
            "North edge");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void Cuts_that_are_not_in_site_order_are_refused_rather_than_sorted()
    {
        // The model normalises — Box.Cuts's initialiser sorts — so a file out of order would load
        // as a sketch that no longer equals it. Refusing keeps Load(Save(s)) == s true, the same
        // stance the format takes on an un-normalised rotation.
        LoadProblem problem = Scenes.RefuseWith(
            EveryKindOfCut.With(
                "{ \"kind\": \"roundedCorner\", \"corner\": \"southWest\", \"radius\": 1024 },\n"
                + "        { \"kind\": \"cornerCut\", \"corner\": \"southEast\", \"alongX\": 3072, \"alongY\": 5120 },",
                "{ \"kind\": \"cornerCut\", \"corner\": \"southEast\", \"alongX\": 3072, \"alongY\": 5120 },\n"
                + "        { \"kind\": \"roundedCorner\", \"corner\": \"southWest\", \"radius\": 1024 },"),
            LoadProblemKind.InvalidValue,
            "site order",
            "SouthWest corner",
            "SouthEast corner");

        Assert.Contains(
            "southWest, southEast, northEast, northWest, south, east, north, west",
            problem.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_cut_that_does_not_fit_its_box_is_refused()
    {
        // A 26-inch scallop in a blank 24 inches across: an inward curve has to stay strictly
        // inside the blank, or there is nothing left in the middle of it.
        Refused refused = Scenes.Refuse(
            EveryKindOfCut.With("\"bow\": \"inward\", \"depth\": 2048", "\"bow\": \"inward\", \"depth\": 26624"));

        Assert.Contains(
            refused.Problems,
            problem => problem.Kind == LoadProblemKind.InvalidValue
                && problem.Message.Contains("does not fit", StringComparison.Ordinal)
                && problem.Message.Contains("North edge", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"radius\": 1024", "\"radius\": 0", "radius")]
    [InlineData("\"radius\": 1024", "\"radius\": -1024", "radius")]
    [InlineData("\"alongX\": 3072", "\"alongX\": 0", "alongX")]
    [InlineData("\"alongY\": 5120", "\"alongY\": -5120", "alongY")]
    [InlineData("\"bow\": \"inward\", \"depth\": 2048", "\"bow\": \"inward\", \"depth\": 0", "depth")]
    [Trait("Feature", "PRJ-002")]
    public void A_cut_value_that_is_not_positive_is_refused(string original, string replacement, string named)
        => Scenes.RefuseWith(
            EveryKindOfCut.With(original, replacement),
            LoadProblemKind.InvalidValue,
            named,
            "greater than zero");

    [Fact]
    [Trait("Feature", "PRJ-002")]
    public void A_bow_that_is_not_one_of_the_two_is_refused()
        => Scenes.RefuseWith(
            EveryKindOfCut.With("\"bow\": \"inward\"", "\"bow\": \"inwards\""),
            LoadProblemKind.UnknownValue,
            "inwards",
            "outward, inward");

    [Theory]
    [InlineData("\"corner\": \"southWest\"", "\"corner\": \"left\"", "left", "southWest, southEast, northEast, northWest")]
    [InlineData("\"edge\": \"north\"", "\"edge\": \"up\"", "up", "south, east, north, west")]
    [Trait("Feature", "PRJ-002")]
    public void A_corner_or_an_edge_a_cut_does_not_spell_is_refused(
        string original,
        string replacement,
        string named,
        string offered)
        => Scenes.RefuseWith(
            EveryKindOfCut.With(original, replacement),
            LoadProblemKind.UnknownValue,
            named,
            offered);

    [Theory]
    [InlineData("\"kind\": \"roundedCorner\", ", "", "kind")]
    [InlineData("\"corner\": \"southWest\", ", "", "corner")]
    [InlineData("\"corner\": \"southWest\", \"radius\": 1024", "\"corner\": \"southWest\"", "radius")]
    [InlineData("\"alongX\": 3072, ", "", "alongX")]
    [InlineData(", \"alongY\": 5120", "", "alongY")]
    [InlineData("\"edge\": \"north\", ", "", "edge")]
    [InlineData("\"bow\": \"inward\", ", "", "bow")]
    [InlineData("\"bow\": \"inward\", \"depth\": 2048", "\"bow\": \"inward\"", "depth")]
    [Trait("Feature", "PRJ-002")]
    public void Every_field_a_cut_of_that_kind_carries_is_required(string original, string replacement, string named)
        => Scenes.RefuseWith(
            EveryKindOfCut.With(original, replacement),
            LoadProblemKind.MissingField,
            named);

    [Theory]
    [InlineData("{ \"kind\": \"roundedCorner\", \"corner\": \"southWest\", \"radius\": 1024 }", "5", "a cut", "a number")]
    [InlineData("\"kind\": \"roundedCorner\"", "\"kind\": 7", "kind", "a number")]
    [InlineData("\"radius\": 1024", "\"radius\": \"one inch\"", "radius", "text")]
    [Trait("Feature", "PRJ-002")]
    public void A_cut_of_the_wrong_json_shape_is_refused(
        string original,
        string replacement,
        string named,
        string found)
        => Scenes.RefuseWith(
            EveryKindOfCut.With(original, replacement),
            LoadProblemKind.Malformed,
            named,
            $"found {found}");

    [Fact]
    [Trait("Feature", "PRJ-004")]
    public void A_format_version_2_file_is_refused_with_the_message_the_reader_already_gives()
    {
        // Nothing about the file is wrong except its version, and the missing "cuts" is never
        // reported: the stamp is judged before any of the scene is parsed, so the message is the
        // one the reader has always given for a version it does not read. Per beta policy there
        // is no converter.
        LoadProblem problem = Assert.Single(Scenes.Refuse(TheFormatBeforeCuts).Problems);

        Assert.Equal(LoadProblemKind.UnsupportedFormatVersion, problem.Kind);
        Assert.Contains("format version 2", problem.Message, StringComparison.Ordinal);
        Assert.Contains($"format version {SceneReader.FormatVersion}", problem.Message, StringComparison.Ordinal);
        Assert.Contains("no migration code", problem.Message, StringComparison.Ordinal);
    }
}
