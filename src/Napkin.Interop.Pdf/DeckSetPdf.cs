using System.Globalization;

using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Pdf;

/// <summary>
/// What a deck's permit set is drawn from (docs/design/permit-set.md §2): the set's title block, paper
/// and results; the design; the packs and library its checks read; and the elevation facing the deck,
/// built by the app from the very edges its locked view draws.
/// </summary>
/// <param name="Permit">The set's title block, paper and every result.</param>
/// <param name="Sketch">The design.</param>
/// <param name="Packs">The code packs napkin found.</param>
/// <param name="Library">The materials library.</param>
/// <param name="Elevation">The standard view facing the deck's open side; null when none is drawn.</param>
public sealed record DeckSet(PermitSet Permit, Sketch Sketch, CodePacks Packs, MaterialsLibrary Library, DrawingView? Elevation);

/// <summary>
/// The deck set (#226, docs/design/permit-set.md §2, §8 slice D): S1 site plan, A2 elevation, S2
/// framing plan and S3 details for each deck, then C1 and — when anything is not sized — W1, on #225's
/// framework. Every size a member would print is either its check's answer or, when the check has
/// none, a blank rule for a hand to fill in, with the note beneath (§3.1).
/// </summary>
public static class DeckSetPdf
{
    /// <summary>The share of a drawn sheet's width its notes column takes, on the right.</summary>
    const double NotesShare = 0.36;

    /// <summary>Space between the drawing and the notes, and inside the drawing area, in points.</summary>
    const double Pad = 12;

    /// <summary>The notes' text size.</summary>
    const double Size = 8;

    /// <summary>How long a blank for a member's size is, in points.</summary>
    public const double SizeBlank = 54;

    /// <summary>What S1 says when the design has no property lines.</summary>
    public const string NoBoundary = "No property lines are entered. Type the survey's courses under Project → Site plan… to draw them here with each line's distance from the structure.";

    /// <summary>What S1 says of itself, whatever it shows (§5.4).</summary>
    public const string NotASurvey = "This site plan is not a survey.";

    /// <summary>Writes the set to a stream as a PDF.</summary>
    /// <param name="set">The set.</param>
    /// <param name="stream">Where to write it; left open.</param>
    public static void Write(DeckSet set, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Document(set).Save(stream);
    }

    /// <summary>The deck set as a PDF document.</summary>
    /// <param name="set">The set.</param>
    public static PdfDocument Document(DeckSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return PermitSetPdf.Document(set.Permit, area =>
        {
            List<PermitSheet> sheets = [SitePlan(set, area), Elevation(set, area)];
            foreach (DeckChecks deck in DeckCheck.Of(set.Sketch, set.Packs, set.Library))
            {
                sheets.Add(Framing(set, deck, area));
                sheets.Add(Details(set, deck));
            }

            return sheets;
        });
    }

    /// <summary>The largest of a list of scales that fits an extent in a rectangle; the engineer scales, then a halving ratio, when none does.</summary>
    internal static SheetScale Fit(IReadOnlyList<SheetScale> scales, DrawingExtent extent, PageRect area)
    {
        double fits = Math.Min(area.Width / Math.Max(extent.Width, 1e-9), area.Height / Math.Max(extent.Height, 1e-9));
        return SheetScale.Largest(scales, fits) ?? SheetScale.Largest(SheetScale.Engineer, fits) ?? SheetScale.AtMost(fits);
    }

    /// <summary>The drawing's side of a sheet: its left part, less the pad.</summary>
    static PageRect DrawingSide(PageRect area) => new(area.Left + Pad, area.Bottom + Pad, (area.Width * (1 - NotesShare)) - (2 * Pad), area.Height - (2 * Pad));

    /// <summary>The notes' side: its right part, less the pad.</summary>
    static PageRect NotesSide(PageRect area) => new(area.Left + (area.Width * (1 - NotesShare)) + Pad, area.Bottom + Pad, (area.Width * NotesShare) - (2 * Pad), area.Height - (2 * Pad));

    /// <summary>A mapping from a plan's inches to the page: a centre point drawn at an origin, at a scale, y up.</summary>
    internal readonly record struct PlanFit(DrawingPoint Centre, PagePoint Origin, SheetScale Scale)
    {
        public PagePoint ToPage(double x, double y) => new(
            Origin.X + ((x - Centre.Along) * Scale.PointsPerInch),
            Origin.Y + ((y - Centre.Across) * Scale.PointsPerInch));

        public static PlanFit Of(DrawingExtent extent, PageRect area, SheetScale scale) =>
            new(new DrawingPoint((extent.Left + extent.Right) / 2, (extent.Bottom + extent.Top) / 2), area.Centre, scale);
    }

    static double In(Length length) => length.ToInches();

    static string Text(Length length, LengthFormat format) => length.Format(format).Text;

    /// <summary>Notes laid down a column: laid out once the column's rectangle is known.</summary>
    static void Notes(SheetInk ink, PageRect column, IReadOnlyList<FlowItem> items)
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
    static PermitSheet SitePlan(DeckSet set, PageRect area)
    {
        SitePlanMeasure? measure = Napkin.Modules.Building.SitePlan.Of(set.Sketch);
        List<(Point2 From, Point2 To)> footprints = [];
        foreach (Box box in set.Sketch.Entities.Values.OfType<Box>()
                     .Where(box => box.Phase is Phase.New or Phase.Existing
                         && (Wall.Is(set.Sketch, box) || Deck.Is(set.Sketch, box) || Roof.Is(set.Sketch, box)))
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

            NorthArrow(ink, new PagePoint(drawing.Right - 16, drawing.Top - 30), set.Sketch.Site.North);
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

            if (set.Sketch.Site.Underlay is { } underlay)
            {
                notes.Add(new FlowItem($"Survey underlay: {underlay.Name}, calibrated to {Text(underlay.Distance, set.Permit.Format)} between two points.", ink.Fonts.Regular(Size), 0, Size));
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
    static void NorthArrow(SheetInk ink, PagePoint centre, Angle north)
    {
        double radians = north.ToDegrees() * Math.PI / 180;
        (double X, double Y) up = (Math.Sin(radians), Math.Cos(radians));
        PagePoint tail = new(centre.X - (up.X * 14), centre.Y - (up.Y * 14)), tip = new(centre.X + (up.X * 14), centre.Y + (up.Y * 14));
        ink.Line(tail, tip, DrawingLines.Of(LineKind.Visible), null);
        ink.Arrowhead(tip, up);
        ink.CentredText("N", ink.Fonts.Medium(Size + 2), tip.X + (up.X * 8), tip.Y + (up.Y * 8) - 3);
    }

    // ─── A2 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A2, the elevation (§2): the standard view facing the deck's open side, as its locked view draws
    /// it, at an architect scale; at the side each deck's height above grade and its guard and stair
    /// lines, a guard's height blank when its check has no answer.
    /// </summary>
    static PermitSheet Elevation(DeckSet set, PageRect area)
    {
        DrawingView? view = set.Elevation;
        DrawingExtent? extent = view?.Extent();
        PageRect drawing = DrawingSide(area);
        PageRect usable = drawing.Inset(DimensionMarks.LabelSize, SheetPdf.CaptionBand);
        SheetScale? scale = extent is { } fitted ? Fit(SheetScale.Architect, fitted, usable) : null;
        return new PermitSheet("A2", view is null ? "Elevation" : $"Elevation: {view.Name}", scale, (ink, area) =>
        {
            if (view is not null && extent is { } e && scale is { } drawnAt)
            {
                ViewPlacement placement = new(view, drawing, new DrawingPoint((e.Left + e.Right) / 2, (e.Bottom + e.Top) / 2), usable.Centre);
                SheetPdf.DrawView(ink, placement, drawnAt);
            }

            List<FlowItem> notes = [new FlowItem("Heights above grade", ink.Fonts.Medium(Size + 2))];
            IReadOnlyList<PermitItem> items = set.Permit.Items;
            foreach (Deck deck in Deck.All(set.Sketch))
            {
                notes.Add(new FlowItem($"{deck.Name}: the decking is {Text(deck.Height, set.Permit.Format)} above grade.", ink.Fonts.Regular(Size), 0, Size / 2));
                DeckInputs? inputs = deck.Box.Deck;
                notes.AddRange(Member(ink, items, $"{deck.Name}: guard", "Guard height", inputs?.Guard is { } guard ? Text(guard.Height, set.Permit.Format) : "none typed"));
                notes.AddRange(Member(ink, items, $"{deck.Name}: stair", "Stair", inputs?.Stair is { } stair ? $"width {Text(stair.Width, set.Permit.Format)}, tread {Text(stair.Run, set.Permit.Format)}" : "none typed"));
            }

            Notes(ink, NotesSide(area), notes);
        });
    }

    /// <summary>
    /// A member's lines at the side of a sheet: its label and size — or, when its check has no answer, a
    /// blank rule where the size would be (§3.1) — then every line its check printed, as C1 prints it.
    /// </summary>
    static IEnumerable<FlowItem> Member(SheetInk ink, IReadOnlyList<PermitItem> items, string what, string label, string size)
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

    // ─── S2 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// S2, a deck's framing plan (§2): the frame as <see cref="DeckFrame"/> lays it, with the house at the
    /// top — the ledger along it, the rim at the far side, each joist, a blocking row at mid-span, and the
    /// beam and posts under the joists, dashed as hidden — its width and depth dimensioned, at an
    /// architect scale; at the side each member's size (or a blank) and its check.
    /// </summary>
    static PermitSheet Framing(DeckSet set, DeckChecks checks, PageRect area)
    {
        string title = $"Framing plan: {checks.Deck.Name}";
        if (checks.Framing is not { } frame)
        {
            return new PermitSheet("S2", title, null, (ink, area) => Notes(ink, NotesSide(area), [.. Refused(ink, checks)]));
        }

        DeckInputs inputs = checks.Deck.Box.Deck!;
        LumberStock joist = set.Library.TryFindLumber(inputs.Joist, out LumberStock j) ? j : throw new InvalidOperationException("A framed deck's joist is in the library.");
        LumberStock beam = set.Library.TryFindLumber(inputs.Beam.Lumber, out LumberStock b) ? b : throw new InvalidOperationException("A framed deck's beam is in the library.");
        LumberStock post = set.Library.TryFindLumber(inputs.Post, out LumberStock p) ? p : throw new InvalidOperationException("A framed deck's post is in the library.");
        double w = In(frame.Width), d = In(frame.Depth), t = In(joist.Thickness);
        double beamOuter = d - In(inputs.Cantilever), beamInner = beamOuter - (inputs.Beam.Plies * In(beam.Thickness)), pw = In(post.Width);
        DrawingExtent extent = new(0, -d, w, 0);
        PageRect drawing = DrawingSide(area).Inset(DimensionMarks.LabelSize * 2);
        SheetScale scale = Fit(SheetScale.Architect, extent, drawing);

        return new PermitSheet("S2", title, scale, (ink, area) =>
        {
            PlanFit fit = PlanFit.Of(extent, drawing, scale);

            // u runs along the ledger, v out from the house: the page's x and down its y.
            PagePoint At(double u, double v) => fit.ToPage(u, -v);
            void Box(double u0, double v0, double u1, double v1, LineKind kind)
            {
                LineStyle style = DrawingLines.Of(kind);
                PagePoint a = At(u0, v0), b = At(u1, v0), c = At(u1, v1), e = At(u0, v1);
                ink.Line(a, b, style, null);
                ink.Line(b, c, style, null);
                ink.Line(c, e, style, null);
                ink.Line(e, a, style, null);
            }

            Box(0, beamInner, w, beamOuter, LineKind.Hidden);
            int n = inputs.PostCount;
            for (int i = 0; i < n; i++)
            {
                double left = i * (w - pw) / (n - 1);
                double middle = (beamInner + beamOuter) / 2;
                Box(left, middle - (pw / 2), left + pw, middle + (pw / 2), LineKind.Hidden);
            }

            Box(0, 0, w, t, LineKind.Visible);
            Box(0, d - t, w, d, LineKind.Visible);
            foreach (Length face in frame.Joists)
            {
                Box(In(face), t, In(face) + t, d - t, LineKind.Visible);
            }

            if (inputs.Blocking)
            {
                double row = t + (In(frame.JoistSpan) / 2);
                foreach ((Length near, Length far) in frame.Joists.Zip(frame.Joists.Skip(1)))
                {
                    ink.Line(At(In(near) + t, row), At(In(far), row), DrawingLines.Of(LineKind.Visible), null);
                }
            }

            // The house side, and the frame's width and depth.
            ink.CentredText("House: the ledger's side", ink.Fonts.Regular(Size), At(w / 2, 0).X, At(w / 2, 0).Y + DimensionMarks.LabelSize + 12);
            SheetPdf.DrawDimension(ink, (At(0, d), At(w, d)), (At(0, d + 1.5 * DimensionMarks.LabelSize / scale.PointsPerInch), At(w, d + 1.5 * DimensionMarks.LabelSize / scale.PointsPerInch)), Text(frame.Width, set.Permit.Format));
            SheetPdf.DrawDimension(ink, (At(w, 0), At(w, d)), (At(w + 1.5 * DimensionMarks.LabelSize / scale.PointsPerInch, 0), At(w + 1.5 * DimensionMarks.LabelSize / scale.PointsPerInch, d)), Text(frame.Depth, set.Permit.Format));

            LengthFormat format = set.Permit.Format;
            string deck = checks.Deck.Name;
            IReadOnlyList<PermitItem> items = set.Permit.Items;
            List<FlowItem> notes = [new FlowItem($"{deck}: the frame", ink.Fonts.Medium(Size + 2))];
            notes.AddRange(Member(ink, items, $"{deck}: ledger", "Ledger", $"{inputs.Joist}, {Text(frame.Width, format)} long"));
            notes.AddRange(Member(ink, items, $"{deck}: joists", "Joists", $"{inputs.Joist} at {Text(inputs.JoistSpacing, format)} o.c., {frame.Joists.Length.ToString(CultureInfo.InvariantCulture)} of them, span {Text(frame.JoistSpan, format)}"));
            if (inputs.Cantilever > Length.Zero)
            {
                notes.AddRange(Member(ink, items, $"{deck}: joist cantilever", "Cantilever", Text(inputs.Cantilever, format)));
            }

            notes.AddRange(Member(ink, items, $"{deck}: beam", "Beam", $"({inputs.Beam.Plies.ToString(CultureInfo.InvariantCulture)}) {inputs.Beam.Lumber}, span {frame.BeamSpanText} between posts"));
            notes.AddRange(Member(ink, items, $"{deck}: end posts", "Posts", $"{n.ToString(CultureInfo.InvariantCulture)} {inputs.Post}, {Text(frame.PostLength, format)} from grade to the beam"));
            if (n > 2)
            {
                notes.AddRange(Member(ink, items, $"{deck}: middle posts", "Middle posts", inputs.Post));
            }

            notes.Add(new FlowItem($"Rim joist: {inputs.Joist}.{(inputs.Blocking ? " Blocking: one row at mid-span." : string.Empty)} Decking: {inputs.Decking}, {frame.DeckingBoards.ToString(CultureInfo.InvariantCulture)} boards.", ink.Fonts.Regular(Size), 0, Size));
            Notes(ink, NotesSide(area), notes);
        });
    }

    /// <summary>A deck napkin could not frame: its name and why, for every sheet that would draw it.</summary>
    static IEnumerable<FlowItem> Refused(SheetInk ink, DeckChecks checks)
    {
        yield return new FlowItem($"{checks.Deck.Name}: not framed", ink.Fonts.Medium(Size + 2));
        yield return new FlowItem(checks.Refusal!.Text, ink.Fonts.Regular(Size), 0, Size);
    }

    // ─── S3 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// S3, a deck's details (§2): fixed blocks — ledger attachment, footings and frost, guard, stair —
    /// each its size from the model (or a blank) and its check's lines, in a box of its own.
    /// </summary>
    static PermitSheet Details(DeckSet set, DeckChecks checks)
    {
        string title = $"Details: {checks.Deck.Name}";
        if (checks.Framing is null)
        {
            return new PermitSheet("S3", title, null, (ink, area) => Notes(ink, NotesSide(area), [.. Refused(ink, checks)]));
        }

        DeckInputs inputs = checks.Deck.Box.Deck!;
        LengthFormat format = set.Permit.Format;
        string deck = checks.Deck.Name;
        (string Title, Func<SheetInk, IEnumerable<FlowItem>> Lines)[] blocks =
        [
            ("Ledger attachment", ink => Member(ink, set.Permit.Items, $"{deck}: ledger", "Ledger", $"{inputs.Joist} to the house")),
            ("Footings", ink => Member(ink, set.Permit.Items, $"{deck}: footing", "Footing", inputs.FootingDepth is { } depth ? $"{Text(depth, format)} below grade" : "depth not entered")
                .Concat(Member(ink, set.Permit.Items, $"{deck}: frost", "Frost line", set.Sketch.Site.FrostDepth is { } frost ? Text(frost, format) : "not entered"))),
            ("Guard", ink => Member(ink, set.Permit.Items, $"{deck}: guard", "Guard height", inputs.Guard is { } guard ? Text(guard.Height, format) : "none typed")),
            ("Stair", ink => Member(ink, set.Permit.Items, $"{deck}: stair", "Stair", inputs.Stair is { } stair ? $"width {Text(stair.Width, format)}, tread {Text(stair.Run, format)}" : "none typed")),
        ];

        return new PermitSheet("S3", title, null, (ink, area) =>
        {
            PageRect inner = area.Inset(Pad);
            double width = inner.Width / 2, height = inner.Height / 2;
            for (int i = 0; i < blocks.Length; i++)
            {
                PageRect box = new(inner.Left + ((i % 2) * width), inner.Top - (((i / 2) + 1) * height), width, height);
                PageRect cell = box.Inset(4);
                ink.Rectangle(cell, DrawingLines.Of(LineKind.Dimension));
                Notes(ink, cell.Inset(6), [new FlowItem(blocks[i].Title, ink.Fonts.Medium(Size + 2)), .. blocks[i].Lines(ink)]);
            }
        });
    }
}
