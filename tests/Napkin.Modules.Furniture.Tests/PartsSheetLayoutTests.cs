using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>The Parts sheet's grid (docs/design/parts-view.md §4.1, §7.2), worked by hand.</summary>
public sealed class PartsSheetLayoutTests
{
    private static ImmutableArray<PartsCell> Sheet(string fixture) =>
        PartsSheet.Of(CutList.Of(Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture))).Sketch, MaterialsLibrary.Shipped));

    [Theory]
    // (w − 2·16 + 12) / (256 + 12) at 100 %, (w − 64 + 24) / (512 + 24) at 200 %, floored, at least one.
    [InlineData(300, 1.0, 1)]
    [InlineData(560, 1.0, 2)]
    [InlineData(1100, 1.0, 4)]
    [InlineData(300, 2.0, 1)]
    [InlineData(560, 2.0, 1)]
    [InlineData(1100, 2.0, 1)]
    [InlineData(1200, 2.0, 2)]
    [InlineData(40, 1.0, 1)]
    [InlineData(0, 0.5, 1)]
    [Trait("Feature", "CUT-022")]
    public void The_columns_are_what_the_width_buys_and_never_fewer_than_one(double width, double zoom, int columns)
    {
        Assert.Equal(columns, PartsSheetLayout.Columns(width, zoom));
    }

    [Fact]
    [Trait("Feature", "CUT-022")]
    public void Cells_fill_rows_in_the_sheets_order_and_never_overlap()
    {
        // The coffee table's four cells at 560 px: two to a row, Top and Apron long above Leg and
        // Apron short, at x 16 and 284, y 16 and 228.
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        IReadOnlyList<PartsPlacement> placed = PartsSheetLayout.Arrange(cells, 560, 1);

        Assert.Equal(cells, placed.Select(placement => placement.Cell!));
        Assert.Equal([(16.0, 16.0), (284.0, 16.0), (16.0, 228.0), (284.0, 228.0)], placed.Select(placement => (placement.Left, placement.Top)));
        Assert.All(placed, placement => Assert.Equal((256.0, 200.0), (placement.Width, placement.Height)));
        Assert.All(placed, placement => Assert.Null(placement.Title));
        for (int i = 0; i < placed.Count; i++)
        {
            for (int j = i + 1; j < placed.Count; j++)
            {
                PartsPlacement a = placed[i], b = placed[j];
                Assert.False(a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom, $"{i} and {j} overlap.");
            }
        }

        Assert.Equal(228 + 200 + 16, PartsSheetLayout.Height(placed, 1));
    }

    [Fact]
    [Trait("Feature", "CUT-022")]
    public void A_group_starts_its_own_row_under_its_title_band()
    {
        // The stocked bench grouped, at 560 px (two columns): "3/4 plywood" [Top]; "1x4" [Apron,
        // End rail]; "2x4" [Stretcher, Leg]. Each band is 24 px, each group's rows follow it, and the
        // next band starts a gutter below the last cell.
        ImmutableArray<PartsGroup> groups = PartsSheet.GroupedByStock(Sheet("stocked-bench"));
        IReadOnlyList<PartsPlacement> placed = PartsSheetLayout.Arrange(groups, 560, 1);

        Assert.Equal(["3/4 plywood", null, "1x4", null, null, "2x4", null, null], placed.Select(placement => placement.Title));
        Assert.Equal([16.0, 40.0, 252.0, 276.0, 276.0, 488.0, 512.0, 512.0], placed.Select(placement => placement.Top));
        Assert.Equal((16.0, 24.0, 256.0 * 2 + 12), (placed[0].Left, placed[0].Height, placed[0].Width));
        Assert.Equal(512 + 200 + 16, PartsSheetLayout.Height(placed, 1));
    }

    [Fact]
    [Trait("Feature", "CUT-022")]
    public void A_zoom_scales_everything_and_every_edge_lands_on_a_whole_pixel()
    {
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        IReadOnlyList<PartsPlacement> placed = PartsSheetLayout.Arrange(cells, 900, 0.75);

        // At 75 %: cells 192 × 150, gutter 9, margin 12; (900 − 24 + 9) / 201 = 4.4, so one row of four.
        Assert.Equal(4, PartsSheetLayout.Columns(900, 0.75));
        Assert.All(placed, placement =>
        {
            Assert.Equal(Math.Round(placement.Left), placement.Left);
            Assert.Equal(Math.Round(placement.Top), placement.Top);
            Assert.Equal(Math.Round(placement.Right), placement.Right);
            Assert.Equal(Math.Round(placement.Bottom), placement.Bottom);
        });
        Assert.Equal([12.0, 213.0, 414.0, 615.0], placed.Select(placement => placement.Left));

        // At 110 % a cell is 281.6 px and the margin 17.6: nothing lands on a whole pixel unless snapped.
        Assert.All(PartsSheetLayout.Arrange(cells, 900, 1.1), placement =>
            Assert.Equal((Math.Round(placement.Left), Math.Round(placement.Right)), (placement.Left, placement.Right)));
    }

    [Fact]
    public void The_same_cells_width_and_zoom_give_the_same_sheet_and_an_empty_one_is_its_margins()
    {
        ImmutableArray<PartsCell> cells = Sheet("stocked-bench");
        Assert.Equal(PartsSheetLayout.Arrange(cells, 700, 1.5), PartsSheetLayout.Arrange(cells, 700, 1.5));

        Assert.Empty(PartsSheetLayout.Arrange(ImmutableArray<PartsCell>.Empty, 700, 1));
        Assert.Equal(32, PartsSheetLayout.Height([], 1));
        Assert.Equal(64, PartsSheetLayout.Height([], 2));
    }

    [Fact]
    public void A_zoom_outside_fifty_to_three_hundred_percent_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PartsSheetLayout.Columns(500, 0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartsSheetLayout.Columns(500, 3.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartsSheetLayout.Columns(500, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartsSheetLayout.Height([], 0));
        Assert.Throws<ArgumentNullException>(() => PartsSheetLayout.Arrange((IReadOnlyList<PartsCell>)null!, 500, 1));
        Assert.Throws<ArgumentNullException>(() => PartsSheetLayout.Arrange((IReadOnlyList<PartsGroup>)null!, 500, 1));
        Assert.Throws<ArgumentNullException>(() => PartsSheetLayout.Height(null!, 1));
    }

    [Fact]
    [Trait("Feature", "CUT-022")]
    public void A_cell_reads_to_a_screen_reader_as_its_row_does()
    {
        PartsCell leg = Sheet("stocked-bench").Single(cell => cell.Row.Label == "Leg");
        CutListRow row = leg.Row;
        Assert.Equal(
            $"Leg, ×4, {CutListCsv.Text(row.Length)} × {CutListCsv.Text(row.Width)} × {CutListCsv.Text(row.Thickness)}, 2x4",
            PartsCellText.AutomationName(leg));

        PartsCell coffeeLeg = Sheet("coffee-table").Single(cell => cell.Row.Label == "Leg");
        Assert.StartsWith("Leg, ×4, ", PartsCellText.AutomationName(coffeeLeg), StringComparison.Ordinal);
        Assert.EndsWith(CutListCsv.Text(Length.Inches(2, 1, 2)), PartsCellText.AutomationName(coffeeLeg), StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => PartsCellText.AutomationName(null!));
    }
}
