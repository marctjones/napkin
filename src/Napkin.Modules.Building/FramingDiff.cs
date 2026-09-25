using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building;

/// <summary>
/// One wall's framing diff (docs/design/renovation-sketches.md §6.3): the pieces the wall as it
/// will be has beyond the wall as it is — new material, bought — and the pieces the wall as it is
/// has beyond the wall as it will be — they come out.
/// </summary>
/// <param name="Wall">The wall (as it will be, or as it is when it is demolished).</param>
/// <param name="New">The new material, compared by role, length and stock.</param>
/// <param name="Out">What comes out.</param>
/// <param name="FromExisting">Whether the wall is already there, so what comes out is a guess about a real wall (§4.4).</param>
/// <param name="Spacing">The stud spacing the wall's regular layout assumes.</param>
public sealed record WallDiff(Wall Wall, ImmutableArray<FramingPiece> New, ImmutableArray<FramingPiece> Out, bool FromExisting, Length Spacing)
{
    /// <summary>"assuming a regular 16" layout in the existing wall" — said with every count out of an existing wall (§4.4).</summary>
    public string Assumption => $"assuming a regular {Spacing.Format(new InchesOnlyFormat(16)).Text} layout in the existing wall";

    /// <summary>Whether anything is bought or comes out.</summary>
    public bool Changes => !New.IsEmpty || !Out.IsEmpty;

    /// <summary>
    /// The diff in one line for the message bar and the panel: "new — 2 king studs, 2 jack studs,
    /// header, sill, 4 cripples; out — 2 studs".
    /// </summary>
    public string Sentence
    {
        get
        {
            string news = New.IsEmpty ? "nothing" : string.Join(", ", Short(New));
            string outs = Out.IsEmpty ? "nothing" : string.Join(", ", Short(Out));
            return $"new — {news}; out — {outs}";
        }
    }

    static IEnumerable<string> Short(ImmutableArray<FramingPiece> pieces)
    {
        foreach (IGrouping<string, FramingPiece> group in pieces.GroupBy(piece => Word(piece.Role)))
        {
            // A header is one member however many plies it has: counted by kind, not by piece.
            int count = group.Key == "header" ? group.Count() : group.Sum(piece => piece.Quantity);
            yield return count == 1 && group.Key is "header" or "sill" or "plate"
                ? group.Key
                : $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? group.Key : group.Key + "s")}";
        }
    }

    /// <summary>A piece's short word: cripples above and below are both "cripple", a rough sill is a "sill", the plates "plate".</summary>
    static string Word(FramingRole role) => role switch
    {
        FramingRole.BottomPlate or FramingRole.TopPlate => "plate",
        FramingRole.Stud => "stud",
        FramingRole.KingStud => "king stud",
        FramingRole.JackStud => "jack stud",
        FramingRole.Header => "header",
        FramingRole.RoughSill => "sill",
        FramingRole.CrippleAbove or FramingRole.CrippleBelow => "cripple",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Not a framing role."),
    };
}

/// <summary>
/// The framing diff of every wall (renovation-sketches §6.3), derived from the drawing, never
/// stored: <see cref="FramingList.Frame"/> on the building as it will be and as it is, compared by
/// role, length and stock. A New wall has no before: all new. A Demolish wall has no after: all
/// out. An existing wall with no change: nothing either way. Nothing is special-cased.
/// </summary>
public static class FramingDiff
{
    /// <summary>Every wall's diff, walls in id order, whether they are in the after view, the before view, or both.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="library">The materials library the frame is cut from.</param>
    /// <param name="packs">The code packs, for the headers on each side.</param>
    public static ImmutableArray<WallDiff> Of(Sketch sketch, MaterialsLibrary library, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(packs);
        Sketch after = sketch.After(), before = sketch.Before();
        FramingOptions afterOptions = CodeCheck.Framing(CodeCheck.OfView(after, packs), library);
        FramingOptions beforeOptions = CodeCheck.Framing(CodeCheck.OfView(before, packs), library);
        Dictionary<EntityId, Wall> afterWalls = Wall.All(after).ToDictionary(wall => wall.Id);
        Dictionary<EntityId, Wall> beforeWalls = Wall.All(before).ToDictionary(wall => wall.Id);

        List<WallDiff> diffs = [];
        foreach (EntityId id in afterWalls.Keys.Union(beforeWalls.Keys).Order())
        {
            WallFraming? will = afterWalls.TryGetValue(id, out Wall? a) ? FramingList.Frame(after, a, library, afterOptions) : null;
            WallFraming? was = beforeWalls.TryGetValue(id, out Wall? b) ? FramingList.Frame(before, b, library, beforeOptions) : null;
            diffs.Add(Between(will, was));
        }

        return [.. diffs];
    }

    /// <summary>The diff of one wall's frame as it will be against its frame as it is; either may be absent, not both.</summary>
    public static WallDiff Between(WallFraming? after, WallFraming? before)
    {
        WallFraming either = after ?? before ?? throw new ArgumentException("A diff needs the wall as it is or as it will be.", nameof(after));
        Dictionary<(FramingRole, Length, LumberStock?), int> counts = [];
        foreach (FramingPiece piece in after?.Pieces ?? [])
        {
            counts[(piece.Role, piece.Length, piece.Stock)] = counts.GetValueOrDefault((piece.Role, piece.Length, piece.Stock)) + piece.Quantity;
        }

        foreach (FramingPiece piece in before?.Pieces ?? [])
        {
            counts[(piece.Role, piece.Length, piece.Stock)] = counts.GetValueOrDefault((piece.Role, piece.Length, piece.Stock)) - piece.Quantity;
        }

        IEnumerable<FramingPiece> Pieces(Func<int, int> quantity) => counts
            .Select(entry => new FramingPiece(entry.Key.Item1, quantity(entry.Value), entry.Key.Item2, entry.Key.Item3))
            .Where(piece => piece.Quantity > 0)
            .OrderBy(piece => piece.Role)
            .ThenByDescending(piece => piece.Length);

        // A wall in the before view is already there (existing, or to be demolished): what comes out of
        // it is a guess about a real wall (§4.4). A new wall has no before.
        bool existing = before is not null;
        return new WallDiff(either.Wall, [.. Pieces(q => q)], [.. Pieces(q => -q)], existing, either.Spacing);
    }

    /// <summary>
    /// The new material as cut-list rows, one per wall and kind of piece, so the shopping list buys
    /// it through the same aggregation as a cut list's (<see cref="FramingList.CutRows"/>).
    /// </summary>
    public static ImmutableArray<CutListRow> CutRows(IEnumerable<WallDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(diffs);
        return FramingList.CutRows(diffs.Select(diff => (diff.Wall, diff.New)));
    }

    /// <summary>
    /// What comes out, for the shopping list's Demolition section: "Wall 1: 2 studs 7'-7 1/2" come
    /// out (assuming a regular 16" layout in the existing wall)", one line per wall and kind of piece.
    /// </summary>
    public static ImmutableArray<DemolitionLine> Demolition(IEnumerable<WallDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(diffs);
        List<DemolitionLine> lines = [];
        foreach (WallDiff diff in diffs)
        {
            foreach (FramingPiece piece in diff.Out)
            {
                string what = $"{diff.Wall.Name}: {piece.Quantity.ToString(CultureInfo.InvariantCulture)} {FramingList.Label(piece.Role, piece.Quantity)} {CutListCsv.Text(piece.Length)}"
                              + (piece.Stock is { } stock ? $" ({stock.Name})" : string.Empty);
                lines.Add(new DemolitionLine($"{diff.Wall.Name}: {FramingList.Label(piece.Role, 1)} {CutListCsv.Text(piece.Length)}", piece.Quantity, diff.FromExisting ? diff.Assumption : string.Empty)
                {
                    Words = $"{what} come out" + (diff.FromExisting ? $" ({diff.Assumption})" : string.Empty),
                });
            }
        }

        return [.. lines];
    }
}
