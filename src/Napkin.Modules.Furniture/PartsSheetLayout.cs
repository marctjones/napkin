namespace Napkin.Modules.Furniture;

/// <summary>
/// One thing placed on the Parts sheet (docs/design/parts-view.md §4.1): a cell, or a group's title
/// band, in pixels from the sheet's top-left, y down.
/// </summary>
/// <param name="Cell">The cell placed here, or null for a title band.</param>
/// <param name="Title">The group title placed here, or null for a cell.</param>
/// <param name="Left">Its left edge.</param>
/// <param name="Top">Its top edge.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public sealed record PartsPlacement(PartsCell? Cell, string? Title, double Left, double Top, double Width, double Height)
{
    /// <summary>Its right edge.</summary>
    public double Right => Left + Width;

    /// <summary>Its bottom edge.</summary>
    public double Bottom => Top + Height;
}

/// <summary>
/// Where the Parts sheet's cells go (docs/design/parts-view.md §4.1): a wrapped grid of equal cells in
/// the sheet's order, as many columns as the width buys and never fewer than one, growing only
/// downwards; a group, when grouped, starts its own row under a title band. Pure and deterministic:
/// the same cells, width and zoom give the same placements, on whole pixels.
/// </summary>
public static class PartsSheetLayout
{
    /// <summary>A cell's height at 100 %, with <see cref="PartsScale.CellWidth"/> its width.</summary>
    public const double CellHeight = 200;

    /// <summary>The space between cells, at 100 %.</summary>
    public const double Gutter = 12;

    /// <summary>The space round the sheet, at 100 %.</summary>
    public const double Margin = 16;

    /// <summary>A group title band's height, at 100 %.</summary>
    public const double TitleBand = 24;

    /// <summary>The smallest zoom the sheet is drawn at.</summary>
    public const double MinZoom = 0.5;

    /// <summary>The largest.</summary>
    public const double MaxZoom = 3;

    /// <summary>How many cells a row holds at a width and zoom: what fits between the margins, at least one.</summary>
    public static int Columns(double viewportWidth, double zoom)
    {
        CheckZoom(zoom);
        double cell = PartsScale.CellWidth * zoom, gutter = Gutter * zoom, margin = Margin * zoom;
        return Math.Max(1, (int)Math.Floor((viewportWidth - (2 * margin) + gutter) / (cell + gutter)));
    }

    /// <summary>The cells, ungrouped, in the sheet's order.</summary>
    public static IReadOnlyList<PartsPlacement> Arrange(IReadOnlyList<PartsCell> cells, double viewportWidth, double zoom)
    {
        ArgumentNullException.ThrowIfNull(cells);
        List<PartsPlacement> placed = [];
        Place(placed, cells, Margin * zoom, Columns(viewportWidth, zoom), zoom);
        return placed;
    }

    /// <summary>The cells grouped by stock, each group under its title band, starting its own row.</summary>
    public static IReadOnlyList<PartsPlacement> Arrange(IReadOnlyList<PartsGroup> groups, double viewportWidth, double zoom)
    {
        ArgumentNullException.ThrowIfNull(groups);
        int columns = Columns(viewportWidth, zoom);
        double gutter = Gutter * zoom, band = TitleBand * zoom;
        double rowWidth = (columns * (PartsScale.CellWidth * zoom)) + ((columns - 1) * gutter);
        List<PartsPlacement> placed = [];
        double top = Margin * zoom;
        foreach (PartsGroup group in groups)
        {
            placed.Add(Snapped(null, group.Title, Margin * zoom, top, rowWidth, band));
            top = Place(placed, group.Cells, top + band, columns, zoom) + gutter;
        }

        return placed;
    }

    /// <summary>The sheet's height: below the last placement, the margin; an empty sheet is its two margins.</summary>
    public static double Height(IReadOnlyList<PartsPlacement> placements, double zoom)
    {
        ArgumentNullException.ThrowIfNull(placements);
        CheckZoom(zoom);
        return placements.Count == 0 ? 2 * Margin * zoom : placements.Max(placement => placement.Bottom) + (Margin * zoom);
    }

    /// <summary>Places cells in rows from a top edge; returns the bottom of the last row (the top, if none).</summary>
    static double Place(List<PartsPlacement> placed, IReadOnlyList<PartsCell> cells, double top, int columns, double zoom)
    {
        double cell = PartsScale.CellWidth * zoom, height = CellHeight * zoom, gutter = Gutter * zoom, margin = Margin * zoom;
        double bottom = top;
        for (int i = 0; i < cells.Count; i++)
        {
            int row = i / columns, column = i % columns;
            PartsPlacement placement = Snapped(cells[i], null, margin + (column * (cell + gutter)), top + (row * (height + gutter)), cell, height);
            placed.Add(placement);
            bottom = placement.Bottom;
        }

        return bottom;
    }

    /// <summary>A placement on whole pixels: both edges rounded, so neighbours never overlap or gap by a sliver.</summary>
    static PartsPlacement Snapped(PartsCell? cell, string? title, double left, double top, double width, double height)
    {
        double l = Math.Round(left), t = Math.Round(top);
        return new PartsPlacement(cell, title, l, t, Math.Round(left + width) - l, Math.Round(top + height) - t);
    }

    static void CheckZoom(double zoom)
    {
        if (!(zoom >= MinZoom && zoom <= MaxZoom))
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), zoom, $"The Parts sheet is drawn between {MinZoom:P0} and {MaxZoom:P0}.");
        }
    }
}
