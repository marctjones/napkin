using System.Collections.Immutable;
using Avalonia;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>One face of a cell's isometric drawing, on the cell's pixels.</summary>
/// <param name="Points">Its corners, projected, in order.</param>
/// <param name="Normal">Which way it faces in the world, for its tone.</param>
public readonly record struct PartsFace(ImmutableArray<Point> Points, Vector3d Normal);

/// <summary>A cell drawn in 3D, built and ready to paint (docs/design/parts-view.md §3).</summary>
/// <param name="Faces">The faces towards the eye, back to front.</param>
/// <param name="Drawn">The rectangle the drawing occupies, in the cell's pixels.</param>
/// <param name="PixelsPerInch">The scale it is drawn at.</param>
/// <param name="NotToScale">Whether that is the floor's scale rather than the sheet's.</param>
/// <param name="Texts">The label, the badge, the details line and the cut lines, as in 2D.</param>
public sealed record PartsIsometricDrawn(
    ImmutableArray<PartsFace> Faces,
    Rect Drawn,
    double PixelsPerInch,
    bool NotToScale,
    ImmutableArray<PartsText> Texts);

/// <summary>
/// The Parts view's 3D option (docs/design/parts-view.md §3): each piece's solid drawn once,
/// isometric and orthographic, every piece at one common scale so their sizes still compare, a piece
/// too small to read at it drawn at the floor and flagged, as in 2D.
/// </summary>
/// <remarks>
/// The scale is the 3D drawing's own, found the way §2.1 finds the 2D one: the largest at which every
/// piece's isometric extent fits the cell's drawing. The 2D scale would not do — an isometric 48″ × 24″
/// top is about 62″ across, wider than its cell at the 2D scale. The camera projects at one pixel to
/// the inch about the origin and the scale is applied here, so no camera clamp touches a small scale.
/// </remarks>
public static class PartsIsometric
{
    static readonly Camera Eye = Camera.Isometric() with { PixelsPerInch = 1, Projection = CameraProjection.Orthographic };

    /// <summary>A cell's isometric extent at one pixel to the inch: across and up the screen.</summary>
    public static (double Width, double Height) Extent(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        (double left, double top, double right, double bottom) = Bounds(Polygons(cell).SelectMany(polygon => polygon.Points).Select(Eye.Project));
        return (right - left, bottom - top);
    }

    /// <summary>The sheet's 3D scale: the largest at which every cell's isometric extent fits its drawing. Null with nothing to draw.</summary>
    public static double? Sheet(IEnumerable<PartsCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        double? scale = null;
        foreach (PartsCell cell in cells)
        {
            (double width, double height) = Extent(cell);
            double fits = Math.Min(
                width > 0 ? PartsScale.DrawingWidth / width : double.PositiveInfinity,
                height > 0 ? PartsScale.DrawingHeight / height : double.PositiveInfinity);
            if (double.IsFinite(fits))
            {
                scale = scale is { } smaller ? Math.Min(smaller, fits) : fits;
            }
        }

        return scale;
    }

    /// <summary>The scale one cell is drawn at in 3D: the sheet's, or the floor's when its longer extent would be under it.</summary>
    public static (double PixelsPerInch, bool NotToScale) Cell(PartsCell cell, double sheet)
    {
        (double width, double height) = Extent(cell);
        double longer = Math.Max(width, height);
        return longer > 0 && longer * sheet < PartsScale.FloorPixels ? (PartsScale.FloorPixels / longer, true) : (sheet, false);
    }

    /// <summary>One cell in 3D, in a cell rectangle, at the sheet's 3D scale.</summary>
    public static PartsIsometricDrawn Build(PartsCell cell, double sheet, Point at)
    {
        ArgumentNullException.ThrowIfNull(cell);
        (double scale, bool notToScale) = Cell(cell, sheet);
        ImmutableArray<ScenePolygon> polygons = Polygons(cell);
        (double left, double top, double right, double bottom) = Bounds(polygons.SelectMany(polygon => polygon.Points).Select(Eye.Project));

        // Centre the drawing in the cell's drawing area: the projected middle goes to its middle.
        Point middle = new(at.X + PartsScale.Inset + (PartsScale.DrawingWidth / 2), at.Y + PartsCellDrawing.LabelRow + (PartsScale.DrawingHeight / 2));
        Point from = new((left + right) / 2, (top + bottom) / 2);
        Point ToCell(Vector3d point)
        {
            Point projected = Eye.Project(point);
            return new Point(middle.X + ((projected.X - from.X) * scale), middle.Y + ((projected.Y - from.Y) * scale));
        }

        ImmutableArray<PartsFace> faces =
        [
            .. ModelScene.Of(polygons).BackToFront(Eye)
                .Select(polygon => new PartsFace([.. polygon.Points.Select(ToCell)], polygon.Normal)),
        ];
        double width = (right - left) * scale, height = (bottom - top) * scale;
        Rect drawn = new(middle.X - (width / 2), middle.Y - (height / 2), width, height);
        return new PartsIsometricDrawn(faces, drawn, scale, notToScale, PartsCellDrawing.TextsFor(cell, PartsPicture.Of(cell), notToScale, at));
    }

    static ImmutableArray<ScenePolygon> Polygons(PartsCell cell) => [.. ModelScene.PolygonsOf(cell.Blank.Id, cell.Solid)];

    static (double Left, double Top, double Right, double Bottom) Bounds(IEnumerable<Point> points)
    {
        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (Point point in points)
        {
            left = Math.Min(left, point.X);
            top = Math.Min(top, point.Y);
            right = Math.Max(right, point.X);
            bottom = Math.Max(bottom, point.Y);
        }

        return double.IsFinite(left) ? (left, top, right, bottom) : (0, 0, 0, 0);
    }
}
