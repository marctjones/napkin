using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;

namespace Napkin.Interop.Pdf;

/// <summary>
/// A permit set's paper (docs/design/permit-set.md §2–§4): the title block every sheet shares, the
/// paper the person chose, and every rules-engine result in the design.
/// </summary>
/// <param name="Title">What every sheet's title block says; the banner is added from <paramref name="Items"/>.</param>
/// <param name="Format">How lengths are written: a scale's words.</param>
/// <param name="Paper">Letter or Tabloid, as the person chooses (§4).</param>
/// <param name="Items">Every result (<see cref="PermitItems.Of"/>).</param>
public sealed record PermitSet(TitleBlock Title, LengthFormat Format, SheetPaper Paper, IReadOnlyList<PermitItem> Items);

/// <summary>One drawn sheet of a permit set: its number and title, its scale, and what it draws in the drawing area.</summary>
/// <param name="Number">"S1", "A2".</param>
/// <param name="Title">"Site plan", "Elevation".</param>
/// <param name="Scale">The scale it is drawn at; null for a sheet drawn to none.</param>
/// <param name="Draw">Draws the sheet inside the frame's drawing area, which it is given.</param>
internal sealed record PermitSheet(string Number, string Title, SheetScale? Scale, Action<SheetInk, PageRect> Draw);

/// <summary>
/// The permit set's sheet framework (#225, docs/design/permit-set.md §8 slice C): every sheet in one
/// frame on Letter or Tabloid, its number and title in the title block, the adopted code and the
/// disclaimer on every sheet, and — when anything is not sized — a banner across every title block,
/// the C1 code page listing every result as the panels word it, and the W1 worksheet with a blank for
/// each unsized item's answer and its citation (§3, §3.1). The drawn sheets (S1, A1, A2, S2, S3) are the
/// deck and window sets' (#226, #227).
/// </summary>
public static class PermitSetPdf
{
    /// <summary>The code page's number and title.</summary>
    public const string CodePage = "C1 Code page";

    /// <summary>The worksheet's number and title.</summary>
    public const string Worksheet = "W1 Worksheet: what is not sized";

    /// <summary>What a page that runs on is called after its sheet's name.</summary>
    public const string Continued = ", continued";

    /// <summary>What the code page says of a design that asks the code nothing.</summary>
    public const string NothingAsked = "Nothing in this design asks the adopted code a question: no opening in a wall, no wall line, no deck.";

    /// <summary>How the worksheet begins (§3.1): what it is for, and that a filled-in answer is the person's, not napkin's.</summary>
    public const string WorksheetIntro =
        "napkin could not size these. Look each one up in your own copy of the adopted code, and write the answer and "
        + "where it is printed. An answer written here is your reading, not napkin's: napkin shows it only once it is "
        + "entered in napkin with its citation, marked ENTERED BY HAND.";

    /// <summary>The words a status prints as, after an item's name.</summary>
    public static string StatusWords(PermitStatus status) => status switch
    {
        PermitStatus.Sized => "sized",
        PermitStatus.NotSized => "NOT SIZED",
        _ => "not checked",
    };

    /// <summary>Writes the set to a stream as a PDF.</summary>
    /// <param name="set">The set.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(PermitSet set, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Document(set).Save(stream);
    }

    /// <summary>The set's code page and, when anything is not sized, its worksheet, as a PDF document.</summary>
    /// <param name="set">The set.</param>
    public static PdfDocument Document(PermitSet set) => Document(set, []);

    /// <summary>The set as a PDF: its drawn sheets, then C1, then W1 when anything is not sized.</summary>
    internal static PdfDocument Document(PermitSet set, IReadOnlyList<PermitSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        return Document(set, _ => sheets);
    }

    /// <summary>
    /// The set as a PDF, its drawn sheets made for the drawing area every sheet has — so a sheet can
    /// choose the scale its title block prints before it is drawn.
    /// </summary>
    internal static PdfDocument Document(PermitSet set, Func<PageRect, IReadOnlyList<PermitSheet>> drawn)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(drawn);
        TitleBlock title = set.Title with { Banner = PermitItems.Banner(set.Items) };
        SheetFonts fonts = SheetFonts.Load();
        SheetFrame frame = SheetFrame.For(title, fonts, set.Paper);
        PageRect area = frame.DrawingArea.Inset(DrawingPad);
        IReadOnlyList<PermitSheet> sheets = drawn(frame.DrawingArea);

        List<SetPage> pages = [.. sheets.Select(sheet => new SetPage($"{sheet.Number} {sheet.Title}", sheet.Scale, ink => sheet.Draw(ink, frame.DrawingArea)))];
        pages.AddRange(Flowed(CodePage, CodePageItems(set.Items, fonts), area));
        if (set.Items.Any(item => item.Status == PermitStatus.NotSized))
        {
            pages.AddRange(Flowed(Worksheet, WorksheetItems(set.Items, fonts), area));
        }

        return SheetSet.Document(title, set.Format, frame, fonts, pages);
    }

    /// <summary>Space kept inside the drawing area, in points.</summary>
    const double DrawingPad = 12;

    /// <summary>The size of the code page's and the worksheet's text.</summary>
    const double Size = 9;

    /// <summary>How long a blank for an answer is, in points: room for a nominal size and more.</summary>
    public const double AnswerBlank = 180;

    /// <summary>How long a blank for a citation is, in points.</summary>
    public const double CitationBlank = 260;

    /// <summary>Paragraphs laid down pages titled as one sheet, the second and later titled as its continuation.</summary>
    static IEnumerable<SetPage> Flowed(string title, IReadOnlyList<FlowItem> items, PageRect area) =>
        TextFlow.Lay(items, area, area).Select((lines, page) => new SetPage(page == 0 ? title : title + Continued, null, ink => ink.Text(lines)));

    /// <summary>C1: every item, its status after its name, then the panel's words under it.</summary>
    static List<FlowItem> CodePageItems(IReadOnlyList<PermitItem> items, SheetFonts fonts)
    {
        PdfFont heading = fonts.Medium(Size + 1), name = fonts.Medium(Size), body = fonts.Regular(Size);
        List<FlowItem> flow = [new("Every rules-engine result in this design, as napkin's panels show it, with its code, table and row.", heading)];
        if (items.Count == 0)
        {
            flow.Add(new FlowItem(NothingAsked, body, 0, Size));
        }

        foreach (PermitItem item in items)
        {
            flow.Add(new FlowItem($"{item.What} — {StatusWords(item.Status)}", name, 0, Size * 0.8));
            flow.AddRange(item.Lines.Select(line => new FlowItem(line, body, Size)));
        }

        return flow;
    }

    /// <summary>W1: what it is for, then each unsized item — its note, what to look up, and blanks for the answer and its citation.</summary>
    static List<FlowItem> WorksheetItems(IReadOnlyList<PermitItem> items, SheetFonts fonts)
    {
        PdfFont heading = fonts.Medium(Size + 1), name = fonts.Medium(Size), body = fonts.Regular(Size);
        List<FlowItem> flow = [new(WorksheetIntro, heading)];
        foreach (PermitItem item in items.Where(item => item.Status == PermitStatus.NotSized))
        {
            flow.Add(new FlowItem(item.What, name, 0, Size * 1.2));
            flow.AddRange(item.Lines.Select(line => new FlowItem(line, body, Size)));
            flow.Add(new FlowItem(item.Lookup, body, Size, Size * 0.4));
            flow.Add(new FlowItem("Answer:", body, Size, Size * 0.6, AnswerBlank));
            flow.Add(new FlowItem("Where it is printed (table and row), or entered by hand:", body, Size, Size * 0.6, CitationBlank));
        }

        return flow;
    }
}
