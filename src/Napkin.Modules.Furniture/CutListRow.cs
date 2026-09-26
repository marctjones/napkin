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
        => CutDescription.Describe(Cuts, Drawn ?? new FinishedSize(Length, Width, Thickness), PlanAxes, SetbacksExact, CompoundEnds);

    /// <summary>
    /// Which of the three dimensions is derived rather than stored — a strut's, from where its ends
    /// are (<c>docs/design/assembly-model.md</c> &#xA7;3a.6) — or <see langword="null"/> for a box,
    /// whose three are all stored.
    /// </summary>
    public PartDimension? Derived { get; init; }

    /// <summary>
    /// Whether the derived dimension was proven to be exactly the listed value; when not, it was
    /// rounded onto the grid once and reads with &#x2248; whatever it rounds to. True for a box.
    /// </summary>
    public bool DerivedExact { get; init; } = true;

    /// <summary>Whether every mitre's setback on a strut's blank was proven exact. True for a box, whose cuts are typed.</summary>
    public bool SetbacksExact { get; init; } = true;

    /// <summary>A strut's ends cut at a mitre and a bevel, west first (<c>docs/design/angled-parts.md</c> &#xA7;2.2). Empty for a box.</summary>
    public ImmutableArray<DerivedCompoundEnd> CompoundEnds { get; init; } = [];

    /// <summary>The length as the table and the CSV say it.</summary>
    public string LengthText => SizeText(PartDimension.Length, Length);

    /// <summary>The width as the table and the CSV say it.</summary>
    public string WidthText => SizeText(PartDimension.Width, Width);

    /// <summary>The thickness as the table and the CSV say it.</summary>
    public string ThicknessText => SizeText(PartDimension.Thickness, Thickness);

    /// <summary>
    /// A size as <see cref="CutListCsv.Text"/> says it, and marked &#x2248; as well when it is the
    /// derived dimension and was rounded: a rounded length that lands on a sixteenth is still not
    /// the leg's length (assembly-model &#xA7;3a.4).
    /// </summary>
    private string SizeText(PartDimension dimension, Length value)
    {
        string text = CutListCsv.Text(value);
        return Derived == dimension && !DerivedExact && !text.StartsWith(CutListCsv.Approximately, StringComparison.Ordinal)
            ? CutListCsv.Approximately + text
            : text;
    }

    /// <summary>
    /// What the part's joints do to it, in structure: the allowances that are already in
    /// <see cref="Length"/>, <see cref="Width"/> and <see cref="Thickness"/>, and each groove, rabbet,
    /// lap, run of pocket holes and set of clips, in the first member's own frame
    /// (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.3, &#xA7;6.4). Empty for a part nothing is done to.
    /// </summary>
    public ImmutableArray<JointFact> Joinery { get; init; } = [];

    /// <summary>
    /// What to do to the blank because of its joints, in the design note's fixed sentences
    /// (&#xA7;6.2); derived from <see cref="Joinery"/> the way <see cref="CutText"/> is from the cuts.
    /// </summary>
    public ImmutableArray<string> JointText => JointDescription.Describe(Joinery);

    /// <summary>The size as drawn, before the joinery allowances: what <see cref="CutText"/> measures its cuts on.</summary>
    public FinishedSize? Drawn { get; init; }

    /// <summary>Whether any part on this row has a joint that no longer holds (&#xA7;6.4).</summary>
    public bool JointsUnsatisfied { get; init; }

    /// <summary>Which of the three dimensions the grain runs along, when the part says (#140); null when unsaid.</summary>
    public PartDimension? Grain { get; init; }

    /// <summary>Which face of the part shows, when it says (#140); null when unsaid.</summary>
    public BoxFace? ShowFace { get; init; }

    /// <summary>
    /// Notes on the row: <see cref="JointDescription.NotSatisfied"/>, then the grain and show face the
    /// part states, and a warning when a board is asked to run its grain across itself (#140).
    /// </summary>
    public ImmutableArray<string> Flags =>
    [
        .. JointsUnsatisfied ? [JointDescription.NotSatisfied] : Array.Empty<string>(),
        .. Grain is { } grain ? [$"Grain along its {Word(grain)}."] : Array.Empty<string>(),
        .. ShowFace is { } face ? [$"Show face: {face.ToString().ToLowerInvariant()}."] : Array.Empty<string>(),
        .. Grain is { } across && across != PartDimension.Length && Stock is LumberStock
            ? [$"Check the grain: a board's grain runs its length, and this part asks for it along its {Word(across)}."]
            : Array.Empty<string>(),
    ];

    static string Word(PartDimension dimension) => dimension.ToString().ToLowerInvariant();

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
    /// Whether any member is rough: entered in Rough mode, its sizes as drawn and its stock not yet
    /// chosen (<c>docs/design/sketch-mode.md</c> &#xA7;5). Not in the grouping key, so firming one leg of
    /// four does not split the row.
    /// </summary>
    public bool Rough { get; init; }

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
           && Joinery.SequenceEqual(other.Joinery)
           && JointsUnsatisfied == other.JointsUnsatisfied
           && Rough == other.Rough
           && Grain == other.Grain
           && ShowFace == other.ShowFace
           && Drawn == other.Drawn
           && PlanAxes == other.PlanAxes
           && Derived == other.Derived
           && DerivedExact == other.DerivedExact
           && SetbacksExact == other.SetbacksExact
           && CompoundEnds.SequenceEqual(other.CompoundEnds)
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

        foreach (JointFact fact in Joinery)
        {
            hash.Add(fact);
        }

        hash.Add(JointsUnsatisfied);
        hash.Add(Rough);
        hash.Add(Grain);
        hash.Add(ShowFace);
        hash.Add(Drawn);
        hash.Add(Derived);
        hash.Add(DerivedExact);
        hash.Add(SetbacksExact);

        foreach (DerivedCompoundEnd end in CompoundEnds)
        {
            hash.Add(end);
        }

        foreach (EntityId member in Members)
        {
            hash.Add(member);
        }

        return hash.ToHashCode();
    }
}
