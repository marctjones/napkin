using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>Where a site value came from: free text and a date, printed with the results (design §5).</summary>
public sealed record InputProvenance(string Text, DateOnly? On);

/// <summary>
/// The site and hazard values a person entered for the project (design §5). A null field means
/// "not entered yet", and it is never replaced by a default: a table that needs it returns
/// <see cref="HeaderResult.InputMissing"/> naming it. There are no default parameter values.
/// </summary>
public sealed record SiteInputs
{
    /// <summary>Builds the site inputs; every argument is required, null meaning "not entered".</summary>
    /// <exception cref="ArgumentOutOfRangeException">A negative value (a caller bug, design §3.1).</exception>
    public SiteInputs(
        int? groundSnowLoadPsf,
        int? ultimateWindSpeedMph,
        string? seismicDesignCategory,
        Length? frostDepth,
        Length? buildingWidth,
        InputProvenance? provenance)
    {
        if (groundSnowLoadPsf < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(groundSnowLoadPsf), groundSnowLoadPsf, "A ground snow load is not negative.");
        }

        if (ultimateWindSpeedMph < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ultimateWindSpeedMph), ultimateWindSpeedMph, "A wind speed is not negative.");
        }

        if (frostDepth < Length.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(frostDepth), frostDepth, "A frost depth is not negative.");
        }

        if (buildingWidth <= Length.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(buildingWidth), buildingWidth, "A building width is positive.");
        }

        GroundSnowLoadPsf = groundSnowLoadPsf;
        UltimateWindSpeedMph = ultimateWindSpeedMph;
        SeismicDesignCategory = seismicDesignCategory;
        FrostDepth = frostDepth;
        BuildingWidth = buildingWidth;
        Provenance = provenance;
    }

    /// <summary>Ground snow load, whole psf, or null when not entered.</summary>
    public int? GroundSnowLoadPsf { get; }

    /// <summary>Ultimate design wind speed, whole mph, or null when not entered.</summary>
    public int? UltimateWindSpeedMph { get; }

    /// <summary>Seismic design category as the pack's tables name it, or null when not entered.</summary>
    public string? SeismicDesignCategory { get; }

    /// <summary>Frost depth, or null when not entered.</summary>
    public Length? FrostDepth { get; }

    /// <summary>Building width as the code text defines it, or null when not entered.</summary>
    public Length? BuildingWidth { get; }

    /// <summary>Where the values came from.</summary>
    public InputProvenance? Provenance { get; }
}

/// <summary>A question for the header table: what the wall supports, which wall, the span and the site (design §3.2).</summary>
public sealed record HeaderRequest
{
    /// <summary>Builds the request.</summary>
    /// <param name="supports">What the wall carries, one of the table's declared <c>supports</c> values.</param>
    /// <param name="kind">Which wall; selects the table.</param>
    /// <param name="headerSpan">The span as the table defines it (computed by the building module).</param>
    /// <param name="site">The project's site inputs.</param>
    /// <exception cref="ArgumentException">A non-positive span: a caller bug, not a result (design §3.1).</exception>
    public HeaderRequest(string supports, WallKind kind, Length headerSpan, SiteInputs site)
    {
        ArgumentNullException.ThrowIfNull(supports);
        ArgumentNullException.ThrowIfNull(site);
        if (headerSpan <= Length.Zero)
        {
            throw new ArgumentException("A header span is positive.", nameof(headerSpan));
        }

        Supports = supports;
        Kind = kind;
        HeaderSpan = headerSpan;
        Site = site;
    }

    /// <summary>What the wall carries.</summary>
    public string Supports { get; }

    /// <summary>Which wall.</summary>
    public WallKind Kind { get; }

    /// <summary>The header span.</summary>
    public Length HeaderSpan { get; }

    /// <summary>The site inputs.</summary>
    public SiteInputs Site { get; }
}

/// <summary>Which limit put a request outside the prescriptive table (design §3.2).</summary>
public enum OutOfScopeReason
{
    /// <summary>The span is longer than the longest row in the matched bands.</summary>
    SpanExceedsTable,

    /// <summary>An input is heavier or wider than the table's most demanding band.</summary>
    InputAboveTableBands,

    /// <summary>An input is below the smallest value the table's domain declares.</summary>
    InputBelowTableBands,

    /// <summary>A category (what the wall supports, say) the table has no rows for.</summary>
    ConditionNotCovered,

    /// <summary>A footnote encoded as a limit excludes this case.</summary>
    NarrowedByFootnote,

    /// <summary>The code text itself says to consult an engineer for this case.</summary>
    NotPrescriptive,
}

/// <summary>Why there is no answer from data at all.</summary>
public enum NoDataReason
{
    /// <summary>The project has no adopted code selected, or its pack is unavailable.</summary>
    NoPackSelected,

    /// <summary>The loaded pack has no header table for this wall kind (for example, its base tables are not filled in yet).</summary>
    NoTableForWallKind,
}

/// <summary>
/// The answer to a header question: a closed set of named, expected results, with no catch-all
/// (design §3.1). Every member says what happened; none is a guess.
/// </summary>
public abstract record HeaderResult
{
    private HeaderResult()
    {
    }

    /// <summary>A row covers the request: the header, stud counts and that row's citation.</summary>
    public sealed record Sized(MemberSpec Header, int JackStuds, int KingStuds, Citation Citation) : HeaderResult
    {
        /// <inheritdoc/>
        public override string ToString() => $"{Header}, {JackStuds} jack, {KingStuds} king — {Citation}";
    }

    /// <summary>
    /// No row covers the request. Which limit stopped it, and its citation: "outside the
    /// prescriptive tables — get an engineer", never an error state and never a number.
    /// </summary>
    public sealed record OutOfScope(OutOfScopeReason Reason, Citation Limit, string Explanation) : HeaderResult
    {
        /// <inheritdoc/>
        public override string ToString() => $"out of prescriptive scope ({Reason}): {Explanation} — limit: {Limit}";
    }

    /// <summary>
    /// The table needs a site value the person has not entered. No lookup ran and nothing was
    /// defaulted; the inputs are named so the UI can ask for exactly them.
    /// </summary>
    public sealed record InputMissing(ValueList<string> Inputs, string Table, AdoptedCodeRef Code, string Explanation) : HeaderResult
    {
        /// <inheritdoc/>
        public override string ToString() => Explanation;
    }

    /// <summary>
    /// There is no data to answer from: no pack is selected, or the pack has no table for this
    /// wall. An honest "napkin cannot say", never a guess.
    /// </summary>
    public sealed record NoData(NoDataReason Reason, AdoptedCodeRef? Code, WallKind Kind, string Explanation) : HeaderResult
    {
        /// <inheritdoc/>
        public override string ToString() => Explanation;
    }
}
