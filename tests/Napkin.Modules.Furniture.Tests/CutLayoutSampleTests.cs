using System.Collections.Immutable;

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
    [Trait("Feature", "CUT-007")]
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

        Assert.Contains(CutLayout.SheetGoodsNote, plan.Notes);
    }

    [Fact]
    [Trait("Feature", "CUT-007")]
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
}
