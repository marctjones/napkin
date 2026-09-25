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

/// <summary>A footnote shown with a result.</summary>
public sealed record FootnoteRef(string Id, string Text, FootnoteEncoding EncodedAs);

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
public sealed record Citation(
    AdoptedCodeRef Code,
    string Table,
    string? RowId,
    string RowLabel,
    CitationLayer Layer,
    SourceRef Source,
    ValueList<FootnoteRef> Footnotes,
    ValueList<BandMatch> Trace)
{
    /// <summary>"IRC 2021 Table X row Y, as adopted by CT 2022 — source, location".</summary>
    public override string ToString()
    {
        string layer = Layer switch
        {
            CitationLayer.ModelCode => $"{Code.BaseCode} Table {Table}, as adopted by {Code.ShortName}",
            CitationLayer.StateAmendment => $"{Code.ShortName} state amendment to Table {Table}",
            _ => $"{Code.ShortName} municipal amendment to Table {Table}",
        };
        string row = RowId is null ? string.Empty : $" row {RowId}";
        return $"{layer}{row} ({RowLabel}); {Source.Title}, {Source.Printing}, {Source.Location}";
    }
}
