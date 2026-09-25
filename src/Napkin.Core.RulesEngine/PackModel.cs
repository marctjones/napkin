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
public sealed record Footnote(
    string Id,
    string Text,
    FootnoteEncoding EncodedAs,
    FootnoteScope AppliesTo,
    FootnoteLimit? Limit);

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

/// <summary>A pack that loaded, validated and composed. The only way pack data enters memory (design §9.1).</summary>
public sealed record LoadedPack(PackManifest Manifest, ValueList<HeaderSizingTable> Tables)
{
    /// <summary>The identity a citation prints.</summary>
    public AdoptedCodeRef Code => new(
        Manifest.Id,
        Manifest.Revision,
        Manifest.Adoption.ShortName,
        Manifest.BaseCode.ToString(),
        Manifest.Review.Status);
}
