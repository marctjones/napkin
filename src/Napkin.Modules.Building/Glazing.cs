using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>
/// A porch's glazing against its walls and roof (docs/design/deck-and-porch.md §5.5), every area in
/// square 1/1024″: the walls standing on the deck, gross; the triangle above each side wall; the roof's
/// sloped area; and the glass in the walls' openings. Screens and solid doors count nothing.
/// </summary>
/// <param name="Walls">The porch's walls: every wall standing on the deck.</param>
/// <param name="WallsGross">Σ length × height, openings not subtracted (the definition says gross).</param>
/// <param name="RakeFill">Σ over the side walls of ½ × length × (length × rise ÷ run), exact.</param>
/// <param name="Roof">The roof's sloped area, width × rafter length (§5.4), as the roof's framing gives it.</param>
/// <param name="Glass">Σ width × height of the glass-filled openings in those walls.</param>
public sealed record GlazingRatio(ImmutableArray<Wall> Walls, Int128 WallsGross, ExactFraction RakeFill, ExactFraction Roof, Int128 Glass)
{
    /// <summary>The gross area the ratio is taken of: walls, rake fill and roof.</summary>
    public ExactFraction Envelope => Add(Add(ExactFraction.Whole((long)WallsGross), RakeFill), Roof);

    static ExactFraction Add(ExactFraction a, ExactFraction b) => new((a.Numerator * b.Denominator) + (b.Numerator * a.Denominator), a.Denominator * b.Denominator);

    /// <summary>Glass over the envelope, in tenths of a percent, rounded half up.</summary>
    public long TenthsOfPercent => (long)(((Glass * 1000 * Envelope.Denominator) + (Envelope.Numerator / 2)) / Envelope.Numerator);

    /// <summary>Whether the glazing is over the 40 % line (strictly more, as the definition's "in excess of" reads).</summary>
    public bool OverTheLine => Glass * 100 * Envelope.Denominator > Envelope.Numerator * 40;

    /// <summary>The sunroom line as the panel shows it.</summary>
    public string Text
    {
        get
        {
            string ratio = $"Glazing {TenthsOfPercent / 10}.{TenthsOfPercent % 10} % of walls and roof ({DeckFrame.SquareFeet(new ExactFraction(Glass, 1))} of {DeckFrame.SquareFeet(Envelope)})";
            return OverTheLine
                ? $"{ratio}: over the 40 % line, a 'sunroom' by IRC 2021 §R202's definition ({Glazing.Source}); §R301.2.1.1.1 asks you to assign it a category — a three-season porch is I, II or III (nonhabitable, unconditioned)."
                : $"{ratio}: under the 40 % line, so not a 'sunroom' by IRC 2021 §R202's definition ({Glazing.Source}); an ordinary unconditioned roofed addition.";
        }
    }
}

/// <summary>The 40 % line (§5.5): napkin computes the ratio and says which side the drawing is on; it never classifies.</summary>
public static class Glazing
{
    /// <summary>Where the definition was read.</summary>
    public const string Source = "read via UpCodes 2026-09-25, docs/research/porch-rules.md";

    /// <summary>What the line says until there is a roof to count.</summary>
    public const string NoRoof = "Glazing: draw the porch roof first — the 40 % line counts the roof's area with the walls'.";

    /// <summary>
    /// The ratio for a deck's porch under a roof whose run and rise give the side walls' triangles and
    /// whose sloped area the roof's framing works out, or null when no wall stands on the deck.
    /// </summary>
    public static GlazingRatio? Of(Sketch sketch, Deck deck, Length run, Length rise, ExactFraction roofArea)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(deck);
        if (deck.Outline is not { } outline || deck.Ledger(sketch).Edge is not { } ledger)
        {
            return null;
        }

        Length surface = deck.Box.Anchor.Z + deck.Height;
        ImmutableArray<Wall> walls =
        [
            .. Wall.All(sketch).Where(wall =>
                wall.Box.Phase != Phase.Demolish
                && wall.Box.Anchor.Z == surface
                && Deck.Bounds(wall.Box) is { } w
                && outline.West <= w.West && w.East <= outline.East && outline.South <= w.South && w.North <= outline.North),
        ];
        if (walls.IsEmpty)
        {
            return null;
        }

        Int128 gross = walls.Aggregate(Int128.Zero, (sum, wall) => sum + ((Int128)wall.Length.Units * wall.Height.Units));

        // The side walls run out from the house, square to the ledger: each has a triangle above it.
        bool ledgerAlongX = ledger is DeckEdge.North or DeckEdge.South;

        ExactFraction rake = ExactFraction.Whole(0);
        foreach (Wall side in walls.Where(wall => Deck.Bounds(wall.Box) is { } w && (w.East - w.West == wall.Length) != ledgerAlongX))
        {
            Int128 length = side.Length.Units;
            ExactFraction triangle = new(length * length * rise.Units, 2 * (Int128)run.Units);
            rake = new ExactFraction((rake.Numerator * triangle.Denominator) + (triangle.Numerator * rake.Denominator), rake.Denominator * triangle.Denominator);
        }

        Int128 glass = walls
            .SelectMany(wall => Opening.In(sketch, wall))
            .Where(opening => opening.Fill == OpeningFill.Glass)
            .Aggregate(Int128.Zero, (sum, opening) => sum + ((Int128)opening.Width.Units * opening.Height.Units));
        return new GlazingRatio(walls, gross, rake, roofArea, glass);
    }
}
