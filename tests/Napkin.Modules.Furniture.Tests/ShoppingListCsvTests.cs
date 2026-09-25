using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>The shopping list's export: exactly the rows, in the same order, with the same text.</summary>
public sealed class ShoppingListCsvTests
{
    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);

    private static ImmutableArray<ShoppingListRow> Rows() => ShoppingList.Of(CutList.Of(
        Design.WithParts(
            ("Rail", 36 * 1024, 3584, new Piece("2x4", null, 2, new Length(1536), Flat)),
            ("Shelf", 30 * 1024, 12 * 1024, new Piece("3/4 plywood", null, 1, new Length(768), Flat)),
            ("Top", 48 * 1024, 24 * 1024, new Piece(null, null, 1, new Length(768), Flat))),
        MaterialsLibrary.Shipped));

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void The_export_parses_back_to_the_rows_it_came_from_row_for_row()
    {
        ImmutableArray<ShoppingListRow> rows = Rows();

        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(ShoppingListCsv.ToCsv(rows));

        Assert.Equal([ShoppingList.Statement(CutLayout.DefaultKerf)], lines[0]);
        Assert.Equal(ShoppingListCsv.Header.Split(','), lines[1]);
        Assert.Equal(rows.Length + 2, lines.Length);

        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(ShoppingListCsv.Fields(rows[i]), lines[i + 2]);
        }

        // The fields that carry a comma, a quote or a dash survive: "1 × 6'-0"" has a quote, and the
        // sheet row's buy text has a comma.
        Assert.Equal("2x4", lines[2][0]);
        // Two 36" rails: 72 in of pieces + 2 cuts x 1/8 = 72 1/4 in, which a 6' (72 in) does not hold,
        // so the 8'. (Before the kerf was planned this was a 6'.)
        Assert.Equal("1 × 8'-0\"", lines[2][2]);
        Assert.Equal("1 sheet, 4'-0\" × 8'-0\"", lines[3][2]);
        Assert.Equal(ShoppingList.SheetsByArea, lines[3][7]);
        Assert.Equal("no stock chosen, so nothing is bought for it", lines[4][7]);
    }

    [Fact]
    [Trait("Feature", "CUT-006")]
    public void The_header_says_the_list_is_before_kerf_defect_and_joinery()
    {
        string csv = ShoppingListCsv.ToCsv([]);

        // The statement has commas in it, so it is one quoted field on a line of its own.
        Assert.Equal($"\"{ShoppingList.Statement(CutLayout.DefaultKerf)}\"\n{ShoppingListCsv.Header}\n", csv);
        Assert.Contains("kerf", ShoppingList.Statement(CutLayout.DefaultKerf), StringComparison.Ordinal);
        Assert.Contains("joinery", ShoppingList.Statement(CutLayout.DefaultKerf), StringComparison.Ordinal);
        Assert.DoesNotContain('\r', ShoppingListCsv.ToCsv(Rows()));
    }
}
