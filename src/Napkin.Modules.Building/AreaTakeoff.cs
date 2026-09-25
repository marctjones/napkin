using System.Collections.Immutable;
using System.Globalization;
using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building;

/// <summary>Which finish a takeoff line is for.</summary>
public enum Finish
{
    /// <summary>The room's surfaces: walls gross and net, openings, ceiling, perimeter.</summary>
    Surfaces,

    /// <summary>Drywall sheets, one pool for walls and ceiling.</summary>
    Drywall,

    /// <summary>Insulation by area, in bags.</summary>
    Insulation,

    /// <summary>Insulation by stud bays: counted, not sized.</summary>
    InsulationBays,

    /// <summary>Paint, in gallons.</summary>
    Paint,

    /// <summary>Flooring with its waste allowance, in boxes.</summary>
    Flooring,

    /// <summary>Baseboard, in sticks.</summary>
    Baseboard,
}

/// <summary>
/// One line of a room's area takeoff (docs/design/renovation-sketches.md §5): what is counted, with
/// the typed values it used and how it was rounded.
/// </summary>
/// <param name="Room">The room's name.</param>
/// <param name="Finish">Which finish.</param>
/// <param name="Exact">The exact quantity the line rests on, before its one rounding: square units (1024² to the square inch) for an area, units for a length, a count of bays.</param>
/// <param name="Count">What to buy — sheets, bags, gallons, boxes, sticks, bays — or null when nothing typed says what it comes in.</param>
/// <param name="Shown">The quantity in words: "558 sq ft; 18 sheets 4'-0" × 8'-0"".</param>
/// <param name="Note">What it assumes or what to type: "sheets by area — a layout may need more".</param>
public sealed record TakeoffLine(string Room, Finish Finish, Int128 Exact, long? Count, string Shown, string Note)
{
    /// <summary>"Drywall: 558 sq ft; 18 sheets 4'-0" × 8'-0" (sheets by area — a layout may need more)".</summary>
    public string Text => $"{AreaTakeoff.Word(Finish)}: {Shown}" + (Note.Length > 0 ? $" ({Note})" : string.Empty);
}

/// <summary>
/// The area takeoff of a room (renovation-sketches §5): exact arithmetic in square units, every
/// product and quotient exact, each line rounding once at its end — up to a whole count, or to one
/// decimal place of square feet for an area shown alone. Only a New room is taken off; walls and
/// openings are read from the building as it will be.
/// </summary>
public static class AreaTakeoff
{
    /// <summary>Square units in a square foot: (12 × 1024)².</summary>
    public static readonly Int128 SquareFoot = Area.Of(Length.Inches(12), Length.Inches(12));

    /// <summary>What the drywall line says of its count.</summary>
    public const string SheetsByArea = "sheets by area — a layout may need more";

    /// <summary>What the flooring line says of its allowance.</summary>
    public const string Allowance = "napkin's allowance, not a fact about your floor";

    /// <summary>Every New room's takeoff, rooms in id order.</summary>
    public static ImmutableArray<TakeoffLine> All(Sketch sketch, MaterialsLibrary library, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(packs);
        FramingOptions options = CodeCheck.Framing(CodeCheck.Of(sketch, packs), library);
        return [.. Room.All(sketch).Where(room => room.Box.Phase == Phase.New).SelectMany(room => Of(sketch, room, library, options))];
    }

    /// <summary>One room's takeoff: the surfaces, then each finish ticked, in §5's order.</summary>
    public static ImmutableArray<TakeoffLine> Of(Sketch sketch, Room room, MaterialsLibrary library, FramingOptions options)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(options);
        Sketch after = sketch.After();
        RoomInputs inputs = room.Box.Room ?? RoomInputs.None;
        string name = room.Name;
        Length l = room.Length, w = room.Width, h = room.Height;
        ImmutableArray<BoundingWall> bounding = RoomBounds.Of(after, room);
        ImmutableArray<Opening> openings = RoomBounds.Openings(after, room);

        Int128 gross = Area.Of(room.Perimeter, h);
        Int128 openingArea = openings.Aggregate(Int128.Zero, (sum, o) => sum + Area.Of(o.Width, o.Height));
        Int128 net = gross - openingArea;
        Int128 ceiling = Area.Of(l, w);
        Length doors = openings.Where(o => o.Kind == OpeningKind.Door).Aggregate(Length.Zero, (sum, o) => sum + o.Width);

        List<TakeoffLine> lines = [];
        List<string> said = [];
        if (bounding.IsEmpty)
        {
            said.Add("no walls bound this room; openings are not subtracted");
        }

        MeasuredRoom m = inputs.Measured;
        if (new[] { m.South, m.North, m.East, m.West, m.Diagonal1, m.Diagonal2 }.Any(length => length is not null))
        {
            said.Add("from the drawn size");
        }

        lines.Add(new TakeoffLine(
            name,
            Finish.Surfaces,
            net,
            null,
            $"walls {SquareFeet(gross)} sq ft less openings {SquareFeet(openingArea)} = {SquareFeet(net)} sq ft; ceiling {SquareFeet(ceiling)} sq ft; perimeter {Text(room.Perimeter)}",
            string.Join("; ", said)));

        if (inputs.Drywall != RoomSurfaces.None)
        {
            Int128 area = inputs.Drywall == RoomSurfaces.WallsAndCeiling ? net + ceiling : net;
            string on = inputs.Drywall == RoomSurfaces.WallsAndCeiling ? "walls and ceiling" : "walls";
            lines.Add(inputs.Sheet is { } sheet
                ? new TakeoffLine(name, Finish.Drywall, area, Ceiling(area, Area.Of(sheet.Width, sheet.Length)), $"{on}, {SquareFeet(area)} sq ft; {Ceiling(area, Area.Of(sheet.Width, sheet.Length))} sheets {Text(sheet.Width)} × {Text(sheet.Length)}", SheetsByArea)
                : new TakeoffLine(name, Finish.Drywall, area, null, $"{on}, {SquareFeet(area)} sq ft", "type the sheet size you will buy"));
        }

        if (inputs.Insulation != InsulatedWalls.None)
        {
            List<BoundingWall> insulated = [.. bounding.Where(b => inputs.Insulation == InsulatedWalls.All || b.Wall.Box.WallInputs?.Side == WallSide.Exterior)];
            string which = inputs.Insulation == InsulatedWalls.All ? "all walls" : "exterior walls";
            if (inputs.InsulationBy == InsulationBy.Area)
            {
                Int128 area = insulated.Aggregate(Int128.Zero, (sum, b) => sum + Area.Of(b.Along, h))
                              - openings.Where(o => insulated.Any(b => b.Wall.Id == o.Wall.Id)).Aggregate(Int128.Zero, (sum, o) => sum + Area.Of(o.Width, o.Height));
                lines.Add(inputs.InsulationCoverage is { } coverage
                    ? new TakeoffLine(name, Finish.Insulation, area, Ceiling(area, coverage * SquareFoot), $"{which}, {SquareFeet(area)} sq ft; {Ceiling(area, coverage * SquareFoot)} bags at {coverage} sq ft", "coverage typed from the package")
                    : new TakeoffLine(name, Finish.Insulation, area, null, $"{which}, {SquareFeet(area)} sq ft", "type the coverage from the package"));
            }

            lines.Add(Bays(name, after, insulated, which, library, options, reference: inputs.InsulationBy == InsulationBy.Area));
        }

        if (inputs.Paint != RoomSurfaces.None)
        {
            Int128 surface = inputs.Paint == RoomSurfaces.WallsAndCeiling ? net + ceiling : net;
            string on = inputs.Paint == RoomSurfaces.WallsAndCeiling ? "walls and ceiling" : "walls";
            int coats = inputs.PaintCoats ?? 1;
            Int128 area = surface * coats;
            string coatsText = inputs.PaintCoats is { } typed ? $"{typed} {(typed == 1 ? "coat" : "coats")}" : "type the number of coats (1 used)";
            lines.Add(inputs.PaintCoverage is { } coverage
                ? new TakeoffLine(name, Finish.Paint, area, Ceiling(area, coverage * SquareFoot), $"{on}, {SquareFeet(area)} sq ft to cover; {Ceiling(area, coverage * SquareFoot)} gallons at {coverage} sq ft a gallon", $"{coatsText}; coverage typed from the can")
                : new TakeoffLine(name, Finish.Paint, area, null, $"{on}, {SquareFeet(area)} sq ft to cover", $"{coatsText}; type the coverage from your can"));
        }

        if (inputs.Flooring)
        {
            // The waste is an exact rational: area × (100 + waste) / 100, rounded up once.
            Int128 scaled = ceiling * (100 + inputs.FlooringWaste);
            long shown = Ceiling(scaled, 100 * SquareFoot);
            string waste = inputs.FlooringWaste == RoomInputs.DefaultFlooringWaste ? $"{inputs.FlooringWaste} % allowance, {Allowance}" : $"{inputs.FlooringWaste} % allowance, typed";
            lines.Add(inputs.FlooringBox is { } box
                ? new TakeoffLine(name, Finish.Flooring, scaled, Ceiling(scaled, 100 * box * SquareFoot), $"{shown} sq ft; {Ceiling(scaled, 100 * box * SquareFoot)} boxes at {box} sq ft", $"{waste}; rounded up")
                : new TakeoffLine(name, Finish.Flooring, scaled, null, $"{shown} sq ft", $"{waste}; rounded up; type the coverage from the box"));
        }

        if (inputs.Baseboard)
        {
            Length linear = room.Perimeter - doors;
            lines.Add(inputs.BaseboardStick is { } stick
                ? new TakeoffLine(name, Finish.Baseboard, linear.Units, Ceiling(linear.Units, stick.Units), $"{Text(linear)}; {Ceiling(linear.Units, stick.Units)} sticks of {Text(stick)}", "not allowing for corners or waste")
                : new TakeoffLine(name, Finish.Baseboard, linear.Units, null, Text(linear), "napkin has read no stock-length list for trim"));
        }

        return [.. lines];
    }

    /// <summary>
    /// The stud bays of the insulated walls napkin frames (§5.2): the gaps between adjacent
    /// full-height members, less one per opening; the odd bays at ends and beside openings are not sized.
    /// </summary>
    static TakeoffLine Bays(string name, Sketch after, List<BoundingWall> insulated, string which, MaterialsLibrary library, FramingOptions options, bool reference)
    {
        int bays = 0;
        List<Length> tall = [];
        List<string> unframed = [];
        Length spacing = FramingOptions.DefaultSpacing;
        foreach (Wall wall in insulated.Select(b => b.Wall).DistinctBy(wall => wall.Id))
        {
            WallFraming framing = FramingList.Frame(after, wall, library, options);
            if (framing.Pieces.IsEmpty)
            {
                unframed.Add(wall.Name);
                continue;
            }

            spacing = framing.Spacing;
            int members = framing.Count(FramingRole.Stud) + framing.Count(FramingRole.KingStud);
            bays += members - 1 - framing.Openings.Count(opening => opening.Refusal is null);
            tall.AddRange(framing.Pieces.Where(piece => piece.Role == FramingRole.Stud).Select(piece => piece.Length));
        }

        string heights = string.Join(" and ", tall.Distinct().Order().Select(Text));
        List<string> notes = [$"buy by area for square feet; the odd bays at ends and beside openings are not sized"];
        if (unframed.Count > 0)
        {
            notes.Add($"{string.Join(", ", unframed)} {(unframed.Count == 1 ? "is" : "are")} not framed by napkin, so {(unframed.Count == 1 ? "it has" : "they have")} no bays here");
        }

        return new TakeoffLine(
            name,
            Finish.InsulationBays,
            bays,
            bays,
            $"{(reference ? "for reference, " : string.Empty)}{which}, {bays} bays{(heights.Length > 0 ? $", {heights} tall" : string.Empty)}, at {FramingList.SpacingWords(spacing)}",
            string.Join("; ", notes));
    }

    /// <summary>The word a finish is said with.</summary>
    public static string Word(Finish finish) => finish switch
    {
        Finish.Surfaces => "Surfaces",
        Finish.Drywall => "Drywall",
        Finish.Insulation => "Insulation",
        Finish.InsulationBays => "Insulation by stud bays",
        Finish.Paint => "Paint",
        Finish.Flooring => "Flooring",
        Finish.Baseboard => "Baseboard",
        _ => throw new ArgumentOutOfRangeException(nameof(finish), finish, "Not a finish."),
    };

    /// <summary>
    /// An area in square feet, rounded once to one decimal place (half away from zero), the ".0"
    /// dropped: 558, 17.4, 184.8.
    /// </summary>
    public static string SquareFeet(Int128 units)
    {
        Int128 tenths = (units * 10 + (units >= 0 ? SquareFoot / 2 : -(SquareFoot / 2))) / SquareFoot;
        Int128 whole = tenths / 10, part = Int128.Abs(tenths % 10);
        return part == 0 ? whole.ToString(CultureInfo.InvariantCulture) : $"{whole.ToString(CultureInfo.InvariantCulture)}.{part.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>⌈a ÷ b⌉ for a non-negative a and a positive b, exactly.</summary>
    public static long Ceiling(Int128 a, Int128 b) => (long)((a + b - 1) / b);

    /// <summary>The takeoff as a CSV file carries it: its own header, then Room, Finish, Quantity, Count, Note.</summary>
    public static string ToCsv(IEnumerable<TakeoffLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        StringBuilder text = new();
        text.Append("Area takeoff\n");
        text.Append("Room,Finish,Quantity,Count,Note\n");
        foreach (TakeoffLine line in lines)
        {
            text.Append(CutListCsv.Field(line.Room)).Append(',')
                .Append(CutListCsv.Field(Word(line.Finish))).Append(',')
                .Append(CutListCsv.Field(line.Shown)).Append(',')
                .Append(line.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(CutListCsv.Field(line.Note)).Append('\n');
        }

        return text.ToString();
    }

    static string Text(Length length) => CutListCsv.Text(length);
}

/// <summary>
/// What a person measured of a real room, compared with the drawn box and reported, never drawn
/// (renovation-sketches §5.6). A square is compared in <see cref="Int128"/>: no square roots, no
/// floating point.
/// </summary>
public static class OutOfSquare
{
    /// <summary>What the measurements say about the room; empty when nothing disagrees or nothing is measured.</summary>
    public static ImmutableArray<string> Of(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        MeasuredRoom m = (room.Box.Room ?? RoomInputs.None).Measured;
        List<string> said = [];
        Opposite("south", m.South, "north", m.North, room.Length, said);
        Opposite("west", m.West, "east", m.East, room.Width, said);

        if (m.Diagonal1 is { } d1 && m.Diagonal2 is { } d2)
        {
            if (d1 != d2)
            {
                said.Add($"diagonals differ by {Text(Length.Abs(d1 - d2))}: out of square");
            }
        }
        else if ((m.Diagonal1 ?? m.Diagonal2) is { } d)
        {
            Int128 square = Area.Of(d, d), expected = Area.Of(room.Length, room.Length) + Area.Of(room.Width, room.Width);
            if (square != expected)
            {
                said.Add($"diagonal {Text(d)} measures {(square > expected ? "long" : "short")} for {Text(room.Width)} × {Text(room.Length)}");
            }
        }

        return [.. said];
    }

    static void Opposite(string a, Length? first, string b, Length? second, Length drawn, List<string> said)
    {
        if (first is { } one && second is { } two && one != two)
        {
            said.Add($"measured {a} {Text(one)}, {b} {Text(two)}: out of square; drawn as {Text(drawn)}");
        }
    }

    static string Text(Length length) => CutListCsv.Text(length);
}
