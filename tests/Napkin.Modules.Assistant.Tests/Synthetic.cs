using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// SYNTHETIC TEST DATA - NOT CODE VALUES. Invented codes, tables and rows, shaped like the rules
/// engine's results, for checking that the pack carries a result's own text; nothing here was read
/// from, or stands for, any building code.
/// </summary>
internal static class Synthetic
{
    public static AdoptedCodeRef Code { get; } = new("us-zz-frame", 1, "ZZ FRAME", "IRC 2099", ReviewStatus.Unreviewed);

    public static SourceRef Source { get; } = new(
        "zz-synthetic", "ZZ synthetic source", "p. 1", "https://example.invalid/zz", "synthetic printing", new DateOnly(2026, 9, 26), "none");

    public static Citation Citation(AdoptedCodeRef? code = null, ValueList<BandMatch>? trace = null, ValueList<FootnoteRef>? footnotes = null)
        => new(
            code ?? Code,
            "ZZ-HEADER",
            "r.zz.a",
            "supports zz-roof, zz band a",
            CitationLayer.ModelCode,
            Source,
            footnotes ?? ValueList<FootnoteRef>.Empty,
            trace ?? ValueList<BandMatch>.Empty);
}
