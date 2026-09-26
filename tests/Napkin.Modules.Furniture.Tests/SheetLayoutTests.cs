using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Sheet-goods nesting (#26), worked by hand on the shipped 3/4 plywood's 48 × 96 in sheet (the
/// shopping-list tests' sheet). Strips are ripped along the 96" side and pieces crosscut from them;
/// the kerf is 1/8 in unless a test says otherwise.
/// </summary>
public sealed class SheetLayoutTests
{
    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);
    private static readonly Length Kerf = Length.Inches(0, 1, 8);

    private static long In(double inches) => (long)(inches * 1024);

    private static Length Inches(double inches) => new(In(inches));

    private static (string, long, long, Piece) Panel(string name, double length, double width, int quantity = 1)
        => (name, In(length), In(width), new Piece("3/4 plywood", null, quantity, new Length(768), Flat));

    private static ImmutableArray<CutListRow> Rows(params (string, long, long, Piece)[] parts)
        => CutList.Of(Design.WithParts(parts), MaterialsLibrary.Shipped);

    private static PanelLayout Lay(IEnumerable<CutListRow> rows, Length? kerf = null)
    {
        CutListRow[] members = [.. rows];
        return SheetLayout.Of((PanelStock)members[0].Stock!, string.Empty, members, kerf ?? Kerf);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void One_piece_is_one_strip_its_long_side_along_the_sheet()
    {
        // 24 x 48: the 48 runs along the 96. One strip 24 wide: 1 rip (24 + 0 kerfs < 48, so an offcut
        // remains), 1 crosscut (48 < 96), no trim: 2 cuts. Waste 4608 - 1152 = 3456 sq in = 24.0 sq ft.
        PanelLayout layout = Lay(Rows(Panel("Top", 24, 48)));

        PlannedSheet sheet = Assert.Single(layout.Sheets);
        SheetStrip strip = Assert.Single(sheet.Strips);
        SheetPiece piece = Assert.Single(strip.Pieces);
        Assert.Equal((Inches(48), Inches(24), true), (piece.Along, piece.Across, piece.Turned));
        Assert.Equal((Length.Zero, Length.Zero), (piece.X, piece.Y));
        Assert.Equal(Inches(24), strip.Width);
        Assert.Equal((1, 1, 0, 2), (sheet.Rips, strip.Crosscuts, strip.Trims, sheet.Cuts));
        Assert.Equal("24.0 sq ft", SheetLayout.SquareFeet(sheet.WasteArea));
        Assert.Empty(layout.Refused);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Two_pieces_whose_area_fits_one_sheet_can_need_two()
    {
        // 30 x 60 twice is 3600 sq in, under one sheet's 4608 — the old area count said 1. Laid out:
        // strip 1 is 30 wide; the second 60 does not fit beside the first (60 + 60 > 96), and a second
        // 30 strip does not fit across (30 + 1/8 + 30 > 48). So a second sheet.
        PanelLayout layout = Lay(Rows(Panel("Side", 60, 30, quantity: 2)));

        Assert.Equal(2, layout.Sheets.Length);
        Assert.All(layout.Sheets, sheet => Assert.Single(Assert.Single(sheet.Strips).Pieces));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void Pieces_share_a_strip_with_a_kerf_between_and_a_narrower_piece_is_trimmed()
    {
        // Widest across first: 20 x 40 (across 20), then 16 x 30 (across 16). The 30 joins the 20"
        // strip: 40 + 30 = 70, 2 pieces, 70 + 1/8 < 96, so X = 40 1/8. 2 crosscuts (an offcut remains),
        // 1 trim (16 < 20), 1 rip: 4 cuts.
        PanelLayout layout = Lay(Rows(Panel("Shelf", 30, 16), Panel("Side", 40, 20)));

        PlannedSheet sheet = Assert.Single(layout.Sheets);
        SheetStrip strip = Assert.Single(sheet.Strips);
        Assert.Equal(["Side", "Shelf"], strip.Pieces.Select(piece => piece.Label));
        Assert.Equal(Length.Inches(40, 1, 8), strip.Pieces[1].X);
        Assert.Equal((2, 1, 1, 4), (strip.Crosscuts, strip.Trims, sheet.Rips, sheet.Cuts));
        Assert.Equal("strip 1, 20 in: Side 40 × 20 in + Shelf 30 × 16 in", SheetLayout.Pieces(sheet));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_second_strip_starts_a_kerf_past_the_first_and_exact_fills_take_one_cut_fewer()
    {
        // Three 16 x 96 pieces with no kerf: each fills a strip's length exactly (0 crosscuts, n-1 = 0),
        // and three strips of 16 fill 48 exactly: 2 rips. With a 1/8 kerf, 16 + 1/8 + 16 + 1/8 + 16 =
        // 48 1/4 > 48, so the third piece goes to a second sheet.
        ImmutableArray<CutListRow> rows = Rows(Panel("Slat", 96, 16, quantity: 3));

        PlannedSheet exact = Assert.Single(Lay(rows, Length.Zero).Sheets);
        Assert.Equal(3, exact.Strips.Length);
        Assert.Equal(Inches(32), exact.Strips[2].Y);
        Assert.Equal((2, 2), (exact.Rips, exact.Cuts));

        PanelLayout kerfed = Lay(rows);
        Assert.Equal(2, kerfed.Sheets.Length);
        Assert.Equal(Length.Inches(16, 1, 8), kerfed.Sheets[0].Strips[1].Y);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_with_its_grain_set_keeps_the_grain_along_the_sheet_and_is_never_turned()
    {
        // 30 long x 40 wide, grain along its length: the 30 must run along the sheet, so across is 40.
        CutListRow row = Assert.Single(Rows(Panel("Door", 30, 40))) with { Grain = PartDimension.Length };

        SheetPiece piece = Assert.Single(Assert.Single(Lay([row]).Sheets).Pieces);
        Assert.Equal((Inches(30), Inches(40), false), (piece.Along, piece.Across, piece.Turned));

        // Grain along its width: the 40 runs along, turned.
        CutListRow across = row with { Grain = PartDimension.Width };
        SheetPiece turned = Assert.Single(Assert.Single(Lay([across]).Sheets).Pieces);
        Assert.Equal((Inches(40), Inches(30), true), (turned.Along, turned.Across, turned.Turned));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_that_fits_only_against_its_grain_is_refused_for_that_reason()
    {
        // 90 x 40 with its grain along the 40: the 40 must run along, so 90 across a 48 sheet. Turned
        // it would fit; the grain forbids it.
        CutListRow row = Assert.Single(Rows(Panel("Panel", 90, 40))) with { Grain = PartDimension.Width };

        PanelLayout layout = Lay([row]);

        Assert.Empty(layout.Sheets);
        RefusedSheetPiece refused = Assert.Single(layout.Refused);
        Assert.True(refused.AgainstGrain);

        ImmutableArray<CutLayoutRow> lines = CutLayout.Rows(CutLayout.Of([row], Kerf));
        Assert.Equal("No sheet: 3/4 plywood: Panel 90 in × 40 in: fits only turned, against the grain set on it, so none is bought", CutLayout.Line(CutLayout.Fields(Assert.Single(lines))));

        ShoppingListRow shop = Assert.Single(ShoppingList.Of([row]));
        Assert.Equal(0, shop.Sheets);
        Assert.Contains("1 × 7'-6\" × 3'-4\" fit only turned, against the grain set on them", shop.Note, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_whose_grain_runs_its_length_is_refused_when_only_turned_it_fits()
    {
        // 40 long x 90 wide, grain along its 40: the 40 runs along, 90 across a 48 sheet. Turned
        // (90 along, 40 across) it would fit.
        CutListRow row = Assert.Single(Rows(Panel("Panel", 40, 90))) with { Grain = PartDimension.Length };

        Assert.True(Assert.Single(Lay([row]).Refused).AgainstGrain);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_summary_names_the_species_and_counts_sheets_and_pieces_in_the_plural()
    {
        // 60 x 30 twice: two sheets, one piece each (as above). Waste 2 x (4608 - 1800) = 5616 sq in
        // = 39.0 sq ft of 9216 = 60.9375 % -> 60.9 %.
        ImmutableArray<CutListRow> rows = [.. Rows(Panel("Side", 60, 30, quantity: 2)).Select(row => row with { Species = "Birch" })];

        Assert.Equal(
            "3/4 plywood (Birch): 2 sheets; 2 pieces, waste 39.0 sq ft (60.9%) counting offcuts and kerf",
            CutLayout.Summary(CutLayout.Of(rows, Kerf))[0]);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void A_piece_larger_than_the_sheet_either_way_is_refused_as_too_big()
    {
        PanelLayout layout = Lay(Rows(Panel("Bed", 100, 50)));

        Assert.False(Assert.Single(layout.Refused).AgainstGrain);
        Assert.Equal(
            "No sheet: 3/4 plywood: Bed 100 in × 50 in: no stocked size holds it, so none is bought",
            CutLayout.Line(CutLayout.Fields(Assert.Single(CutLayout.Rows(CutLayout.Of(Rows(Panel("Bed", 100, 50)), Kerf))))));
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_same_parts_always_give_the_same_layout()
    {
        ImmutableArray<CutListRow> rows = Rows(Panel("A", 30, 16, 3), Panel("B", 40, 20, 2), Panel("C", 60, 12));

        string once = string.Join("\n", CutLayout.Rows(CutLayout.Of(rows, Kerf)).Select(row => CutLayout.Line(CutLayout.Fields(row))));
        string again = string.Join("\n", CutLayout.Rows(CutLayout.Of(rows, Kerf)).Select(row => CutLayout.Line(CutLayout.Fields(row))));

        Assert.Equal(once, again);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_cut_layout_lists_each_sheet_and_the_summary_gives_sheets_and_waste()
    {
        // 24 x 48 as above: one sheet, 2 cuts, waste 24.0 sq ft of 32.0 = 75.0 %.
        CutLayoutPlan plan = CutLayout.Of(Rows(Panel("Top", 24, 48)), Kerf);

        CutLayoutRow row = Assert.Single(CutLayout.Rows(plan));
        Assert.NotNull(row.Sheet);
        Assert.Null(row.Planned);
        Assert.Equal("Sheet 1: 3/4 plywood x 4 ft × 8 ft: strip 1, 24 in: Top 48 × 24 in (turned) | 2 cuts, kerf 1/8 in | offcut 24.0 sq ft", CutLayout.Line(CutLayout.Fields(row)));
        Assert.Equal(
            ["3/4 plywood: 1 sheet; 1 piece, waste 24.0 sq ft (75.0%) counting offcuts and kerf", SheetLayout.Statement],
            CutLayout.Summary(plan));
        Assert.Single(plan.AllSheets);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
    public void The_shopping_list_buys_the_sheets_the_layout_lays_out()
    {
        ShoppingListRow row = Assert.Single(ShoppingList.Of(Rows(Panel("Side", 60, 30, quantity: 2))));

        Assert.Equal(2, row.Sheets);
        Assert.Equal(SheetLayout.SheetsByLayout, row.Note);
    }

    [Fact]
    public void A_negative_kerf_is_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Lay(Rows(Panel("Top", 24, 48)), new Length(-1)));

    [Theory]
    [InlineData(0, "0.0 sq ft")]
    [InlineData(144L * 1024 * 1024, "1.0 sq ft")]
    // 0.05 sq ft rounds half up to 0.1.
    [InlineData((144L * 1024 * 1024 / 20) + 1, "0.1 sq ft")]
    [InlineData(144L * 1024 * 1024 / 20, "0.0 sq ft")]
    public void Square_feet_are_to_a_tenth_rounded_half_up(long area, string expected) =>
        Assert.Equal(expected, SheetLayout.SquareFeet(area));
}
