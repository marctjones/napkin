using System.Globalization;

using Excise.Core.Document;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.PermitSheets;

namespace Napkin.Interop.Pdf;

/// <summary>
/// What a window or door move's permit set is drawn from (docs/design/permit-set.md §2): the set's title
/// block, paper and results; the design; the packs and library its checks read; and its plan and the
/// elevation facing the wall, built by the app from the very edges its locked views draw.
/// </summary>
/// <param name="Permit">The set's title block, paper and every result.</param>
/// <param name="Sketch">The design.</param>
/// <param name="Packs">The code packs napkin found.</param>
/// <param name="Library">The materials library.</param>
/// <param name="Plan">The Top view, the plan; null when none is drawn.</param>
/// <param name="Elevation">The standard view facing the wall; null when none is drawn.</param>
public sealed record WindowSet(PermitSet Permit, Sketch Sketch, CodePacks Packs, MaterialsLibrary Library, DrawingView? Plan, DrawingView? Elevation);

/// <summary>
/// The window set (#227, docs/design/permit-set.md §2, §8 slice E; napkin's own proposal, §9.2): S1 site
/// plan, A1 plan, A2 elevation, S3 the header details of every opening, then C1 and — when anything is
/// not sized — W1, on #225's framework. A header napkin could not size prints a blank where its size
/// would be, the note beneath (§3.1).
/// </summary>
public static class WindowSetPdf
{
    /// <summary>How many openings' detail blocks an S3 sheet holds, two across and two down.</summary>
    public const int BlocksPerSheet = 4;

    /// <summary>Writes the set to a stream as a PDF.</summary>
    /// <param name="set">The set.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(WindowSet set, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Document(set).Save(stream);
    }

    /// <summary>The window set as a PDF document.</summary>
    /// <param name="set">The set.</param>
    public static PdfDocument Document(WindowSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        IReadOnlyList<OpeningCheck> openings = CodeCheck.Of(set.Sketch, set.Packs);
        return PermitSetPdf.Document(set.Permit, area =>
        {
            List<PermitSheet> sheets =
            [
                SitePlan(set.Permit, set.Sketch, area),
                View("A1", "Plan", set.Plan, area, ink => PlanNotes(ink, set, openings)),
                View("A2", "Elevation", set.Elevation, area, ink => ElevationNotes(ink, set, openings)),
            ];
            for (int first = 0; first < openings.Count; first += BlocksPerSheet)
            {
                sheets.Add(Details(set, [.. openings.Skip(first).Take(BlocksPerSheet)], first == 0 ? "Details" : "Details, continued"));
            }

            return sheets;
        });
    }

    /// <summary>A1's notes: each opening's size and where it is in its wall, and the wall's length (§2).</summary>
    static IEnumerable<FlowItem> PlanNotes(SheetInk ink, WindowSet set, IReadOnlyList<OpeningCheck> openings)
    {
        LengthFormat format = set.Permit.Format;
        yield return new FlowItem("Openings in plan", ink.Fonts.Medium(Size + 2));
        foreach (OpeningCheck check in openings)
        {
            Opening opening = check.Opening;
            string kind = opening.Kind == OpeningKind.Door ? "door" : "window";
            yield return new FlowItem(
                $"{opening.Name} ({kind}): {Text(opening.Width, format)} wide, its near side {Text(opening.Offset, format)} from the start of {opening.Wall.Name}, which is {Text(opening.Wall.Length, format)} long.",
                ink.Fonts.Regular(Size),
                0,
                Size / 2);
        }
    }

    /// <summary>A2's notes: each opening's sill and head above its wall's bottom.</summary>
    static IEnumerable<FlowItem> ElevationNotes(SheetInk ink, WindowSet set, IReadOnlyList<OpeningCheck> openings)
    {
        LengthFormat format = set.Permit.Format;
        yield return new FlowItem("Heights above the wall's bottom", ink.Fonts.Medium(Size + 2));
        foreach (OpeningCheck check in openings)
        {
            Opening opening = check.Opening;
            yield return new FlowItem(
                $"{opening.Name}: rough opening {Text(opening.Width, format)} × {Text(opening.Height, format)}, sill {Text(opening.Sill, format)}, head {Text(opening.Top, format)}; {opening.Wall.Name} {Text(opening.Wall.Height, format)} tall.",
                ink.Fonts.Regular(Size),
                0,
                Size / 2);
        }
    }

    /// <summary>
    /// S3, the header details: a block for each opening — its header's size and its jacks and kings, or a
    /// blank when the check has no answer, then the check's lines as C1 prints them.
    /// </summary>
    static PermitSheet Details(WindowSet set, IReadOnlyList<OpeningCheck> openings, string title) =>
        new("S3", title, null, (ink, area) =>
        {
            PageRect inner = area.Inset(Pad);
            double width = inner.Width / 2, height = inner.Height / 2;
            for (int i = 0; i < openings.Count; i++)
            {
                OpeningCheck check = openings[i];
                PageRect box = new(inner.Left + ((i % 2) * width), inner.Top - (((i / 2) + 1) * height), width, height);
                PageRect cell = box.Inset(4);
                ink.Rectangle(cell, DrawingLines.Of(LineKind.Dimension));
                Notes(ink, cell.Inset(6), [new FlowItem($"{check.Opening.Name}: header", ink.Fonts.Medium(Size + 2)), .. Member(ink, set.Permit.Items, $"{check.Opening.Name}: header", "Header", HeaderSize(check))]);
            }
        });

    /// <summary>
    /// A header as the block prints it: its size and its jacks and kings each side when sized, the
    /// person's own choice when the wall is said not to bear (never a code result), nothing else.
    /// </summary>
    internal static string HeaderSize(OpeningCheck check) => check.Result switch
    {
        HeaderResult.Sized sized => $"{sized.Header}, {sized.JackStuds.ToString(CultureInfo.InvariantCulture)} jack and {sized.KingStuds.ToString(CultureInfo.InvariantCulture)} king studs each side",
        _ => CodeCheck.Short(check),
    };
}
