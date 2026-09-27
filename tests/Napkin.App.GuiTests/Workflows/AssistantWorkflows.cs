using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Assistant;
using Napkin.Modules.Building;

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

    // ---- Steps and fixtures ---------------------------------------------------------------

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
