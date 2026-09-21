namespace Napkin.Core.Geometry;

/// <summary>
/// The identity of an entity, stable for its whole life.
/// </summary>
/// <remarks>
/// Ids survive save/load (#6) and undo/redo (#11), and they are what relationships and dimensions
/// refer to. A GUID rather than a per-file counter so that copy/paste between projects never
/// collides and so a file never needs a "next id" field. Version 7 only for locality when
/// sorting; nothing depends on it (design &#xA7;2.2).
/// </remarks>
/// <param name="Value">The underlying GUID.</param>
public readonly record struct EntityId(Guid Value) : IComparable<EntityId>
{
    /// <summary>A fresh id.</summary>
    public static EntityId New() => new(Guid.CreateVersion7());

    /// <inheritdoc/>
    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D")[..8];
}

/// <summary>The identity of a relationship, stable for its whole life.</summary>
/// <remarks>
/// Relationships are iterated in this order — never in dictionary order — so that the direct
/// updater's results are reproducible (design &#xA7;4.4 step 3).
/// </remarks>
/// <param name="Value">The underlying GUID.</param>
public readonly record struct RelationshipId(Guid Value) : IComparable<RelationshipId>
{
    /// <summary>A fresh id.</summary>
    public static RelationshipId New() => new(Guid.CreateVersion7());

    /// <inheritdoc/>
    public int CompareTo(RelationshipId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D")[..8];
}

/// <summary>The identity of a layer.</summary>
/// <param name="Value">The underlying GUID.</param>
public readonly record struct LayerId(Guid Value) : IComparable<LayerId>
{
    /// <summary>
    /// The layer every sketch starts with. A well-known id so that <see cref="Sketch.Empty"/> is
    /// the same value in every process, which keeps tests and round trips deterministic.
    /// </summary>
    public static readonly LayerId Default = new(new Guid("00000000-0000-0000-0000-000000000001"));

    /// <summary>A fresh id.</summary>
    public static LayerId New() => new(Guid.CreateVersion7());

    /// <inheritdoc/>
    public int CompareTo(LayerId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D")[..8];
}

/// <summary>A drawing layer. #11 owns the UI for these.</summary>
/// <param name="Id">The layer's identity.</param>
/// <param name="Name">The layer's name, as shown to the user.</param>
public sealed record Layer(LayerId Id, string Name)
{
    /// <summary>The layer every sketch starts with.</summary>
    public static readonly Layer Default = new(LayerId.Default, "Default");
}
