using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// Issue #98: <em>every</em> stock in the shipped library is checked against the cell of its cited
/// source — not just the ones somebody remembered to write a golden row for.
/// </summary>
/// <remarks>
/// <para>
/// The golden rows themselves live beside their sources: <see cref="SoftwoodLumberGoldenTests"/>
/// (PS 20-25 Table 3), <see cref="SheetGoodsGoldenTests"/> (PS 1-19 Table 10, PS 2-18 Table 1) and
/// <see cref="HardwoodAndFastenerGoldenTests"/> (NHLA 2023 ¶14, FF-N-105B §3.6.11.2). This test only
/// asks that together they reach every item the library ships, so a row added to a data file
/// without a golden row read from its source fails here rather than going unchecked.
/// </para>
/// <para>
/// Re-read for #98 on 2026-09-24, against the same documents the citations name: PS 20-25 Table 3
/// p. 15 (<c>https://www.nist.gov/document/ps-20-25-final</c>) — every Dry cell of the softwood and
/// decking rows agrees; PS 1-19 Table 10 p. 39 — every plywood Performance Category carried is a
/// row of the table and its sanded/unsanded limits agree; PS 2-18 Table 1 p. 7 — every OSB
/// category carried is a row and its limits agree; NHLA 2023 Rules ¶14 p. 6 of the rulebook — every
/// surfaced thickness agrees. FF-N-105B was not re-read: the citation's URL needs a Referer
/// header, so the nail rows rest on the reading done in the task that wrote them.
/// </para>
/// </remarks>
public sealed class GoldenCoverageTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static IEnumerable<string> Names(IEnumerable<object[]> rows) => rows.Select(row => (string)row[0]);

    [Fact]
    [Trait("Feature", "MAT-001")]
    public void EveryShippedStockHasAGoldenRowReadFromItsSource()
    {
        IEnumerable<string> golden = Names(SoftwoodLumberGoldenTests.Rows)
            .Concat(Names(SheetGoodsGoldenTests.Rows))
            .Concat(Names(HardwoodAndFastenerGoldenTests.HardwoodRows))
            .Concat(Names(HardwoodAndFastenerGoldenTests.NailRows))
            .Concat(Names(HardwoodAndFastenerGoldenTests.FinishNailRows))
            .Concat(Names(HardwoodAndFastenerGoldenTests.BradRows));

        HashSet<string> checkedItems = [];
        foreach (string name in golden)
        {
            Assert.True(Library.TryFind(name, out StockItem item), $"Golden row {name} is not in the shipped library.");
            Assert.True(checkedItems.Add(item.Name), $"{item.Name} has two golden rows.");
        }

        string[] unchecked_ = [.. Library.Items.Select(item => item.Name).Where(name => !checkedItems.Contains(name))];
        Assert.True(unchecked_.Length == 0, $"No golden row checks: {string.Join(", ", unchecked_)}.");
        Assert.Equal(Library.Items.Length, checkedItems.Count);
    }
}
