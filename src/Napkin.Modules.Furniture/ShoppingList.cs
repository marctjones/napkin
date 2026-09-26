using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// The shopping list: the cut list aggregated into the stock to buy, with its board-foot takeoff
/// (<c>docs/design/parts-and-cut-list.md</c> §4, issue #9).
/// </summary>
/// <remarks>
/// <para>
/// <strong>It consumes the cut list's rows, not the design</strong>, so the two lists can never
/// disagree about what is being built. The rows already carry the stock item each resolved to, so
/// the materials library is not read a second time — a second lookup could only disagree with the
/// first.
/// </para>
/// <para>
/// A pure function. Nothing is guessed: where the library carries no stock-length list, where no
/// stocked length holds a piece, and where a sheet count can only be a floor, the row says so
/// instead of inventing an answer (§4 step 2's three honest refusals).
/// </para>
/// </remarks>
public static class ShoppingList
{
    /// <summary>
    /// What a shopping list includes, said on the table and in the exported file (§1.3, §4 "Saw kerf"):
    /// the kerf it was planned with, and what it is still before.
    /// </summary>
    /// <param name="kerf">The kerf used.</param>
    public static string Statement(Length kerf)
        => $"Shopping list: boards needed from finished sizes, joinery allowances included; includes a {CutLayout.Inches(kerf)} saw kerf per cut "
           + "(set it in the Saw kerf box on the Cut layout tab); before defect.";

    /// <summary>The note on a sheet count: the sheets the cut layout nests the parts on (#26).</summary>
    public const string SheetsByLayout = SheetLayout.SheetsByLayout;

    /// <summary>The note on lumber the library has no stock-length list for.</summary>
    public const string NoLengthList = "napkin has read no stock-length list for this size";

    /// <summary>The start of the note on a piece no stocked length or sheet holds.</summary>
    public const string NothingHolds = "no stocked size holds";

    /// <summary>The note on hardwood, which is sold by the board foot and never counted in pieces.</summary>
    public const string ByTheBoardFoot = "board feet from the rough thickness; buy this plus waste";

    /// <summary>
    /// The shopping list for a cut list.
    /// </summary>
    /// <param name="rows">The cut list's rows, in its own order.</param>
    /// <returns>
    /// One row per stock item and species, ordinal by stock name and then species; then one row for
    /// each cut row that buys nothing, in cut-list order.
    /// </returns>
    public static ImmutableArray<ShoppingListRow> Of(IEnumerable<CutListRow> rows) => Of(rows, CutLayout.DefaultKerf);

    /// <summary>
    /// The line under the shopping list when rough parts have no stock (<c>docs/design/sketch-mode.md</c>
    /// &#xA7;5), counting pieces: "4 parts are rough and have no stock; they are not on this list", so
    /// the total never quietly omits half a bench. Null when there are none.
    /// </summary>
    /// <param name="rows">The cut list's rows.</param>
    public static string? RoughFooter(IEnumerable<CutListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int pieces = rows.Where(row => row.Rough && row.Stock is null).Sum(row => row.Quantity);
        return pieces switch
        {
            0 => null,
            1 => "1 part is rough and has no stock; it is not on this list.",
            _ => $"{pieces.ToString(System.Globalization.CultureInfo.InvariantCulture)} parts are rough and have no stock; they are not on this list.",
        };
    }

    /// <summary>
    /// The shopping list for a cut list, its boards planned with a saw kerf.
    /// </summary>
    /// <param name="rows">The cut list's rows, in its own order.</param>
    /// <param name="kerf">The kerf per saw cut; zero is allowed.</param>
    /// <returns>As <see cref="Of(IEnumerable{CutListRow})"/>.</returns>
    public static ImmutableArray<ShoppingListRow> Of(IEnumerable<CutListRow> rows, Length kerf)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<CutListRow> all = [.. rows];

        // Step 1 — bucket by stock. The key is the library's own normalised key, so "2 X 4" and
        // "2x4" are one bucket, and the species is part of it (§4.1, one row per item and species).
        List<ShoppingListRow> stocked = [];
        foreach (IGrouping<(string Key, string Species), CutListRow> bucket in all
                     .Where(row => row.Stock is LumberStock or PanelStock or HardwoodStock)
                     .GroupBy(row => (row.Stock!.Key, row.Species)))
        {
            CutListRow[] members = [.. bucket];
            StockItem stock = members[0].Stock!;

            stocked.Add(stock switch
            {
                LumberStock lumber when lumber.StandardLengths.IsEmpty => Unlisted(lumber, bucket.Key.Species, members),
                LumberStock lumber => Boards(lumber, bucket.Key.Species, members, kerf),
                PanelStock panel => Sheets(panel, bucket.Key.Species, members, kerf),
                _ => Hardwood((HardwoodStock)stock, bucket.Key.Species, members),
            });
        }

        // Rows with nothing to buy for them are reported on their own and never aggregated.
        IEnumerable<ShoppingListRow> nothing = all
            .Where(row => row.Stock is not (LumberStock or PanelStock or HardwoodStock))
            .Select(NothingToBuy);

        return
        [
            .. stocked
                .OrderBy(row => row.Material, StringComparer.Ordinal)
                .ThenBy(row => row.Species, StringComparer.Ordinal),
            .. nothing,
        ];
    }

    /// <summary>Lumber: the boards <see cref="CutLayout"/> plans, so the two lists never disagree (§4 step 2, #138).</summary>
    private static ShoppingListRow Boards(LumberStock lumber, string species, CutListRow[] members, Length kerf)
    {
        StockLayout layout = CutLayout.Boards(lumber, species, members, kerf);

        // Used volume is the placed pieces' own; a piece nothing holds is refused and buys nothing.
        Int128 used = layout.Boards
            .SelectMany(board => board.Pieces)
            .Aggregate(Int128.Zero, (sum, piece) => sum + Area.Volume(lumber.NominalThickness, lumber.NominalWidth, piece.Length));
        Int128 boughtVolume = layout.Boards.Aggregate(
            Int128.Zero,
            (sum, board) => sum + Area.Volume(lumber.NominalThickness, lumber.NominalWidth, board.StockLength));

        return new ShoppingListRow(
            lumber.Name,
            species,
            ShoppingListKind.Boards,
            For(members),
            [
                .. layout.Boards
                    .GroupBy(board => board.StockLength)
                    .OrderBy(group => group.Key)
                    .Select(group => new BoardsOfLength(group.Key, group.Count())),
            ],
            Sheets: 0,
            boughtVolume,
            used,
            TooBig(layout.TooLong.Select(piece => Text(piece.Length))),
            lumber);
    }

    /// <summary>Lumber with no stock-length list: total length, and nothing bought (§4).</summary>
    private static ShoppingListRow Unlisted(LumberStock lumber, string species, CutListRow[] members)
    {
        Length total = members.Aggregate(Length.Zero, (sum, row) => sum + (row.Length * row.Quantity));

        return new ShoppingListRow(
            lumber.Name,
            species,
            ShoppingListKind.NoLengthList,
            For(members),
            [],
            Sheets: 0,
            Int128.Zero,
            Area.Volume(lumber.NominalThickness, lumber.NominalWidth, total),
            $"{NoLengthList}: {Text(total)} of pieces in all, nothing bought",
            lumber);
    }

    /// <summary>Panels: the sheets <see cref="SheetLayout"/> nests the parts on, so the two lists never disagree (#26).</summary>
    private static ShoppingListRow Sheets(PanelStock panel, string species, CutListRow[] members, Length kerf)
    {
        PanelLayout layout = SheetLayout.Of(panel, species, members, kerf);
        string refusal = TooBig(layout.Refused.Where(piece => !piece.AgainstGrain).Select(piece => $"{Text(piece.Length)} × {Text(piece.Width)}"));
        string[] againstGrain =
        [
            .. layout.Refused
                .Where(piece => piece.AgainstGrain)
                .GroupBy(piece => $"{Text(piece.Length)} × {Text(piece.Width)}", StringComparer.Ordinal)
                .Select(group => $"{group.Count()} × {group.Key}"),
        ];
        string grain = againstGrain.Length == 0 ? string.Empty : $"{string.Join(", ", againstGrain)} fit only turned, against the grain set on them, so none is bought";
        string note = string.Join("; ", new[] { SheetsByLayout, refusal, grain }.Where(part => part.Length > 0));
        return new ShoppingListRow(
            panel.Name,
            species,
            ShoppingListKind.Sheets,
            For(members),
            [],
            layout.Sheets.Length,
            Int128.Zero,
            Int128.Zero,
            note,
            panel);
    }

    /// <summary>Hardwood: board feet from the rough thickness, and no piece count (§4, §4.1).</summary>
    private static ShoppingListRow Hardwood(HardwoodStock hardwood, string species, CutListRow[] members)
        => new(
            hardwood.Name,
            species,
            ShoppingListKind.BoardFeet,
            For(members),
            [],
            Sheets: 0,
            Int128.Zero,
            members.Aggregate(
                Int128.Zero,
                (sum, row) => sum + (Area.Volume(hardwood.RoughThickness, row.Width, row.Length) * row.Quantity)),
            ByTheBoardFoot,
            hardwood);

    /// <summary>A cut row nothing is bought for, reported on its own.</summary>
    private static ShoppingListRow NothingToBuy(CutListRow row)
    {
        string why = row switch
        {
            { Unresolved: true } => "its stock is not in this build's materials library",
            { Stock: FastenerStock } => "a fastener is counted by a schedule, not cut to a size",
            _ => "no stock chosen",
        };

        return new ShoppingListRow(
            row.MaterialText,
            row.Species,
            ShoppingListKind.NothingToBuy,
            For([row]),
            [],
            Sheets: 0,
            Int128.Zero,
            Int128.Zero,
            $"{why}, so nothing is bought for it",
            Stock: null);
    }

    /// <summary>"Leg × 4, Stretcher × 2": what a row is for, in cut-list order.</summary>
    private static string For(IEnumerable<CutListRow> rows)
        => string.Join(", ", rows.Select(row => $"{row.Label} × {row.Quantity}"));

    /// <summary>The refusal for pieces nothing holds, counted by size, or empty when there are none.</summary>
    private static string TooBig(IEnumerable<string> sizes)
    {
        string[] counted =
        [
            .. sizes
                .GroupBy(size => size, StringComparer.Ordinal)
                .Select(group => $"{group.Count()} × {group.Key}"),
        ];

        return counted.Length == 0
            ? string.Empty
            : $"{NothingHolds} {string.Join(", ", counted)}, so none is bought";
    }

    private static string Text(Length length) => CutListCsv.Text(length);
}
