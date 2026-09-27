using System.Globalization;

using Excise.Core.Graphics;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>
/// What every permit set's drawn sheets share (docs/design/permit-set.md §2): the drawing on the left
/// and the notes on the right, the choice of scale, S1 the site plan, a sheet of one standard view, and
/// a member's size or its blank (§3.1) with its check's lines. The deck set (#226) and the window set
/// (#227) are drawn with it.
/// </summary>
public static class PermitSheets
{
    /// <summary>The share of a drawn sheet's width its notes column takes, on the right.</summary>
    internal const double NotesShare = 0.36;

    /// <summary>Space between the drawing and the notes, and inside the drawing area, in points.</summary>
    internal const double Pad = 12;

    /// <summary>The notes' text size.</summary>
    internal const double Size = 8;

    /// <summary>How long a blank for a member's size is, in points.</summary>
    public const double SizeBlank = 54;

    /// <summary>What S1 says when the design has no property lines.</summary>
    public const string NoBoundary = "No property lines are entered. Type the survey's courses under Project → Site plan… to draw them here with each line's distance from the structure.";

    /// <summary>What S1 says of itself, whatever it shows (§5.4).</summary>
    public const string NotASurvey = "This site plan is not a survey.";

    /// <summary>The largest of a list of scales that fits an extent in a rectangle; the engineer scales, then a halving ratio, when none does.</summary>
    internal static SheetScale Fit(IReadOnlyList<SheetScale> scales, DrawingExtent extent, PageRect area)
    {
        double fits = Math.Min(area.Width / Math.Max(extent.Width, 1e-9), area.Height / Math.Max(extent.Height, 1e-9));
        return SheetScale.Largest(scales, fits) ?? SheetScale.Largest(SheetScale.Engineer, fits) ?? SheetScale.AtMost(fits);
    }

    /// <summary>The drawing's side of a sheet: its left part, less the pad.</summary>
    internal static PageRect DrawingSide(PageRect area) => new(area.Left + Pad, area.Bottom + Pad, (area.Width * (1 - NotesShare)) - (2 * Pad), area.Height - (2 * Pad));

    /// <summary>The notes' side: its right part, less the pad.</summary>
    internal static PageRect NotesSide(PageRect area) => new(area.Left + (area.Width * (1 - NotesShare)) + Pad, area.Bottom + Pad, (area.Width * NotesShare) - (2 * Pad), area.Height - (2 * Pad));

    /// <summary>A mapping from a plan's inches to the page: a centre point drawn at an origin, at a scale, y up.</summary>
    internal readonly record struct PlanFit(DrawingPoint Centre, PagePoint Origin, SheetScale Scale)
    {
        public PagePoint ToPage(double x, double y) => new(
            Origin.X + ((x - Centre.Along) * Scale.PointsPerInch),
            Origin.Y + ((y - Centre.Across) * Scale.PointsPerInch));

        public static PlanFit Of(DrawingExtent extent, PageRect area, SheetScale scale) =>
            new(new DrawingPoint((extent.Left + extent.Right) / 2, (extent.Bottom + extent.Top) / 2), area.Centre, scale);
    }

    internal static double In(Length length) => length.ToInches();

    internal static string Text(Length length, LengthFormat format) => length.Format(format).Text;

    /// <summary>Notes laid down a column: laid out once the column's rectangle is known.</summary>
    internal static void Notes(SheetInk ink, PageRect column, IReadOnlyList<FlowItem> items)
    {
        List<IReadOnlyList<PlacedText>> pages = TextFlow.Lay(items, column, column);
        if (pages.Count > 0)
        {
            ink.Text(pages[0]);
        }
    }

    // ─── S1 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// S1, the site plan (§5): the property lines as the survey's courses lay them, the setbacks offset
    /// inward in the centre line's chain, the structure's footprints, a north arrow, and at the side each
    /// line's distance from the structure against its setback, the closure, the zoning note and that
    /// the plan is not a survey. At an architect or engineer scale that fits.
    /// </summary>
    internal static PermitSheet SitePlan(PermitSet permit, Sketch sketch, PageRect area)
    {
        SitePlanMeasure? measure = Napkin.Modules.Building.SitePlan.Of(sketch);
        List<(Point2 From, Point2 To)> footprints = [];
        foreach (Box box in sketch.Entities.Values.OfType<Box>()
                     .Where(box => box.Phase is Phase.New or Phase.Existing
                         && (Wall.Is(sketch, box) || Deck.Is(sketch, box) || Roof.Is(sketch, box)))
                     .OrderBy(box => box.Id))
        {
            footprints.AddRange(PlanShape.Outline(box).Segments.Select(segment => (segment.From, segment.To)));
        }

        DrawingExtent? extent = null;
        foreach (Point2 point in footprints.SelectMany(line => (Point2[])[line.From, line.To]).Concat(measure?.Corners ?? []))
        {
            extent = DrawingExtent.Grow(extent, new DrawingPoint(point.X.ToInches(), point.Y.ToInches()));
        }

        PageRect drawing = DrawingSide(area);
        SheetScale? scale = extent is { } fitted ? Fit([.. SheetScale.Architect, .. SheetScale.Engineer], fitted, drawing) : null;
        return new PermitSheet("S1", "Site plan", scale, (ink, area) =>
        {
            if (extent is { } e && scale is { } drawnAt)
            {
                PlanFit fit = PlanFit.Of(e, drawing, drawnAt);
                PagePoint At(Point2 point) => fit.ToPage(point.X.ToInches(), point.Y.ToInches());
                foreach ((Point2 from, Point2 to) in footprints)
                {
                    ink.Line(At(from), At(to), DrawingLines.Of(LineKind.Visible), null);
                }

                if (measure is not null)
                {
                    bool clockwise = SignedArea(measure.Corners) < 0;
                    foreach (PropertyLine line in measure.Lines)
                    {
                        ink.Line(At(line.From), At(line.To), DrawingLines.Of(LineKind.Visible), null);
                        if (line.Course.Setback is { } setback)
                        {
                            (PagePoint a, PagePoint b) = Inward(At(line.From), At(line.To), In(setback.Distance) * drawnAt.PointsPerInch, clockwise);
                            ink.Line(a, b, DrawingLines.Of(LineKind.Centre), null);
                        }

                        PagePoint middle = new((At(line.From).X + At(line.To).X) / 2, (At(line.From).Y + At(line.To).Y) / 2);
                        ink.CentredText(line.Index.ToString(CultureInfo.InvariantCulture), ink.Fonts.Medium(Size), middle.X, middle.Y + 3);
                    }
                }

            }

            NorthArrow(ink, new PagePoint(drawing.Right - 16, drawing.Top - 30), sketch.Site.North);
            List<FlowItem> notes = [new FlowItem("Site plan", ink.Fonts.Medium(Size + 2))];
            if (measure is null)
            {
                notes.Add(new FlowItem(NoBoundary, ink.Fonts.Regular(Size), 0, Size));
            }
            else
            {
                notes.Add(new FlowItem("Each property line, numbered as drawn, and the structure's distance from it:", ink.Fonts.Regular(Size), 0, Size));
                notes.AddRange(measure.Lines.Select(line => new FlowItem($"{line.Index.ToString(CultureInfo.InvariantCulture)}. {line.Text}", ink.Fonts.Regular(Size), Size, Size / 2)));
                notes.Add(new FlowItem(measure.ClosureText, ink.Fonts.Regular(Size), 0, Size));
                notes.Add(new FlowItem(Napkin.Modules.Building.SitePlan.ZoningNote, ink.Fonts.Regular(Size), 0, Size / 2));
            }

            if (sketch.Site.Underlay is { } underlay)
            {
                notes.Add(new FlowItem($"Survey underlay: {underlay.Name}, calibrated to {Text(underlay.Distance, permit.Format)} between two points.", ink.Fonts.Regular(Size), 0, Size));
            }

            notes.Add(new FlowItem(NotASurvey, ink.Fonts.Medium(Size), 0, Size));
            Notes(ink, NotesSide(area), notes);
        });
    }

    /// <summary>Twice a polygon's signed area, by the shoelace sum: positive when its corners run anticlockwise.</summary>
    internal static double SignedArea(IReadOnlyList<Point2> corners)
    {
        double sum = 0;
        for (int i = 0; i < corners.Count; i++)
        {
            Point2 a = corners[i], b = corners[(i + 1) % corners.Count];
            sum += (a.X.ToInches() * b.Y.ToInches()) - (b.X.ToInches() * a.Y.ToInches());
        }

        return sum;
    }

    /// <summary>A line offset a distance to the inside of a polygon that runs anticlockwise (or clockwise, when said).</summary>
    internal static (PagePoint From, PagePoint To) Inward(PagePoint from, PagePoint to, double distance, bool clockwise)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y, length = Math.Sqrt((dx * dx) + (dy * dy));
        double side = clockwise ? -1 : 1;
        (double nx, double ny) = (-dy / length * side * distance, dx / length * side * distance);
        return (new PagePoint(from.X + nx, from.Y + ny), new PagePoint(to.X + nx, to.Y + ny));
    }

    /// <summary>A north arrow at a point: an arrow 28 pt long pointing to the site's north, clockwise from the page's up, and N at its tip.</summary>
    internal static void NorthArrow(SheetInk ink, PagePoint centre, Angle north)
    {
        double radians = north.ToDegrees() * Math.PI / 180;
        (double X, double Y) up = (Math.Sin(radians), Math.Cos(radians));
        PagePoint tail = new(centre.X - (up.X * 14), centre.Y - (up.Y * 14)), tip = new(centre.X + (up.X * 14), centre.Y + (up.Y * 14));
        ink.Line(tail, tip, DrawingLines.Of(LineKind.Visible), null);
        ink.Arrowhead(tip, up);
        ink.CentredText("N", ink.Fonts.Medium(Size + 2), tip.X + (up.X * 8), tip.Y + (up.Y * 8) - 3);
    }

    /// <summary>
    /// A sheet that is one standard view (A1, A2, §2): the view as its locked view draws it — built by the
    /// app from the very calls it draws with — at the largest architect scale that fits the drawing side,
    /// printed in the title block, and the sheet's notes at the side.
    /// </summary>
    internal static PermitSheet View(string number, string title, DrawingView? view, PageRect area, Func<SheetInk, IEnumerable<FlowItem>> notes)
    {
        DrawingExtent? extent = view?.Extent();
        PageRect drawing = DrawingSide(area);
        PageRect usable = drawing.Inset(DimensionMarks.LabelSize, SheetPdf.CaptionBand);
        SheetScale? scale = extent is { } fitted ? Fit(SheetScale.Architect, fitted, usable) : null;
        return new PermitSheet(number, view is null ? title : $"{title}: {view.Name}", scale, (ink, area) =>
        {
            if (view is not null && extent is { } e && scale is { } drawnAt)
            {
                ViewPlacement placement = new(view, drawing, new DrawingPoint((e.Left + e.Right) / 2, (e.Bottom + e.Top) / 2), usable.Centre);
                SheetPdf.DrawView(ink, placement, drawnAt);
            }

            Notes(ink, NotesSide(area), [.. notes(ink)]);
        });
    }

    /// <summary>
    /// A member's lines at the side of a sheet: its label and size — or, when its check has no answer, a
    /// blank rule where the size would be (§3.1) — then every line its check printed, as C1 prints it.
    /// </summary>
    internal static IEnumerable<FlowItem> Member(SheetInk ink, IReadOnlyList<PermitItem> items, string what, string label, string size)
    {
        PermitItem[] checks = [.. items.Where(item => item.What == what)];
        PdfFont body = ink.Fonts.Regular(Size), bold = ink.Fonts.Medium(Size);
        bool unsized = checks.Any(item => item.Status == PermitStatus.NotSized);
        yield return unsized
            ? new FlowItem($"{label}:", bold, 0, Size * 0.8, SizeBlank)
            : new FlowItem($"{label}: {size}", bold, 0, Size * 0.8);
        foreach (string line in checks.SelectMany(item => item.Lines))
        {
            yield return new FlowItem(line, body, Size);
        }
    }

}
