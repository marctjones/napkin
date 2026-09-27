using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.Interop.Pdf;

/// <summary>
/// One label for one piece, to stick on it as it comes off the saw (#211): the part as the cut list
/// and the Parts view name it, which of its row's pieces this is, its finished size in the cut list's
/// own text, and where it is cut from.
/// </summary>
/// <param name="Part">The cut-list row's label.</param>
/// <param name="Copy">"2 of 4": which of the row's pieces.</param>
/// <param name="Size">Length × width × thickness, as the cut list writes them.</param>
/// <param name="Material">The row's material, as the cut list writes it; empty with none chosen.</param>
/// <param name="From">"Board 2: 2x4 × 8 ft", "Sheet 1: 3/4 plywood", or why it is on neither.</param>
public sealed record ShopLabel(string Part, string Copy, string Size, string Material, string From);

/// <summary>
/// The shop set (#211): what a person takes to the saw — the cut list, the cut layout's boards and
/// sheets drawn, and a label per piece — all read from the one list of rows the cut-list window
/// shows (<see cref="CutList.Of"/>) and the one layout it draws (<see cref="CutLayout.Of"/>), so
/// paper and screen cannot disagree about a number, a name or a board.
/// </summary>
/// <param name="Title">What every page's title block says.</param>
/// <param name="Format">How a scale is said in words.</param>
/// <param name="Rows">The cut list's rows, in its order.</param>
/// <param name="Layout">The cut layout planned from them.</param>
/// <param name="Labels">One label per piece, in the cut list's order, each row's pieces together.</param>
/// <param name="Empty">Why there is nothing to cut, when there is not; otherwise null.</param>
public sealed record ShopSet(
    TitleBlock Title,
    LengthFormat Format,
    ImmutableArray<CutListRow> Rows,
    CutLayoutPlan Layout,
    ImmutableArray<ShopLabel> Labels,
    string? Empty)
{
    /// <summary>What a label says of a piece no board or sheet was laid out for.</summary>
    public const string NotLaidOut = "not in the cut layout (see its notes)";

    /// <summary>The shop set of a design.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="library">The materials library its stock is read from.</param>
    /// <param name="kerf">The saw kerf the cut layout is planned with: the cut-list window's.</param>
    /// <param name="title">What every page's title block says.</param>
    /// <param name="format">How a scale is said in words.</param>
    public static ShopSet Of(Sketch sketch, MaterialsLibrary library, Length kerf, TitleBlock title, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(format);
        ImmutableArray<CutListRow> rows = CutList.Of(sketch, library);
        CutLayoutPlan layout = CutLayout.Of(rows, kerf);
        return new ShopSet(title, format, rows, layout, LabelsOf(rows, layout), rows.IsEmpty ? CutList.WhyEmpty(sketch) : null);
    }

    /// <summary>The finished size a label prints: the cut list's three texts, length first.</summary>
    /// <param name="row">The row.</param>
    public static string SizeOf(CutListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return $"{row.LengthText} × {row.WidthText} × {row.ThicknessText}";
    }

    /// <summary>
    /// One label per piece: each row's pieces are matched, in the cut list's order, to the boards
    /// and sheets the layout cut them from — the same stock and species, the same label, the same
    /// length (and width, on a sheet, either way round) — in the order the layout placed them.
    /// A piece the layout refused, or never laid out, says so.
    /// </summary>
    /// <param name="rows">The cut list's rows.</param>
    /// <param name="layout">The layout planned from them.</param>
    public static ImmutableArray<ShopLabel> LabelsOf(IReadOnlyList<CutListRow> rows, CutLayoutPlan layout)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layout);
        List<string>[] from = [.. rows.Select(_ => new List<string>())];

        void Claim(Func<CutListRow, bool> matches, string where)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (from[i].Count < rows[i].Quantity && matches(rows[i]))
                {
                    from[i].Add(where);
                    return;
                }
            }
        }

        bool Same(CutListRow row, StockItem stock, string species) =>
            row.Stock is { } item && item.Key == stock.Key && row.Species == species;

        foreach (StockLayout stock in layout.Stocks)
        {
            string name = stock.Species.Length == 0 ? stock.Stock.Name : $"{stock.Stock.Name} ({stock.Species})";
            foreach (PlannedBoard board in stock.Boards)
            {
                foreach (PlacedPiece piece in board.Pieces)
                {
                    Claim(
                        row => Same(row, stock.Stock, stock.Species) && row.Label == piece.Label && row.Length == piece.Length,
                        $"Board {board.Number.ToString(CultureInfo.InvariantCulture)}: {name} × {CutLayout.Feet(board.StockLength)}");
                }
            }

            foreach (PlacedPiece piece in stock.TooLong)
            {
                Claim(
                    row => Same(row, stock.Stock, stock.Species) && row.Label == piece.Label && row.Length == piece.Length,
                    $"no board: {ShoppingList.NothingHolds} it");
            }
        }

        foreach (PanelLayout panel in layout.Panels)
        {
            string name = panel.Species.Length == 0 ? panel.Panel.Name : $"{panel.Panel.Name} ({panel.Species})";
            bool Fits(CutListRow row, Length along, Length across) =>
                (row.Length == along && row.Width == across) || (row.Length == across && row.Width == along);
            foreach (PlannedSheet sheet in panel.Sheets)
            {
                foreach (SheetPiece piece in sheet.Pieces)
                {
                    Claim(
                        row => Same(row, panel.Panel, panel.Species) && row.Label == piece.Label && Fits(row, piece.Along, piece.Across),
                        $"Sheet {sheet.Number.ToString(CultureInfo.InvariantCulture)}: {name}");
                }
            }

            foreach (RefusedSheetPiece piece in panel.Refused)
            {
                Claim(
                    row => Same(row, panel.Panel, panel.Species) && row.Label == piece.Label && Fits(row, piece.Length, piece.Width),
                    piece.AgainstGrain ? "no sheet: it fits only turned, against its grain" : $"no sheet: {ShoppingList.NothingHolds} it");
            }
        }

        List<ShopLabel> labels = [];
        for (int i = 0; i < rows.Count; i++)
        {
            CutListRow row = rows[i];
            for (int copy = 0; copy < row.Quantity; copy++)
            {
                labels.Add(new ShopLabel(
                    row.Label,
                    $"{(copy + 1).ToString(CultureInfo.InvariantCulture)} of {row.Quantity.ToString(CultureInfo.InvariantCulture)}",
                    SizeOf(row),
                    row.MaterialText,
                    copy < from[i].Count ? from[i][copy] : NotLaidOut));
            }
        }

        return [.. labels];
    }
}
