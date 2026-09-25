using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// A note's panel (docs/design/renovation-sketches.md §8): its words, typed, and its symbol, one of
/// six small pencil glyphs, the chosen one moss-washed. One undo step each.
/// </summary>
public partial class MainWindow
{
    readonly List<Button> _noteSymbolButtons = [];
    bool _fillingNote;

    /// <summary>Whether the note panel is showing (a note is selected).</summary>
    public bool IsShowingNote => NotePanel.IsVisible;

    /// <summary>The note's words box, for the GUI suite.</summary>
    public TextBox NoteTextField => NoteTextBox;

    /// <summary>The six symbol buttons, none first, for the GUI suite.</summary>
    public IReadOnlyList<Button> NoteSymbolButtons => _noteSymbolButtons;

    /// <summary>The symbol whose button is washed as chosen, or null with no note shown.</summary>
    public NoteSymbol? ChosenNoteSymbol => NotePanel.IsVisible ? SelectedNote()?.Symbol : null;

    /// <summary>What the panel says of the note's symbol.</summary>
    public string NoteSymbolText => NotePanel.IsVisible ? NoteSymbolWord.Text ?? string.Empty : string.Empty;

    void WireNotes()
    {
        NotePhaseBox.ItemsSource = PhaseChoices.Select(PhaseCommand.Word).ToArray();
        foreach (NoteSymbol symbol in NoteTool.Symbols)
        {
            Button button = new()
            {
                Width = 30,
                Height = 26,
                Padding = new Avalonia.Thickness(2),
                Focusable = false,
                Tag = symbol,
                Content = new NoteGlyphIcon { Symbol = symbol },
            };
            Avalonia.Automation.AutomationProperties.SetName(button, $"Symbol: {NoteTool.Word(symbol)}");
            ToolTip.SetTip(button, symbol == NoteSymbol.None ? "No symbol: a small circle and the words" : $"Symbol: {NoteTool.Word(symbol)}");
            button.Click += (_, _) => SetNoteSymbol(symbol);
            NoteSymbolRow.Children.Add(button);
            _noteSymbolButtons.Add(button);
        }

        NoteTextBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyNoteText();
                FocusDrawing();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        NoteTextBox.LostFocus += (_, _) => ApplyNoteText();
        DrawingCanvas.NotePlaced += (_, _) =>
        {
            ShowNotePanel();
            NoteTextBox.Focus();
        };
        Editor.SelectionChanged += (_, _) => ShowNotePanel();
        Editor.DesignChanged += (_, _) => ShowNotePanel();
    }

    Note? SelectedNote() => Editor.OnlySelected is { } id ? Editor.Sketch.Find<Note>(id) : null;

    /// <summary>Shows the note panel for one selected note, or hides it.</summary>
    void ShowNotePanel()
    {
        Note? note = SelectedNote();
        NotePanel.IsVisible = note is not null && !IsShapingPart;
        if (note is null)
        {
            return;
        }

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant, DrawingCanvas.Look);
        _fillingNote = true;
        try
        {
            NoteHeadline.Text = Editor.NameOf(note.Id);
            if (!NoteTextBox.IsFocused)
            {
                NoteTextBox.Text = note.Text;
            }

            NotePhaseBox.SelectedIndex = Array.IndexOf(PhaseChoices, note.Phase);
            foreach (Button button in _noteSymbolButtons)
            {
                // The chosen glyph is moss-washed, as an armed tool is; the others sit on the paper.
                bool chosen = (NoteSymbol)button.Tag! == note.Symbol;
                button.Background = chosen ? new SolidColorBrush(palette.Selection, 0.28) : Brushes.Transparent;
                button.BorderBrush = chosen ? new SolidColorBrush(palette.Selection) : Brushes.Transparent;
                button.BorderThickness = new Avalonia.Thickness(1);
                ((NoteGlyphIcon)button.Content!).Ink = palette.Dimension;
                ((NoteGlyphIcon)button.Content!).InvalidateVisual();
            }

            NoteSymbolWord.Text = note.Symbol == NoteSymbol.None ? "No symbol." : $"Symbol: {NoteTool.Word(note.Symbol)}.";
        }
        finally
        {
            _fillingNote = false;
        }
    }

    void OnNotePhaseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_fillingNote && NotePhaseBox.SelectedIndex >= 0 && NotePhaseBox.SelectedIndex < PhaseChoices.Length)
        {
            SetPhase(PhaseChoices[NotePhaseBox.SelectedIndex]);
        }
    }

    /// <summary>Says what the selected note says; a rough-in word napkin knows gives it its symbol. One undo step.</summary>
    public void ApplyNoteText()
    {
        if (_fillingNote || SelectedNote() is not { } note)
        {
            return;
        }

        string text = NoteTextBox.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 && note.Symbol == NoteSymbol.None)
        {
            if (note.Text.Length > 0)
            {
                Editor.Say(EditSeverity.Hint, $"A note with no symbol says something: {Editor.NameOf(note.Id)} keeps \"{note.Text}\".");
            }

            return;
        }

        if (NoteTool.Say(note, text) is { } request)
        {
            Editor.Apply(request, $"Noted \"{text}\"");
        }
    }

    /// <summary>Gives the selected note a symbol; one undo step.</summary>
    public void SetNoteSymbol(NoteSymbol symbol)
    {
        if (SelectedNote() is not { } note || note.Symbol == symbol || (symbol == NoteSymbol.None && note.Text.Length == 0))
        {
            ShowNotePanel();
            return;
        }

        Editor.Apply(new SetNote(note.Id, note.Text, symbol), $"Marked {Editor.NameOf(note.Id)} {NoteTool.Word(symbol)}");
    }
}
