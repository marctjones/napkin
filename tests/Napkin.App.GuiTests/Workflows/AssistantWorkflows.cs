using System.Collections.Immutable;
using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Assistant.Mlx;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Assistant;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;

using Xunit;

using Design = Napkin.Modules.Editing.Design;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Ask and Explain this result (docs/design/llm-assistant.md &#xA7;2.4, &#xA7;8, &#xA7;9.1,
/// &#xA7;11.3), driven the way a person drives it. A <see cref="ScriptedModel"/> stands in for a
/// real one: nothing here loads weights or opens a socket (&#xA7;2.1), and the script's item
/// references are read off a context pack built the same way the app builds one (headers, bracing
/// and deck checks together), never guessed from the design note's simpler hand-written golden.
/// </summary>
public class AssistantWorkflows
{
    static readonly string Shipped = Path.Combine(AppContext.BaseDirectory, "packs");

    [GuiWorkflow("GUI-AST-01")]
    public void Explain_a_no_data_header_then_a_made_up_size_is_refused()
    {
        (Design design, CodePacks packs, EntityId windowId) = LoadSample("window-in-existing-wall");
        const string question = "why is this header No data?";

        // The pack the app itself will build once the sample is open and Window 1 is selected —
        // §9.1's example plus the bracing check the real app also computes, which the hand-written
        // slice-A golden does not carry, so item numbers are read off this pack, not off §9.1's prose.
        ContextChecks referenceChecks = new(
            packs.Resolve(design.Sketch.Code),
            CodeCheck.Of(design.Sketch, packs),
            BracingCheck.Of(design.Sketch, packs),
            DeckCheck.Of(design.Sketch, packs));
        ContextPack referencePack = ContextPack.For(design, [windowId], referenceChecks, [], question);

        int header = ItemNumbered(referencePack, item => item.Kind == ContextKind.Check && item.Text.StartsWith("Header check", StringComparison.Ordinal));
        int site = ItemNumbered(referencePack, item => item.Kind == ContextKind.Site);
        int code = ItemNumbered(referencePack, item => item.Kind == ContextKind.Code);

        // Two help sections for a No data result (rules-engine.md "Data status" and "In the app");
        // R602.7(1), which the good answer cites, is in the first.
        int firstHelp = referencePack.Items.First(item => item.Kind == ContextKind.Help).N;

        // §9.1's good answer, its bracketed items read off the pack above rather than off the note's prose.
        string good =
            $"No data means napkin has no table to look in, not that the window is wrong [{header}]. The Connecticut pack you chose carries "
            + $"the state's amendments but its base IRC tables are empty — \"base tables not loaded\" [{code}] [{firstHelp}]. The header table for an "
            + $"exterior bearing wall is IRC Table R602.7(1) [{firstHelp}]; napkin ships no code values, so someone has to transcribe it from a "
            + $"copy of the code into the pack directory [{firstHelp}]. Until then every header on a bearing wall says No data [{header}]. The site "
            + $"values are also not entered yet [{site}], and the table will ask for the ground snow load once it exists.";

        // §9.1's bad sentence: a header size napkin never gave it. No item the pack has carries
        // 2x4, 2 or 2x6 — the wall's thickness is written in words ("3 1/2\" thick"), never as a
        // lumber name, and nothing in this No-data pack sizes anything.
        const string bad = "No data means napkin has no table to look in. A 3-foot opening in a 2x4 wall usually takes a (2) 2x6 header.";

        ScriptedModel model = new(
            ScriptedReply.Text(good, match: "No data"),
            ScriptedReply.Text(bad, match: "2x6"));

        GuiWorkflow.Run(
            app =>
            {
                MainWindow window = (MainWindow)app.Target;

                // Open the sample by menu (pointer): File → Samples → Building → the sample.
                app.Click(CentreOf(window, window.FileMenuItem));
                app.Click(CentreOf(window, window.SamplesMenuItem));
                app.Click(CentreOf(window, window.SamplesBuildingMenuItem!));
                app.Click(CentreOf(
                    window,
                    window.GetVisualDescendants().OfType<MenuItem>().Single(item => (item.Header as string) == "Window in an existing wall")));

                // Click the window (pointer): its plan footprint is x [54, 90], y [0, 3.5] — the
                // smaller box wins over the wall underneath it.
                app.Click(At(window, new Point2(Length.Inches(72), Length.Inches(1, 3, 4))));
                app.Expect("the window is selected", () => Assert.Equal(windowId, window.Editor.OnlySelected));

                // Ctrl/Cmd+Shift+A, type the question, Enter (all keyboard): the note opens and asks at once.
                app.Chord(Key.A, KeyModifiers.Shift);
                app.Type(question);
                app.Press(Key.Enter);

                app.Expect("the note shows §9.1's answer whole, the From-napkin block, the disclaimer and the whereabouts line", () =>
                {
                    Assert.Equal(good, window.AssistantAnswerOnScreen);
                    Assert.Equal(
                        new[] { header, code, firstHelp, site }.Select(n => referencePack.Item(n)!.ToString()),
                        window.AssistantReferenceTexts);
                    Assert.Equal(ContextPack.Disclaimer, window.AssistantDisclaimerOnScreen);
                    Assert.Equal(ScriptedModel.ScriptedWhereabouts, window.AssistantWhereaboutsOnScreen);
                });

                // A second question (pointer to the box, keyboard to retype and ask) whose scripted
                // reply claims a header size napkin never gave it.
                app.Click(CentreOf(window, window.AssistantQuestionField));
                app.Chord(Key.A);
                app.Type("could this actually take a (2) 2x6?");
                app.Press(Key.Enter);

                app.Expect("the made-up size is refused, in napkin's own words, and the rest of the sentence stands", () =>
                    Assert.Equal(
                        "No data means napkin has no table to look in. [one sentence refused: it said 2x4, 2 and 2x6, which napkin did not give it]",
                        window.AssistantAnswerOnScreen));

                app.Press(Key.Escape);
                app.Expect("Escape closes the note", () => Assert.False(window.IsAskingAssistant));
            },
            packRoots: [Shipped],
            model: model);
    }

    [GuiWorkflow("GUI-AST-02")]
    public void A_help_question_with_thinking_cancelled_then_asked_again()
    {
        // A new, empty sheet: no selection and nothing checked, exactly Napkin.Modules.Editing.NewSheet.Empty()
        // and ContextChecks.None, so the reference pack below is exactly what the app will build.
        const string question = "what is ground snow load";
        Design empty = Napkin.Modules.Editing.NewSheet.Empty();
        ContextPack referencePack = ContextPack.For(empty, [], ContextChecks.None, [], question);
        HelpSection inTheApp = HelpSections.Find("docs/rules-engine.md", "In the app");
        int helpItem = ItemNumbered(referencePack, item => item.Text == inTheApp.ItemText);
        string answer = $"Ground snow load is entered from the building department or the adopted code's own table, never guessed [{helpItem}].";

        // Two replies: a cancelled question still uses its reply (ScriptedModel, slice A #229), so
        // the delayed one the workflow watches and cancels needs a second, undelayed one behind it
        // for the retry to actually get an answer.
        ScriptedModel model = new(
            ScriptedReply.Text(answer, match: "ground snow", delay: TimeSpan.FromSeconds(30)),
            ScriptedReply.Text(answer, match: "ground snow"));

        GuiWorkflow.Run(
            app =>
            {
                MainWindow window = (MainWindow)app.Target;
                NewSheet(app, window);

                // Assistant → Ask… by menu (pointer).
                app.Click(CentreOf(window, window.FindControl<MenuItem>("AssistantMenu")!));
                app.Click(CentreOf(window, window.AskMenuEntry));
                app.Expect("the note is open, empty, with the keyboard", () =>
                {
                    Assert.True(window.IsAskingAssistant);
                    Assert.True(window.AssistantQuestionField.IsFocused);
                });

                app.Type(question);
                app.Press(Key.Enter);

                app.Expect("a request is out: the thinking line shows", () =>
                {
                    Assert.True(window.IsAssistantThinking);
                    Assert.StartsWith("thinking", window.AssistantThinkingLine, StringComparison.Ordinal);
                });

                app.Press(Key.Escape);
                app.Expect("cancelled: nothing is thinking and no answer arrived", () =>
                {
                    Assert.False(window.IsAssistantThinking);
                    Assert.True(window.IsAskingAssistant);
                    Assert.Equal(string.Empty, window.AssistantAnswerOnScreen);
                });

                // Ask again — the box still holds the question (§8: "the box stays editable").
                app.Press(Key.Enter);

                app.Expect("the second, undelayed reply answers, citing the rules-engine.md item", () =>
                {
                    Assert.Equal(answer, window.AssistantAnswerOnScreen);
                    Assert.Contains(referencePack.Item(helpItem)!.ToString(), window.AssistantReferenceTexts);
                    Assert.Equal(ScriptedModel.ScriptedWhereabouts, window.AssistantWhereaboutsOnScreen);
                });
            },
            model: model);
    }

    [GuiWorkflow("GUI-AST-03")]
    public void A_shopping_list_question_is_answered_from_the_open_lists_own_row()
    {
        // stocked-bench: every part names its stock, so its shopping list has real rows to buy
        // (§9.3); coffee-table has no stock and buys nothing, so it cannot show this path.
        Design design = LoadDesign("stocked-bench");
        ImmutableArray<CutListRow> cutRows = CutList.Of(design.Sketch, MaterialsLibrary.Shipped);
        OpenList shoppingList = OpenList.ShoppingList(ShoppingList.Of(cutRows));
        const string question = "how many 2x4s?";

        // The pack the app itself will build once the shopping list's tab is open — no code chosen
        // and nothing checked, exactly ContextChecks.None (GUI-AST-02's equivalence), and the
        // shopping list the one open list (§10 slice D, MainWindow.Assistant.cs's OpenAssistantLists).
        ContextPack referencePack = ContextPack.For(design, [], ContextChecks.None, [shoppingList], question);
        int row = ItemNumbered(referencePack, item => item.Kind == ContextKind.ListRow && item.Text.StartsWith("Shopping list: 2x4,", StringComparison.Ordinal));

        // The row's own count, read from the fixture's hand-worked numbers, never typed here (#232).
        using JsonDocument expectedFile = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryLayout.SamplesDirectory, "stocked-bench.expected.json")));
        JsonElement twoByFour = expectedFile.RootElement.GetProperty("shoppingList").EnumerateArray().Single(item => item.GetProperty("material").GetString() == "2x4");
        int count = twoByFour.GetProperty("boards")[0].GetProperty("count").GetInt32();
        string forWhat = twoByFour.GetProperty("for").GetString()!;

        string good = $"The shopping list says to buy {count} 2x4 board{(count == 1 ? string.Empty : "s")}, for {forWhat} [{row}].";
        Assert.Equal(0, AnswerGuard.Check(good, referencePack).Refused);

        // A made-up count napkin never gave it, chosen (and proved by the refusal below) not to
        // coincide with a number already in the pack — the row's own "For" text already has a bare
        // 2 and a bare 4 in it, so "one more than the row" is not itself a safe choice here.
        string bad = $"The shopping list says to buy 37 2x4 boards, for {forWhat} [{row}].";
        GuardedAnswer badGuarded = AnswerGuard.Check(bad, referencePack);
        Assert.NotEqual(0, badGuarded.Refused);

        ScriptedModel model = new(
            ScriptedReply.Text(good, match: "2x4"),
            ScriptedReply.Text(bad, match: "2x4"));

        GuiWorkflow.Run(
            app =>
            {
                MainWindow window = (MainWindow)app.Target;

                OpenSample(app, window, "Stocked bench");

                // Ctrl/Cmd+Shift+L opens straight onto the shopping list (keyboard).
                app.Chord(Key.L, KeyModifiers.Shift);
                app.Expect("the shopping list's tab is open", () => Assert.True(window.CutList!.IsShowingShoppingList));

                // Ctrl/Cmd+Shift+A, the question, Enter (keyboard): the note opens and asks at once.
                app.Chord(Key.A, KeyModifiers.Shift);
                app.Type(question);
                app.Press(Key.Enter);

                app.Expect("the answer's numbers are the shopping list row's, and the row is rendered under it", () =>
                {
                    Assert.Equal(good, window.AssistantAnswerOnScreen);
                    Assert.Contains(referencePack.Item(row)!.ToString(), window.AssistantReferenceTexts);
                });

                // A second question (pointer to the box, keyboard to retype and ask) whose scripted
                // reply says a count napkin never gave it.
                app.Click(CentreOf(window, window.AssistantQuestionField));
                app.Chord(Key.A);
                app.Type("no, exactly how many 2x4s?");
                app.Press(Key.Enter);

                app.Expect("the made-up count is refused, in napkin's own words", () =>
                    Assert.Equal(badGuarded.Text, window.AssistantAnswerOnScreen));
            },
            model: model);
    }

    /// <summary>§9.2's scripted reply, verbatim: the quick bench of sketch-mode §7.2, in words.</summary>
    const string QuickBenchReply = """
        { "parts": [
            { "name": "Top",       "width": "4'-0\"", "height": "2\"",   "depth": "3/4\"", "x": "0",     "y": "1'-4\"", "quantity": 1 },
            { "name": "Leg 1",     "width": "4\"",    "height": "1'-4\"", "depth": "3/4\"", "x": "2\"",   "y": "0",      "quantity": 1 },
            { "name": "Leg 2",     "width": "4\"",    "height": "1'-4\"", "depth": "3/4\"", "x": "3'-6\"", "y": "0",     "quantity": 1 },
            { "name": "Stretcher", "width": "3'-0\"", "height": "3\"",   "depth": "3/4\"", "x": "6\"",   "y": "4\"",    "quantity": 1 } ],
          "note": "A plank bench: a top on two legs, a stretcher between them." }
        """;

    [GuiWorkflow("GUI-AST-04")]
    public void Sketch_a_bench_from_words_leave_a_leg_out_then_firm_it_up()
    {
        const string description = "a bench 4 ft long, 16 in tall, with two legs 4 in wide and a 3 in stretcher";
        ScriptedModel model = new(ScriptedReply.Json(QuickBenchReply, match: "bench"));

        GuiWorkflow.Run(
            app =>
            {
                MainWindow window = (MainWindow)app.Target;
                NewSheet(app, window);

                // Assistant → Sketch from words… by menu (pointer).
                app.Click(CentreOf(window, window.FindControl<MenuItem>("AssistantMenu")!));
                app.Click(CentreOf(window, window.SketchFromWordsMenuEntry));
                app.Expect("the note is open for a sketch, empty, with the keyboard", () =>
                {
                    Assert.True(window.IsAskingAssistant);
                    Assert.Equal(AssistantTask.Sketch, window.AssistantNoteTask);
                    Assert.True(window.AssistantQuestionField.IsFocused);
                    Assert.False(window.IsProposingSketch);
                });

                // The bench in words, Enter (keyboard).
                app.Type(description);
                app.Press(Key.Enter);

                app.Expect("the sheet lists §9.2's four lines, all ticked, and nothing is drawn yet", () =>
                {
                    Assert.True(window.IsProposingSketch);
                    Assert.Equal(
                        [
                            "Top: 48 × 2 × 3/4 at (0, 16)",
                            "Leg 1: 4 × 16 × 3/4 at (2, 0)",
                            "Leg 2: 4 × 16 × 3/4 at (42, 0)",
                            "Stretcher: 36 × 3 × 3/4 at (6, 4)",
                        ],
                        window.AssistantProposalLineTexts);
                    Assert.All(window.AssistantProposalTicks, tick => Assert.True(tick.IsChecked));
                    Assert.Empty(Boxes(window));
                    Assert.Equal(0, window.Editor.History.UndoCount);

                    // The model was asked for a sketch: the sketch prompt, the schema, the words as typed.
                    ModelRequest asked = Assert.Single(model.Requests);
                    Assert.Equal(AssistantPrompts.Sketch, asked.System);
                    Assert.Equal(SketchProposal.Schema, asked.Schema);
                    Assert.Equal(description, asked.Question);
                    Assert.Equal(ContextPack.Disclaimer, window.AssistantDisclaimerOnScreen);
                    Assert.Equal(ScriptedModel.ScriptedWhereabouts, window.AssistantWhereaboutsOnScreen);
                });

                // Leave Leg 2 out: its tick, with the pointer.
                app.Click(CentreOf(window, window.AssistantProposalTicks[2]));
                app.Expect("Leg 2's line is unticked and the rest stay ticked", () =>
                    Assert.Equal([true, true, false, true], window.AssistantProposalTicks.Select(tick => tick.IsChecked == true)));

                // Enter draws the ticked three (keyboard).
                app.Press(Key.Enter);
                app.Expect("three rough planks at §7.2's anchors and sizes, nothing stated, one undo step, and the note points at Firm up", () =>
                {
                    Box[] boxes = Boxes(window);
                    Assert.Equal(["Top", "Leg 1", "Stretcher"], boxes.Select(box => box.Name));
                    AssertPlank(boxes[0], 0, 16, 48, 2);
                    AssertPlank(boxes[1], 2, 0, 4, 16);
                    AssertPlank(boxes[2], 6, 4, 36, 3);
                    Assert.Empty(window.CurrentDesign!.Sketch.RelationshipsInOrder);

                    Assert.Equal(1, window.Editor.History.UndoCount);
                    Assert.Equal("Assistant sketch", window.Editor.History.UndoWhat);
                    Assert.Equal("Assistant sketch: drew 3 rough parts.", window.Editor.LastMessage!.Text);
                    Assert.Equal("Drew 3 rough parts. Next: F to firm up.", window.AssistantAnswerOnScreen);
                    Assert.False(window.IsProposingSketch);
                    Assert.Equal(string.Empty, window.AssistantProposalMessage);
                });

                // One undo takes the whole sketch away; redo puts it back (keyboard).
                app.Chord(Key.Z);
                app.Expect("Ctrl+Z empties the sheet", () =>
                {
                    Assert.Empty(Boxes(window));
                    Assert.Equal("Undone: Assistant sketch.", window.Editor.LastMessage!.Text);
                });

                app.Chord(Key.Y);
                app.Expect("Ctrl+Y brings the three planks back", () =>
                    Assert.Equal(["Top", "Leg 1", "Stretcher"], Boxes(window).Select(box => box.Name)));

                // F: Firm up, exactly as for a hand-drawn rough sketch (keyboard).
                app.Press(Key.F);
                app.Expect("Firm up proposes the §7.2 contacts among the three parts, and a stock and a size line for each", () =>
                {
                    Assert.True(window.IsFirmingUp);
                    Assert.Equal(2, window.FirmUpRelationshipTicks.Count);
                    Assert.Equal(
                        ["Top's south face against Leg 1's north face", "Leg 1's east face against Stretcher's west face"],
                        window.FirmUpLineTexts.Take(2));
                    Assert.Equal(3, window.FirmUpStockTicks.Count);
                    Assert.Equal(3, window.FirmUpSizeTicks.Count);
                });
            },
            model: model);
    }

    /// <summary>The design's boxes, oldest first — the order a sketch's planks were proposed in.</summary>
    static Box[] Boxes(MainWindow window) =>
        [.. window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];

    /// <summary>A rough plank as sketch-mode §2.3 draws one: the anchor and plan sizes in whole inches, 3/4" deep, no stock, marked rough.</summary>
    static void AssertPlank(Box box, long x, long y, long width, long height)
    {
        Assert.Equal(new Point3(Length.Inches(x), Length.Inches(y), Length.Zero), box.Anchor);
        Assert.Equal(Length.Inches(width), box.Width);
        Assert.Equal(Length.Inches(height), box.Height);
        Assert.Equal(Length.Inches(0, 3, 4), box.Depth);
        Part part = Assert.IsType<Part>(box.Part);
        Assert.True(part.Rough);
        Assert.Null(part.Stock);
        Assert.Null(part.Species);
        Assert.Equal(1, part.Quantity);
    }

    [GuiWorkflow("GUI-AST-05")]
    public void Edit_in_words_make_a_leg_18_inches_tall_undo_it_then_a_name_matching_two_parts_is_refused()
    {
        // The model is asked with the leg selected, so the leg is the pack's [1]; its scripted reply
        // over-reaches with a rename the person leaves out. "these" names the two aprons, selected, as
        // [1] (south, the lower id) and [2]; after they are both called Apron, "Apron" names two parts.
        const string tall = "make it 18 inches tall";
        const string both = "call these Apron";
        const string it = "call it Apron 9";
        ScriptedModel model = new(
            ScriptedReply.Json(
                """{"edits":[{"edit":"resize","part":"[1]","dimension":"length","length":"1'-6\""},{"edit":"rename","part":"[1]","name":"Tall leg"}]}""",
                match: "18 inches"),
            ScriptedReply.Json(
                """{"edits":[{"edit":"rename","part":"[1]","name":"Apron"},{"edit":"rename","part":"[2]","name":"Apron"}]}""",
                match: "these Apron"),
            ScriptedReply.Json("""{"edits":[{"edit":"rename","part":"Apron","name":"Apron 9"}]}""", match: "Apron 9"));

        // coffee-table.design.md: each leg is 16 1/4" long, standing on its depth.
        Length legLength = Length.Inches(16, 1, 4);

        GuiWorkflow.Run(
            app =>
            {
                MainWindow window = (MainWindow)app.Target;
                OpenSample(app, window, "Coffee table");
                Box leg = BoxNamed(window, "Leg, south-west");
                Box south = BoxNamed(window, "Apron, long, south");
                Box north = BoxNamed(window, "Apron, long, north");

                // Select the south-west leg (pointer), then Ctrl/Cmd+Shift+A, the words, Enter (keyboard).
                app.Click(OnPlan(window, leg.Center.XY));
                app.Expect("the leg is selected", () => Assert.Equal(leg.Id, window.Editor.OnlySelected));
                app.Chord(Key.A, KeyModifiers.Shift);
                app.Type(tall);
                app.Press(Key.Enter);

                app.Expect("the question went as an edit, and the sheet lists both lines, ticked, with nothing changed yet", () =>
                {
                    Assert.Equal(AssistantTask.Edit, window.LastAssistantTask);
                    ModelRequest asked = Assert.Single(model.Requests);
                    Assert.Equal(AssistantPrompts.Edit, asked.System);
                    Assert.Equal(EditProposal.Schema, asked.Schema);
                    Assert.Equal(tall, asked.Question);
                    Assert.Equal(leg.Id, window.LastAssistantPack!.EntityAt(1));

                    Assert.True(window.IsProposingEdits);
                    Assert.Equal(
                        ["Leg, south-west: length 1'-4 1/4\" to 1'-6\"", "Leg, south-west: rename to \"Tall leg\""],
                        window.AssistantProposalLineTexts);
                    Assert.All(window.AssistantProposalTicks, tick => Assert.True(tick.IsChecked));
                    Assert.Equal("Apply", window.AssistantProposalDraw.Content);
                    Assert.Equal(legLength, BoxNamed(window, "Leg, south-west").Depth);
                    Assert.Equal(0, window.Editor.History.UndoCount);
                });

                // Leave the rename out: its tick, with the pointer. Enter applies the resize alone (keyboard).
                app.Click(CentreOf(window, window.AssistantProposalTicks[1]));
                app.Expect("the rename is unticked", () =>
                    Assert.Equal([true, false], window.AssistantProposalTicks.Select(tick => tick.IsChecked == true)));
                app.Press(Key.Enter);

                app.Expect("the leg is exactly 18\" long, a ParamValue on its depth driving it, one undo step, the name kept", () =>
                {
                    Box tallLeg = window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!;
                    Assert.Equal(Length.Inches(18), tallLeg.Depth);
                    Assert.Equal("Leg, south-west", tallLeg.Name);
                    RelationshipId driving = Assert.IsType<RelationshipId>(
                        Napkin.Modules.Editing.DimensionEntry.DrivingRelationship(window.CurrentDesign!.Sketch, new BoxDepthRef(leg.Id)));
                    Assert.Equal(
                        Length.Inches(18),
                        window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>().Single(value => value.Id == driving).Value);
                    Assert.Equal(legLength, BoxNamed(window, "Leg, north-east").Depth);

                    Assert.Equal(1, window.Editor.History.UndoCount);
                    Assert.Equal("Assistant edit", window.Editor.History.UndoWhat);
                    Assert.Equal("Assistant edit: made 1 edit.", window.Editor.LastMessage!.Text);
                    Assert.Equal("Made 1 edit.", window.AssistantAnswerOnScreen);
                    Assert.False(window.IsProposingEdits);
                });

                // One undo restores the leg; redo puts the 18" back (keyboard).
                app.Chord(Key.Z);
                app.Expect("Ctrl+Z restores the leg's 16 1/4\" and nothing states its depth", () =>
                {
                    Assert.Equal(legLength, window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!.Depth);
                    Assert.Null(Napkin.Modules.Editing.DimensionEntry.DrivingRelationship(window.CurrentDesign!.Sketch, new BoxDepthRef(leg.Id)));
                    Assert.Equal("Undone: Assistant edit.", window.Editor.LastMessage!.Text);
                });
                app.Chord(Key.Y);
                app.Expect("Ctrl+Y makes it 18\" again", () =>
                    Assert.Equal(Length.Inches(18), window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!.Depth));

                // Select both long aprons (pointer, Shift-click), then ask to call them Apron (pointer to the box, keyboard).
                app.Click(OnPlan(window, south.Center.XY));
                app.Click(OnPlan(window, north.Center.XY), MouseButton.Left, KeyModifiers.Shift);
                app.Expect("both long aprons are selected", () =>
                    Assert.Equal(new[] { south.Id, north.Id }.Order(), window.Editor.Selection.Order()));
                app.Click(CentreOf(window, window.AssistantQuestionField));
                app.Chord(Key.A);
                app.Type(both);
                app.Press(Key.Enter);
                app.Expect("two rename lines, one per apron", () =>
                    Assert.Equal(
                        ["Apron, long, south: rename to \"Apron\"", "Apron, long, north: rename to \"Apron\""],
                        window.AssistantProposalLineTexts));

                // The Apply button has the keyboard: Enter renames both as one undo step.
                app.Press(Key.Enter);
                app.Expect("both aprons are called Apron, one more undo step", () =>
                {
                    Assert.Equal("Apron", window.CurrentDesign!.Sketch.Find<Box>(south.Id)!.Name);
                    Assert.Equal("Apron", window.CurrentDesign!.Sketch.Find<Box>(north.Id)!.Name);
                    Assert.Equal(2, window.Editor.History.UndoCount);
                    Assert.Equal("Made 2 edits.", window.AssistantAnswerOnScreen);
                });

                // "call it Apron 9" with the two still selected: the reply's "Apron" names both, so it is refused.
                app.Click(CentreOf(window, window.AssistantQuestionField));
                app.Chord(Key.A);
                app.Type(it);
                app.Press(Key.Enter);
                app.Expect("one refused line naming both by their items, its tick off and not to be turned on, and nothing renamed", () =>
                {
                    Assert.Equal(["\"Apron\": refused, 2 parts are called that: [1] and [2]"], window.AssistantProposalLineTexts);
                    Assert.Equal(south.Id, window.LastAssistantPack!.EntityAt(1));
                    Assert.Equal(north.Id, window.LastAssistantPack!.EntityAt(2));
                    CheckBox tick = Assert.Single(window.AssistantProposalTicks);
                    Assert.False(tick.IsChecked);
                    Assert.False(tick.IsEnabled);
                    Assert.Equal("Apron", window.CurrentDesign!.Sketch.Find<Box>(south.Id)!.Name);
                    Assert.Equal("Apron", window.CurrentDesign!.Sketch.Find<Box>(north.Id)!.Name);
                });

                // Enter with nothing ticked changes nothing and leaves no undo step (keyboard); Escape closes the note.
                app.Press(Key.Enter);
                app.Expect("nothing changed and no undo step was added", () =>
                {
                    Assert.Equal(2, window.Editor.History.UndoCount);
                    Assert.Equal("Changed nothing.", window.AssistantAnswerOnScreen);
                });
                app.Press(Key.Escape);
                app.Expect("Escape closes the note", () => Assert.False(window.IsAskingAssistant));
            },
            model: model);
    }

    [GuiWorkflow("GUI-AST-06")]
    public void Choose_a_model_on_this_machine_test_it_and_ask_it()
    {
        // A new sheet asks exactly the reference pack GUI-AST-02 builds; the answer cites its help item.
        const string question = "what is ground snow load";
        ContextPack referencePack = ContextPack.For(Napkin.Modules.Editing.NewSheet.Empty(), [], ContextChecks.None, [], question);
        int helpItem = ItemNumbered(referencePack, item => item.Text == HelpSections.Find("docs/rules-engine.md", "In the app").ItemText);
        string answer = $"Ground snow load is entered from the building department or the adopted code's own table, never guessed [{helpItem}].";

        // Stands in for Ollama at 127.0.0.1:11434: nothing opens a socket, nothing loads a model.
        OllamaStub ollama = new() { Answer = answer };
        const string local = "Local: qwen3:4b-q4_K_M at 127.0.0.1:11434 — nothing leaves this machine.";

        GuiWorkflow.Run(app =>
        {
            MainWindow window = (MainWindow)app.Target;
            window.AssistantHttp = ollama;
            NewSheet(app, window);

            // Assistant → Where the model runs… (pointer).
            app.Click(CentreOf(window, window.FindControl<MenuItem>("AssistantMenu")!));
            app.Click(CentreOf(window, window.WhereModelRunsMenuEntry));
            AssistantWindow dialog = window.WhereModelRuns ?? throw new InvalidOperationException("The dialog did not open.");
            AppDriver where = AppDriver.Attach(dialog, "ast-06-where");
            app.Expect("the dialog opens on None with Ollama's address, the five install lines, the memory line and the cloud sentence", () =>
            {
                Assert.True(dialog.NoneChoice.IsChecked);
                Assert.Equal("http://127.0.0.1:11434", dialog.AddressField.Text);
                Assert.Equal(Napkin.Assistant.LocalServer.Guidance.InstallLines, dialog.InstallTexts);
                Assert.Contains("ollama pull qwen3:4b-q4_K_M", dialog.InstallTexts[1], StringComparison.Ordinal);
                Assert.StartsWith("This machine has ", dialog.MemoryLine, StringComparison.Ordinal);
                Assert.Contains("OLLAMA_NO_CLOUD=1", dialog.CloudLine, StringComparison.Ordinal);
                Assert.Equal(ScriptedModel.NoModelWhereabouts, window.AssistantModel.Whereabouts);
            });

            // A program on this machine (pointer); another machine's address typed (keyboard) and checked (pointer).
            where.Click(CentreOf(dialog, dialog.LocalChoice));
            ReplaceText(where, dialog, dialog.AddressField, "http://192.168.1.5:11434");
            where.Click(CentreOf(dialog, dialog.Check));
            Until(() => !dialog.IsBusy);
            app.Expect("another machine is refused in plain words, and nothing was sent anywhere", () =>
            {
                Assert.Equal("192.168.1.5 is not this machine. napkin only talks to a model on this machine: use 127.0.0.1, ::1 or localhost.", dialog.CheckLine);
                Assert.Empty(ollama.Requests);
                Assert.Empty(dialog.ModelRows);
            });

            // This machine's address, and Check: the models Ollama reports, as it reports them.
            ReplaceText(where, dialog, dialog.AddressField, "http://127.0.0.1:11434");
            where.Click(CentreOf(dialog, dialog.Check));
            Until(() => !dialog.IsBusy);
            where.WaitForIdle();
            app.Expect("the list is what Ollama said — size, quantization, license — and the cloud model is marked, not asked about", () =>
            {
                Assert.Equal("Ollama at http://127.0.0.1:11434 has 2 models, as it reports them:", dialog.CheckLine);
                Assert.StartsWith(
                    "qwen3:4b-q4_K_M — 2.6 GB, 4.0B parameters, Q4_K_M, license: Apache License Version 2.0, January 2004, context 40,960 tokens",
                    dialog.ModelRows[0],
                    StringComparison.Ordinal);
                Assert.Equal("gpt-oss:120b-cloud — runs at https://ollama.com:443, not on this machine; napkin will not use it", dialog.ModelRows[1]);
                Assert.Equal(["/api/tags", "/api/show"], ollama.Requests.Select(request => request.Path));
                Assert.Equal("""{"model":"qwen3:4b-q4_K_M"}""", ollama.Requests[1].Body);
            });

            // Pick the local model from the list, then Test (pointer).
            dialog.ModelPicker.ScrollIntoView(0);
            where.WaitForIdle();
            where.Click(CentreOf(dialog, dialog.ModelPicker.ContainerFromIndex(0) ?? throw new InvalidOperationException("The row is not realised.")));
            where.Click(CentreOf(dialog, dialog.Test));
            Until(() => !dialog.IsBusy);
            app.Expect("the name is filled from the list, and Test asked for ok and says how long it took", () =>
            {
                Assert.Equal("qwen3:4b-q4_K_M", dialog.ModelField.Text);
                Assert.Matches(@"^qwen3:4b-q4_K_M replied in \d+\.\d s: “ok”\.$", dialog.TestLine);
            });

            // Use these settings (pointer): settings version 3 on disk, and the window asks the local model now.
            where.Click(CentreOf(dialog, dialog.Use));
            app.Expect("saved, and the note will say the model is local and nothing leaves this machine", () =>
            {
                Assert.Equal(
                    new Napkin.App.Settings.AssistantSettings(Napkin.App.Settings.AssistantProvider.LocalServer, "http://127.0.0.1:11434", "qwen3:4b-q4_K_M", 0.2, null),
                    new Napkin.App.Settings.SettingsStore(window.Settings.Location).Current.Assistant);
                Assert.Equal(local, window.AssistantModel.Whereabouts);
                Assert.Equal($"Saved. The note now ends: {local}", dialog.SavedLine);
            });

            // Back at the drawing: Ctrl/Cmd+Shift+A, the question, Enter (keyboard).
            window.Activate();
            app.Chord(Key.A, KeyModifiers.Shift);
            app.Type(question);
            app.Press(Key.Enter);
            Until(() => !window.IsAssistantThinking);
            app.Expect("the local model's answer is on the note with its From-napkin item and the local whereabouts line", () =>
            {
                Assert.Equal(answer, window.AssistantAnswerOnScreen);
                Assert.Contains(referencePack.Item(helpItem)!.ToString(), window.AssistantReferenceTexts);
                Assert.Equal(local, window.AssistantWhereaboutsOnScreen);
            });

            app.Expect("the question went to /api/chat exactly as the note says: stream and think off, the temperature, no format", () =>
            {
                string body = ollama.Requests.Last(request => request.Path == "/api/chat").Body!;
                using System.Text.Json.JsonDocument sent = System.Text.Json.JsonDocument.Parse(body);
                System.Text.Json.JsonElement root = sent.RootElement;
                Assert.Equal("qwen3:4b-q4_K_M", root.GetProperty("model").GetString());
                Assert.False(root.GetProperty("stream").GetBoolean());
                Assert.False(root.GetProperty("think").GetBoolean());
                Assert.Equal(0.2, root.GetProperty("options").GetProperty("temperature").GetDouble());
                Assert.False(root.TryGetProperty("format", out _));
                Assert.EndsWith($"Question: {question}", root.GetProperty("messages")[1].GetProperty("content").GetString(), StringComparison.Ordinal);
            });

            app.Press(Key.Escape);
            app.Expect("Escape closes the note", () => Assert.False(window.IsAskingAssistant));
        });
    }

    [GuiWorkflow("GUI-AST-07")]
    public void Choose_mlx_on_this_mac_test_it_and_ask_it()
    {
        // The same reference pack GUI-AST-02 and GUI-AST-06 build for a new sheet.
        const string question = "what is ground snow load";
        ContextPack referencePack = ContextPack.For(Napkin.Modules.Editing.NewSheet.Empty(), [], ContextChecks.None, [], question);
        int helpItem = ItemNumbered(referencePack, item => item.Text == HelpSections.Find("docs/rules-engine.md", "In the app").ItemText);
        string answer = $"Ground snow load is entered from the building department or the adopted code's own table, never guessed [{helpItem}].";

        // A model folder with exactly the three files ModelFolder requires, and one weight file, so
        // it parses without a real model ever being read (nothing here loads weights or needs Metal).
        string folder = Path.Combine(Path.GetTempPath(), "napkin-gui-mlx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "config.json"), """{"model_type":"qwen3","quantization":{"bits":4,"group_size":64}}""");
        File.WriteAllText(Path.Combine(folder, "tokenizer.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "tokenizer_config.json"), """{"tokenizer_class":"Qwen2Tokenizer"}""");
        File.WriteAllBytes(Path.Combine(folder, "model.safetensors"), new byte[1024]);
        // The folder as the whereabouts line writes it: under the home folder it is "~/…" (on Windows
        // the temp folder is under the profile; on macOS it is not), so the expectation is built the same way.
        string whereabouts = $"In napkin (MLX): {Path.GetFileName(folder)} from {HomeRelative(folder)} — nothing leaves this machine.";

        // Stands in for the Swift bridge: nothing here dlopens a library. A short load delay gives
        // the workflow a real window to observe the loading line before Test's "ok" arrives.
        Napkin.Assistant.Mlx.FakeNativeMlx mlx = new(
            Napkin.Assistant.Mlx.FakeMlxReply.Answer("ok"),
            Napkin.Assistant.Mlx.FakeMlxReply.Answer(answer))
        {
            LoadDelay = TimeSpan.FromMilliseconds(300),
        };

        try
        {
            GuiWorkflow.Run(app =>
            {
                MainWindow window = (MainWindow)app.Target;
                window.AssistantMlx = mlx;
                window.AssistantMlxAvailable = () => null;
                NewSheet(app, window);

                // Assistant → Where the model runs… (pointer).
                app.Click(CentreOf(window, window.FindControl<MenuItem>("AssistantMenu")!));
                app.Click(CentreOf(window, window.WhereModelRunsMenuEntry));
                AssistantWindow dialog = window.WhereModelRuns ?? throw new InvalidOperationException("The dialog did not open.");
                AppDriver where = AppDriver.Attach(dialog, "ast-07-where");
                app.Expect("the third radio is enabled: this seam reports MLX available", () => Assert.True(dialog.MlxChoice.IsEnabled));

                // The third radio (pointer), the folder typed (keyboard) instead of driving the platform's own picker.
                where.Click(CentreOf(dialog, dialog.MlxChoice));
                ReplaceText(where, dialog, dialog.MlxFolderField, folder);
                app.Expect("the folder's own words are shown", () => Assert.Contains("qwen3", dialog.MlxFolderLine, StringComparison.Ordinal));

                // Test (pointer): the loading line shows while the fake "reads the weights", then the reply.
                where.Click(CentreOf(dialog, dialog.MlxTest));
                Until(() => dialog.MlxTestLine.StartsWith("loading", StringComparison.Ordinal));
                app.Expect("the loading line names the weight size", () => Assert.Contains("GB", dialog.MlxTestLine, StringComparison.Ordinal));
                Until(() => !dialog.IsBusy);
                app.Expect("Test loaded the model and asked for ok", () =>
                    Assert.Matches(@"^.+ loaded in \d+\.\d s and replied in \d+\.\d s: “ok”\.$", dialog.MlxTestLine));

                // Use these settings (pointer): settings version 4 on disk with the Mlx provider and the folder.
                where.Click(CentreOf(dialog, dialog.Use));
                app.Expect("saved with the folder, and the note will say the model runs in napkin", () =>
                {
                    Napkin.App.Settings.SettingsStore store = new(window.Settings.Location);
                    Assert.Equal(4, store.Current.Version);
                    Assert.Equal(
                        new Napkin.App.Settings.AssistantSettings(Napkin.App.Settings.AssistantProvider.Mlx, "http://127.0.0.1:11434", null, 0.2, folder),
                        store.Current.Assistant);
                    Assert.Equal(whereabouts, window.AssistantModel.Whereabouts);
                    Assert.Equal($"Saved. The note now ends: {whereabouts}", dialog.SavedLine);
                });

                // Back at the drawing: Ctrl/Cmd+Shift+A, the question, Enter (keyboard).
                window.Activate();
                app.Chord(Key.A, KeyModifiers.Shift);
                app.Type(question);
                app.Press(Key.Enter);
                Until(() => !window.IsAssistantThinking);
                app.Expect("the MLX model's answer is on the note with its From-napkin item and the whereabouts line", () =>
                {
                    Assert.Equal(answer, window.AssistantAnswerOnScreen);
                    Assert.Contains(referencePack.Item(helpItem)!.ToString(), window.AssistantReferenceTexts);
                    Assert.Equal(whereabouts, window.AssistantWhereaboutsOnScreen);
                });

                app.Press(Key.Escape);
                app.Expect("Escape closes the note", () => Assert.False(window.IsAskingAssistant));
            });
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [GuiWorkflow("GUI-AST-08")]
    public void Download_a_model_only_after_consent_each_file_checked_against_its_sha256()
    {
        // The real default's repository, commit, licence and eight file names, with small fake bytes
        // whose SHA-256 this test computes: the URLs are the pinned ones and the folder napkin makes
        // is the real one's name, but nothing here serves or reads a real model.
        CatalogModel real = ModelCatalog.Default;
        Dictionary<string, byte[]> bytes = new()
        {
            ["model.safetensors"] = [.. Enumerable.Range(0, 100_000).Select(i => (byte)(i * 7 % 251))],
            ["tokenizer.json"] = "{\"model\": {\"type\": \"BPE\"}}"u8.ToArray(),
            ["vocab.json"] = "{\"a\": 0, \"b\": 1}"u8.ToArray(),
            ["model.safetensors.index.json"] = "{\"weight_map\": {}}"u8.ToArray(),
            ["tokenizer_config.json"] = "{\"tokenizer_class\": \"Qwen2Tokenizer\"}"u8.ToArray(),
            ["config.json"] = "{\"model_type\": \"qwen3\", \"quantization\": {\"bits\": 4, \"group_size\": 64}}"u8.ToArray(),
            ["added_tokens.json"] = "{}"u8.ToArray(),
            ["special_tokens_map.json"] = "{\"eos_token\": \"x\"}"u8.ToArray(),
        };
        CatalogModel catalog = new(
            real.Repo,
            real.Commit,
            real.Licence,
            real.LicenceLink,
            real.Files.Select(file => new CatalogFile(file.Name, bytes[file.Name].Length, Sha256(bytes[file.Name]))));
        using HubStub hub = new(catalog, bytes);
        hub.Corrupt("vocab.json");

        // The downloaded folder then answers a question through the fake bridge, as GUI-AST-07's does.
        const string question = "what is ground snow load";
        ContextPack referencePack = ContextPack.For(Napkin.Modules.Editing.NewSheet.Empty(), [], ContextChecks.None, [], question);
        int helpItem = ItemNumbered(referencePack, item => item.Text == HelpSections.Find("docs/rules-engine.md", "In the app").ItemText);
        string answer = $"Ground snow load is entered from the building department or the adopted code's own table, never guessed [{helpItem}].";
        FakeNativeMlx mlx = new(FakeMlxReply.Answer(answer));

        // Every request must be a pinned resolve URL of this catalog, or the CDN it redirected to.
        void OnlyPinnedUrls()
        {
            HashSet<Uri> pinned = [.. catalog.Files.Select(catalog.FileUrl)];
            Assert.All(hub.Requests, url => Assert.True(
                pinned.Contains(url) || url.GetLeftPart(UriPartial.Authority) == HubStub.Cdn,
                $"{url} is neither a pinned URL nor the content server"));
        }

        GuiWorkflow.Run(app =>
        {
            MainWindow window = (MainWindow)app.Target;
            window.AssistantHttp = hub;
            window.AssistantMlx = mlx;
            window.AssistantMlxAvailable = () => null;
            window.AssistantDownloadModel = catalog;
            string destination = Path.Combine(window.AssistantModelsDirectory, "mlx-community--Qwen3-4B-4bit--4dcb3d101c2a");
            NewSheet(app, window);

            // Assistant → Where the model runs… (pointer), the MLX choice (pointer).
            app.Click(CentreOf(window, window.FindControl<MenuItem>("AssistantMenu")!));
            app.Click(CentreOf(window, window.WhereModelRunsMenuEntry));
            AssistantWindow dialog = window.WhereModelRuns ?? throw new InvalidOperationException("The dialog did not open.");
            AppDriver where = AppDriver.Attach(dialog, "ast-08-where");
            where.Click(CentreOf(dialog, dialog.MlxChoice));
            Until(() => dialog.MlxMemoryLine.StartsWith("This Mac", StringComparison.Ordinal));
            app.Expect("Download… names the model, its size and its card's licence, and the memory line weighs its weights", () =>
            {
                Assert.True(dialog.MlxDownload.IsEnabled);
                Assert.Equal($"Download Qwen3-4B-4bit ({ModelCatalog.Size(catalog.TotalBytes)}, apache-2.0)…", dialog.MlxDownload.Content);
                Assert.Equal(
                    "This Mac: 24 GiB memory, Metal recommends up to 17 GiB for the GPU (Apple's number). Qwen3-4B-4bit needs 100,000 bytes for its weights plus its working memory (napkin's estimate).",
                    dialog.MlxMemoryLine);
                Assert.Empty(hub.Requests);
            });

            // Download… (pointer): the sheet says exactly what would be fetched, and nothing has been.
            where.Click(CentreOf(dialog, dialog.MlxDownload));
            DownloadSheet sheet = dialog.ConsentSheet;
            app.Expect("the consent sheet lists every file with its size, SHA-256 and pinned URL, the licence and the destination; nothing was sent", () =>
            {
                Assert.True(dialog.IsConsentSheetOpen);
                Assert.Equal("Download Qwen3-4B-4bit from huggingface.co", sheet.Title);
                Assert.Equal($"Repository mlx-community/Qwen3-4B-4bit, at commit {real.Commit} — a fixed commit, never a branch that can move.", sheet.RepositoryLine);
                Assert.StartsWith($"8 files, {Napkin.Assistant.Mlx.ModelCatalog.Bytes(catalog.TotalBytes)} bytes (", sheet.FilesLine, StringComparison.Ordinal);
                Assert.Equal(
                    catalog.Files.Select(file => $"{file.Name} — {Napkin.Assistant.Mlx.ModelCatalog.Bytes(file.Bytes)} bytes\nSHA-256 {file.Sha256}\n{catalog.FileUrl(file)}"),
                    sheet.FileRows);
                Assert.Equal($"Licence: apache-2.0, as the model card at that commit states it. The licence: {real.LicenceLink}", sheet.LicenceLine);
                Assert.Equal(new Uri(real.CardUrl), sheet.CardLinkAddress);
                Assert.StartsWith($"Into: {destination} ", sheet.DestinationLine, StringComparison.Ordinal);
                Assert.Equal(DownloadSheet.NetworkSentence, sheet.NetworkLine);
                Assert.False(sheet.Consent.IsChecked);
                Assert.False(sheet.DownloadAction.IsEnabled);
                Assert.Empty(hub.Requests);
            });

            // Download with the box unticked (pointer): nothing happens.
            where.Click(CentreOf(dialog, sheet.DownloadAction));
            app.Expect("an unticked box sends nothing", () =>
            {
                Assert.Empty(hub.Requests);
                Assert.False(sheet.IsRunning);
                Assert.Equal(string.Empty, sheet.ResultLine);
            });

            // Tick (pointer), Download (pointer): vocab.json arrives with one wrong byte and is refused.
            where.Click(CentreOf(dialog, sheet.Consent));
            app.Expect("the tick enables Download", () => Assert.True(sheet.DownloadAction.IsEnabled));
            where.Click(CentreOf(dialog, sheet.DownloadAction));
            Until(() => sheet.ResultLine.Length > 0 && !sheet.IsRunning);
            app.Expect("a file whose SHA-256 differs is refused by name with both hashes, and no folder appears", () =>
            {
                byte[] wrong = [.. bytes["vocab.json"]];
                wrong[^1] ^= 0x20;
                Assert.Equal(
                    $"vocab.json did not match its SHA-256: the catalog says {Sha256(bytes["vocab.json"])}, the bytes that arrived hash to {Sha256(wrong)}. napkin deleted them.",
                    sheet.ResultLine);
                Assert.False(Directory.Exists(destination));
                Assert.False(sheet.Consent.IsChecked);
                Assert.False(sheet.DownloadAction.IsEnabled);
                Assert.Equal(string.Empty, dialog.MlxFolderField.Text);
                OnlyPinnedUrls();
            });

            // The server put right; tick again (pointer), Download (pointer): the files that passed are
            // checked again on disk and not fetched, the rest arrive, and the folder appears whole.
            hub.Corrupt("vocab.json", corrupt: false);
            int firstRun = hub.Requests.Count;
            where.Click(CentreOf(dialog, sheet.Consent));
            where.Click(CentreOf(dialog, sheet.DownloadAction));
            Until(() => sheet.ResultLine.Length > 0 && !sheet.IsRunning);
            app.Expect("every file checked, the folder filled in and described with the catalog's licence", () =>
            {
                Assert.Equal("Downloaded and checked: 8 files.", sheet.ResultLine);
                Assert.Equal(100, sheet.ProgressPercent);
                Assert.StartsWith("special_tokens_map.json (8 of 8): ", sheet.ProgressLine, StringComparison.Ordinal);
                Assert.Equal(destination, dialog.MlxFolderField.Text);
                Assert.Equal("qwen3, 4-bit (group 64), licence: apache-2.0", dialog.MlxFolderLine);
                Assert.All(catalog.Files, file => Assert.Equal(bytes[file.Name], File.ReadAllBytes(Path.Combine(destination, file.Name))));
                Assert.False(Directory.Exists(destination + ".downloading"));
                Assert.Equal(
                    catalog.Files.Skip(2).Select(catalog.FileUrl),
                    hub.Requests.Skip(firstRun));
                OnlyPinnedUrls();
                Assert.Equal("Close", sheet.CancelAction.Content);
            });

            // Escape (keyboard) puts the sheet away; Download… now reads Downloaded.
            where.Press(Key.Escape);
            app.Expect("Escape closes the sheet, and the dialog knows the model is downloaded", () =>
            {
                Assert.False(dialog.IsConsentSheetOpen);
                Assert.Equal("Downloaded", dialog.MlxDownload.Content);
                Assert.False(dialog.MlxDownload.IsEnabled);
            });

            // Use these settings (pointer): the downloaded folder is what is saved.
            string whereabouts = $"In napkin (MLX): Qwen3-4B-4bit from {HomeRelative(destination)} — nothing leaves this machine.";
            where.Click(CentreOf(dialog, dialog.Use));
            app.Expect("the settings name the downloaded folder", () =>
            {
                Napkin.App.Settings.SettingsStore store = new(window.Settings.Location);
                Assert.Equal(Napkin.App.Settings.AssistantProvider.Mlx, store.Current.Assistant.Provider);
                Assert.Equal(destination, store.Current.Assistant.ModelFolder);
                Assert.Equal(whereabouts, window.AssistantModel.Whereabouts);
            });

            // Back at the drawing: Ctrl/Cmd+Shift+A, the question, Enter (keyboard) — the downloaded
            // folder is the model that answers.
            window.Activate();
            app.Chord(Key.A, KeyModifiers.Shift);
            app.Type(question);
            app.Press(Key.Enter);
            Until(() => !window.IsAssistantThinking);
            app.Expect("the downloaded model answers on the note, in napkin", () =>
            {
                Assert.Equal(answer, window.AssistantAnswerOnScreen);
                Assert.Equal(whereabouts, window.AssistantWhereaboutsOnScreen);
                Assert.Equal(destination, Assert.Single(mlx.Loads));
            });

            app.Press(Key.Escape);
            app.Expect("Escape closes the note", () => Assert.False(window.IsAskingAssistant));
        });

        // Nothing but the stub was ever asked: no socket, and the workflow's folder went with its settings.
        OnlyPinnedUrls();
    }

    static string Sha256(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    /// <summary>A path as the MLX whereabouts line writes it: "~" for the home folder, as <see cref="ModelFolder.HomeRelativePath"/> does.</summary>
    static string HomeRelative(string path) => ModelFolder.HomeRelative(path, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    // ---- Steps and fixtures ---------------------------------------------------------------

    /// <summary>Clicks a text field, selects what is in it and types over it (pointer, keyboard).</summary>
    static void ReplaceText(AppDriver driver, Visual root, TextBox field, string text)
    {
        driver.Click(CentreOf(root, field));
        driver.Chord(Key.A);
        driver.Type(text);
    }

    /// <summary>
    /// Runs the UI thread's queued work until <paramref name="done"/> holds: the local runtime is
    /// asynchronous even against a stub, and its reply comes back to the UI thread as queued work.
    /// </summary>
    static void Until(Func<bool> done)
    {
        DateTime stop = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!done() && DateTime.UtcNow < stop)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A blank sheet through the real shortcut, then a click clear of anything drawn (keyboard, pointer).</summary>
    static void NewSheet(AppDriver app, MainWindow window)
    {
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }
    }

    /// <summary>A sample read straight from disk, the shipped packs, and the id of its "Window 1".</summary>
    static (Design Design, CodePacks Packs, EntityId WindowId) LoadSample(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "samples", $"{name}.scene.json");
        LoadResult result = SceneReader.ReadFile(path);
        Design design = Design.Named(name, Assert.IsType<Loaded>(result).Sketch);
        CodePacks packs = CodePacks.Discover([Shipped]);
        EntityId windowId = Assert.Single(design.Sketch.Entities.Values, entity => entity.Name == "Window 1").Id;
        return (design, packs, windowId);
    }

    /// <summary>A sample read straight from disk, for a reference pack — no window to find, no packs needed.</summary>
    static Design LoadDesign(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "samples", $"{name}.scene.json");
        LoadResult result = SceneReader.ReadFile(path);
        return Design.Named(name, Assert.IsType<Loaded>(result).Sketch);
    }

    /// <summary>The number of the one item a pack holds matching <paramref name="predicate"/>.</summary>
    static int ItemNumbered(ContextPack pack, Func<ContextItem, bool> predicate) => pack.Items.Single(predicate).N;

    /// <summary>The window coordinate a model point is drawn at on the plan.</summary>
    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }
}
