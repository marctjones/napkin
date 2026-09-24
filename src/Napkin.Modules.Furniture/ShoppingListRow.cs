using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>What a shopping-list row buys, which is what its stock is sold as.</summary>
public enum ShoppingListKind
{
    /// <summary>Softwood boards and dimension lumber: a count of boards of each stocked length.</summary>
    Boards,

    /// <summary>Panels: a count of sheets, by area, which is a floor until nesting (#26).</summary>
    Sheets,

    /// <summary>Hardwood: board feet in random widths, and no piece count.</summary>
    BoardFeet,

    /// <summary>Lumber the library has read no stock-length list for: nothing is bought.</summary>
    NoLengthList,

    /// <summary>
    /// Parts with no stock, a stock this build does not carry, or a stock nothing is cut from: shown on
    /// their own and never aggregated into anything.
    /// </summary>
    NothingToBuy,
}

/// <summary>A number of boards of one stocked length.</summary>
/// <param name="StockLength">The length the boards are bought in.</param>
/// <param name="Count">How many.</param>
public readonly record struct BoardsOfLength(Length StockLength, int Count);

/// <summary>
/// One line of the shopping list: one stock item and species, and what to buy of it
/// (<c>docs/design/parts-and-cut-list.md</c> §4).
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="CutListRow"/>, a value with the numbers kept exact: board feet are carried as the
/// exact <see cref="Int128"/> sum of cubic units and turned into a decimal only by
/// <see cref="BoardFeet.Text"/>, once, at the end (§4.1).
/// </para>
/// </remarks>
/// <param name="Material">The stock item's name; for a <see cref="ShoppingListKind.NothingToBuy"/> row, the cut row's material text, possibly empty.</param>
/// <param name="Species">The species the parts ask for, or empty.</param>
/// <param name="Kind">What the row buys.</param>
/// <param name="For">The cut-list rows it is for, as "label × quantity", in cut-list order.</param>
/// <param name="Boards">For <see cref="ShoppingListKind.Boards"/>: the boards to buy, shortest length first.</param>
/// <param name="Sheets">For <see cref="ShoppingListKind.Sheets"/>: the sheets to buy, by area.</param>
/// <param name="BoughtCubicUnits">For boards: nominal thickness × nominal width × length, summed over the boards bought.</param>
/// <param name="UsedCubicUnits">
/// For boards and lumber without a length list: nominal thickness × nominal width × piece length over
/// the pieces bought for. For hardwood: rough thickness × width × length over every piece.
/// </param>
/// <param name="Note">What the row cannot say with a number: a refusal, or what a count is a floor of.</param>
/// <param name="Stock">The library item, or <see langword="null"/> for a row with nothing to buy.</param>
public sealed record ShoppingListRow(
    string Material,
    string Species,
    ShoppingListKind Kind,
    string For,
    ImmutableArray<BoardsOfLength> Boards,
    int Sheets,
    Int128 BoughtCubicUnits,
    Int128 UsedCubicUnits,
    string Note,
    StockItem? Stock)
{
    /// <summary>What to buy, as a person asks for it: "3 × 6'-0"", "1 sheet, 4'-0" × 8'-0"".</summary>
    public string BuyText => Kind switch
    {
        ShoppingListKind.Boards => string.Join(
            ", ",
            Boards.Select(boards => $"{boards.Count} × {boards.StockLength.Format(StockItem.LengthFormatting).Text}")),
        ShoppingListKind.Sheets when Stock is PanelStock panel => $"{Sheets} {(Sheets == 1 ? "sheet" : "sheets")}, "
            + $"{panel.SheetWidth.Format(StockItem.LengthFormatting).Text} × {panel.SheetLength.Format(StockItem.LengthFormatting).Text}",
        ShoppingListKind.BoardFeet => $"{BoardFeet.Text(UsedCubicUnits)} bd ft, random widths",
        _ => string.Empty,
    };

    /// <summary>How many boards or sheets are bought; zero for a row that buys neither.</summary>
    public int Count => Kind switch
    {
        ShoppingListKind.Boards => Boards.Sum(boards => boards.Count),
        ShoppingListKind.Sheets => Sheets,
        _ => 0,
    };

    /// <summary>Board feet bought, to one decimal place; empty where nothing is bought by the board.</summary>
    public string BoughtText => Kind == ShoppingListKind.Boards ? BoardFeet.Text(BoughtCubicUnits) : string.Empty;

    /// <summary>Board feet of the parts themselves; empty for sheets and for nothing to buy.</summary>
    public string UsedText => Kind is ShoppingListKind.Boards or ShoppingListKind.BoardFeet or ShoppingListKind.NoLengthList
        ? BoardFeet.Text(UsedCubicUnits)
        : string.Empty;

    /// <summary>Board feet bought and not used — the number that makes a person pick another length.</summary>
    public string WasteText => Kind == ShoppingListKind.Boards
        ? BoardFeet.Text(BoughtCubicUnits - UsedCubicUnits)
        : string.Empty;

    /// <summary>Two rows are equal when they say the same thing; the board list is compared as a sequence.</summary>
    /// <param name="other">The row to compare with.</param>
    public bool Equals(ShoppingListRow? other)
        => other is not null
           && string.Equals(Material, other.Material, StringComparison.Ordinal)
           && string.Equals(Species, other.Species, StringComparison.Ordinal)
           && Kind == other.Kind
           && string.Equals(For, other.For, StringComparison.Ordinal)
           && Boards.SequenceEqual(other.Boards)
           && Sheets == other.Sheets
           && BoughtCubicUnits == other.BoughtCubicUnits
           && UsedCubicUnits == other.UsedCubicUnits
           && string.Equals(Note, other.Note, StringComparison.Ordinal)
           && Equals(Stock, other.Stock);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Material, StringComparer.Ordinal);
        hash.Add(Species, StringComparer.Ordinal);
        hash.Add(Kind);
        hash.Add(For, StringComparer.Ordinal);
        hash.Add(Sheets);
        hash.Add(BoughtCubicUnits);
        hash.Add(UsedCubicUnits);

        foreach (BoardsOfLength boards in Boards)
        {
            hash.Add(boards);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Board feet, exactly: PS 20-20 §2.2 "Board measure" — nominal thickness in inches × nominal width
/// in feet × length in feet, which is cubic inches over 144.
/// </summary>
public static class BoardFeet
{
    /// <summary>Cubic units of the 1/1024&#x2033; grid in one board foot: 1024³ × 144.</summary>
    public static readonly Int128 CubicUnitsPerBoardFoot =
        (Int128)Length.UnitsPerInch * Length.UnitsPerInch * Length.UnitsPerInch * 144;

    /// <summary>
    /// An exact volume as board feet to one decimal place, half away from zero — the one rounding a
    /// takeoff does (§4.1).
    /// </summary>
    /// <param name="cubicUnits">The exact volume, in cubic units; never negative.</param>
    public static string Text(Int128 cubicUnits)
    {
        Int128 tenths = ((cubicUnits * 10) + (CubicUnitsPerBoardFoot / 2)) / CubicUnitsPerBoardFoot;
        return $"{tenths / 10}.{tenths % 10}";
    }
}
