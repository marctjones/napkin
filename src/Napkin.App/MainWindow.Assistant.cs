using System.Collections.Immutable;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using Napkin.App.Viewing;
using Napkin.Assistant.Mlx;
using Napkin.Core.Geometry;
using Napkin.Modules.Assistant;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// Ask and Explain this result (docs/design/llm-assistant.md &#xA7;2.4, &#xA7;8, &#xA7;10 slice B):
/// the Assistant menu, Ctrl/Cmd+Shift+A, and the note on the sheet; Where the model runs&#x2026;
/// (slice C, #231; the MLX choice, docs/design/mlx-runtime.md #241), which chooses the model the
/// note asks; and Sketch from words&#x2026; (slice E, #233), whose reply is a proposal sheet on the
/// same note in Firm up's shape — a tick per rough plank, Enter draws the ticked ones as one undo
/// step, "Assistant sketch", and F firms them up as if they had been drawn by hand.
/// </summary>
public partial class MainWindow
{
    CancellationTokenSource? _assistantCancel;
    DispatcherTimer? _assistantTimer;
    DateTime _assistantStarted;
    int _assistantGeneration;
    AssistantWindow? _assistantWindow;
    AssistantTask _assistantTask = AssistantTask.Ask;
    ProposalPlan? _assistantPlan;
    readonly List<(CheckBox Tick, ProposalLine Line)> _assistantProposalLines = [];

    /// <summary>
    /// How the assistant reaches a program on this machine: null for napkin's own loopback-only
    /// handler (<see cref="Napkin.Assistant.LocalServer.LoopbackHttp"/>). The GUI suite sets a stub
    /// here, the <see cref="PackRoots"/> way, so no workflow opens a socket.
    /// </summary>
    public HttpMessageHandler? AssistantHttp { get; set; }

    /// <summary>
    /// The MLX bridge the assistant's in-process model uses: null for the real one
    /// (<see cref="NativeMlx"/>). The GUI suite sets a fake here, the <see cref="AssistantHttp"/> way,
    /// so no workflow loads the library (docs/design/mlx-runtime.md &#xA7;4.3).
    /// </summary>
    public INativeMlx? AssistantMlx { get; set; }

    /// <summary>
    /// Why MLX cannot run in this process, or null when it can:
    /// <see cref="MlxAvailability.ForThisProcess"/> by default. The GUI suite overrides it so an MLX
    /// workflow runs on every CI platform, not only Apple silicon (docs/design/mlx-runtime.md &#xA7;7.3).
    /// </summary>
    public Func<string?> AssistantMlxAvailable { get; set; } = MlxAvailability.ForThisProcess;

    /// <summary>
    /// The model <em>Download…</em> offers (docs/design/mlx-runtime.md &#xA7;6, #242):
    /// <see cref="ModelCatalog.Default"/>. The GUI suite sets a catalog entry of small files whose
    /// hashes it computed, served by the stub under <see cref="AssistantHttp"/>, so no workflow
    /// fetches a real model.
    /// </summary>
    public CatalogModel AssistantDownloadModel { get; set; } = ModelCatalog.Default;

    /// <summary>
    /// Where downloaded models live: <c>models/</c> beside the settings file — napkin's config
    /// directory for the person's own settings (&#xA7;6.4, decision 5), and a workflow's own throw-away
    /// folder under the GUI suite.
    /// </summary>
    public string AssistantModelsDirectory => ModelCatalog.ModelsDirectory(
        Path.GetDirectoryName(Settings.Location) is { Length: > 0 } folder ? folder : Napkin.App.Settings.SettingsStore.ConfigDirectory());

    /// <summary>The Where the model runs&#x2026; dialog, when it is open.</summary>
    public AssistantWindow? WhereModelRuns => _assistantWindow;

    /// <summary>The Assistant menu's Where the model runs&#x2026; entry.</summary>
    public MenuItem WhereModelRunsMenuEntry => WhereModelRunsMenuItem;

    void OnWhereModelRunsClicked(object? sender, RoutedEventArgs e) => OpenWhereModelRuns();

    /// <summary>
    /// Opens Assistant &#x2192; Where the model runs&#x2026; (&#xA7;8) on the person's settings, or brings
    /// the open one forward without disturbing what is typed in it.
    /// </summary>
    /// <returns>The dialog.</returns>
    public AssistantWindow OpenWhereModelRuns()
    {
        if (_assistantWindow is null)
        {
            _assistantWindow = new AssistantWindow
            {
                Http = AssistantHttp,
                Mlx = AssistantMlx,
                MlxUnavailable = AssistantMlxAvailable,
                DownloadModel = AssistantDownloadModel,
                ModelsDirectory = AssistantModelsDirectory,
                Apply = UseAssistantSettings,
            };
            _assistantWindow.ShowSettings(Settings.Current.Assistant);
            _assistantWindow.Closed += (_, _) => _assistantWindow = null;
        }

        _assistantWindow.Show(this);
        _assistantWindow.Activate();
        return _assistantWindow;
    }

    /// <summary>
    /// Saves where the model runs and builds the model the note asks from then on. A question still
    /// out is cancelled first, so no reply from the old model lands on the note.
    /// </summary>
    /// <returns>The note's whereabouts line under the new model.</returns>
    string UseAssistantSettings(Napkin.App.Settings.AssistantSettings chosen)
    {
        Settings.Update(settings => settings with { Assistant = chosen });
        if (IsAssistantThinking)
        {
            CancelAssistantThinking();
        }

        IAssistantModel previous = AssistantModel;
        AssistantModel = AssistantModels.FromSettings(Settings.Current, AssistantHttp, AssistantMlx, AssistantMlxAvailable);
        (previous as IDisposable)?.Dispose();
        return AssistantModel.Whereabouts;
    }

    /// <summary>The note, for the GUI suite to find a control on.</summary>
    public Border AssistantSheet => AssistantPanel;

    /// <summary>Whether the note is up.</summary>
    public bool IsAskingAssistant => AssistantPanel.IsVisible;

    /// <summary>Whether a request is out (the thinking line shows).</summary>
    public bool IsAssistantThinking => AssistantThinkingText.IsVisible;

    /// <summary>The thinking line's text, empty while nothing is out.</summary>
    public string AssistantThinkingLine => AssistantThinkingText.IsVisible ? AssistantThinkingText.Text ?? string.Empty : string.Empty;

    /// <summary>The question box.</summary>
    public TextBox AssistantQuestionField => AssistantQuestionBox;

    /// <summary>The answer as the note shows it, refusals inline in napkin's own words; empty until one arrives.</summary>
    public string AssistantAnswerOnScreen => AssistantAnswerText.IsVisible
        ? string.Concat(AssistantAnswerText.Inlines?.OfType<Run>().Select(run => run.Text ?? string.Empty) ?? [])
        : string.Empty;

    /// <summary>The "From napkin:" block's lines, verbatim, in the order the answer referred to them.</summary>
    public IReadOnlyList<string> AssistantReferenceTexts =>
        [.. AssistantReferenceLines.Children.OfType<TextBlock>().Select(line => line.Text ?? string.Empty)];

    /// <summary>The disclaimer line, empty until an answer is on screen.</summary>
    public string AssistantDisclaimerOnScreen => AssistantDisclaimerText.IsVisible ? AssistantDisclaimerText.Text ?? string.Empty : string.Empty;

    /// <summary>The whereabouts line, empty until an answer is on screen.</summary>
    public string AssistantWhereaboutsOnScreen => AssistantWhereaboutsText.IsVisible ? AssistantWhereaboutsText.Text ?? string.Empty : string.Empty;

    /// <summary>The context pack the last question was asked with, for the GUI suite to compute expected item numbers from.</summary>
    public ContextPack? LastAssistantPack { get; private set; }

    /// <summary>The Assistant menu's Ask… entry.</summary>
    public MenuItem AskMenuEntry => AskMenuItem;

    /// <summary>The Assistant menu's Explain this result entry.</summary>
    public MenuItem ExplainMenuEntry => ExplainMenuItem;

    /// <summary>The Assistant menu's Sketch from words&#x2026; entry.</summary>
    public MenuItem SketchFromWordsMenuEntry => SketchFromWordsMenuItem;

    /// <summary>What the note is doing: answering (Ask, Explain) or sketching (Sketch from words).</summary>
    public AssistantTask AssistantNoteTask => _assistantTask;

    /// <summary>Whether a proposal sheet is up on the note, waiting for Enter.</summary>
    public bool IsProposingSketch => _assistantPlan is not null;

    /// <summary>The proposal sheet's ticks, one per line in the reply's order; a refused line's is off and cannot be ticked.</summary>
    public IReadOnlyList<CheckBox> AssistantProposalTicks => [.. _assistantProposalLines.Select(line => line.Tick)];

    /// <summary>The proposal sheet's lines, word for word, in the reply's order.</summary>
    public IReadOnlyList<string> AssistantProposalLineTexts => [.. _assistantProposalLines.Select(line => line.Line.Sentence)];

    /// <summary>The proposal sheet's message line: a refusal in the updater's words, or the stale rule's sentence; empty when none.</summary>
    public string AssistantProposalMessage => AssistantProposalMessageText.IsVisible ? AssistantProposalMessageText.Text ?? string.Empty : string.Empty;

    /// <summary>The proposal sheet's Draw button.</summary>
    public Button AssistantProposalDraw => AssistantProposalOkButton;

    void WireAssistant()
    {
        AddHandler(KeyDownEvent, OnAssistantKeyDown, RoutingStrategies.Tunnel);
        AssistantProposalOkButton.Click += (_, _) => AcceptAssistantProposal();
        AssistantProposalCancelButton.Click += (_, _) => CloseAssistant();
    }

    void OnAskClicked(object? sender, RoutedEventArgs e) => BeginAsk();

    void OnExplainClicked(object? sender, RoutedEventArgs e) => BeginExplain();

    void OnSketchFromWordsClicked(object? sender, RoutedEventArgs e) => BeginSketchFromWords();

    void OnAssistantKeyDown(object? sender, KeyEventArgs e)
    {
        if (!AssistantPanel.IsVisible || e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (IsAssistantThinking)
            {
                CancelAssistantThinking();
            }
            else
            {
                CloseAssistant();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !IsAssistantThinking)
        {
            if (e.Source is TextBox)
            {
                _ = AskAssistant();
                e.Handled = true;
            }
            else if (_assistantPlan is not null && e.Source is not Button { Name: "AssistantProposalCancelButton" })
            {
                // Firm up's key: Enter anywhere but the Cancel button draws what is ticked.
                AcceptAssistantProposal();
                e.Handled = true;
            }
        }
    }

    /// <summary>Opens the note for Ask (or brings the open one to the front) and gives the question box the keyboard.</summary>
    void BeginAsk() => OpenAssistant(AssistantTask.Ask);

    /// <summary>Opens the note for Sketch from words and gives the description box the keyboard.</summary>
    void BeginSketchFromWords() => OpenAssistant(AssistantTask.Sketch);

    /// <summary>
    /// Opens the note for a task, or brings the open one to the front. Switching task starts the
    /// note afresh: a question still out for the other task is cancelled and its reply never lands.
    /// </summary>
    void OpenAssistant(AssistantTask task)
    {
        if (IsShapingPart || IsAskingToSave || IsJoining || IsFirmingUp)
        {
            return;
        }

        if (!AssistantPanel.IsVisible || _assistantTask != task)
        {
            _assistantCancel?.Cancel();
            _assistantGeneration++;
            ShowAssistantThinking(false);
            _assistantTask = task;
            bool sketching = task == AssistantTask.Sketch;
            AssistantTitle.Text = sketching ? "Sketch from words" : "Assistant";
            AssistantQuestionBox.PlaceholderText = sketching
                ? "Describe a piece of furniture; rough planks are proposed for you to tick…"
                : "Ask a question about the design or result on screen…";
            AssistantHintText.Text = sketching ? "Enter to sketch · Esc to cancel or close" : "Enter to ask · Esc to cancel or close";
            AssistantPanel.IsVisible = true;
            AssistantQuestionBox.Text = string.Empty;
            ClearAssistantAnswer();
        }

        AssistantQuestionBox.Focus();
    }

    /// <summary>Opens the note with the selected result's question filled in, and asks it at once.</summary>
    void BeginExplain()
    {
        if (ExplainQuestionForSelection() is not { } question)
        {
            return;
        }

        BeginAsk();
        AssistantQuestionBox.Text = question;
        AssistantQuestionBox.CaretIndex = question.Length;
        _ = AskAssistant();
    }

    /// <summary>
    /// The question Explain this result asks, for the selected opening's header check, the selected
    /// wall's bracing check or the selected deck's checks — or null when the selection has none, which
    /// is also when the menu item is disabled.
    /// </summary>
    string? ExplainQuestionForSelection()
    {
        if (Editor.OnlySelected is not { } id)
        {
            return null;
        }

        if (Checks.FirstOrDefault(check => check.Opening.Id == id || check.Opening.Wall.Id == id) is { } header)
        {
            return $"Explain the header check for {Editor.NameOf(header.Opening.Id)}.";
        }

        if (BracingChecks.FirstOrDefault(check => check.Wall.Id == id) is { } bracing)
        {
            return $"Explain the bracing check for {Editor.NameOf(bracing.Wall.Id)}.";
        }

        if (DeckCheck.Of(Editor.Sketch, Packs).FirstOrDefault(deck => deck.Deck.Id == id) is { } deckChecks)
        {
            return $"Explain the deck check for {Editor.NameOf(deckChecks.Deck.Id)}.";
        }

        return null;
    }

    /// <summary>Whether the selection has a check result Explain this result can ask about.</summary>
    bool SelectionHasCheckResult => ExplainQuestionForSelection() is not null;

    void CloseAssistant()
    {
        _assistantCancel?.Cancel();
        AssistantPanel.IsVisible = false;
        ShowAssistantThinking(false);
        EndAssistantProposal();
        FocusDrawing();
    }

    void CancelAssistantThinking()
    {
        _assistantCancel?.Cancel();
        ShowAssistantThinking(false);
    }

    /// <summary>
    /// Builds the context pack for a question (docs/design/llm-assistant.md &#xA7;3) from what the
    /// app already computed: the design, the selection, every header, bracing and deck check on
    /// screen, and the rows of whichever list is open (&#xA7;3.2, &#xA7;10 slice D, #232).
    /// </summary>
    ContextPack BuildAssistantPack(string question)
    {
        ContextChecks checks = new(Packs.Resolve(Editor.Sketch.Code), Checks, BracingChecks, DeckCheck.Of(Editor.Sketch, Packs));
        return ContextPack.For(Editor.Design, Editor.Selection, checks, OpenAssistantLists(), question);
    }

    /// <summary>
    /// The open list's rows (docs/design/llm-assistant.md &#xA7;3.2): when the cut-list window is
    /// open, exactly the CSV of the tab on screen — the list the person is looking at when they
    /// ask, and nothing else, so the pack never carries more than what is actually shown. Napkin's
    /// four lists live in one window's tabs rather than four windows of their own (&#xA7;10 slice D
    /// as built, docs/design/llm-assistant.md), so "the shopping list is open" means that tab is
    /// the one showing.
    /// </summary>
    /// <remarks>
    /// The shopping list's own CSV property, not <see cref="OpenList.ShoppingList"/>'s row-based
    /// factory, because the window's <see cref="CutListWindow.ShoppingCsv"/> also carries the
    /// estimate lines once the person has priced anything (#141) — exactly what is on screen, which
    /// a rebuild from rows alone would miss.
    /// </remarks>
    IEnumerable<OpenList> OpenAssistantLists()
    {
        if (CutList is not { } window)
        {
            return [];
        }

        if (window.IsShowingShoppingList)
        {
            return [new OpenList("Shopping list", window.ShoppingCsv)];
        }

        if (window.IsShowingCutLayout)
        {
            return [new OpenList("Cut layout", window.LayoutCsv)];
        }

        if (window.IsShowingSizes)
        {
            return [new OpenList("Fasteners and supplies", window.ExtrasCsv)];
        }

        return [new OpenList("Cut list", window.Csv)];
    }

    /// <summary>
    /// Asks the question in the box. The request runs off the UI thread's blocking path — the
    /// editor is never blocked, and Escape cancels while it is thinking (&#xA7;8, &#xA7;12.1).
    /// </summary>
    async Task AskAssistant()
    {
        string question = AssistantQuestionBox.Text?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            return;
        }

        _assistantCancel?.Cancel();
        _assistantCancel?.Dispose();
        CancellationTokenSource cancel = new();
        _assistantCancel = cancel;
        int generation = ++_assistantGeneration;
        AssistantTask task = _assistantTask;

        ContextPack pack = BuildAssistantPack(question);
        LastAssistantPack = pack;

        // A sketch is planned against the design as it is now, not as it is when the reply comes:
        // the person may draw meanwhile, and then the plan is stale and refused (§4.3).
        Sketch askedAbout = Editor.Sketch;
        LayerId layer = Editor.LayerForNewParts();
        ModelRequest request = task == AssistantTask.Sketch
            ? ModelRequest.ForProposal(AssistantPrompts.Sketch, pack, question, SketchProposal.Schema)
            : ModelRequest.ForAnswer(AssistantPrompts.Ask, pack, question);
        ClearAssistantAnswer();
        ShowAssistantThinking(true);

        ModelReply reply;
        try
        {
            reply = await AssistantModel.AskAsync(request, cancel.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by Escape (CancelAssistantThinking) or a later question taking over: no reply is surfaced (§2.1).
            return;
        }

        // A later question, or the note closing and reopening, has already moved on; this reply is stale.
        if (generation != _assistantGeneration)
        {
            return;
        }

        ShowAssistantThinking(false);
        if (task == AssistantTask.Sketch)
        {
            RenderSketchReply(reply, askedAbout, layer);
        }
        else
        {
            RenderAssistantReply(reply, pack);
        }
    }

    void ShowAssistantThinking(bool thinking)
    {
        AssistantThinkingText.IsVisible = thinking;
        if (thinking)
        {
            _assistantStarted = DateTime.UtcNow;
            AssistantThinkingText.Text = "thinking…";
            _assistantTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _assistantTimer.Tick -= OnAssistantTimerTick;
            _assistantTimer.Tick += OnAssistantTimerTick;
            _assistantTimer.Start();
        }
        else
        {
            _assistantTimer?.Stop();
        }
    }

    void OnAssistantTimerTick(object? sender, EventArgs e) =>
        AssistantThinkingText.Text = $"thinking… ({(int)(DateTime.UtcNow - _assistantStarted).TotalSeconds}s)";

    void ClearAssistantAnswer()
    {
        AssistantAnswerText.Inlines?.Clear();
        AssistantAnswerText.IsVisible = false;
        AssistantReferenceLines.Children.Clear();
        AssistantReferences.IsVisible = false;
        AssistantDisclaimerText.IsVisible = false;
        AssistantWhereaboutsText.IsVisible = false;
        EndAssistantProposal();
        AssistantProposalMessageText.IsVisible = false;
    }

    /// <summary>
    /// Renders the note in order (&#xA7;8): the guarded answer, refusals inline in the pencil colour;
    /// "From napkin:" then every referenced item verbatim, mono; the disclaimer; the whereabouts —
    /// the last two always, whatever the reply was.
    /// </summary>
    void RenderAssistantReply(ModelReply reply, ContextPack pack)
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        InlineCollection inlines = AssistantAnswerText.Inlines ??= [];
        inlines.Clear();
        AssistantReferenceLines.Children.Clear();

        switch (reply)
        {
            case ModelReply.Text text:
            {
                GuardedAnswer guarded = AnswerGuard.Check(text.Answer, pack);
                foreach (GuardedSentence sentence in guarded.Sentences)
                {
                    inlines.Add(new Run(sentence.Shown + sentence.Separator)
                    {
                        Foreground = sentence.Kept ? null : new SolidColorBrush(palette.Dimension),
                    });
                }

                FontFamily mono = (FontFamily)this.FindResource("SeFontMono")!;
                foreach (int n in guarded.References)
                {
                    if (pack.Item(n) is { } item)
                    {
                        AssistantReferenceLines.Children.Add(new TextBlock
                        {
                            Text = item.ToString(),
                            FontFamily = mono,
                            FontSize = 11,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        });
                    }
                }

                break;
            }

            case ModelReply.Refused refused:
                inlines.Add(new Run(refused.Reason));
                break;

            default:
                inlines.Add(new Run("The assistant's reply was not something napkin could read."));
                break;
        }

        AssistantAnswerText.IsVisible = true;
        AssistantReferences.IsVisible = AssistantReferenceLines.Children.Count > 0;
        ShowAssistantFooter();
    }

    /// <summary>The disclaimer and the whereabouts, the note's last two lines whatever the reply was (&#xA7;8).</summary>
    void ShowAssistantFooter()
    {
        AssistantDisclaimerText.Text = ContextPack.Disclaimer;
        AssistantDisclaimerText.IsVisible = true;
        AssistantWhereaboutsText.Text = AssistantModel.Whereabouts;
        AssistantWhereaboutsText.IsVisible = true;
    }

    /// <summary>One plain line in the answer's place: a refusal, the unreadable-reply line, the closing line.</summary>
    void ShowAssistantLine(string text)
    {
        InlineCollection inlines = AssistantAnswerText.Inlines ??= [];
        inlines.Clear();
        inlines.Add(new Run(text));
        AssistantAnswerText.IsVisible = true;
    }

    /// <summary>
    /// A sketch reply (&#xA7;4.3&#x2013;&#xA7;4.4): a document napkin reads becomes the proposal sheet,
    /// planned against the design the question was asked about; anything else is one line — the
    /// runtime's refusal in its words, or napkin's "not a proposal" — and nothing else happens. The
    /// model's note is parsed and never shown: the guard would have to vet it, and the planks are the
    /// proposal (&#xA7;18 of the note).
    /// </summary>
    void RenderSketchReply(ModelReply reply, Sketch askedAbout, LayerId layer)
    {
        switch (reply)
        {
            case ModelReply.Json json when SketchProposal.Parse(json.Document) is { } proposal:
            {
                ProposalPlan plan = proposal.Plan(askedAbout, layer, Editor.NextPartName);
                if (plan.Lines.IsEmpty)
                {
                    ShowAssistantLine(SketchProposal.NoParts);
                }
                else if (!plan.IsFor(Editor.Sketch))
                {
                    ShowAssistantProposalMessage(ProposalPlan.StaleText);
                }
                else
                {
                    ShowAssistantProposal(plan);
                }

                break;
            }

            case ModelReply.Refused refused:
                ShowAssistantLine(refused.Reason);
                break;

            default:
                ShowAssistantLine(SketchProposal.Unreadable);
                break;
        }

        ShowAssistantFooter();
    }

    /// <summary>
    /// The proposal sheet, in Firm up's shape (<see cref="FirmUpPanel"/>): a tick per line, all
    /// ticked; a line napkin refused is shown with its reason in the pencil colour and a tick that is
    /// off and cannot be turned on. The Draw button has the keyboard, so Enter draws.
    /// </summary>
    void ShowAssistantProposal(ProposalPlan plan)
    {
        EndAssistantProposal();
        _assistantPlan = plan;
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        foreach (ProposalLine line in plan.Lines)
        {
            CheckBox tick = new()
            {
                IsChecked = !line.Refused,
                IsEnabled = !line.Refused,
                MinWidth = 0,
                Padding = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(tick, line.Sentence);
            TextBlock text = new()
            {
                Text = line.Sentence,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
                Foreground = line.Refused ? new SolidColorBrush(palette.Dimension) : null,
            };

            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(text, 1);
            row.Children.Add(tick);
            row.Children.Add(text);
            AssistantProposalLines.Children.Add(row);
            _assistantProposalLines.Add((tick, line));
        }

        AssistantProposalMessageText.IsVisible = false;
        AssistantProposal.IsVisible = true;
        AssistantProposalButtons.IsVisible = true;
        AssistantProposalOkButton.Focus();
    }

    /// <summary>Takes the proposal sheet off the note; nothing is drawn.</summary>
    void EndAssistantProposal()
    {
        _assistantPlan = null;
        _assistantProposalLines.Clear();
        AssistantProposalLines.Children.Clear();
        AssistantProposal.IsVisible = false;
        AssistantProposalButtons.IsVisible = false;
    }

    void ShowAssistantProposalMessage(string text)
    {
        AssistantProposalMessageText.Text = text;
        AssistantProposalMessageText.Foreground = new SolidColorBrush(CanvasPalette.For(ActualThemeVariant).Selection);
        AssistantProposalMessageText.IsVisible = true;
    }

    /// <summary>
    /// Draws the ticked planks (&#xA7;4.3): one gesture, one undo step "Assistant sketch", the message
    /// bar saying how many. A plan made against a design that has since changed is refused whole and
    /// the sheet goes; a line the updater refused is reported on the message line in its words while
    /// the rest land, and the sheet stays with every tick off, as Firm up's does. Once everything
    /// ticked has landed the note says so and points at Firm up, and the drawing has the keyboard so
    /// F, and undo, reach it.
    /// </summary>
    void AcceptAssistantProposal()
    {
        if (_assistantPlan is not { } plan)
        {
            return;
        }

        ProposalOutcome outcome = plan.Accept(
            Editor,
            _assistantProposalLines.Where(line => line.Tick.IsChecked == true).Select(line => line.Line),
            SketchProposal.MessageLine);

        if (outcome.Stale)
        {
            EndAssistantProposal();
            ShowAssistantProposalMessage(ProposalPlan.StaleText);
            return;
        }

        if (!outcome.Rejections.IsEmpty)
        {
            ShowAssistantProposalMessage(string.Join("\n", outcome.Rejections));
            foreach ((CheckBox tick, _) in _assistantProposalLines)
            {
                tick.IsChecked = false;
            }

            return;
        }

        EndAssistantProposal();
        ShowAssistantLine(SketchProposal.ClosingLine(outcome.Landed));
        FocusDrawing();
    }
}
