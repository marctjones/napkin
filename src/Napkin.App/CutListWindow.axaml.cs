using System.Collections.Immutable;

using Avalonia.Controls;

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
        ShowDesign(design: null);
    }

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
