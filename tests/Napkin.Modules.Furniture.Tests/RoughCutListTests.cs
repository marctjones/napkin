using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Rough parts on the lists (<c>docs/design/sketch-mode.md</c> &#xA7;5, &#xA7;7.3) and the stock Firm up
/// suggests for them (&#xA7;3.3). Every stock size here is read from the library this build ships,
/// never typed into the test.
/// </summary>
public sealed class RoughCutListTests
{
    private static readonly PlanAxes LengthWidth = new(PartDimension.Length, PartDimension.Width);

    private static StockItem Item(string name)
    {
        Assert.True(MaterialsLibrary.Shipped.TryFind(name, out StockItem item), $"{name} is not in the library.");
        return item;
    }

    private static int next;

    // A box lying as drawn, x by y, depth z, in 1/1024 units.
    private static Box Part(string name, Length x, Length y, Length z, Part part)
        => new(new EntityId(new Guid(++next, 0, 0, new byte[8])), LayerId.Default, Point3.Inches(0, 0, 0), x, y, z, BoxFace.Top, Angle.Zero)
        {
            Name = name,
            Part = part,
        };

    private static Sketch Of(params Box[] boxes) => boxes.Aggregate(Sketch.Empty, (sketch, box) => sketch.WithEntity(box));

    [Fact]
    [Trait("Feature", "CUT-018")]
    public void A_rough_leg_and_a_firm_leg_of_equal_sizes_are_one_rough_row_of_two()
    {
        Part firm = new(null, null, 1, LengthWidth);
        Sketch sketch = Of(
            Part("Leg 1", Length.Inches(16), Length.Inches(4), Length.Inches(0, 3, 4), firm with { Rough = true }),
            Part("Leg 2", Length.Inches(16), Length.Inches(4), Length.Inches(0, 3, 4), firm));

        CutListRow row = Assert.Single(CutList.Of(sketch, MaterialsLibrary.Shipped));

        Assert.Equal(2, row.Quantity);
        Assert.True(row.Rough);
        Assert.Equal("1 row is rough — sizes as drawn, stock not chosen", CutList.RoughFooter([row]));
    }

    [Fact]
    [Trait("Feature", "CUT-018")]
    public void Firm_rows_are_not_rough_and_have_no_footer()
    {
        Part firm = new(null, null, 1, LengthWidth);
        ImmutableArray<CutListRow> rows = CutList.Of(
            Of(Part("Top", Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4), firm)),
            MaterialsLibrary.Shipped);

        Assert.False(Assert.Single(rows).Rough);
        Assert.Null(CutList.RoughFooter(rows));
        Assert.Null(ShoppingList.RoughFooter(rows));
        Assert.Equal(CutList.BeforeKerfAndJoinery, CutListCsv.Parse(CutListCsv.ToCsv(rows))[0][0]);
    }

    [Fact]
    [Trait("Feature", "CUT-018")]
    public void The_csv_says_yes_on_rough_rows_only_and_the_first_line_says_they_are_as_drawn()
    {
        Part plank = new(null, null, 1, LengthWidth);
        ImmutableArray<CutListRow> rows = CutList.Of(
            Of(
                Part("Top", Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4), plank with { Rough = true }),
                Part("Shelf", Length.Inches(30), Length.Inches(10), Length.Inches(0, 3, 4), plank)),
            MaterialsLibrary.Shipped);

        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(CutListCsv.ToCsv(rows));

        Assert.Equal("Cut list: finished sizes: joinery allowances included; before saw kerf (#138), rough rows are as drawn.", Assert.Single(lines[0]));
        Assert.Equal("Rough", lines[1][6]);
        Assert.Equal(["Top", "yes"], new[] { lines[2][0], lines[2][6] });
        Assert.Equal(["Shelf", string.Empty], new[] { lines[3][0], lines[3][6] });
        Assert.Equal("1 row is rough — sizes as drawn, stock not chosen", CutList.RoughFooter(rows));
    }

    [Fact]
    [Trait("Feature", "CUT-018")]
    public void The_shopping_list_counts_the_rough_pieces_it_leaves_out()
    {
        Part plank = new(null, null, 1, LengthWidth);
        ImmutableArray<CutListRow> rows = CutList.Of(
            Of(
                Part("Leg", Length.Inches(16), Length.Inches(4), Length.Inches(0, 3, 4), plank with { Rough = true, Quantity = 3 }),
                Part("Top", Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4), plank with { Rough = true }),
                // Rough with stock is counted as its stock, so it is not in the footer.
                Part("Rail", Length.Inches(30), ((LumberStock)Item("2x4")).Width, ((LumberStock)Item("2x4")).Thickness, new Part("2x4", null, 1, LengthWidth) { Rough = true })),
            MaterialsLibrary.Shipped);

        Assert.Equal("2 rows are rough — sizes as drawn, stock not chosen", CutList.RoughFooter(rows.Where(row => row.Stock is null)));
        Assert.Equal("3 rows are rough — sizes as drawn, stock not chosen", CutList.RoughFooter(rows));
        Assert.Equal("4 parts are rough and have no stock; they are not on this list.", ShoppingList.RoughFooter(rows));
        Assert.Equal(
            "1 part is rough and has no stock; it is not on this list.",
            ShoppingList.RoughFooter(rows.Where(row => row.Label == "Top")));
    }

    [Fact]
    [Trait("Feature", "CUT-018")]
    public void Rough_is_part_of_a_rows_equality()
    {
        CutListRow row = Assert.Single(CutList.Of(
            Of(Part("Top", Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4), new Part(null, null, 1, LengthWidth))),
            MaterialsLibrary.Shipped));

        Assert.NotEqual(row, row with { Rough = true });
        Assert.NotEqual(row.GetHashCode(), (row with { Rough = true }).GetHashCode());
    }

    [Fact]
    [Trait("Feature", "CUT-017")]
    public void A_part_the_size_of_a_2x4_ranks_the_2x4_first_by_zero()
    {
        LumberStock twoByFour = (LumberStock)Item("2x4");

        ImmutableArray<StockItem> suggested = StockSuggestion.For(
            new FinishedSize(Length.Inches(48), twoByFour.Width, twoByFour.Thickness),
            MaterialsLibrary.Shipped);

        Assert.Equal("2x4", suggested[0].Name);
    }

    [Fact]
    [Trait("Feature", "CUT-017")]
    public void A_three_quarter_plank_a_foot_wide_ranks_a_board_above_a_panel_of_its_thickness()
    {
        // 48 x 12 x 3/4. The 1x12 (read below) is within an inch on both its width and its thickness;
        // a 3/4 panel matches the thickness exactly but fixes only that, so it ranks after every
        // two-dimension candidate.
        LumberStock oneByTwelve = (LumberStock)Item("1x12");
        Assert.True(Length.Abs(oneByTwelve.Width - Length.Inches(12)) <= StockSuggestion.Tolerance);
        Assert.True(Length.Abs(oneByTwelve.Thickness - Length.Inches(0, 3, 4)) <= StockSuggestion.Tolerance);
        PanelStock plywood = (PanelStock)Item("3/4 plywood");
        Assert.Equal(Length.Inches(0, 3, 4), plywood.Thickness);

        ImmutableArray<StockItem> suggested = StockSuggestion.For(
            new FinishedSize(Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4)),
            MaterialsLibrary.Shipped);

        Assert.IsType<LumberStock>(suggested[0]);
        Assert.Contains(plywood, suggested);
        int firstPanel = suggested.IndexOf(suggested.First(item => item is PanelStock));
        Assert.True(firstPanel > 0);
        Assert.All(suggested.Take(firstPanel), item => Assert.Equal(2, StockAssignment.Fixes(item).Length));
        Assert.All(suggested.Skip(firstPanel), item => Assert.Single(StockAssignment.Fixes(item)));
        Assert.Contains(oneByTwelve, suggested.Take(firstPanel));
    }

    [Fact]
    [Trait("Feature", "CUT-017")]
    public void Equal_distances_break_on_the_fewer_differences_then_the_name()
    {
        // A strip 36 x 3 x 3/4: the library's 1x3 and 1x4 are each some distance from 3" wide and
        // from 3/4" thick; whichever is nearer in total comes first, and a tie goes to the name.
        LumberStock oneByThree = (LumberStock)Item("1x3");
        LumberStock oneByFour = (LumberStock)Item("1x4");
        long Off(LumberStock stock) => Length.Abs(stock.Width - Length.Inches(3)).Units + Length.Abs(stock.Thickness - Length.Inches(0, 3, 4)).Units;

        ImmutableArray<StockItem> suggested = StockSuggestion.For(
            new FinishedSize(Length.Inches(36), Length.Inches(3), Length.Inches(0, 3, 4)),
            MaterialsLibrary.Shipped);

        int three = suggested.IndexOf(oneByThree);
        int four = suggested.IndexOf(oneByFour);
        Assert.True(three >= 0 && four >= 0);
        Assert.Equal(Off(oneByThree) < Off(oneByFour) || (Off(oneByThree) == Off(oneByFour) && string.CompareOrdinal("1x3", "1x4") < 0), three < four);
    }

    [Fact]
    [Trait("Feature", "CUT-017")]
    public void A_part_near_nothing_gets_no_suggestion_and_a_fastener_never_appears()
    {
        // 60 x 20 x 5: no lumber is near 20" wide, and no panel or hardwood thickness is within an inch of 5".
        FinishedSize size = new(Length.Inches(60), Length.Inches(20), Length.Inches(5));
        Assert.All(MaterialsLibrary.Shipped.Items.OfType<LumberStock>(), lumber => Assert.True(Length.Abs(lumber.Width - size.Width) > StockSuggestion.Tolerance));
        Assert.All(MaterialsLibrary.Shipped.Items.OfType<PanelStock>(), panel => Assert.True(Length.Abs(panel.Thickness - size.Thickness) > StockSuggestion.Tolerance));
        Assert.All(MaterialsLibrary.Shipped.Items.OfType<HardwoodStock>(), wood => Assert.True(Length.Abs(wood.SurfacedTwoSides - size.Thickness) > StockSuggestion.Tolerance));

        Assert.Empty(StockSuggestion.For(size, MaterialsLibrary.Shipped));

        // A spread of sizes: whatever comes back, no fastener is among it.
        foreach (int inches in new[] { 1, 2, 4, 8 })
        {
            FinishedSize any = new(Length.Inches(inches * 6), Length.Inches(inches), Length.Inches(0, 3, 4));
            Assert.DoesNotContain(StockSuggestion.For(any, MaterialsLibrary.Shipped), item => item is FastenerStock);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-017")]
    public void Each_fixed_dimension_must_be_within_an_inch()
    {
        // The 2x4's thickness exactly, and its width 1 1/4" off: the width is too far, so no 2x4.
        LumberStock twoByFour = (LumberStock)Item("2x4");
        ImmutableArray<StockItem> suggested = StockSuggestion.For(
            new FinishedSize(Length.Inches(48), twoByFour.Width + Length.Inches(1, 1, 4), twoByFour.Thickness),
            MaterialsLibrary.Shipped);
        Assert.DoesNotContain(twoByFour, suggested);

        // Exactly an inch off is still near.
        ImmutableArray<StockItem> edge = StockSuggestion.For(
            new FinishedSize(Length.Inches(48), twoByFour.Width + Length.Inches(1), twoByFour.Thickness),
            MaterialsLibrary.Shipped);
        Assert.Contains(twoByFour, edge);
    }
}
