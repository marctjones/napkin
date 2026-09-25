using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// The adopted code a result was computed under: pack id and revision, the name a person knows it
/// by ("CT 2022"), the model code it incorporates ("IRC 2021") and its review state.
/// </summary>
public sealed record AdoptedCodeRef(
    string PackId,
    int Revision,
    string ShortName,
    string BaseCode,
    ReviewStatus Review)
{
    /// <summary>"CT 2022 (IRC 2021), pack us-ct-2022 rev 1", plus "UNREVIEWED" until signed off (design §13 Decision 5).</summary>
    public override string ToString()
        => $"{ShortName} ({BaseCode}), pack {PackId} rev {Revision}"
           + (Review == ReviewStatus.SignedOff ? string.Empty : " — UNREVIEWED: values not yet checked against the source");
}

/// <summary>How one input landed in its band: the "show your work" line (design §2, §4.2).</summary>
/// <param name="Column">The table column.</param>
/// <param name="Input">The value the request carried, as printed.</param>
/// <param name="Band">The band chosen, as printed ("≤ 99 psf", "= test-roof").</param>
public sealed record BandMatch(string Column, string Input, string Band)
{
    /// <summary>"groundSnowLoad 35 psf → ≤ 50 psf".</summary>
    public override string ToString() => $"{Column} {Input} → {Band}";
}

/// <summary>A footnote shown with a result: its id, verbatim text, encoding and where it was read.</summary>
public sealed record FootnoteRef(string Id, string Text, FootnoteEncoding EncodedAs, SourceRef Source);

/// <summary>
/// How an interpolated span was computed (design §4.4, decided 2026-09-25): both rows used, the
/// exact weight, the exact span and the span shown, and the footnote that permits it.
/// </summary>
/// <param name="Column">The interpolated column ("groundSnowLoad").</param>
/// <param name="Input">The request's value in that column.</param>
/// <param name="Lower">The lower declared column.</param>
/// <param name="Upper">The upper declared column.</param>
/// <param name="LowerRowId">The row used at the lower column.</param>
/// <param name="UpperRowId">The row used at the upper column.</param>
/// <param name="LowerSpan">The lower row's span.</param>
/// <param name="UpperSpan">The upper row's span.</param>
/// <param name="Weight">(input − lower) / (upper − lower), exact.</param>
/// <param name="SpanUnits">The interpolated span in 1/1024″ units, exact; the opening is compared to this.</param>
/// <param name="SpanShown">The interpolated span rounded down to a whole 1/16″, for display only.</param>
/// <param name="Footnote">The footnote that permits the interpolation.</param>
public sealed record InterpolationTrace(
    string Column,
    CellValue Input,
    CellValue Lower,
    CellValue Upper,
    string LowerRowId,
    string UpperRowId,
    Length LowerSpan,
    Length UpperSpan,
    ExactFraction Weight,
    ExactFraction SpanUnits,
    Length SpanShown,
    FootnoteRef Footnote)
{
    /// <summary>The one-line summary a person reads under the result.</summary>
    public string Summary(AdoptedCodeRef code)
        => $"Interpolated between the {Lower} row ({LowerRowId}) and the {Upper} row ({UpperRowId}) ({code.ShortName} footnote {Footnote.Id}, {Footnote.Source.Location})";

    /// <summary>The full working: both rows, the weight as an exact fraction, the exact and shown span, the footnote verbatim and its source.</summary>
    public override string ToString()
        => $"{Column} {Input} is between the {Lower} and {Upper} columns: row {LowerRowId} ({Show(LowerSpan)}) and row {UpperRowId} ({Show(UpperSpan)}), "
           + $"weight ({Input.Magnitude} − {Lower.Magnitude}) / ({Upper.Magnitude} − {Lower.Magnitude}) = {Weight}; "
           + $"span {Show(LowerSpan)} + ({Show(UpperSpan)} − {Show(LowerSpan)}) × {Weight} = {SpanUnits} × 1/1024\", shown rounded down to {Show(SpanShown)}; "
           + $"footnote {Footnote.Id}: \"{Footnote.Text}\" — {Footnote.Source.Title}, {Footnote.Source.Location}";

    private static string Show(Length length) => CellValue.Of(length).ToString();
}

/// <summary>
/// Everything needed to find a result's row in the paper source without the app (design §2).
/// Every result the engine returns carries one.
/// </summary>
/// <param name="Code">The adopted code (edition).</param>
/// <param name="Table">The table designation as printed.</param>
/// <param name="RowId">
/// The pack-stable row id. Null only when the limit is the table itself — a category the table
/// declares no rows for, or a footnote that applies to the whole table.
/// </param>
/// <param name="RowLabel">What the row (or limit) covers, in words built from its inputs.</param>
/// <param name="Layer">Which layer produced the row.</param>
/// <param name="Source">Which document, where in it, which printing, retrieved when.</param>
/// <param name="Footnotes">The footnotes that apply to the row.</param>
/// <param name="Trace">How each input landed in its band.</param>
/// <param name="Interpolation">How an interpolated span was computed; null for a plain lookup.</param>
/// <param name="IsSection">Whether <paramref name="Table"/> names a section (a bracing method) rather than a table.</param>
public sealed record Citation(
    AdoptedCodeRef Code,
    string Table,
    string? RowId,
    string RowLabel,
    CitationLayer Layer,
    SourceRef Source,
    ValueList<FootnoteRef> Footnotes,
    ValueList<BandMatch> Trace,
    InterpolationTrace? Interpolation = null,
    bool IsSection = false)
{
    /// <summary>"IRC 2021 Table X row Y, as adopted by CT 2022 — source, location".</summary>
    public override string ToString()
    {
        string what = IsSection ? "Section" : "Table";
        string layer = Layer switch
        {
            CitationLayer.ModelCode => $"{Code.BaseCode} {what} {Table}, as adopted by {Code.ShortName}",
            CitationLayer.StateAmendment => $"{Code.ShortName} state amendment to {what} {Table}",
            _ => $"{Code.ShortName} municipal amendment to {what} {Table}",
        };
        string row = RowId is null ? string.Empty : $" row {RowId}";
        return $"{layer}{row} ({RowLabel}); {Source.Title}, {Source.Printing}, {Source.Location}";
    }
}
