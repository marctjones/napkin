using System.Collections.Immutable;

using Napkin.Modules.Building;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Assistant;

/// <summary>What one item of a context pack is (docs/design/llm-assistant.md §3).</summary>
public enum ContextKind
{
    /// <summary>A selected entity, in words: first in the pack.</summary>
    Selection,

    /// <summary>Any other entity in the design, in words.</summary>
    Entity,

    /// <summary>A relationship, as the relationship list says it.</summary>
    Relationship,

    /// <summary>The site values as entered.</summary>
    Site,

    /// <summary>The adopted code and its status: the choice, not the pack's contents.</summary>
    Code,

    /// <summary>A check result, in the engine's own words.</summary>
    Check,

    /// <summary>One line of an open list, as napkin writes it to CSV.</summary>
    ListRow,

    /// <summary>What was cut from an open list for the budget: "… 40 more rows not shown; napkin's list has 63".</summary>
    ListCut,

    /// <summary>A section of napkin's help.</summary>
    Help,

    /// <summary>napkin's disclaimer: always the last item.</summary>
    Disclaimer,
}

/// <summary>One numbered item of a context pack.</summary>
/// <param name="N">Its number, from 1; the model refers to it as <c>[N]</c>.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Text">What it says, as napkin says it elsewhere.</param>
public sealed record ContextItem(int N, ContextKind Kind, string Text)
{
    /// <summary>"[5] Header check, Window 1: …", as the pack renders it and the note shows it under an answer.</summary>
    public override string ToString() => $"[{N}] {Text}";
}

/// <summary>
/// A list window that is open, as the CSV napkin already writes for it (§3.2): its lines go into the
/// pack one item each, header lines included.
/// </summary>
/// <param name="Title">What the list is called: "Cut list", "Shopping list".</param>
/// <param name="Csv">The list exactly as its CSV export writes it.</param>
/// <param name="HeaderLines">How many lines at the top are not rows: the statement and the column names.</param>
public sealed record OpenList(string Title, string Csv, int HeaderLines = 2)
{
    /// <summary>The list's lines, empty ones dropped.</summary>
    public ImmutableArray<string> Lines => [.. Csv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(line => line.Length > 0)];

    /// <summary>How many rows the list has: its lines after the header lines.</summary>
    public int Rows => Math.Max(0, Lines.Length - HeaderLines);

    /// <summary>The cut list as <see cref="CutListCsv"/> writes it.</summary>
    /// <param name="rows">The rows, in the order on screen.</param>
    public static OpenList CutList(IEnumerable<CutListRow> rows) => new("Cut list", CutListCsv.ToCsv(rows));

    /// <summary>The shopping list as <see cref="ShoppingListCsv"/> writes it.</summary>
    /// <param name="rows">The rows, in the order on screen.</param>
    /// <param name="kerf">The kerf it was planned with; napkin's default when omitted.</param>
    public static OpenList ShoppingList(IEnumerable<ShoppingListRow> rows, Napkin.Core.Geometry.Length? kerf = null)
        => new("Shopping list", ShoppingListCsv.ToCsv(rows, kerf));
}

/// <summary>
/// The check results on screen, as the app already computed them: the pack is handed results, it
/// never computes one (§1, §12.9) — no rules engine and no pack is ever built by the assistant.
/// </summary>
/// <param name="Code">The project's adopted code as resolved: the pack, or why there is none.</param>
/// <param name="Headers">Every opening's header check.</param>
/// <param name="Bracing">Every wall line's bracing check.</param>
/// <param name="Decks">Every deck's checks.</param>
public sealed record ContextChecks(
    CodeResolution Code,
    ImmutableArray<OpeningCheck> Headers,
    ImmutableArray<WallBracingCheck> Bracing,
    ImmutableArray<DeckChecks> Decks)
{
    /// <summary>No code chosen and nothing checked: a furniture design.</summary>
    public static ContextChecks None { get; } = new(new CodeResolution(null, CodeCheck.NoCodeSelectedText), [], [], []);

    /// <summary>The header checks under a code, and nothing else.</summary>
    public static ContextChecks Of(CodeResolution code, IEnumerable<OpeningCheck> headers)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(headers);
        return new ContextChecks(code, [.. headers], [], []);
    }
}
