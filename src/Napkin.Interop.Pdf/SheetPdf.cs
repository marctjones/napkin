using System.Globalization;

using Excise.Core.Authoring;
using Excise.Core.Document;
using Excise.Core.Graphics;

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

/// <summary>Where one view is drawn: its pane, and which point of the design lands on which point of the page.</summary>
/// <param name="View">The view.</param>
/// <param name="Pane">Its whole pane, caption included.</param>
/// <param name="Centre">The point of the view's plane, in inches, drawn at <paramref name="Origin"/>.</param>
/// <param name="Origin">Where that point is on the page.</param>
public sealed record ViewPlacement(DrawingView View, PageRect Pane, DrawingPoint Centre, PagePoint Origin)
{
    /// <summary>Where a point of the view's plane lands on the page at a scale.</summary>
    public PagePoint ToPage(DrawingPoint point, SheetScale scale) => new(
        Origin.X + ((point.Along - Centre.Along) * scale.PointsPerInch),
        Origin.Y + ((point.Across - Centre.Across) * scale.PointsPerInch));
}

/// <summary>How a sheet falls on its pages: the one scale, each view's place, and where the notes go.</summary>
/// <param name="Scale">The one scale every view is drawn at.</param>
/// <param name="Views">Each view's placement.</param>
/// <param name="DrawingArea">What the title block leaves of every page.</param>
/// <param name="NotesPane">The spare pane the notes start in.</param>
/// <param name="TitleBlock">The title block's box, the same on every page.</param>
public sealed record SheetPlacement(
    SheetScale Scale,
    IReadOnlyList<ViewPlacement> Views,
    PageRect DrawingArea,
    PageRect NotesPane,
    PageRect TitleBlock);

/// <summary>
/// The plan-and-elevations sheet as a PDF (#25, DESIGN.md §5.5): true scale, vector, US Letter
/// landscape, written with Excise.Core. The three views sit where the on-screen sheet puts them
/// (<see cref="SheetLayout.Panes"/>: Top over Front, Right beside Front) at one scale, the largest of
/// <see cref="SheetScale.Ratios"/> at which every view fits (<see cref="SheetLayout.SharedScale"/>,
/// then rounded down to a ratio), with Top's X over Front's and Front's heights beside Right's.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Drawn as the screen draws it.</strong> Each line kind takes the one table's weight and
/// dashes (<see cref="DrawingLines"/>), a screen pixel printed as a point; hidden edges first, then
/// visible ones, then dimensions, whose marks are <see cref="DimensionMarks"/>'s. Ink is black on
/// white paper, with no fills: the hidden-edge split already says what is seen.
/// </para>
/// <para>
/// <strong>Two workarounds for Excise.Core's pen</strong> (marctjones/excise#1851, which has no dash
/// pattern, cap, join or opacity yet): every dash is its own short solid line (<see cref="Dashes"/>),
/// and a line drawn at an opacity on screen is drawn in the solid grey that ink of that opacity lays
/// on white paper (<see cref="PaperGrey"/>) — exact here, because nothing but paper lies under it.
/// Lines keep PDF's default butt cap where the screen rounds a visible edge's ends.
/// </para>
/// <para>
/// <strong>Every page</strong> has the title block (<see cref="Pdf.TitleBlock"/>). The notes — the
/// rules engine's results, in the panels' words — fill the spare top-right pane and continue on
/// further pages, each titled and numbered. Text is IBM Plex Sans, embedded and subset, so a label's
/// ≈ or a citation's → prints and can be selected and searched.
/// </para>
/// </remarks>
public static class SheetPdf
{
    /// <summary>The page: US Letter, landscape (Excise.Core's <see cref="PageSize.Letter"/>).</summary>
    public static readonly PageSize Page = PageSize.Letter.Landscape();

    /// <summary>The empty border round every page, in points: half an inch.</summary>
    public const double Margin = 36;

    /// <summary>The space between the drawing area and the title block, in points.</summary>
    public const double TitleGap = 8;

    /// <summary>How far a view is kept from its pane's edges, in points: room for a label on its dimension line.</summary>
    public const double PanePadding = DimensionMarks.LabelSize;

    /// <summary>The band at the top of a pane its caption sits in, in points.</summary>
    public const double CaptionBand = 16;

    /// <summary>The size of a pane's caption.</summary>
    public const double CaptionSize = 10;

    /// <summary>The size of the notes' text; their headings are the same size, in the medium weight.</summary>
    public const double NoteSize = 8;

    /// <summary>The size of the title block's small print: the code line and the disclaimer.</summary>
    public const double SmallPrint = 7;

    /// <summary>The project name's size in the title block.</summary>
    public const double ProjectNameSize = 14;

    /// <summary>What the first page is called in its title block.</summary>
    public const string SheetTitle = "Plan and elevations";

    /// <summary>What a page of notes that did not fit on the first is called.</summary>
    public const string NotesTitle = "Code check notes, continued";

    /// <summary>Line spacing as a multiple of text size.</summary>
    const double Leading = 1.3;

    /// <summary>Space inside the title block's cells, in points.</summary>
    const double CellPad = 5;

    /// <summary>The height of the title block's first row: the project name, the scale, the date and the sheet.</summary>
    const double TitleRow = 34;

    /// <summary>
    /// The solid grey that black ink laid at an opacity leaves on white paper, as a PDF grey level
    /// (0 black, 1 white): what the screen composites, without transparency.
    /// </summary>
    /// <param name="opacity">How much of the ink is laid down, 0–1.</param>
    public static double PaperGrey(double opacity) => 1 - Math.Clamp(opacity, 0, 1);

    /// <summary>Writes the sheet to a stream as a PDF.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(PlanSheet sheet, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Document(sheet).Save(stream);
    }

    /// <summary>The sheet as a PDF document, before it is saved.</summary>
    /// <param name="sheet">The sheet.</param>
    public static PdfDocument Document(PlanSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        SheetFonts fonts = SheetFonts.Load();
        SheetPlacement placement = Place(sheet, fonts);
        List<IReadOnlyList<PlacedText>> notePages = NotePages(sheet, placement, fonts);
        int pages = Math.Max(notePages.Count, 1);

        PdfDocument document = PdfDocument.CreateNew();
        document.SetTitle(sheet.Title.ProjectName);
        document.SetCreator("napkin");
        for (int number = 1; number <= pages; number++)
        {
            PdfPage page = document.Pages.AddBlank(Page.Width, Page.Height);
            using PdfGraphics graphics = page.GetGraphics();
            Ink ink = new(graphics, fonts);
            if (number == 1)
            {
                foreach (ViewPlacement view in placement.Views)
                {
                    DrawView(ink, view, placement.Scale);
                }
            }

            if (number <= notePages.Count)
            {
                foreach (PlacedText text in notePages[number - 1])
                {
                    ink.Text(text.Text, text.Font, text.X, text.Y);
                }
            }

            DrawTitleBlock(ink, sheet, placement, number == 1 ? SheetTitle : NotesTitle, number, pages);
        }

        return document;
    }

    /// <summary>How the sheet falls on its pages: the scale, the views' places and the title block's box.</summary>
    /// <param name="sheet">The sheet.</param>
    public static SheetPlacement Place(PlanSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Place(sheet, SheetFonts.Load());
    }

    static SheetPlacement Place(PlanSheet sheet, SheetFonts fonts)
    {
        double inner = Page.Width - (2 * Margin) - (2 * CellPad);
        double smallLines = SmallPrintLines(sheet.Title, fonts, inner).Count;
        PageRect title = new(Margin, Margin, Page.Width - (2 * Margin), TitleRow + (2 * CellPad) + (smallLines * SmallPrint * Leading));
        PageRect area = new(Margin, title.Top + TitleGap, Page.Width - (2 * Margin), Page.Height - Margin - (title.Top + TitleGap));

        IReadOnlyList<SheetPane> panes = SheetLayout.Panes(area.Width, area.Height, SheetLayout.Gutter);
        PageRect OnPage(SheetPane pane) => new(area.Left + pane.Left, area.Top - pane.Top - pane.Height, pane.Width, pane.Height);
        PageRect notes = OnPage(panes.Single(pane => pane.View is null));

        Dictionary<StandardView, DrawingView> views = [];
        foreach (DrawingView view in sheet.Views)
        {
            if (!panes.Any(pane => pane.View == view.View))
            {
                throw new ArgumentException($"The sheet has no pane for the {view.Name} view.", nameof(sheet));
            }

            if (!views.TryAdd(view.View, view))
            {
                throw new ArgumentException($"The sheet has two {view.Name} views.", nameof(sheet));
            }
        }

        // One centre per shared axis, so the three line up (standard-views §11.4): Top and Front share
        // screen right (world X), Front and Right share screen up (world Z).
        DrawingExtent? Of(StandardView view) => views.TryGetValue(view, out DrawingView? drawn) ? drawn.Extent() : null;
        DrawingExtent? top = Of(StandardView.Top), front = Of(StandardView.Front), right = Of(StandardView.Right);
        double? sharedAlong = Middle(top?.Left, top?.Right, front?.Left, front?.Right);
        double? sharedUp = Middle(front?.Bottom, front?.Top, right?.Bottom, right?.Top);

        List<(DrawingView View, PageRect Pane, PageRect Usable, DrawingPoint Centre)> laid = [];
        List<(SheetPane Pane, double Across, double Up)> needs = [];
        foreach (SheetPane pane in panes)
        {
            if (pane.View is not { } which || !views.TryGetValue(which, out DrawingView? view))
            {
                continue;
            }

            PageRect onPage = OnPage(pane);
            PageRect usable = onPage.Inset(PanePadding, CaptionBand);
            DrawingExtent? extent = view.Extent();
            double centreAlong = which is StandardView.Top or StandardView.Front && sharedAlong is { } along
                ? along
                : extent is { } e1 ? (e1.Left + e1.Right) / 2 : 0;
            double centreUp = which is StandardView.Front or StandardView.Right && sharedUp is { } up
                ? up
                : extent is { } e2 ? (e2.Bottom + e2.Top) / 2 : 0;
            laid.Add((view, onPage, usable, new DrawingPoint(centreAlong, centreUp)));
            if (extent is { } e)
            {
                double halfAcross = Math.Max(Math.Abs(e.Left - centreAlong), Math.Abs(e.Right - centreAlong));
                double halfUp = Math.Max(Math.Abs(e.Bottom - centreUp), Math.Abs(e.Top - centreUp));
                needs.Add((new SheetPane(which, usable.Left, 0, usable.Width, usable.Height), 2 * halfAcross, 2 * halfUp));
            }
        }

        SheetScale scale = SheetLayout.SharedScale(needs, 0) is { } fits ? SheetScale.AtMost(fits) : new SheetScale(1);
        return new SheetPlacement(
            scale,
            [.. laid.Select(entry => new ViewPlacement(entry.View, entry.Pane, entry.Centre, entry.Usable.Centre))],
            area,
            notes,
            title);
    }

    /// <summary>The middle of every end given; null when none is.</summary>
    static double? Middle(params double?[] ends)
    {
        double[] known = [.. ends.OfType<double>()];
        return known.Length == 0 ? null : (known.Min() + known.Max()) / 2;
    }

    /// <summary>A view: its caption, its hidden edges, its visible edges, then its dimensions.</summary>
    static void DrawView(Ink ink, ViewPlacement placement, SheetScale scale)
    {
        ink.Text(placement.View.Name, ink.Fonts.Regular(CaptionSize), placement.Pane.Left + 8, placement.Pane.Top - 6 - CaptionSize);
        PagePoint At(DrawingPoint point) => placement.ToPage(point, scale);

        foreach (LineKind kind in (LineKind[])[LineKind.Hidden, LineKind.Visible])
        {
            foreach (DrawingLine line in placement.View.Lines.Where(line => line.Kind == kind))
            {
                ink.Line(At(line.From), At(line.To), DrawingLines.Of(line.Kind), line.Dashed ? DrawingLines.DashedOutline : null);
            }
        }

        foreach (DrawingDimension dimension in placement.View.Dimensions)
        {
            DrawDimension(ink, (At(dimension.From), At(dimension.To)), (At(dimension.LineFrom), At(dimension.LineTo)), dimension.Label);
        }
    }

    /// <summary>
    /// A dimension exactly as the canvas draws one (its <c>DrawDimensionAt</c>): extension lines from a
    /// gap off what is measured to past the line, the line, an arrowhead at each end — outside,
    /// pointing in, when the line is too short for them — and the label on a white chip that breaks
    /// the line, reading up the page when the line runs up it.
    /// </summary>
    static void DrawDimension(Ink ink, (PagePoint From, PagePoint To) measured, (PagePoint From, PagePoint To) line, string label)
    {
        (double X, double Y) along = (line.To.X - line.From.X, line.To.Y - line.From.Y);
        double length = Math.Sqrt((along.X * along.X) + (along.Y * along.Y));
        if (length < 2)
        {
            return;
        }

        LineStyle thin = DrawingLines.Of(LineKind.Dimension);
        (double X, double Y) direction = (along.X / length, along.Y / length);
        ExtensionLine(ink, measured.From, line.From);
        ExtensionLine(ink, measured.To, line.To);

        bool tight = length < DimensionMarks.TightArrowheads * DimensionMarks.ArrowLength;
        double stub = tight ? DimensionMarks.ArrowLength : 0;
        ink.Line(
            new PagePoint(line.From.X - (direction.X * stub), line.From.Y - (direction.Y * stub)),
            new PagePoint(line.To.X + (direction.X * stub), line.To.Y + (direction.Y * stub)),
            thin,
            null);
        double inward = tight ? 1 : -1;
        ink.Arrowhead(line.From, (direction.X * inward, direction.Y * inward));
        ink.Arrowhead(line.To, (-direction.X * inward, -direction.Y * inward));

        PagePoint centre = new((line.From.X + line.To.X) / 2, (line.From.Y + line.To.Y) / 2);
        bool vertical = Math.Abs(direction.Y) > Math.Abs(direction.X);
        ink.Label(label, centre, vertical);
    }

    static void ExtensionLine(Ink ink, PagePoint measured, PagePoint line)
    {
        (double X, double Y) away = (line.X - measured.X, line.Y - measured.Y);
        double length = Math.Sqrt((away.X * away.X) + (away.Y * away.Y));
        if (length <= DimensionMarks.ExtensionGap + 1)
        {
            return;
        }

        (double X, double Y) direction = (away.X / length, away.Y / length);
        ink.Line(
            new PagePoint(measured.X + (direction.X * DimensionMarks.ExtensionGap), measured.Y + (direction.Y * DimensionMarks.ExtensionGap)),
            new PagePoint(line.X + (direction.X * DimensionMarks.ExtensionOvershoot), line.Y + (direction.Y * DimensionMarks.ExtensionOvershoot)),
            DrawingLines.Of(LineKind.Extension),
            null);
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
    /// The title block: the project and what the page is, the scale in ratio and in words, the date and
    /// the sheet's number; under them the adopted code and the disclaimer, in full.
    /// </summary>
    static void DrawTitleBlock(Ink ink, PlanSheet sheet, SheetPlacement placement, string pageTitle, int number, int pages)
    {
        PageRect box = placement.TitleBlock;
        LineStyle rule = DrawingLines.Of(LineKind.Dimension);
        double rowBottom = box.Top - TitleRow - (2 * CellPad);
        double scaleLeft = box.Left + (box.Width * 0.5), dateLeft = box.Left + (box.Width * 0.75);

        ink.Line(new(box.Left, box.Bottom), new(box.Right, box.Bottom), rule, null);
        ink.Line(new(box.Right, box.Bottom), new(box.Right, box.Top), rule, null);
        ink.Line(new(box.Right, box.Top), new(box.Left, box.Top), rule, null);
        ink.Line(new(box.Left, box.Top), new(box.Left, box.Bottom), rule, null);
        ink.Line(new(box.Left, rowBottom), new(box.Right, rowBottom), rule, null);
        ink.Line(new(scaleLeft, rowBottom), new(scaleLeft, box.Top), rule, null);
        ink.Line(new(dateLeft, rowBottom), new(dateLeft, box.Top), rule, null);

        double firstLine = box.Top - CellPad - ProjectNameSize;
        double secondLine = rowBottom + CellPad + 2;
        ink.Text(sheet.Title.ProjectName, ink.Fonts.Medium(ProjectNameSize), box.Left + CellPad, firstLine);
        ink.Text($"{pageTitle} · {StandardViewWords.ThirdAngle}", ink.Fonts.Regular(NoteSize), box.Left + CellPad, secondLine);

        ink.Text($"Scale {placement.Scale.Label}", ink.Fonts.Medium(11), scaleLeft + CellPad, firstLine);
        ink.Text(placement.Scale.InWords(sheet.Format), ink.Fonts.Regular(NoteSize), scaleLeft + CellPad, secondLine);

        ink.Text($"Date {sheet.Title.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ink.Fonts.Regular(10), dateLeft + CellPad, firstLine);
        ink.Text(
            $"Sheet {number.ToString(CultureInfo.InvariantCulture)} of {pages.ToString(CultureInfo.InvariantCulture)}",
            ink.Fonts.Regular(NoteSize),
            dateLeft + CellPad,
            secondLine);

        PdfFont small = ink.Fonts.Regular(SmallPrint);
        double y = rowBottom - CellPad - SmallPrint;
        foreach (string line in SmallPrintLines(sheet.Title, ink.Fonts, box.Width - (2 * CellPad)))
        {
            ink.Text(line, small, box.Left + CellPad, y);
            y -= SmallPrint * Leading;
        }
    }

    /// <summary>A line of text at its baseline, in the font it is set in.</summary>
    readonly record struct PlacedText(string Text, PdfFont Font, double X, double Y);

    /// <summary>
    /// The notes laid out page by page: the heading, then each note's heading and its lines, wrapped,
    /// filling the spare pane and then the drawing area of each further page. Empty with no notes.
    /// </summary>
    static List<IReadOnlyList<PlacedText>> NotePages(PlanSheet sheet, SheetPlacement placement, SheetFonts fonts)
    {
        List<IReadOnlyList<PlacedText>> pages = [];
        if (sheet.Notes.Count == 0)
        {
            return pages;
        }

        PdfFont heading = fonts.Medium(NoteSize + 1), title = fonts.Medium(NoteSize), body = fonts.Regular(NoteSize);
        List<(string Text, PdfFont Font, double Indent, double Before)> items = [(sheet.NotesHeading, heading, 0, 0)];
        foreach (SheetNote note in sheet.Notes)
        {
            items.Add((note.Heading, title, 0, NoteSize * 0.6));
            items.AddRange(note.Lines.Select(line => (line, body, NoteSize, 0.0)));
        }

        PageRect first = placement.NotesPane.Inset(PanePadding), rest = placement.DrawingArea.Inset(PanePadding);
        List<PlacedText> page = [];
        PageRect area = first;
        double y = area.Top;
        foreach ((string text, PdfFont font, double indent, double spaceBefore) in items)
        {
            double step = font.Size * Leading, before = spaceBefore;
            foreach (string line in Wrap(text, font, area.Width - indent))
            {
                double baseline = y - (page.Count == 0 ? 0 : before) - font.Size;
                if (baseline < area.Bottom && page.Count > 0)
                {
                    pages.Add(page);
                    page = [];
                    area = rest;
                    baseline = area.Top - font.Size;
                }

                page.Add(new PlacedText(line, font, area.Left + indent, baseline));
                y = baseline - (step - font.Size);
                before = 0;
            }
        }

        pages.Add(page);
        return pages;
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
                int fits = 1;
                while (fits < current.Length && font.MeasureWidth(current[..(fits + 1)]) <= width)
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

    /// <summary>What the sheet draws with: one page's graphics, black ink on white, and the embedded fonts.</summary>
    sealed class Ink(PdfGraphics graphics, SheetFonts fonts)
    {
        public SheetFonts Fonts { get; } = fonts;

        /// <summary>A line in a kind's weight and ink, cut into its dashes (the table's, or <paramref name="dashes"/>), each a solid line.</summary>
        public void Line(PagePoint from, PagePoint to, LineStyle style, IReadOnlyList<double>? dashes)
        {
            PdfPen pen = new(PdfColor.FromGray(PaperGrey(style.Opacity)), style.Pixels);
            foreach (PageSegment piece in Dashes.Along(from, to, dashes ?? style.Dashes))
            {
                graphics.DrawLine(piece.From.X, piece.From.Y, piece.To.X, piece.To.Y, pen);
            }
        }

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
    }
}
