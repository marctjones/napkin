using System.Collections.Immutable;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// Firm up (docs/design/sketch-mode.md &#xA7;3): F or Edit ▸ Firm up…, a sheet over the paper in the
/// join popover's shape. Every ticked line lands in one undo step, "Firm up"; the status bar then
/// says what was firmed and points at Join all touching.
/// </summary>
public partial class MainWindow
{
    /// <summary>One line of the popover: its tick, and what accepting it means.</summary>
    sealed record FirmUpLine(CheckBox Tick, TextBlock Text, FirmUpProposal? Relationship, FirmUpStockLine? Stock, ComboBox? Choice, FirmUpSizeLine? Size);

    readonly List<FirmUpLine> _firmUpLines = [];

    /// <summary>Whether the Firm up sheet is up.</summary>
    public bool IsFirmingUp => FirmUpPanel.IsVisible;

    /// <summary>The sheet, for the GUI suite to find a line on.</summary>
    public Border FirmUpSheet => FirmUpPanel;

    /// <summary>The ticks of the relationship lines, in the popover's order.</summary>
    public IReadOnlyList<CheckBox> FirmUpRelationshipTicks => [.. _firmUpLines.Where(line => line.Relationship is not null).Select(line => line.Tick)];

    /// <summary>The ticks of the stock lines.</summary>
    public IReadOnlyList<CheckBox> FirmUpStockTicks => [.. _firmUpLines.Where(line => line.Stock is not null).Select(line => line.Tick)];

    /// <summary>The ticks of the size lines.</summary>
    public IReadOnlyList<CheckBox> FirmUpSizeTicks => [.. _firmUpLines.Where(line => line.Size is not null).Select(line => line.Tick)];

    /// <summary>Every line's words, in the popover's order.</summary>
    public IReadOnlyList<string> FirmUpLineTexts => [.. _firmUpLines.Select(line => line.Text.Text ?? string.Empty)];

    /// <summary>The popover's message line: what the updater refused, if anything.</summary>
    public string FirmUpMessage => FirmUpMessageText.IsVisible ? FirmUpMessageText.Text ?? string.Empty : string.Empty;

    void WireFirmUp()
    {
        FirmUpOkButton.Click += (_, _) => ConfirmFirmUp();
        FirmUpCancelButton.Click += (_, _) => CancelFirmUp();
        AddHandler(KeyDownEvent, OnFirmUpKeyDown, RoutingStrategies.Tunnel);
    }

    void OnFirmUpClicked(object? sender, RoutedEventArgs e) => Run(EditCommand.FirmUp);

    void OnFirmUpKeyDown(object? sender, KeyEventArgs e)
    {
        if (!FirmUpPanel.IsVisible || e.Handled)
        {
            return;
        }

        bool listOpen = _firmUpLines.Any(line => line.Choice?.IsDropDownOpen == true);
        if (e.Key == Key.Escape && !listOpen)
        {
            CancelFirmUp();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && !listOpen && e.Source is not Button { Name: "FirmUpCancelButton" })
        {
            ConfirmFirmUp();
            e.Handled = true;
        }
    }

    /// <summary>Opens the sheet over the selection, or every part when nothing is selected.</summary>
    void BeginFirmUp()
    {
        if (IsShapingPart || IsAskingToSave || IsJoining)
        {
            return;
        }

        ImmutableArray<EntityId> scope = FirmUp.Scope(Editor.Sketch, Editor.Selection);
        FirmUpPlan plan = FirmUp.Plan(Editor.Sketch, scope, MaterialsLibrary.Shipped, Editor.NameOf, Editor.LabelFormat);
        if (plan.IsEmpty)
        {
            Editor.Say(
                EditSeverity.Hint,
                scope.IsEmpty
                    ? "Nothing to firm up: draw some parts first."
                    : "Nothing to firm up: no parts touch that are not already held, and none is rough.");
            return;
        }

        _firmUpLines.Clear();
        FirmUpLines.Children.Clear();
        FirmUpTitle.Text = Editor.Selection.Count == 0
            ? $"Firm up every part ({scope.Length})"
            : $"Firm up the {scope.Length} selected part{(scope.Length == 1 ? string.Empty : "s")}";

        Section("Relationships", plan.Relationships.Select(proposal => Line(proposal.Sentence, ticked: true, relationship: proposal)));
        Section("Stock", plan.Stocks.Select(stock => Line(stock.Sentence, ticked: !stock.Candidates.IsEmpty, stock: stock)));
        Section("Sizes", plan.Sizes.Select(size => Line(size.Sentence, ticked: true, size: size)));

        FirmUpMessageText.IsVisible = false;
        FirmUpPanel.IsVisible = true;
        FirmUpOkButton.Focus();
    }

    /// <summary>A section: its header tick, which ticks or unticks every line under it, then its lines.</summary>
    void Section(string title, IEnumerable<FirmUpLine> lines)
    {
        FirmUpLine[] all = [.. lines];
        if (all.Length == 0)
        {
            return;
        }

        CheckBox header = new()
        {
            Content = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            IsChecked = all.Any(line => line.Tick.IsChecked == true),
            Margin = new Thickness(0, 4, 0, 0),
        };
        AutomationProperties.SetName(header, $"All {title.ToLowerInvariant()}");
        header.IsCheckedChanged += (_, _) =>
        {
            foreach (FirmUpLine line in all)
            {
                line.Tick.IsChecked = header.IsChecked == true && (line.Stock is null || ChosenStock(line.Stock, line.Choice!) is not null);
            }
        };
        FirmUpLines.Children.Add(header);

        foreach (FirmUpLine line in all)
        {
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(14, 0, 0, 0) };
            Grid.SetColumn(line.Tick, 0);
            Grid.SetColumn(line.Text, 1);
            row.Children.Add(line.Tick);
            row.Children.Add(line.Text);
            if (line.Choice is { } choice)
            {
                Grid.SetColumn(choice, 2);
                row.Children.Add(choice);
            }

            FirmUpLines.Children.Add(row);
            _firmUpLines.Add(line);
        }
    }

    FirmUpLine Line(string sentence, bool ticked, FirmUpProposal? relationship = null, FirmUpStockLine? stock = null, FirmUpSizeLine? size = null)
    {
        CheckBox tick = new() { IsChecked = ticked, MinWidth = 0, Padding = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(tick, sentence);
        TextBlock text = new()
        {
            Text = sentence,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 6, 0),
        };

        ComboBox? choice = null;
        if (stock is not null)
        {
            // The nearest three, best first, and "none" always available: a suggestion to accept, never a guess.
            List<string> items = [.. stock.Candidates.Select(item => item.Name), "none"];
            choice = new ComboBox { ItemsSource = items, SelectedIndex = 0, FontSize = 12, Width = 120, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(choice, $"Stock for {sentence}");
            choice.SelectionChanged += (_, _) => tick.IsChecked = ChosenStock(stock, choice) is not null;
            if (stock.Candidates.IsEmpty)
            {
                tick.IsEnabled = false;
            }
        }

        // A click on a relationship's words selects its two parts, so the person can see which pair it is.
        if (relationship is not null)
        {
            text.Cursor = new Cursor(StandardCursorType.Hand);
            text.PointerPressed += (_, e) =>
            {
                Editor.SelectAll(relationship.Relationship.References);
                e.Handled = true;
            };
        }

        return new FirmUpLine(tick, text, relationship, stock, choice, size);
    }

    /// <summary>The stock a line's drop-down has chosen, or null for "none".</summary>
    static StockItem? ChosenStock(FirmUpStockLine line, ComboBox choice)
        => choice.SelectedIndex >= 0 && choice.SelectedIndex < line.Candidates.Length ? line.Candidates[choice.SelectedIndex] : null;

    void CancelFirmUp()
    {
        FirmUpPanel.IsVisible = false;
        _firmUpLines.Clear();
        FocusDrawing();
    }

    /// <summary>Accepts the ticked lines: one gesture, one undo step (&#xA7;3, &#xA7;6.2).</summary>
    void ConfirmFirmUp()
    {
        FirmUpLine[] ticked = [.. _firmUpLines.Where(line => line.Tick.IsChecked == true)];
        FirmUpOutcome outcome = FirmUp.Accept(
            Editor,
            ticked.Select(line => line.Relationship).OfType<FirmUpProposal>(),
            ticked.Where(line => line.Stock is not null && ChosenStock(line.Stock, line.Choice!) is not null)
                .Select(line => (line.Stock!.Part, ChosenStock(line.Stock!, line.Choice!)!)),
            ticked.Select(line => line.Size?.Part).OfType<EntityId>());

        if (!outcome.Rejections.IsEmpty)
        {
            // The rest landed; the sheet stays up long enough to say what did not, in the updater's words.
            FirmUpMessageText.Text = string.Join("\n", outcome.Rejections);
            FirmUpMessageText.Foreground = new SolidColorBrush(CanvasPalette.For(ActualThemeVariant).Selection);
            FirmUpMessageText.IsVisible = true;
            foreach (FirmUpLine line in _firmUpLines)
            {
                line.Tick.IsChecked = false;
            }

            return;
        }

        CancelFirmUp();
    }
}
