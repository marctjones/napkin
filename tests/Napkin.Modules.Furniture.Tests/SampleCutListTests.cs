using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The coffee table's cut list, compared row for row against the expectations a person worked out
/// by hand from <c>samples/coffee-table.design.md</c> (issue #8's worked example, <c>CUT-004</c>).
/// </summary>
/// <remarks>
/// When one of these fails, the first question is which side is wrong. An expectation is changed
/// only after the arithmetic has been re-done by hand and the new derivation written into the
/// expectations file — never by copying what napkin printed (<c>samples/README.md</c>).
/// </remarks>
public sealed class SampleCutListTests
{
    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "CUT-004")]
    public void A_samples_cut_list_is_the_one_the_expectations_state(string fixture)
    {
        (ImmutableArray<CutListRow> rows, ExpectedFixture expected) = Load(fixture);

        Assert.Equal(expected.CutList.Count, rows.Length);

        for (int i = 0; i < rows.Length; i++)
        {
            CutListRow row = rows[i];
            ExpectedCutRow want = expected.CutList[i];

            Assert.Equal(want.Label, row.Label);
            Assert.Equal(want.Quantity, row.Quantity);

            // Integer units, compared exactly — never inches and never with a tolerance.
            Assert.Equal(want.LengthUnits, row.Length.Units);
            Assert.Equal(want.WidthUnits, row.Width.Units);
            Assert.Equal(want.ThicknessUnits, row.Thickness.Units);

            Assert.Equal(want.LengthText, CutListCsv.Text(row.Length));
            Assert.Equal(want.WidthText, CutListCsv.Text(row.Width));
            Assert.Equal(want.ThicknessText, CutListCsv.Text(row.Thickness));

            Assert.Equal(want.Material, row.Material);
            Assert.Equal(want.Unresolved, row.Unresolved);
            Assert.Equal(want.Members, row.Members.Select(id => id.Value.ToString("D")));

            // A fixture of plain rectangles says nothing about cuts, which is the same statement
            // as an empty list: no sentence is a sentence about nothing to do.
            Assert.Equal(want.Cuts ?? [], row.CutText);

            Assert.False(string.IsNullOrWhiteSpace(want.Derivation), $"{want.Label} has no derivation.");
        }
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "CUT-004")]
    public void A_samples_csv_is_the_one_the_expectations_state(string fixture)
    {
        (ImmutableArray<CutListRow> rows, ExpectedFixture expected) = Load(fixture);

        Assert.Equal(expected.Csv, CutListCsv.ToCsv(rows));
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_coffee_tables_four_rows_come_out_largest_first()
    {
        (ImmutableArray<CutListRow> rows, _) = Load("coffee-table");

        Assert.Equal(["Top", "Apron, long", "Leg", "Apron, short"], rows.Select(row => row.Label));
        Assert.Equal([1, 2, 4, 2], rows.Select(row => row.Quantity));

        // Nine boxes in the file, nine pieces on the bench.
        Assert.Equal(9, rows.Sum(row => row.Quantity));
        Assert.Equal(9, rows.Sum(row => row.Members.Length));
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_wall_has_nothing_to_cut()
    {
        // Both of its boxes are "part": null, which is a statement and not a gap: a wall and an
        // opening are things you build, not pieces you cut.
        (ImmutableArray<CutListRow> rows, _) = Load("wall-with-window");

        Assert.Empty(rows);
    }

    private static (ImmutableArray<CutListRow> Rows, ExpectedFixture Expected) Load(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));
        Loaded loaded = Assert.IsType<Loaded>(result);
        ExpectedFixture expected = ExpectedFixture.Read(fixture);

        Assert.Equal(expected.FormatVersion, SceneReader.FormatVersion);

        return (CutList.Of(loaded.Sketch, MaterialsLibrary.Shipped), expected);
    }
}
