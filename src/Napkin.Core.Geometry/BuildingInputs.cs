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
    /// <summary>Nothing entered yet: a new design's site.</summary>
    public static readonly SiteValues NotEntered = new(null, null, null, null, null, null, null);
}

/// <summary>
/// What a person entered for a box that is a wall: what it supports (one of the values the adopted
/// code's header table declares) and its stud spacing. Either may be null; a box with neither holds
/// no <see cref="WallInputs"/> at all.
/// </summary>
/// <param name="Supports">What the wall carries, as the pack's table names it; null when not chosen.</param>
/// <param name="StudSpacing">The stud spacing on centre; null for the default.</param>
public sealed record WallInputs(string? Supports, Length? StudSpacing)
{
    /// <summary>These inputs, or null when neither is set — the one spelling of "nothing entered".</summary>
    public WallInputs? OrNull() => Supports is null && StudSpacing is null ? null : this;
}
