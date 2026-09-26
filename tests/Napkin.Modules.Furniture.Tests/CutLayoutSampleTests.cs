using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The cut layout of the two stocked samples, worked out by hand (kerf 1/8 in; stocked lengths 72, 96,
/// 120, 144, 168, 192 in). The arithmetic is in each case's comments and, in prose, in the samples'
/// <c>expected.json</c> derivations.
/// </summary>
public sealed class CutLayoutSampleTests
{
    private static readonly Length Kerf = Length.Inches(0, 1, 8);

    private static CutLayoutPlan PlanOf(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));
        return CutLayout.Of(CutList.Of(Assert.IsType<Loaded>(result).Sketch, MaterialsLibrary.Shipped), Kerf);
    }

    private static PlannedBoard Board(CutLayoutPlan plan, string stock) => Assert.Single(plan.Stocks.Single(layout => layout.Stock.Name == stock).Boards);

    [Fact]
    [Trait("Feature", "CUT-015")]
    public void The_stocked_bench_is_one_12_foot_1x4_and_one_14_foot_2x4()
    {
        CutLayoutPlan plan = PlanOf("stocked-bench");

        // 1x4: 46 + 46 + 14 + 14 = 120 in, 4 pieces; 4 cuts x 1/8 = 1/2, so 120 1/2 in: over the 10'
        // (120), inside the 12' (144). Offcut 144 - 120 1/2 = 23 1/2.
        PlannedBoard oneByFour = Board(plan, "1x4");
        Assert.Equal(new Length(144 * 1024), oneByFour.StockLength);
        Assert.Equal(["Apron", "Apron", "End rail", "End rail"], oneByFour.Pieces.Select(piece => piece.Label));
        Assert.Equal(4, oneByFour.Cuts);
        Assert.Equal(Length.Inches(0, 1, 2), oneByFour.KerfTotal);
        Assert.Equal(Length.Inches(23, 1, 2), oneByFour.Offcut);

        // 2x4: 43 + 43 + 4 x 16 1/2 = 152 in, 6 pieces, 6 cuts = 3/4: 152 3/4 in: over the 12' (144),
        // inside the 14' (168). Offcut 168 - 152 3/4 = 15 1/4.
        PlannedBoard twoByFour = Board(plan, "2x4");
        Assert.Equal(new Length(168 * 1024), twoByFour.StockLength);
        Assert.Equal(6, twoByFour.Cuts);
        Assert.Equal(Length.Inches(15, 1, 4), twoByFour.Offcut);

        // The 3/4 plywood top is nested on one sheet (#26).
        Assert.Single(Assert.Single(plan.Panels).Sheets);
    }

    [Fact]
    [Trait("Feature", "CUT-015")]
    public void The_diy_coffee_table_is_a_6_foot_1x2_a_12_foot_1x6_and_a_6_foot_2x2()
    {
        CutLayoutPlan plan = PlanOf("diy-coffee-table-drawers");

        // 1x2: 36 + 16 + 16 = 68 in, 3 cuts: 68 3/8 <= 72; offcut 3 5/8.
        PlannedBoard oneByTwo = Board(plan, "1x2");
        Assert.Equal(new Length(72 * 1024), oneByTwo.StockLength);
        Assert.Equal(3, oneByTwo.Cuts);
        Assert.Equal(Length.Inches(3, 5, 8), oneByTwo.Offcut);

        // 1x6: 36 + 2 x 17 3/4 + 17 1/2 + 2 x 16 = 121 in, 6 cuts: 121 3/4 > 120, <= 144; offcut 22 1/4.
        PlannedBoard oneBySix = Board(plan, "1x6");
        Assert.Equal(new Length(144 * 1024), oneBySix.StockLength);
        Assert.Equal(6, oneBySix.Cuts);
        Assert.Equal(Length.Inches(22, 1, 4), oneBySix.Offcut);

        // 2x2: 4 x 16 1/4 = 65 in, 4 cuts: 65 1/2 <= 72; offcut 6 1/2.
        PlannedBoard twoByTwo = Board(plan, "2x2");
        Assert.Equal(new Length(72 * 1024), twoByTwo.StockLength);
        Assert.Equal(Length.Inches(6, 1, 2), twoByTwo.Offcut);
    }

    /// <summary>
    /// Each sheet of the sample's plywood against its <c>sheetLayout</c>, worked out by hand in the
    /// sample's <c>expected.json</c> (strip by strip, piece by piece, with the arithmetic in each
    /// sheet's <c>derivation</c>) — and the shopping list buys exactly those sheets.
    /// </summary>
    [Theory]
    [InlineData("stocked-bench")]
    [InlineData("diy-coffee-table-drawers")]
    [Trait("Feature", "CUT-007")]
    public void A_samples_sheets_are_the_ones_worked_out_by_hand(string fixture)
    {
        JsonElement expected;
        using (FileStream stream = File.OpenRead(Path.Combine(ExpectedFixture.Directory, $"{fixture}.expected.json")))
        {
            expected = JsonDocument.Parse(stream).RootElement.GetProperty("sheetLayout").Clone();
        }

        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));
        ImmutableArray<CutListRow> rows = CutList.Of(Assert.IsType<Loaded>(result).Sketch, MaterialsLibrary.Shipped);
        Length kerf = new(expected.GetProperty("kerfUnits").GetInt64());
        CutLayoutPlan plan = CutLayout.Of(rows, kerf);
        ImmutableArray<CutLayoutRow> lines = CutLayout.Rows(plan);
        ImmutableArray<string> summary = CutLayout.Summary(plan);
        ImmutableArray<ShoppingListRow> shopping = ShoppingList.Of(rows, kerf);

        JsonElement[] panels = [.. expected.GetProperty("panels").EnumerateArray()];
        Assert.Equal(panels.Length, plan.Panels.Length);
        for (int p = 0; p < panels.Length; p++)
        {
            PanelLayout layout = plan.Panels[p];
            JsonElement panel = panels[p];
            Assert.Equal(panel.GetProperty("material").GetString(), layout.Panel.Name);
            Assert.Equal(panel.GetProperty("species").GetString(), layout.Species);
            Assert.Equal(panel.GetProperty("refused").GetInt32(), layout.Refused.Length);
            Assert.Contains(panel.GetProperty("summary").GetString(), summary);

            JsonElement[] sheets = [.. panel.GetProperty("sheets").EnumerateArray()];
            Assert.Equal(sheets.Length, layout.Sheets.Length);
            Assert.Equal(sheets.Length, Assert.Single(shopping, row => row.Material == layout.Panel.Name && row.Species == layout.Species).Sheets);

            for (int s = 0; s < sheets.Length; s++)
            {
                PlannedSheet sheet = layout.Sheets[s];
                JsonElement want = sheets[s];
                Assert.Equal(want.GetProperty("number").GetInt32(), sheet.Number);
                Assert.Equal(want.GetProperty("longUnits").GetInt64(), sheet.Long.Units);
                Assert.Equal(want.GetProperty("shortUnits").GetInt64(), sheet.Short.Units);
                Assert.Equal(want.GetProperty("rips").GetInt32(), sheet.Rips);
                Assert.Equal(want.GetProperty("cuts").GetInt32(), sheet.Cuts);
                Assert.Equal((Int128)want.GetProperty("wasteSquareUnits").GetInt64(), sheet.WasteArea);
                Assert.False(string.IsNullOrWhiteSpace(want.GetProperty("derivation").GetString()));

                JsonElement[] strips = [.. want.GetProperty("strips").EnumerateArray()];
                Assert.Equal(strips.Length, sheet.Strips.Length);
                for (int t = 0; t < strips.Length; t++)
                {
                    SheetStrip strip = sheet.Strips[t];
                    JsonElement wantStrip = strips[t];
                    Assert.Equal(wantStrip.GetProperty("yUnits").GetInt64(), strip.Y.Units);
                    Assert.Equal(wantStrip.GetProperty("widthUnits").GetInt64(), strip.Width.Units);
                    Assert.Equal(wantStrip.GetProperty("crosscuts").GetInt32(), strip.Crosscuts);
                    Assert.Equal(wantStrip.GetProperty("trims").GetInt32(), strip.Trims);
                    Assert.Equal(
                        wantStrip.GetProperty("pieces").EnumerateArray().Select(piece => (
                            piece.GetProperty("label").GetString(),
                            piece.GetProperty("alongUnits").GetInt64(),
                            piece.GetProperty("acrossUnits").GetInt64(),
                            piece.GetProperty("turned").GetBoolean(),
                            piece.GetProperty("xUnits").GetInt64(),
                            strip.Y.Units)),
                        strip.Pieces.Select(piece => ((string?)piece.Label, piece.Along.Units, piece.Across.Units, piece.Turned, piece.X.Units, piece.Y.Units)));
                }

                CutLayoutRow line = Assert.Single(lines, row => ReferenceEquals(row.Sheet, sheet));
                Assert.Equal(want.GetProperty("line").GetString(), CutLayout.Line(CutLayout.Fields(line)));
            }
        }
    }
}
