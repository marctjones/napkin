using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>Why a deck cannot be framed yet: said in the panel, never a guess (deck-and-porch §2.1).</summary>
public enum DeckProblem
{
    /// <summary>No edge lies on a long face of an existing wall.</summary>
    NotAgainstAWall,

    /// <summary>Two edges lie on existing walls: an inside corner, not modelled.</summary>
    AgainstTwoWalls,

    /// <summary>The deck is not a level, square-on box in the plan.</summary>
    NotLevel,

    /// <summary>The box is on the Deck layer but has no deck inputs yet.</summary>
    NoInputs,

    /// <summary>A lumber the inputs name is not in the materials library.</summary>
    UnknownLumber,

    /// <summary>The decking, joists and beam are taller than the deck stands: no post fits.</summary>
    TooLow,
}

/// <summary>
/// A deck: a box on the layer Deck, or called "Deck" (<c>docs/design/deck-and-porch.md</c> §2.1). Its
/// plan outline is the deck, its depth the walking surface's height above grade (Z = 0 is grade), and
/// its ledger edge the one edge that lies on a long face of an existing wall, judged from exact corners
/// with no tolerance.
/// </summary>
/// <param name="Box">The box the deck is.</param>
public sealed record Deck(Box Box)
{
    /// <summary>The deck's id.</summary>
    public EntityId Id => Box.Id;

    /// <summary>What the deck is called on screen.</summary>
    public string Name => Box.Name.Length == 0 ? "Deck" : Box.Name;

    /// <summary>The walking surface's height above grade.</summary>
    public Length Height => Box.Depth;

    /// <summary>Whether a box reads as a deck in this sketch.</summary>
    public static bool Is(Sketch sketch, Box box) => Wall.Reads(sketch, box, BuildingLayers.Deck, "Deck") || box.Deck is not null;

    /// <summary>Every deck in a sketch, in id order.</summary>
    public static ImmutableArray<Deck> All(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return [.. Wall.Boxes(sketch).Where(box => Is(sketch, box)).Select(box => new Deck(box))];
    }

    /// <summary>The deck's outline in the plan: west, south, east and north lines.</summary>
    public (Length West, Length South, Length East, Length North)? Outline => Bounds(Box);

    /// <summary>
    /// The ledger edge, or why there is none: the one edge on a long face of an Existing wall, the
    /// edge's whole length on that face.
    /// </summary>
    public (DeckEdge? Edge, DeckProblem? Problem) Ledger(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        if (Outline is not { } deck)
        {
            return (null, DeckProblem.NotLevel);
        }

        HashSet<DeckEdge> against = [];
        foreach (Wall wall in Wall.All(sketch).Where(wall => wall.Box.Phase == Phase.Existing))
        {
            if (Bounds(wall.Box) is not { } w)
            {
                continue;
            }

            bool alongX = w.East - w.West == wall.Length;
            foreach (DeckEdge edge in Enum.GetValues<DeckEdge>())
            {
                bool edgeAlongX = edge is DeckEdge.North or DeckEdge.South;
                if (edgeAlongX != alongX)
                {
                    continue;
                }

                Length line = edge switch
                {
                    DeckEdge.North => deck.North,
                    DeckEdge.South => deck.South,
                    DeckEdge.East => deck.East,
                    _ => deck.West,
                };
                (Length from, Length to) = edgeAlongX ? (deck.West, deck.East) : (deck.South, deck.North);
                (Length faceA, Length faceB) = alongX ? (w.South, w.North) : (w.West, w.East);
                (Length low, Length high) = alongX ? (w.West, w.East) : (w.South, w.North);
                if ((line == faceA || line == faceB) && low <= from && to <= high)
                {
                    against.Add(edge);
                }
            }
        }

        return against.Count switch
        {
            0 => (null, DeckProblem.NotAgainstAWall),
            1 => (against.Single(), null),
            _ => (null, DeckProblem.AgainstTwoWalls),
        };
    }

    /// <summary>
    /// The deck's open edges (§4.4): every edge but the ledger's with no wall (but a demolished one)
    /// standing on it at the deck's surface, a long face on the edge's line and covering it.
    /// </summary>
    public ImmutableArray<DeckEdge> OpenEdges(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        if (Outline is not { } deck || Ledger(sketch).Edge is not { } ledger)
        {
            return [];
        }

        List<DeckEdge> open = [];
        foreach (DeckEdge edge in Enum.GetValues<DeckEdge>().Where(edge => edge != ledger))
        {
            bool edgeAlongX = edge is DeckEdge.North or DeckEdge.South;
            Length line = edge switch
            {
                DeckEdge.North => deck.North,
                DeckEdge.South => deck.South,
                DeckEdge.East => deck.East,
                _ => deck.West,
            };
            (Length from, Length to) = edgeAlongX ? (deck.West, deck.East) : (deck.South, deck.North);
            bool closed = Wall.All(sketch).Any(wall =>
                wall.Box.Phase != Phase.Demolish
                && wall.Box.Anchor.Z == Box.Anchor.Z + Height
                && Bounds(wall.Box) is { } w
                && (w.East - w.West == wall.Length) == edgeAlongX
                && (edgeAlongX ? line == w.South || line == w.North : line == w.West || line == w.East)
                && (edgeAlongX ? w.West <= from && to <= w.East : w.South <= from && to <= w.North));
            if (!closed)
            {
                open.Add(edge);
            }
        }

        return [.. open];
    }

    /// <summary>A box's plan bounds, when it stands level and square-on; otherwise null.</summary>
    public static (Length West, Length South, Length East, Length North)? Bounds(Box box)
    {
        if (!box.Orientation.IsExact || box.FaceUp != BoxFace.Top)
        {
            return null;
        }

        Point2[] corners = [.. new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(box.Corner)];
        return (corners.Min(point => point.X), corners.Min(point => point.Y), corners.Max(point => point.X), corners.Max(point => point.Y));
    }
}
