using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building;

/// <summary>What a framing piece does in the wall.</summary>
public enum FramingRole
{
    /// <summary>The single plate the studs stand on.</summary>
    BottomPlate,

    /// <summary>The doubled plate over the studs (two pieces).</summary>
    TopPlate,

    /// <summary>A full-height stud on the layout, or an end stud.</summary>
    Stud,

    /// <summary>A full-height stud beside an opening, outside its jack.</summary>
    KingStud,

    /// <summary>A stud under the header's end, beside the opening.</summary>
    JackStud,

    /// <summary>The member over the opening. Its size is the code check's (M4 part 2).</summary>
    Header,

    /// <summary>The flat member at the bottom of a window's rough opening.</summary>
    RoughSill,

    /// <summary>A short stud under a rough sill, on the layout.</summary>
    CrippleBelow,

    /// <summary>A short stud between a sized header and the top plates, on the layout.</summary>
    CrippleAbove,
}

/// <summary>One kind of piece in a wall's frame: how many, how long, and out of what.</summary>
/// <param name="Role">What the piece does.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Length">Each one's exact length.</param>
/// <param name="Stock">What it is cut from, or <see langword="null"/> for a header nobody has sized yet.</param>
public sealed record FramingPiece(FramingRole Role, int Quantity, Length Length, LumberStock? Stock)
{
    /// <summary>What the piece is called, singular: "king stud".</summary>
    public string Label => FramingList.Label(Role, 1);
}

/// <summary>How one opening is framed.</summary>
/// <param name="Opening">The opening.</param>
/// <param name="JacksPerSide">Jack studs each side (one, a placeholder, until the code check sizes the header).</param>
/// <param name="HeaderLength">The header's length: the opening's width plus the jacks it bears on.</param>
/// <param name="HeaderRoom">The height between the top of the opening and the underside of the top plates.</param>
/// <param name="HeaderDepth">The header's depth, when the code check has sized it; otherwise null.</param>
/// <param name="Refusal">Why this opening could not be framed, or null when it was.</param>
public sealed record OpeningFraming(
    Opening Opening,
    int JacksPerSide,
    Length HeaderLength,
    Length HeaderRoom,
    Length? HeaderDepth,
    string? Refusal);

/// <summary>A wall's frame: its pieces, how each opening was framed, and anything it could not do.</summary>
/// <param name="Wall">The wall.</param>
/// <param name="Stock">The stud stock the wall's thickness names, or null when none in the library matches.</param>
/// <param name="Spacing">The stud spacing on centre used.</param>
/// <param name="Openings">Each opening in the wall, nearest the wall's start first.</param>
/// <param name="Pieces">The pieces, in <see cref="FramingRole"/> order and then longest first.</param>
/// <param name="Problems">Why the wall, or one of its openings, could not be framed; empty when all is well.</param>
public sealed record WallFraming(
    Wall Wall,
    LumberStock? Stock,
    Length Spacing,
    ImmutableArray<OpeningFraming> Openings,
    ImmutableArray<FramingPiece> Pieces,
    ImmutableArray<string> Problems)
{
    /// <summary>
    /// What the numbers rest on that is a choice rather than a fact: the spacing, when it is the
    /// default, and the jack count, while it is part 1's placeholder. Said wherever the frame is shown.
    /// </summary>
    public ImmutableArray<string> Notes { get; init; } = [];

    /// <summary>How many of the pieces with a role there are.</summary>
    public int Count(FramingRole role) => Pieces.Where(piece => piece.Role == role).Sum(piece => piece.Quantity);

    /// <summary>"8 studs, 2 king studs, 2 jack studs, …, 3 plates, 1 header (not yet sized)": the frame in one line.</summary>
    public string Summary
    {
        get
        {
            if (Pieces.IsEmpty)
            {
                return Problems.IsEmpty ? "nothing to frame" : Problems[0];
            }

            List<string> parts = [];
            foreach (FramingRole role in new[]
                     {
                         FramingRole.Stud, FramingRole.KingStud, FramingRole.JackStud, FramingRole.CrippleBelow,
                         FramingRole.CrippleAbove, FramingRole.RoughSill,
                     })
            {
                int count = Count(role);
                if (count > 0)
                {
                    parts.Add($"{count} {FramingList.Label(role, count)}");
                }
            }

            parts.Add($"{Count(FramingRole.BottomPlate) + Count(FramingRole.TopPlate)} plates");
            foreach (IGrouping<string?, FramingPiece> header in Pieces.Where(piece => piece.Role == FramingRole.Header).GroupBy(piece => piece.Stock?.Name))
            {
                int count = header.Sum(piece => piece.Quantity);
                parts.Add(header.Key is { } stock
                    ? $"{count} header {(count == 1 ? "piece" : "pieces")} ({stock})"
                    : $"{count} {(count == 1 ? "header" : "headers")} (not yet sized)");
            }

            return string.Join(", ", parts);
        }
    }
}

/// <summary>A sized header: how many plies of which library lumber (the code check's answer, <see cref="CodeCheck"/>).</summary>
/// <param name="Plies">How many pieces side by side.</param>
/// <param name="Stock">The lumber each ply is.</param>
public sealed record HeaderMember(int Plies, LumberStock Stock);

/// <summary>
/// Settings for <see cref="FramingList.Of(Sketch, MaterialsLibrary, FramingOptions?)"/>. The
/// functions are where the code check plugs in its jack and king counts and the header
/// (<see cref="CodeCheck.Framing"/>); a null answer means "not sized".
/// </summary>
public sealed record FramingOptions
{
    /// <summary>The default stud spacing, 16 in on centre: a design default, not a code requirement.</summary>
    public static readonly Length DefaultSpacing = Length.Inches(16);

    /// <summary>What <see cref="DefaultSpacing"/> is, said wherever it shows.</summary>
    public const string DefaultSpacingNote = "design default, not a code requirement";

    /// <summary>What the jack count is while no code has sized the header, said wherever it shows.</summary>
    public const string PlaceholderJacks = "1 jack and 1 king stud each side: a placeholder until the code check sizes the header";

    /// <summary>The stud spacing on centre, for a wall that has none of its own (<see cref="WallInputs.StudSpacing"/>).</summary>
    public Length Spacing { get; init; } = DefaultSpacing;

    /// <summary>Jack studs each side of an opening. Null, or a null answer, means the placeholder, one.</summary>
    public Func<Opening, int?>? JacksPerSide { get; init; }

    /// <summary>King studs each side of an opening. Null, or a null answer, means one.</summary>
    public Func<Opening, int?>? KingsPerSide { get; init; }

    /// <summary>The header's depth for an opening. Null, or a null answer, means the header member's width, if any.</summary>
    public Func<Opening, Length?>? HeaderDepth { get; init; }

    /// <summary>The sized header member for an opening. Null, or a null answer, means not yet sized: it buys nothing.</summary>
    public Func<Opening, HeaderMember?>? Header { get; init; }
}

/// <summary>
/// The frame each wall in a design implies — plates, studs, kings, jacks, header slots, rough sills
/// and cripples — derived from the walls and openings, never stored (docs/building.md).
/// </summary>
/// <remarks>
/// <para>The conventions, with <c>t</c> the stud stock's thickness (1 1/2 in for a 2x4 or 2x6),
/// <c>L</c>, <c>H</c> the wall's length and height, and <c>s</c> the spacing:</para>
/// <list type="bullet">
/// <item>The stud stock is the library's lumber whose dressed width is the wall's thickness.</item>
/// <item>One bottom plate and two top plates, each <c>L</c> long, in one piece.</item>
/// <item>Layout studs have their start face at <c>k·s</c> for k = 0, 1, … while <c>k·s + t ≤ L</c>;
/// then an end stud at <c>L − t</c> unless the last layout stud is already there. Each is
/// <c>H − 3t</c> long. For a 144 in wall at 16 in: 0…128 is 9 studs, plus the end stud, 10.</item>
/// <item>Each opening of width <c>w</c> at <c>a</c> has <c>j</c> jacks each side just outside it,
/// <c>sill + height − t</c> long, and <c>k</c> kings outside those, <c>H − 3t</c> long; both are 1
/// (a placeholder) until the code check sizes the header. A layout or end stud whose body touches
/// <c>[a − (j+k)t, a + w + (j+k)t)</c> is left out.</item>
/// <item>A header <c>w + 2jt</c> long fills the room from the opening's top to the top plates
/// (<c>H − 2t − top</c>). Its member is the code check's (<see cref="HeaderMember"/>): plies of a
/// library lumber, whose dressed width is the header's depth <c>d</c>; cripples above,
/// <c>room − d</c> long, stand at the layout positions over it. Unsized, it buys nothing.</item>
/// <item>A window gets a rough sill <c>w</c> long and cripples below, <c>sill − 2t</c> long, at the
/// layout positions wholly inside the opening. A door (sill 0) gets neither; its bottom plate is
/// bought whole and cut out across the opening when the wall stands.</item>
/// </list>
/// </remarks>
public static class FramingList
{
    /// <summary>The frame of every wall in a sketch, in id order.</summary>
    public static ImmutableArray<WallFraming> Of(Sketch sketch, MaterialsLibrary library, FramingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);
        options ??= new FramingOptions();
        return [.. Wall.All(sketch).Select(wall => Frame(sketch, wall, library, options))];
    }

    /// <summary>The frame of one wall.</summary>
    public static WallFraming Frame(Sketch sketch, Wall wall, MaterialsLibrary library, FramingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(library);
        options ??= new FramingOptions();

        Length spacing = wall.Box.WallInputs?.StudSpacing ?? options.Spacing;
        LumberStock? stock = StudStock(library, wall.Thickness);
        ImmutableArray<Opening> openings = Opening.In(sketch, wall);

        string? refusal = WallRefusal(wall, stock, spacing);
        if (refusal is not null)
        {
            return new WallFraming(wall, stock, spacing, [], [], [refusal]);
        }

        Length t = stock!.Thickness;
        Length studLength = wall.Height - (3 * t);
        Length plateTop = wall.Height - (2 * t);

        List<Length> layout = [];
        for (Length at = Length.Zero; at + t <= wall.Length; at += spacing)
        {
            layout.Add(at);
        }

        List<Length> studs = [.. layout];
        if (studs[^1] != wall.Length - t)
        {
            studs.Add(wall.Length - t);
        }

        List<OpeningFraming> framed = [];
        List<string> problems = [];
        List<(Length From, Length To, string Name)> zones = [];
        List<FramingPiece> pieces =
        [
            new(FramingRole.BottomPlate, 1, wall.Length, stock),
            new(FramingRole.TopPlate, 2, wall.Length, stock),
        ];

        bool placeholder = false;
        foreach (Opening opening in openings)
        {
            int? sizedJacks = options.JacksPerSide?.Invoke(opening);
            int jacks = sizedJacks ?? 1;
            int kings = options.KingsPerSide?.Invoke(opening) ?? 1;
            HeaderMember? member = options.Header?.Invoke(opening);
            Length side = (jacks + kings) * t;
            Length from = opening.Offset - side;
            Length to = opening.Offset + opening.Width + side;
            Length headerLength = opening.Width + (2 * jacks * t);
            Length room = plateTop - opening.Top;
            Length? depth = options.HeaderDepth?.Invoke(opening) ?? member?.Stock.Width;

            string? why = OpeningRefusal(wall, opening, t, jacks, kings, from, to, room, depth, zones);
            framed.Add(new OpeningFraming(opening, jacks, headerLength, room, depth, why));
            if (why is not null)
            {
                problems.Add($"{opening.Name}: {why}");
                continue;
            }

            placeholder |= sizedJacks is null;
            zones.Add((from, to, opening.Name));
            pieces.Add(new FramingPiece(FramingRole.KingStud, 2 * kings, studLength, stock));
            pieces.Add(new FramingPiece(FramingRole.JackStud, 2 * jacks, opening.Top - t, stock));
            pieces.Add(member is { } sized
                ? new FramingPiece(FramingRole.Header, sized.Plies, headerLength, sized.Stock)
                : new FramingPiece(FramingRole.Header, 1, headerLength, null));

            if (depth is { } d && room - d > Length.Zero)
            {
                Length low = opening.Offset - (jacks * t);
                Length high = opening.Offset + opening.Width + (jacks * t);
                int above = layout.Count(at => at >= low && at + t <= high);
                pieces.Add(new FramingPiece(FramingRole.CrippleAbove, above, room - d, stock));
            }

            if (opening.Kind == OpeningKind.Window)
            {
                pieces.Add(new FramingPiece(FramingRole.RoughSill, 1, opening.Width, stock));
                Length below = opening.Sill - (2 * t);
                int count = layout.Count(at => at >= opening.Offset && at + t <= opening.Offset + opening.Width);
                if (below > Length.Zero)
                {
                    pieces.Add(new FramingPiece(FramingRole.CrippleBelow, count, below, stock));
                }
            }
        }

        int common = studs.Count(at => !zones.Any(zone => at < zone.To && at + t > zone.From));
        pieces.Add(new FramingPiece(FramingRole.Stud, common, studLength, stock));

        List<string> notes = [];
        if (spacing == FramingOptions.DefaultSpacing && wall.Box.WallInputs?.StudSpacing is null)
        {
            notes.Add($"studs {spacing.Format(new InchesOnlyFormat(16)).Text} on centre: {FramingOptions.DefaultSpacingNote}");
        }

        if (placeholder)
        {
            notes.Add(FramingOptions.PlaceholderJacks);
        }

        return new WallFraming(wall, stock, spacing, [.. framed], Merge(pieces), [.. problems]) { Notes = [.. notes] };
    }

    /// <summary>The frame of the wall with this id, or of the wall an opening with this id is in; null for anything else.</summary>
    public static WallFraming? For(Sketch sketch, EntityId id, MaterialsLibrary library, FramingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        if (sketch.Find<Box>(id) is { } box && Wall.Is(sketch, box))
        {
            return Frame(sketch, new Wall(box), library, options);
        }

        return Opening.Find(sketch, id) is { } opening ? Frame(sketch, opening.Wall, library, options) : null;
    }

    /// <summary>
    /// The frames as cut-list rows, one per wall and kind of piece, so that
    /// <see cref="ShoppingList.Of"/> turns them into boards to buy exactly as it does a cut list's.
    /// </summary>
    public static ImmutableArray<CutListRow> CutRows(IEnumerable<WallFraming> framings)
    {
        ArgumentNullException.ThrowIfNull(framings);
        List<CutListRow> rows = [];
        foreach (WallFraming framing in framings)
        {
            foreach (FramingPiece piece in framing.Pieces)
            {
                rows.Add(new CutListRow(
                    $"{framing.Wall.Name} {piece.Label}",
                    piece.Quantity,
                    piece.Length,
                    piece.Stock?.Width ?? framing.Wall.Thickness,
                    piece.Stock?.Thickness ?? Length.Zero,
                    piece.Stock?.Name ?? "header, not yet sized",
                    Unresolved: false,
                    piece.Stock,
                    [],
                    new PlanAxes(PartDimension.Length, PartDimension.Width),
                    [framing.Wall.Id]));
            }
        }

        return [.. rows];
    }

    /// <summary>The lumber in the library whose dressed width is this thickness, 2x first; null when there is none.</summary>
    public static LumberStock? StudStock(MaterialsLibrary library, Length thickness)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Items
            .OfType<LumberStock>()
            .Where(lumber => lumber.Width == thickness && lumber.SizeClass == SizeClass.Dimension && !lumber.StandardLengths.IsEmpty)
            .OrderBy(lumber => lumber.NominalThickness)
            .FirstOrDefault();
    }

    /// <summary>"stud", "studs", "king stud", …</summary>
    public static string Label(FramingRole role, int count)
    {
        string one = role switch
        {
            FramingRole.BottomPlate => "bottom plate",
            FramingRole.TopPlate => "top plate",
            FramingRole.Stud => "stud",
            FramingRole.KingStud => "king stud",
            FramingRole.JackStud => "jack stud",
            FramingRole.Header => "header",
            FramingRole.RoughSill => "rough sill",
            FramingRole.CrippleBelow => "cripple below",
            FramingRole.CrippleAbove => "cripple above",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Not a framing role."),
        };

        return count == 1 ? one : one switch
        {
            "cripple below" => "cripples below",
            "cripple above" => "cripples above",
            _ => one + "s",
        };
    }

    private static string? WallRefusal(Wall wall, LumberStock? stock, Length spacing)
    {
        if (!wall.Box.Orientation.IsExact || wall.Box.FaceUp != BoxFace.Top)
        {
            return "napkin frames a wall only standing as drawn, turned by a right angle at most";
        }

        if (stock is null)
        {
            return $"no framing lumber in the library is {Text(wall.Thickness)} deep; "
                   + "make the wall 3 1/2\" (2x4) or 5 1/2\" (2x6) thick";
        }

        if (spacing <= stock.Thickness)
        {
            return $"a stud spacing of {Text(spacing)} leaves no room between studs";
        }

        if (wall.Length < 2 * stock.Thickness)
        {
            return $"the wall is {Text(wall.Length)} long, too short for its two end studs";
        }

        if (wall.Height <= 3 * stock.Thickness)
        {
            return $"the wall is {Text(wall.Height)} tall, no taller than its three plates";
        }

        return null;
    }

    private static string? OpeningRefusal(
        Wall wall,
        Opening opening,
        Length t,
        int jacks,
        int kings,
        Length from,
        Length to,
        Length room,
        Length? depth,
        List<(Length From, Length To, string Name)> zones)
    {
        if (jacks < 1)
        {
            return "an opening needs at least one jack stud each side";
        }

        if (kings < 1)
        {
            return "an opening needs at least one king stud each side";
        }

        if (opening.Width >= wall.Length)
        {
            return $"it is {Text(opening.Width)} wide, as wide as the wall or wider";
        }

        if (from < Length.Zero || to > wall.Length)
        {
            return $"it is too near the wall's end for its king and jack studs, which need {Text((jacks + kings) * t)} each side";
        }

        if (room < Length.Zero)
        {
            return "it reaches into the top plates";
        }

        if (room == Length.Zero)
        {
            return "it leaves no room for a header under the top plates";
        }

        if (depth is { } d && d > room)
        {
            return $"a {Text(d)} header does not fit the {Text(room)} above it";
        }

        if (opening.Kind == OpeningKind.Window && opening.Sill < 2 * t)
        {
            return $"its sill is {Text(opening.Sill)} up, too low for a rough sill on the bottom plate ({Text(2 * t)} at least)";
        }

        foreach ((Length From, Length To, string Name) zone in zones)
        {
            if (from < zone.To && to > zone.From)
            {
                return $"it is too close to {zone.Name} for both openings' king and jack studs";
            }
        }

        return null;
    }

    private static ImmutableArray<FramingPiece> Merge(IEnumerable<FramingPiece> pieces)
        => [
            .. pieces
                .Where(piece => piece.Quantity > 0)
                .GroupBy(piece => (piece.Role, piece.Length, piece.Stock))
                .Select(group => group.First() with { Quantity = group.Sum(piece => piece.Quantity) })
                .OrderBy(piece => piece.Role)
                .ThenByDescending(piece => piece.Length),
        ];

    private static string Text(Length length) => CutListCsv.Text(length);
}
