namespace Napkin.Core.Materials;

/// <summary>
/// The top level of the stock picker: how a lumber yard is organised, not one entry per size.
/// </summary>
/// <remarks>
/// Issue #7 decided the picker shows one small icon per category and then a plain text list of the
/// sizes inside the chosen category ("2x4" already is the icon). These are those categories, so
/// the toolbox's top row is <c>Enum.GetValues&lt;StockCategory&gt;()</c> and adding a size never
/// adds an icon.
/// </remarks>
public enum StockCategory
{
    /// <summary>
    /// Softwood boards, dimension lumber and timbers — everything sold by a nominal
    /// thickness-by-width name and cut to length. PS 20 calls these three different size classes
    /// (<see cref="SizeClass"/>); a yard piles them in one aisle and so does the picker.
    /// </summary>
    DimensionalLumber,

    /// <summary>Panel products sold by the sheet — plywood, OSB, gypsum board.</summary>
    SheetGood,

    /// <summary>
    /// Hardwood lumber, named by quarter-inch rough thickness (4/4, 5/4, 8/4) and sold by the
    /// board foot in random widths, which is why its rows carry no width.
    /// </summary>
    HardwoodBoard,

    /// <summary>Decking boards, which a yard racks and prices separately from framing lumber.</summary>
    Decking,

    /// <summary>Nails, screws and bolts, named by penny size, gauge or diameter.</summary>
    Fastener,
}

/// <summary>
/// Which size class of PS 20 a softwood row belongs to, which is what decides how its
/// nominal-to-actual size is read out of the standard.
/// </summary>
/// <remarks>
/// PS 20-20 §3.4 splits softwood lumber by nominal thickness, and Table 3 gives each class its own
/// block of cells. The class is stored so a reviewer checking a row knows which block to look in,
/// and so <see cref="StockItem.Derivation"/> can name the cell.
/// </remarks>
public enum SizeClass
{
    /// <summary>Less than nominal 2 in thick and nominal 2 in or more wide (PS 20-20 §3.4.1).</summary>
    Board,

    /// <summary>Nominal 2 in up to, but not including, nominal 5 in thick (PS 20-20 §3.4.2).</summary>
    Dimension,

    /// <summary>
    /// Nominal 5 in or more in least dimension (PS 20-20 §3.4.3). Table 3 states these as an
    /// amount taken off the nominal size rather than as finished cells.
    /// </summary>
    Timber,
}
