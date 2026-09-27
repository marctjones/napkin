using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// Edit in words (docs/design/llm-assistant.md &#xA7;4.3, &#xA7;4.5, &#xA7;11.1 items 5 and 8): the closed
/// set of six edits, each the request the panel already makes for that change, on
/// <c>samples/coffee-table</c>; the part named by <c>[n]</c> or by name and refused on no match or
/// two; a length that would need rounding refused, never snapped; the strict parser. Every expected
/// size is the sample's own (its design.md) or worked out here in a comment; napkin's output is never
/// the source of an expectation.
/// </summary>
public class EditProposalTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;

    /// <summary>coffee-table.design.md: each leg is 16 1/4" long, its depth in the scene.</summary>
    static readonly Length LegLength = Length.Inches(16, 1, 4);

    static Design CoffeeTable() => Fixtures.Sample("coffee-table");

    /// <summary>The pack the app builds for a question about the design with these selected: no code, nothing checked, no list open.</summary>
    static ContextPack PackFor(Design design, params string[] selected)
        => ContextPack.For(design, [.. selected.Select(name => Fixtures.Named(design, name))], ContextChecks.None, [], "an edit");

    static string Reply(params string[] edits) => $$"""{"edits":[{{string.Join(",", edits)}}]}""";

    static string Resize(string part, string dimension, string length)
        => Object(("edit", "resize"), ("part", part), ("dimension", dimension), ("length", length));

    static string Move(string part, string x, string y) => Object(("edit", "move"), ("part", part), ("x", x), ("y", y));

    static string Rename(string part, string name) => Object(("edit", "rename"), ("part", part), ("name", name));

    static string Stock(string part, string stock) => Object(("edit", "stock"), ("part", part), ("stock", stock));

    static string Quantity(string part, int quantity)
        => $$"""{"edit":"quantity","part":{{JsonSerializer.Serialize(part)}},"quantity":{{quantity}}}""";

    static string Remove(string part) => Object(("edit", "remove"), ("part", part));

    static string Object(params (string Name, string Value)[] members)
        => "{" + string.Join(",", members.Select(member => $"{JsonSerializer.Serialize(member.Name)}:{JsonSerializer.Serialize(member.Value)}")) + "}";

    static ProposalPlan PlanOf(string reply, Design design, ContextPack pack)
        => EditProposal.Parse(reply)!.Plan(design, pack, Library);

    static Request Only(ProposalLine line) => Assert.Single(line.Requests);

    static EditorAndPlan OnCoffeeTable(string reply, params string[] selected)
    {
        DesignEditor editor = new();
        editor.Open(CoffeeTable());
        ContextPack pack = PackFor(editor.Design, selected);
        return new EditorAndPlan(editor, PlanOf(reply, editor.Design, pack), pack);
    }

    sealed record EditorAndPlan(DesignEditor Editor, ProposalPlan Plan, ContextPack Pack);

    static Box BoxNamed(DesignEditor editor, string name) => Assert.Single(editor.Sketch.Entities.Values.OfType<Box>(), box => box.Name == name);

    /// <summary>
    /// A request with its fresh relationship ids set aside, so a proposal's request can be compared
    /// with the one the panel's own code makes: a new <see cref="ParamValue"/> is its size and value.
    /// </summary>
    static object Shape(Request request) => request switch
    {
        AddRelationship { Relationship: ParamValue value } => ("state", value.Param, value.Value),
        Batch batch => batch.Requests.Select(Shape).ToImmutableArray() is var shapes ? string.Join(" | ", shapes) : string.Empty,
        _ => request,
    };

    // ---- The six, each the panel's request ------------------------------------------------

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Resize_is_typing_the_size_a_ParamValue_on_the_boxs_depth_and_the_leg_is_exactly_18_inches_after_one_undo_step()
    {
        // GUI-AST-05's edit: "make it 18 inches tall" with the south-west leg selected. A leg stands
        // on its depth (coffee-table.design.md), which the pack calls its length.
        EditorAndPlan made = OnCoffeeTable(Reply(Resize("[1]", "depth", "1'-6\"")), "Leg, south-west");
        Box leg = BoxNamed(made.Editor, "Leg, south-west");
        Assert.Equal(leg.Id, made.Pack.EntityAt(1));
        Assert.StartsWith("Selected: Leg, south-west — part, 1'-4 1/4\" long", made.Pack.Item(1)!.Text, StringComparison.Ordinal);

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal("Leg, south-west: length 1'-4 1/4\" to 1'-6\"", line.Sentence);

        // Nothing drives the leg's depth in the sample, so typing it states it: AddRelationship(ParamValue).
        Assert.Null(DimensionEntry.DrivingRelationship(made.Editor.Sketch, new BoxDepthRef(leg.Id)));
        Assert.Equal(("state", (ParamRef)new BoxDepthRef(leg.Id), Length.Inches(18)), Shape(Only(line)));

        ProposalOutcome outcome = made.Plan.Accept(made.Editor, made.Plan.Lines, EditProposal.MessageLine);
        Assert.Equal(1, outcome.Landed);
        Assert.Equal(Length.Inches(18), BoxNamed(made.Editor, "Leg, south-west").Depth);
        RelationshipId driving = Assert.IsType<RelationshipId>(DimensionEntry.DrivingRelationship(made.Editor.Sketch, new BoxDepthRef(leg.Id)));
        Assert.Equal(Length.Inches(18), made.Editor.Sketch.RelationshipsInOrder.OfType<ParamValue>().Single(value => value.Id == driving).Value);
        Assert.Equal(1, made.Editor.History.UndoCount);
        Assert.Equal("Assistant edit", made.Editor.History.UndoWhat);
        Assert.Equal("Assistant edit: made 1 edit.", made.Editor.LastMessage!.Text);

        // The other legs are not tied to its depth: they keep the sample's.
        Assert.Equal(LegLength, BoxNamed(made.Editor, "Leg, north-east").Depth);

        Assert.True(made.Editor.Undo());
        Assert.Equal(LegLength, BoxNamed(made.Editor, "Leg, south-west").Depth);
        Assert.Null(DimensionEntry.DrivingRelationship(made.Editor.Sketch, new BoxDepthRef(leg.Id)));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Resize_of_a_size_a_ParamValue_already_owns_is_SetParameter_on_it_one_number_one_owner()
    {
        // The sample states the south-west leg's width (a ParamValue of 2 1/2"); typing it changes that number.
        EditorAndPlan made = OnCoffeeTable(Reply(Resize("Leg, south-west", "width", "3\"")));
        Box leg = BoxNamed(made.Editor, "Leg, south-west");
        RelationshipId owner = Assert.IsType<RelationshipId>(DimensionEntry.DrivingRelationship(made.Editor.Sketch, new BoxWidthRef(leg.Id)));

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal(new SetParameter(owner, Length.Inches(3)), Only(line));
        Assert.Equal(DimensionEntry.RequestFor(made.Editor.Sketch, new BoxWidthRef(leg.Id), Length.Inches(3)), Only(line));

        // The leg's plan axes put its width along X: the sentence says "width" in the panel's word.
        Assert.Equal("Leg, south-west: width 2 1/2\" to 3\"", line.Sentence);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Resize_of_a_rough_part_carries_the_SetPart_clearing_rough_as_typing_does()
    {
        DesignEditor editor = new();
        ProposalPlan sketched = SketchProposal.Parse(SketchProposalTests.QuickBench)!
            .Plan(editor.Sketch, editor.LayerForNewParts(), () => throw new InvalidOperationException("Every part is named."));
        sketched.Accept(editor, sketched.Lines, SketchProposal.MessageLine);
        Box leg = BoxNamed(editor, "Leg 1");
        Assert.True(leg.Part!.Rough);

        // Two sizes of one rough part: both clear the mark to the same part, so both stand.
        ContextPack pack = ContextPack.For(editor.Design, [leg.Id], ContextChecks.None, [], "an edit");
        ProposalPlan plan = PlanOf(Reply(Resize("Leg 1", "height", "18\""), Resize("[1]", "width", "3 1/2\"")), editor.Design, pack);

        // A rough 4 x 16 plank's longer plan side (Y) is its length (sketch-mode §2.3).
        Assert.Equal(["Leg 1: length 1'-4\" to 1'-6\"", "Leg 1: width 4\" to 3 1/2\""], plan.Lines.Select(line => line.Sentence));
        // What the canvas's dimension field puts to the updater for the same typed size: the size and the mark cleared, one batch.
        Request typed = RoughEntry.Typed(editor.Sketch, leg.Id, DimensionEntry.RequestFor(editor.Sketch, new BoxHeightRef(leg.Id), Length.Inches(18)));
        Assert.Equal(Shape(typed), Shape(Only(plan.Lines[0])));
        Assert.Equal(new SetPart(leg.Id, leg.Part with { Rough = false }), Assert.IsType<Batch>(Only(plan.Lines[0])).Requests[1]);

        plan.Accept(editor, plan.Lines, EditProposal.MessageLine);
        Box firmed = BoxNamed(editor, "Leg 1");
        Assert.False(firmed.Part!.Rough);
        Assert.Equal(Length.Inches(18), firmed.Height);
        Assert.Equal(Length.Inches(3, 1, 2), firmed.Width);
        Assert.True(editor.Undo());
        Assert.True(BoxNamed(editor, "Leg 1").Part!.Rough);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Move_is_the_part_panels_typed_place_its_south_west_corner_moved_its_height_kept()
    {
        // coffee-table.design.md: the south-west leg's corner is 1 1/2" in from each edge, at z = 0.
        EditorAndPlan made = OnCoffeeTable(Reply(Move("Leg, south-west", "2\"", "3\"")));
        Box leg = BoxNamed(made.Editor, "Leg, south-west");
        Assert.Equal(new Point3(Length.Inches(1, 1, 2), Length.Inches(1, 1, 2), Length.Zero), SpaceSnapResolver.Extent(leg).Low);

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal("Leg, south-west: move to x 2\", y 3\"", line.Sentence);
        Assert.Equal(new SetPosition(leg.Id, new Point3(Length.Inches(2), Length.Inches(3), Length.Zero)), Only(line));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Rename_is_SetName_and_the_name_is_trimmed()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Rename("Apron, long, south", "  Apron 9 ")));
        Box apron = BoxNamed(made.Editor, "Apron, long, south");

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal("Apron, long, south: rename to \"Apron 9\"", line.Sentence);
        Assert.Equal(new SetName(apron.Id, "Apron 9"), Only(line));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Stock_by_a_name_the_library_carries_is_the_panels_assignment_and_the_part_stays_as_rough_as_it_was()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Stock("Top", "3/4 plywood")));
        Box top = BoxNamed(made.Editor, "Top");
        Assert.True(Library.TryFind("3/4 plywood", out StockItem plywood));

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal($"Top: stock {plywood.Name}", line.Sentence);
        Batch panel = StockAssignment.RequestsFor(made.Editor.Sketch, top, top.Part! with { Stock = plywood.Name }, plywood);
        Assert.Equal(Shape(panel), Shape(Only(line)));
        Assert.Equal(new SetPart(top.Id, top.Part! with { Stock = plywood.Name }), Assert.IsType<Batch>(Only(line)).Requests[0]);

        made.Plan.Accept(made.Editor, made.Plan.Lines, EditProposal.MessageLine);
        Assert.Equal(plywood.Name, BoxNamed(made.Editor, "Top").Part!.Stock);
        Assert.False(BoxNamed(made.Editor, "Top").Part!.Rough);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Stock_by_a_name_the_library_does_not_carry_is_refused_naming_the_three_nearest_and_nothing_is_guessed()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Stock("Top", "oak butcher block")));
        Box top = BoxNamed(made.Editor, "Top");

        // The three nearest are StockSuggestion's, read from the library at run time (§4.5).
        ImmutableArray<StockItem> near = StockSuggestion.For(top.Part!.SizeOn(top), Library);
        Assert.True(near.Length >= 3);
        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal(
            $"Top: refused, \"oak butcher block\" is not in napkin's materials library; nearest for this part's sizes: {near[0].Name}, {near[1].Name} or {near[2].Name}",
            line.Sentence);
        Assert.True(line.Refused);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Stock_refused_where_nothing_in_the_library_is_near_says_so()
    {
        // The top is 4' x 2' x 3/4": a leg-sized square stock would be near nothing if the leg were
        // 1' thick. A part 12" square in section is nowhere within an inch of any lumber or panel.
        DesignEditor editor = new();
        Box block = Box.AsDrawn(EntityId.New(), editor.LayerForNewParts(), Point2.Origin, Length.Inches(12), Length.Inches(12), Length.Inches(12), Angle.Zero) with
        {
            Name = "Block",
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Width, PartDimension.Thickness)),
        };
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new AddEntity(block), "draw"));
        Assert.Empty(StockSuggestion.For(block.Part!.SizeOn(block), Library));

        ProposalPlan plan = PlanOf(Reply(Stock("Block", "4x4 timber")), editor.Design, ContextPack.For(editor.Design, [], ContextChecks.None, [], "q"));
        Assert.Equal("Block: refused, \"4x4 timber\" is not in napkin's materials library; nothing in it is near this part's sizes", Assert.Single(plan.Lines).Sentence);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Quantity_is_SetPart_with_the_new_count_and_less_than_one_is_refused()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Quantity("Apron, short, west", 2), Quantity("Apron, short, east", 0), Quantity("Top", -3)));
        Box apron = BoxNamed(made.Editor, "Apron, short, west");

        Assert.Equal(
            ["Apron, short, west: quantity 1 to 2", "Apron, short, east: refused, a quantity of 0, not at least 1", "Top: refused, a quantity of -3, not at least 1"],
            made.Plan.Lines.Select(line => line.Sentence));
        Assert.Equal(new SetPart(apron.Id, apron.Part! with { Quantity = 2 }), Only(made.Plan.Lines[0]));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Remove_is_RemoveEntity()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Remove("Apron, short, east")));
        Box apron = BoxNamed(made.Editor, "Apron, short, east");

        ProposalLine line = Assert.Single(made.Plan.Lines);
        Assert.Equal("Apron, short, east: remove", line.Sentence);
        Assert.Equal(new RemoveEntity(apron.Id), Only(line));

        made.Plan.Accept(made.Editor, made.Plan.Lines, EditProposal.MessageLine);
        Assert.Null(made.Editor.Sketch.Find(apron.Id));
        Assert.True(made.Editor.Undo());
        Assert.NotNull(made.Editor.Sketch.Find(apron.Id));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Every_edit_in_one_acceptance_is_one_undo_step_and_the_unticked_do_not_land()
    {
        EditorAndPlan made = OnCoffeeTable(
            Reply(Resize("Leg, south-west", "depth", "18\""), Rename("Apron, long, north", "Back apron"), Quantity("Top", 2)),
            "Leg, south-west");

        ProposalOutcome outcome = made.Plan.Accept(made.Editor, [made.Plan.Lines[0], made.Plan.Lines[1]], EditProposal.MessageLine);

        Assert.Equal(2, outcome.Landed);
        Assert.Equal(Length.Inches(18), BoxNamed(made.Editor, "Leg, south-west").Depth);
        Assert.NotNull(BoxNamed(made.Editor, "Back apron"));
        Assert.Equal(1, BoxNamed(made.Editor, "Top").Part!.Quantity);
        Assert.Equal(1, made.Editor.History.UndoCount);
        Assert.Equal("Assistant edit: made 2 edits.", made.Editor.LastMessage!.Text);

        Assert.True(made.Editor.Undo());
        Assert.Equal(LegLength, BoxNamed(made.Editor, "Leg, south-west").Depth);
        Assert.NotNull(BoxNamed(made.Editor, "Apron, long, north"));
        Assert.Equal("Undone: Assistant edit.", made.Editor.LastMessage!.Text);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void A_line_the_updater_refuses_is_reported_in_its_words_and_the_rest_still_land()
    {
        // The south-east leg's width is held equal to the south-west's (an EqualParam in the sample),
        // so stating it as well is more than the design can hold; the rename still lands.
        EditorAndPlan made = OnCoffeeTable(Reply(Resize("Leg, south-east", "width", "3\""), Rename("Top", "Table top")));

        ProposalOutcome outcome = made.Plan.Accept(made.Editor, made.Plan.Lines, EditProposal.MessageLine);

        Assert.Equal(1, outcome.Landed);
        string rejection = Assert.Single(outcome.Rejections);
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(Length.Inches(2, 1, 2), BoxNamed(made.Editor, "Leg, south-east").Width);
        Assert.NotNull(BoxNamed(made.Editor, "Table top"));
        Assert.Equal(EditSeverity.Problem, made.Editor.LastMessage!.Severity);
    }

    // ---- Naming a part --------------------------------------------------------------------

    [Trait("Feature", "AST-005")]
    [Fact]
    public void A_part_is_named_by_its_item_number_or_by_its_name_whatever_the_letter_case()
    {
        Design design = CoffeeTable();
        ContextPack pack = PackFor(design, "Apron, long, south", "Leg, north-east");
        EntityId apron = Fixtures.Named(design, "Apron, long, south");
        EntityId leg = Fixtures.Named(design, "Leg, north-east");

        // The selection comes first in the pack, in id order: the leg (…05) before the apron (…06).
        Assert.Equal(leg, pack.EntityAt(1));
        Assert.Equal(apron, pack.EntityAt(2));

        ProposalPlan plan = PlanOf(Reply(Remove("[2]"), Remove(" apron, LONG, south "), Remove("[1]")), design, pack);

        Assert.Equal(
            ["Apron, long, south: remove", "Apron, long, south: remove", "Leg, north-east: remove"],
            plan.Lines.Select(line => line.Sentence));
        Assert.Equal(new RemoveEntity(apron), Only(plan.Lines[0]));
        Assert.Equal(new RemoveEntity(apron), Only(plan.Lines[1]));
        Assert.Equal(new RemoveEntity(leg), Only(plan.Lines[2]));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void A_name_that_matches_two_parts_is_refused_naming_both_and_one_that_matches_none_is_refused()
    {
        DesignEditor editor = new();
        editor.Open(CoffeeTable());
        Box south = BoxNamed(editor, "Apron, long, south");
        Box north = BoxNamed(editor, "Apron, long, north");
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(Batch.Of(new SetName(south.Id, "Apron"), new SetName(north.Id, "Apron")), "call both Apron"));

        ContextPack pack = ContextPack.For(editor.Design, [south.Id, north.Id], ContextChecks.None, [], "call it Apron 9");
        ProposalPlan plan = PlanOf(Reply(Rename("Apron", "Apron 9"), Rename("Apron 7", "Apron 9"), Rename("", "Apron 9")), editor.Design, pack);

        Assert.Equal(
            [
                "\"Apron\": refused, 2 parts are called that: [1] and [2]",
                "\"Apron 7\": refused, no part in the design is called that",
                "\"\": refused, no part in the design is called that",
            ],
            plan.Lines.Select(line => line.Sentence));
        Assert.All(plan.Lines, line => Assert.True(line.Refused));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void An_item_number_that_is_not_an_entity_and_an_entity_that_is_not_a_part_are_refused()
    {
        Design design = Fixtures.Sample("window-in-existing-wall");
        ContextPack pack = ContextPack.For(design, [], ContextChecks.None, [], "q");
        int site = pack.Items.Single(item => item.Kind == ContextKind.Site).N;
        Assert.Null(pack.EntityAt(site));

        ProposalPlan plan = PlanOf(
            Reply(Rename("Wall 1", "North wall"), Remove("Window 1"), Remove($"[{site}]"), Remove("[99]"), Remove("[0]"), Remove("[x]")),
            design,
            pack);

        Assert.Equal(
            [
                "Wall 1: refused, it is not a part",
                "Window 1: refused, it is not a part",
                $"[{site}]: refused, [{site}] is not a part in the design",
                "[99]: refused, [99] is not a part in the design",
                "[0]: refused, [0] is not a part in the design",
                "\"[x]\": refused, no part in the design is called that",
            ],
            plan.Lines.Select(line => line.Sentence));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void A_pack_made_from_items_alone_names_no_entity()
    {
        ContextPack pack = ContextPack.Of([(ContextKind.Entity, "Leg — part.")]);
        Assert.Null(pack.EntityAt(1));
        Assert.Null(pack.EntityAt(0));
    }

    // ---- Lengths: exact, or refused -------------------------------------------------------

    [Trait("Feature", "AST-005")]
    [Theory]
    // 18.005" is 18 5/1000", which is not a whole number of 1/1024": napkin would have to round it (§4.5).
    [InlineData("18.005\"", "Leg, south-west: refused, 18.005\" is not a size napkin can hold exactly")]
    [InlineData("18 1/3\"", "Leg, south-west: refused, 18 1/3\" is not a size napkin can hold exactly")]
    [InlineData("eighteen", "Leg, south-west: refused, its new size is not a length napkin reads")]
    [InlineData("0", "Leg, south-west: refused, a size of 0\"")]
    [InlineData("-6\"", "Leg, south-west: refused, a size of -6\"")]
    // On the grid is exact, whatever form it is written in: 18 1/1024" and 1.5 ft.
    [InlineData("18 1/1024\"", "Leg, south-west: length 1'-4 1/4\" to 1'-6 1/1024\"")]
    [InlineData("1.5 ft", "Leg, south-west: length 1'-4 1/4\" to 1'-6\"")]
    public void A_size_off_the_grid_is_refused_never_rounded(string length, string sentence)
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Resize("Leg, south-west", "depth", length)));
        Assert.Equal(sentence, Assert.Single(made.Plan.Lines).Sentence);
    }

    [Trait("Feature", "AST-005")]
    [Theory]
    [InlineData("2.0001\"", "3\"", "Leg, south-west: refused, 2.0001\" is not a place napkin can hold exactly")]
    [InlineData("2\"", "3.0001\"", "Leg, south-west: refused, 3.0001\" is not a place napkin can hold exactly")]
    [InlineData("two", "3\"", "Leg, south-west: refused, its x is not a length napkin reads")]
    [InlineData("2\"", "", "Leg, south-west: refused, its y is not a length napkin reads")]
    [InlineData("-1'", "0", "Leg, south-west: move to x -1'-0\", y 0\"")]
    public void A_place_off_the_grid_is_refused_never_rounded(string x, string y, string sentence)
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Move("Leg, south-west", x, y)));
        Assert.Equal(sentence, Assert.Single(made.Plan.Lines).Sentence);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void An_empty_name_is_refused()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Rename("Top", "   ")));
        Assert.Equal("Top: refused, an empty name", Assert.Single(made.Plan.Lines).Sentence);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Two_lines_setting_what_one_part_is_to_different_things_are_not_both_offered()
    {
        // Stock and quantity each set the whole Part: the second, made from the part as it was, would
        // put the first's field back. It is refused; one on another part stands.
        EditorAndPlan made = OnCoffeeTable(Reply(Stock("Top", "3/4 plywood"), Quantity("Top", 2), Quantity("Apron, short, west", 2), Quantity("Apron, short, west", 2)));

        Assert.Equal(
            [
                made.Plan.Lines[0].Sentence,
                "Top: refused, another line already changes what this part is; ask for this once that has landed",
                "Apron, short, west: quantity 1 to 2",
                "Apron, short, west: quantity 1 to 2",
            ],
            made.Plan.Lines.Select(line => line.Sentence));
        Assert.False(made.Plan.Lines[0].Refused);
    }

    // ---- The stale rule, the lines, the schema, the parser ---------------------------------

    [Trait("Feature", "AST-005")]
    [Fact]
    public void A_plan_made_against_a_design_that_has_since_changed_is_refused_whole()
    {
        EditorAndPlan made = OnCoffeeTable(Reply(Rename("Top", "Table top")));
        Box top = BoxNamed(made.Editor, "Top");
        Assert.IsAssignableFrom<Succeeded>(made.Editor.Apply(new SetName(top.Id, "Lid"), "rename"));

        ProposalOutcome outcome = made.Plan.Accept(made.Editor, made.Plan.Lines, EditProposal.MessageLine);

        Assert.True(outcome.Stale);
        Assert.Equal(0, outcome.Landed);
        Assert.Equal("Lid", BoxNamed(made.Editor, "Lid").Name);
        Assert.Equal(ProposalPlan.StaleText, made.Editor.LastMessage!.Text);
        Assert.Equal(EditProposal.What, made.Plan.What);
    }

    [Trait("Feature", "AST-005")]
    [Theory]
    [InlineData(0, "Changed nothing.", "Assistant edit: changed nothing.")]
    [InlineData(1, "Made 1 edit.", "Assistant edit: made 1 edit.")]
    [InlineData(3, "Made 3 edits.", "Assistant edit: made 3 edits.")]
    public void The_closing_line_and_the_message_bar_line_verbatim(int made, string closing, string message)
    {
        Assert.Equal(closing, EditProposal.ClosingLine(made));
        Assert.Equal(message, EditProposal.MessageLine(made));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void The_schema_is_byte_for_byte_the_one_the_mlx_bridge_prewarms_and_holds_exactly_the_six_edits()
    {
        // native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift: "the .NET side must send these exact bytes".
        string swift = Fixtures.RepositoryText("native/NapkinMlx/Sources/NapkinMlxSchemas/Schemas.swift");
        Assert.Contains("\n    " + EditProposal.Schema + "\n", swift, StringComparison.Ordinal);

        using JsonDocument schema = JsonDocument.Parse(EditProposal.Schema);
        JsonElement root = schema.RootElement;
        Assert.Equal(["edits"], root.GetProperty("required").EnumerateArray().Select(name => name.GetString()));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        // Each shape in the schema is exactly the parser's: its const, its members, all required, no other.
        JsonElement[] shapes = [.. root.GetProperty("properties").GetProperty("edits").GetProperty("items").GetProperty("anyOf").EnumerateArray()];
        Assert.Equal(6, shapes.Length);
        foreach (JsonElement shape in shapes)
        {
            string name = shape.GetProperty("properties").GetProperty("edit").GetProperty("const").GetString()!;
            string[] members = [.. shape.GetProperty("required").EnumerateArray().Select(member => member.GetString()!)];
            Assert.Equal(EditProposal.Members[name], members);
            Assert.Equal(members, shape.GetProperty("properties").EnumerateObject().Select(member => member.Name));
            Assert.False(shape.GetProperty("additionalProperties").GetBoolean());
        }

        Assert.Equal(EditProposal.Members.Keys.Order(StringComparer.Ordinal), shapes.Select(shape => shape.GetProperty("properties").GetProperty("edit").GetProperty("const").GetString()!).Order(StringComparer.Ordinal));
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void The_closed_set_is_exactly_six_edits()
    {
        Type[] edits = [.. typeof(PartEdit).GetNestedTypes(BindingFlags.Public).Where(type => type.IsSubclassOf(typeof(PartEdit)))];
        Assert.Equal(
            ["Move", "Quantity", "Remove", "Rename", "Resize", "Stock"],
            edits.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(edits, type => Assert.True(type.IsSealed));

        // Nothing outside can add a seventh: the base's only constructor is private.
        Assert.All(typeof(PartEdit).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters() is not [{ ParameterType: var type }] || type != typeof(PartEdit)),
            constructor => Assert.True(constructor.IsPrivate));
        Assert.Equal(6, EditProposal.Members.Count);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void An_edit_request_carries_the_edit_prompt_the_pack_and_the_schema()
    {
        ContextPack pack = PackFor(CoffeeTable(), "Leg, south-west");
        ModelRequest request = ModelRequest.ForProposal(AssistantPrompts.Edit, pack, "make it 18 inches tall", EditProposal.Schema);

        Assert.Equal(AssistantPrompts.Edit, request.System);
        Assert.Equal(pack.Text, request.Context);
        Assert.Equal("make it 18 inches tall", request.Question);
        Assert.Equal(EditProposal.Schema, request.Schema);
    }

    [Trait("Feature", "AST-005")]
    [Fact]
    public void Every_one_of_the_six_parses_to_its_own_edit()
    {
        EditProposal proposal = EditProposal.Parse(Reply(
            Resize("[1]", "depth", "1'-6\""),
            Move("Top", "0", "1'"),
            Rename("Top", "Lid"),
            Stock("Top", "3/4 plywood"),
            Quantity("Top", 2),
            Remove("[2]")))!;

        Assert.Equal(
            [
                new PartEdit.Resize("[1]", EditedSize.Depth, "1'-6\""),
                new PartEdit.Move("Top", "0", "1'"),
                new PartEdit.Rename("Top", "Lid"),
                new PartEdit.Stock("Top", "3/4 plywood"),
                new PartEdit.Quantity("Top", 2),
                new PartEdit.Remove("[2]"),
            ],
            proposal.Edits);
        Assert.Equal(EditedSize.Width, EditProposal.Parse(Reply(Resize("P", "width", "1")))!.Edits[0] is PartEdit.Resize { Size: var width } ? width : EditedSize.Depth);
        Assert.Equal(EditedSize.Height, EditProposal.Parse(Reply(Resize("P", "height", "1")))!.Edits[0] is PartEdit.Resize { Size: var height } ? height : EditedSize.Depth);
        Assert.Equal("Top", proposal.Edits[1].Part);
        Assert.Empty(EditProposal.Parse("""{"edits":[]}""")!.Edits);
    }

    [Trait("Feature", "AST-005")]
    [Theory]
    [InlineData("not json", "it is not JSON")]
    [InlineData("""{"edits":[]} {}""", "it is not JSON")]
    [InlineData("""[]""", "the reply is not an object")]
    [InlineData("""{}""", "the reply has no \"edits\"")]
    [InlineData("""{"edits":[],"note":"n"}""", "the reply has a member napkin does not know: \"note\"")]
    [InlineData("""{"edits":{}}""", "\"edits\" is not a list")]
    [InlineData("""{"edits":["remove"]}""", "edit 1 is not an object")]
    [InlineData("""{"edits":[{"part":"Top"}]}""", "edit 1 does not say which edit it is")]
    [InlineData("""{"edits":[{"edit":2,"part":"Top"}]}""", "edit 1 does not say which edit it is")]
    // Anything outside the closed set fails to parse: a joint, a relationship, a wall's input, a phase.
    [InlineData("""{"edits":[{"edit":"joint","part":"Top"}]}""", "edit 1 is not an edit napkin makes: \"joint\"")]
    [InlineData("""{"edits":[{"edit":"flush","part":"Top"}]}""", "edit 1 is not an edit napkin makes: \"flush\"")]
    [InlineData("""{"edits":[{"edit":"phase","part":"Top"}]}""", "edit 1 is not an edit napkin makes: \"phase\"")]
    [InlineData("""{"edits":[{"edit":"Remove","part":"Top"}]}""", "edit 1 is not an edit napkin makes: \"Remove\"")]
    [InlineData("""{"edits":[{"edit":"remove","part":"Top","why":"x"}]}""", "edit 1 has a member napkin does not know: \"why\"")]
    [InlineData("""{"edits":[{"edit":"remove","part":"Top","part":"Leg"}]}""", "edit 1 has \"part\" twice")]
    [InlineData("""{"edits":[{"edit":"rename","part":"Top"}]}""", "edit 1 has no \"name\"")]
    [InlineData("""{"edits":[{"edit":"remove","part":3}]}""", "edit 1: \"part\" is not text")]
    [InlineData("""{"edits":[{"edit":"move","part":"Top","x":0,"y":"0"}]}""", "edit 1: \"x\" is not text")]
    [InlineData("""{"edits":[{"edit":"resize","part":"Top","dimension":"length","length":"1"}]}""", "edit 1: \"dimension\" is not width, height or depth")]
    [InlineData("""{"edits":[{"edit":"resize","part":"Top","dimension":"Width","length":"1"}]}""", "edit 1: \"dimension\" is not width, height or depth")]
    [InlineData("""{"edits":[{"edit":"quantity","part":"Top","quantity":"2"}]}""", "edit 1: \"quantity\" is not a whole number")]
    [InlineData("""{"edits":[{"edit":"quantity","part":"Top","quantity":1.5}]}""", "edit 1: \"quantity\" is not a whole number")]
    [InlineData("""{"edits":[{"edit":"quantity","part":"Top","quantity":null}]}""", "edit 1: \"quantity\" is not a whole number")]
    [InlineData("""{"edits":[{"edit":"remove","part":"Top"},{"edit":"remove"}]}""", "edit 2 has no \"part\"")]
    public void A_reply_that_is_not_exactly_the_schema_is_refused_whole(string document, string reason)
    {
        Assert.False(EditProposal.TryParse(document, out EditProposal? proposal, out string? why));
        Assert.Null(proposal);
        Assert.Equal(reason, why);
        Assert.Null(EditProposal.Parse(document));
    }

    [Fact]
    public void Nulls_are_refused_at_the_door()
    {
        Assert.Throws<ArgumentNullException>(() => EditProposal.TryParse(null!, out _, out _));
        EditProposal proposal = EditProposal.Parse(Reply(Remove("Top")))!;
        Design design = CoffeeTable();
        ContextPack pack = PackFor(design);
        Assert.Throws<ArgumentNullException>(() => proposal.Plan(null!, pack, Library));
        Assert.Throws<ArgumentNullException>(() => proposal.Plan(design, null!, Library));
        Assert.Throws<ArgumentNullException>(() => proposal.Plan(design, pack, null!));
    }
}
