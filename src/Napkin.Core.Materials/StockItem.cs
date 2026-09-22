using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Core.Materials;

/// <summary>
/// One row of the reference library: a stock item a person can walk into a yard and buy, with the
/// finished dimensions the cut list needs and the citation they were read from.
/// </summary>
/// <remarks>
/// <para>
/// Every dimension is a <see cref="Length"/> — an exact count of 1/1024 inch — never a
/// <see cref="double"/>. Every fraction a lumber standard prints lands on that grid exactly
/// (docs/design/geometry-model.md §1.1), so a row round-trips through the file format, the cut
/// list and the takeoff without a rounding step anywhere.
/// </para>
/// <para>
/// Subtypes differ by what a category's sizes even are: framing lumber has a cross-section and a
/// length you cut, a panel has a thickness and a sheet size, a fastener has a length and a
/// diameter. They are separate record types rather than one row with a dozen nullable fields, so
/// that nothing can ask a nail for its sheet width.
/// </para>
/// </remarks>
public abstract record StockItem
{
    private protected StockItem()
    {
    }

    /// <summary>The name as a yard prints it — "2x4", "5/4x6", "16d".</summary>
    public required string Name { get; init; }

    /// <summary>The spelling the library is keyed by (<see cref="NominalName.Normalize"/>).</summary>
    public required string Key { get; init; }

    /// <summary>Which drawer of the picker this is in.</summary>
    public required StockCategory Category { get; init; }

    /// <summary>The source this row's dimensions were read from.</summary>
    public required Citation Source { get; init; }

    /// <summary>
    /// The arithmetic from the cited cell to the numbers in this row, written out so a reviewer
    /// can re-check it without running anything — the same rule <c>samples/README.md</c> sets for
    /// the hand-derived fixtures.
    /// </summary>
    public required string Derivation { get; init; }

    /// <summary>
    /// The actual size, as the picker's hover shows it before the item is placed — "1 1/2" x
    /// 3 1/2"" for a 2x4 (issue #7's picker design, <c>GUI-CUT-02</c>).
    /// </summary>
    public abstract string ActualSizeText { get; }

    /// <summary>
    /// The whole hover line: the name, the actual size and the standard it came from.
    /// </summary>
    public string HoverText => $"{Name} — actual {ActualSizeText}, {Source.ShortForm}";
}

/// <summary>
/// Softwood lumber sold by a nominal thickness-by-width name and cut to length: boards, dimension
/// lumber and timbers.
/// </summary>
/// <remarks>
/// The cross-section is fixed by the standard and the length is the free dimension — a 2x4 leg is
/// 1 1/2" x 3 1/2" whatever else it is, and only how long it is is up to the design. That is the
/// rule the part model in <c>docs/design/parts-and-cut-list.md</c> builds on.
/// </remarks>
public sealed record LumberStock : StockItem
{
    /// <summary>
    /// The nominal thickness the name states — 2" of a 2x4, 1 1/4" of a 5/4 deck board.
    /// </summary>
    /// <remarks>
    /// Carried as an exact length rather than as a label because the takeoff needs it: PS 20-20
    /// §2.3 defines a board foot as the <em>nominal</em> thickness in inches by the nominal width
    /// in feet by the length in feet, so #9 computes board feet from these two and not from the
    /// dressed size.
    /// </remarks>
    public required Length NominalThickness { get; init; }

    /// <summary>The nominal width the name states — 4" of a 2x4.</summary>
    public required Length NominalWidth { get; init; }

    /// <summary>The minimum dressed dry thickness — 1 1/2" for a 2x4.</summary>
    public required Length Thickness { get; init; }

    /// <summary>The minimum dressed dry width — 3 1/2" for a 2x4.</summary>
    public required Length Width { get; init; }

    /// <summary>Which of PS 20's three size classes the row was read out of.</summary>
    public required SizeClass SizeClass { get; init; }

    /// <summary>
    /// The lengths this size is stocked in, shortest first, or empty when the library carries no
    /// sourced length list for it.
    /// </summary>
    /// <remarks>
    /// PS 20-20 §5.3.1 does not list stock lengths: it says they "shall be in multiples of
    /// 0.3048 m (1 foot) or 0.6096 m (2 feet) as specified in the certified grading rules", so the
    /// list belongs to a grading agency's rulebook and carries that rulebook's citation, not
    /// PS 20's. Empty means napkin has not read such a rulebook for this size — the shopping list
    /// must then say it cannot pick a board length rather than invent one.
    /// </remarks>
    public ImmutableArray<Length> StockLengths { get; init; } = ImmutableArray<Length>.Empty;

    /// <summary>Where the stock-length list was read from, when there is one.</summary>
    public Citation? StockLengthSource { get; init; }

    /// <inheritdoc/>
    public override string ActualSizeText
        => $"{Thickness.Format(LengthFormat.Default).Text} x {Width.Format(LengthFormat.Default).Text}";
}
