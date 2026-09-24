using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Issue #98: a part typed by its nominal name is cut at the stock's actual size, and a part whose
/// stock is missing or unknown is still listed and says so.
/// </summary>
/// <remarks>
/// That each library row is the cell of its cited table is <c>Napkin.Core.Materials.Tests</c>'
/// job (<c>SoftwoodLumberGoldenTests</c>, re-read against PS 20-25 Table 3 for this issue, and
/// <c>GoldenCoverageTests</c>, which makes sure no row escapes). This takes the next link: from the
/// library row, through the name a person actually types and the stock assignment, to the cut-list
/// row — so nominal-for-actual cannot creep in between the table and the saw.
/// </remarks>
public sealed class StockSizeResolutionTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static readonly PlanAxes Flat = new(PartDimension.Length, PartDimension.Width);

    /// <summary>Every sawn-lumber item the library ships: softwood dimension, boards, timbers, decking.</summary>
    public static IEnumerable<object[]> Lumber
        => Library.Items.OfType<LumberStock>().Select(lumber => new object[] { lumber.Name });

    /// <summary>"2x4" as a person might type it: upper case, spaces round the x.</summary>
    private static string Retyped(string name) => name.ToUpperInvariant().Replace("X", " X ", StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(Lumber))]
    [Trait("Feature", "CUT-001")]
    public void A_part_typed_by_nominal_name_and_drawn_at_the_nominal_size_is_cut_at_the_actual_size(string name)
    {
        Assert.True(Library.TryFind(name, out StockItem found));
        LumberStock lumber = Assert.IsType<LumberStock>(found);

        // The classic mistake, drawn on purpose: a board lying flat, 8' long, drawn at its NOMINAL
        // width and thickness, with its stock typed the way a person types it.
        string typed = Retyped(name);
        Part part = new(typed, Species: null, Quantity: 1, Flat);
        Box drawn = Box.AsDrawn(
            new EntityId(Guid.Parse("98000000-0000-4000-8000-000000000001")),
            LayerId.Default,
            Point2.Origin,
            Length.Feet(8),
            lumber.NominalWidth,
            lumber.NominalThickness,
            Angle.Zero) with
        {
            Name = "Board",
            Part = part,
        };
        Sketch sketch = Sketch.Empty.WithEntity(drawn);

        Assert.True(Library.TryFind(typed, out StockItem resolved), $"\"{typed}\" does not resolve.");
        Assert.Same(lumber, resolved);

        Sketch assigned = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(sketch, StockAssignment.RequestsFor(sketch, drawn, part, resolved))).Sketch;

        CutListRow row = Assert.Single(CutList.Of(assigned, Library));

        // The actual cross-section, which is the table's cell, and never the nominal one.
        Assert.Equal(lumber.Width, row.Width);
        Assert.Equal(lumber.Thickness, row.Thickness);
        Assert.True(row.Width < lumber.NominalWidth, $"{name}: listed {row.Width} wide, nominal {lumber.NominalWidth}.");
        Assert.True(row.Thickness < lumber.NominalThickness, $"{name}: listed {row.Thickness} thick, nominal {lumber.NominalThickness}.");

        // The length is the design's and the stock is named by its library spelling.
        Assert.Equal(Length.Feet(8), row.Length);
        Assert.Same(lumber, row.Stock);
        Assert.Equal(lumber.Name, row.Material);
        Assert.False(row.Unresolved);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void The_2x4_typed_every_which_way_is_one_and_a_half_by_three_and_a_half()
    {
        // PS 20-25 Table 3 p. 15, retrieved 2026-09-24 from https://www.nist.gov/document/ps-20-25-final:
        // Dimension, nominal 2 -> 1-1/2 in dry thickness; nominal 4 -> 3-1/2 in dry width.
        // 1.5 x 1024 = 1536, 3.5 x 1024 = 3584.
        foreach (string typed in new[] { "2x4", "2 X 4", "2X4", " 2 x 4 " })
        {
            Assert.True(Library.TryFindLumber(typed, out LumberStock lumber), $"\"{typed}\" does not resolve.");
            Assert.Equal(1536, lumber.Thickness.Units);
            Assert.Equal(3584, lumber.Width.Units);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void No_stock_and_an_unknown_stock_are_listed_and_marked_never_dropped()
    {
        // Three parts, three different sizes so they are three rows: one on a real stock, one with
        // no stock chosen, one on a name the library does not carry.
        Piece flat = new(null, null, 1, new Length(768), Flat);
        Sketch sketch = Design.WithParts(
            ("Stud", 36864, 3584, flat with { Stock = "2x4", Depth = new Length(1536) }),
            ("Shelf", 24576, 10240, flat),
            ("Rail", 12288, 2048, flat with { Stock = "9x17 unobtainium" }));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, Library);

        Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Sum(row => row.Quantity));

        CutListRow stud = Assert.Single(rows, row => row.Label == "Stud");
        Assert.IsType<LumberStock>(stud.Stock);
        Assert.False(stud.Unresolved);

        // No stock chosen: nothing to buy it from, nothing claimed about it, not an error.
        CutListRow shelf = Assert.Single(rows, row => row.Label == "Shelf");
        Assert.Null(shelf.Stock);
        Assert.False(shelf.Unresolved);
        Assert.Empty(shelf.Material);

        // An unknown stock: kept at the size the design states and reported as unresolved, in the
        // list and in the CSV a person prints.
        CutListRow rail = Assert.Single(rows, row => row.Label == "Rail");
        Assert.Null(rail.Stock);
        Assert.True(rail.Unresolved);
        Assert.Equal(12288, rail.Length.Units);
        Assert.Contains(
            CutListRow.UnresolvedText("9x17 unobtainium").Replace("\"", "\"\"", StringComparison.Ordinal),
            CutListCsv.ToCsv(rows),
            StringComparison.Ordinal);
    }
}
