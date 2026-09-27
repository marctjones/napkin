using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

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
/// on white paper (<see cref="SheetInk.PaperGrey"/>) — exact here, because nothing but paper lies under it.
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
    /// <summary>How far a view is kept from its pane's edges, in points: room for a label on its dimension line.</summary>
    public const double PanePadding = DimensionMarks.LabelSize;

    /// <summary>The band at the top of a pane its caption sits in, in points.</summary>
    public const double CaptionBand = 16;

    /// <summary>The size of a pane's caption.</summary>
    public const double CaptionSize = 10;

    /// <summary>The size of the notes' text; their headings are the same size, in the medium weight.</summary>
    public const double NoteSize = 8;

    /// <summary>What the first page is called in its title block.</summary>
    public const string SheetTitle = "Plan and elevations";

    /// <summary>What a page of notes that did not fit on the first is called.</summary>
    public const string NotesTitle = "Code check notes, continued";

    /// <summary>What the first page's title block says it is: the sheet and how its views are arranged.</summary>
    public static string FirstPageTitle => $"{SheetTitle} · {StandardViewWords.ThirdAngle}";

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
        SheetFrame frame = SheetFrame.For(sheet.Title, fonts);
        SheetPlacement placement = Place(sheet, frame);
        List<IReadOnlyList<PlacedText>> notes = TextFlow.Lay(
            NoteItems(sheet, fonts),
            placement.NotesPane.Inset(PanePadding),
            placement.DrawingArea.Inset(PanePadding));

        List<SetPage> pages =
        [
            new(FirstPageTitle, placement.Scale, ink =>
            {
                foreach (ViewPlacement view in placement.Views)
                {
                    DrawView(ink, view, placement.Scale);
                }

                if (notes.Count > 0)
                {
                    ink.Text(notes[0]);
                }
            }),
            .. notes.Skip(1).Select(lines => new SetPage(NotesTitle, null, ink => ink.Text(lines))),
        ];
        return SheetSet.Document(sheet.Title, sheet.Format, frame, fonts, pages);
    }

    /// <summary>How the sheet falls on its pages: the scale, the views' places and the title block's box.</summary>
    /// <param name="sheet">The sheet.</param>
    public static SheetPlacement Place(PlanSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Place(sheet, SheetFrame.For(sheet.Title));
    }

    static SheetPlacement Place(PlanSheet sheet, SheetFrame frame)
    {
        PageRect area = frame.DrawingArea, title = frame.TitleBlock;

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
    static void DrawView(SheetInk ink, ViewPlacement placement, SheetScale scale)
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
    static void DrawDimension(SheetInk ink, (PagePoint From, PagePoint To) measured, (PagePoint From, PagePoint To) line, string label)
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

    static void ExtensionLine(SheetInk ink, PagePoint measured, PagePoint line)
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

    /// <summary>The notes as paragraphs: the heading, then each note's heading and its lines, indented.</summary>
    static List<FlowItem> NoteItems(PlanSheet sheet, SheetFonts fonts)
    {
        List<FlowItem> items = [];
        if (sheet.Notes.Count == 0)
        {
            return items;
        }

        PdfFont heading = fonts.Medium(NoteSize + 1), title = fonts.Medium(NoteSize), body = fonts.Regular(NoteSize);
        items.Add(new FlowItem(sheet.NotesHeading, heading));
        foreach (SheetNote note in sheet.Notes)
        {
            items.Add(new FlowItem(note.Heading, title, 0, NoteSize * 0.6));
            items.AddRange(note.Lines.Select(line => new FlowItem(line, body, NoteSize)));
        }

        return items;
    }
}
