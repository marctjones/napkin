using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Assistant;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// Ask and Explain this result (docs/design/llm-assistant.md &#xA7;2.4, &#xA7;8, &#xA7;10 slice B):
/// the Assistant menu, Ctrl/Cmd+Shift+A, and the note on the sheet. Sketch from words and Where the
/// model runs are later slices (E, C) and stay greyed out here.
/// </summary>
public partial class MainWindow
{
    CancellationTokenSource? _assistantCancel;
    DispatcherTimer? _assistantTimer;
    DateTime _assistantStarted;
    int _assistantGeneration;

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

    void WireAssistant()
    {
        AddHandler(KeyDownEvent, OnAssistantKeyDown, RoutingStrategies.Tunnel);
    }

    void OnAskClicked(object? sender, RoutedEventArgs e) => BeginAsk();

    void OnExplainClicked(object? sender, RoutedEventArgs e) => BeginExplain();

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
        else if (e.Key == Key.Enter && e.Source is TextBox && !IsAssistantThinking)
        {
            _ = AskAssistant();
            e.Handled = true;
        }
    }

    /// <summary>Opens the note (or brings the open one to the front) and gives the question box the keyboard.</summary>
    void BeginAsk()
    {
        if (IsShapingPart || IsAskingToSave || IsJoining || IsFirmingUp)
        {
            return;
        }

        if (!AssistantPanel.IsVisible)
        {
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
    /// screen, and — until slice D (#232) — no open lists.
    /// </summary>
    ContextPack BuildAssistantPack(string question)
    {
        ContextChecks checks = new(Packs.Resolve(Editor.Sketch.Code), Checks, BracingChecks, DeckCheck.Of(Editor.Sketch, Packs));
        return ContextPack.For(Editor.Design, Editor.Selection, checks, [], question);
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

        ContextPack pack = BuildAssistantPack(question);
        LastAssistantPack = pack;
        ClearAssistantAnswer();
        ShowAssistantThinking(true);

        ModelReply reply;
        try
        {
            reply = await AssistantModel.AskAsync(ModelRequest.ForAnswer(AssistantPrompts.Ask, pack, question), cancel.Token).ConfigureAwait(true);
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
        RenderAssistantReply(reply, pack);
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
        AssistantDisclaimerText.Text = ContextPack.Disclaimer;
        AssistantDisclaimerText.IsVisible = true;
        AssistantWhereaboutsText.Text = AssistantModel.Whereabouts;
        AssistantWhereaboutsText.IsVisible = true;
    }
}
