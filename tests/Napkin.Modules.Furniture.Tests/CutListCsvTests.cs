using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The CSV export: exactly the rows on screen, in the same order, with the same numbers
/// (<c>docs/design/parts-and-cut-list.md</c> §3.1 and issue #8's "Output format").
/// </summary>
public sealed class CutListCsvTests
{
    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_export_parses_back_to_the_rows_it_came_from_in_the_same_order()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(
                ("Leg, south-west", 2560, 2560, Leg),
                ("Leg, north-east", 2560, 2560, Leg),
                ("Apron, long, south", 40960, 768, Apron with { Stock = "1x4" }),
                ("Apron, long, north", 40960, 768, Apron with { Stock = "1x4" }),
                ("Top", 49152, 24576, Top with { Stock = "9x17 unobtainium" })),
            MaterialsLibrary.Shipped);

        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(CutListCsv.ToCsv(rows));

        // A header line about what the list is before, then the column names, then the rows.
        Assert.Equal(rows.Length + 2, lines.Length);
        Assert.Equal(CutList.BeforeKerfAndJoinery, Assert.Single(lines[0]));
        Assert.Equal(CutListCsv.Header.Split(','), lines[1]);

        for (int i = 0; i < rows.Length; i++)
        {
            CutListRow row = rows[i];
            ImmutableArray<string> line = lines[i + 2];

            Assert.Equal(6, line.Length);
            Assert.Equal(row.Label, line[0]);
            Assert.Equal(row.Quantity, int.Parse(line[1], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(CutListCsv.Text(row.Length), line[2]);
            Assert.Equal(CutListCsv.Text(row.Width), line[3]);
            Assert.Equal(CutListCsv.Text(row.Thickness), line[4]);
            Assert.Equal(row.MaterialText, line[5]);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void A_label_with_a_comma_in_it_survives_the_round_trip()
    {
        // "Apron, long" is the grouped label of the coffee table's two long aprons, so this is not
        // a hypothetical: a naive export would turn one row into seven columns.
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(
                ("Apron, long, south", 40960, 768, Apron),
                ("Apron, long, north", 40960, 768, Apron)),
            MaterialsLibrary.Shipped);

        string csv = CutListCsv.ToCsv(rows);

        Assert.Contains("\"Apron, long\",2,", csv, StringComparison.Ordinal);
        Assert.Equal("Apron, long", CutListCsv.Parse(csv)[2][0]);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void A_length_carrying_a_quote_is_quoted_and_its_quote_doubled()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(("Top", 49152, 24576, Top)),
            MaterialsLibrary.Shipped);

        string csv = CutListCsv.ToCsv(rows);

        // 4'-0" written as a CSV field is "4'-0""".
        Assert.Contains("\"4'-0\"\"\"", csv, StringComparison.Ordinal);
        Assert.Equal("4'-0\"", CutListCsv.Parse(csv)[2][2]);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void A_length_that_is_not_exact_at_a_sixteenth_says_so()
    {
        // 10 1/64" cannot be written at 1/16", so the file marks it the way the canvas does rather
        // than claiming a size nobody could cut to.
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(("Odd", 10256, 768, Apron)),
            MaterialsLibrary.Shipped);

        Assert.StartsWith(CutListCsv.Approximately, CutListCsv.Text(rows[0].Length), StringComparison.Ordinal);
        Assert.Contains(CutListCsv.Approximately, CutListCsv.ToCsv(rows), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void An_unresolved_stock_says_so_in_the_file_too()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithParts(("Shelf", 10240, 768, Apron with { Stock = "9x17 unobtainium" })),
            MaterialsLibrary.Shipped);

        string material = CutListCsv.Parse(CutListCsv.ToCsv(rows))[2][5];

        Assert.Equal(CutListRow.UnresolvedText("9x17 unobtainium"), material);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void An_empty_cut_list_exports_its_header_and_nothing_else()
    {
        string csv = CutListCsv.ToCsv(CutList.Of(Sketch.Empty, MaterialsLibrary.Shipped));

        Assert.Equal(2, CutListCsv.Parse(csv).Length);
        Assert.EndsWith(CutListCsv.Header + "\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void Lines_end_in_a_newline_on_every_platform()
    {
        string csv = CutListCsv.ToCsv(CutList.Of(
            Design.WithParts(("Shelf", 10240, 768, Apron)),
            MaterialsLibrary.Shipped));

        Assert.DoesNotContain('\r', csv);
        Assert.EndsWith("\n", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void The_exporter_rejects_nonsense_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => CutListCsv.ToCsv(null!));
        Assert.Throws<ArgumentNullException>(() => CutListCsv.Parse(null!));
    }

    private static Part Leg => new(
        null, null, 1, new Length(16640), new PlanAxes(PartDimension.Width, PartDimension.Thickness));

    private static Part Apron => new(
        null, null, 1, new Length(3584), new PlanAxes(PartDimension.Length, PartDimension.Thickness));

    private static Part Top => new(
        null, null, 1, new Length(768), new PlanAxes(PartDimension.Length, PartDimension.Width));
}
