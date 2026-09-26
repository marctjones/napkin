using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>Which date decides that an adopted code governs a permit (design §1.1).</summary>
public enum AppliesTo
{
    /// <summary>The code in force on the date the permit application was made.</summary>
    PermitApplicationDate,

    /// <summary>The code in force on the date the permit was issued.</summary>
    PermitIssuanceDate,

    /// <summary>A transition rule that is not a date window; <see cref="Adoption.TransitionNotes"/> holds it.</summary>
    SeeNotes,
}

/// <summary>Where a pack is in the two-role transcribe-and-review process (design §8.3).</summary>
public enum ReviewStatus
{
    /// <summary>Transcribed, not yet reviewed against the source. Results carry an UNREVIEWED label.</summary>
    Unreviewed,

    /// <summary>A reviewer is checking it against the source.</summary>
    InReview,

    /// <summary>An independent reviewer checked every row against the source.</summary>
    SignedOff,
}

/// <summary>Which layer of a composed pack produced a row (design §1.3, §2).</summary>
public enum CitationLayer
{
    /// <summary>The model-code base layer, as adopted by the pack.</summary>
    ModelCode,

    /// <summary>A state amendment overlay.</summary>
    StateAmendment,

    /// <summary>A municipal amendment overlay.</summary>
    MunicipalAmendment,
}

/// <summary>What a wall is, which selects the header table (design §3.2).</summary>
public enum WallKind
{
    /// <summary>An exterior bearing wall.</summary>
    ExteriorBearing,

    /// <summary>An interior bearing wall.</summary>
    InteriorBearing,
}

/// <summary>A table column's value type (design §1.5).</summary>
public enum ColumnType
{
    /// <summary>A category from the column's declared <c>values</c>.</summary>
    Enum,

    /// <summary>A load in whole pounds per square foot.</summary>
    Psf,

    /// <summary>A wind speed in whole miles per hour.</summary>
    Mph,

    /// <summary>An exact <see cref="Geometry.Length"/>.</summary>
    Length,
}

/// <summary>How an input value selects rows (design §4.1).</summary>
public enum BandKind
{
    /// <summary>The input equals the row's value.</summary>
    Exact,

    /// <summary>The row covers every input up to and including its bound; the smallest bound that still covers the input wins.</summary>
    UpperBound,

    /// <summary>The row's result serves every demand up to its capacity; the smallest capacity that still serves the demand wins.</summary>
    Capacity,
}

/// <summary>How a footnote is encoded (design §1.4). A footnote without one makes its table invalid.</summary>
public enum FootnoteEncoding
{
    /// <summary>Shown with the result; no logic implements it.</summary>
    NotEncoded,

    /// <summary>Its effect is already expressed in the rows.</summary>
    AsRows,

    /// <summary>It narrows the table's scope; an excluded request is out of scope, citing it.</summary>
    AsLimit,

    /// <summary>
    /// It declares operations the engine applies exactly as written (<see cref="FootnoteOperation"/>):
    /// substituting an input, or interpolating between two declared columns. The engine applies
    /// them only where a footnote declares them (design §4.4, decided 2026-09-25).
    /// </summary>
    AsOperations,
}

/// <summary>Whether a footnote applies to the whole table or only to the rows that list it.</summary>
public enum FootnoteScope
{
    /// <summary>Applies to every row of the table.</summary>
    Table,

    /// <summary>Applies only to rows whose <c>footnotes</c> list names it.</summary>
    Rows,
}

/// <summary>The jurisdiction that adopted a code.</summary>
public sealed record Jurisdiction(string Country, string State, string? Municipality);

/// <summary>The adoption a pack encodes: its name, in-force window and transition rule (design §1.1).</summary>
public sealed record Adoption(
    string Name,
    string ShortName,
    string AdoptedBy,
    DateOnly InForceFrom,
    DateOnly? InForceTo,
    AppliesTo AppliesTo,
    string? TransitionNotes);

/// <summary>The model code an adoption incorporates. Descriptive, not identity.</summary>
public sealed record BaseCode(string Publisher, string Code, int Year)
{
    /// <summary>How a citation prints the model code: "IRC 2021".</summary>
    public override string ToString() => $"{Code} {Year}";
}

/// <summary>A document a pack's rows were read from, with its printing/errata state and hash.</summary>
public sealed record SourceDocument(
    string Id,
    string Title,
    string Publisher,
    string Url,
    string Printing,
    DateOnly RetrievedOn,
    string Sha256);

/// <summary>The pack's review state (design §8.3).</summary>
public sealed record PackReview(ReviewStatus Status, string? Checklist);

/// <summary>The contents of a pack's <c>pack.json</c> (design §1.6).</summary>
public sealed record PackManifest(
    string Id,
    int Revision,
    Jurisdiction Jurisdiction,
    Adoption Adoption,
    BaseCode BaseCode,
    ValueList<string> Layers,
    ValueList<SourceDocument> Sources,
    PackReview Review);

/// <summary>
/// One value in a table cell or a request: a category, a whole number of psf or mph, or an exact
/// length in 1/1024″ units. Never a floating-point number (design §1.5).
/// </summary>
/// <param name="Type">The column type.</param>
/// <param name="Symbol">The category, for <see cref="ColumnType.Enum"/>; otherwise null.</param>
/// <param name="Magnitude">The whole number, or the length's units; 0 for a category.</param>
public readonly record struct CellValue(ColumnType Type, string? Symbol, long Magnitude)
{
    /// <summary>A category.</summary>
    public static CellValue Category(string symbol) => new(ColumnType.Enum, symbol, 0);

    /// <summary>A whole number of psf or mph.</summary>
    public static CellValue Whole(ColumnType type, int value) => new(type, null, value);

    /// <summary>An exact length.</summary>
    public static CellValue Of(Length length) => new(ColumnType.Length, null, length.Units);

    /// <summary>How a citation or trace prints the value: "35 psf", "6'-0\"", "roof-ceiling".</summary>
    public override string ToString() => Type switch
    {
        ColumnType.Enum => Symbol ?? string.Empty,
        ColumnType.Psf => $"{Magnitude} psf",
        ColumnType.Mph => $"{Magnitude} mph",
        _ => new Length(Magnitude).Format(new FeetInchesFormat((int)Length.UnitsPerInch)).Text,
    };
}

/// <summary>The range a banded column declares it covers (design §4.3).</summary>
public sealed record ColumnDomain(CellValue Min, CellValue Max);

/// <summary>A table input column: its name, type, band semantics, categories and domain.</summary>
public sealed record InputColumn(
    string Name,
    ColumnType Type,
    BandKind Band,
    ValueList<string> Values,
    ColumnDomain? Domain);

/// <summary>
/// What an <see cref="FootnoteEncoding.AsLimit"/> footnote excludes: an input above a value, or an
/// input equal to a category.
/// </summary>
public sealed record FootnoteLimit(string Input, CellValue? Above, string? EqualTo);

/// <summary>A footnote, transcribed verbatim, with its classification (design §1.4).</summary>
/// <param name="Id">The footnote's letter or number as printed.</param>
/// <param name="Text">The footnote, verbatim.</param>
/// <param name="EncodedAs">How it is encoded.</param>
/// <param name="AppliesTo">The whole table, or only the rows that list it.</param>
/// <param name="Limit">What an <see cref="FootnoteEncoding.AsLimit"/> footnote excludes; otherwise null.</param>
/// <param name="Operations">What an <see cref="FootnoteEncoding.AsOperations"/> footnote does; otherwise empty.</param>
/// <param name="Source">Where an overlay's amended footnote was read; null for the table's own source.</param>
public sealed record Footnote(
    string Id,
    string Text,
    FootnoteEncoding EncodedAs,
    FootnoteScope AppliesTo,
    FootnoteLimit? Limit,
    ValueList<FootnoteOperation> Operations,
    SourceRef? Source);

/// <summary>
/// An operation a footnote declares (<see cref="FootnoteEncoding.AsOperations"/>). A closed set: the
/// engine does exactly these and nothing a footnote does not declare.
/// </summary>
public abstract record FootnoteOperation
{
    private FootnoteOperation()
    {
    }

    /// <summary>
    /// "Use <paramref name="Use"/> for <paramref name="Input"/> less than <paramref name="Below"/>
    /// when <paramref name="WhenInput"/> is at most <paramref name="AtMost"/>". When the condition
    /// fails the request is out of scope citing the footnote; when the condition's input is not
    /// entered, it is missing. Never applied to an input at or above <paramref name="Below"/>.
    /// </summary>
    public sealed record SubstituteInput(string Input, CellValue Below, CellValue Use, string WhenInput, CellValue AtMost) : FootnoteOperation;

    /// <summary>
    /// "Linear interpolation is permitted" for <paramref name="Input"/> strictly between the two
    /// declared columns <paramref name="Lower"/> and <paramref name="Upper"/>, of the capacity
    /// column <paramref name="Quantity"/> only. Count columns are never interpolated.
    /// </summary>
    public sealed record Interpolate(string Input, CellValue Lower, CellValue Upper, string Quantity) : FootnoteOperation;
}

/// <summary>A header member: plies and nominal size, e.g. (2) 2x10.</summary>
public sealed record MemberSpec(int Plies, string Nominal)
{
    /// <summary>"(2) 2x10".</summary>
    public override string ToString() => $"({Plies}) {Nominal}";
}

/// <summary>Where in which document a row, operation or table was read (design §2).</summary>
public sealed record SourceRef(
    string SourceId,
    string Title,
    string Location,
    string Url,
    string Printing,
    DateOnly RetrievedOn,
    string Sha256);

/// <summary>A composed header-sizing row: its inputs, outputs, footnotes and provenance.</summary>
public sealed record HeaderRow(
    string Id,
    ImmutableSortedDictionary<string, CellValue> Inputs,
    MemberSpec Header,
    int JackStuds,
    int KingStuds,
    ValueList<string> Footnotes,
    CitationLayer Layer,
    SourceRef Source);

/// <summary>A composed, validated header-sizing table (design §1.4).</summary>
public sealed record HeaderSizingTable(
    string Designation,
    string Title,
    WallKind WallKind,
    CitationLayer Layer,
    SourceRef Source,
    ValueList<InputColumn> Inputs,
    ValueList<Footnote> Footnotes,
    ValueList<HeaderRow> Rows)
{
    /// <summary>The site and request inputs this table needs: its columns plus any input a limit footnote tests.</summary>
    public IEnumerable<string> RequiredInputs
        => Inputs.Select(i => i.Name)
            .Concat(Footnotes.Where(f => f.Limit is not null).Select(f => f.Limit!.Input))
            .Distinct(StringComparer.Ordinal);
}

/// <summary>
/// An overlay's footnote amendment to a table the layer below does not have yet (its base table is
/// not filled in). Kept, listed, and applied the moment the table is added; never silently dropped.
/// </summary>
/// <param name="Table">The table designation the amendment names.</param>
/// <param name="FootnoteId">The footnote it amends.</param>
/// <param name="File">The overlay file.</param>
/// <param name="Source">Where the amendment was read.</param>
public sealed record PendingAmendment(string Table, string FootnoteId, string File, SourceRef Source);

/// <summary>A pack that loaded, validated and composed. The only way pack data enters memory (design §9.1).</summary>
/// <param name="Manifest">The pack's manifest.</param>
/// <param name="Tables">The composed header tables.</param>
/// <param name="Pending">Footnote amendments waiting for a base table that is not loaded.</param>
/// <param name="Bracing">The base layer's wall-bracing provisions, or null when it has none (docs/rules-engine.md).</param>
public sealed record LoadedPack(
    PackManifest Manifest,
    ValueList<HeaderSizingTable> Tables,
    ValueList<PendingAmendment> Pending,
    BracingProvisions? Bracing = null)
{
    /// <summary>The status label shown in the pack picker when no header table is loaded (the base layer is unfilled).</summary>
    public const string BaseTablesNotLoaded = "base tables not loaded";

    /// <summary>The per-municipality site values the pack carries (#210), or null.</summary>
    public SiteValuesTable? Site { get; init; }

    /// <summary>Whether this pack can size any header at all.</summary>
    public bool HasHeaderTables => Tables.Count > 0;

    /// <summary>A short status for the UI: empty when the pack has any data, else <see cref="BaseTablesNotLoaded"/>.</summary>
    public string StatusLabel => HasHeaderTables || Bracing is not null ? string.Empty : BaseTablesNotLoaded;

    /// <summary>The identity a citation prints.</summary>
    public AdoptedCodeRef Code => new(
        Manifest.Id,
        Manifest.Revision,
        Manifest.Adoption.ShortName,
        Manifest.BaseCode.ToString(),
        Manifest.Review.Status);
}
