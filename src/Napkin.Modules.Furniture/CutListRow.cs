using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One line of a cut list: what to cut, how many, how big, and out of what.
/// </summary>
/// <remarks>
/// <para>
/// A row is a value with no formatting decisions in it. The table on screen and the CSV export are
/// two renderings of the same list of these, which is what makes "the export is exactly the rows
/// on screen" true rather than asserted (issue #8, "Output format").
/// </para>
/// <para>
/// The three dimensions are read in the order <see cref="Length"/> &#xD7; <see cref="Width"/>
/// &#xD7; <see cref="Thickness"/>, because that is how a cut list is read at a bench.
/// </para>
/// </remarks>
/// <param name="Label">What to call the pieces on this row.</param>
/// <param name="Quantity">How many to cut. At least one.</param>
/// <param name="Length">The finished length.</param>
/// <param name="Width">The finished width.</param>
/// <param name="Thickness">The finished thickness.</param>
/// <param name="Material">
/// The stock item's name, the unresolved name the part asked for, or empty when the part names no
/// stock at all.
/// </param>
/// <param name="Unresolved">
/// Whether <see cref="Material"/> is a name this build's materials library does not carry. Such a
/// row is shown and exported saying so, and is never silently dropped or guessed at.
/// </param>
/// <param name="Stock">The library item the material resolved to, or <see langword="null"/>.</param>
/// <param name="Cuts">
/// What has been cut off the blank, in site order; empty for a plain rectangle. The three
/// dimensions above are the blank's whatever is in here: a taper never shrinks the listed size
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.1).
/// </param>
/// <param name="PlanAxes">
/// Which of the three dimensions the box's stored width and height are, so that
/// <see cref="CutText"/> can say which edge is which and whether the cut goes through the
/// thickness (&#xA7;4.2).
/// </param>
/// <param name="Members">
/// The entities this row stands for, in ascending id order, so that clicking a row can select the
/// parts it came from.
/// </param>
public sealed record CutListRow(
    string Label,
    int Quantity,
    Length Length,
    Length Width,
    Length Thickness,
    string Material,
    bool Unresolved,
    StockItem? Stock,
    ImmutableArray<Cut> Cuts,
    PlanAxes PlanAxes,
    ImmutableArray<EntityId> Members)
{
    /// <summary>
    /// What to do to the blank, in plain words: one sentence per cut, or per group of cuts that
    /// read the same at different sites. Empty for a plain rectangle.
    /// </summary>
    /// <remarks>
    /// Derived rather than stored, the way <see cref="MaterialText"/> is, so that a row stays a
    /// value with no formatting decisions in it and the table, the CSV and a test all read one
    /// answer (<c>docs/design/parts-and-cut-list.md</c> &#xA7;3.1).
    /// </remarks>
    public ImmutableArray<string> CutText
        => CutDescription.Describe(Cuts, new FinishedSize(Length, Width, Thickness), PlanAxes);

    /// <summary>
    /// The species the part asks for, as typed in the properties panel, or empty when it names none.
    /// </summary>
    /// <remarks>
    /// Part of the cut list's grouping key (&#xA7;3 step 4), so two rows that differ only in species
    /// are two rows; carried here so the shopping list can group by it too (&#xA7;4.1, one row per
    /// stock item and species) without reading the design a second time.
    /// </remarks>
    public string Species { get; init; } = string.Empty;

    /// <summary>How an unresolved stock name reads, in the table and in the export alike.</summary>
    /// <param name="name">The name the part asked for.</param>
    public static string UnresolvedText(string name) => $"{name} — not in this build's materials library";

    /// <summary>
    /// What the material column says: the stock's name, the unresolved name with its explanation,
    /// or nothing at all.
    /// </summary>
    public string MaterialText => Unresolved ? UnresolvedText(Material) : Material;

    /// <summary>
    /// Two rows are equal when they say the same thing about the same parts.
    /// </summary>
    /// <remarks>
    /// Written out because <see cref="ImmutableArray{T}"/> compares by the identity of the array
    /// it wraps, so the compiler's own equality would call two separately computed cut lists of
    /// one design different. "The same design gives the same list" is a property worth being able
    /// to assert. <see cref="Cuts"/> is in it, compared as a sequence for the same reason: two
    /// rows that say to do different things to the same blank are not the same row
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.2).
    /// </remarks>
    /// <param name="other">The row to compare with.</param>
    public bool Equals(CutListRow? other)
        => other is not null
           && string.Equals(Label, other.Label, StringComparison.Ordinal)
           && Quantity == other.Quantity
           && Length == other.Length
           && Width == other.Width
           && Thickness == other.Thickness
           && string.Equals(Material, other.Material, StringComparison.Ordinal)
           && Unresolved == other.Unresolved
           && string.Equals(Species, other.Species, StringComparison.Ordinal)
           && Equals(Stock, other.Stock)
           && Cuts.SequenceEqual(other.Cuts)
           && PlanAxes == other.PlanAxes
           && Members.SequenceEqual(other.Members);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Label, StringComparer.Ordinal);
        hash.Add(Quantity);
        hash.Add(Length);
        hash.Add(Width);
        hash.Add(Thickness);
        hash.Add(Material, StringComparer.Ordinal);
        hash.Add(Unresolved);
        hash.Add(Species, StringComparer.Ordinal);
        hash.Add(Stock);
        hash.Add(PlanAxes);

        foreach (Cut cut in Cuts)
        {
            hash.Add(cut);
        }

        foreach (EntityId member in Members)
        {
            hash.Add(member);
        }

        return hash.ToHashCode();
    }
}
