using System.Text;

using Excise.Core.Document;
using Excise.Core.Text;

using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Interop.Pdf;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The coffee table on paper (#25), end to end: the sample, the views as the locked panes build them,
/// the sheet, and the PDF read back. Every coordinate is worked by hand from the design's stated
/// dimensions (coffee-table.design.md: the top 48 × 24, z 16¼–17; the legs 2½ square, z 0–16¼, the
/// south-west one at x 1½–4) and the dimensions' placements (ViewDimensionTests):
/// <code>
/// extents, inches — Top: x −4 (Top depth, 4" west) … 48, y −4 (Top width, 4" south) … 24
///                   Front: x 0 … 48, z −1 (Leg width, 1" under the leg) … 18 (the inset, 1" over the top)
///                   Right: y 0 … 24, z 0 … 17
/// centres — X shared by Top and Front: (−4 + 48) / 2 = 22; Z shared by Front and Right: (−1 + 18) / 2 = 8.5;
///           Top's Y: (−4 + 24) / 2 = 10; Right's Y: 12
/// scale — Top needs 2 × 26 = 52" across in 332 pt: 6.38 pt/in at most, so 1:12, 6 pt/in (1:8 would be 9)
/// origins, x — Top and Front 214, Right 578 (SheetPdfTests: the panes' usable centres)
/// </code>
/// </summary>
public class PaperSheetTests
{
    static readonly DateOnly Day = new(2026, 9, 27);
    static readonly LengthFormat Format = new FeetInchesFormat(16);

    static Sketch Table() => SampleExpectations.Sample("coffee-table").Load().Sketch;

    static PlanSheet Sheet(bool hiddenEdges = true) => PaperSheet.Of(Table(), "Coffee table", Day, Format, CodePacks.None, hiddenEdges);

    static (IReadOnlyList<string> Operators, IReadOnlyList<string> Text) Read(PlanSheet sheet)
    {
        using MemoryStream stream = new();
        SheetPdf.Write(sheet, stream);
        PdfDocument document = PdfDocument.Open(stream.ToArray());
        return (
            [.. Enumerable.Range(0, document.PageCount).Select(i => Encoding.Latin1.GetString(document.Pages[i].GetContentStreamBytes()))],
            [.. Enumerable.Range(0, document.PageCount).Select(i => new TextExtractor(document.Pages[i]).ExtractText())]);
    }

    static string N(double value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A stroke between two page points, drawn either way round (a face's winding decides which).</summary>
    static void AssertStroke(string page, double x1, double y1, double x2, double y2)
    {
        string forward = $"{N(x1)} {N(y1)} m\n{N(x2)} {N(y2)} l\nS", backward = $"{N(x2)} {N(y2)} m\n{N(x1)} {N(y1)} l\nS";
        Assert.True(
            page.Contains("0 G\n1.4 w\n" + forward, StringComparison.Ordinal) || page.Contains("0 G\n1.4 w\n" + backward, StringComparison.Ordinal),
            $"no visible stroke {forward.Replace('\n', ' ')}");
    }

    [Fact]
    public void The_sheet_draws_the_views_the_locked_panes_draw()
    {
        Sketch sketch = Table();
        ModelScene scene = ModelScene.Of(sketch);
        Assert.Equal([StandardView.Top, StandardView.Front, StandardView.Right], PaperSheet.Views);
        foreach (StandardView view in PaperSheet.Views)
        {
            StandardViewEdges edges = StandardViewEdges.Of(scene, StandardViews.CameraFor(view, Camera.Isometric()), view);
            DrawingView drawn = PaperSheet.View(sketch, view, Format, hiddenEdges: true);
            Assert.Equal(
                edges.Edges.Visible.Select(piece => (piece.From.U, piece.From.V, piece.To.U, piece.To.V)).Order(),
                drawn.Lines.Where(line => line.Kind == LineKind.Visible).Select(line => (line.From.Along, line.From.Across, line.To.Along, line.To.Across)).Order());
            Assert.Equal(
                edges.Edges.Hidden.Select(piece => (piece.From.U, piece.From.V, piece.To.U, piece.To.V)).Order(),
                drawn.Lines.Where(line => line.Kind == LineKind.Hidden).Select(line => (line.From.Along, line.From.Across, line.To.Along, line.To.Across)).Order());
            Assert.Equal(
                DimensionLayout.Measure(sketch, view).Select(dimension => dimension.Label(Format)),
                drawn.Dimensions.Select(dimension => dimension.Label));
            Assert.Empty(PaperSheet.View(sketch, view, Format, hiddenEdges: false).Lines.Where(line => line.Kind == LineKind.Hidden));
        }

        // Top covers the frame under it, so it has hidden edges; the coffee table has no opening.
        Assert.NotEmpty(PaperSheet.View(sketch, StandardView.Top, Format, hiddenEdges: true).Lines.Where(line => line.Kind == LineKind.Hidden));
        Assert.DoesNotContain(PaperSheet.View(sketch, StandardView.Front, Format, true).Lines, line => line.Dashed);
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void The_coffee_table_prints_at_1_to_12_with_its_4_foot_top_4_inches_long_where_the_hand_says()
    {
        PlanSheet sheet = Sheet();
        SheetPlacement placement = SheetPdf.Place(sheet);
        Assert.Equal(new SheetScale(12), placement.Scale);
        ViewPlacement top = placement.Views.Single(view => view.View.View == StandardView.Top);
        ViewPlacement front = placement.Views.Single(view => view.View.View == StandardView.Front);
        ViewPlacement right = placement.Views.Single(view => view.View.View == StandardView.Right);
        Assert.Equal((new DrawingPoint(22, 10), new DrawingPoint(22, 8.5), new DrawingPoint(12, 8.5)), (top.Centre, front.Centre, right.Centre));
        Assert.Equal((214.0, 214.0, 578.0), (top.Origin.X, front.Origin.X, right.Origin.X));
        Assert.Equal(front.Origin.Y, right.Origin.Y);

        string page = Assert.Single(Read(sheet).Operators);
        double row = front.Origin.Y;

        // Front: the top's front face, x 0…48, its underside z 16¼: x 214 + (0 − 22) × 6 = 82 to
        // 214 + 26 × 6 = 370 — 288 pt, 4" of paper, 48" at 1:12 — at y row + (16¼ − 8½) × 6 = row + 46.5.
        AssertStroke(page, 82, row + 46.5, 370, row + 46.5);
        // Its upper edge, z 17: row + 51.
        AssertStroke(page, 82, row + 51, 370, row + 51);
        // The south-west leg's foot, x 1½…4 at z 0: 214 + (1.5 − 22) × 6 = 91 to 106, at row − 8.5 × 6 = row − 51.
        AssertStroke(page, 91, row - 51, 106, row - 51);
        // Right: the top's east face's underside, y 0…24: 578 ∓ 12 × 6 = 506 to 650, level with Front's.
        AssertStroke(page, 506, row + 46.5, 650, row + 46.5);
        // Top: the top's south edge, x 0…48 at y 0: straight above Front's, at Top's origin + (0 − 10) × 6.
        AssertStroke(page, 82, top.Origin.Y - 60, 370, top.Origin.Y - 60);
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void The_sheet_reads_its_labels_its_scale_its_date_and_the_disclaimer()
    {
        string text = Assert.Single(Read(Sheet()).Text);
        foreach (string label in (string[])["4'-0\"", "2'-0\"", "2 1/2\"", "3'-4\"", "1'-4\"", "1 1/2\""])
        {
            Assert.Contains(label, text, StringComparison.Ordinal);
        }

        Assert.Contains("Coffee table", text, StringComparison.Ordinal);
        Assert.Contains("Scale 1:12", text, StringComparison.Ordinal);
        Assert.Contains("1\" = 1'-0\"", text, StringComparison.Ordinal);
        Assert.Contains("Date 2026-09-27", text, StringComparison.Ordinal);
        Assert.Contains("Sheet 1 of 1", text, StringComparison.Ordinal);
        Assert.Contains(ScopeDisclaimer.Text, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hidden_edges_print_as_grey_dashes_only_when_the_view_shows_them()
    {
        Assert.Contains("0.65 G\n1 w\n", Read(Sheet(hiddenEdges: true)).Operators[0], StringComparison.Ordinal);
        Assert.DoesNotContain("0.65 G", Read(Sheet(hiddenEdges: false)).Operators[0], StringComparison.Ordinal);
    }
}
