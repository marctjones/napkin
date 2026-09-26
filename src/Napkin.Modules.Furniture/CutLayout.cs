using System.Collections.Immutable;
using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>One piece on a board: what it is called and how long it is cut.</summary>
/// <param name="Label">The cut-list row's label.</param>
/// <param name="Length">The row's length (a mitred piece's long point).</param>
public readonly record struct PlacedPiece(string Label, Length Length);

/// <summary>One board to buy, with the pieces cut from it.</summary>
/// <param name="Number">1, 2, 3 ... within its stock and species.</param>
/// <param name="StockLength">The stocked length bought.</param>
/// <param name="Pieces">The pieces, in the order they are cut (longest first).</param>
/// <param name="Cuts">The saw cuts this board takes (see <see cref="CutLayout"/> for the rule).</param>
/// <param name="Kerf">The blade's kerf used.</param>
public sealed record PlannedBoard(int Number, Length StockLength, ImmutableArray<PlacedPiece> Pieces, int Cuts, Length Kerf)
{
    /// <summary>The pieces' lengths added up.</summary>
    public Length PiecesLength => Pieces.Aggregate(Length.Zero, (sum, piece) => sum + piece.Length);

    /// <summary>
    /// The kerf spent on this board: <see cref="Cuts"/> times the kerf, except that the last cut of a
    /// board whose remainder is thinner than the kerf can only take what is left of the board.
    /// </summary>
    public Length KerfTotal => Length.Min(Kerf * Cuts, StockLength - PiecesLength);

    /// <summary>The board's used length: the pieces and the kerf.</summary>
    public Length Used => PiecesLength + KerfTotal;

    /// <summary>What is left of the board after the last cut. Never negative.</summary>
    public Length Offcut => StockLength - Used;
}

/// <summary>The boards of one stock item and species, and the pieces no stocked length holds.</summary>
/// <param name="Stock">The lumber.</param>
/// <param name="Species">The species, or empty.</param>
/// <param name="Boards">The boards, in the order they were opened.</param>
/// <param name="TooLong">The pieces longer than the longest stocked length, in the order refused.</param>
public sealed record StockLayout(LumberStock Stock, string Species, ImmutableArray<PlannedBoard> Boards, ImmutableArray<PlacedPiece> TooLong);

/// <summary>One line of the cut layout as it is shown and exported: a board, or a refusal.</summary>
/// <param name="Stock">The stock's name.</param>
/// <param name="Species">The species, or empty.</param>
/// <param name="Board">"Board 3", or "No board" for a refusal.</param>
/// <param name="BoardLength">The stocked length, or empty.</param>
/// <param name="Pieces">The pieces in cutting order, "Leg 36 in + Rail 24 in".</param>
/// <param name="Cuts">The number of cuts, as text, or empty.</param>
/// <param name="Kerf">The kerf spent on the board, or empty.</param>
/// <param name="Offcut">The offcut, or empty.</param>
/// <param name="Planned">The board itself, for drawing it; <see langword="null"/> on a refusal or a sheet.</param>
public sealed record CutLayoutRow(
    string Stock,
    string Species,
    string Board,
    string BoardLength,
    string Pieces,
    string Cuts,
    string Kerf,
    string Offcut,
    PlannedBoard? Planned)
{
    /// <summary>The sheet itself, on a sheet's row (#26); <see langword="null"/> otherwise.</summary>
    public PlannedSheet? Sheet { get; init; }
}

/// <summary>A whole cut layout: every stock's boards, and what was not laid out.</summary>
/// <param name="Kerf">The saw kerf the layout was planned with.</param>
/// <param name="Stocks">One layout per lumber stock and species, ordinal by name then species.</param>
/// <param name="Notes">What was not laid out and why (hardwood, lumber with no length list).</param>
public sealed record CutLayoutPlan(Length Kerf, ImmutableArray<StockLayout> Stocks, ImmutableArray<string> Notes)
{
    /// <summary>One sheet layout per panel and species (#26), ordinal by name then species.</summary>
    public ImmutableArray<PanelLayout> Panels { get; init; } = [];

    /// <summary>Every sheet of every panel.</summary>
    public IEnumerable<PlannedSheet> AllSheets => Panels.SelectMany(panel => panel.Sheets);

    /// <summary>Every board of every stock.</summary>
    public IEnumerable<PlannedBoard> AllBoards => Stocks.SelectMany(stock => stock.Boards);

    /// <summary>The length of all the boards bought.</summary>
    public Length BoughtLength => AllBoards.Aggregate(Length.Zero, (sum, board) => sum + board.StockLength);

    /// <summary>The length of all the pieces placed.</summary>
    public Length PiecesLength => AllBoards.Aggregate(Length.Zero, (sum, board) => sum + board.PiecesLength);

    /// <summary>Everything bought that is not a piece: offcuts and kerf.</summary>
    public Length WasteLength => BoughtLength - PiecesLength;

    /// <summary>The waste as tenths of a percent of the length bought, rounded half up; zero when nothing is bought.</summary>
    public long WasteTenthsOfPercent
        => BoughtLength.Units == 0 ? 0 : ((WasteLength.Units * 1000) + (BoughtLength.Units / 2)) / BoughtLength.Units;
}

/// <summary>
/// The cut layout (issue #138): which piece is cut from which board, with saw kerf and offcuts. The
/// one place that decides how many boards a lumber part list needs, so the shopping list and the layout
/// never disagree.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The rules.</strong> Per lumber stock and species, every needed piece (a row's length, once
/// per unit of quantity) is sorted longest first, ties keeping cut-list order, and placed first-fit
/// on the first open board that still holds it; if none does, a new board of the <em>longest</em>
/// stocked length is opened. When all pieces are placed, each board <em>shrinks</em> to the smallest
/// stocked length that still holds its pieces. A piece longer than the longest stocked length is
/// refused (named, nothing bought for it), never given a made-up board.
/// </para>
/// <para>
/// <strong>Kerf.</strong> Each saw cut removes one kerf. A board with n pieces takes n cuts when an
/// offcut remains (n-1 separate the pieces and one separates the last piece from the offcut), and
/// n-1 cuts when the pieces and their kerfs exactly fill the board (the last piece ends at the
/// board's end). A board of length L holds its pieces when pieces + kerf x (n-1) &lt;= L: the kerfs
/// between the pieces must fit, and the last cut may take a kerf or, when less than a kerf of board
/// is left, whatever is left (the blade runs out through the end of the board). The piece length is the cut-list row's length (a mitred piece's long point). The kerf is
/// a practice default the person sets, not a sourced fact; the stocked lengths come only from the
/// materials library. Sheet goods are laid out on sheets by <see cref="SheetLayout"/> (#26).
/// </para>
/// <para>All arithmetic is on exact <see cref="Length"/> values (1/1024 in); no doubles.</para>
/// </remarks>
public static class CutLayout
{
    /// <summary>The kerf napkin plans with until the person sets one: a practice default, not a fact about their blade.</summary>
    public static readonly Length DefaultKerf = Length.Inches(0, 1, 8);

    /// <summary>The header of the exported file's columns.</summary>
    public const string Header = "Stock,Species,Board,Board length,Pieces,Cuts,Kerf,Offcut";

    /// <summary>What the cut layout says about the kerf it used, on screen and in the exported file.</summary>
    /// <param name="kerf">The kerf used.</param>
    public static string Statement(Length kerf)
        => $"Cut layout: first-fit decreasing on the stocked lengths; includes a {Inches(kerf)} saw kerf per cut "
           + "(set it in the Saw kerf box on the Cut layout tab); piece lengths are the cut list's finished sizes.";

    /// <summary>
    /// Whether pieces of total length <paramref name="sum"/>, <paramref name="count"/> of them, fit a
    /// board, and how many cuts they take (the rule in the class remarks).
    /// </summary>
    /// <returns>Whether they fit.</returns>
    public static bool TryFit(Length sum, int count, Length kerf, Length boardLength, out int cuts)
    {
        Length tight = sum + (kerf * (count - 1));
        cuts = tight == boardLength ? count - 1 : count;
        return tight <= boardLength;
    }

    /// <summary>Lays out the lumber in a cut list's rows.</summary>
    /// <param name="rows">The cut list's rows.</param>
    /// <param name="kerf">The saw kerf; zero is allowed.</param>
    public static CutLayoutPlan Of(IEnumerable<CutListRow> rows, Length kerf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (kerf < Length.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(kerf), kerf, "A saw kerf cannot be negative.");
        }

        List<CutListRow> all = [.. rows];
        List<StockLayout> stocks = [];
        List<string> notes = [];

        foreach (IGrouping<(string Key, string Species), CutListRow> bucket in all
                     .Where(row => row.Stock is LumberStock { StandardLengths.IsEmpty: false })
                     .GroupBy(row => (row.Stock!.Key, row.Species)))
        {
            CutListRow[] members = [.. bucket];
            stocks.Add(Boards((LumberStock)members[0].Stock!, bucket.Key.Species, members, kerf));
        }

        foreach (string name in all.Where(row => row.Stock is LumberStock { StandardLengths.IsEmpty: true }).Select(row => row.Stock!.Name).Distinct(StringComparer.Ordinal))
        {
            notes.Add($"{name}: napkin has read no stock-length list for this size, so it is not laid out");
        }

        List<PanelLayout> panels = [];
        foreach (IGrouping<(string Key, string Species), CutListRow> bucket in all
                     .Where(row => row.Stock is PanelStock)
                     .GroupBy(row => (row.Stock!.Key, row.Species)))
        {
            CutListRow[] members = [.. bucket];
            panels.Add(SheetLayout.Of((PanelStock)members[0].Stock!, bucket.Key.Species, members, kerf));
        }

        if (all.Any(row => row.Stock is HardwoodStock))
        {
            notes.Add("hardwood: sold by the board foot in random widths, not laid out");
        }

        return new CutLayoutPlan(
            kerf,
            [.. stocks.OrderBy(stock => stock.Stock.Name, StringComparer.Ordinal).ThenBy(stock => stock.Species, StringComparer.Ordinal)],
            [.. notes])
        {
            Panels = [.. panels.OrderBy(panel => panel.Panel.Name, StringComparer.Ordinal).ThenBy(panel => panel.Species, StringComparer.Ordinal)],
        };
    }

    /// <summary>The boards of one lumber stock and species.</summary>
    /// <param name="lumber">The lumber, with a non-empty stock-length list.</param>
    /// <param name="species">The species.</param>
    /// <param name="members">The cut-list rows that use it.</param>
    /// <param name="kerf">The saw kerf.</param>
    public static StockLayout Boards(LumberStock lumber, string species, IEnumerable<CutListRow> members, Length kerf)
    {
        // Longest first; LINQ's OrderBy is stable, so equal lengths keep cut-list order.
        PlacedPiece[] pieces =
        [
            .. members
                .SelectMany(row => Enumerable.Repeat(new PlacedPiece(row.Label, row.Length), row.Quantity))
                .OrderByDescending(piece => piece.Length),
        ];

        Length longest = lumber.StandardLengths.Max();
        List<List<PlacedPiece>> open = [];
        List<Length> sums = [];
        List<PlacedPiece> tooLong = [];

        foreach (PlacedPiece piece in pieces)
        {
            if (piece.Length > longest)
            {
                tooLong.Add(piece);
                continue;
            }

            int board = -1;
            for (int i = 0; i < open.Count && board < 0; i++)
            {
                if (TryFit(sums[i] + piece.Length, open[i].Count + 1, kerf, longest, out _))
                {
                    board = i;
                }
            }

            if (board < 0)
            {
                open.Add([]);
                sums.Add(Length.Zero);
                board = open.Count - 1;
            }

            open[board].Add(piece);
            sums[board] += piece.Length;
        }

        // The shrink step: the smallest stocked length that still holds the board's pieces.
        Length[] lengths = [.. lumber.StandardLengths.Order()];
        List<PlannedBoard> boards = [];
        for (int i = 0; i < open.Count; i++)
        {
            Length chosen = longest;
            int cuts = 0;
            foreach (Length candidate in lengths)
            {
                if (TryFit(sums[i], open[i].Count, kerf, candidate, out int candidateCuts))
                {
                    chosen = candidate;
                    cuts = candidateCuts;
                    break;
                }
            }

            boards.Add(new PlannedBoard(i + 1, chosen, [.. open[i]], cuts, kerf));
        }

        return new StockLayout(lumber, species, [.. boards], [.. tooLong]);
    }

    /// <summary>The lines of a plan as shown and exported: one per board, then one per refused piece.</summary>
    /// <param name="plan">The plan.</param>
    public static ImmutableArray<CutLayoutRow> Rows(CutLayoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<CutLayoutRow> rows = [];
        foreach (StockLayout stock in plan.Stocks)
        {
            foreach (PlannedBoard board in stock.Boards)
            {
                rows.Add(new CutLayoutRow(
                    stock.Stock.Name,
                    stock.Species,
                    $"Board {board.Number}",
                    Feet(board.StockLength),
                    string.Join(" + ", board.Pieces.Select(piece => $"{piece.Label} {Inches(piece.Length)}")),
                    board.Cuts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Inches(board.KerfTotal),
                    Inches(board.Offcut),
                    board));
            }

            foreach (PlacedPiece piece in stock.TooLong)
            {
                rows.Add(new CutLayoutRow(
                    stock.Stock.Name,
                    stock.Species,
                    "No board",
                    string.Empty,
                    $"{piece.Label} {Inches(piece.Length)}: {ShoppingList.NothingHolds} it, so none is bought",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Planned: null));
            }
        }

        foreach (PanelLayout panel in plan.Panels)
        {
            string size = $"{Feet(Length.Min(panel.Panel.SheetWidth, panel.Panel.SheetLength))} × {Feet(Length.Max(panel.Panel.SheetWidth, panel.Panel.SheetLength))}";
            foreach (PlannedSheet sheet in panel.Sheets)
            {
                rows.Add(new CutLayoutRow(
                    panel.Panel.Name,
                    panel.Species,
                    $"Sheet {sheet.Number}",
                    size,
                    SheetLayout.Pieces(sheet),
                    sheet.Cuts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Inches(sheet.Kerf),
                    SheetLayout.SquareFeet(sheet.WasteArea),
                    Planned: null)
                {
                    Sheet = sheet,
                });
            }

            foreach (RefusedSheetPiece piece in panel.Refused)
            {
                string why = piece.AgainstGrain
                    ? "fits only turned, against the grain set on it, so none is bought"
                    : $"{ShoppingList.NothingHolds} it, so none is bought";
                rows.Add(new CutLayoutRow(
                    panel.Panel.Name,
                    panel.Species,
                    "No sheet",
                    string.Empty,
                    $"{piece.Label} {Inches(piece.Length)} × {Inches(piece.Width)}: {why}",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    Planned: null));
            }
        }

        return [.. rows];
    }

    /// <summary>The fields of a row, in the order of <see cref="Header"/>.</summary>
    /// <param name="row">The row.</param>
    public static ImmutableArray<string> Fields(CutLayoutRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return [row.Stock, row.Species, row.Board, row.BoardLength, row.Pieces, row.Cuts, row.Kerf, row.Offcut];
    }

    /// <summary>
    /// The one line a row reads as on screen: "Board 1: 2x2 x 8 ft: Leg 36 in + ... | 3 cuts, kerf 3/8 in | offcut 4 5/8 in".
    /// </summary>
    /// <param name="fields">A row's fields, as <see cref="Fields"/> gives them or a file carries them.</param>
    public static string Line(IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        string stock = fields[1].Length == 0 ? fields[0] : $"{fields[0]} ({fields[1]})";
        if (fields[3].Length == 0)
        {
            return $"{fields[2]}: {stock}: {fields[4]}";
        }

        string cuts = fields[5] == "1" ? "1 cut" : $"{fields[5]} cuts";
        return $"{fields[2]}: {stock} x {fields[3]}: {fields[4]} | {cuts}, kerf {fields[6]} | offcut {fields[7]}";
    }

    /// <summary>The summary lines under a layout: boards per stock and length, then totals, then what was not laid out.</summary>
    /// <param name="plan">The plan.</param>
    public static ImmutableArray<string> Summary(CutLayoutPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<string> lines = [];
        foreach (StockLayout stock in plan.Stocks.Where(stock => !stock.Boards.IsEmpty))
        {
            string buy = string.Join(
                ", ",
                stock.Boards.GroupBy(board => board.StockLength).OrderBy(group => group.Key).Select(group => $"{group.Count()} x {Feet(group.Key)}"));
            int pieces = stock.Boards.Sum(board => board.Pieces.Length);
            string name = stock.Species.Length == 0 ? stock.Stock.Name : $"{stock.Stock.Name} ({stock.Species})";
            lines.Add($"{name}: {buy}; {pieces} {(pieces == 1 ? "piece" : "pieces")} on {stock.Boards.Length} {(stock.Boards.Length == 1 ? "board" : "boards")}");
        }

        foreach (PanelLayout panel in plan.Panels.Where(panel => !panel.Sheets.IsEmpty))
        {
            int pieces = panel.Sheets.Sum(sheet => sheet.Pieces.Count());
            Int128 bought = panel.Sheets.Aggregate(Int128.Zero, (sum, sheet) => sum + sheet.SheetArea);
            Int128 waste = panel.Sheets.Aggregate(Int128.Zero, (sum, sheet) => sum + sheet.WasteArea);
            long tenths = (long)(((waste * 1000) + (bought / 2)) / bought);
            string name = panel.Species.Length == 0 ? panel.Panel.Name : $"{panel.Panel.Name} ({panel.Species})";
            int count = panel.Sheets.Length;
            lines.Add($"{name}: {count} {(count == 1 ? "sheet" : "sheets")}; {pieces} {(pieces == 1 ? "piece" : "pieces")}, "
                      + $"waste {SheetLayout.SquareFeet(waste)} ({tenths / 10}.{tenths % 10}%) counting offcuts and kerf");
        }

        if (plan.Panels.Any(panel => !panel.Sheets.IsEmpty))
        {
            lines.Add(SheetLayout.Statement);
        }

        if (plan.AllBoards.Any())
        {
            long tenths = plan.WasteTenthsOfPercent;
            int total = plan.AllBoards.Count();
            lines.Add($"Total: {total} {(total == 1 ? "board" : "boards")}, {Feet(plan.BoughtLength)} bought, {Feet(plan.PiecesLength)} of pieces, "
                      + $"waste {Feet(plan.WasteLength)} ({tenths / 10}.{tenths % 10}%) counting offcuts and kerf");
        }

        lines.AddRange(plan.Notes);
        return [.. lines];
    }

    /// <summary>The layout as CSV text: a statement, the column names, one line per row.</summary>
    /// <param name="rows">The rows, in the order on screen.</param>
    /// <param name="kerf">The kerf the layout was planned with.</param>
    public static string ToCsv(IEnumerable<CutLayoutRow> rows, Length kerf)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder csv = new();
        csv.Append(CutListCsv.Field(Statement(kerf))).Append('\n');
        csv.Append(Header).Append('\n');
        foreach (CutLayoutRow row in rows)
        {
            csv.AppendJoin(',', Fields(row).Select(CutListCsv.Field)).Append('\n');
        }

        return csv.ToString();
    }

    /// <summary>A length in inches and reduced fractions, exact to 1/1024: "4 5/8 in", "36 in", "3/8 in".</summary>
    /// <param name="length">The length.</param>
    public static string Inches(Length length)
    {
        long units = Math.Abs(length.Units);
        long whole = units / Length.UnitsPerInch;
        long rest = units % Length.UnitsPerInch;
        string sign = length.Units < 0 ? "-" : string.Empty;
        if (rest == 0)
        {
            return $"{sign}{whole} in";
        }

        long divisor = Gcd(rest, Length.UnitsPerInch);
        string fraction = $"{rest / divisor}/{Length.UnitsPerInch / divisor}";
        return whole == 0 ? $"{sign}{fraction} in" : $"{sign}{whole} {fraction} in";
    }

    /// <summary>A stocked length: "8 ft" when it is whole feet, inches otherwise.</summary>
    /// <param name="length">The length.</param>
    public static string Feet(Length length)
        => length.Units % Length.UnitsPerFoot == 0 ? $"{length.Units / Length.UnitsPerFoot} ft" : Inches(length);

    private static long Gcd(long a, long b) => b == 0 ? a : Gcd(b, a % b);
}
