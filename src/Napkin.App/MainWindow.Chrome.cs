using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Napkin.App.Designs;
using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Editing;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Building;
using Napkin.Modules.Furniture;
using Napkin.Modules.Editing;

namespace Napkin.App;

public partial class MainWindow
{
    /// <summary>
    /// Greys out what has nothing to act on — the menu item and its toolbar button together, so
    /// the two never disagree about whether a function is available.
    /// </summary>
    void UpdateMenuEnablement()
    {
        bool anything = Editor.Selection.Count > 0;
        bool shapeable = Editor.OnlySelected is not null && !IsShapingPart && !IsShowingStandardView;
        DeleteMenuItem.IsEnabled = DeleteToolButton.IsEnabled = anything;
        PinMenuItem.IsEnabled = PinToolButton.IsEnabled = anything;
        DuplicateMenuItem.IsEnabled = DuplicateToolButton.IsEnabled = anything;
        MirrorEastWestMenuItem.IsEnabled = MirrorNorthSouthMenuItem.IsEnabled = anything;
        ShapeMenuItem.IsEnabled = ShapeToolButton.IsEnabled = shapeable;

        bool turnable = Editor.OnlySelectedBox is not null && !IsShapingPart;
        TurnXMenuItem.IsEnabled = TurnXToolButton.IsEnabled = turnable;
        TurnYMenuItem.IsEnabled = TurnYToolButton.IsEnabled = turnable;
        TurnZMenuItem.IsEnabled = TurnZToolButton.IsEnabled = turnable;

        // The shape workshop covers the paper and takes the toolbar's stock icons with it, so the
        // menu's way in to the same stock goes too: there is no paper to drag it onto.
        StockMenu.IsEnabled = !IsShapingPart && !IsShowingStandardView;

        // Undo and redo name what they would do, and grey out when there is nothing to. An
        // underscore in a part's name is doubled so the menu shows it rather than taking it as
        // an access key.
        UndoHistory history = Editor.History;
        UndoMenuItem.IsEnabled = history.CanUndo;
        UndoMenuItem.Header = history.UndoWhat is { } undo ? $"_Undo {Escaped(undo)}" : "_Undo";
        RedoMenuItem.IsEnabled = history.CanRedo;
        RedoMenuItem.Header = history.RedoWhat is { } redo ? $"_Redo {Escaped(redo)}" : "_Redo";

        static string Escaped(string text) => text.Replace("_", "__", StringComparison.Ordinal);
    }

    void UpdateMessageBar()
    {
        EditMessage? message = Editor.LastMessage;
        if (message is null)
        {
            MessageBar.IsVisible = false;
            MessageOfferButton.IsVisible = false;
            return;
        }

        // The message bar is on the bench, which is dark in both themes, so it always reads
        // with the dark palette.
        CanvasPalette palette = CanvasPalette.Dark;
        MessageText.Text = message.Text;
        MessageText.Foreground = new SolidColorBrush(message.Severity switch
        {
            EditSeverity.Problem => palette.Snap,
            EditSeverity.Hint => palette.Dimension,
            _ => palette.Label,
        });

        MessageOfferButton.IsVisible = message.Offer is not null;
        MessageOfferButton.Content = message.Offer?.Text ?? string.Empty;
        MessageBar.IsVisible = true;
    }

    /// <summary>
    /// The relationship list, on demand (#62): a count badge while nothing it talks about is in
    /// play, and every sentence once a part it names is selected or under the pointer.
    /// </summary>
    /// <remarks>
    /// Expanded, it shows the whole list, not only the selected part's rows: the sentences are the
    /// same ones it has always shown, and which part is selected only decides whether the drawing
    /// has something to say that is worth the room — and which rows come first, so that when the
    /// list is longer than the room the Part panel leaves it (#73), what is about the part in play
    /// is at the top rather than scrolled out of sight. The panel stays off the screen while the
    /// shape workshop is open, whatever changes underneath it.
    /// </remarks>
    void UpdateRelationships()
    {
        IReadOnlyList<RelationshipEntry> entries = Editor.RelationshipEntries();
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        bool expanded = entries.Any(entry => entry.Entities.Any(IsInPlay));

        RelationshipsList.Children.Clear();
        if (expanded)
        {
            foreach (RelationshipEntry entry in entries.OrderBy(entry => entry.Entities.Any(IsInPlay) ? 0 : 1))
            {
                RelationshipsList.Children.Add(RelationshipRowFor(entry, palette));
            }
        }

        RelationshipsList.IsVisible = expanded;
        RelationshipsHeadline.Text = expanded
            ? $"Relationships — {entries.Count}"
            : entries.Count == 1 ? "1 relationship" : $"{entries.Count} relationships";
        RelationshipsHeadline.FontSize = expanded ? 12 : 11;
        RelationshipsPanel.Padding = expanded ? new Thickness(10, 8) : new Thickness(8, 3);
        RelationshipsPanel.CornerRadius = new CornerRadius(4);
        RelationshipsPanel.IsVisible = entries.Count > 0 && !IsShapingPart;

        // Open, its rows take clicks (#77) and a wheel turn scrolls them. Collapsed to its badge it
        // is only a label on the drawing, and a click or a wheel turn there is the drawing's.
        RelationshipsPanel.IsHitTestVisible = expanded;
        if (!expanded)
        {
            _rowUnderPointer = null;
        }
    }

    /// <summary>
    /// One row of the relationship list: the sentence, and a button that removes it (#77). Resting
    /// the pointer on the row draws attention to the parts it holds, in both views.
    /// </summary>
    Grid RelationshipRowFor(RelationshipEntry entry, CanvasPalette palette)
    {
        TextBlock sentence = new()
        {
            Text = entry.Text,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = new SolidColorBrush(palette.Label),
        };

        Button remove = new()
        {
            Content = "×",
            FontSize = 11,
            Padding = new Thickness(5, 0),
            MinHeight = 0,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ToolTip.SetTip(remove, "Remove: " + entry.Text);
        AutomationProperties.SetName(remove, "Remove: " + entry.Text);
        remove.Click += (_, _) => RemoveRelationshipFromList(entry);

        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Background = Brushes.Transparent };
        Grid.SetColumn(remove, 1);
        row.Children.Add(sentence);
        row.Children.Add(remove);
        row.PointerEntered += (_, _) =>
        {
            _rowUnderPointer = entry;
            UpdateAttention();
        };
        row.PointerExited += (_, _) =>
        {
            if (_rowUnderPointer?.Id == entry.Id)
            {
                _rowUnderPointer = null;
                UpdateAttention();
            }
        };

        return row;
    }

    /// <summary>Removes the relationship a row of the list says, as one undo step (#77).</summary>
    void RemoveRelationshipFromList(RelationshipEntry entry)
    {
        string what = "Removed: " + entry.Text;
        _rowUnderPointer = null;
        Editor.BeginGesture(what);
        Editor.Apply(new RemoveRelationship(entry.Id), what);
        Editor.EndGesture();
        FocusDrawing();
    }

    /// <summary>
    /// What both views outline in the problem colour: the parts the relationships the message
    /// highlights hold — a conflict (#72), a refused turn (#76) — and the parts the relationship row
    /// under the pointer holds (#77).
    /// </summary>
    void UpdateAttention()
    {
        Sketch sketch = Editor.Sketch;
        HashSet<EntityId> parts = [];
        if (_rowUnderPointer is { } row && sketch.Relationships.ContainsKey(row.Id))
        {
            parts.UnionWith(row.Entities);
        }

        if (Editor.LastMessage is { } message)
        {
            foreach (RelationshipId id in message.Highlight)
            {
                if (sketch.Relationships.TryGetValue(id, out Relationship? relationship))
                {
                    parts.UnionWith(relationship.References);
                }
            }
        }

        ImmutableHashSet<EntityId> attention = [.. parts];
        DrawingCanvas.Attention = attention;
        ModelDrawing.Attention = attention;
    }

    /// <summary>The parts both views are drawing attention to now.</summary>
    public IReadOnlySet<EntityId> AttentionOnScreen => IsShowingModel ? ModelDrawing.Attention : DrawingCanvas.Attention;

    /// <summary>
    /// Whether a part is one the relationship list should open for: selected, or resting under the
    /// pointer. Hovering is the quick look; selecting keeps it open while the pointer goes elsewhere.
    /// </summary>
    bool IsInPlay(EntityId id) =>
        Editor.Selection.Contains(id)
        || (IsShowingModel ? ModelDrawing.HoveredPart == id : DrawingCanvas.HoveredPart == id);

    void UpdateToolButtons()
    {
        if (IsShowingModel)
        {
            SelectToolButton.IsChecked = !ModelDrawing.Placement.IsArmed;
            RectangleToolButton.IsChecked = ModelDrawing.Placement.PlainBoard;
            WallToolButton.IsChecked = false;
            StockToolboxPanel.ShowArmed(ModelDrawing.Placement.Stock, inModel: true);
            return;
        }

        SelectToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Select;
        RectangleToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Rectangle;
        WallToolButton.IsChecked = DrawingCanvas.Tool == EditTool.Wall;
        StockToolboxPanel.ShowArmed(DrawingCanvas.ArmedStock);
    }

    void ShowRefusal(string what, IReadOnlyList<string> problems)
    {
        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        RefusalProblems = [.. problems];
        RefusalHeadline.Text = problems.Count == 1
            ? $"Could not open {what}:"
            : $"Could not open {what} ({problems.Count} problems):";

        // One line per problem, all of them. Showing the first and hiding the rest would make a
        // file look like it had one thing wrong with it when it had four.
        RefusalProblemList.Children.Clear();
        foreach (string problem in RefusalProblems)
        {
            RefusalProblemList.Children.Add(new TextBlock
            {
                Text = problem,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(palette.Label),
            });
        }

        RefusalPanel.IsVisible = true;
    }
}
