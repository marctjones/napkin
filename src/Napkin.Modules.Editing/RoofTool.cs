using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;

namespace Napkin.Modules.Editing;

/// <summary>
/// Draw → Porch roof (<c>docs/design/deck-and-porch.md</c> §5.3, §8): a click on a deck makes a shed
/// roof over its outline. The high edge is the deck's ledger; the low end is the wall standing on the
/// deck along the far edge, or, with no wall there, a beam on posts at a wall's starting height. It
/// starts at 4 in 12 with napkin's starting inputs, each a value to type over, never a standard.
/// </summary>
public static class RoofTool
{
    /// <summary>The pitch a new roof starts at, in 12: napkin's starting value, to type over.</summary>
    public const int StartingPitch = 4;

    /// <summary>The beam an open porch roof starts on: a (2) 2x10 on 2 4x4 posts, to type over.</summary>
    public static readonly BeamLowEnd StartingBeam = new(new BeamSpec(2, "2x10"), "4x4", 2);

    /// <summary>
    /// The inputs a new roof starts with: napkin's design defaults where the note names one (16″
    /// spacing, a 12″ overhang, blocking) and a starting frame to type over (2x8 rafters and ledger).
    /// The sheathing and the roofing's coverage start empty: never defaulted.
    /// </summary>
    public static RoofInputs StartingInputs(RoofLowEnd low) =>
        new(Length.Inches(16), "2x8", "2x8", Length.Inches(12), true, null, new Roofing("roofing", null, 0), low);

    /// <summary>What the message bar says the starting values are.</summary>
    public const string StartingWords =
        "It starts at 4 in 12 with 2x8 rafters at 16\" and a 1'-0\" overhang: starting values to type over, not a standard.";

    /// <summary>The deck whose outline holds a point in plan, or null.</summary>
    public static Deck? DeckAt(Sketch sketch, Point2 at)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return Deck.All(sketch).FirstOrDefault(deck => deck.Outline is { } o && o.West <= at.X && at.X <= o.East && o.South <= at.Y && at.Y <= o.North);
    }

    /// <summary>
    /// The request that adds a roof over <paramref name="deck"/> — a box on the Roof layer with
    /// <see cref="StartingInputs"/> — and its layer, if new; or why there is none.
    /// </summary>
    public static (Request? Request, string? Problem) Request(Sketch sketch, Deck deck, LayerId layer, Request? addLayer, EntityId id, string name)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(deck);
        if (deck.Outline is not { } outline || deck.Ledger(sketch).Edge is not { } ledger)
        {
            return (null, $"{deck.Name} has no ledger on the house, so napkin cannot tell the roof's high edge: draw the deck out from an existing wall's face.");
        }

        if (Roof.All(sketch).Any(roof => roof.Over(sketch) == deck))
        {
            return (null, $"{deck.Name} already has a roof: select it to change it.");
        }

        bool alongX = ledger is DeckEdge.North or DeckEdge.South;
        Length width = alongX ? outline.East - outline.West : outline.North - outline.South;
        Length run = alongX ? outline.North - outline.South : outline.East - outline.West;
        Length rise = new(((run.Units * StartingPitch) + 6) / 12);

        Length surface = deck.Box.Anchor.Z + deck.Height;
        RoofLowEnd low;
        Length top;
        if (FrontWall(sketch, deck, Far(ledger)) is { } front)
        {
            low = new WallLowEnd(front.Id);
            top = front.Box.Anchor.Z + front.Height;
        }
        else
        {
            low = StartingBeam;
            top = surface + WallTool.StartingHeight;
        }

        // Width along the house, the run across it: a house running north–south turns the box a right angle.
        Box probe = new(id, layer, Point3.Origin, width, run, rise, BoxFace.Top, alongX ? Angle.Zero : Angle.Right);
        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(probe.Corner)];
        Box box = probe with
        {
            Anchor = new Point3(outline.West - corners.Min(c => c.X), outline.South - corners.Min(c => c.Y), top),
            Name = name,
            Roof = StartingInputs(low),
        };
        AddEntity add = new(box);
        return (addLayer is null ? add : Batch.Of(addLayer, add), null);
    }

    /// <summary>The wall standing on the deck with a long face on its far edge, or null.</summary>
    public static Wall? FrontWall(Sketch sketch, Deck deck, DeckEdge far)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(deck);
        var outline = deck.Outline!.Value;
        Length surface = deck.Box.Anchor.Z + deck.Height;
        return Wall.All(sketch).FirstOrDefault(wall =>
            wall.Box.Phase != Phase.Demolish
            && wall.Box.Anchor.Z == surface
            && Deck.Bounds(wall.Box) is { } w
            && outline.West <= w.West && w.East <= outline.East && outline.South <= w.South && w.North <= outline.North
            && far switch
            {
                DeckEdge.South => w.South == outline.South && w.East - w.West == wall.Length,
                DeckEdge.North => w.North == outline.North && w.East - w.West == wall.Length,
                DeckEdge.West => w.West == outline.West && w.North - w.South == wall.Length,
                _ => w.East == outline.East && w.North - w.South == wall.Length,
            });
    }

    /// <summary>The edge across the deck from the ledger.</summary>
    public static DeckEdge Far(DeckEdge ledger) => ledger switch
    {
        DeckEdge.North => DeckEdge.South,
        DeckEdge.South => DeckEdge.North,
        DeckEdge.East => DeckEdge.West,
        _ => DeckEdge.East,
    };

    /// <summary>
    /// A typed pitch — "5 in 12", "5", or "4.5 in 12" — as the rise over <paramref name="run"/>, rounded
    /// to the grid; null when it does not read or is not between 0 and 24 in 12.
    /// </summary>
    public static Length? RiseFor(string? typed, Length run)
    {
        string text = (typed ?? string.Empty).Trim();
        if (text.EndsWith("in 12", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^5].Trim();
        }

        if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal pitch) || pitch <= 0 || pitch > 24)
        {
            return null;
        }

        // The pitch as a whole ratio p/q, then rise = run × p ÷ (12 q), half up to the 1/1024″ grid.
        long q = 1;
        while (pitch != decimal.Truncate(pitch))
        {
            pitch *= 10;
            q *= 10;
        }

        Int128 numerator = run.Units * (Int128)(long)pitch, denominator = 12 * (Int128)q;
        return new Length((long)(((2 * numerator) + denominator) / (2 * denominator)));
    }
}
