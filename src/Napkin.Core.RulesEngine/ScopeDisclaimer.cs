namespace Napkin.Core.RulesEngine;

/// <summary>
/// napkin's scope disclaimer (DESIGN.md §7): one sentence, said the same way wherever it is said —
/// under every assistant answer (<c>ContextPack.Disclaimer</c>) and in the title block of every
/// exported sheet (#25).
/// </summary>
public static class ScopeDisclaimer
{
    /// <summary>The sentence, in full.</summary>
    public const string Text =
        "napkin applies published prescriptive tables and is not a substitute for a licensed engineer, a permit office's review, "
        + "or professional judgment about site-specific conditions the tables don't cover.";
}
