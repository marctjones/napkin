using System.Collections.Immutable;

using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;

using Design = Napkin.Modules.Editing.Design;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
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

        // A row pressed selects its parts in the drawing (#205): the window has no editor, so it asks.
        Table.RowPicked += (_, pick) => SelectRow?.Invoke(pick);
        KerfNote.Text = CutList.BeforeKerfAndJoinery;
        KerfBox.Text = CutLayout.Inches(_kerf);
        SetKerfButton.Click += (_, _) => CommitKerf();
        KerfBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitKerf();
                e.Handled = true;
            }
        };
        ExtrasNote.Text = SuppliesList.Statement;
        SaveSizesButton.Click += (_, _) => SaveSizes();
        SaveSuppliesButton.Click += (_, _) => SaveSupplies();
        ShowDesign(design: null);
    }

    private Length _kerf = CutLayout.DefaultKerf;

    /// <summary>
    /// The saw kerf the lists are planned with: a per-person setting the owner keeps, a practice default
    /// until the person sets their blade's. Setting it re-plans the shopping list and the cut layout.
    /// </summary>
    public Length SawKerf
    {
        get => _kerf;
        set
        {
            _kerf = value;
            KerfBox.Text = CutLayout.Inches(value);
            KerfError.IsVisible = false;
            ShowDesign(Design);
        }
    }

    /// <summary>Told the new kerf when the person sets one, so the owner can keep it between runs.</summary>
    public Action<Length>? KerfChanged { get; set; }

    /// <summary>The saw kerf text box, for the GUI suite.</summary>
    public TextBox KerfField => KerfBox;

    /// <summary>The button beside the kerf box.</summary>
    public Button SetKerfControl => SetKerfButton;

    /// <summary>The refusal under the kerf box, empty when there is none.</summary>
    public string KerfMessage => KerfError.IsVisible ? KerfError.Text ?? string.Empty : string.Empty;

    /// <summary>The tab of the cut layout.</summary>
    public TabItem CutLayoutTabItem => CutLayoutTab;

    /// <summary>Whether the cut layout is the tab on show.</summary>
    public bool IsShowingCutLayout => ReferenceEquals(Lists.SelectedItem, CutLayoutTab);

    /// <summary>Shows the cut layout's tab.</summary>
    public void ShowCutLayout() => Lists.SelectedItem = CutLayoutTab;

    /// <summary>Shows the cut layout with the keyboard in the saw kerf field, to type a new one.</summary>
    public void EditKerf()
    {
        ShowCutLayout();
        KerfBox.Focus();
        KerfBox.SelectAll();
    }

    /// <summary>Whether the fastener sizes and supplies tab is the one showing.</summary>
    public bool IsShowingSizes => ReferenceEquals(Lists.SelectedItem, SizesTab);

    /// <summary>Shows the fastener sizes and supplies tab.</summary>
    public void ShowSizes() => Lists.SelectedItem = SizesTab;

    /// <summary>The parts' boards, for the GUI suite to read.</summary>
    public CutLayoutView LayoutRows => LayoutView;

    /// <summary>The walls' framing boards.</summary>
    public CutLayoutView FramingLayoutRows => FramingLayoutView;

    /// <summary>The summary lines under the parts' layout.</summary>
    public string LayoutSummaryText => LayoutSummary.Text ?? string.Empty;

    /// <summary>The cut layout of the parts as a CSV file would carry it, exactly the rows on screen.</summary>
    public string LayoutCsv => CutLayout.ToCsv(LayoutView.Rows, _kerf);

    /// <summary>The cut layout of the walls' framing as a CSV file would carry it.</summary>
    public string FramingLayoutCsv => CutLayout.ToCsv(FramingLayoutView.Rows, _kerf);

    /// <summary>Reads the kerf box: a length, zero allowed. A refusal says why and changes nothing.</summary>
    private void CommitKerf()
    {
        string text = KerfBox.Text ?? string.Empty;
        if (!Length.TryParse(text, out Length value, out _) || value < Length.Zero)
        {
            KerfError.Text = $"The saw kerf is a length like 1/8\" or 3/32\", or 0; \"{text.Trim()}\" is not.";
            KerfError.IsVisible = true;
            return;
        }

        KerfError.IsVisible = false;
        _kerf = value;
        KerfBox.Text = CutLayout.Inches(value);
        KerfChanged?.Invoke(value);
        ShowDesign(Design);
    }

    private readonly List<(FastenerKind Kind, Length? Thickness, TextBox Size, TextBox Pack)> _sizeEditors = [];
    private string _sizesBuiltFrom = string.Empty;
    private string _suppliesBuiltFrom = string.Empty;

    /// <summary>
    /// How this window changes the design (the fastener sizes and the supplies are the design's, not the
    /// window's): set by the owner to put a request through the editor, so undo covers it.
    /// </summary>
    public Action<Request, string>? ApplyRequest { get; set; }

    /// <summary>Selects a pressed row's parts in the drawing (#205); the main window's, which holds the editor.</summary>
    public Action<CutListRowPick>? SelectRow { get; set; }

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
                PlaceholderText = SuppliesList.SizeNotChosen,
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
                    SizesError.Text = SuppliesList.PackSizeRefusal(packText);
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

    /// <summary>The line under the cut list about rough rows, empty when there are none (sketch-mode §5).</summary>
    public string RoughNoteText => RoughNote.IsVisible ? RoughNote.Text ?? string.Empty : string.Empty;

    /// <summary>The line under the shopping list about rough parts with no stock, empty when there are none.</summary>
    public string ShoppingRoughNoteText => ShoppingRoughNote.IsVisible ? ShoppingRoughNote.Text ?? string.Empty : string.Empty;

    static void ShowNote(TextBlock note, string? text)
    {
        note.Text = text ?? string.Empty;
        note.IsVisible = text is not null;
    }

    /// <summary>The shopping list as a CSV file would carry it, in the order it is on screen.</summary>
    public string ShoppingCsv => ShoppingListCsv.ToCsv(ShoppingTable.Sorted, _kerf, ShoppingCost.Lines(ShoppingTable.Sorted, _prices));

    Dictionary<PriceKey, decimal> _prices = [];

    /// <summary>The prices the window prices with; set from the settings, changed by typing.</summary>
    public IReadOnlyDictionary<PriceKey, decimal> Prices
    {
        get => _prices;
        set
        {
            _prices = new Dictionary<PriceKey, decimal>(value);
            ShowPrices();
        }
    }

    /// <summary>Told when a price is typed, so the window's owner can remember it.</summary>
    public Action<IReadOnlyDictionary<PriceKey, decimal>>? PricesChanged { get; set; }

    /// <summary>The price boxes, one per line to buy, for the GUI suite.</summary>
    public IReadOnlyList<TextBox> PriceFields => [.. PriceRows.Children.OfType<Grid>().Select(row => row.Children.OfType<TextBox>().Single())];

    /// <summary>What the estimate line says.</summary>
    public string EstimateLine => EstimateText.Text ?? string.Empty;

    /// <summary>One row per line to buy, with its price box, and the estimate under them (#141).</summary>
    void ShowPrices()
    {
        ImmutableArray<CostLine> lines = ShoppingCost.Lines(ShoppingTable.Rows, _prices);
        PriceSection.IsVisible = !lines.IsEmpty;
        PriceRows.Children.Clear();
        foreach (CostLine line in lines)
        {
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,90"), ColumnSpacing = 6 };
            string unit = line.Unit switch { PriceUnit.Board => "board", PriceUnit.Sheet => "sheet", _ => "bd ft" };
            row.Children.Add(new TextBlock
            {
                Text = $"{line.What} — {line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)} × price per {unit}",
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });
            TextBox price = new()
            {
                FontSize = 11,
                Text = line.Price is { } each ? ShoppingCost.Money(each) : string.Empty,
                PlaceholderText = "price",
            };
            Avalonia.Automation.AutomationProperties.SetName(price, $"Price: {line.What}");
            Grid.SetColumn(price, 1);
            PriceKey key = line.Key;
            price.LostFocus += (_, _) => CommitPrice(key, price.Text);
            price.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    CommitPrice(key, price.Text);
                    e.Handled = true;
                }
            };
            row.Children.Add(price);
            PriceRows.Children.Add(row);
        }

        EstimateText.Text = ShoppingCost.Summary(lines) ?? string.Empty;
    }

    void CommitPrice(PriceKey key, string? text)
    {
        string typed = (text ?? string.Empty).Trim().TrimStart('$');
        bool changed;
        if (typed.Length == 0)
        {
            changed = _prices.Remove(key);
        }
        else if (decimal.TryParse(typed, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal amount) && amount >= 0)
        {
            changed = !_prices.TryGetValue(key, out decimal had) || had != amount;
            _prices[key] = amount;
        }
        else
        {
            EstimateText.Text = $"A price is a number like 4.98; \"{typed}\" is not.";
            return;
        }

        if (changed)
        {
            PricesChanged?.Invoke(_prices);
            ShowPrices();
        }
    }

    /// <summary>The New notes counted by symbol.</summary>
    public ImmutableArray<NoteCount> NoteCounts { get; private set; } = [];

    /// <summary>The Notes line: "outlet × 4, switch × 1, light × 1"; empty when there is no New note.</summary>
    public string NotesText => NotesSection.IsVisible ? NotesLine.Text ?? string.Empty : string.Empty;

    /// <summary>The Notes as a CSV file carries them, under their own header.</summary>
    public string NotesCsv => NotesList.ToCsv(NoteCounts);

    /// <summary>The Area takeoff section's lines, in the order on screen.</summary>
    public ImmutableArray<TakeoffLine> TakeoffLines { get; private set; } = [];

    /// <summary>Whether the Area takeoff section is showing (it is when a New room is drawn).</summary>
    public bool IsShowingAreaTakeoff => AreaTakeoffSection.IsVisible;

    /// <summary>The Area takeoff section as a CSV file carries it, under its own header.</summary>
    public string AreaTakeoffCsv => AreaTakeoff.ToCsv(TakeoffLines);

    /// <summary>The Demolition section's lines, in the order on screen.</summary>
    public ImmutableArray<DemolitionLine> DemolitionLines { get; private set; } = [];

    /// <summary>Whether the Demolition section is showing (it is when anything comes out).</summary>
    public bool IsShowingDemolition => DemolitionSection.IsVisible;

    /// <summary>The Demolition section as a CSV file carries it, under its own header.</summary>
    public string DemolitionCsv => Demolition.ToCsv(DemolitionLines);

    /// <summary>The line that says only New is listed; empty for a design with nothing existing or demolished.</summary>
    public string RenovationNoteText => RenovationNote.IsVisible ? RenovationNote.Text ?? string.Empty : string.Empty;

    /// <summary>The code packs the main window found, for the code check on the walls' openings (#18).</summary>
    public CodePacks Packs { get; set; } = CodePacks.None;

    /// <summary>The framing section's table: the walls' studs, plates and the rest, as boards to buy.</summary>
    public ShoppingListTable FramingRows => FramingTable;

    /// <summary>Whether the framing section is showing (it is when the design has a wall).</summary>
    public bool IsShowingFraming => FramingSection.IsVisible;

    /// <summary>The line over the framing section.</summary>
    public string FramingNoteText => FramingNote.Text ?? string.Empty;

    /// <summary>The framing section as a CSV file would carry it, in the order on screen.</summary>
    public string FramingCsv => ShoppingListCsv.ToCsv(FramingTable.Sorted, _kerf);

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
        ShoppingTable.Rows = ShoppingList.Of(rows, _kerf);
        ShowPrices();
        ShoppingNote.Text = ShoppingList.Statement(_kerf);
        ShowNote(RoughNote, CutList.RoughFooter(rows));
        ShowNote(ShoppingRoughNote, ShoppingList.RoughFooter(rows));
        LayoutNote.Text = CutLayout.Statement(_kerf);
        CutLayoutPlan layout = CutLayout.Of(rows, _kerf);
        LayoutView.Rows = CutLayout.Rows(layout);
        LayoutSummary.Text = string.Join("\n", CutLayout.Summary(layout));
        ExtrasGrid.Rows = SuppliesList.Of(sketch);

        // The walls' framing diff (renovation-sketches §6.3): new material is bought, what comes out
        // is counted under Demolition with the demolished boxes.
        ImmutableArray<WallDiff> diffs = FramingDiff.Of(sketch, MaterialsLibrary.Shipped, Packs);

        // The rooms' finishes by area (renovation-sketches §5).
        ImmutableArray<TakeoffLine> takeoff = AreaTakeoff.All(sketch, MaterialsLibrary.Shipped, Packs);
        TakeoffLines = takeoff;
        AreaTakeoffList.ItemsSource = takeoff.Select(line => (takeoff.Select(each => each.Room).Distinct().Count() > 1 ? $"{line.Room} — " : string.Empty) + line.Text).ToArray();
        AreaTakeoffSection.IsVisible = !takeoff.IsEmpty;

        // The New notes counted by symbol (renovation-sketches §6.2).
        NoteCounts = NotesList.Of(sketch);
        NotesLine.Text = NotesList.Line(NoteCounts) ?? string.Empty;
        NotesSection.IsVisible = !NoteCounts.IsEmpty;

        // What comes out, and the line that says only New is bought (renovation-sketches §6.2).
        ImmutableArray<DemolitionLine> demolition = [.. Demolition.Boxes(sketch), .. FramingDiff.Demolition(diffs)];
        DemolitionLines = demolition;
        DemolitionList.ItemsSource = demolition.Select(line => line.Text).ToArray();
        DemolitionSection.IsVisible = !demolition.IsEmpty;
        ShowNote(RenovationNote, Demolition.Header(sketch, demolition));

        // The walls' frame is bought through the very aggregation the parts are, from rows the
        // framing diff derives — a New wall's whole frame, an existing wall's new pieces only; kept
        // in a section of its own so a wall's studs read as a wall's. The checks run on the building
        // as it will be.
        ImmutableArray<OpeningCheck> checks = CodeCheck.Of(sketch, Packs);
        ImmutableArray<WallFraming> walls = FramingList.Of(sketch.After(), MaterialsLibrary.Shipped, CodeCheck.Framing(checks, MaterialsLibrary.Shipped));
        ImmutableArray<CutListRow> framingRows = FramingDiff.CutRows(diffs);
        FramingTable.Rows = ShoppingList.Of(framingRows, _kerf);
        CutLayoutPlan framingLayout = CutLayout.Of(framingRows, _kerf);
        FramingLayoutView.Rows = CutLayout.Rows(framingLayout);
        FramingLayoutSummary.Text = string.Join("\n", CutLayout.Summary(framingLayout));
        FramingLayoutSection.IsVisible = !walls.IsEmpty;
        FramingSection.IsVisible = !walls.IsEmpty;
        FramingNote.Text = walls.IsEmpty
            ? string.Empty
            : $"From {string.Join(", ", walls.Select(wall => $"{wall.Wall.Name} ({wall.Summary})"))}. "
              + string.Join(" ", walls.SelectMany(wall => wall.Notes).Distinct().Select(note => char.ToUpperInvariant(note[0]) + note[1..] + "."))
              + (walls.Any(wall => !wall.Problems.IsEmpty && !wall.Pieces.IsEmpty) ? " Some openings could not be framed; the drawing's panel says why." : string.Empty)
              + string.Concat(diffs.Where(diff => diff.FromExisting && diff.Changes).Select(diff => $" {diff.Wall.Name} is existing: only its new pieces are bought ({diff.Sentence}), {diff.Assumption}."))
              + CodeCheckNote(sketch, checks);
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
            : $"{design.Name}: {CutList.Headline(rows.Length, rows.Sum(row => row.Quantity))}";

        // An empty list is never silence: a design with nothing to cut says which of the two
        // reasons it is, because "no rows" and "no parts" are different problems to a person.
        EmptyNote.IsVisible = design is not null && rows.IsEmpty;
        EmptyText.Text = CutList.WhyEmpty(sketch);
    }

    /// <summary>The code check in one line per opening, with the code it is checked against (#18).</summary>
    private string CodeCheckNote(Sketch sketch, ImmutableArray<OpeningCheck> checks)
    {
        if (checks.IsEmpty)
        {
            return string.Empty;
        }

        CodeResolution code = Packs.Resolve(sketch.Code);
        string under = CodeCheck.UnderHeading(code.Pack?.Code);
        string results = code.Pack is null
            ? code.Problem ?? string.Empty
            : string.Join("; ", checks.Select(check => $"{check.Opening.Name}: {CodeCheck.Short(check)}")) + ".";
        return $"\n{under}: {results}";
    }
}
