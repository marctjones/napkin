using System.Collections.Immutable;

namespace Napkin.Core.Materials;

/// <summary>
/// One data file's worth of the library: a set of stock items read from one source.
/// </summary>
/// <param name="Id">The table's stable id, unique across the library.</param>
/// <param name="Title">What the table is, for a person reading a sources list.</param>
/// <param name="Category">The picker drawer every item in this table is in.</param>
/// <param name="Source">The citation the table was authored from, and every item's default.</param>
/// <param name="File">The data file it was read from, so a refusal or a review can name it.</param>
/// <param name="Items">The rows, in the order the file wrote them. Never empty.</param>
public sealed record StockTable(
    string Id,
    string Title,
    StockCategory Category,
    Citation Source,
    string File,
    ImmutableArray<StockItem> Items);
