using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>Which column a shopping list is sorted by.</summary>
public enum ShoppingListColumn
{
    /// <summary>The stock's name — the order the list comes in.</summary>
    Material,

    /// <summary>Board feet bought.</summary>
    Bought,

    /// <summary>Board feet of the parts themselves.</summary>
    Used,

    /// <summary>Board feet bought and not used.</summary>
    Waste,
}

/// <summary>
/// The shopping list on screen: the stock to buy, beside the cut list but plainly a different list
/// (issue #9, "Output format").
/// </summary>
/// <remarks>
/// <para>
/// <strong>It renders rows; it does not compute them</strong>, the way <see cref="CutListTable"/>
/// does not. <see cref="ShoppingList.Of"/> produces the rows, this draws each cell with the very
/// string <see cref="ShoppingListCsv.Fields"/> gives the export, so the table and the file cannot
/// disagree.
/// </para>
/// <para>
/// Sorting reorders what is shown and never changes a number, and <see cref="Sorted"/> is the order
/// on screen, which is what an export writes.
/// </para>
/// </remarks>
public sealed class ShoppingListTable : Grid
{
    private static readonly string[] Headings =
        ["Material", "Species", "Buy", "For", "Bd ft bought", "Bd ft used", "Waste", "Note"];

    private ImmutableArray<ShoppingListRow> _rows = [];
    private ShoppingListColumn _sortBy = ShoppingListColumn.Material;
    private bool _descending;

    /// <summary>A table with nothing in it yet.</summary>
    public ShoppingListTable()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,*");
        Rebuild();
    }

    /// <summary>The rows this table shows, in the order <see cref="ShoppingList.Of"/> produced them.</summary>
    public ImmutableArray<ShoppingListRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            Rebuild();
        }
    }

    /// <summary>The column the table is sorted by.</summary>
    public ShoppingListColumn SortBy => _sortBy;

    /// <summary>Whether the sort runs largest or last first.</summary>
    public bool IsDescending => _descending;

    /// <summary>The rows in the order they are on screen, which is what an export writes.</summary>
    public ImmutableArray<ShoppingListRow> Sorted => [.. Order(_rows)];

    /// <summary>What the table reads, header line first and then one line per row, tab separated.</summary>
    public ImmutableArray<string> LinesOnScreen =>
    [
        .. Enumerable.Range(0, RowDefinitions.Count)
            .Select(row => string.Join(
                "\t",
                Children
                    .Where(cell => GetRow(cell) == row)
                    .OrderBy(GetColumn)
                    .Select(TextOf))),
    ];

    /// <summary>
    /// Sorts by a column, turning the direction around when it is the column already sorted by.
    /// </summary>
    /// <param name="column">The column to sort by.</param>
    public void SortByColumn(ShoppingListColumn column)
    {
        if (_sortBy == column)
        {
            _descending = !_descending;
        }
        else
        {
            // Board feet read largest first; names read A to Z.
            _sortBy = column;
            _descending = column != ShoppingListColumn.Material;
        }

        Rebuild();
    }

    private static string TextOf(Control cell) => cell switch
    {
        TextBlock text => text.Text ?? string.Empty,
        ContentControl { Content: TextBlock caption } => caption.Text ?? string.Empty,
        _ => string.Empty,
    };

    private IEnumerable<ShoppingListRow> Order(IEnumerable<ShoppingListRow> rows)
    {
        // By material, the list's own order is kept rather than re-sorted: rows with nothing to buy
        // stay after the stock, where ShoppingList.Of put them.
        return _sortBy switch
        {
            ShoppingListColumn.Bought => By(row => row.BoughtCubicUnits),
            ShoppingListColumn.Used => By(row => row.UsedCubicUnits),
            ShoppingListColumn.Waste => By(row => row.BoughtCubicUnits - row.UsedCubicUnits),
            _ => _descending ? rows.Reverse() : rows,
        };

        IEnumerable<ShoppingListRow> By(Func<ShoppingListRow, Int128> key)
            => _descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
    }

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int column = 0; column < Headings.Length; column++)
        {
            Add(Header(column), 0, column);
        }

        int line = 1;
        foreach (ShoppingListRow row in Order(_rows))
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            ImmutableArray<string> fields = ShoppingListCsv.Fields(row);
            for (int column = 0; column < fields.Length; column++)
            {
                TextBlock cell = Cell(fields[column], right: column is >= 4 and <= 6);
                if (column == 7)
                {
                    // The note is where a refusal is said; it wraps rather than widening the table.
                    cell.TextWrapping = TextWrapping.Wrap;
                    cell.FontStyle = row.Note.Length > 0 ? FontStyle.Italic : FontStyle.Normal;
                }

                Add(cell, line, column);
            }

            line++;
        }
    }

    private Control Header(int column)
    {
        string text = Headings[column];
        ShoppingListColumn? sortable = column switch
        {
            0 => ShoppingListColumn.Material,
            4 => ShoppingListColumn.Bought,
            5 => ShoppingListColumn.Used,
            6 => ShoppingListColumn.Waste,
            _ => null,
        };
        bool right = column is >= 4 and <= 6;

        TextBlock caption = new()
        {
            // The arrow is part of the text so "what is it sorted by?" is answerable from the screen.
            Text = sortable == _sortBy ? $"{text} {(_descending ? "▼" : "▲")}" : text,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
        };

        if (sortable is not { } by)
        {
            caption.Margin = new Thickness(8, 5);
            caption.VerticalAlignment = VerticalAlignment.Center;
            return caption;
        }

        Button header = new()
        {
            Content = caption,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };

        header.Click += (_, _) => SortByColumn(by);
        return header;
    }

    private static TextBlock Cell(string text, bool right = false) => new()
    {
        Text = text,
        FontSize = 12,
        Margin = new Thickness(8, 3),
        TextAlignment = right ? TextAlignment.Right : TextAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void Add(Control control, int row, int column)
    {
        SetRow(control, row);
        SetColumn(control, column);
        Children.Add(control);
    }
}
