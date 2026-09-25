using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The cut layout (issue #138), on small stocks whose answers are worked out by hand in each test's
/// comments. The stocked lengths are the shipped library's own for a 2x4 (6', 8', 10', 12', 14', 16':
/// 72, 96, 120, 144, 168, 192 in; WCLIB No. 17 ¶260-a). The kerf in most tests is 1/8 in, the practice
/// default; the tests pass it explicitly.
/// </summary>
public sealed class CutLayoutTests
{
    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);
    private static readonly Length Kerf = Length.Inches(0, 1, 8);
    private static readonly Length NoKerf = Length.Zero;

    private static long In(double inches) => (long)(inches * 1024);

    private static (string, long, long, Piece) Bar(string name, double length, int quantity = 1)
        => (name, In(length), In(3.5), new Piece("2x4", null, quantity, new Length(In(1.5)), Flat));

    private static ImmutableArray<CutListRow> Rows(params (string, long, long, Piece)[] parts)
        => CutList.Of(Design.WithParts(parts), MaterialsLibrary.Shipped);

    private static CutLayoutPlan Plan(Length kerf, params (string, long, long, Piece)[] parts)
        => CutLayout.Of(Rows(parts), kerf);

    private static PlannedBoard Only(CutLayoutPlan plan) => Assert.Single(Assert.Single(plan.Stocks).Boards);

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Three_pieces_on_a_board_take_three_cuts_and_leave_the_offcut()
    {
        // 36 + 24 + 20 = 80 in of pieces, 3 pieces. One board opens at 16'. Shrink: 72 in is too short
        // (80 > 72); 96 in: is 80 + 2 x 1/8 = 80 1/4 exactly 96? no. So an offcut remains and there are
        // 3 cuts: 80 + 3 x 1/8 = 80 3/8 <= 96. Offcut 96 - 80 3/8 = 15 5/8.
        PlannedBoard board = Only(Plan(Kerf, Bar("Rail", 36), Bar("Stile", 24), Bar("Block", 20)));

        Assert.Equal(new Length(96 * 1024), board.StockLength);
        Assert.Equal(["Rail", "Stile", "Block"], board.Pieces.Select(piece => piece.Label));
        Assert.Equal(3, board.Cuts);
        Assert.Equal(Length.Inches(0, 3, 8), board.KerfTotal);
        Assert.Equal(Length.Inches(80, 3, 8), board.Used);
        Assert.Equal(Length.Inches(15, 5, 8), board.Offcut);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Pieces_that_exactly_fill_a_board_take_one_cut_fewer()
    {
        // 30 + 30 + 20 + 15 5/8 = 95 5/8 in, 4 pieces, 3 kerfs between them = 3/8: 95 5/8 + 3/8 = 96
        // exactly. The last piece ends at the board's end, so 3 cuts (n-1), no offcut, on the 8' (96 in).
        // Counting a fourth cut would make 96 1/8 > 96 and need the 10'.
        PlannedBoard board = Only(Plan(Kerf, Bar("A", 30), Bar("B", 30), Bar("C", 20), Bar("D", 15.625)));

        Assert.Equal(new Length(96 * 1024), board.StockLength);
        Assert.Equal(3, board.Cuts);
        Assert.Equal(Length.Zero, board.Offcut);
        Assert.Equal(new Length(96 * 1024), board.Used);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_sliver_thinner_than_the_kerf_still_fits_and_the_last_cut_takes_what_is_left()
    {
        // 30 + 30 + 20 + 15 3/4 = 95 3/4 in, 4 pieces. The 3 kerfs between them (3/8) give 96 1/8 > 96,
        // so the 8' does not hold them and the 10' does, with 4 cuts.
        Assert.Equal(new Length(120 * 1024), Only(Plan(Kerf, Bar("A", 30), Bar("B", 30), Bar("C", 20), Bar("D", 15.75))).StockLength);

        // 95 in in 4 pieces (30, 30, 20, 15): 3 kerfs = 3/8 gives 95 3/8 <= 96; a 4th cut would make
        // 95 1/2, still <= 96: 4 cuts, offcut 1/2.
        PlannedBoard board = Only(Plan(Kerf, Bar("A", 30), Bar("B", 30), Bar("C", 20), Bar("D", 15)));
        Assert.Equal(new Length(96 * 1024), board.StockLength);
        Assert.Equal(4, board.Cuts);
        Assert.Equal(Length.Inches(0, 1, 2), board.Offcut);

        // One 191 15/16 in piece on the 16' (192): 1/16 in is left, thinner than the 1/8 kerf. It
        // fits (0 kerfs between pieces), takes 1 cut, and that cut spends only the 1/16 there is.
        PlannedBoard sliver = Only(Plan(Kerf, Bar("A", 191.9375)));
        Assert.Equal(new Length(192 * 1024), sliver.StockLength);
        Assert.Equal(1, sliver.Cuts);
        Assert.Equal(Length.Inches(0, 1, 16), sliver.KerfTotal);
        Assert.Equal(Length.Zero, sliver.Offcut);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void First_fit_decreasing_saves_a_board_over_packing_in_order()
    {
        // Kerf 0, pieces in the order 70, 70, 122, 122 (in). Packing in order on 16' boards (192 in):
        // 70 -> b1; 70 -> b1 (140); 122 -> b2 (b1 has 52 left); 122 -> b3 (b1 52, b2 70 left): 3 boards.
        // Longest first, 122 -> b1; 122 -> b2; 70 -> b1 (192, full); 70 -> b2 (192): 2 boards.
        int[] order = [70, 70, 122, 122];
        Assert.Equal(3, NaiveBoards(order, 192));

        CutLayoutPlan plan = Plan(NoKerf, Bar("Short", 70, 2), Bar("Long", 122, 2));

        StockLayout stock = Assert.Single(plan.Stocks);
        Assert.Equal(2, stock.Boards.Length);
        Assert.All(stock.Boards, board => Assert.Equal(new Length(192 * 1024), board.StockLength));
        Assert.All(stock.Boards, board => Assert.Equal(Length.Zero, board.Offcut));
        Assert.Equal(["Long", "Short"], stock.Boards[0].Pieces.Select(piece => piece.Label));
    }

    private static int NaiveBoards(int[] pieces, int length)
    {
        List<int> left = [];
        foreach (int piece in pieces)
        {
            int board = left.FindIndex(room => room >= piece);
            if (board < 0)
            {
                left.Add(length - piece);
            }
            else
            {
                left[board] -= piece;
            }
        }

        return left.Count;
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_exactly_as_long_as_a_stocked_length_needs_no_cut_and_no_bigger_board()
    {
        // 96 in on a 96 in board: n = 1, 96 + 0 x kerf == 96, so 0 cuts, no offcut, the 8'.
        PlannedBoard exact = Only(Plan(Kerf, Bar("Rail", 96)));
        Assert.Equal(new Length(96 * 1024), exact.StockLength);
        Assert.Equal(0, exact.Cuts);
        Assert.Equal(Length.Zero, exact.Offcut);

        // 96 1/16 in: not exact and over the 8', so the 10' with one cut (96 1/16 + 1/8 <= 120).
        PlannedBoard over = Only(Plan(Kerf, Bar("Rail", 96.0625)));
        Assert.Equal(new Length(120 * 1024), over.StockLength);
        Assert.Equal(1, over.Cuts);

        // 72 in is the shortest stocked length: the 6', no cut.
        Assert.Equal(new Length(72 * 1024), Only(Plan(Kerf, Bar("Rail", 72))).StockLength);

        // 73 in: between 6' and 8'; the smallest that holds it, with a cut, is the 8'.
        Assert.Equal(new Length(96 * 1024), Only(Plan(Kerf, Bar("Rail", 73))).StockLength);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_longer_than_any_stocked_length_is_refused_by_name_and_no_board_is_made()
    {
        // 200 in > 192 in, the longest 2x4 the library lists.
        CutLayoutPlan plan = Plan(Kerf, Bar("Ridge", 200), Bar("Block", 30));

        StockLayout stock = Assert.Single(plan.Stocks);
        Assert.Equal([new PlacedPiece("Ridge", new Length(200 * 1024))], stock.TooLong);
        PlannedBoard board = Assert.Single(stock.Boards);
        Assert.Equal(["Block"], board.Pieces.Select(piece => piece.Label));

        ImmutableArray<CutLayoutRow> rows = CutLayout.Rows(plan);
        Assert.Equal(2, rows.Length);
        Assert.Equal("No board", rows[1].Board);
        Assert.Equal("Ridge 200 in: no stocked size holds it, so none is bought", rows[1].Pieces);
        Assert.Null(rows[1].Planned);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_kerf_flips_the_board_at_the_boundary()
    {
        // 4 x 24 = 96 in. Kerf 0: 96 + 0 == 96, exact fill, the 8'. Kerf 1/8: 96 + 3/8 = 96 3/8 is not
        // 96 and 96 + 4/8 = 96 1/2 > 96, so the 8' no longer holds them; the 10' does (offcut 23 1/2).
        Assert.Equal(new Length(96 * 1024), Only(Plan(NoKerf, Bar("Rail", 24, 4))).StockLength);
        PlannedBoard withKerf = Only(Plan(Kerf, Bar("Rail", 24, 4)));
        Assert.Equal(new Length(120 * 1024), withKerf.StockLength);
        Assert.Equal(Length.Inches(23, 1, 2), withKerf.Offcut);

        // 8 x 24 = 192 in, the longest board. Kerf 0: one 16', exact. Kerf 1/8: 192 + 7/8 > 192, so
        // seven pieces (168 + 7/8 = 168 7/8 <= 192, on a 16'; 168 is not exact for the 14') and the
        // eighth on a second board, a 6' (24 + 1/8 <= 72).
        StockLayout none = Assert.Single(Plan(NoKerf, Bar("Rail", 24, 8)).Stocks);
        Assert.Equal([new Length(192 * 1024)], none.Boards.Select(board => board.StockLength));

        StockLayout some = Assert.Single(Plan(Kerf, Bar("Rail", 24, 8)).Stocks);
        Assert.Equal([new Length(192 * 1024), new Length(72 * 1024)], some.Boards.Select(board => board.StockLength));
        Assert.Equal([7, 1], some.Boards.Select(board => board.Pieces.Length));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Each_board_shrinks_to_the_smallest_stocked_length_that_holds_its_pieces()
    {
        // 50 + 30 = 80 in of pieces with 2 cuts (80 1/4 in): the 6' (72) is too short, the 8' (96) holds it.
        Assert.Equal(new Length(96 * 1024), Only(Plan(Kerf, Bar("A", 50), Bar("B", 30))).StockLength);

        // 100 + 60 = 160 1/4: 12' (144) is short; 14' (168) holds it.
        Assert.Equal(new Length(168 * 1024), Only(Plan(Kerf, Bar("A", 100), Bar("B", 60))).StockLength);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_same_input_always_gives_the_same_layout_and_equal_pieces_keep_cut_list_order()
    {
        (string, long, long, Piece)[] parts = [Bar("First", 40), Bar("Second", 40), Bar("Third", 40), Bar("Long", 60)];

        string once = CutLayout.ToCsv(CutLayout.Rows(Plan(Kerf, parts)), Kerf);
        string again = CutLayout.ToCsv(CutLayout.Rows(Plan(Kerf, parts)), Kerf);
        Assert.Equal(once, again);

        // 60 first, then the three 40s (the cut list merges identical parts into one row).
        PlannedBoard board = Only(Plan(Kerf, parts));
        Assert.Equal([In(60), In(40), In(40), In(40)], board.Pieces.Select(piece => piece.Length.Units));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_negative_kerf_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CutLayout.Of(Rows(Bar("A", 10)), new Length(-1)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [Trait("Feature", "CUT-007")]
    public void Random_pieces_are_each_placed_once_and_no_board_is_over_length(int seed)
    {
        Random random = new(seed);
        Length[] kerfs = [Length.Zero, Length.Inches(0, 1, 16), Kerf, Length.Inches(0, 3, 16)];
        LumberStock stock = Assert.IsType<LumberStock>(MaterialsLibrary.Shipped.Items.First(item => item.Name == "2x4"));

        for (int round = 0; round < 60; round++)
        {
            Length kerf = kerfs[random.Next(kerfs.Length)];
            (string, long, long, Piece)[] parts =
            [
                .. Enumerable.Range(0, random.Next(1, 8)).Select(i => Bar($"P{i}", random.Next(1, 192 * 16) / 16.0, random.Next(1, 5))),
            ];

            ImmutableArray<CutListRow> rows = Rows(parts);
            StockLayout layout = Assert.Single(CutLayout.Of(rows, kerf).Stocks);

            // Every piece exactly once: label and length counted, and the total length equal.
            string[] want = [.. rows.SelectMany(row => Enumerable.Repeat($"{row.Label}/{row.Length.Units}", row.Quantity)).Order(StringComparer.Ordinal)];
            string[] placed = [.. layout.Boards.SelectMany(board => board.Pieces).Concat(layout.TooLong).Select(piece => $"{piece.Label}/{piece.Length.Units}").Order(StringComparer.Ordinal)];
            Assert.Equal(want, placed);

            foreach (PlannedBoard board in layout.Boards)
            {
                Assert.Contains(board.StockLength, stock.StandardLengths);
                Assert.True(board.Used <= board.StockLength, "kerf included, a board is never over its length");
                Assert.True(board.Offcut >= Length.Zero);
                // n - 1 cuts exactly when the pieces and the kerfs between them fill the board; otherwise n
                // (a last cut, which may consume the last of the board: offcut 0 with n cuts).
                bool exact = board.PiecesLength + (kerf * (board.Pieces.Length - 1)) == board.StockLength;
                Assert.Equal(exact ? board.Pieces.Length - 1 : board.Pieces.Length, board.Cuts);

                // The shrink is minimal: no shorter stocked length holds these pieces.
                foreach (Length shorter in stock.StandardLengths.Where(length => length < board.StockLength))
                {
                    Assert.False(CutLayout.TryFit(board.PiecesLength, board.Pieces.Length, kerf, shorter, out _));
                }
            }

            Assert.Equal(
                rows.Sum(row => row.Length.Units * row.Quantity) - layout.TooLong.Sum(piece => piece.Length.Units),
                layout.Boards.Sum(board => board.PiecesLength.Units));
        }
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_shopping_list_buys_exactly_the_boards_the_layout_plans()
    {
        Length[] kerfs = [Length.Zero, Kerf, Length.Inches(0, 3, 16)];
        ImmutableArray<CutListRow> rows = Rows(Bar("Beam", 90), Bar("Rail", 50, 2), Bar("Block", 20), Bar("Cleat", 24, 8), Bar("Ridge", 200));

        foreach (Length kerf in kerfs)
        {
            ShoppingListRow shop = Assert.Single(ShoppingList.Of(rows, kerf));
            StockLayout layout = Assert.Single(CutLayout.Of(rows, kerf).Stocks);

            Assert.Equal(
                layout.Boards.GroupBy(board => board.StockLength).OrderBy(group => group.Key).Select(group => new BoardsOfLength(group.Key, group.Count())),
                shop.Boards);
            Assert.Equal(layout.Boards.Length, shop.Count);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_shopping_list_changes_with_the_kerf_it_is_given()
    {
        ImmutableArray<CutListRow> rows = Rows(Bar("Rail", 24, 8));

        Assert.Equal("1 × 16'-0\"", Assert.Single(ShoppingList.Of(rows, Length.Zero)).BuyText);
        Assert.Equal("1 × 6'-0\", 1 × 16'-0\"", Assert.Single(ShoppingList.Of(rows, Kerf)).BuyText);
        Assert.Equal("1 × 6'-0\", 1 × 16'-0\"", Assert.Single(ShoppingList.Of(rows)).BuyText);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_board_reads_as_a_line_with_its_cuts_kerf_and_offcut()
    {
        CutLayoutPlan plan = Plan(Kerf, Bar("Rail", 36), Bar("Stile", 24), Bar("Block", 20));
        CutLayoutRow row = Assert.Single(CutLayout.Rows(plan));

        Assert.Equal(
            "Board 1: 2x4 x 8 ft: Rail 36 in + Stile 24 in + Block 20 in | 3 cuts, kerf 3/8 in | offcut 15 5/8 in",
            CutLayout.Line(CutLayout.Fields(row)));

        // Board feet aside, the summary: one 8' board bought, 80 in of pieces, 16 in waste = 16.666...%.
        Assert.Equal(
            [
                "2x4: 1 x 8 ft; 3 pieces on 1 board",
                "Total: 1 board, 8 ft bought, 80 in of pieces, waste 16 in (16.7%) counting offcuts and kerf",
            ],
            CutLayout.Summary(plan));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Sheet_goods_are_reported_as_not_nested_and_are_not_laid_out()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(("Top", In(48), In(24), new Piece("3/4 plywood", null, 1, new Length(768), Flat))),
            MaterialsLibrary.Shipped);

        CutLayoutPlan plan = CutLayout.Of(rows, Kerf);

        Assert.Empty(plan.Stocks);
        Assert.Contains(CutLayout.SheetGoodsNote, plan.Notes);
    }
}
