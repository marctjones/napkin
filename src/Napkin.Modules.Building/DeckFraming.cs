using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building;

/// <summary>
/// A deck's frame, derived every time from the box and its inputs (<c>docs/design/deck-and-porch.md</c>
/// §2.3): ledger, joists, rim, blocking, beam, posts and decking, with the numbers the code checks
/// read — the joist span, the beam span between post faces and the most loaded post's tributary area —
/// kept exact.
/// </summary>
/// <param name="Deck">The deck.</param>
/// <param name="Ledger">The ledger's edge.</param>
/// <param name="Width">W: the deck's length along the ledger.</param>
/// <param name="Depth">D: its depth out from the house.</param>
/// <param name="Joists">Each joist's near face, from the start of the ledger.</param>
/// <param name="JoistSpan">The joists' clear span, ledger face to beam: D − 2t − cantilever.</param>
/// <param name="BeamSpan">
/// The beam span L_B between adjacent posts, face to face, in 1/1024″, exact: (W − posts × post width) ÷ (posts − 1),
/// the end posts flush with the deck's ends and the rest evenly between. Measured as DCA 6 measures the L_B of its beam
/// tables (Figure 3, p. 7, dimensions it from one post's face to the next; p. 5: the beam "can extend past the post
/// face up to L_B/4"; #41, Marc's decision of 2026-09-27). A beam's overhang past an end post runs from that post's
/// outer face, and napkin's beam has none: it ends flush with its end posts' outer faces, so there is no L_B/4 to check.
/// </param>
/// <param name="EndPost">An end post's tributary area, a corner post's (Eq. B-2), measured as DCA 6 Appendix B measures it (pp. B1–B2).</param>
/// <param name="MiddlePost">
/// With three or more posts, the most loaded middle post's tributary area, a centre post's (Eq. B-1); null with two posts.
/// </param>
/// <param name="PostLength">
/// Each post's length, from grade to the beam's underside: the deck's height less the decking, a joist's depth and the
/// beam's depth — the post height DCA 6 measures "from grade or top of foundation, whichever is highest, to the underside
/// of the beam" (POST REQUIREMENTS, p. 10), napkin's posts standing at grade.
/// </param>
/// <param name="DeckingBoards">How many decking boards cover the depth.</param>
/// <param name="LastBoardWidth">How much of the last board shows: "adjust the gaps or the overhang".</param>
/// <param name="Pieces">Every piece, as boards to buy.</param>
public sealed record DeckFraming(
    Deck Deck,
    DeckEdge Ledger,
    Length Width,
    Length Depth,
    ImmutableArray<Length> Joists,
    Length JoistSpan,
    ExactFraction BeamSpan,
    DeckTributary EndPost,
    DeckTributary? MiddlePost,
    Length PostLength,
    int DeckingBoards,
    Length LastBoardWidth,
    ImmutableArray<FramingPiece> Pieces)
{
    /// <summary>The beam span, face to face of posts, in words, with ≈ when it is not on the grid: "5'-6 3/4\"".</summary>
    public string BeamSpanText => DeckFrame.Words(BeamSpan);

    /// <summary>
    /// The most loaded post's tributary area: a middle post's with three or more posts (a corner post's area is half of it,
    /// p. B1), otherwise an end post's. The footing check sizes every footing for it.
    /// </summary>
    public DeckTributary Tributary => MiddlePost ?? EndPost;

    /// <summary>The most loaded post's tributary area, in square 1/1024″, exact.</summary>
    public ExactFraction TributaryArea => Tributary.Area;

    /// <summary>The tributary area in square feet to one decimal, as the panel shows it: "29.6 sq ft".</summary>
    public string TributaryAreaText => DeckFrame.SquareFeet(TributaryArea);
}

/// <summary>Which of DCA 6 Appendix B's two tributary-area equations a post takes (p. B1).</summary>
public enum TributaryPost
{
    /// <summary>A centre post, Eq. B-1: (½J_L + J_O)(B_L) — a middle post, when the beam has three or more.</summary>
    Centre,

    /// <summary>A corner post, Eq. B-2: (½J_L + J_O)(½B_L + B_O) — an end post, the most loaded when the beam has two.</summary>
    Corner,
}

/// <summary>
/// The most loaded post's tributary area, measured exactly as DCA 6 Appendix B defines it (pp. B1–B2; #41, Marc's
/// decision of 2026-09-27), for the footing check and slice B3's post and footing tables. Every length is in 1/1024″,
/// exact. napkin's beam never overhangs its end posts, so the beam overhang B_O is zero, as p. B2 says: "If a center
/// post or footing is being considered or no overhang exists, zero is entered into the equation B-1 or B-2 for B_O."
/// </summary>
/// <param name="Post">
/// Which post: a middle post, with three or more posts the one beside an end post (its greater adjacent span is the end
/// span, so no middle post carries more); or an end post, a corner post of Figure B1.
/// </param>
/// <param name="BeamSpan">
/// B_L, p. B2: "measured from either centerline of post to centerline of post, if there are overhangs, or to the outside
/// edges of the deck, if there are no overhangs. For posts or footings being considered with two unequal, adjacent beam
/// spans, the greater span shall be used." napkin's beam has no overhang, so an end span runs from the next post's
/// centreline to the deck's outside edge: with three or more posts, (W − post width) ÷ (posts − 1) + ½ post width — the
/// greater of the middle post's two, and the end post's one; with two posts the one span runs outside edge to outside
/// edge, W.
/// </param>
/// <param name="JoistLength">
/// J_L, p. B1: "from the ledger face to either the center point of the beam, if there is an overhang, or to the outside
/// face of the rimboard if there is not an overhang." Without a cantilever D − t; with one, the ledger face to the beam's
/// centre, the beam's outer face being the cantilever in from the rim's outer face (deck-and-porch §2.3).
/// </param>
/// <param name="JoistOverhang">
/// J_O, p. B1: "measure[d] from the outside edge of the deck to the centerline of the beam … If no overhang exists, zero
/// is entered". With a cantilever, the cantilever plus half the beam's thickness (its plies side by side).
/// </param>
public sealed record DeckTributary(TributaryPost Post, ExactFraction BeamSpan, ExactFraction JoistLength, ExactFraction JoistOverhang)
{
    /// <summary>The area, Eq. B-1 or Eq. B-2, in square 1/1024″, exact.</summary>
    public ExactFraction Area => DeckFrame.Times(JoistSide, BeamSide);

    /// <summary>"a middle post" or "an end post".</summary>
    public string Which => Post == TributaryPost.Centre ? "a middle post" : "an end post";

    /// <summary>Which of DCA 6 Appendix B's post tables and equations the post takes: a centre post or a corner post.</summary>
    public PostPosition Position => Post == TributaryPost.Centre ? PostPosition.Center : PostPosition.Corner;

    /// <summary>An end post's B_L runs the deck's whole width, outside edge to outside edge: the beam has two posts.</summary>
    public bool EdgeToEdge { get; init; }

    /// <summary>
    /// The area and how it was measured: "29.6 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0\" of beam, post
    /// centreline to the deck's outside edge, × half the joists' 9'-10 1/2\", ledger face to the rim's outside face)".
    /// </summary>
    public string Words
    {
        get
        {
            string beam = Post == TributaryPost.Centre
                ? $"{DeckFrame.Words(BeamSpan)} of beam, post centreline to the deck's outside edge"
                : EdgeToEdge
                    ? $"half the beam's {DeckFrame.Words(BeamSpan)}, the deck's outside edge to outside edge"
                    : $"half the beam's {DeckFrame.Words(BeamSpan)}, the next post's centreline to the deck's outside edge";
            string joists = JoistOverhang.Numerator == 0
                ? $"half the joists' {DeckFrame.Words(JoistLength)}, ledger face to the rim's outside face"
                : $"(half the joists' {DeckFrame.Words(JoistLength)}, ledger face to the beam's centre, + {DeckFrame.Words(JoistOverhang)}, the beam's centre to the deck's outside edge)";
            string equation = Post == TributaryPost.Centre ? "B-1" : "B-2";
            return $"{DeckFrame.SquareFeet(Area)} (DCA 6 Appendix B Eq. {equation}, pp. B1–B2: {beam}, × {joists})";
        }
    }

    /// <summary>½J_L + J_O: how far out from the house the post's area reaches.</summary>
    ExactFraction JoistSide => DeckFrame.Plus(DeckFrame.Half(JoistLength), JoistOverhang);

    /// <summary>B_L for a centre post; ½B_L + B_O (zero) for a corner post: how far along the beam its area reaches.</summary>
    ExactFraction BeamSide => Post == TributaryPost.Centre ? BeamSpan : DeckFrame.Half(BeamSpan);
}

/// <summary>A deck that cannot be framed, and why (the panel's words).</summary>
/// <param name="Problem">Why.</param>
/// <param name="Detail">The lumber that is not in the library, or empty.</param>
public sealed record DeckRefusal(DeckProblem Problem, string Detail)
{
    /// <summary>The sentence the panel shows.</summary>
    public string Text => Problem switch
    {
        DeckProblem.NotAgainstAWall => "Not against a wall: draw the deck with one edge on the face of an existing wall — that edge is the ledger.",
        DeckProblem.AgainstTwoWalls => "Against two walls: a deck in an inside corner is not modelled.",
        DeckProblem.NotLevel => "The deck is turned or tipped: draw it level and square to the plan.",
        DeckProblem.NoInputs => "No deck inputs yet: choose the joists, beam and posts in the panel.",
        DeckProblem.UnknownLumber => $"{Detail} is not in the materials library, so the frame cannot be sized.",
        _ => "The deck is too low for its frame: the decking, joists and beam are taller than it stands.",
    };
}

/// <summary>Derives a deck's frame (§2.3). Everything exact; nothing stored.</summary>
public static class DeckFrame
{
    /// <summary>The frame, or why there is none.</summary>
    public static (DeckFraming? Framing, DeckRefusal? Refusal) Of(Sketch sketch, Deck deck, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(library);

        (DeckEdge? ledgerEdge, DeckProblem? problem) = deck.Ledger(sketch);
        if (problem is { } p)
        {
            return (null, new DeckRefusal(p, string.Empty));
        }

        if (deck.Box.Deck is not { } inputs)
        {
            return (null, new DeckRefusal(DeckProblem.NoInputs, string.Empty));
        }

        LumberStock? Find(string name) => library.TryFindLumber(name, out LumberStock lumber) ? lumber : null;
        foreach (string name in new[] { inputs.Joist, inputs.Beam.Lumber, inputs.Post, inputs.Decking })
        {
            if (Find(name) is null)
            {
                return (null, new DeckRefusal(DeckProblem.UnknownLumber, name));
            }
        }

        LumberStock joist = Find(inputs.Joist)!, beam = Find(inputs.Beam.Lumber)!, post = Find(inputs.Post)!, board = Find(inputs.Decking)!;
        var outline = deck.Outline!.Value;
        DeckEdge edge = ledgerEdge!.Value;
        bool alongX = edge is DeckEdge.North or DeckEdge.South;
        Length w = alongX ? outline.East - outline.West : outline.North - outline.South;
        Length d = alongX ? outline.North - outline.South : outline.East - outline.West;
        Length t = joist.Thickness;

        // Joists: faces at k·s while k·s + t ≤ W, then an end joist at W − t unless one is already there.
        List<Length> joists = [];
        for (Length at = Length.Zero; at + t <= w; at += inputs.JoistSpacing)
        {
            joists.Add(at);
        }

        if (joists[^1] != w - t)
        {
            joists.Add(w - t);
        }

        Length postLength = deck.Height - board.Thickness - joist.Width - beam.Width;
        if (postLength <= Length.Zero)
        {
            return (null, new DeckRefusal(DeckProblem.TooLow, string.Empty));
        }

        Length joistLength = d - (t * 2);
        Length joistSpan = joistLength - inputs.Cantilever;
        List<FramingPiece> pieces =
        [
            new(FramingRole.Ledger, 1, w, joist),
            new(FramingRole.RimJoist, 1, w, joist),
            new(FramingRole.Joist, joists.Count, joistLength, joist),
        ];

        if (inputs.Blocking)
        {
            // One row at mid-span: a piece per bay, each the bay's clear width.
            foreach (IGrouping<Length, Length> bay in joists.Zip(joists.Skip(1), (near, far) => far - near - t).GroupBy(clear => clear).OrderByDescending(group => group.Count()))
            {
                pieces.Add(new FramingPiece(FramingRole.Blocking, bay.Count(), bay.Key, joist));
            }
        }

        pieces.Add(new FramingPiece(FramingRole.Beam, inputs.Beam.Plies, w, beam));
        pieces.Add(new FramingPiece(FramingRole.Post, inputs.PostCount, postLength, post));

        // Decking: the least n with n·bw + (n − 1)·gap ≥ D, each board W long.
        Length pitch = board.Width + inputs.DeckingGap;
        long boards = (long)(((Int128)d.Units + inputs.DeckingGap.Units + pitch.Units - 1) / pitch.Units);
        Length last = d - (pitch * (boards - 1));
        pieces.Add(new FramingPiece(FramingRole.DeckingBoard, (int)boards, w, board));

        // L_B face to face of posts (DCA 6 Figure 3, p. 7): the n posts take n post widths of W, and the rest is n − 1
        // equal clear spans.
        int n = inputs.PostCount;
        Int128 postWidth = post.Width.Units;
        ExactFraction beamSpan = new(w.Units - (n * postWidth), n - 1);
        Int128 beamThickness = inputs.Beam.Plies * (Int128)beam.Thickness.Units;
        DeckTributary end = Tributary(TributaryPost.Corner, w, d, t, postWidth, n, inputs.Cantilever, beamThickness);
        DeckTributary? middle = n > 2 ? Tributary(TributaryPost.Centre, w, d, t, postWidth, n, inputs.Cantilever, beamThickness) : null;

        return (new DeckFraming(deck, edge, w, d, [.. joists], joistSpan, beamSpan, end, middle, postLength, (int)boards, last, [.. pieces]), null);
    }

    /// <summary>
    /// A post's tributary area as DCA 6 Appendix B measures it (pp. B1–B2; see <see cref="DeckTributary"/>): B_L to post
    /// centrelines and the deck's outside edges, J_L to the beam's centre or the rim's outside face.
    /// </summary>
    static DeckTributary Tributary(TributaryPost post, Length w, Length d, Length t, Int128 postWidth, int posts, Length cantilever, Int128 beamThickness)
    {
        // No beam overhang: with two posts the one span runs outside edge to outside edge; with more, the end span runs from
        // the middle post beside an end post — half a post plus one centre-to-centre spacing in — to the edge. It is that
        // middle post's greater span and the end post's only one.
        ExactFraction beamSpan = posts == 2
            ? ExactFraction.Whole(w.Units)
            : new ExactFraction((2 * (w.Units - postWidth)) + (postWidth * (posts - 1)), 2 * (posts - 1));

        // Without a cantilever the joist length runs to the rim's outside face, D − t; with one, to the beam's centre (its
        // outer face the cantilever in from the rim's outer face), and the joist overhang is the rest, out to the deck's edge.
        DeckTributary area = cantilever == Length.Zero
            ? new DeckTributary(post, beamSpan, ExactFraction.Whole((d - t).Units), ExactFraction.Whole(0))
            : new DeckTributary(
                post,
                beamSpan,
                new ExactFraction((2 * (Int128)(d - cantilever - t).Units) - beamThickness, 2),
                new ExactFraction((2 * (Int128)cantilever.Units) + beamThickness, 2));
        return area with { EdgeToEdge = posts == 2 };
    }

    /// <summary>a + b, exact.</summary>
    internal static ExactFraction Plus(ExactFraction a, ExactFraction b)
        => new((a.Numerator * b.Denominator) + (b.Numerator * a.Denominator), a.Denominator * b.Denominator);

    /// <summary>a × b, exact.</summary>
    internal static ExactFraction Times(ExactFraction a, ExactFraction b) => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    /// <summary>a ÷ 2, exact.</summary>
    internal static ExactFraction Half(ExactFraction a) => new(a.Numerator, a.Denominator * 2);

    /// <summary>A deck's pieces as cut-list rows, "Deck 1 joist", so the shopping list buys them as a cut list's.</summary>
    public static ImmutableArray<CutListRow> CutRows(DeckFraming framing) => CutRows(framing, []);

    /// <summary>A deck's pieces and more of its own — its guard's and stair's — as cut-list rows.</summary>
    public static ImmutableArray<CutListRow> CutRows(DeckFraming framing, IEnumerable<FramingPiece> more)
    {
        ArgumentNullException.ThrowIfNull(framing);
        ArgumentNullException.ThrowIfNull(more);
        return
        [
            .. framing.Pieces.Concat(more).Select(piece => new CutListRow(
                $"{framing.Deck.Name} {piece.Label}",
                piece.Quantity,
                piece.Length,
                piece.Stock!.Width,
                piece.Stock.Thickness,
                piece.Stock.Name,
                Unresolved: false,
                piece.Stock,
                [],
                new PlanAxes(PartDimension.Length, PartDimension.Width),
                [framing.Deck.Id])),
        ];
    }

    /// <summary>A length kept as an exact fraction of 1/1024″, in feet and inches: with ≈ when it is between sixteenths.</summary>
    public static string Words(ExactFraction units)
    {
        Length rounded = new((long)((units.Numerator + (units.Denominator / 2)) / units.Denominator));
        FormattedLength text = rounded.Format(new FeetInchesFormat(16));
        return units.Denominator == 1 && text.IsExact ? text.Text : "≈" + text.Text;
    }

    /// <summary>Square 1/1024″ as square feet to a tenth, rounded half up.</summary>
    public static string SquareFeet(ExactFraction squareUnits)
    {
        Int128 perFoot = (Int128)Length.UnitsPerFoot * Length.UnitsPerFoot;
        Int128 tenths = ((squareUnits.Numerator * 10) + (squareUnits.Denominator * perFoot / 2)) / (squareUnits.Denominator * perFoot);
        return $"{(long)(tenths / 10)}.{(long)(tenths % 10)} sq ft";
    }
}
