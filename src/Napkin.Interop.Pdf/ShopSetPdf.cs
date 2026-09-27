using System.Globalization;

using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Napkin.Modules.Furniture;

namespace Napkin.Interop.Pdf;

/// <summary>
/// The shop set as a PDF (#211): the cut list, the cut layout drawn — every board as a bar and every
/// sheet as a rectangle, pieces labelled, kerf marked, offcuts hatched, grain arrowed — and a sheet
/// of labels, one per piece. Every page is in #25's frame, with its title block and disclaimer.
/// </summary>
/// <remarks>
/// <para>
/// <strong>One set of numbers.</strong> Every size, count, name and board on paper is text the
/// cut-list window already shows: a row's <see cref="CutListRow.LengthText"/> and the rest, a layout
/// line's <see cref="CutLayout.Line"/>, the layout's <see cref="CutLayout.Summary"/>. Nothing is
/// measured or rounded here (docs/design/parts-and-cut-list.md §4.1: rounded once, at the end, by
/// the rows).
/// </para>
/// <para>
/// <strong>The drawings</strong> are at one scale, boards and sheets alike, as on screen: the largest
/// of <see cref="SheetScale.Ratios"/> at which the longest board or sheet fits the page's width. A
/// sheet's pieces sit where <see cref="Napkin.Modules.Furniture.SheetLayout"/> put them, measured
/// from its top-left corner as the screen draws it; a piece whose grain is set carries an arrow along
/// the sheet's long side, which is where the layout keeps a grained piece's grain.
/// </para>
/// </remarks>
public static class ShopSetPdf
{
    /// <summary>What the cut list's pages are called.</summary>
    public const string CutListTitle = "Cut list";

    /// <summary>What the cut layout's pages are called.</summary>
    public const string LayoutTitle = "Cut layout: boards and sheets";

    /// <summary>What the labels' pages are called.</summary>
    public const string LabelsTitle = "Part labels: cut out, one per piece";

    /// <summary>The cut list's columns, as the file names them, and each one's share of the width.</summary>
    public static IReadOnlyList<(string Name, double Share)> Columns { get; } =
    [
        ("Label", 0.17), ("Qty", 0.05), ("Length", 0.09), ("Width", 0.09), ("Thickness", 0.09),
        ("Material", 0.15), ("Rough", 0.06), ("Cuts", 0.15), ("Joinery", 0.15),
    ];

    /// <summary>How many labels across a page, and down it.</summary>
    public const int LabelColumns = 3, LabelRows = 8;

    /// <summary>How tall a board's bar is drawn, in points: the screen's bar height (<c>CutLayoutBar.BarHeight</c>).</summary>
    public const double BarHeight = 18;

    /// <summary>The hatch's step, in points: the screen's.</summary>
    public const double HatchStep = 5;

    /// <summary>How the hatch is drawn: the screen's, the stroke at 0.55 opacity, one pixel (a point here).</summary>
    public static readonly LineStyle HatchLine = new(1, [], 0.55);

    /// <summary>How a piece's outline is drawn: the screen's, one pixel (a point here).</summary>
    public static readonly LineStyle PieceLine = new(1, [], 1);

    /// <summary>How a kerf mark is drawn: the screen's, two pixels (points here).</summary>
    public static readonly LineStyle KerfLine = new(2, [], 1);

    /// <summary>The text size of the tables, captions and labels.</summary>
    const double Text = SheetFrame.TextSize;

    /// <summary>Space inside a table cell, in points.</summary>
    const double CellPad = 3;

    /// <summary>Space around the content inside the drawing area, in points.</summary>
    const double Pad = 12;

    /// <summary>Writes the shop set to a stream as a PDF.</summary>
    /// <param name="set">The shop set.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(ShopSet set, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Document(set).Save(stream);
    }

    /// <summary>The shop set as a PDF document, before it is saved.</summary>
    /// <param name="set">The shop set.</param>
    public static PdfDocument Document(ShopSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        SheetFonts fonts = SheetFonts.Load();
        SheetFrame frame = SheetFrame.For(set.Title, fonts);
        PageRect area = frame.DrawingArea.Inset(Pad);

        List<SetPage> pages = [.. CutListPages(set, area, fonts)];
        if (!set.Rows.IsEmpty)
        {
            pages.AddRange(LayoutPages(set, area, fonts));
            pages.AddRange(LabelPages(set, area, fonts));
        }

        return SheetSet.Document(set.Title, set.Format, frame, fonts, pages);
    }

    /// <summary>The scale every board and sheet is drawn at: the largest ratio at which the longest fits the width.</summary>
    /// <param name="layout">The cut layout.</param>
    /// <param name="width">The width available, in points.</param>
    public static SheetScale LayoutScale(CutLayoutPlan layout, double width)
    {
        ArgumentNullException.ThrowIfNull(layout);
        long longest = layout.AllBoards.Select(board => board.StockLength.Units)
            .Concat(layout.AllSheets.Select(sheet => sheet.Long.Units))
            .DefaultIfEmpty(0)
            .Max();
        return longest == 0 ? new SheetScale(1) : SheetScale.AtMost(width / (longest / (double)Length.UnitsPerInch));
    }

    /// <summary>The fields of a cut-list row in the file's order (<see cref="CutListCsv.Header"/>): what each cell prints.</summary>
    /// <param name="row">The row.</param>
    public static IReadOnlyList<string> Cells(CutListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return
        [
            row.Label,
            row.Quantity.ToString(CultureInfo.InvariantCulture),
            row.LengthText,
            row.WidthText,
            row.ThicknessText,
            row.MaterialText,
            row.Rough ? "yes" : string.Empty,
            string.Join(CutListCsv.BetweenCuts, row.CutText),
            string.Join(CutListCsv.BetweenCuts, row.JointText.AddRange(row.Flags)),
        ];
    }

    /// <summary>
    /// The cut list as a table: its statement, then a header and the rows, each cell wrapped in its
    /// column, repeated on as many pages as it takes; the rough rows' footer after the last.
    /// </summary>
    static IEnumerable<SetPage> CutListPages(ShopSet set, PageRect area, SheetFonts fonts)
    {
        PdfFont body = fonts.Regular(Text), bold = fonts.Medium(Text);
        double step = Text * SheetFrame.Leading;
        double[] widths = [.. Columns.Select(column => column.Share * area.Width)];

        List<List<PlacedText>> pages = [];
        List<(double Y, double Height)> ruleRows = [];
        List<List<(double Y, double Height)>> rules = [];
        List<PlacedText> page = [];
        double y = area.Top;

        void Start()
        {
            page = [];
            ruleRows = [];
            pages.Add(page);
            rules.Add(ruleRows);
            y = area.Top;
        }

        void Paragraph(string text, PdfFont font)
        {
            foreach (string line in SheetFrame.Wrap(text, font, area.Width))
            {
                page.Add(new PlacedText(line, font, area.Left, y - Text));
                y -= step;
            }

            y -= step / 2;
        }

        // A row of cells: each cell's lines together, left to right, so the page reads row by row.
        void Row(IReadOnlyList<string> cells, PdfFont font)
        {
            List<IReadOnlyList<string>> wrapped = [.. cells.Select((cell, i) => SheetFrame.Wrap(cell, font, widths[i] - (2 * CellPad)))];
            double height = (wrapped.Max(lines => lines.Count) * step) + (2 * CellPad);
            if (y - height < area.Bottom)
            {
                Start();
                Row([.. Columns.Select(column => column.Name)], bold);
            }

            double x = area.Left;
            for (int i = 0; i < cells.Count; i++)
            {
                double lineY = y - CellPad - Text;
                foreach (string line in wrapped[i])
                {
                    page.Add(new PlacedText(line, font, x + CellPad, lineY));
                    lineY -= step;
                }

                x += widths[i];
            }

            ruleRows.Add((y, height));
            y -= height;
        }

        Start();
        if (set.Empty is { } empty)
        {
            Paragraph(empty, body);
        }
        else
        {
            Paragraph(CutListCsv.Statement(set.Rows), body);
            Row([.. Columns.Select(column => column.Name)], bold);
            foreach (CutListRow row in set.Rows)
            {
                Row(Cells(row), body);
            }

            if (CutList.RoughFooter(set.Rows) is { } footer)
            {
                y -= step / 2;
                if (y - Text < area.Bottom)
                {
                    Start();
                }

                Paragraph(footer, body);
            }
        }

        LineStyle rule = DrawingLines.Of(LineKind.Dimension);
        for (int i = 0; i < pages.Count; i++)
        {
            IReadOnlyList<PlacedText> lines = pages[i];
            IReadOnlyList<(double Y, double Height)> rows = rules[i];
            yield return new SetPage(CutListTitle, null, ink =>
            {
                // A rule over the header, and one under every row.
                if (rows.Count > 0)
                {
                    ink.Line(new(area.Left, rows[0].Y), new(area.Right, rows[0].Y), rule, null);
                }

                foreach ((double top, double height) in rows)
                {
                    ink.Line(new(area.Left, top - height), new(area.Right, top - height), rule, null);
                }

                ink.Text(lines);
            });
        }
    }

    /// <summary>One board or sheet of the layout: its line, and its drawing's height (none for a refusal).</summary>
    readonly record struct LayoutBlock(CutLayoutRow Row, IReadOnlyList<string> Caption, double Drawing);

    /// <summary>
    /// The layout's pages: its statement, then each board or sheet — its line as the screen reads it
    /// and its drawing under it — never split across a page, then the summary.
    /// </summary>
    static IEnumerable<SetPage> LayoutPages(ShopSet set, PageRect area, SheetFonts fonts)
    {
        PdfFont body = fonts.Regular(Text);
        double step = Text * SheetFrame.Leading;
        SheetScale scale = LayoutScale(set.Layout, area.Width);
        double perUnit = scale.PointsPerInch / Length.UnitsPerInch;

        List<List<Action<SheetInk>>> pages = [[]];
        double y = area.Top;
        void Fit(double height)
        {
            if (y - height < area.Bottom && pages[^1].Count > 0)
            {
                pages.Add([]);
                y = area.Top;
            }
        }

        void Lines(IReadOnlyList<string> lines)
        {
            foreach (string line in lines)
            {
                double baseline = y - Text;
                pages[^1].Add(ink => ink.Text(line, body, area.Left, baseline));
                y -= step;
            }
        }

        List<string> Wrapped(IEnumerable<string> texts) => [.. texts.SelectMany(text => SheetFrame.Wrap(text, body, area.Width))];

        List<string> statement = Wrapped([CutLayout.Statement(set.Layout.Kerf)]);
        Fit(statement.Count * step);
        Lines(statement);
        foreach (CutLayoutRow row in CutLayout.Rows(set.Layout))
        {
            List<string> caption = Wrapped([CutLayout.Line(CutLayout.Fields(row))]);
            double drawing = row.Planned is not null ? BarHeight : row.Sheet is { } sheet ? sheet.Short.Units * perUnit : 0;
            y -= step / 2;
            Fit((caption.Count * step) + drawing + (step / 2));
            Lines(caption);
            if (row.Planned is { } board)
            {
                PageRect bar = new(area.Left, y - BarHeight, board.StockLength.Units * perUnit, BarHeight);
                pages[^1].Add(ink => DrawBoard(ink, board, bar, perUnit));
                y -= BarHeight + (step / 2);
            }
            else if (row.Sheet is { } drawn)
            {
                PageRect whole = new(area.Left, y - (drawn.Short.Units * perUnit), drawn.Long.Units * perUnit, drawn.Short.Units * perUnit);
                pages[^1].Add(ink => DrawSheet(ink, drawn, whole, perUnit, Grained(set.Rows, drawn)));
                y -= whole.Height + (step / 2);
            }
        }

        List<string> summary = Wrapped(CutLayout.Summary(set.Layout));
        y -= step / 2;
        Fit(summary.Count * step);
        Lines(summary);

        return pages.Select(actions => new SetPage(LayoutTitle, scale, ink =>
        {
            foreach (Action<SheetInk> draw in actions)
            {
                draw(ink);
            }
        }));
    }

    /// <summary>
    /// A board as the screen's bar draws it: each piece in cutting order, labelled where its name
    /// fits, a kerf mark after each, and the offcut hatched.
    /// </summary>
    static void DrawBoard(SheetInk ink, PlannedBoard board, PageRect bar, double perUnit)
    {
        PdfFont small = ink.Fonts.Regular(Text - 1);
        double x = bar.Left;
        double kerf = board.Kerf.Units * perUnit;
        foreach (PlacedPiece piece in board.Pieces)
        {
            PageRect drawn = new(x, bar.Bottom, piece.Length.Units * perUnit, bar.Height);
            ink.Rectangle(drawn, PieceLine);
            Name(ink, small, piece.Label, drawn);
            x = drawn.Right;
            if (kerf > 0 && board.Cuts > 0)
            {
                ink.Line(new(x + (kerf / 2), bar.Bottom), new(x + (kerf / 2), bar.Top), KerfLine, null);
            }

            x += kerf;
        }

        if (bar.Right > x)
        {
            PageRect offcut = new(x, bar.Bottom, bar.Right - x, bar.Height);
            ink.Rectangle(offcut, PieceLine);
            foreach (PageSegment line in Hatch.Lines(offcut, [], HatchStep))
            {
                ink.Line(line.From, line.To, HatchLine, null);
            }
        }
    }

    /// <summary>
    /// A sheet as the screen draws it, its long side across the page: what is not a piece hatched,
    /// each piece outlined where the layout put it (from the sheet's top-left corner) and labelled
    /// where its name fits, and a grain arrow along the sheet on a piece whose grain is set.
    /// </summary>
    static void DrawSheet(SheetInk ink, PlannedSheet sheet, PageRect whole, double perUnit, IReadOnlySet<(string Label, Length Along, Length Across)> grained)
    {
        PdfFont small = ink.Fonts.Regular(Text - 1);
        List<(SheetPiece Piece, PageRect Rect)> pieces =
        [
            .. sheet.Pieces.Select(piece => (piece, new PageRect(
                whole.Left + (piece.X.Units * perUnit),
                whole.Top - ((piece.Y.Units + piece.Across.Units) * perUnit),
                piece.Along.Units * perUnit,
                piece.Across.Units * perUnit))),
        ];

        ink.Rectangle(whole, PieceLine);
        foreach (PageSegment line in Hatch.Lines(whole, [.. pieces.Select(piece => piece.Rect)], HatchStep))
        {
            ink.Line(line.From, line.To, HatchLine, null);
        }

        foreach ((SheetPiece piece, PageRect rect) in pieces)
        {
            ink.Rectangle(rect, PieceLine);
            Name(ink, small, piece.Label, rect);
            if (grained.Contains((piece.Label, piece.Along, piece.Across)))
            {
                GrainArrow(ink, small, rect);
            }
        }
    }

    /// <summary>A piece's name, centred in it when it fits across and up; otherwise left to the line above the drawing.</summary>
    static void Name(SheetInk ink, PdfFont font, string label, PageRect rect)
    {
        if (font.MeasureWidth(label) <= rect.Width - 4 && font.Size + 4 <= rect.Height)
        {
            ink.CentredText(label, font, rect.Centre.X, rect.Centre.Y - (font.Size / 3));
        }
    }

    /// <summary>
    /// A double-headed arrow along the sheet's long side, under the piece's name, saying "grain":
    /// the layout keeps a grained piece's grain along the sheet's long side (<see cref="Napkin.Modules.Furniture.SheetLayout.Statement"/>).
    /// </summary>
    static void GrainArrow(SheetInk ink, PdfFont font, PageRect rect)
    {
        double half = Math.Min(rect.Width * 0.3, 36);
        double y = rect.Centre.Y - (font.Size * 1.2);
        if (half < DimensionMarks.ArrowLength || y - DimensionMarks.ArrowHalfWidth < rect.Bottom + 1)
        {
            y = rect.Centre.Y;
        }

        PagePoint west = new(rect.Centre.X - half, y), east = new(rect.Centre.X + half, y);
        ink.Line(west, east, DrawingLines.Of(LineKind.Dimension), null);
        ink.Arrowhead(west, (-1, 0));
        ink.Arrowhead(east, (1, 0));
        ink.CentredText("grain", font, rect.Centre.X, y - font.Size - 1);
    }

    /// <summary>The sheet pieces whose grain is set: a row with that label and size, either way round, and a grain.</summary>
    static HashSet<(string Label, Length Along, Length Across)> Grained(IEnumerable<CutListRow> rows, PlannedSheet sheet) =>
    [
        .. sheet.Pieces
            .Where(piece => rows.Any(row => row.Grain is not null && row.Label == piece.Label
                && ((row.Length == piece.Along && row.Width == piece.Across) || (row.Length == piece.Across && row.Width == piece.Along))))
            .Select(piece => (piece.Label, piece.Along, piece.Across)),
    ];

    /// <summary>The labels, <see cref="LabelColumns"/> × <see cref="LabelRows"/> a page, each in a thin box to cut along.</summary>
    static IEnumerable<SetPage> LabelPages(ShopSet set, PageRect area, SheetFonts fonts)
    {
        PdfFont name = fonts.Medium(Text + 2), body = fonts.Regular(Text - 1);
        double width = area.Width / LabelColumns, height = area.Height / LabelRows;
        int perPage = LabelColumns * LabelRows;
        LineStyle rule = DrawingLines.Of(LineKind.Dimension);

        for (int first = 0; first < set.Labels.Length; first += perPage)
        {
            ShopLabel[] labels = [.. set.Labels.Skip(first).Take(perPage)];
            yield return new SetPage(LabelsTitle, null, ink =>
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    ShopLabel label = labels[i];
                    PageRect cell = new(area.Left + ((i % LabelColumns) * width), area.Top - (((i / LabelColumns) + 1) * height), width, height);
                    ink.Rectangle(cell, rule);
                    double x = cell.Left + CellPad + 2, y = cell.Top - CellPad - name.Size;
                    ink.Text(label.Part, name, x, y);
                    ink.Text(label.Copy, body, cell.Right - CellPad - 2 - body.MeasureWidth(label.Copy), y);
                    foreach (string line in ((string[])[label.Size, label.Material, label.From])
                                 .Where(text => text.Length > 0)
                                 .SelectMany(text => SheetFrame.Wrap(text, body, cell.Width - (2 * CellPad) - 4)))
                    {
                        y -= body.Size * SheetFrame.Leading;
                        ink.Text(line, body, x, y);
                    }
                }
            });
        }
    }
}
