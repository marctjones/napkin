using System.Collections.Immutable;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>
/// The fasteners, hardware and supplies below the boards on the shopping list: one table, each
/// cell the very string <see cref="SuppliesList.Fields"/> gives the export, so the table and the file
/// cannot disagree (joinery note &#xA7;7.4, &#xA7;7.5, &#xA7;8). Not sortable: the list is in the order it is
/// derived in.
/// </summary>
public sealed class ExtrasTable : Grid
{
    private static readonly string[] Headings = ["Section", "Item", "Size", "Count", "Pack", "Packs", "For"];

    private ImmutableArray<ExtraRow> _rows = [];

    /// <summary>A table with nothing in it yet.</summary>
    public ExtrasTable()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,*");
        Rebuild();
    }

    /// <summary>The rows this table shows, in the order <see cref="SuppliesList.Of"/> produced them.</summary>
    public ImmutableArray<ExtraRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            Rebuild();
        }
    }

    /// <summary>What the table reads, header line first and then one line per row, tab separated.</summary>
    public ImmutableArray<string> LinesOnScreen =>
    [
        .. Enumerable.Range(0, RowDefinitions.Count)
            .Select(row => string.Join(
                "\t",
                Children.Where(cell => GetRow(cell) == row).OrderBy(GetColumn).Select(cell => ((TextBlock)cell).Text ?? string.Empty))),
    ];

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();

        for (int line = 0; line <= _rows.Length; line++)
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            ImmutableArray<string> fields = line == 0 ? [.. Headings] : SuppliesList.Fields(_rows[line - 1]);
            for (int column = 0; column < fields.Length; column++)
            {
                TextBlock cell = new()
                {
                    Text = fields[column],
                    FontSize = 12,
                    Margin = new Thickness(8, line == 0 ? 5 : 3),
                    FontWeight = line == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                    TextAlignment = column is >= 3 and <= 5 ? TextAlignment.Right : TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = column == 6 ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    FontStyle = line > 0 && column == 2 && fields[column] == SuppliesList.SizeNotChosen ? FontStyle.Italic : FontStyle.Normal,
                };
                SetRow(cell, line);
                SetColumn(cell, column);
                Children.Add(cell);
            }
        }
    }
}
