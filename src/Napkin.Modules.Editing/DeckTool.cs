using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;

namespace Napkin.Modules.Editing;

/// <summary>
/// Draw → Deck (<c>docs/design/deck-and-porch.md</c> §8): a deck is a rectangle dragged out from the
/// face of an existing wall — the edge that lands on the face is the ledger. It starts 3'-0" above
/// grade with napkin's starting inputs, each a value to type over, never a standard.
/// </summary>
public static class DeckTool
{
    /// <summary>The height a new deck starts at, above grade: a starting value to type over, not a standard.</summary>
    public static readonly Length StartingHeight = Length.Inches(36);

    /// <summary>
    /// The inputs a new deck starts with: napkin's design defaults where the note names one (joists out
    /// at 16″, a 1/8″ decking gap, blocking, no cantilever) and a starting frame to type over (2x8
    /// joists, a (2) 2x10 beam on 3 4x4 posts, 5/4x6 decking). What the deck supports, the species and
    /// the footing depth start empty: never defaulted.
    /// </summary>
    public static readonly DeckInputs StartingInputs = new(
        JoistDirection.Out, Length.Inches(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), true,
        null, null, null, null, null);

    /// <summary>
    /// The guard a tick starts with (§4.1, §9.3): 3'-0" high, posts at most 6'-0" apart, 3 1/2" gaps and
    /// clearance, 4x4 posts, 2x4 rails, a 2x6 cap and 2x2 balusters — napkin's starting layout, to type
    /// over, not a code value.
    /// </summary>
    public static readonly GuardInputs StartingGuard = new(
        Length.Inches(36), Length.Inches(72), Length.Inches(3, 1, 2), Length.Inches(3, 1, 2), "4x4", "2x4", "2x6", "2x2");

    /// <summary>The stair a tick starts with (§4.2): 3'-0" wide at the start of an edge, 10" treads, risers from the pack, 3 2x12 stringers, 2 boards a tread.</summary>
    public static StairInputs StartingStair(DeckEdge edge) => new(edge, Length.Zero, Length.Inches(36), Length.Inches(10), null, 3, "2x12", 2);

    /// <summary>What the message bar says the starting values are.</summary>
    public const string StartingWords =
        "It starts 3'-0\" above grade with 2x8 joists at 16\", a (2) 2x10 beam on 3 4x4 posts and 5/4x6 decking: starting values to type over, not a standard.";

    /// <summary>
    /// A dragged rectangle with each edge moved onto a parallel long face of an existing wall within
    /// <paramref name="reach"/> that runs along it — the snap that puts the ledger edge on the house.
    /// </summary>
    public static (Point2 Anchor, Length Length, Length Width) SnapToHouse(Sketch sketch, Point2 anchor, Length length, Length width, Length reach)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ImmutableArray<WallFace> faces = [.. Wall.All(sketch).Where(wall => wall.Box.Phase == Phase.Existing).SelectMany(wall => RoomBounds.FacesOf(wall))];
        Length x0 = anchor.X, x1 = anchor.X + length, y0 = anchor.Y, y1 = anchor.Y + width;

        Length Snap(Length edge, bool vertical, Length lo, Length hi)
            => faces
                .Where(f => f.Vertical == vertical && Length.Max(f.Lo, lo) < Length.Min(f.Hi, hi) && Length.Abs(f.At - edge) <= reach)
                .OrderBy(f => Length.Abs(f.At - edge))
                .Select(f => (Length?)f.At)
                .FirstOrDefault() ?? edge;

        Length west = Snap(x0, true, y0, y1), east = Snap(x1, true, y0, y1);
        Length south = Snap(y0, false, x0, x1), north = Snap(y1, false, x0, x1);
        return east > west && north > south ? (new Point2(west, south), east - west, north - south) : (anchor, length, width);
    }

    /// <summary>The request that adds a deck — a box on the Deck layer, <see cref="StartingHeight"/> high, with <see cref="StartingInputs"/> — and its layer, if new.</summary>
    public static Request Request(LayerId layer, Request? addLayer, EntityId id, string name, Point2 anchor, Length length, Length width)
    {
        AddEntity add = new(Box.AsDrawn(id, layer, anchor, length, width, StartingHeight, Angle.Zero) with { Name = name, Deck = StartingInputs });
        return addLayer is null ? add : Batch.Of(addLayer, add);
    }

    /// <summary>The frame in one line, as the panel shows it: "ledger, 10 joists 2x8 at 16", rim, (2) 2x10 beam on 3 posts, 22 boards".</summary>
    public static string FrameLine(DeckFraming framing)
    {
        ArgumentNullException.ThrowIfNull(framing);
        DeckInputs inputs = framing.Deck.Box.Deck!;
        string spacing = inputs.JoistSpacing.Format(new InchesOnlyFormat(16)).Text;
        return $"ledger, {framing.Joists.Length} joists {inputs.Joist} at {spacing}, rim, ({inputs.Beam.Plies}) {inputs.Beam.Lumber} beam on {inputs.PostCount} posts "
               + $"spanning {framing.BeamSpanText}, {framing.DeckingBoards} boards (the last {framing.LastBoardWidth.Format(new FeetInchesFormat(16)).Text} wide)";
    }
}
