using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;

namespace Napkin.Interop.Pdf;

/// <summary>One page of a set: what it is called, the scale it is drawn at (none for a table), and what is drawn on it.</summary>
/// <param name="Title">What the title block calls the page.</param>
/// <param name="Scale">The scale its drawing is at; null for a page drawn to none.</param>
/// <param name="Draw">Draws the page's content inside the frame's drawing area.</param>
internal sealed record SetPage(string Title, SheetScale? Scale, Action<SheetInk> Draw);

/// <summary>
/// A set of pages as one PDF, every page in the same frame and carrying the same title block — the
/// project, the date, the adopted code and the disclaimer — with its own title, scale and number
/// (DESIGN.md §7: the disclaimer travels with every sheet).
/// </summary>
internal static class SheetSet
{
    /// <summary>The pages as one document.</summary>
    /// <param name="title">What every page's title block says; also the document's title.</param>
    /// <param name="format">How lengths are written: the scale in words.</param>
    /// <param name="frame">The frame every page is in.</param>
    /// <param name="fonts">The document's fonts, the ones its pages were laid out with.</param>
    /// <param name="pages">The pages, in order; at least one.</param>
    public static PdfDocument Document(TitleBlock title, LengthFormat format, SheetFrame frame, SheetFonts fonts, IReadOnlyList<SetPage> pages)
    {
        PdfDocument document = PdfDocument.CreateNew();
        document.SetTitle(title.ProjectName);
        document.SetCreator("napkin");
        for (int number = 1; number <= pages.Count; number++)
        {
            SetPage set = pages[number - 1];
            PdfPage page = document.Pages.AddBlank(frame.Page.Width, frame.Page.Height);
            using PdfGraphics graphics = page.GetGraphics();
            SheetInk ink = new(graphics, fonts);
            set.Draw(ink);
            frame.DrawTitleBlock(ink, title, format, set.Title, set.Scale, number, pages.Count);
        }

        return document;
    }
}
