using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Whether a project's adopted code is locked to one revision of its pack or follows the pack's newer ones.</summary>
public enum CodeMode
{
    /// <summary>Locked to this pack at this revision, from the date it was locked.</summary>
    Locked,

    /// <summary>Follows the pack: a newer revision of the same pack is used, and the change is shown.</summary>
    Following,
}

/// <summary>
/// The adopted code a person chose for the project (docs/design/rules-engine-model.md §7.1): the
/// pack's id and revision, locked or following, and the date it was locked. Stored with the
/// design (format version 6); the rules engine's <c>CodeSelection</c> is built from it.
/// </summary>
/// <param name="PackId">The pack's id, e.g. <c>us-ct-2022</c>.</param>
/// <param name="Revision">The pack revision the project last used; at least 1.</param>
/// <param name="Mode">Locked or following.</param>
/// <param name="LockedOn">The date it was locked; null exactly when following.</param>
public sealed record CodeChoice(string PackId, int Revision, CodeMode Mode, DateOnly? LockedOn);

/// <summary>Where the site values came from: free text and a date, printed with the results.</summary>
/// <param name="Text">What the person wrote: "Town building department, phone".</param>
/// <param name="On">When, or null.</param>
public sealed record SiteSource(string Text, DateOnly? On);

/// <summary>
/// The site and hazard values the person typed for the project. Every field is null until entered
/// and is never given a default: the rules engine answers "input missing" for a table that needs
/// one that is null (docs/design/rules-engine-model.md §5).
/// </summary>
/// <param name="GroundSnowLoadPsf">Ground snow load, whole psf.</param>
/// <param name="UltimateWindSpeedMph">Ultimate design wind speed, whole mph.</param>
/// <param name="SeismicDesignCategory">Seismic design category, as the pack's tables name it.</param>
/// <param name="FrostDepth">Frost depth.</param>
/// <param name="BuildingWidth">Building width, as the code text defines it.</param>
/// <param name="RoofLiveLoadPsf">Roof live load, whole psf; asked for only when a table's footnote needs it.</param>
/// <param name="Source">Where the values came from, or null.</param>
public sealed record SiteValues(
    int? GroundSnowLoadPsf,
    int? UltimateWindSpeedMph,
    string? SeismicDesignCategory,
    Length? FrostDepth,
    Length? BuildingWidth,
    int? RoofLiveLoadPsf,
    SiteSource? Source)
{
    /// <summary>The soil bearing value, whole psf (format version 13, #42); null until entered.</summary>
    public int? SoilBearingPsf { get; init; }

    /// <summary>Nothing entered yet: a new design's site.</summary>
    public static readonly SiteValues NotEntered = new(null, null, null, null, null, null, null);
}

/// <summary>
/// A bracing method a person assigned to one solid segment of a wall line (issue #39,
/// docs/building.md): the segment is named by what bounds it, the opening it starts after and the
/// opening it ends before, where null is the wall's start or end. The method is a pack's method id;
/// it is not checked against any pack here.
/// </summary>
/// <param name="From">The opening the segment starts after, or null for the wall's start.</param>
/// <param name="To">The opening the segment ends before, or null for the wall's end.</param>
/// <param name="Method">The method id, e.g. as the adopted code's bracing provisions name it.</param>
public sealed record BracingAssignment(EntityId? From, EntityId? To, string Method);

/// <summary>
/// What a person entered for a box that is a wall: what it supports (one of the values the adopted
/// code's header table declares), its stud spacing, and the bracing methods assigned to its
/// segments. Any may be absent; a box with none holds no <see cref="WallInputs"/> at all.
/// </summary>
/// <param name="Supports">What the wall carries, as the pack's table names it; null when not chosen.</param>
/// <param name="StudSpacing">The stud spacing on centre; null for the default.</param>
/// <param name="Bracing">The bracing assignments, in the order written; empty when none (never defaulted).</param>
public sealed record WallInputs(string? Supports, Length? StudSpacing, ImmutableArray<BracingAssignment> Bracing)
{
    /// <summary>Inputs with no bracing assigned.</summary>
    public WallInputs(string? supports, Length? studSpacing)
        : this(supports, studSpacing, [])
    {
    }

    /// <summary>The bracing assignments; empty, never default.</summary>
    public ImmutableArray<BracingAssignment> Bracing { get; init; } = Bracing.IsDefault ? [] : Bracing;

    /// <summary>Exterior or interior; null until the person says (format version 10, renovation-sketches §4.3).</summary>
    public WallSide? Side { get; init; }

    /// <summary>Whether the wall is bearing; null until the person says. Only a bearing wall's headers are code-checked.</summary>
    public bool? Bearing { get; init; }

    /// <summary>The header the person chose for every opening in a not-bearing wall; null when none. Never a code result.</summary>
    public TypedHeader? Header { get; init; }

    /// <summary>These inputs, or null when nothing is set — the one spelling of "nothing entered".</summary>
    public WallInputs? OrNull()
        => Supports is null && StudSpacing is null && Bracing.IsEmpty && Side is null && Bearing is null && Header is null ? null : this;

    /// <summary>Equal when every field is, the assignments in order.</summary>
    public bool Equals(WallInputs? other)
        => other is not null && Supports == other.Supports && StudSpacing == other.StudSpacing && Bracing.SequenceEqual(other.Bracing)
           && Side == other.Side && Bearing == other.Bearing && Header == other.Header;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Supports);
        hash.Add(StudSpacing);
        hash.Add(Side);
        hash.Add(Bearing);
        hash.Add(Header);
        foreach (BracingAssignment assignment in Bracing)
        {
            hash.Add(assignment);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Which side of the building envelope a wall is on (renovation-sketches §4.3).</summary>
public enum WallSide
{
    /// <summary>An exterior wall: the exterior header table; insulated "on exterior walls".</summary>
    Exterior,

    /// <summary>An interior wall: the interior-bearing header table, when it is bearing.</summary>
    Interior,
}

/// <summary>
/// The header a person typed for a not-bearing wall: how many plies of which lumber. Their choice,
/// not a code result; the lumber is not checked against the library here, as a part's stock is not.
/// </summary>
/// <param name="Plies">How many pieces side by side, 1 to 3.</param>
/// <param name="Lumber">The lumber's nominal name, e.g. <c>2x6</c>.</param>
public sealed record TypedHeader(int Plies, string Lumber)
{
    /// <summary>"(2) 2x6".</summary>
    public override string ToString() => $"({Plies}) {Lumber}";
}

/// <summary>Which of a room's surfaces a finish goes on.</summary>
public enum RoomSurfaces
{
    /// <summary>The walls and the ceiling.</summary>
    WallsAndCeiling,

    /// <summary>The walls only.</summary>
    Walls,

    /// <summary>Not at all.</summary>
    None,
}

/// <summary>Which of a room's bounding walls are insulated.</summary>
public enum InsulatedWalls
{
    /// <summary>The bounding walls whose side is exterior.</summary>
    Exterior,

    /// <summary>Every bounding wall.</summary>
    All,

    /// <summary>None.</summary>
    None,
}

/// <summary>How insulation is taken off: by net area, or by counting stud bays.</summary>
public enum InsulationBy
{
    /// <summary>By the walls' net area.</summary>
    Area,

    /// <summary>By the full-height stud bays napkin frames.</summary>
    Bays,
}

/// <summary>A sheet size the person typed from the package: width by length, exact lengths.</summary>
/// <param name="Width">The sheet's width.</param>
/// <param name="Length">The sheet's length.</param>
public sealed record SheetSize(Length Width, Length Length);

/// <summary>
/// What a person measured of a real room: four wall lengths and two diagonals, any of them not
/// typed (null). Compared with the drawn box and reported, never drawn (renovation-sketches §5.6).
/// </summary>
/// <param name="South">The south wall's measured length.</param>
/// <param name="North">The north wall's.</param>
/// <param name="East">The east wall's.</param>
/// <param name="West">The west wall's.</param>
/// <param name="Diagonal1">One diagonal.</param>
/// <param name="Diagonal2">The other.</param>
public sealed record MeasuredRoom(Length? South, Length? North, Length? East, Length? West, Length? Diagonal1, Length? Diagonal2)
{
    /// <summary>Nothing measured.</summary>
    public static readonly MeasuredRoom None = new(null, null, null, null, null, null);
}

/// <summary>
/// The finishes a person ticked for a room and the values they typed from the packages
/// (renovation-sketches §5, §7). No sheet size, coverage or stick length is ever defaulted; the
/// one default is <see cref="DefaultFlooringWaste"/>, napkin's own allowance, labelled and editable.
/// </summary>
/// <param name="Drywall">Which surfaces get drywall.</param>
/// <param name="Sheet">The drywall sheet size, or null when not typed.</param>
/// <param name="Insulation">Which walls are insulated.</param>
/// <param name="InsulationBy">By area or by stud bays.</param>
/// <param name="InsulationCoverage">Square feet a bag covers, or null.</param>
/// <param name="Paint">Which surfaces are painted.</param>
/// <param name="PaintCoats">How many coats, or null.</param>
/// <param name="PaintCoverage">Square feet a gallon covers, or null.</param>
/// <param name="Flooring">Whether the floor is finished.</param>
/// <param name="FlooringWaste">The waste allowance, a whole percent.</param>
/// <param name="FlooringBox">Square feet a box covers, or null.</param>
/// <param name="Baseboard">Whether there is baseboard.</param>
/// <param name="BaseboardStick">The stick length bought, or null.</param>
/// <param name="Measured">What was measured of the real room.</param>
public sealed record RoomInputs(
    RoomSurfaces Drywall,
    SheetSize? Sheet,
    InsulatedWalls Insulation,
    InsulationBy InsulationBy,
    int? InsulationCoverage,
    RoomSurfaces Paint,
    int? PaintCoats,
    int? PaintCoverage,
    bool Flooring,
    int FlooringWaste,
    int? FlooringBox,
    bool Baseboard,
    Length? BaseboardStick,
    MeasuredRoom Measured)
{
    /// <summary>napkin's flooring waste allowance, 10 %: a design default, not a fact about any floor (§5.4).</summary>
    public const int DefaultFlooringWaste = 10;

    /// <summary>Nothing ticked, nothing typed, the default waste allowance, nothing measured.</summary>
    public static readonly RoomInputs None = new(
        RoomSurfaces.None, null, InsulatedWalls.None, InsulationBy.Area, null, RoomSurfaces.None, null, null,
        false, DefaultFlooringWaste, null, false, null, MeasuredRoom.None);
}

/// <summary>What a room's inputs may not be: the rules the loader and the updater share (§7).</summary>
public static class RoomRules
{
    /// <summary>Why these inputs are refused, or null when they are fine.</summary>
    public static string? Refusal(RoomInputs room)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Sheet is { } sheet && (sheet.Width <= Length.Zero || sheet.Length <= Length.Zero))
        {
            return "a drywall sheet's sides must be longer than zero";
        }

        if (room.InsulationCoverage is <= 0 || room.PaintCoverage is <= 0 || room.FlooringBox is <= 0)
        {
            return "a coverage must be more than zero square feet";
        }

        if (room.PaintCoats is < 1)
        {
            return "paint takes at least one coat";
        }

        if (room.FlooringWaste < 0)
        {
            return "a waste allowance cannot be negative";
        }

        if (room.BaseboardStick is { } stick && stick <= Length.Zero)
        {
            return "a baseboard stick must be longer than zero";
        }

        MeasuredRoom m = room.Measured;
        return new[] { m.South, m.North, m.East, m.West, m.Diagonal1, m.Diagonal2 }.Any(length => length is { } l && l <= Length.Zero)
            ? "a measured length must be longer than zero"
            : null;
    }
}
