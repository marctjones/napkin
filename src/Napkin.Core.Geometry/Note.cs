namespace Napkin.Core.Geometry;

/// <summary>
/// Whether an entity is already there, going in, or coming out (docs/design/renovation-sketches.md
/// §6.1). Every entity has one; a new entity starts <see cref="New"/>, napkin's default.
/// </summary>
public enum Phase
{
    /// <summary>Going in: drawn solid, and what the cut list and the shopping list buy.</summary>
    New,

    /// <summary>Already there: drawn ghosted, never bought or cut.</summary>
    Existing,

    /// <summary>Coming out: drawn dashed with a cross, counted under Demolition, in no check.</summary>
    Demolish,
}

/// <summary>The small symbol a note is drawn with, for the rough-in words napkin knows (§7).</summary>
public enum NoteSymbol
{
    /// <summary>No symbol: the note is drawn as a small pencil circle with its words.</summary>
    None,

    /// <summary>An electrical outlet.</summary>
    Outlet,

    /// <summary>A light switch.</summary>
    Switch,

    /// <summary>A light.</summary>
    Light,

    /// <summary>A water supply.</summary>
    Supply,

    /// <summary>A drain.</summary>
    Drain,
}

/// <summary>
/// Words at a point on the plan: "outlet", "switch", "drain", anything (renovation-sketches §7).
/// A note has no size and no relationships; it is counted on the shopping list, never modelled.
/// </summary>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the note is on (Notes, by convention; drawn as a note whatever its layer).</param>
/// <param name="Position">Where the note is, in the plan.</param>
/// <param name="Text">What it says; may be empty when <paramref name="Symbol"/> is not <see cref="NoteSymbol.None"/>.</param>
/// <param name="Symbol">The symbol it is drawn with.</param>
public sealed record Note(EntityId Id, LayerId Layer, Point2 Position, string Text, NoteSymbol Symbol) : Entity(Id, Layer)
{
    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };
}
