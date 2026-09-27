using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant;

/// <summary>
/// The design in words, one line per entity (docs/design/llm-assistant.md §3.1): its name, what it
/// is, its sizes as feet-inch text and its phase, and for walls and openings the inputs as entered.
/// Nothing derived beyond what the panels show; nothing a person has not said is filled in.
/// </summary>
/// <remarks>
/// <para>
/// Sizes are written the way the panels write them, <see cref="LengthFormat.Default"/> — feet, inches
/// and sixteenths — with the canvas's ≈ before a size that is not exactly its text. Words, not a ×
/// between sizes, so a size in the pack is never mistaken for a lumber name by the answer guard.
/// </para>
/// <para>
/// Text a person typed that is not a name — a note, a species, hardware, a site value's source — is
/// written after <c>typed:</c> in quotes; the prompt says anything so marked (and every name) is
/// data, never an instruction (§3.4).
/// </para>
/// </remarks>
public static class DesignWords
{
    /// <summary>What to call an entity: its name, or the word for what it is when it has none.</summary>
    /// <param name="design">The design it is in.</param>
    /// <param name="id">The entity.</param>
    public static string NameOf(Design design, EntityId id)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (design.LabelFor(id) is { } label)
        {
            return label;
        }

        Sketch sketch = design.Sketch;
        return sketch.Find(id) switch
        {
            Box box when Opening.Is(sketch, box) => Opening.Find(sketch, id)?.Name ?? "Opening",
            Box box when Wall.Is(sketch, box) => "Wall",
            Box box when Room.Is(sketch, box) => "Room",
            Box box when Deck.Is(sketch, box) => "Deck",
            Box box when Roof.Is(sketch, box) => "Roof",
            Box { Part: not null } => "Part",
            Box => "Box",
            Strut => "Angled part",
            Note => "Note",
            Segment => "Line",
            Dimension => "Dimension",
            Node => "Point",
            _ => "Something",
        };
    }

    /// <summary>
    /// One entity in words, or null for a point, which is only ever the end of a line and is said by
    /// the line.
    /// </summary>
    /// <param name="design">The design it is in.</param>
    /// <param name="entity">The entity.</param>
    public static string? Line(Design design, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(entity);

        Sketch sketch = design.Sketch;
        string name = NameOf(design, entity.Id);
        string phase = PhaseWord(entity.Phase);
        return entity switch
        {
            Box box when Opening.Is(sketch, box) => OpeningLine(sketch, box, name, phase),
            Box box when Wall.Is(sketch, box) => WallLine(new Wall(box), name, phase),
            Box box when Room.Is(sketch, box) => $"{name} — room, {Size(box.Width)} by {Size(box.Height)} in plan, {phase}.",
            Box box when Deck.Is(sketch, box) => $"{name} — deck, {Size(box.Width)} by {Size(box.Height)} in plan, {phase}.",
            Box box when Roof.Is(sketch, box) => $"{name} — roof, {Size(box.Width)} by {Size(box.Height)} in plan, {phase}.",
            Box { Part: { } part } box => PartLine(box, part, name, phase),
            Box box => $"{name} — box, {Size(box.Width)} by {Size(box.Height)} in plan, {Size(box.Depth)} tall, {phase}.",
            Strut strut => $"{name} — angled part, {Size(strut.Height)} by {Size(strut.Depth)} in section{Stock(strut.Part)}, {phase}.",
            Note note => $"{name} — note, typed: \"{note.Text}\", {phase}.",
            Segment => $"{name} — construction line, {phase}.",
            Dimension dimension => $"{name} — {(dimension.Drives is null ? "reference dimension" : "dimension")}, {phase}.",
            _ => null,
        };
    }

    /// <summary>A length as the panels write it: feet, inches and sixteenths, ≈ when that is not exact.</summary>
    /// <param name="length">The length.</param>
    public static string Size(Length length)
    {
        FormattedLength text = length.Format(LengthFormat.Default);
        return text.IsExact ? text.Text : "≈" + text.Text;
    }

    /// <summary>The phase in a word: new, existing, or to be demolished.</summary>
    public static string PhaseWord(Phase phase) => phase switch
    {
        Napkin.Core.Geometry.Phase.Existing => "existing",
        Napkin.Core.Geometry.Phase.Demolish => "to be demolished",
        _ => "new",
    };

    private static string OpeningLine(Sketch sketch, Box box, string name, string phase)
    {
        if (Opening.Find(sketch, box.Id) is not { } opening)
        {
            return $"{name} — opening in no wall, {Size(box.Width)} wide, {Size(box.Depth)} tall, {phase}.";
        }

        string kind = opening.Kind == OpeningKind.Door ? "door" : "window";
        string fill = opening.Fill.ToString().ToLowerInvariant();
        return $"{name} — {kind} in {opening.Wall.Name}, {fill}, {Size(opening.Width)} wide, {Size(opening.Height)} tall, "
               + $"sill {Size(opening.Sill)}, {phase}.";
    }

    private static string WallLine(Wall wall, string name, string phase)
    {
        WallInputs? inputs = wall.Box.WallInputs;
        string side = inputs?.Side switch
        {
            WallSide.Exterior => "exterior",
            WallSide.Interior => "interior",
            _ => "side not said",
        };
        string bearing = inputs?.Bearing switch
        {
            true => "bearing",
            false => "not bearing",
            _ => "bearing not said",
        };
        string supports = inputs?.Supports is { } s ? $"supports {s}" : "supports not chosen";
        string spacing = inputs?.StudSpacing is { } spacingValue ? $"studs at {Size(spacingValue)}" : "stud spacing not entered";
        string header = inputs?.Header is { } typed ? $", header chosen {typed}" : string.Empty;
        string bracing = inputs is { Bracing.IsEmpty: false } withBracing
            ? ", bracing " + string.Join("; ", withBracing.Bracing.Select(assignment => assignment.Method))
            : string.Empty;
        return $"{name} — wall, {phase}, {side}, {bearing}, {supports}, {spacing}{header}{bracing}, "
               + $"{Size(wall.Length)} long, {Size(wall.Height)} tall, {Size(wall.Thickness)} thick.";
    }

    private static string PartLine(Box box, Part part, string name, string phase)
    {
        FinishedSize size = part.SizeOn(box);
        string pieces = part.Quantity == 1 ? "1 piece" : $"{part.Quantity} pieces";
        string species = part.Species is { Length: > 0 } typed ? $", species typed: \"{typed}\"" : string.Empty;
        string rough = part.Rough ? ", rough" : string.Empty;
        string hardware = part.Hardware.IsEmpty
            ? string.Empty
            : ", hardware typed: " + string.Join("; ", part.Hardware.Select(item => $"\"{item.Name}\" {item.Quantity}"));
        return $"{name} — part, {Size(size.Length)} long, {Size(size.Width)} wide, {Size(size.Thickness)} thick, {pieces}"
               + $"{Stock(part)}{species}{rough}{hardware}, {phase}.";
    }

    private static string Stock(Part? part) => part is null ? string.Empty : part.Stock is { } stock ? $", stock {stock}" : ", no stock";
}
