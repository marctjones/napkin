namespace Napkin.Core.Geometry;

/// <summary>
/// How a value that does not land on the 1/1024-inch grid is moved onto it.
/// </summary>
/// <remarks>
/// Rounding happens in exactly four places, each of which takes this as an explicit argument or
/// reports that it happened: <see cref="Length.Divide"/>/<see cref="Length.Scale"/>,
/// <see cref="Length.FromInches"/>, parsing and formatting. Nothing rounds silently
/// (docs/design/geometry-model.md §1.4).
/// </remarks>
public enum Rounding
{
    /// <summary>
    /// Round half to even. Unbiased under repeated computation; used by internal arithmetic, by
    /// the solver boundary and by <see cref="Length.Scale"/>.
    /// </summary>
    HalfToEven,

    /// <summary>
    /// Round half away from zero. How a tape measure is read: 1/32&#x2033; shown at 1/16&#x2033;
    /// precision reads as 1/16&#x2033;, not 0. Used for display and for parsing user input.
    /// </summary>
    HalfAwayFromZero,
}
