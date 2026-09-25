using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;

using Napkin.App.Designs;
using Design = Napkin.App.Designs.Design;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.App;

/// <summary>
/// The cut list for the design on screen: what to cut, how many, how big, and out of what.
/// </summary>
/// <remarks>
/// <para>
/// A window of its own rather than a panel over the drawing, because it is read while working
/// <em>from</em> the drawing — beside it, or on a second screen — and because it wants the room.
/// It is not modal: the design can go on being edited with the list open, and
/// <see cref="ShowDesign"/> re-reads it whenever it changes.
/// </para>
/// <para>
/// Nothing here computes anything. <see cref="CutList.Of"/> produces the rows and
/// <see cref="CutListTable"/> draws them, so what is on screen is the list, not a second reading
/// of the design that could disagree with it.
/// </para>
/// </remarks>
public partial class CutListWindow : Window
{
    /// <summary>An empty cut-list window, for the designer and for a test.</summary>
    public CutListWindow()
    {
        InitializeComponent();
        KerfNote.Text = CutList.BeforeKerfAndJoinery;
        ShoppingNote.Text = ShoppingList.BeforeKerfAndJoinery;
        ExtrasNote.Text = SuppliesList.Statement;
        SaveSizesButton.Click += (_, _) => SaveSizes();
        SaveSuppliesButton.Click += (_, _) => SaveSupplies();
        ShowDesign(design: null);
    }

    private readonly List<(FastenerKind Kind, Length? Thickness, TextBox Size, TextBox Pack)> _sizeEditors = [];
    private string _sizesBuiltFrom = string.Empty;
    private string _suppliesBuiltFrom = string.Empty;

    /// <summary>
    /// How this window changes the design (the fastener sizes and the supplies are the design's, not the
    /// window's): set by the owner to put a request through the editor, so undo covers it.
    /// </summary>
    public Action<Request, string>? ApplyRequest { get; set; }

    /// <summary>The fasteners, hardware and supplies below the boards, for the GUI suite to read.</summary>
    public ExtrasTable Extras => ExtrasGrid;

    /// <summary>The fasteners, hardware and supplies as a CSV file would carry them, in the order on screen.</summary>
    public string ExtrasCsv => SuppliesList.ToCsv(ExtrasGrid.Rows);

    /// <summary>The tab of fastener sizes and supplies, for the GUI suite to click.</summary>
    public TabItem SizesTabItem => SizesTab;

    /// <summary>The stack of size editors, one row per kind and thickness the joints need.</summary>
    public StackPanel SizeEditorRows => SizeRows;

    /// <summary>The button that keeps the typed sizes.</summary>
    public Button SaveSizesControl => SaveSizesButton;

    /// <summary>The message under the sizes, empty when there is none.</summary>
    public string SizesMessage => SizesError.IsVisible ? SizesError.Text ?? string.Empty : string.Empty;

    /// <summary>The supplies text box.</summary>
    public TextBox SuppliesField => SuppliesBox;

    /// <summary>The button that keeps the typed supplies.</summary>
    public Button SaveSuppliesControl => SaveSuppliesButton;

    /// <summary>
    /// A button beside a brad's or nail's size that lists the sizes the shipped, cited tables carry (FF-N-105B). Picking
    /// one only fills the size box: nothing is saved, and napkin never chooses a size for the builder.
    /// </summary>
    private static Button? SuggestionButton(FastenerKind kind, TextBox size, string what)
    {
        string? family = kind switch
        {
            FastenerKind.Brad => "brad",
            FastenerKind.Nail => "nail",
            _ => null,
        };
        FastenerStock[] listed = family is null ? [] : [.. MaterialsLibrary.Shipped.Items.OfType<FastenerStock>().Where(stock => stock.Family == family)];
        if (listed.Length == 0)
        {
            return null;
        }

        ListBox choices = new() { MaxHeight = 240, ItemsSource = listed.Select(stock => stock.Name).ToArray() };
        Flyout flyout = new() { Content = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = $"Sizes in {listed[0].Source.Designation} (pick one to fill the box; then save)", FontSize = 11 }, choices } } };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedItem is string name)
            {
                size.Text = name;
                flyout.Hide();
            }
        };
        Button button = new() { Content = "Cited sizes\u2026", FontSize = 11, Flyout = flyout };
        AutomationProperties.SetName(button, $"Cited sizes for {what}");
        return button;
    }

    /// <summary>The size box of the n-th editor row, blank ones first.</summary>
    /// <param name="index">The row, from zero.</param>
    public TextBox SizeBox(int index) => _sizeEditors[index].Size;

    /// <summary>The pack box of the n-th editor row.</summary>
    /// <param name="index">The row, from zero.</param>
    public TextBox PackBox(int index) => _sizeEditors[index].Pack;

    private void BuildSizes(Sketch sketch)
    {
        ImmutableArray<FastenerRow> needed =
        [
            .. FastenerList.Of(sketch).OrderBy(row => row.SizeText.Length == 0 ? 0 : 1),
        ];
        string signature = string.Join("|", needed.Select(row => $"{row.Kind}/{row.Thickness?.Units}/{row.SizeText}/{row.PackSize}/{row.Count}"));
        if (signature == _sizesBuiltFrom)
        {
            return;
        }

        _sizesBuiltFrom = signature;
        _sizeEditors.Clear();
        SizeRows.Children.Clear();
        SizesError.IsVisible = false;

        if (needed.IsEmpty)
        {
            SizeRows.Children.Add(new TextBlock
            {
                Text = "No joint needs a fastener yet.",
                FontSize = 12,
                Opacity = 0.75,
            });
        }

        foreach (FastenerRow row in needed)
        {
            string what = row.Thickness is { } thickness
                ? $"{SuppliesList.KindName(row.Kind)}, {CutListCsv.Text(thickness)} stock"
                : SuppliesList.KindName(row.Kind);
            TextBox size = new()
            {
                Text = row.SizeText,
                Width = 220,
                FontSize = 12,
                PlaceholderText = "size not chosen",
            };
            TextBox pack = new()
            {
                Text = row.PackSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Width = 70,
                FontSize = 12,
                PlaceholderText = "pack",
            };
            AutomationProperties.SetName(size, $"Size of {what}");
            AutomationProperties.SetName(pack, $"Pack size of {what}");
            size.KeyDown += (_, e) => EnterSaves(e);
            pack.KeyDown += (_, e) => EnterSaves(e);
            _sizeEditors.Add((row.Kind, row.Thickness, size, pack));

            StackPanel line = new() { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(new TextBlock
            {
                Text = $"{what} ({row.Count} needed)",
                Width = 260,
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });
            line.Children.Add(size);
            line.Children.Add(pack);
            if (SuggestionButton(row.Kind, size, what) is { } suggest)
            {
                line.Children.Add(suggest);
            }

            SizeRows.Children.Add(line);
        }
    }

    private void EnterSaves(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveSizes();
        }
    }

    private void SaveSizes()
    {
        List<FastenerChoice> choices = [];
        foreach ((FastenerKind kind, Length? thickness, TextBox size, TextBox pack) in _sizeEditors)
        {
            string packText = (pack.Text ?? string.Empty).Trim();
            int? packSize = null;
            if (packText.Length > 0)
            {
                if (!int.TryParse(packText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsed) || parsed < 1)
                {
                    SizesError.Text = $"A pack size is a whole number of at least 1, or blank; \"{packText}\" is not.";
                    SizesError.IsVisible = true;
                    return;
                }

                packSize = parsed;
            }

            string sizeText = (size.Text ?? string.Empty).Trim();
            if (sizeText.Length > 0 || packSize is not null)
            {
                choices.Add(new FastenerChoice(kind, thickness, sizeText, packSize));
            }
        }

        // A choice for something the joints no longer need is left as it was; only the rows shown are edited.
        IEnumerable<FastenerChoice> kept = (Design?.Sketch ?? Sketch.Empty).FastenerChoices
            .Where(choice => !_sizeEditors.Any(editor => editor.Kind == choice.Kind && editor.Thickness == choice.Thickness));
        SizesError.IsVisible = false;
        ApplyRequest?.Invoke(new SetFastenerChoices([.. kept, .. choices]), "set fastener sizes");
    }

    private void SaveSupplies()
    {
        List<SupplyLine> lines = [];
        foreach (string raw in (SuppliesBox.Text ?? string.Empty).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            int bar = line.IndexOf('|', StringComparison.Ordinal);
            string item = (bar < 0 ? line : line[..bar]).Trim();
            if (item.Length > 0)
            {
                lines.Add(new SupplyLine(item, bar < 0 ? string.Empty : line[(bar + 1)..].Trim()));
            }
        }

        ApplyRequest?.Invoke(new SetSupplies([.. lines]), "set supplies");
    }

    private static string SuppliesText(Sketch sketch)
        => string.Join("\n", sketch.Supplies.Select(line => line.Note.Length == 0 ? line.Item : $"{line.Item} | {line.Note}"));

    /// <summary>The table, for the GUI suite to read and to click a header on.</summary>
    public CutListTable Rows => Table;

    /// <summary>The design this list was last built from, or <see langword="null"/>.</summary>
    public Design? Design { get; private set; }

    /// <summary>
    /// What the list says when there is nothing on it, or empty when there is something.
    /// </summary>
    public string EmptyMessage => EmptyNote.IsVisible ? EmptyText.Text ?? string.Empty : string.Empty;

    /// <summary>The line naming the design the list is of.</summary>
    public string Headline => DesignHeadline.Text ?? string.Empty;

    /// <summary>The cut list as a CSV file would carry it, in the order it is on screen.</summary>
    public string Csv => CutListCsv.ToCsv(Table.Sorted);

    /// <summary>The shopping-list table, for the GUI suite to read and to click a header on.</summary>
    public ShoppingListTable ShoppingRows => ShoppingTable;

    /// <summary>The shopping list as a CSV file would carry it, in the order it is on screen.</summary>
    public string ShoppingCsv => ShoppingListCsv.ToCsv(ShoppingTable.Sorted);

    /// <summary>The tab that shows the shopping list, for the GUI suite to click.</summary>
    public TabItem ShoppingListTabItem => ShoppingListTab;

    /// <summary>Whether the shopping list, rather than the cut list, is the tab on show.</summary>
    public bool IsShowingShoppingList => ReferenceEquals(Lists.SelectedItem, ShoppingListTab);

    /// <summary>Shows the shopping list's tab.</summary>
    public void ShowShoppingList() => Lists.SelectedItem = ShoppingListTab;

    /// <summary>
    /// Builds the list for a design, or empties it when there is none.
    /// </summary>
    /// <param name="design">The design on screen.</param>
    public void ShowDesign(Design? design)
    {
        Design = design;

        Sketch sketch = design?.Sketch ?? Sketch.Empty;
        ImmutableArray<CutListRow> rows = CutList.Of(sketch, MaterialsLibrary.Shipped);

        Table.Rows = rows;

        // The shopping list is read from the cut list's rows, never from the design a second time,
        // so the two tabs cannot disagree about what is being built (§4).
        ShoppingTable.Rows = ShoppingList.Of(rows);
        ExtrasGrid.Rows = SuppliesList.Of(sketch);
        BuildSizes(sketch);
        string supplies = SuppliesText(sketch);
        if (supplies != _suppliesBuiltFrom)
        {
            _suppliesBuiltFrom = supplies;
            SuppliesBox.Text = supplies;
        }

        Title = design is null ? "Cut list" : $"Cut list — {design.Name}";
        DesignHeadline.Text = design is null
            ? "No design is open."
            : $"{design.Name}: {Describe(rows)}";

        // An empty list is never silence: a design with nothing to cut says which of the two
        // reasons it is, because "no rows" and "no parts" are different problems to a person.
        bool anyBoxes = sketch.Entities.Values.OfType<Box>().Any();
        EmptyNote.IsVisible = design is not null && rows.IsEmpty;
        EmptyText.Text = anyBoxes
            ? "Nothing in this design is a part yet. A box becomes a part when it is given a "
              + "thickness and told which of its three dimensions the drawing is showing; a wall "
              + "and an opening are boxes nobody cuts, and they stay off this list."
            : "This design has nothing in it to cut.";
    }

    private static string Describe(ImmutableArray<CutListRow> rows)
    {
        if (rows.IsEmpty)
        {
            return "nothing to cut";
        }

        int pieces = rows.Sum(row => row.Quantity);
        string rowWord = rows.Length == 1 ? "row" : "rows";
        string pieceWord = pieces == 1 ? "piece" : "pieces";

        return $"{rows.Length} {rowWord}, {pieces} {pieceWord} to cut";
    }
}
