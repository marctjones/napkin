using Excise.Core.Graphics;

using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>
/// What a sheet draws with: one page's graphics, black ink on white paper, and the embedded fonts.
/// Every stroke takes a kind's weight from the one line table (<see cref="DrawingLines"/>), a screen
/// pixel printed as a point.
/// </summary>
/// <remarks>
/// Two workarounds for Excise.Core's pen (marctjones/excise#1851, no dash pattern, cap, join or
/// opacity yet): a dashed line is its dashes, each a solid <see cref="PdfGraphics.DrawLine"/>
/// (<see cref="Dashes"/>), and a line drawn at an opacity on screen is drawn in the solid grey that
/// ink of that opacity lays on white paper (<see cref="PaperGrey"/>) — exact wherever nothing but
/// paper lies under it, which on a sheet with no fills is everywhere. <c>DrawLine</c> and
/// <c>DrawRectangle</c> set colour and width before their path, so the page's strokes do too.
/// </remarks>
/// <param name="graphics">The page's graphics.</param>
/// <param name="fonts">The document's fonts.</param>
internal sealed class SheetInk(PdfGraphics graphics, SheetFonts fonts)
{
    /// <summary>The document's fonts.</summary>
    public SheetFonts Fonts { get; } = fonts;

    /// <summary>
    /// The solid grey that black ink laid at an opacity leaves on white paper, as a PDF grey level
    /// (0 black, 1 white): what the screen composites, without transparency.
    /// </summary>
    /// <param name="opacity">How much of the ink is laid down, 0–1.</param>
    public static double PaperGrey(double opacity) => 1 - Math.Clamp(opacity, 0, 1);

    /// <summary>A line in a kind's weight and ink, cut into its dashes (the table's, or <paramref name="dashes"/>), each a solid line.</summary>
    public void Line(PagePoint from, PagePoint to, LineStyle style, IReadOnlyList<double>? dashes)
    {
        PdfPen pen = Pen(style);
        foreach (PageSegment piece in Dashes.Along(from, to, dashes ?? style.Dashes))
        {
            graphics.DrawLine(piece.From.X, piece.From.Y, piece.To.X, piece.To.Y, pen);
        }
    }

    /// <summary>A rectangle's outline in a kind's weight and ink; solid.</summary>
    public void Rectangle(PageRect rect, LineStyle style) =>
        graphics.DrawRectangle(rect.Left, rect.Bottom, rect.Width, rect.Height, null, Pen(style));

    /// <summary>A filled arrowhead with its tip at <paramref name="tip"/>, pointing along <paramref name="pointing"/> (a unit vector).</summary>
    public void Arrowhead(PagePoint tip, (double X, double Y) pointing)
    {
        (double X, double Y) back = (-pointing.X * DimensionMarks.ArrowLength, -pointing.Y * DimensionMarks.ArrowLength);
        (double X, double Y) side = (-pointing.Y * DimensionMarks.ArrowHalfWidth, pointing.X * DimensionMarks.ArrowHalfWidth);
        graphics.MoveTo(tip.X, tip.Y);
        graphics.LineTo(tip.X + back.X + side.X, tip.Y + back.Y + side.Y);
        graphics.LineTo(tip.X + back.X - side.X, tip.Y + back.Y - side.Y);
        graphics.ClosePath();
        graphics.Fill(PdfBrush.Black);
    }

    /// <summary>A dimension's label centred on a point, on a white chip, turned to read up the page when <paramref name="vertical"/>.</summary>
    public void Label(string text, PagePoint centre, bool vertical)
    {
        PdfFont font = Fonts.Regular(DimensionMarks.LabelSize);
        double width = font.MeasureWidth(text), height = font.Ascender + font.Descender;
        graphics.SaveState();
        graphics.Transform(vertical ? 0 : 1, vertical ? 1 : 0, vertical ? -1 : 0, vertical ? 0 : 1, centre.X, centre.Y);
        graphics.DrawRectangle(
            -(width / 2) - DimensionMarks.ChipPadAlong,
            -(height / 2) - DimensionMarks.ChipPadAcross,
            width + (2 * DimensionMarks.ChipPadAlong),
            height + (2 * DimensionMarks.ChipPadAcross),
            PdfBrush.White);
        graphics.DrawString(text, font, PdfBrush.Black, -(width / 2), (font.Descender - font.Ascender) / 2);
        graphics.RestoreState();
    }

    /// <summary>A line of text with its baseline starting at a point.</summary>
    public void Text(string text, PdfFont font, double x, double y) => graphics.DrawString(text, font, PdfBrush.Black, x, y);

    /// <summary>A line of text centred on a point along its baseline.</summary>
    public void CentredText(string text, PdfFont font, double x, double y) =>
        graphics.DrawString(text, font, PdfBrush.Black, x, y, TextAlignment.Center);

    /// <summary>Text laid out beforehand, line by line.</summary>
    public void Text(IEnumerable<PlacedText> lines)
    {
        foreach (PlacedText line in lines)
        {
            Text(line.Text, line.Font, line.X, line.Y);
        }
    }

    static PdfPen Pen(LineStyle style) => new(PdfColor.FromGray(PaperGrey(style.Opacity)), style.Pixels);
}
