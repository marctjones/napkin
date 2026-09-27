namespace Napkin.Core.Geometry;

/// <summary>What a piece of furniture is, when the person says (docs/design/furniture-checks.md §9.1): never detected.</summary>
public enum FurnitureKind
{
    /// <summary>Nothing said: no furniture-specific check applies.</summary>
    None,

    /// <summary>A clothing storage unit: a dresser, a chest, a wardrobe (the tip-over estimate, §4).</summary>
    ClothingStorage,

    /// <summary>A bunk bed (§5).</summary>
    BunkBed,
}

/// <summary>The design-level marks the furniture checks read (format version 14, §4.2, §9.1, §9.3).</summary>
/// <param name="Kind">What the piece is, as the person said.</param>
/// <param name="Anchored">Whether it is anchored to the wall: said beside the tip-over estimate, which still runs (§9.3).</param>
public sealed record FurnitureMarks(FurnitureKind Kind, bool Anchored)
{
    /// <summary>Nothing said.</summary>
    public static readonly FurnitureMarks None = new(FurnitureKind.None, false);
}

/// <summary>
/// A part marked as a drawer (§4.2): the mark is on one part, and the drawer is every part joined to
/// it — its jointed group, derived, never stored.
/// </summary>
/// <param name="Extension">How far the drawer opens, typed; longer than zero.</param>
public sealed record DrawerMark(Length Extension);
