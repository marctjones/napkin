using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The shopping list of <c>docs/design/parts-and-cut-list.md</c> §4, on small designs whose answers
/// are worked out by hand in each test's comments. The stock lengths and sizes are the shipped
/// library's own rows (2x4 and 1x4: 6' to 16' in 2' steps, WCLIB No. 17 ¶260-a; 5/4x6: no length
/// list; 3/4 plywood: 48" × 96", PS 1-19 §5.4; 4/4 hardwood: 1" rough, NHLA ¶13) — nothing here is
/// a size from memory.
/// </summary>
public sealed class ShoppingListTests
{
    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);

    private static long In(double inches) => (long)(inches * 1024);

    /// <summary>A piece lying flat along X: its length across X, its width up Y, its thickness out of plane.</summary>
    private static (string, long, long, Piece) Along(string name, double length, double width, double thickness, string? stock, int quantity = 1, string? species = null)
        => (name, In(length), In(width), new Piece(stock, species, quantity, new Length(In(thickness)), Flat));

    private static (string, long, long, Piece) TwoByFour(string name, double length, int quantity = 1, string? stock = "2x4", string? species = null)
        => Along(name, length, 3.5, 1.5, stock, quantity, species);

    private static ImmutableArray<ShoppingListRow> Shop(params (string, long, long, Piece)[] parts)
        => ShoppingList.Of(CutList.Of(Design.WithParts(parts), MaterialsLibrary.Shipped));

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Several_parts_that_fit_one_board_are_one_board_to_buy()
    {
        // 36" + 30" = 66", which a 6' (72") 2x4 holds. Cut list: two rows. Shopping list: one board.
        // Board feet, nominal 2" x 4": bought 2 x 4 x 72 = 576 in³ = 4.0; used 2 x 4 x 66 = 528 in³
        // = 3.666… -> 3.7; waste 48 in³ = 0.333… -> 0.3.
        ImmutableArray<CutListRow> cut = CutList.Of(
            Design.WithParts(TwoByFour("Rail", 36), TwoByFour("Stile", 30)),
            MaterialsLibrary.Shipped);
        ShoppingListRow row = Assert.Single(ShoppingList.Of(cut));

        Assert.Equal(2, cut.Length);
        Assert.Equal(ShoppingListKind.Boards, row.Kind);
        Assert.Equal("2x4", row.Material);
        Assert.Equal([new BoardsOfLength(new Length(72 * 1024), 1)], row.Boards);
        Assert.Equal(1, row.Count);
        Assert.Equal("1 × 6'-0\"", row.BuyText);
        Assert.Equal("4.0", row.BoughtText);
        Assert.Equal("3.7", row.UsedText);
        Assert.Equal("0.3", row.WasteText);
        Assert.Equal("Rail × 1, Stile × 1", row.For);
        Assert.Equal(string.Empty, row.Note);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void First_fit_decreasing_fills_bought_boards_before_buying_the_shortest_that_holds_the_next_piece()
    {
        // Longest first: 90" -> no board yet, shortest stocked length holding 90" is 8' (96"), 6" left.
        // 50" -> the 8' has 6" left, so a new 6' (72"), 22" left. 50" again -> neither holds it, another
        // 6', 22" left. 20" -> the first board with room is the first 6' (22" left), 2" left.
        // So 2 x 6' and 1 x 8', shortest first.
        ShoppingListRow row = Assert.Single(Shop(
            TwoByFour("Beam", 90),
            TwoByFour("Rail", 50, quantity: 2),
            TwoByFour("Block", 20)));

        Assert.Equal(
            [new BoardsOfLength(new Length(72 * 1024), 2), new BoardsOfLength(new Length(96 * 1024), 1)],
            row.Boards);
        Assert.Equal("2 × 6'-0\", 1 × 8'-0\"", row.BuyText);
        Assert.Equal(3, row.Count);

        // Bought 2 x 4 x (72 + 72 + 96) = 1920 in³ = 13.333… -> 13.3. Used 2 x 4 x 210 = 1680 in³
        // = 11.666… -> 11.7. Waste 240 in³ = 1.666… -> 1.7.
        Assert.Equal("13.3", row.BoughtText);
        Assert.Equal("11.7", row.UsedText);
        Assert.Equal("1.7", row.WasteText);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void A_piece_longer_than_any_stocked_length_is_refused_and_nothing_is_bought_for_it()
    {
        // 200" = 16'-8", longer than the longest stocked 2x4, 16' = 192". The 30" piece still gets
        // its 6' board; the long one is reported and neither bought for nor counted as used.
        ShoppingListRow row = Assert.Single(Shop(TwoByFour("Ridge", 200), TwoByFour("Block", 30)));

        Assert.Equal("1 × 6'-0\"", row.BuyText);
        Assert.Equal("no stocked size holds 1 × 16'-8\", so none is bought", row.Note);

        // Used is the 30" piece alone: 2 x 4 x 30 = 240 in³ = 1.666… -> 1.7.
        Assert.Equal("1.7", row.UsedText);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Lumber_with_no_stock_length_list_reports_its_length_and_buys_nothing()
    {
        // The shipped 5/4x6 decking row carries no length list. Two 60" pieces: 120" = 10'-0" of
        // pieces. Used, nominal 1 1/4" x 6": 1.25 x 6 x 120 = 900 in³ = 6.25 bd ft, which is exactly
        // half a tenth and rounds away from zero to 6.3.
        ShoppingListRow row = Assert.Single(Shop(Along("Deck board", 60, 5.5, 1, "5/4x6", quantity: 2)));

        Assert.Equal(ShoppingListKind.NoLengthList, row.Kind);
        Assert.Equal(0, row.Count);
        Assert.Equal(string.Empty, row.BuyText);
        Assert.Equal(string.Empty, row.BoughtText);
        Assert.Equal("6.3", row.UsedText);
        Assert.Equal(
            "napkin has read no stock-length list for this size: 10'-0\" of pieces in all, nothing bought",
            row.Note);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void A_sheet_count_is_by_area_and_says_it_is_a_floor()
    {
        // Two 30" x 60" panels are 2 x 1800 = 3600 in² against a 48" x 96" = 4608 in² sheet, so 1
        // sheet by area. No layout gets both out of one sheet (side by side is 60" across a 48"
        // sheet; end to end is 120" along a 96" one), which is exactly why the count says it is a
        // floor. The 50" x 50" piece is wider than the sheet's 48" side whichever way it is turned,
        // so it is refused and not counted.
        ShoppingListRow row = Assert.Single(Shop(
            Along("Door", 60, 30, 0.75, "3/4 plywood", quantity: 2),
            Along("Table top", 50, 50, 0.75, "3/4 plywood")));

        Assert.Equal(ShoppingListKind.Sheets, row.Kind);
        Assert.Equal(1, row.Sheets);
        Assert.Equal(1, row.Count);
        Assert.Equal("1 sheet, 4'-0\" × 8'-0\"", row.BuyText);
        Assert.Equal(
            "sheets by area — a nesting layout may need more; no stocked size holds 1 × 4'-2\" × 4'-2\", so none is bought",
            row.Note);
        Assert.Equal(string.Empty, row.UsedText);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void A_panel_longer_than_the_sheet_is_refused_even_when_narrow_enough()
    {
        // 100" x 20": the 20" fits the sheet's 48" side, but 100" is longer than its 96" side.
        ShoppingListRow row = Assert.Single(Shop(Along("Shelf", 100, 20, 0.75, "3/4 plywood", quantity: 2)));

        Assert.Equal(0, row.Sheets);
        Assert.EndsWith("no stocked size holds 2 × 8'-4\" × 1'-8\", so none is bought", row.Note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void A_panel_that_fits_turned_is_not_refused_and_the_area_is_rounded_up()
    {
        // 40" x 90" fits a 48" x 96" sheet with its long side along the sheet's. Two of them are
        // 7200 in², over one sheet's 4608: ceil(7200 / 4608) = 2 sheets.
        ShoppingListRow row = Assert.Single(Shop(Along("Side", 40, 90, 0.75, "3/4 plywood", quantity: 2)));

        Assert.Equal("2 sheets, 4'-0\" × 8'-0\"", row.BuyText);
        Assert.Equal(ShoppingList.SheetsByArea, row.Note);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Hardwood_is_board_feet_from_the_rough_thickness_and_no_piece_count()
    {
        // 4/4 is 1" rough (13/16" surfaced, which is the part's thickness). Two 36" x 6" pieces:
        // 2 x 1 x 6 x 36 = 432 in³ = 3.0 bd ft — from the rough 1", not the 13/16".
        ShoppingListRow row = Assert.Single(Shop(Along("Slat", 36, 6, 0.8125, "4/4", quantity: 2, species: "walnut")));

        Assert.Equal(ShoppingListKind.BoardFeet, row.Kind);
        Assert.Equal(0, row.Count);
        Assert.Equal("3.0", row.UsedText);
        Assert.Equal("3.0 bd ft, random widths", row.BuyText);
        Assert.Equal(string.Empty, row.WasteText);
        Assert.Equal("walnut", row.Species);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Board_feet_are_rounded_once_at_the_end_not_per_part()
    {
        // Twelve 1" 2x4 blocks: each is 2 x 4 x 1 = 8 in³ = 0.0555… bd ft, which rounds to 0.1, and
        // twelve of those would say 1.2. Summed exactly first: 96 in³ = 0.666… -> 0.7.
        ShoppingListRow row = Assert.Single(Shop(TwoByFour("Block", 1, quantity: 12)));

        Assert.Equal("0.7", row.UsedText);

        Int128 onePiece = Area.Volume(new Length(2048), new Length(4096), new Length(1024));
        Assert.Equal("0.1", BoardFeet.Text(onePiece));
        Assert.NotEqual("1.2", row.UsedText);
    }

    [Theory]
    [Trait("Feature", "CUT-006")]
    [InlineData(0, "0.0")]
    [InlineData(144, "1.0")]
    [InlineData(7, "0.0")]      // 7/144 = 0.0486… -> 0.0
    [InlineData(8, "0.1")]      // 8/144 = 0.0555… -> 0.1
    [InlineData(36, "0.3")]     // 36/144 = 0.25 -> 0.3, half away from zero
    [InlineData(1728, "12.0")]  // one cubic foot
    public void Board_feet_text_is_one_decimal_half_away_from_zero(long cubicInches, string text)
    {
        Int128 cubicUnits = cubicInches * (Int128)1024 * 1024 * 1024;

        Assert.Equal(text, BoardFeet.Text(cubicUnits));
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Stock_names_are_normalised_so_2_X_4_and_2x4_buy_from_one_bucket()
    {
        ShoppingListRow row = Assert.Single(Shop(TwoByFour("Rail", 36, stock: "2 X 4"), TwoByFour("Stile", 30, stock: "2x4")));

        Assert.Equal("2x4", row.Material);
        Assert.Equal(1, row.Count);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Each_species_is_its_own_row_and_rows_run_by_stock_name_then_species()
    {
        // Fed in reverse, so the order asserted is the shopping list's own and not the cut list's.
        ImmutableArray<CutListRow> cut = CutList.Of(
            Design.WithParts(
                TwoByFour("Rail", 36, species: "walnut"),
                Along("Shelf", 30, 12, 0.75, "3/4 plywood"),
                TwoByFour("Rail", 36, species: "oak"),
                Along("Apron", 30, 3.5, 0.75, "1x4")),
            MaterialsLibrary.Shipped);
        ImmutableArray<ShoppingListRow> rows = ShoppingList.Of(cut.Reverse());

        Assert.Equal(
            [("1x4", ""), ("2x4", "oak"), ("2x4", "walnut"), ("3/4 plywood", "")],
            rows.Select(row => (row.Material, row.Species)));
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void Parts_with_nothing_to_buy_are_listed_on_their_own_after_the_stock()
    {
        ImmutableArray<ShoppingListRow> rows = Shop(
            Along("Top", 48, 24, 0.75, stock: null),
            Along("Mystery", 30, 3, 1, stock: "4x5x6"),
            Along("Nail", 3.5, 0.25, 0.25, stock: "16d"),
            TwoByFour("Rail", 36));

        Assert.Equal(
            ["2x4", "", "4x5x6 — not in this build's materials library", "16d"],
            rows.Select(row => row.Material));
        Assert.All(rows.Skip(1), row => Assert.Equal(ShoppingListKind.NothingToBuy, row.Kind));
        Assert.Equal(
            [
                "no stock chosen, so nothing is bought for it",
                "its stock is not in this build's materials library, so nothing is bought for it",
                "a fastener is counted by a schedule, not cut to a size, so nothing is bought for it",
            ],
            rows.Skip(1).Select(row => row.Note));
        Assert.Equal(["Top × 1", "Mystery × 1", "Nail × 1"], rows.Skip(1).Select(row => row.For));
        Assert.All(rows.Skip(1), row => Assert.Equal(string.Empty, row.UsedText + row.BoughtText + row.BuyText));
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void The_same_cut_list_gives_an_equal_shopping_list()
    {
        (string, long, long, Piece)[] parts = [TwoByFour("Rail", 36), Along("Shelf", 30, 12, 0.75, "3/4 plywood")];

        ImmutableArray<ShoppingListRow> first = Shop(parts);
        ImmutableArray<ShoppingListRow> second = Shop(parts);

        Assert.Equal(first, second);
        Assert.Equal(first[0].GetHashCode(), second[0].GetHashCode());
        Assert.NotEqual(first[0], first[1]);
        Assert.NotEqual(first[0], first[0] with { Boards = [] });
        Assert.False(first[0].Equals(null));
    }
}
