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
    /// The lengths this item is stocked in, shortest first, or empty when the library carries no
    /// sourced length list for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stock length list has a different source from a cross-section. PS 20-20 §5.3.1 does not
    /// list stock lengths: it says they "shall be in multiples of 0.3048 m (1 foot) or 0.6096 m
    /// (2 feet) as specified in the certified grading rules", so the list belongs to a grading
    /// agency's rulebook and carries that rulebook's citation in
    /// <see cref="StandardLengthSource"/>, not the size table's.
    /// </para>
    /// <para>
    /// Empty means napkin has read no such list for this item. The shopping list must then say it
    /// cannot pick a stock length rather than invent one.
    /// </para>
    /// </remarks>
    public ImmutableArray<Length> StandardLengths { get; init; } = [];

    /// <summary>Where the stock-length list was read from, when there is one.</summary>
    public Citation? StandardLengthSource { get; init; }

    /// <summary>The arithmetic from the cited length clause to <see cref="StandardLengths"/>.</summary>
    public string StandardLengthDerivation { get; init; } = string.Empty;

    /// <summary>
    /// How a cross-section or a thickness is written: inches and a fraction down to 1/64", the
    /// finest mark on a tape.
    /// </summary>
    /// <remarks>
    /// <strong>Not <see cref="LengthFormat.Default"/>, which rounds to 1/16".</strong> A 23/32
    /// panel shown at 1/16" precision reads "3/4" — the exact confusion between a Performance
    /// Category and a thickness that this library exists to stop. Every dimension in every shipped
    /// table is exact at 1/64", and a test holds that.
    /// </remarks>
    public static readonly LengthFormat SizeFormat = new InchesOnlyFormat(64);

    /// <summary>
    /// How a stock length or a sheet's side is written: feet and inches, because that is how a
    /// person asks for a board.
    /// </summary>
    public static readonly LengthFormat LengthFormatting = LengthFormat.Default;

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
    /// §2.2, "Board measure", obtains the number of board feet "by multiplying the nominal
    /// thickness in inches or fraction of an inch by the nominal width in feet by the length in
    /// feet", so #9 computes board feet from these two and not from the dressed size.
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

    /// <inheritdoc/>
    public override string ActualSizeText
        => $"{Thickness.Format(SizeFormat).Text} x {Width.Format(SizeFormat).Text}";
}

/// <summary>
/// A panel product sold by the sheet: structural plywood (PS 1) and wood structural panels
/// including OSB (PS 2).
/// </summary>
/// <remarks>
/// <para>
/// <strong>These standards do not map a "nominal" thickness to an "actual" one.</strong> Since
/// PS 1-09/PS 2-10 a panel is designated by a <em>Performance Category</em> — a label, not a
/// measurement — and the standard states only a minimum and a maximum thickness the panel must
/// fall between (PS 1-19 Table 10; PS 2-18 Table 1). 23/32 and 3/4 are two <em>different</em>
/// Performance Categories, each with its own range, and neither is "the actual size of" the other.
/// </para>
/// <para>
/// So <see cref="Thickness"/> is the Performance Category read as a length, which is the only
/// exact number the standard attaches to the panel a person picks; the range it is allowed to
/// measure is in <see cref="StockItem.Derivation"/>, in the standard's own decimals, because those
/// decimals are rounded conversions of millimetre limits and do not land on the 1/1024 inch grid.
/// </para>
/// </remarks>
public sealed record PanelStock : StockItem
{
    /// <summary>The Performance Category as the trademark prints it — "23/32", "7/16".</summary>
    public required string PerformanceCategory { get; init; }

    /// <summary>The Performance Category read as an exact length: the design thickness.</summary>
    public required Length Thickness { get; init; }

    /// <summary>The sheet's width — the 4 ft side of a 4x8.</summary>
    public required Length SheetWidth { get; init; }

    /// <summary>The sheet's length — the 8 ft side of a 4x8.</summary>
    public required Length SheetLength { get; init; }

    /// <inheritdoc/>
    public override string ActualSizeText
        => $"{Thickness.Format(SizeFormat).Text} thick, "
           + $"{SheetWidth.Format(LengthFormatting).Text} x {SheetLength.Format(LengthFormatting).Text} sheet";
}

/// <summary>
/// Hardwood lumber, which is named and sold differently from softwood: by rough thickness in
/// quarter inches (4/4, 5/4, 8/4), in random widths, by the board foot.
/// </summary>
/// <remarks>
/// There is no width here on purpose. NHLA lumber is tallied by surface measure in random widths,
/// so "8/4 walnut" names a thickness and nothing else — the width of the board is whatever came
/// off the log. A part cut from it therefore has two free plan dimensions, not one, which is the
/// case the part model in <c>docs/design/parts-and-cut-list.md</c> handles separately from a 2x4.
/// </remarks>
public sealed record HardwoodStock : StockItem
{
    /// <summary>The standard rough thickness the name states — 1" for 4/4, 2" for 8/4.</summary>
    public required Length RoughThickness { get; init; }

    /// <summary>
    /// The standard thickness after surfacing two sides, which is what a part cut from this stock
    /// actually finishes at — 13/16" for 4/4.
    /// </summary>
    public required Length SurfacedTwoSides { get; init; }

    /// <inheritdoc/>
    public override string ActualSizeText
        => $"{RoughThickness.Format(SizeFormat).Text} rough, "
           + $"{SurfacedTwoSides.Format(SizeFormat).Text} surfaced two sides";
}

/// <summary>
/// A driven fastener named by penny size: a nail or a spike, whose length and shank diameter are
/// what a schedule specifies and what a shopping list buys.
/// </summary>
public sealed record FastenerStock : StockItem
{
    /// <summary>The penny size as it is written — "16d".</summary>
    public required string PennySize { get; init; }

    /// <summary>The overall length.</summary>
    public required Length FastenerLength { get; init; }

    /// <summary>
    /// The shank diameter as a decimal of an inch, exactly as the specification prints it —
    /// <c>0.162</c> for a 16d common nail.
    /// </summary>
    /// <remarks>
    /// A <see cref="double"/> here, alone in this library, because a wire diameter genuinely is a
    /// decimal: the specification states <c>.162</c>, not a tape-measure fraction, and forcing it
    /// onto the 1/1024 inch grid would invent a value the source does not give. It is never a
    /// dimension anything is cut to, so it never enters the geometry model.
    /// </remarks>
    public required double ShankDiameterInches { get; init; }

    /// <inheritdoc/>
    public override string ActualSizeText
        => $"{FastenerLength.Format(SizeFormat).Text} long, "
           + $"{ShankDiameterInches.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}\" shank";
}
