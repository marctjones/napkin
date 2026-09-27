using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>The pieces the sheet is drawn with: dashes cut by hand, the scale ratios, the view's extent, word wrap.</summary>
public class SheetPartsTests
{
    static PagePoint P(double x, double y) => new(x, y);

    [Fact]
    public void A_dashed_line_is_its_on_pieces_restarting_at_its_start()
    {
        // 10 pt with [3, 3]: on 0–3, off 3–6, on 6–9, off 9–10.
        Assert.Equal(
            [new PageSegment(P(0, 0), P(3, 0)), new PageSegment(P(6, 0), P(9, 0))],
            Dashes.Along(P(0, 0), P(10, 0), [3, 3]));

        // Upwards, 8 pt with [4, 3]: 0–4, 7–8, the last dash cut short at the end.
        Assert.Equal(
            [new PageSegment(P(5, 0), P(5, 4)), new PageSegment(P(5, 7), P(5, 8))],
            Dashes.Along(P(5, 0), P(5, 8), [4, 3]));

        // An odd pattern repeats as a PDF dash array does: [2] is 2 on, 2 off.
        Assert.Equal(
            [new PageSegment(P(0, 0), P(2, 0)), new PageSegment(P(4, 0), P(6, 0))],
            Dashes.Along(P(0, 0), P(6, 0), [2]));
    }

    [Fact]
    public void A_solid_line_is_one_piece_and_a_point_is_none()
    {
        Assert.Equal([new PageSegment(P(0, 0), P(3, 4))], Dashes.Along(P(0, 0), P(3, 4), []));
        Assert.Empty(Dashes.Along(P(1, 1), P(1, 1), [3, 3]));
        Assert.Throws<ArgumentException>(() => Dashes.Along(P(0, 0), P(1, 0), [0, 0]));
        Assert.Throws<ArgumentException>(() => Dashes.Along(P(0, 0), P(1, 0), [-1, 2]));
        Assert.Throws<ArgumentException>(() => Dashes.Along(P(0, 0), P(1, 0), [double.NaN]));
        Assert.Throws<ArgumentNullException>(() => Dashes.Along(P(0, 0), P(1, 0), null!));
    }

    [Fact]
    [Trait("Feature", "IOP-003")]
    public void The_scale_is_the_largest_ratio_that_fits_and_says_itself_as_the_canvas_writes_lengths()
    {
        // 72 pt/in is full size; 13.8 pt/in fits 1:8 (9) but not 1:4 (18); 6 pt/in is exactly 1:12.
        Assert.Equal(new SheetScale(1), SheetScale.AtMost(100));
        Assert.Equal(new SheetScale(8), SheetScale.AtMost(13.8));
        Assert.Equal(new SheetScale(12), SheetScale.AtMost(6));
        Assert.Equal(new SheetScale(96), SheetScale.AtMost(0.75));
        // Past 1:96 each further scale halves: 72 / 192 = 0.375 fits 0.4.
        Assert.Equal(new SheetScale(192), SheetScale.AtMost(0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetScale.AtMost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetScale.AtMost(double.NaN));

        FeetInchesFormat format = new(16);
        Assert.Equal(("1:12", "1\" = 1'-0\""), (new SheetScale(12).Label, new SheetScale(12).InWords(format)));
        Assert.Equal("1\" = 4'-0\"", new SheetScale(48).InWords(format));
        Assert.Equal(6, new SheetScale(12).PointsPerInch);
        Assert.Throws<ArgumentNullException>(() => new SheetScale(1).InWords(null!));
    }

    [Fact]
    public void A_view_reaches_as_far_as_its_lines_and_its_dimensions()
    {
        DrawingView view = new(
            StandardView.Front,
            [new DrawingLine(LineKind.Visible, new(0, 0), new(24, 6))],
            [new DrawingDimension("2'-0\"", new(0, 0), new(24, 0), new(0, -4), new(24, -4))]);
        Assert.Equal(new DrawingExtent(0, -4, 24, 6), view.Extent());
        Assert.Equal((24.0, 10.0), (view.Extent()!.Value.Width, view.Extent()!.Value.Height));
        Assert.Equal("Front", view.Name);
        Assert.Null(new DrawingView(StandardView.Top, [], []).Extent());
    }

    [Fact]
    public void Text_wraps_at_spaces_and_a_word_too_long_for_the_line_is_broken()
    {
        Excise.Core.Graphics.PdfFont font = Excise.Core.Graphics.PdfFont.Courier(10); // 6 pt a character
        Assert.Equal(["one two", "three"], SheetPdf.Wrap("one two three", font, 45));
        Assert.Equal(["abcde", "fghij", "k"], SheetPdf.Wrap("abcdefghijk", font, 30));
        Assert.Equal([string.Empty], SheetPdf.Wrap(string.Empty, font, 30));
    }
}
