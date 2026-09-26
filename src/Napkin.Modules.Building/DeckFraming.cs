using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building;

/// <summary>
/// A deck's frame, derived every time from the box and its inputs (<c>docs/design/deck-and-porch.md</c>
/// §2.3): ledger, joists, rim, blocking, beam, posts and decking, with the numbers the code checks
/// read — the joist span, the beam span between posts and a middle post's tributary area — kept
/// exact.
/// </summary>
/// <param name="Deck">The deck.</param>
/// <param name="Ledger">The ledger's edge.</param>
/// <param name="Width">W: the deck's length along the ledger.</param>
/// <param name="Depth">D: its depth out from the house.</param>
/// <param name="Joists">Each joist's near face, from the start of the ledger.</param>
/// <param name="JoistSpan">The joists' clear span, ledger face to beam: D − 2t − cantilever.</param>
/// <param name="BeamSpan">The clear length between adjacent posts, in 1/1024″, exact.</param>
/// <param name="TributaryArea">A middle post's tributary area, in square 1/1024″, exact: beam span × (joist span ÷ 2 + cantilever).</param>
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
    ExactFraction TributaryArea,
    int DeckingBoards,
    Length LastBoardWidth,
    ImmutableArray<FramingPiece> Pieces)
{
    /// <summary>The beam span in words, with ≈ when it is not on the grid: "5'-6 3/4\"".</summary>
    public string BeamSpanText => DeckFrame.Words(BeamSpan);

    /// <summary>The tributary area in square feet to one decimal, as the panel shows it: "27.1 sq ft".</summary>
    public string TributaryAreaText => DeckFrame.SquareFeet(TributaryArea);
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

        ExactFraction beamSpan = new(w.Units - (inputs.PostCount * (Int128)post.Width.Units), inputs.PostCount - 1);
        ExactFraction depth = new(joistSpan.Units + (2 * (Int128)inputs.Cantilever.Units), 2);
        ExactFraction area = new(beamSpan.Numerator * depth.Numerator, beamSpan.Denominator * depth.Denominator);

        return (new DeckFraming(deck, edge, w, d, [.. joists], joistSpan, beamSpan, area, (int)boards, last, [.. pieces]), null);
    }

    /// <summary>A deck's pieces as cut-list rows, "Deck 1 joist", so the shopping list buys them as a cut list's.</summary>
    public static ImmutableArray<CutListRow> CutRows(DeckFraming framing)
    {
        ArgumentNullException.ThrowIfNull(framing);
        return
        [
            .. framing.Pieces.Select(piece => new CutListRow(
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
