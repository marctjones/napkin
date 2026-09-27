using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// Sketch from words (docs/design/llm-assistant.md &#xA7;4.3&#x2013;&#xA7;4.4, &#xA7;9.2, &#xA7;11.1 item 4):
/// the strict parser, the snapping, the limits, the sheet's sentences and the rough planks. Every
/// expectation is the note's hand-worked quick bench (sketch-mode &#xA7;7.2) or worked out here in a
/// comment; napkin's output is never the source.
/// </summary>
public class SketchProposalTests
{
    /// <summary>§9.2's scripted reply, verbatim.</summary>
    public const string QuickBench = """
        { "parts": [
            { "name": "Top",       "width": "4'-0\"", "height": "2\"",   "depth": "3/4\"", "x": "0",     "y": "1'-4\"", "quantity": 1 },
            { "name": "Leg 1",     "width": "4\"",    "height": "1'-4\"", "depth": "3/4\"", "x": "2\"",   "y": "0",      "quantity": 1 },
            { "name": "Leg 2",     "width": "4\"",    "height": "1'-4\"", "depth": "3/4\"", "x": "3'-6\"", "y": "0",     "quantity": 1 },
            { "name": "Stretcher", "width": "3'-0\"", "height": "3\"",   "depth": "3/4\"", "x": "6\"",   "y": "4\"",    "quantity": 1 } ],
          "note": "A plank bench: a top on two legs, a stretcher between them." }
        """;

    static readonly LayerId Layer = LayerId.Default;

    static string Json(params string[] parts) => $$"""{"parts":[{{string.Join(",", parts)}}],"note":"n"}""";

    static string PartJson(string name = "P", string width = "4\"", string height = "16\"", string depth = "3/4\"", string x = "0", string y = "0", string quantity = "1")
        => Object(PartMembers(name, width, height, depth, x, y, quantity));

    /// <summary>A part's members as (name, raw JSON value) pairs, in the schema's order.</summary>
    static List<(string Name, string Raw)> PartMembers(string name = "P", string width = "4\"", string height = "16\"", string depth = "3/4\"", string x = "0", string y = "0", string quantity = "1")
        =>
        [
            ("name", JsonSerializer.Serialize(name)),
            ("width", JsonSerializer.Serialize(width)),
            ("height", JsonSerializer.Serialize(height)),
            ("depth", JsonSerializer.Serialize(depth)),
            ("x", JsonSerializer.Serialize(x)),
            ("y", JsonSerializer.Serialize(y)),
            ("quantity", quantity),
        ];

    static string Object(IEnumerable<(string Name, string Raw)> members)
        => "{" + string.Join(",", members.Select(member => $"{JsonSerializer.Serialize(member.Name)}:{member.Raw}")) + "}";

    static ProposalPlan PlanOf(string document, Sketch? sketch = null, Func<string>? nextName = null, LayerId? layer = null)
        => SketchProposal.Parse(document)!.Plan(sketch ?? Sketch.Empty, layer ?? Layer, nextName ?? (() => throw new InvalidOperationException("No part here is unnamed.")));

    static Box Plank(ProposalLine line) => Assert.IsType<AddEntity>(Assert.Single(line.Requests)).Entity as Box
        ?? throw new InvalidOperationException("Not a box.");

    static void AssertPlank(Box box, string name, double x, double y, double width, double height, Length depth, PlanAxes axes, int quantity = 1)
    {
        Assert.Equal(name, box.Name);
        Assert.Equal(new Point3(Length.FromInches(x, Rounding.HalfAwayFromZero), Length.FromInches(y, Rounding.HalfAwayFromZero), Length.Zero), box.Anchor);
        Assert.Equal(Length.FromInches(width, Rounding.HalfAwayFromZero), box.Width);
        Assert.Equal(Length.FromInches(height, Rounding.HalfAwayFromZero), box.Height);
        Assert.Equal(depth, box.Depth);
        Assert.Equal(BoxFace.Top, box.FaceUp);
        Assert.Equal(Angle.Zero, box.Rotation);
        Assert.Empty(box.Cuts);

        // The rough plank of sketch-mode §2.3, exactly: no stock, no species, marked rough, the
        // longer plan side its length (the rectangle tool's rule).
        Part part = Assert.IsType<Part>(box.Part);
        Assert.Null(part.Stock);
        Assert.Null(part.Species);
        Assert.Equal(quantity, part.Quantity);
        Assert.Equal(axes, part.PlanAxes);
        Assert.True(part.Rough);
        Assert.Null(box.WallInputs);
    }

    static readonly PlanAxes LengthAlongX = new(PartDimension.Length, PartDimension.Width);
    static readonly PlanAxes LengthAlongY = new(PartDimension.Width, PartDimension.Length);

    [Trait("Feature", "AST-004")]
    [Fact]
    public void The_quick_bench_becomes_four_rough_planks_with_the_exact_sizes_and_anchors_of_sketch_mode_7_2()
    {
        ProposalPlan plan = PlanOf(QuickBench);

        // §9.2's sheet, word for word: sizes and anchors in inches after snapping.
        Assert.Equal(
            [
                "Top: 48 × 2 × 3/4 at (0, 16)",
                "Leg 1: 4 × 16 × 3/4 at (2, 0)",
                "Leg 2: 4 × 16 × 3/4 at (42, 0)",
                "Stretcher: 36 × 3 × 3/4 at (6, 4)",
            ],
            plan.Lines.Select(line => line.Sentence));
        Assert.All(plan.Lines, line => Assert.False(line.Refused));
        Assert.All(plan.Lines.Select(Plank), box => Assert.Equal(Layer, box.Layer));
        Assert.Equal(SketchProposal.What, plan.What);
        Assert.Equal("Assistant sketch", plan.What);
        Assert.True(plan.IsFor(Sketch.Empty));

        // sketch-mode §7.2's table: Top (0, 16) 48 x 2, Leg 1 (2, 0) 4 x 16, Leg 2 (42, 0), Stretcher (6, 4) 36 x 3; depth 3/4.
        Length threeQuarters = Length.Inches(0, 3, 4);
        Box[] planks = [.. plan.Lines.Select(Plank)];
        AssertPlank(planks[0], "Top", 0, 16, 48, 2, threeQuarters, LengthAlongX);
        AssertPlank(planks[1], "Leg 1", 2, 0, 4, 16, threeQuarters, LengthAlongY);
        AssertPlank(planks[2], "Leg 2", 42, 0, 4, 16, threeQuarters, LengthAlongY);
        AssertPlank(planks[3], "Stretcher", 6, 4, 36, 3, threeQuarters, LengthAlongX);

        // Ids ascend in the reply's order, as parts drawn one after another would, and are distinct.
        Assert.Equal(planks.Select(box => box.Id).Order(), planks.Select(box => box.Id));
        Assert.Equal(4, planks.Select(box => box.Id).Distinct().Count());
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void Drawn_through_the_editor_the_quick_bench_is_one_undo_step_and_firm_up_proposes_sketch_mode_7_2s_four_contacts()
    {
        DesignEditor editor = new();
        ProposalPlan plan = PlanOf(QuickBench, editor.Sketch, layer: editor.LayerForNewParts());

        ProposalOutcome outcome = plan.Accept(editor, plan.Lines, SketchProposal.MessageLine);

        Assert.Equal(4, outcome.Landed);
        Assert.False(outcome.Stale);
        Assert.Empty(outcome.Rejections);
        Assert.Equal(1, editor.History.UndoCount);
        Assert.Equal("Assistant sketch", editor.History.UndoWhat);
        Assert.Equal("Assistant sketch: drew 4 rough parts.", editor.LastMessage!.Text);
        Assert.Equal(EditSeverity.Done, editor.LastMessage.Severity);

        // Nothing stated, nothing chosen: no relationship, no stock (§4.4).
        Assert.Empty(editor.Sketch.RelationshipsInOrder);
        Box[] boxes = [.. editor.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        Assert.Equal(["Top", "Leg 1", "Leg 2", "Stretcher"], boxes.Select(box => box.Name));

        // The whole path from a sentence to a firmed design (§9.2): Firm up proposes §7.2's four
        // Flushes, lower id first, exactly as for the hand-drawn bench.
        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(editor.Sketch, [.. boxes.Select(box => box.Id)]);
        Assert.Equal(
            [
                "Top's south face against Leg 1's north face",
                "Top's south face against Leg 2's north face",
                "Leg 1's east face against Stretcher's west face",
                "Leg 2's west face against Stretcher's east face",
            ],
            proposals.Select(proposal => proposal.Sentence));

        // One undo takes all four away.
        Assert.True(editor.Undo());
        Assert.Empty(editor.Sketch.Entities);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_size_of_zero_is_refused_on_its_line_and_the_rest_stand()
    {
        string document = QuickBench.Replace("\"height\": \"1'-4\\\"\", \"depth\": \"3/4\\\"\", \"x\": \"2\\\"\"", "\"height\": \"0\", \"depth\": \"3/4\\\"\", \"x\": \"2\\\"\"", StringComparison.Ordinal);
        Assert.NotEqual(QuickBench, document);

        ProposalPlan plan = PlanOf(document);

        Assert.Equal(
            ["Top: 48 × 2 × 3/4 at (0, 16)", "Leg 1: refused, a size of 0", "Leg 2: 4 × 16 × 3/4 at (42, 0)", "Stretcher: 36 × 3 × 3/4 at (6, 4)"],
            plan.Lines.Select(line => line.Sentence));
        Assert.True(plan.Lines[1].Refused);
        Assert.Empty(plan.Lines[1].Requests);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_25th_part_is_refused_and_the_first_24_stand()
    {
        string[] parts = [.. Enumerable.Range(1, 26).Select(n => PartJson(name: $"Slat {n}", x: $"{n * 5}\""))];

        ProposalPlan plan = PlanOf(Json(parts));

        Assert.Equal(26, plan.Lines.Length);
        Assert.All(plan.Lines.Take(24), line => Assert.False(line.Refused));
        Assert.Equal("Slat 24: 4 × 16 × 3/4 at (120, 0)", plan.Lines[23].Sentence);
        Assert.Equal("Slat 25: refused, more than 24 parts", plan.Lines[24].Sentence);
        Assert.Equal("Slat 26: refused, more than 24 parts", plan.Lines[25].Sentence);
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    // Plan sizes and anchors snap to whole inches (the rough ladder's floor), half away from zero.
    [InlineData("15.9\"", "16\"", "3/4\"", "0", "0", "P: 16 × 16 × 3/4 at (0, 0)")]
    [InlineData("4\"", "15 1/2\"", "3/4\"", "0", "0", "P: 4 × 16 × 3/4 at (0, 0)")]
    [InlineData("4\"", "16\"", "3/4\"", "1/3\"", "0", "P: 4 × 16 × 3/4 at (0, 0)")]
    [InlineData("4\"", "16\"", "3/4\"", "-2.6", "11.5", "P: 4 × 16 × 3/4 at (-3, 12)")]
    // The depth snaps to the ladder's finest rung, 1/4": 3/4 and 1 1/2 stand, 1/3 becomes 1/4, 23/32 becomes 3/4.
    [InlineData("4\"", "16\"", "1 1/2\"", "0", "0", "P: 4 × 16 × 1 1/2 at (0, 0)")]
    [InlineData("4\"", "16\"", "1/3\"", "0", "0", "P: 4 × 16 × 1/4 at (0, 0)")]
    [InlineData("4\"", "16\"", "23/32\"", "0", "0", "P: 4 × 16 × 3/4 at (0, 0)")]
    // Any form napkin's length parser reads.
    [InlineData("4 in", "1 ft 4 in", ".75", "3'", "-1'-6\"", "P: 4 × 16 × 3/4 at (36, -18)")]
    // The anchor limit is ±1000", inclusive.
    [InlineData("4\"", "16\"", "3/4\"", "1000", "-1000", "P: 4 × 16 × 3/4 at (1000, -1000)")]
    public void Every_length_is_snapped_so_nothing_off_the_grid_reaches_the_design(string width, string height, string depth, string x, string y, string sentence)
    {
        ProposalPlan plan = PlanOf(Json(PartJson(width: width, height: height, depth: depth, x: x, y: y)));

        ProposalLine line = Assert.Single(plan.Lines);
        Assert.Equal(sentence, line.Sentence);
        Box box = Plank(line);
        Assert.All(
            [box.Width, box.Height, box.Anchor.X, box.Anchor.Y],
            length => Assert.Equal(0L, length.Units % Length.UnitsPerInch));
        Assert.Equal(0L, box.Depth.Units % (Length.UnitsPerInch / 4));
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    // §9.2: 1/3" snaps to 0 and is refused as a size of 0; a negative size is refused with its value.
    [InlineData("4\"", "1/3\"", "3/4\"", "0", "0", "1", "P: refused, a size of 0")]
    [InlineData("-4", "16\"", "3/4\"", "0", "0", "1", "P: refused, a size of -4")]
    [InlineData("4\"", "16\"", "1/10\"", "0", "0", "1", "P: refused, a size of 0")]
    [InlineData("0.4", "16\"", "3/4\"", "0", "0", "1", "P: refused, a size of 0")]
    // An anchor past ±1000" once snapped.
    [InlineData("4\"", "16\"", "3/4\"", "1000.5", "0", "1", "P: refused, an anchor at (1001, 0), beyond ±1000")]
    [InlineData("4\"", "16\"", "3/4\"", "0", "-84'", "1", "P: refused, an anchor at (0, -1008), beyond ±1000")]
    // A quantity outside 1 to 12.
    [InlineData("4\"", "16\"", "3/4\"", "0", "0", "13", "P: refused, a quantity of 13, not 1 to 12")]
    [InlineData("4\"", "16\"", "3/4\"", "0", "0", "0", "P: refused, a quantity of 0, not 1 to 12")]
    [InlineData("4\"", "16\"", "3/4\"", "0", "0", "-1", "P: refused, a quantity of -1, not 1 to 12")]
    // A length napkin's parser does not read: refused, never guessed — the first one named.
    [InlineData("about a foot", "16\"", "3/4\"", "0", "0", "1", "P: refused, its width is not a length napkin reads")]
    [InlineData("4\"", "16 mm", "3/4\"", "0", "0", "1", "P: refused, its height is not a length napkin reads")]
    [InlineData("4\"", "16\"", "", "0", "0", "1", "P: refused, its depth is not a length napkin reads")]
    [InlineData("4\"", "16\"", "3/4\"", "left", "0", "1", "P: refused, its x is not a length napkin reads")]
    [InlineData("4\"", "16\"", "3/4\"", "0", "5.", "1", "P: refused, its y is not a length napkin reads")]
    [InlineData("4 cm", "16 mm", "3/4\"", "0", "0", "1", "P: refused, its width is not a length napkin reads")]
    public void Each_limit_refuses_its_line_with_the_reason(string width, string height, string depth, string x, string y, string quantity, string sentence)
    {
        ProposalPlan plan = PlanOf(Json(PartJson(width: width, height: height, depth: depth, x: x, y: y, quantity: quantity)));

        ProposalLine line = Assert.Single(plan.Lines);
        Assert.Equal(sentence, line.Sentence);
        Assert.True(line.Refused);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void Twelve_pieces_is_the_most_a_line_may_stand_for_and_the_sentence_says_how_many()
    {
        ProposalPlan plan = PlanOf(Json(PartJson(name: "Slat", width: "3\"", height: "18\"", quantity: "12"), PartJson(name: "Leg", quantity: "2")));

        Assert.Equal(["Slat: 3 × 18 × 3/4 at (0, 0), 12 pieces", "Leg: 4 × 16 × 3/4 at (0, 0), 2 pieces"], plan.Lines.Select(line => line.Sentence));
        Assert.Equal(12, Plank(plan.Lines[0]).Part!.Quantity);
        Assert.Equal(2, Plank(plan.Lines[1]).Part!.Quantity);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void An_unnamed_part_takes_napkins_next_name_and_an_unnamed_refused_one_is_called_by_its_place()
    {
        List<string> handedOut = [];
        int next = 7;
        string NextName()
        {
            string name = $"Part {next++}";
            handedOut.Add(name);
            return name;
        }

        ProposalPlan plan = PlanOf(
            Json(PartJson(name: "  "), PartJson(name: "", width: "0"), PartJson(name: " Shelf "), PartJson(name: "")),
            nextName: NextName);

        // Only the two drawable unnamed parts are given a name, in order; the refused one is not.
        Assert.Equal(
            ["Part 7: 4 × 16 × 3/4 at (0, 0)", "Unnamed part 2: refused, a size of 0", "Shelf: 4 × 16 × 3/4 at (0, 0)", "Part 8: 4 × 16 × 3/4 at (0, 0)"],
            plan.Lines.Select(line => line.Sentence));
        Assert.Equal(["Part 7", "Part 8"], handedOut);
        Assert.Equal("Shelf", Plank(plan.Lines[2]).Name);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_readable_reply_with_no_parts_is_an_empty_sheet()
    {
        SketchProposal proposal = SketchProposal.Parse("""{"parts":[],"note":""}""")!;

        Assert.Empty(proposal.Parts);
        Assert.Equal(string.Empty, proposal.Note);
        Assert.Empty(proposal.Plan(Sketch.Empty, Layer, () => "Part 1").Lines);
        Assert.Equal("The assistant proposed no parts.", SketchProposal.NoParts);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void The_parser_reads_the_quick_bench_member_for_member()
    {
        Assert.True(SketchProposal.TryParse(QuickBench, out SketchProposal? proposal, out string? why));
        Assert.Null(why);
        Assert.Equal("A plank bench: a top on two legs, a stretcher between them.", proposal.Note);
        Assert.Equal(new SketchPart("Leg 2", "4\"", "1'-4\"", "3/4\"", "3'-6\"", "0", 1), proposal.Parts[2]);
        Assert.Equal(4, proposal.Parts.Length);
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    [InlineData("", "it is not JSON")]
    [InlineData("A bench, four parts.", "it is not JSON")]
    [InlineData("```json\n{\"parts\":[],\"note\":\"\"}\n```", "it is not JSON")]
    [InlineData("{\"parts\":[],\"note\":\"\",}", "it is not JSON")]
    [InlineData("{\"parts\":[],\"note\":\"\"} {}", "it is not JSON")]
    [InlineData("[]", "the reply is not an object")]
    [InlineData("{\"parts\":[],\"note\":\"\",\"stock\":\"2x4\"}", "the reply has a member napkin does not know: \"stock\"")]
    [InlineData("{\"Parts\":[],\"note\":\"\"}", "the reply has a member napkin does not know: \"Parts\"")]
    [InlineData("{\"parts\":[],\"note\":\"\",\"note\":\"again\"}", "the reply has \"note\" twice")]
    [InlineData("{\"parts\":[]}", "the reply has no \"note\"")]
    [InlineData("{\"note\":\"\"}", "the reply has no \"parts\"")]
    [InlineData("{\"parts\":{},\"note\":\"\"}", "\"parts\" is not a list")]
    [InlineData("{\"parts\":[],\"note\":null}", "\"note\" is not text")]
    [InlineData("{\"parts\":[\"Top\"],\"note\":\"\"}", "part 1 is not an object")]
    public void A_reply_that_is_not_exactly_the_schema_is_refused_whole(string document, string reason)
    {
        Assert.False(SketchProposal.TryParse(document, out SketchProposal? proposal, out string? why));
        Assert.Null(proposal);
        Assert.Equal(reason, why);
        Assert.Null(SketchProposal.Parse(document));
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    [InlineData("stock", "\"2x4\"", null, "part 2 has a member napkin does not know: \"stock\"")]
    [InlineData("rough", "false", null, "part 2 has a member napkin does not know: \"rough\"")]
    [InlineData("Width", "\"4\"", null, "part 2 has a member napkin does not know: \"Width\"")]
    [InlineData("name", "\"again\"", null, "part 2 has \"name\" twice")]
    [InlineData(null, null, "quantity", "part 2 has no \"quantity\"")]
    [InlineData(null, null, "name", "part 2 has no \"name\"")]
    public void A_part_with_a_member_too_many_or_too_few_refuses_the_whole_reply(string? extra, string? raw, string? without, string reason)
    {
        List<(string Name, string Raw)> second = PartMembers(name: "B");
        if (extra is not null)
        {
            second.Add((extra, raw!));
        }

        second.RemoveAll(member => member.Name == without);

        Assert.False(SketchProposal.TryParse(Json(PartJson(name: "A"), Object(second)), out _, out string? why));
        Assert.Equal(reason, why);
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    [InlineData("name", "null", "part 1: \"name\" is not text")]
    [InlineData("width", "48", "part 1: \"width\" is not text")]
    [InlineData("height", "[\"16\"]", "part 1: \"height\" is not text")]
    [InlineData("depth", "0.75", "part 1: \"depth\" is not text")]
    [InlineData("x", "{}", "part 1: \"x\" is not text")]
    [InlineData("y", "false", "part 1: \"y\" is not text")]
    [InlineData("quantity", "\"2\"", "part 1: \"quantity\" is not a whole number")]
    [InlineData("quantity", "1.5", "part 1: \"quantity\" is not a whole number")]
    [InlineData("quantity", "1.0", "part 1: \"quantity\" is not a whole number")]
    [InlineData("quantity", "1e0", "part 1: \"quantity\" is not a whole number")]
    [InlineData("quantity", "99999999999", "part 1: \"quantity\" is not a whole number")]
    [InlineData("quantity", "null", "part 1: \"quantity\" is not a whole number")]
    public void A_value_of_the_wrong_kind_refuses_the_whole_reply(string member, string value, string reason)
    {
        string part = Object(PartMembers().Select(each => each.Name == member ? (each.Name, value) : each));

        Assert.False(SketchProposal.TryParse(Json(part), out _, out string? why));
        Assert.Equal(reason, why);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void The_schema_is_byte_for_byte_the_one_the_mlx_bridge_prewarms_and_names_every_member_required()
    {
        // native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift: "the .NET side must send these exact bytes".
        string swift = Fixtures.RepositoryText("native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift");
        Assert.Contains("\n    " + SketchProposal.Schema + "\n", swift, StringComparison.Ordinal);

        using JsonDocument schema = JsonDocument.Parse(SketchProposal.Schema);
        JsonElement root = schema.RootElement;
        Assert.Equal(["parts", "note"], root.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        JsonElement item = root.GetProperty("properties").GetProperty("parts").GetProperty("items");
        Assert.Equal(
            ["name", "width", "height", "depth", "x", "y", "quantity"],
            item.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("integer", item.GetProperty("properties").GetProperty("quantity").GetProperty("type").GetString());
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_sketch_request_carries_the_sketch_prompt_the_pack_and_the_schema()
    {
        ContextPack pack = ContextPack.Of([(ContextKind.Site, "Site: ground snow load not entered.")]);
        ModelRequest request = ModelRequest.ForProposal(AssistantPrompts.Sketch, pack, "a bench", SketchProposal.Schema);

        Assert.Equal(AssistantPrompts.Sketch, request.System);
        Assert.Equal(pack.Text, request.Context);
        Assert.Equal("a bench", request.Question);
        Assert.Equal(SketchProposal.Schema, request.Schema);
        Assert.Throws<ArgumentNullException>(() => ModelRequest.ForProposal(AssistantPrompts.Sketch, null!, "a bench", SketchProposal.Schema));
        Assert.Throws<ArgumentNullException>(() => ModelRequest.ForProposal(AssistantPrompts.Sketch, pack, "a bench", null!));
    }

    [Trait("Feature", "AST-004")]
    [Theory]
    [InlineData(0, "Drew nothing.", "Assistant sketch: drew nothing.")]
    [InlineData(1, "Drew 1 rough part. Next: F to firm up.", "Assistant sketch: drew 1 rough part.")]
    [InlineData(4, "Drew 4 rough parts. Next: F to firm up.", "Assistant sketch: drew 4 rough parts.")]
    public void The_closing_line_and_the_message_bar_line_verbatim(int drawn, string closing, string message)
    {
        Assert.Equal(closing, SketchProposal.ClosingLine(drawn));
        Assert.Equal(message, SketchProposal.MessageLine(drawn));
    }

    [Fact]
    public void Nulls_are_refused_at_the_door()
    {
        Assert.Throws<ArgumentNullException>(() => SketchProposal.TryParse(null!, out _, out _));
        SketchProposal proposal = SketchProposal.Parse(QuickBench)!;
        Assert.Throws<ArgumentNullException>(() => proposal.Plan(null!, Layer, () => "Part 1"));
        Assert.Throws<ArgumentNullException>(() => proposal.Plan(Sketch.Empty, Layer, null!));
    }
}
