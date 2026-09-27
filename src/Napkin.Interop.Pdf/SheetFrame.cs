using System.Globalization;

using Excise.Core.Authoring;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>A rectangle of the page, in PDF points, y up.</summary>
/// <param name="Left">Its left edge.</param>
/// <param name="Bottom">Its bottom edge.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public readonly record struct PageRect(double Left, double Bottom, double Width, double Height)
{
    /// <summary>Its right edge.</summary>
    public double Right => Left + Width;

    /// <summary>Its top edge.</summary>
    public double Top => Bottom + Height;

    /// <summary>Its centre.</summary>
    public PagePoint Centre => new(Left + (Width / 2), Bottom + (Height / 2));

    /// <summary>The same rectangle less <paramref name="all"/> on every side, and <paramref name="top"/> more off its top.</summary>
    public PageRect Inset(double all, double top = 0) =>
        new(Left + all, Bottom + all, Math.Max(Width - (2 * all), 0), Math.Max(Height - (2 * all) - top, 0));
}

/// <summary>The paper a set is printed on, landscape.</summary>
/// <param name="Name">What the person chooses it by.</param>
/// <param name="Size">Its size, landscape, in points.</param>
public sealed record SheetPaper(string Name, PageSize Size)
{
    /// <summary>US Letter, 11 × 8 1/2 in landscape: Excise.Core's <see cref="PageSize.Letter"/>.</summary>
    public static SheetPaper Letter { get; } = new("Letter", PageSize.Letter.Landscape());

    /// <summary>
    /// Tabloid, 17 × 11 in landscape: "11-inch by 17-inch (279.4 mm by 431.8 mm)", the least paper San
    /// Francisco's building department takes plans on (sf.gov, "Building project plans for full permits",
    /// read 2026-09-27); docs/design/permit-set.md §4 offers Letter or Tabloid.
    /// </summary>
    public static SheetPaper Tabloid { get; } = new("Tabloid", new PageSize(17 * SheetScale.PointsPerPaperInch, 11 * SheetScale.PointsPerPaperInch));
}

/// <summary>
/// Where things go on every page of a set (#25, and the sets built on it): the page, the title block's
/// box along its foot — as tall as its small print needs, the same on every page — and the drawing
/// area it leaves under the top margin.
/// </summary>
/// <param name="Page">The page.</param>
/// <param name="TitleBlock">The title block's box.</param>
/// <param name="DrawingArea">What the title block leaves.</param>
public sealed record SheetFrame(PageSize Page, PageRect TitleBlock, PageRect DrawingArea)
{
    /// <summary>The empty border round every page, in points: half an inch.</summary>
    public const double Margin = 36;

    /// <summary>The space between the drawing area and the title block, in points.</summary>
    public const double TitleGap = 8;

    /// <summary>The size of the title block's small print: the code line and the disclaimer.</summary>
    public const double SmallPrint = 7;

    /// <summary>The project name's size in the title block.</summary>
    public const double ProjectNameSize = 14;

    /// <summary>The size of a page's secondary text: its title, the scale in words, the sheet's number.</summary>
    public const double TextSize = 8;

    /// <summary>Line spacing as a multiple of text size.</summary>
    public const double Leading = 1.3;

    /// <summary>Space inside the title block's cells, in points.</summary>
    const double CellPad = 5;

    /// <summary>The height of the title block's first row: the project name, the scale, the date and the sheet.</summary>
    const double TitleRow = 34;

    /// <summary>The height of the band a banner takes across the top of the title block.</summary>
    public const double BannerBand = 18;

    /// <summary>The frame for a title block on a page of the given paper, Letter unless said.</summary>
    /// <param name="title">What the title block says.</param>
    /// <param name="paper">The paper; Letter when null.</param>
    public static SheetFrame For(TitleBlock title, SheetPaper? paper = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        return For(title, SheetFonts.Load(), paper);
    }

    internal static SheetFrame For(TitleBlock title, SheetFonts fonts, SheetPaper? paper = null)
    {
        PageSize page = (paper ?? SheetPaper.Letter).Size;
        double inner = page.Width - (2 * Margin) - (2 * CellPad);
        double smallLines = SmallPrintLines(title, fonts, inner).Count;
        double banner = title.Banner is null ? 0 : BannerBand;
        PageRect box = new(Margin, Margin, page.Width - (2 * Margin), banner + TitleRow + (2 * CellPad) + (smallLines * SmallPrint * Leading));
        PageRect area = new(Margin, box.Top + TitleGap, page.Width - (2 * Margin), page.Height - Margin - (box.Top + TitleGap));
        return new SheetFrame(page, box, area);
    }

    /// <summary>The code line and the disclaimer, wrapped to the title block's width.</summary>
    static List<string> SmallPrintLines(TitleBlock title, SheetFonts fonts, double width)
    {
        PdfFont small = fonts.Regular(SmallPrint);
        List<string> lines = [];
        if (title.Code is { Length: > 0 } code)
        {
            lines.AddRange(Wrap(code, small, width));
        }

        lines.AddRange(Wrap(title.Disclaimer, small, width));
        return lines;
    }

    /// <summary>
    /// The title block: the project and what the page is, the scale as a ratio and in words (or that
    /// the page is drawn to none), the date and the sheet's number; under them the adopted code and
    /// the disclaimer, in full.
    /// </summary>
    /// <param name="ink">The page's ink.</param>
    /// <param name="title">What every page's title block says.</param>
    /// <param name="format">How lengths are written: the scale in words.</param>
    /// <param name="pageTitle">What this page is.</param>
    /// <param name="scale">The scale this page is drawn at; null for a page drawn to none.</param>
    /// <param name="number">The page's number, from 1.</param>
    /// <param name="pages">How many pages the set has.</param>
    internal void DrawTitleBlock(SheetInk ink, TitleBlock title, LengthFormat format, string pageTitle, SheetScale? scale, int number, int pages)
    {
        PageRect box = TitleBlock;
        LineStyle rule = DrawingLines.Of(LineKind.Dimension);
        if (title.Banner is { } banner)
        {
            // Across the top of the title block, in the heavy line's weight, so no sheet of the set can
            // be taken for complete (docs/design/permit-set.md §3).
            PageRect band = new(box.Left, box.Top - BannerBand, box.Width, BannerBand);
            ink.Rectangle(band, DrawingLines.Of(LineKind.Visible));
            ink.Text(banner, ink.Fonts.Medium(10), band.Left + CellPad, band.Bottom + 5);
            box = box with { Height = box.Height - BannerBand };
        }

        double rowBottom = box.Top - TitleRow - (2 * CellPad);
        double scaleLeft = box.Left + (box.Width * 0.5), dateLeft = box.Left + (box.Width * 0.75);

        ink.Rectangle(box, rule);
        ink.Line(new(box.Left, rowBottom), new(box.Right, rowBottom), rule, null);
        ink.Line(new(scaleLeft, rowBottom), new(scaleLeft, box.Top), rule, null);
        ink.Line(new(dateLeft, rowBottom), new(dateLeft, box.Top), rule, null);

        double firstLine = box.Top - CellPad - ProjectNameSize;
        double secondLine = rowBottom + CellPad + 2;
        ink.Text(title.ProjectName, ink.Fonts.Medium(ProjectNameSize), box.Left + CellPad, firstLine);
        ink.Text(pageTitle, ink.Fonts.Regular(TextSize), box.Left + CellPad, secondLine);

        ink.Text(scale is { } drawn ? $"Scale {drawn.Label}" : "Scale —", ink.Fonts.Medium(11), scaleLeft + CellPad, firstLine);
        ink.Text(scale is { } words ? words.InWords(format) : "not drawn to a scale", ink.Fonts.Regular(TextSize), scaleLeft + CellPad, secondLine);

        ink.Text($"Date {title.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ink.Fonts.Regular(10), dateLeft + CellPad, firstLine);
        ink.Text(
            $"Sheet {number.ToString(CultureInfo.InvariantCulture)} of {pages.ToString(CultureInfo.InvariantCulture)}",
            ink.Fonts.Regular(TextSize),
            dateLeft + CellPad,
            secondLine);

        PdfFont small = ink.Fonts.Regular(SmallPrint);
        double y = rowBottom - CellPad - SmallPrint;
        foreach (string line in SmallPrintLines(title, ink.Fonts, box.Width - (2 * CellPad)))
        {
            ink.Text(line, small, box.Left + CellPad, y);
            y -= SmallPrint * Leading;
        }
    }

    /// <summary>Greedy word wrap to a width, by the font's own measure; a word wider than the line is broken where it must be.</summary>
    internal static IReadOnlyList<string> Wrap(string text, PdfFont font, double width)
    {
        List<string> lines = [];
        string current = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureWidth(candidate) <= width)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            current = word;
            while (current.Length > 1 && font.MeasureWidth(current) > width)
            {
                // The whole word does not fit, so this stops before its last character.
                int fits = 1;
                while (font.MeasureWidth(current[..(fits + 1)]) <= width)
                {
                    fits++;
                }

                lines.Add(current[..fits]);
                current = current[fits..];
            }
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current);
        }

        return lines;
    }
}

/// <summary>A line of text at its baseline, in the font it is set in, and a blank to write on after it.</summary>
/// <param name="Text">The text.</param>
/// <param name="Font">Its font and size.</param>
/// <param name="X">Where its baseline starts.</param>
/// <param name="Y">Its baseline.</param>
/// <param name="Blank">How long a rule to leave after the text for a hand to write on; 0 for none.</param>
internal readonly record struct PlacedText(string Text, PdfFont Font, double X, double Y, double Blank = 0);

/// <summary>One paragraph of flowing text: what it says, in what, how far in, the space before it, and a blank after it.</summary>
/// <param name="Text">What it says; wrapped to fit.</param>
/// <param name="Font">Its font and size.</param>
/// <param name="Indent">How far in from the area's left edge.</param>
/// <param name="Before">Extra space above it, unless it starts an area.</param>
/// <param name="Blank">How long a rule to leave after its last line; 0 for none.</param>
internal readonly record struct FlowItem(string Text, PdfFont Font, double Indent = 0, double Before = 0, double Blank = 0);

/// <summary>Text laid out down an area and on into further ones: notes, statements, anything that reads down a page.</summary>
internal static class TextFlow
{
    /// <summary>
    /// The paragraphs laid out line by line: into <paramref name="first"/>, then page after page of
    /// <paramref name="rest"/>. One list of lines per area used; empty with nothing to lay out.
    /// </summary>
    public static List<IReadOnlyList<PlacedText>> Lay(IReadOnlyList<FlowItem> items, PageRect first, PageRect rest)
    {
        List<IReadOnlyList<PlacedText>> pages = [];
        if (items.Count == 0)
        {
            return pages;
        }

        List<PlacedText> page = [];
        PageRect area = first;
        double y = area.Top;
        foreach (FlowItem item in items)
        {
            double before = item.Before;
            IReadOnlyList<string> lines = SheetFrame.Wrap(item.Text, item.Font, area.Width - item.Indent - item.Blank);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                double baseline = y - (page.Count == 0 ? 0 : before) - item.Font.Size;
                if (baseline < area.Bottom && page.Count > 0)
                {
                    pages.Add(page);
                    page = [];
                    area = rest;
                    baseline = area.Top - item.Font.Size;
                }

                page.Add(new PlacedText(line, item.Font, area.Left + item.Indent, baseline, i == lines.Count - 1 ? item.Blank : 0));
                y = baseline - (item.Font.Size * (SheetFrame.Leading - 1));
                before = 0;
            }
        }

        pages.Add(page);
        return pages;
    }
}
