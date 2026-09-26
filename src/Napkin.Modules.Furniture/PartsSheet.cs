using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One cell of the Parts view (docs/design/parts-view.md §1.1): a cut-list row, drawn once, with
/// its count. The row is the cell's identity; the cell adds only the piece to draw.
/// </summary>
/// <remarks>
/// Equality is the row's: the blank carries a fresh id and layer, and an outline or a solid compares
/// by reference, so two cells for equal rows are equal cells, the way <see cref="CutListRow"/> writes
/// its own equality.
/// </remarks>
public sealed class PartsCell : IEquatable<PartsCell>
{
    readonly Lazy<Outline> _outline;
    readonly Lazy<Solid> _solid;

    /// <summary>The cell for one row of the cut list.</summary>
    /// <param name="row">The row it stands for.</param>
    public PartsCell(CutListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Row = row;
        Blank = BlankFor(row);
        _outline = new Lazy<Outline>(() => Blank.Outline());
        _solid = new Lazy<Solid>(() => Blank.Solid());
    }

    /// <summary>The row this cell stands for.</summary>
    public CutListRow Row { get; }

    /// <summary>How many pieces: the row's quantity, Σ <see cref="Part.Quantity"/> over its members. The badge.</summary>
    public int Count => Row.Quantity;

    /// <summary>The piece to draw, in its own frame (<see cref="BlankFor"/>).</summary>
    public Box Blank { get; }

    /// <summary>The blank's outline, as the plan draws it: what the 2D cell draws. Built once.</summary>
    public Outline Outline => _outline.Value;

    /// <summary>The blank's solid: what the 3D cell draws. Built once.</summary>
    public Solid Solid => _solid.Value;

    /// <summary>The parts it stands for, ascending, for selection (§5); never fetched to draw.</summary>
    public ImmutableArray<EntityId> Members => Row.Members;

    /// <summary>
    /// The piece a row describes, in its own frame: anchored at the origin, unturned, its stored width
    /// and height the two dimensions <see cref="CutListRow.PlanAxes"/> names and its depth the third,
    /// carrying the row's cuts — the one blank the Parts view and the cut list's thumbnails share.
    /// </summary>
    /// <remarks>
    /// Every member of a row has the same three sizes, the same plan axes and the same cuts (they
    /// are the grouping key), so this blank is each member's blank; a part turned any of the 24
    /// ways lists the same row and so the same blank (#95).
    /// </remarks>
    public static Box BlankFor(CutListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Box.AsDrawn(
            EntityId.New(),
            LayerId.New(),
            Point2.Origin,
            Along(row, row.PlanAxes.X),
            Along(row, row.PlanAxes.Y),
            Along(row, row.PlanAxes.OutOfPlane),
            Angle.Zero) with
        {
            Cuts = [.. row.Cuts],
        };
    }

    /// <inheritdoc/>
    public bool Equals(PartsCell? other) => other is not null && Row.Equals(other.Row);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PartsCell);

    /// <inheritdoc/>
    public override int GetHashCode() => Row.GetHashCode();

    /// <summary>Which of a row's three dimensions one of its plan axes is.</summary>
    static Length Along(CutListRow row, PartDimension dimension) => dimension switch
    {
        PartDimension.Length => row.Length,
        PartDimension.Width => row.Width,
        _ => row.Thickness,
    };
}

/// <summary>The cells of one stock, under the stock's name (docs/design/parts-view.md §1.3).</summary>
/// <param name="Title">The stock's name, an unresolved name's sentence, or "No stock".</param>
/// <param name="Cells">Its cells, in the cut list's order.</param>
public sealed record PartsGroup(string Title, ImmutableArray<PartsCell> Cells);

/// <summary>
/// The Parts view's model (docs/design/parts-view.md §1): the cut list's rows, one cell each, in the
/// cut list's order — the same data as the cut list, so a count cannot disagree with it.
/// </summary>
/// <remarks>
/// It takes rows, never the sketch: which pieces are the same is the cut list's grouping, unchanged,
/// because there is no grouping code here.
/// </remarks>
public static class PartsSheet
{
    /// <summary>The title of the group of parts with no stock chosen.</summary>
    public const string NoStockTitle = "No stock";

    /// <summary>One cell per row, in the rows' order.</summary>
    /// <param name="rows">The cut list: <see cref="CutList.Of"/>.</param>
    public static ImmutableArray<PartsCell> Of(IEnumerable<CutListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Select(row => new PartsCell(row))];
    }

    /// <summary>
    /// The cells grouped by stock (§1.3): the stocks the library knows, ordered by where each first
    /// appears in the sheet; then each name the library does not know, likewise; then "No stock".
    /// The cut list's order holds inside every group, and every cell is in exactly one.
    /// </summary>
    /// <param name="cells">The sheet.</param>
    public static ImmutableArray<PartsGroup> GroupedByStock(ImmutableArray<PartsCell> cells) =>
    [
        .. cells
            .GroupBy(cell => (cell.Row.Material, cell.Row.Unresolved))
            .OrderBy(group => Rank(group.Key.Material, group.Key.Unresolved))
            .Select(group => new PartsGroup(Title(group.Key.Material, group.Key.Unresolved), [.. group])),
    ];

    /// <summary>Where a group falls: known stock first, unknown names next, no stock last. Stable within a rank.</summary>
    static int Rank(string material, bool unresolved) =>
        material.Length == 0 ? 2 : unresolved ? 1 : 0;

    static string Title(string material, bool unresolved) =>
        material.Length == 0 ? NoStockTitle : unresolved ? CutListRow.UnresolvedText(material) : material;
}
