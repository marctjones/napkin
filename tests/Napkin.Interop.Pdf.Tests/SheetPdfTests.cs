using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.Tests.ReadBack;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The sheet as a PDF (#25), read back as a reader reads it. The fixture is a plain box 24″ across
/// (X), 12″ deep (Y) and 6″ tall (Z), drawn by hand in its three views, so every coordinate below is
/// worked out here from the page's own arithmetic:
/// <code>
/// page 792 × 612 pt (Letter, landscape); margin 36 → drawing area 720 wide, from x = 36
/// panes (SheetLayout): (720 − 8) / 2 = 356 wide; left column x = 36, right column x = 36 + 356 + 8 = 400
/// usable = pane less 12 (PanePadding) all round, 16 more off the top (CaptionBand): 332 wide
///   → centre x = 36 + 12 + 166 = 214 (Top, Front) and 400 + 12 + 166 = 578 (Right)
/// centres: Top and Front share X, the middle of 0…24 = 12; Front and Right share Z, the middle of 0…6 = 3;
///   Top's own Y centre is 6, Right's own Y centre 6
/// scale: across, 332 / 24 = 13.8 pt/in is the tightest a pane allows (heights have more room), and the
///   largest ratio no bigger is 1:8 = 72 / 8 = 9 pt/in (1:4 would be 18)
/// </code>
/// </summary>
public class SheetPdfTests
{
    static readonly DateOnly Day = new(2026, 9, 27);

    static DrawingPoint P(double along, double across) => new(along, across);

    static IEnumerable<DrawingLine> Rectangle(double width, double height, bool dashed = false) =>
    [
        new(LineKind.Visible, P(0, 0), P(width, 0), dashed),
        new(LineKind.Visible, P(width, 0), P(width, height), dashed),
        new(LineKind.Visible, P(width, height), P(0, height), dashed),
        new(LineKind.Visible, P(0, height), P(0, 0), dashed),
    ];

    static PlanSheet Box(IEnumerable<DrawingLine>? frontExtra = null, IReadOnlyList<DrawingDimension>? topDimensions = null, IReadOnlyList<SheetNote>? notes = null, string? code = null) => new(
        new TitleBlock("Test box", Day, code),
        new FeetInchesFormat(16),
        [
            new DrawingView(StandardView.Top, [.. Rectangle(24, 12)], topDimensions ?? []),
            new DrawingView(StandardView.Front, [.. Rectangle(24, 6), .. frontExtra ?? []], []),
            new DrawingView(StandardView.Right, [.. Rectangle(12, 6)], []),
        ],
        "Code check",
        notes ?? []);

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void A_24_inch_edge_is_24_inches_of_the_stated_scale_drawn_as_a_vector_stroke()
    {
        PlanSheet sheet = Box();
        SheetPlacement placement = SheetPdf.Place(sheet);
        Assert.Equal(new SheetScale(8), placement.Scale);

        ViewPlacement top = placement.Views.Single(view => view.View.View == StandardView.Top);
        ViewPlacement front = placement.Views.Single(view => view.View.View == StandardView.Front);
        ViewPlacement right = placement.Views.Single(view => view.View.View == StandardView.Right);
        Assert.Equal((214.0, 214.0, 578.0), (top.Origin.X, front.Origin.X, right.Origin.X));
        Assert.Equal(front.Origin.Y, right.Origin.Y);
        Assert.Equal((P(12, 6), P(12, 3), P(6, 3)), (top.Centre, front.Centre, right.Centre));

        ReadBack read = Of(sheet);
        string page = Assert.Single(read.Operators);

        // Front's bottom edge, 0…24 along X at Z = 0: x = 214 + (0 − 12) × 9 = 106 to 214 + 12 × 9 = 322,
        // 216 pt = 3 in of paper = 24 in at 1:8; y = the Front origin + (0 − 3) × 9.
        double y = front.Origin.Y - 27;
        Assert.Contains("0 G\n1.4 w\n" + Stroke(106, y, 322, y), page, StringComparison.Ordinal);
        // Right's bottom edge, 0…12 along Y: 578 ∓ 6 × 9.
        Assert.Contains(Stroke(524, y, 632, y), page, StringComparison.Ordinal);
        // Top's south edge, 0…24 along X at Y = 0: the same x as Front's, straight above it.
        Assert.Contains(Stroke(106, top.Origin.Y - 54, 322, top.Origin.Y - 54), page, StringComparison.Ordinal);

        // Vector, not a picture of the canvas: no image or form is painted.
        Assert.DoesNotContain(" Do\n", page, StringComparison.Ordinal);
        Assert.Equal((792.0, 612.0), (read.Document.Pages[0].Width, read.Document.Pages[0].Height));
    }

    [Fact]
    public void The_placement_is_the_page_arithmetic()
    {
        SheetPlacement placement = SheetPdf.Place(Box());
        PageRect title = placement.TitleBlock;
        Assert.Equal((36.0, 36.0, 720.0), (title.Left, title.Bottom, title.Width));

        // The drawing area is what the title block and its 8 pt gap leave under the top margin.
        PageRect area = placement.DrawingArea;
        Assert.Equal((36.0, title.Top + 8, 720.0, 612 - 36.0), (area.Left, area.Bottom, area.Width, area.Top));

        // Front's pane is the lower-left quarter; its usable centre is 12 + (pane − 12 − 12 − 16) / 2 up from its foot.
        double pane = (area.Height - 8) / 2;
        ViewPlacement front = placement.Views.Single(view => view.View.View == StandardView.Front);
        AssertNear(new PageRect(36, area.Bottom, 356, pane), front.Pane);
        Assert.Equal(area.Bottom + 12 + ((pane - 40) / 2), front.Origin.Y, 9);

        // The notes start in the spare top-right quarter.
        AssertNear(new PageRect(400, area.Bottom + pane + 8, 356, pane), placement.NotesPane);
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void Hidden_edges_are_grey_dashes_each_its_own_solid_stroke()
    {
        // A hidden edge across Front at Z = 3: 216 pt long at 1:8, so thirty-six 3 pt dashes with 3 pt gaps,
        // each stroked alone in 1 pt of 0.65 grey (black at 0.35 on white paper); no dash operator, no
        // transparency.
        ReadBack read = Of(Box(frontExtra: [new DrawingLine(LineKind.Hidden, P(0, 3), P(24, 3))]));
        SheetPlacement placement = SheetPdf.Place(Box());
        double y = placement.Views.Single(view => view.View.View == StandardView.Front).Origin.Y;
        string page = read.Operators[0];

        Assert.Equal(36, Count(page, "0.65 G\n1 w\n"));
        Assert.Contains("0.65 G\n1 w\n" + Stroke(106, y, 109, y), page, StringComparison.Ordinal);
        Assert.Contains("0.65 G\n1 w\n" + Stroke(316, y, 319, y), page, StringComparison.Ordinal);
        Assert.DoesNotContain(" d\n", page, StringComparison.Ordinal);
        Assert.DoesNotContain(" gs\n", page, StringComparison.Ordinal);

        // Front's hidden edges go down first, under its visible ones.
        Assert.True(page.IndexOf("0.65 G", StringComparison.Ordinal) < page.IndexOf(Stroke(106, y - 27, 322, y - 27), StringComparison.Ordinal));
    }

    [Fact]
    public void An_opening_outline_is_dashed_4_3_in_the_visible_weight()
    {
        // A 4″ opening's sill across Front at Z = 2: 36 pt at 1:8, so dashes 4 on, 3 off from x = 214 + (10 − 12) × 9 = 196:
        // 196–200, 203–207, 210–214, 217–221, 224–228, and a last 231–232.
        ReadBack read = Of(Box(frontExtra: [new DrawingLine(LineKind.Visible, P(10, 2), P(14, 2), Dashed: true)]));
        double y = SheetPdf.Place(Box()).Views.Single(view => view.View.View == StandardView.Front).Origin.Y - 9;
        string page = read.Operators[0];
        foreach ((double from, double to) in ((double, double)[])[(196, 200), (203, 207), (210, 214), (217, 221), (224, 228), (231, 232)])
        {
            Assert.Contains("0 G\n1.4 w\n" + Stroke(from, y, to, y), page, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void A_dimension_is_drawn_as_the_canvas_draws_it_and_reads_as_the_canvas_labels_it()
    {
        // Top's width, measured along its south edge, the line 3″ south of it (−27 pt at 1:8). The line
        // is part of what Top must fit, so Top's Y now runs −3…12 and its centre is 4.5: the edge is
        // 4.5 × 9 = 40.5 pt under Top's origin.
        DrawingDimension width = new("2'-0\"", P(0, 0), P(24, 0), P(0, -3), P(24, -3));
        PlanSheet sheet = Box(topDimensions: [width]);
        ViewPlacement top = SheetPdf.Place(sheet).Views.Single(view => view.View.View == StandardView.Top);
        Assert.Equal(P(12, 4.5), top.Centre);
        double edge = top.Origin.Y - 40.5, line = edge - 27;
        ReadBack read = Of(sheet);
        string page = read.Operators[0];

        // Extension lines: 3 pt off the edge, 5 pt past the line, in the 0.8 pt thin line.
        Assert.Contains("0 G\n0.8 w\n" + Stroke(106, edge - 3, 106, line - 5), page, StringComparison.Ordinal);
        Assert.Contains("0 G\n0.8 w\n" + Stroke(322, edge - 3, 322, line - 5), page, StringComparison.Ordinal);
        // The line itself, 216 pt: long enough for its arrowheads inside.
        Assert.Contains("0 G\n0.8 w\n" + Stroke(106, line, 322, line), page, StringComparison.Ordinal);
        // The west arrowhead: tip at the line's start, 9 pt long, 2.75 pt each side of it, filled.
        Assert.Contains($"106 {N(line)} m\n115 {N(line - 2.75)} l\n115 {N(line + 2.75)} l\nh\n0 g\nf", page, StringComparison.Ordinal);
        // The label on a white chip at the line's middle, reading across.
        Assert.Contains("1 g\n", page, StringComparison.Ordinal);
        Assert.Contains("2'-0\"", read.Text[0], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void Every_sheet_carries_the_project_the_scale_the_date_the_code_and_the_disclaimer()
    {
        ReadBack read = Of(Box(code: "Code: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1. Locked on 2026-09-25 to pack us-zz-frame revision 1."));
        string text = Assert.Single(read.Text);
        Assert.Contains("Test box", text, StringComparison.Ordinal);
        Assert.Contains("Plan and elevations · Third-angle projection", text, StringComparison.Ordinal);
        Assert.Contains("Scale 1:8", text, StringComparison.Ordinal);
        Assert.Contains("1\" = 8\"", text, StringComparison.Ordinal);
        Assert.Contains("Date 2026-09-27", text, StringComparison.Ordinal);
        Assert.Contains("Sheet 1 of 1", text, StringComparison.Ordinal);
        Assert.Contains("Code: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1. Locked on 2026-09-25 to pack us-zz-frame revision 1.", text, StringComparison.Ordinal);
        Assert.Contains(ScopeDisclaimer.Text, text, StringComparison.Ordinal);
        Assert.Contains("Top", text, StringComparison.Ordinal);
        Assert.Contains("Front", text, StringComparison.Ordinal);
        Assert.Contains("Right", text, StringComparison.Ordinal);
        Assert.Equal("Test box", read.Document.Title);
    }

    [Fact]
    public void Notes_fill_the_spare_pane_then_further_pages_each_with_its_title_block()
    {
        SheetNote[] many = [.. Enumerable.Range(1, 40).Select(i => new SheetNote($"Window {i}: header", [$"Header (2) 2x10 for window {i}.", "ZZ Table ZZ-HEADER row r.s30.b; a long citation line that wraps once it reaches the edge of the pane it is printed in."]))];
        ReadBack read = Of(Box(notes: many));
        Assert.True(read.Operators.Count >= 2, $"{read.Operators.Count} page(s)");

        for (int page = 0; page < read.Text.Count; page++)
        {
            string text = read.Text[page];
            Assert.Contains("Test box", text, StringComparison.Ordinal);
            Assert.Contains(ScopeDisclaimer.Text, text, StringComparison.Ordinal);
            Assert.Contains($"Sheet {page + 1} of {read.Text.Count}", text, StringComparison.Ordinal);
            Assert.Contains(page == 0 ? SheetPdf.SheetTitle : SheetPdf.NotesTitle, text, StringComparison.Ordinal);
        }

        string all = string.Join(" ", read.Text);
        Assert.Contains("Code check", read.Text[0], StringComparison.Ordinal);
        foreach (int i in (int[])[1, 20, 40])
        {
            Assert.Contains($"Window {i}: header", all, StringComparison.Ordinal);
            Assert.Contains($"Header (2) 2x10 for window {i}.", all, StringComparison.Ordinal);
        }

        // The views are drawn once, on the first page; the others are notes only.
        Assert.Contains("1.4 w", read.Operators[0], StringComparison.Ordinal);
        Assert.DoesNotContain("1.4 w", read.Operators[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_sheet_with_nothing_drawn_is_one_page_at_full_size()
    {
        PlanSheet empty = new(new TitleBlock("Blank", Day, null), new FeetInchesFormat(16), [], "Code check", []);
        Assert.Equal(new SheetScale(1), SheetPdf.Place(empty).Scale);
        ReadBack read = Of(empty);
        Assert.Contains("Scale 1:1", Assert.Single(read.Text), StringComparison.Ordinal);
    }

    [Fact]
    public void A_view_the_sheet_has_no_pane_for_or_a_view_twice_is_refused()
    {
        PlanSheet bottom = Box() with { Views = [new DrawingView(StandardView.Bottom, [.. Rectangle(1, 1)], [])] };
        Assert.Contains("Bottom", Assert.Throws<ArgumentException>(() => SheetPdf.Place(bottom)).Message, StringComparison.Ordinal);
        PlanSheet twice = Box() with { Views = [new DrawingView(StandardView.Top, [], []), new DrawingView(StandardView.Top, [], [])] };
        Assert.Contains("two Top", Assert.Throws<ArgumentException>(() => SheetPdf.Place(twice)).Message, StringComparison.Ordinal);
    }

    static void AssertNear(PageRect expected, PageRect actual)
    {
        Assert.Equal(expected.Left, actual.Left, 9);
        Assert.Equal(expected.Bottom, actual.Bottom, 9);
        Assert.Equal(expected.Width, actual.Width, 9);
        Assert.Equal(expected.Height, actual.Height, 9);
    }

    [Fact]
    public void Paper_grey_is_what_black_at_an_opacity_leaves_on_white()
    {
        Assert.Equal(0.65, SheetPdf.PaperGrey(DrawingLines.Of(LineKind.Hidden).Opacity), 12);
        Assert.Equal(0, SheetPdf.PaperGrey(1));
        Assert.Equal(1, SheetPdf.PaperGrey(-1));
    }
}
