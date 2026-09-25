using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// When a bracing factor or limit applies: an input above a value, or an input equal to a category
/// (docs/rules-engine.md, "Wall bracing").
/// </summary>
/// <param name="Input">The input tested.</param>
/// <param name="Above">Applies when the input is strictly above this; null for a category test.</param>
/// <param name="EqualTo">Applies when the input equals this category; null for a numeric test.</param>
public sealed record BracingCondition(string Input, CellValue? Above, string? EqualTo)
{
    /// <summary>"seismicDesignCategory = C", "wallHeight above 10'-0\"".</summary>
    public override string ToString() => Above is { } above ? $"{Input} above {above}" : $"{Input} = {EqualTo}";

    /// <summary>Whether the condition holds for this value (exact comparison).</summary>
    public bool Holds(CellValue value) => Above is { } above ? value.Magnitude > above.Magnitude : value.Symbol == EqualTo;
}

/// <summary>
/// An adjustment of the required braced length, selected by a condition and cited to its own
/// section: either a multiplier (an exact fraction) or a length added after scaling.
/// </summary>
/// <param name="Id">The pack-stable id.</param>
/// <param name="Section">The section or table the factor is printed in.</param>
/// <param name="When">When it applies.</param>
/// <param name="Multiply">The multiplier, or null for an added length.</param>
/// <param name="Add">The added length, or null for a multiplier.</param>
/// <param name="Source">Where it was read.</param>
public sealed record BracingFactor(string Id, string Section, BracingCondition When, ExactFraction? Multiply, Length? Add, SourceRef Source)
{
    /// <summary>"× 3/2" or "+ 1'-0\"".</summary>
    public string Effect => Multiply is { } m ? $"× {m}" : $"+ {CellValue.Of(Add!.Value)}";
}

/// <summary>A limit of the method's scope: when its condition holds, the wall line is out of scope, citing it.</summary>
/// <param name="Id">The pack-stable id.</param>
/// <param name="Section">The section the limit is printed in.</param>
/// <param name="When">When the line is out of scope.</param>
/// <param name="Text">The limit, verbatim.</param>
/// <param name="Source">Where it was read.</param>
public sealed record BracingLimit(string Id, string Section, BracingCondition When, string Text, SourceRef Source);

/// <summary>One base row: the required braced length per unit length of wall line, for its band of inputs.</summary>
/// <param name="Id">The pack-stable row id.</param>
/// <param name="Inputs">The row's value in each input column.</param>
/// <param name="Length">The required braced length per <see cref="BracingProvisions.UnitLength"/>.</param>
/// <param name="Source">Where it was read.</param>
public sealed record BracingRequiredRow(string Id, ImmutableSortedDictionary<string, CellValue> Inputs, Length Length, SourceRef Source);

/// <summary>A method's minimum panel length for walls up to a height.</summary>
/// <param name="Id">The pack-stable row id.</param>
/// <param name="WallHeight">The upper bound of wall height this row covers.</param>
/// <param name="Length">The shortest segment that counts at all.</param>
/// <param name="Source">Where it was read.</param>
public sealed record MinimumPanelRow(string Id, Length WallHeight, Length Length, SourceRef Source);

/// <summary>
/// A bracing method a segment can be assigned: its minimum panel length by wall height, and the
/// most one segment may contribute (null for no cap).
/// </summary>
/// <param name="Id">The pack-stable id stored in the project for an assignment.</param>
/// <param name="Name">What a person reads in the picker.</param>
/// <param name="Section">The section the method is printed in.</param>
/// <param name="HeightDomain">The wall heights the minimum-panel rows cover.</param>
/// <param name="MinimumPanel">The minimum-panel rows, by wall height, ascending.</param>
/// <param name="Cap">The most one segment contributes; null for no cap.</param>
/// <param name="Source">Where it was read.</param>
public sealed record BracingMethod(
    string Id,
    string Name,
    string Section,
    ColumnDomain HeightDomain,
    ValueList<MinimumPanelRow> MinimumPanel,
    Length? Cap,
    SourceRef Source);

/// <summary>
/// A pack's wall-bracing provisions (docs/rules-engine.md, "Wall bracing"): everything is data.
/// Required = (base row × line length / unit length × every applicable multiplier + every
/// applicable added length), rounded UP to <see cref="Step"/>. A segment contributes
/// min(length, cap) rounded DOWN to <see cref="Step"/>, or nothing when it is shorter than its
/// method's minimum panel length for the wall height, or when it has no method.
/// </summary>
/// <param name="Section">The section applied, as printed.</param>
/// <param name="Title">Its title.</param>
/// <param name="Layer">The layer that produced it (the base layer; overlays cannot amend it yet).</param>
/// <param name="Source">Where it was read.</param>
/// <param name="Step">The rounding step for required and provided lengths.</param>
/// <param name="UnitLength">The wall-line length a base row's length is given per.</param>
/// <param name="Inputs">The base rows' input columns (exact and upper-bound).</param>
/// <param name="Required">The base rows.</param>
/// <param name="Factors">The adjustment factors.</param>
/// <param name="Limits">The scope limits.</param>
/// <param name="Methods">The methods a segment can be assigned.</param>
/// <param name="Footnotes">The section's footnotes, shown with results.</param>
public sealed record BracingProvisions(
    string Section,
    string Title,
    CitationLayer Layer,
    SourceRef Source,
    Length Step,
    Length UnitLength,
    ValueList<InputColumn> Inputs,
    ValueList<BracingRequiredRow> Required,
    ValueList<BracingFactor> Factors,
    ValueList<BracingLimit> Limits,
    ValueList<BracingMethod> Methods,
    ValueList<Footnote> Footnotes)
{
    /// <summary>The site inputs a check needs: the columns and every condition's input, except the wall height the wall supplies.</summary>
    public IEnumerable<string> RequiredInputs
        => Inputs.Select(i => i.Name)
            .Concat(Factors.Select(f => f.When.Input))
            .Concat(Limits.Select(l => l.When.Input))
            .Distinct(StringComparer.Ordinal);

    /// <summary>The method with this id, or null.</summary>
    public BracingMethod? Method(string id) => Methods.FirstOrDefault(m => m.Id == id);
}

/// <summary>One solid segment of a wall line, between its openings and ends, and the method assigned to it.</summary>
public sealed record BracedSegment
{
    /// <summary>Builds a segment.</summary>
    /// <param name="label">How a person names it ("from the wall's start to Window 1").</param>
    /// <param name="length">Its length, derived from the drawing.</param>
    /// <param name="method">The method id assigned, or null: not braced. Never defaulted.</param>
    /// <exception cref="ArgumentException">A non-positive length: a caller bug.</exception>
    public BracedSegment(string label, Length length, string? method)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (length <= Length.Zero)
        {
            throw new ArgumentException("A segment is longer than zero.", nameof(length));
        }

        Label = label;
        Length = length;
        Method = method;
    }

    /// <summary>How a person names it.</summary>
    public string Label { get; }

    /// <summary>Its length.</summary>
    public Length Length { get; }

    /// <summary>The method id, or null when none is assigned.</summary>
    public string? Method { get; }
}

/// <summary>A wall line as the bracing check reads it: its length, the wall's height, and its segments in order.</summary>
public sealed record BracedWallLine
{
    /// <summary>Builds a line.</summary>
    /// <exception cref="ArgumentException">A non-positive length or height, or segments longer than the line together.</exception>
    public BracedWallLine(Length length, Length wallHeight, ValueList<BracedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (length <= Length.Zero || wallHeight <= Length.Zero)
        {
            throw new ArgumentException("A wall line has a positive length and height.", nameof(length));
        }

        if (segments.Aggregate(Length.Zero, (sum, s) => sum + s.Length) > length)
        {
            throw new ArgumentException("A wall line's segments are no longer than the line together.", nameof(segments));
        }

        Length = length;
        WallHeight = wallHeight;
        Segments = segments;
    }

    /// <summary>The line's whole length, openings included.</summary>
    public Length Length { get; }

    /// <summary>The wall's height.</summary>
    public Length WallHeight { get; }

    /// <summary>The solid segments, start to end.</summary>
    public ValueList<BracedSegment> Segments { get; }
}

/// <summary>A question for the bracing provisions: a wall line and the site (design §3.3).</summary>
/// <param name="Line">The wall line.</param>
/// <param name="Site">The project's site inputs.</param>
public sealed record BracingRequest(BracedWallLine Line, SiteInputs Site);

/// <summary>What one segment contributed, and why.</summary>
/// <param name="Label">The segment, as a person names it.</param>
/// <param name="Length">Its length.</param>
/// <param name="Method">The method assigned, or null.</param>
/// <param name="Contribution">What it adds to the braced length (rounded down to the step).</param>
/// <param name="Why">Why: "not braced: no method assigned", "shorter than … minimum panel … (row m.h8)", "min(…, cap …) rounded down to …".</param>
public sealed record SegmentContribution(string Label, Length Length, string? Method, Length Contribution, string Why)
{
    /// <summary>Why a segment whose method is another code's counts for nothing: "method 'zz-board' is not one of ZZ BRACE B's methods, …".</summary>
    /// <param name="method">The method's id.</param>
    /// <param name="shortName">The adopted code's short name.</param>
    public static string UnknownMethodWhy(string method, string shortName)
        => $"method '{method}' is not one of {shortName}'s methods, so it counts for nothing: assign one of this code's methods";

    /// <inheritdoc/>
    public override string ToString() => $"{Label}, {CellValue.Of(Length)}: {CellValue.Of(Contribution)} ({Why})";
}

/// <summary>A factor that applied, with the condition that selected it and its source.</summary>
/// <param name="Id">The factor's id.</param>
/// <param name="Section">Its section.</param>
/// <param name="Effect">"× 3/2" or "+ 1'-0\"".</param>
/// <param name="Condition">The condition that held.</param>
/// <param name="Location">Where in the source.</param>
public sealed record FactorApplied(string Id, string Section, string Effect, string Condition, string Location)
{
    /// <inheritdoc/>
    public override string ToString() => $"factor {Id} {Effect} because {Condition} ({Section}, {Location})";
}

/// <summary>
/// The whole working of a bracing check: the base row, each factor with its source, the exact
/// required length and its rounding, and every segment's contribution with why.
/// </summary>
/// <param name="BaseRowId">The base row.</param>
/// <param name="BaseLength">Its required length per unit.</param>
/// <param name="UnitLength">The unit it is per.</param>
/// <param name="LineLength">The wall line's length.</param>
/// <param name="Factors">The factors that applied, in the pack's order.</param>
/// <param name="RequiredExact">The required length before rounding, in 1/1024″ units, exact.</param>
/// <param name="Step">The rounding step.</param>
/// <param name="Required">The required length, rounded up.</param>
/// <param name="Segments">Every segment, in order.</param>
public sealed record BracingWorking(
    string BaseRowId,
    Length BaseLength,
    Length UnitLength,
    Length LineLength,
    ValueList<FactorApplied> Factors,
    ExactFraction RequiredExact,
    Length Step,
    Length Required,
    ValueList<SegmentContribution> Segments)
{
    /// <summary>The working, one step per line.</summary>
    public IEnumerable<string> Lines
    {
        get
        {
            yield return $"Base: row {BaseRowId}, {Show(BaseLength)} per {Show(UnitLength)} of wall line; the line is {Show(LineLength)}.";
            foreach (FactorApplied factor in Factors)
            {
                yield return $"Applied {factor}.";
            }

            yield return $"Required: {RequiredExact} × 1/1024\" exactly, rounded up to the {Show(Step)} step: {Show(Required)}.";
            foreach (SegmentContribution segment in Segments)
            {
                yield return $"Segment {segment}.";
            }
        }
    }

    /// <inheritdoc/>
    public override string ToString() => string.Join(Environment.NewLine, Lines);

    private static string Show(Length length) => CellValue.Of(length).ToString();
}

/// <summary>Why a bracing check has no answer from data at all.</summary>
public enum BracingNoDataReason
{
    /// <summary>The project has no adopted code selected, or its pack is unavailable.</summary>
    NoPackSelected,

    /// <summary>The loaded pack has no wall-bracing provisions (for example, they are not transcribed yet).</summary>
    NoBracingProvisions,
}

/// <summary>
/// The answer to a wall line's bracing question (design §3.3): a closed set of named, expected
/// results with no catch-all. Fails is not an error: it says what to add.
/// </summary>
public abstract record BracingResult
{
    private BracingResult()
    {
    }

    /// <summary>The line provides at least the required braced length.</summary>
    public sealed record Passes(Length Required, Length Provided, Citation Citation, BracingWorking Working) : BracingResult
    {
        /// <inheritdoc/>
        public override string ToString() => $"passes: braced {CellValue.Of(Provided)} of {CellValue.Of(Required)} required — {Citation}";
    }

    /// <summary>The line provides less than required, short by <paramref name="Shortfall"/>; the citation names the section applied.</summary>
    public sealed record Fails(Length Required, Length Provided, Length Shortfall, Citation Citation, BracingWorking Working) : BracingResult
    {
        /// <inheritdoc/>
        public override string ToString() => $"SHORT by {CellValue.Of(Shortfall)}: braced {CellValue.Of(Provided)} of {CellValue.Of(Required)} required — {Citation}";
    }

    /// <summary>The provisions' own limits exclude this line: which, and its citation. Never a number.</summary>
    public sealed record OutOfScope(OutOfScopeReason Reason, Citation Limit, string Explanation) : BracingResult
    {
        /// <inheritdoc/>
        public override string ToString() => $"out of scope ({Reason}): {Explanation} — limit: {Limit}";
    }

    /// <summary>A site value the provisions need has not been entered; nothing was defaulted.</summary>
    public sealed record InputMissing(ValueList<string> Inputs, string Section, AdoptedCodeRef Code, string Explanation) : BracingResult
    {
        /// <inheritdoc/>
        public override string ToString() => Explanation;
    }

    /// <summary>There is no data to answer from: no pack, or a pack without bracing provisions.</summary>
    public sealed record NoData(BracingNoDataReason Reason, AdoptedCodeRef? Code, string Explanation) : BracingResult
    {
        /// <summary>The explanation when the loaded pack has no wall-bracing provisions: "The loaded pack CT 2022 has no wall-bracing provisions, …".</summary>
        /// <param name="shortName">The code's short name.</param>
        public static string NoProvisionsExplanation(string shortName)
            => $"The loaded pack {shortName} has no wall-bracing provisions, so napkin cannot check this wall line's bracing. "
               + "Nothing is guessed: add them to the pack directory from your copy of the code (docs/rules-engine.md).";

        /// <inheritdoc/>
        public override string ToString() => Explanation;
    }
}
