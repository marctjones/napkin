namespace Napkin.Core.Materials;

/// <summary>
/// Where a row was read from: the standard or specification, who publishes it, the exact place in
/// it, the copy that was read, and the day it was retrieved.
/// </summary>
/// <remarks>
/// <para>
/// Every table and every item in this library carries one (<c>MAT-004</c>). The rule it enforces
/// is CLAUDE.md's: no dimension, standard length or grade is ever written from memory — it is read
/// out of a primary source in the same task and cited precisely enough that a reviewer can open
/// the source and check the row against it. A table whose citation is missing or empty fails the
/// load, so an uncited row cannot reach the app at all.
/// </para>
/// <para>
/// <see cref="Where"/> is the part that does the work: "Table 3, page 16, Dry column" is
/// checkable, "the lumber standard" is not.
/// </para>
/// </remarks>
/// <param name="Standard">The document — "Voluntary Product Standard PS 20-20, American Softwood Lumber Standard".</param>
/// <param name="Publisher">Who publishes it, so the right document can be found without the link.</param>
/// <param name="Where">The table, section, page and column the value was read from.</param>
/// <param name="Url">The copy that was actually read.</param>
/// <param name="Retrieved">The day it was retrieved, because a published standard is revised.</param>
public sealed record Citation(string Standard, string Publisher, string Where, string Url, DateOnly Retrieved)
{
    /// <summary>The citation on one line, as a tooltip or a printed sheet shows it.</summary>
    public override string ToString()
        => $"{Standard} ({Publisher}), {Where}. {Url}, retrieved {Retrieved:yyyy-MM-dd}.";

    /// <summary>A short form for a hover tooltip — the standard's designation only.</summary>
    /// <remarks>
    /// The picker's hover shows "2x4 — actual 1 1/2" x 3 1/2", PS 20-20"; the whole citation is
    /// for the properties panel and the printed cut list, not for a tooltip.
    /// </remarks>
    public string ShortForm
    {
        get
        {
            int comma = Standard.IndexOf(',');
            string head = comma < 0 ? Standard : Standard[..comma];
            const string prefix = "Voluntary Product Standard ";
            return head.StartsWith(prefix, StringComparison.Ordinal) ? head[prefix.Length..] : head;
        }
    }
}
