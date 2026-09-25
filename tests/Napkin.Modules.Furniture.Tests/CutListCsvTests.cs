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

            Assert.Equal(9, line.Length);
            Assert.Equal(row.Label, line[0]);
            Assert.Equal(row.Quantity, int.Parse(line[1], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(CutListCsv.Text(row.Length), line[2]);
            Assert.Equal(CutListCsv.Text(row.Width), line[3]);
            Assert.Equal(CutListCsv.Text(row.Thickness), line[4]);
            Assert.Equal(row.MaterialText, line[5]);

            // Nothing in this design is rough, cut or joined, so the last three columns are empty on every line.
            Assert.Equal(string.Empty, line[6]);
            Assert.Equal(string.Empty, line[7]);
            Assert.Equal(string.Empty, line[8]);
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
    [Trait("Feature", "CUT-004")]
    public void A_carriage_return_and_a_missing_last_newline_are_both_read_back()
    {
        // Not something ToCsv writes, but something a file that has been through a Windows editor
        // or a clipboard can carry, and the parser is what a test compares an export with.
        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse("a,\"b\r\nc\"\nd,e");

        Assert.Equal(2, lines.Length);
        Assert.Equal(["a", "b\r\nc"], lines[0]);
        Assert.Equal(["d", "e"], lines[1]);

        // A label whose own text holds a carriage return is quoted on the way out, so the line it
        // sits on is still one line and reads back as what it was.
        string csv = CutListCsv.ToCsv([Row("upper\rlower")]);

        Assert.Contains("\"upper\rlower\"", csv, StringComparison.Ordinal);
        Assert.Equal("upper\rlower", CutListCsv.Parse(csv)[2][0]);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void A_row_is_a_value_that_can_be_compared_and_kept_in_a_set()
    {
        // Two cut lists of one design are equal, which is what makes "the export is exactly the
        // table" assertable; ImmutableArray's own equality would call them different.
        CutListRow row = Row("Leg");

        Assert.Equal(row, Row("Leg"));
        Assert.Equal(row.GetHashCode(), Row("Leg").GetHashCode());
        Assert.NotEqual(row, Row("Apron"));
        Assert.False(row.Equals(null));

        HashSet<CutListRow> set = [row, Row("Leg"), Row("Apron")];
        Assert.Equal(2, set.Count);

        Assert.NotEqual(row, row with { Members = [EntityId.New()] });
        Assert.NotEqual(row, row with { Quantity = 2 });

        // The cuts are in the comparison, by sequence. ImmutableArray's own equality compares the
        // identity of the array it wraps, so a row rounded at one corner and a row rounded at
        // another — and a row with cuts and one without — would otherwise all be the same row
        // (shaped-parts-model.md §4.2).
        CutListRow rounded = row with { Cuts = [new RoundedCorner(BoxCorner.NorthEast, new Length(1024))] };

        Assert.NotEqual(row, rounded);
        Assert.Equal(rounded, row with { Cuts = [new RoundedCorner(BoxCorner.NorthEast, new Length(1024))] });
        Assert.Equal(
            rounded.GetHashCode(),
            (row with { Cuts = [new RoundedCorner(BoxCorner.NorthEast, new Length(1024))] }).GetHashCode());
        Assert.NotEqual(rounded, row with { Cuts = [new RoundedCorner(BoxCorner.SouthWest, new Length(1024))] });
        Assert.NotEqual(rounded, row with { Cuts = [new RoundedCorner(BoxCorner.NorthEast, new Length(512))] });
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_cuts_column_round_trips_and_is_empty_for_a_plain_rectangle()
    {
        // A top rounded at all four corners and a plain apron, so that one line has sentences in
        // its last column and the other has nothing at all (shaped-parts-model.md §4.5, test 15).
        Sketch sketch = Design.WithCutParts(
            ("Top", 49152, 24576, Top,
             [
                 new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                 new RoundedCorner(BoxCorner.SouthEast, new Length(1024)),
                 new RoundedCorner(BoxCorner.NorthEast, new Length(1024)),
                 new RoundedCorner(BoxCorner.NorthWest, new Length(1024)),
             ]),
            ("Apron, long, south", 40960, 768, Apron, []));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, MaterialsLibrary.Shipped);
        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(CutListCsv.ToCsv(rows));

        Assert.Equal("Cuts", lines[1][^2]);
        Assert.Equal("Joinery", lines[1][^1]);

        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(
                string.Join(CutListCsv.BetweenCuts, rows[i].CutText),
                lines[i + 2][7]);
        }

        // The top's one sentence survives a quote-carrying length and the round trip; the apron's
        // column is empty rather than absent.
        Assert.Equal("Round all four corners to a 1\" radius.", lines[2][7]);
        Assert.Equal(string.Empty, lines[3][7]);
    }

    /// <summary>A row of one arbitrary 1-inch cube, for tests about a row rather than a design.</summary>
    private static CutListRow Row(string label) => new(
        label,
        1,
        new Length(1024),
        new Length(1024),
        new Length(1024),
        string.Empty,
        Unresolved: false,
        Stock: null,
        Cuts: [],
        PlanAxes: new PlanAxes(PartDimension.Length, PartDimension.Width),
        Members: [new EntityId(Guid.Parse("10000000-0000-4000-8000-000000000001"))]);

    [Fact]
    public void The_exporter_rejects_nonsense_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => CutListCsv.ToCsv(null!));
        Assert.Throws<ArgumentNullException>(() => CutListCsv.Parse(null!));
    }

    private static Piece Leg => new(
        null, null, 1, new Length(16640), new PlanAxes(PartDimension.Width, PartDimension.Thickness));

    private static Piece Apron => new(
        null, null, 1, new Length(3584), new PlanAxes(PartDimension.Length, PartDimension.Thickness));

    private static Piece Top => new(
        null, null, 1, new Length(768), new PlanAxes(PartDimension.Length, PartDimension.Width));
}
