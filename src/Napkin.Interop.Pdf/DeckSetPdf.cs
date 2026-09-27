using System.Globalization;

using Excise.Core.Document;
using Excise.Core.Graphics;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.PermitSheets;

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
            List<PermitSheet> sheets = [SitePlan(set.Permit, set.Sketch, area), Elevation(set, area)];
            foreach (DeckChecks deck in DeckCheck.Of(set.Sketch, set.Packs, set.Library))
            {
                sheets.Add(Framing(set, deck, area));
                sheets.Add(Details(set, deck));
            }

            return sheets;
        });
    }

    // ─── A2 ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A2, the elevation (§2): the standard view facing the deck's open side; at the side each deck's
    /// height above grade and its guard and stair lines, a guard's height blank when its check has no answer.
    /// </summary>
    static PermitSheet Elevation(DeckSet set, PageRect area) => View("A2", "Elevation", set.Elevation, area, ink => ElevationNotes(ink, set));

    static IEnumerable<FlowItem> ElevationNotes(SheetInk ink, DeckSet set)
    {
        yield return new FlowItem("Heights above grade", ink.Fonts.Medium(Size + 2));
        IReadOnlyList<PermitItem> items = set.Permit.Items;
        foreach (Deck deck in Deck.All(set.Sketch))
        {
            yield return new FlowItem($"{deck.Name}: the decking is {Text(deck.Height, set.Permit.Format)} above grade.", ink.Fonts.Regular(Size), 0, Size / 2);
            DeckInputs? inputs = deck.Box.Deck;
            foreach (FlowItem item in Member(ink, items, $"{deck.Name}: guard", "Guard height", inputs?.Guard is { } guard ? Text(guard.Height, set.Permit.Format) : "none typed")
                         .Concat(Member(ink, items, $"{deck.Name}: stair", "Stair", inputs?.Stair is { } stair ? $"width {Text(stair.Width, set.Permit.Format)}, tread {Text(stair.Run, set.Permit.Format)}" : "none typed")))
            {
                yield return item;
            }
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
        // The lumber the frame was laid out with: its pieces carry it.
        LumberStock Stock(FramingRole role) => frame.Pieces.First(piece => piece.Role == role).Stock!;
        LumberStock joist = Stock(FramingRole.Joist), beam = Stock(FramingRole.Beam), post = Stock(FramingRole.Post);
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
