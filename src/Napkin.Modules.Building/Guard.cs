using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>One run of guard along an open edge (§4.1): where it is, and how it is laid out.</summary>
/// <param name="Edge">The deck edge it runs along.</param>
/// <param name="From">Where it starts along the edge, from the edge's west or south end.</param>
/// <param name="Length">How long it is.</param>
/// <param name="Bays">⌈length ÷ post spacing⌉.</param>
/// <param name="Clear">Each bay's clear width, in 1/1024″, exact: (length − posts × post width) ÷ bays.</param>
/// <param name="Balusters">Balusters in each bay: the fewest giving a gap no wider than the typed one.</param>
/// <param name="Gap">The actual gap, in 1/1024″, exact: (clear − balusters × width) ÷ (balusters + 1).</param>
public sealed record GuardRun(DeckEdge Edge, Length From, Length Length, int Bays, ExactFraction Clear, int Balusters, ExactFraction Gap)
{
    /// <summary>Posts on the run, both ends included.</summary>
    public int Posts => Bays + 1;
}

/// <summary>A deck's guard laid out (§4.1): the runs, the posts once each, and the pieces as boards to buy.</summary>
/// <param name="Runs">The runs, edge by edge, west or south end first.</param>
/// <param name="Posts">Posts, a corner post shared by two runs counted once.</param>
/// <param name="Pieces">The posts, rails, caps and balusters.</param>
public sealed record GuardLayout(ImmutableArray<GuardRun> Runs, int Posts, ImmutableArray<FramingPiece> Pieces);

/// <summary>
/// A deck's guard, napkin's layout (docs/design/deck-and-porch.md §4.1): posts at both ends of every
/// open-edge run and evenly between, rails and a cap, and balusters spaced to the typed gap. Every
/// size is the person's typed lumber; nothing here is a code value, and the panel says so.
/// </summary>
public static class GuardFraming
{
    /// <summary>The guard's layout on a framed deck, or null when the deck has no guard or no open edge.</summary>
    public static GuardLayout? Of(Sketch sketch, DeckFraming framing, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(framing);
        ArgumentNullException.ThrowIfNull(library);
        DeckInputs inputs = framing.Deck.Box.Deck!;
        if (inputs.Guard is not { } guard
            || !library.TryFindLumber(guard.Post, out LumberStock post) || !library.TryFindLumber(guard.Rail, out LumberStock rail)
            || !library.TryFindLumber(guard.Cap, out LumberStock cap) || !library.TryFindLumber(guard.Baluster, out LumberStock baluster)
            || !library.TryFindLumber(inputs.Joist, out LumberStock joist) || !library.TryFindLumber(inputs.Decking, out LumberStock board))
        {
            return null;
        }

        List<GuardRun> runs = [];
        foreach (DeckEdge edge in framing.Deck.OpenEdges(sketch))
        {
            Length along = EdgeLength(framing.Deck, edge);
            foreach ((Length from, Length length) in Spans(along, inputs.Stair is { } stair && stair.Edge == edge ? (stair.At, stair.Width) : null))
            {
                runs.Add(Run(edge, from, length, guard, post.Width, baluster.Width));
            }
        }

        if (runs.Count == 0)
        {
            return null;
        }

        // A corner post is shared by the runs that meet there, counted once.
        int shared = Corners.Count(corner => Touches(runs, corner.First, corner.FirstAtEnd, framing.Deck) && Touches(runs, corner.Second, corner.SecondAtEnd, framing.Deck));
        int posts = runs.Sum(run => run.Posts) - shared;

        List<FramingPiece> pieces = [new(FramingRole.GuardPost, posts, guard.Height + joist.Width + board.Thickness, post)];
        foreach (IGrouping<ExactFraction, GuardRun> clear in runs.GroupBy(run => run.Clear))
        {
            pieces.Add(new FramingPiece(FramingRole.GuardRail, 2 * clear.Sum(run => run.Bays), Floor(clear.Key), rail));
        }

        foreach (IGrouping<Length, GuardRun> length in runs.GroupBy(run => run.Length))
        {
            pieces.Add(new FramingPiece(FramingRole.GuardCap, length.Count(), length.Key, cap));
        }

        pieces.Add(new FramingPiece(
            FramingRole.Baluster,
            runs.Sum(run => run.Bays * run.Balusters),
            guard.Height - cap.Thickness - (rail.Width * 2) - guard.BottomClearance,
            baluster));
        return new GuardLayout([.. runs], posts, [.. pieces]);
    }

    /// <summary>One run: its bays, its clear, and the fewest balusters giving a gap no wider than the typed one.</summary>
    static GuardRun Run(DeckEdge edge, Length from, Length length, GuardInputs guard, Length postWidth, Length balusterWidth)
    {
        int bays = (int)((length.Units + guard.PostSpacing.Units - 1) / guard.PostSpacing.Units);
        ExactFraction clear = new(length.Units - ((bays + 1) * (Int128)postWidth.Units), bays);

        // n = ⌈(clear − b) ÷ (w + b)⌉, the fewest with (clear − n·w) ÷ (n + 1) ≤ b; at least none.
        Int128 over = clear.Numerator - (guard.BalusterGap.Units * clear.Denominator);
        Int128 per = (balusterWidth.Units + guard.BalusterGap.Units) * clear.Denominator;
        int balusters = over <= 0 ? 0 : (int)((over + per - 1) / per);
        ExactFraction gap = new(clear.Numerator - (balusters * balusterWidth.Units * clear.Denominator), clear.Denominator * (balusters + 1));
        return new GuardRun(edge, from, length, bays, clear, balusters, gap);
    }

    /// <summary>The runs an edge gives: all of it, or the two sides of a stair opening on it.</summary>
    static IEnumerable<(Length From, Length Length)> Spans(Length along, (Length At, Length Width)? opening)
    {
        if (opening is not { } stair)
        {
            yield return (Length.Zero, along);
            yield break;
        }

        if (stair.At > Length.Zero)
        {
            yield return (Length.Zero, stair.At);
        }

        Length after = stair.At + stair.Width;
        if (after < along)
        {
            yield return (after, along - after);
        }
    }

    /// <summary>An edge's length: north and south run along the deck's width, east and west along its height.</summary>
    public static Length EdgeLength(Deck deck, DeckEdge edge)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var outline = deck.Outline!.Value;
        return edge is DeckEdge.North or DeckEdge.South ? outline.East - outline.West : outline.North - outline.South;
    }

    /// <summary>The four corners, as the two edges that meet there and whether each meets it at its far (east or north) end.</summary>
    static readonly (DeckEdge First, bool FirstAtEnd, DeckEdge Second, bool SecondAtEnd)[] Corners =
    [
        (DeckEdge.South, false, DeckEdge.West, false),
        (DeckEdge.South, true, DeckEdge.East, false),
        (DeckEdge.North, true, DeckEdge.East, true),
        (DeckEdge.North, false, DeckEdge.West, true),
    ];

    static bool Touches(List<GuardRun> runs, DeckEdge edge, bool atEnd, Deck deck)
        => runs.Any(run => run.Edge == edge && (atEnd ? run.From + run.Length == EdgeLength(deck, edge) : run.From == Length.Zero));

    static Length Floor(ExactFraction units) => new((long)(units.Numerator / units.Denominator));

    /// <summary>A gap or clear width in words, with ≈ when it is not a sixteenth: "3 3/16\"".</summary>
    public static string Words(ExactFraction units) => DeckFrame.Words(units);
}
