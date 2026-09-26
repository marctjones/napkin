using System.Collections.Immutable;
using Avalonia;
using Avalonia.Media;
using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>What a piece of Parts view text is, which decides its colour and weight.</summary>
public enum PartsTextRole
{
    /// <summary>The part's name, top-left, bold.</summary>
    Label,

    /// <summary>The count badge, "×4", top-right.</summary>
    Badge,

    /// <summary>The line under the drawing: caption, material, "not to scale".</summary>
    Details,

    /// <summary>One of the cut sentences under that.</summary>
    Cut,
}

/// <summary>A run of a cell's text and where it starts (for the badge, where it ends: it is right-aligned).</summary>
/// <param name="Text">What it says.</param>
/// <param name="At">Its top-left corner, or for the badge its top-right.</param>
/// <param name="Role">What it is.</param>
public readonly record struct PartsText(string Text, Point At, PartsTextRole Role);

/// <summary>A dimension line of a cell: from one end of the drawn piece to the other, and what it reads.</summary>
/// <param name="From">One end.</param>
/// <param name="To">The other.</param>
/// <param name="Label">The length, as the cut list writes it.</param>
public readonly record struct PartsDimension(Point From, Point To, string Label);

/// <summary>One cell, built and ready to paint (docs/design/parts-view.md §2.2, §2.3).</summary>
/// <param name="Outline">The piece, in the cell's pixels, north up.</param>
/// <param name="Drawn">The rectangle the piece is drawn in: its extents at the cell's scale.</param>
/// <param name="PixelsPerInch">The scale the cell is drawn at.</param>
/// <param name="NotToScale">Whether that is the floor's scale rather than the sheet's.</param>
/// <param name="Length">The dimension below the piece.</param>
/// <param name="Width">The dimension beside it.</param>
/// <param name="Texts">The label, the badge, the details line and the cut lines.</param>
public sealed record PartsCellDrawn(
    Geometry Outline,
    Rect Drawn,
    double PixelsPerInch,
    bool NotToScale,
    PartsDimension Length,
    PartsDimension Width,
    ImmutableArray<PartsText> Texts);

/// <summary>
/// Builds one Parts view cell — geometry, dimension lines and text — without drawing it (§2.3), so
/// the extents a test measures are the ones the view paints. The scale, the pose and the words are
/// <see cref="PartsScale"/>'s, <see cref="PartsPicture"/>'s and <see cref="PartsCellText"/>'s.
/// </summary>
public static class PartsCellDrawing
{
    /// <summary>A cell's height, at 100 % zoom.</summary>
    public const double CellHeight = 200;

    /// <summary>The row the label and badge sit in, above the drawing.</summary>
    public const double LabelRow = 28;

    /// <summary>How far a dimension line stands off the piece.</summary>
    public const double DimensionGap = 10;

    /// <summary>The space between lines of text under the drawing.</summary>
    public const double LineHeight = 16;

    /// <summary>One cell, in a cell rectangle, at the sheet's scale.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="sheetScale">The sheet's scale (<see cref="PartsScale.Sheet"/>).</param>
    /// <param name="at">The cell's top-left corner.</param>
    public static PartsCellDrawn Build(PartsCell cell, double sheetScale, Point at)
    {
        ArgumentNullException.ThrowIfNull(cell);
        PartsPicture picture = PartsPicture.Of(cell);
        (double scale, bool notToScale) = PartsScale.Cell(picture, sheetScale);

        double width = picture.Horizontal.ToInches() * scale, height = picture.Vertical.ToInches() * scale;
        Rect drawing = new(at.X + PartsScale.Inset, at.Y + LabelRow, PartsScale.DrawingWidth, PartsScale.DrawingHeight);
        Rect drawn = new(drawing.Center.X - (width / 2), drawing.Center.Y - (height / 2), width, height);

        // The outline's own frame: x across its blank, y up it. Turned a quarter, its y runs across
        // the cell and its x up — a swap and a flip, no trigonometry (§1.2).
        double blankAcross = picture.QuarterTurn ? picture.Vertical.ToInches() : 0;
        Point ToCell(Point2 point)
        {
            double x = point.X.ToInches(), y = point.Y.ToInches();
            (double across, double up) = picture.QuarterTurn ? (y, blankAcross - x) : (x, y);
            return new Point(drawn.Left + (across * scale), drawn.Bottom - (up * scale));
        }

        Geometry outline = OutlineDrawing.GeometryOf(picture.Outline, ToCell);
        PartsDimension length = new(
            new Point(drawn.Left, drawn.Bottom + DimensionGap),
            new Point(drawn.Right, drawn.Bottom + DimensionGap),
            picture.HorizontalText);
        PartsDimension side = new(
            new Point(drawn.Right + DimensionGap, drawn.Top),
            new Point(drawn.Right + DimensionGap, drawn.Bottom),
            picture.VerticalText);

        double below = drawing.Bottom + DimensionGap + LineHeight;
        ImmutableArray<PartsText>.Builder texts = ImmutableArray.CreateBuilder<PartsText>();
        texts.Add(new PartsText(cell.Row.Label, new Point(at.X + PartsScale.Inset, at.Y + 6), PartsTextRole.Label));
        texts.Add(new PartsText(PartsCellText.Badge(cell), new Point(at.X + PartsScale.CellWidth - PartsScale.Inset, at.Y + 6), PartsTextRole.Badge));
        texts.Add(new PartsText(PartsCellText.Details(cell, picture, notToScale), new Point(at.X + PartsScale.Inset, below), PartsTextRole.Details));
        foreach (string cut in PartsCellText.Cuts(cell))
        {
            below += LineHeight;
            texts.Add(new PartsText(cut, new Point(at.X + PartsScale.Inset, below), PartsTextRole.Cut));
        }

        return new PartsCellDrawn(outline, drawn, scale, notToScale, length, side, texts.ToImmutable());
    }
}
