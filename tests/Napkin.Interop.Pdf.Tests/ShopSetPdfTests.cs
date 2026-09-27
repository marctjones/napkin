using System.Collections.Immutable;
using System.Text.RegularExpressions;

using Excise.Core.Document;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Furniture;

using static Napkin.Interop.Pdf.Tests.ReadBack;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The shop set (#211) printed from the samples and read back as a reader reads it. The paper's
/// numbers must be the cut-list window's: every row's cells, every layout line, every piece's label.
/// </summary>
public class ShopSetPdfTests
{
    static readonly DateOnly Day = new(2026, 9, 27);
    static readonly LengthFormat Format = new FeetInchesFormat(16);

    static Sketch Sample(string name)
        => Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", $"{name}.scene.json"))).Sketch;

    public static TheoryData<string> Samples()
    {
        TheoryData<string> data = [];
        foreach (string file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "samples"), "*.scene.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(file)[..^".scene.json".Length]);
        }

        return data;
    }

    static ShopSet Set(Sketch sketch, string name = "Test") =>
        ShopSet.Of(sketch, MaterialsLibrary.Shipped, CutLayout.DefaultKerf, new TitleBlock(name, Day, null), Format);

    static ReadBack Read(ShopSet set)
    {
        using MemoryStream stream = new();
        ShopSetPdf.Write(set, stream);
        return ReadBack.Open(stream.ToArray());
    }

    /// <summary>The squashed text of the pages whose title block names them.</summary>
    static string PagesTitled(ReadBack read, string title) =>
        string.Concat(read.Letters.Where((_, page) => read.Text[page].Contains(title, StringComparison.Ordinal)));

    [Theory]
    [Trait("Feature", "CUT-024")]
    [MemberData(nameof(Samples))]
    public void Every_sample_prints_the_cut_list_rows_their_layout_lines_and_a_label_per_piece(string sample)
    {
        ShopSet set = Set(Sample(sample), sample);
        ReadBack read = Read(set);
        Assert.All(read.Text, text => Assert.Contains(ScopeDisclaimer.Text, text, StringComparison.Ordinal));
        Assert.All(read.Text.Select((text, page) => (text, page)), each => Assert.Contains($"Sheet {each.page + 1} of {read.Text.Count}", each.text, StringComparison.Ordinal));

        string cutList = PagesTitled(read, ShopSetPdf.CutListTitle);
        if (set.Rows.IsEmpty)
        {
            Assert.Contains(Squash(set.Empty!), cutList, StringComparison.Ordinal);
            Assert.Single(read.Text);
            return;
        }

        // Each row's cells, in the file's order, one after another; the rows in the list's order.
        int at = 0;
        foreach (CutListRow row in set.Rows)
        {
            string cells = Squash(string.Concat(ShopSetPdf.Cells(row)));
            int found = cutList.IndexOf(cells, at, StringComparison.Ordinal);
            Assert.True(found >= 0, $"{sample}: row {row.Label} does not read as the cut list's cells {cells}");
            at = found + cells.Length;
        }

        string layout = PagesTitled(read, ShopSetPdf.LayoutTitle);
        foreach (CutLayoutRow row in CutLayout.Rows(set.Layout))
        {
            Assert.Contains(Squash(CutLayout.Line(CutLayout.Fields(row))), layout, StringComparison.Ordinal);
        }

        foreach (string line in CutLayout.Summary(set.Layout))
        {
            Assert.Contains(Squash(line), layout, StringComparison.Ordinal);
        }

        string labels = PagesTitled(read, ShopSetPdf.LabelsTitle);
        Assert.Equal(set.Rows.Sum(row => row.Quantity), set.Labels.Length);
        foreach (ShopLabel label in set.Labels)
        {
            Assert.Contains(Squash(label.Part + label.Copy + label.Size), labels, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Feature", "CUT-024")]
    public void The_stocked_bench_draws_its_boards_and_its_sheet_at_1_to_24_where_the_hand_says()
    {
        // stocked-bench.expected.json: 1x4, one 12 ft board (46, 46, 14, 14); 2x4, one 14 ft board
        // (43, 43, 16½ × 4); 3/4 plywood, one 96 × 48 sheet with the 48 × 16 top at its corner.
        // Longest drawn: the 14 ft board, 168" in the 720 − 2 × 12 = 696 pt the page gives: 4.14 pt/in
        // at most, so 1:24 (3 pt/in; 1:16 would be 4.5). Every drawing starts at x = 36 + 12 = 48.
        ShopSet set = Set(Sample("stocked-bench"));
        Assert.Equal(new SheetScale(24), ShopSetPdf.LayoutScale(set.Layout, 696));
        ReadBack read = Read(set);
        string layout = string.Concat(read.Operators.Where((_, page) => read.Text[page].Contains(ShopSetPdf.LayoutTitle, StringComparison.Ordinal)));
        Assert.Contains("Scale 1:24", string.Concat(read.Text), StringComparison.Ordinal);

        // The 2x4: 43" pieces are 129 pt; the first at 48, the kerf mark (1/8" = 0.375 pt) centred at
        // 177.1875 in 2 pt, the second at 177.375; the offcut, 168 − 152 − 6 × 1/8 = 15¼" = 45.75 pt,
        // at 48 + 504 − 45.75 = 506.25, hatched.
        Match first = Regex.Match(layout, @"\n48 (\S+) 129 18 re\nS");
        Assert.True(first.Success, "no 43\" piece at the start of a bar");
        string y = first.Groups[1].Value;
        Assert.Contains($"177.375 {y} 129 18 re", layout, StringComparison.Ordinal);
        Assert.Contains($"0 G\n2 w\n177.1875 {y} m\n177.1875 {N(double.Parse(y, System.Globalization.CultureInfo.InvariantCulture) + 18)} l\nS", layout, StringComparison.Ordinal);
        Assert.Contains($"506.25 {y} 45.75 18 re", layout, StringComparison.Ordinal);
        Assert.Contains("0.45 G\n1 w\n", layout, StringComparison.Ordinal);

        // The sheet: 96 × 48 is 288 × 144 pt; the top, 48 × 16, is 144 × 48 pt at its top-left corner.
        Match sheet = Regex.Match(layout, @"\n48 (\S+) 288 144 re\nS");
        Assert.True(sheet.Success, "no 96 × 48 sheet");
        double bottom = double.Parse(sheet.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"48 {N(bottom + 96)} 144 48 re", layout, StringComparison.Ordinal);
        Assert.Contains("Top", string.Concat(read.Text), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-024")]
    public void A_piece_whose_grain_is_set_carries_a_grain_arrow_along_the_sheet()
    {
        // The stocked bench's top, its grain set along its length: still 48 along the sheet, so the
        // arrow runs along x, centred on the piece (x 48–192, centre 120), ±min(0.3 × 144, 36) = ±36.
        Sketch sketch = Sample("stocked-bench");
        Box top = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Top");
        ShopSet set = Set(sketch.WithEntity(top with { Part = top.Part! with { Grain = PartDimension.Length } }));
        ReadBack read = Read(set);
        string layout = string.Concat(read.Operators);
        Assert.Matches(@"0 G\n0\.8 w\n84 \S+ m\n156 \S+ l\nS", layout);
        Assert.Contains("grain", string.Concat(read.Text), StringComparison.Ordinal);

        // Without a grain, no arrow.
        Assert.DoesNotMatch(@"0 G\n0\.8 w\n84 \S+ m\n156 \S+ l\nS", string.Concat(Read(Set(sketch)).Operators));
    }

    [Fact]
    [Trait("Feature", "CUT-024")]
    public void Each_label_says_where_its_piece_is_cut_from()
    {
        ShopSet set = Set(Sample("stocked-bench"));
        ShopLabel[] legs = [.. set.Labels.Where(label => label.Part == "Leg")];
        Assert.Equal(["1 of 4", "2 of 4", "3 of 4", "4 of 4"], legs.Select(label => label.Copy));
        Assert.All(legs, label => Assert.Equal("Board 1: 2x4 × 14 ft", label.From));
        ShopLabel top = Assert.Single(set.Labels, label => label.Part == "Top");
        Assert.Equal("Sheet 1: 3/4 plywood", top.From);
        Assert.Equal("4'-0\" × 1'-4\" × 3/4\"", top.Size);
        Assert.Equal("3/4 plywood", top.Material);
    }

    [Fact]
    public void A_design_with_nothing_to_cut_is_one_page_saying_why()
    {
        ShopSet set = Set(Sketch.Empty);
        Assert.Equal(CutList.NothingToCut, set.Empty);
        ReadBack read = Read(set);
        Assert.Contains(CutList.NothingToCut, Assert.Single(read.Text), StringComparison.Ordinal);
        Assert.Contains("Scale —", read.Text[0], StringComparison.Ordinal);
    }
}
