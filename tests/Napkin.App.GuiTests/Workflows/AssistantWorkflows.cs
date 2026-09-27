using System.Collections.Immutable;
using System.Text.Json;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
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
        string whereabouts = $"In napkin (MLX): {Path.GetFileName(folder)} from {folder} — nothing leaves this machine.";

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
