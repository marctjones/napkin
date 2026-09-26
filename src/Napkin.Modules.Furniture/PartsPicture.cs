using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>How a Parts view cell shows its piece (docs/design/parts-view.md §1.2, as amended).</summary>
public enum PartsPose
{
    /// <summary>Length × width lie in the plan: drawn as it lies, captioned with its thickness.</summary>
    Flat,

    /// <summary>Length × thickness lie in the plan (a board on edge): drawn as it lies, "wide — shown on edge".</summary>
    OnEdge,

    /// <summary>Standing on end with no cuts (a leg): drawn face-on, length × width, captioned with its thickness.</summary>
    FaceOn,

    /// <summary>Standing on end with cuts, which exist only in the plan: drawn as it lies, "long — shown on end".</summary>
    OnEnd,
}

/// <summary>
/// What a Parts view cell draws of its piece: which pose, the outline, and its two extents with the
/// longer one horizontal (§1.2) — the picture's facts, before any scale or pixel.
/// </summary>
/// <param name="Pose">Which way the piece is shown.</param>
/// <param name="Outline">The outline to draw, in its own frame: the blank's for a piece drawn as it lies, a plain length × width rectangle face-on.</param>
/// <param name="QuarterTurn">Whether the outline is turned a quarter so that its longer extent lies horizontal.</param>
/// <param name="Horizontal">The drawn extent across, once turned: the longer.</param>
/// <param name="Vertical">The drawn extent up, once turned: the shorter.</param>
/// <param name="Caption">The dimension the picture does not show, in words: "3/4" thick", "3 1/2" wide — shown on edge".</param>
public sealed record PartsPicture(
    PartsPose Pose,
    Outline Outline,
    bool QuarterTurn,
    Length Horizontal,
    Length Vertical,
    string Caption)
{
    /// <summary>What the dimension line under the picture reads.</summary>
    public string HorizontalText => CutListCsv.Text(Horizontal);

    /// <summary>What the dimension line beside the picture reads.</summary>
    public string VerticalText => CutListCsv.Text(Vertical);

    /// <summary>How a cell shows its piece.</summary>
    /// <param name="cell">The cell.</param>
    public static PartsPicture Of(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        CutListRow row = cell.Row;
        PlanAxes axes = row.PlanAxes;
        bool lengthInPlan = axes.X == PartDimension.Length || axes.Y == PartDimension.Length;

        if (!lengthInPlan && row.Cuts.IsEmpty)
        {
            // A leg: nothing is lost by standing it up on the page, and its length is what it is.
            Box face = Box.AsDrawn(EntityId.New(), LayerId.New(), Point2.Origin, Max(row.Length, row.Width), Min(row.Length, row.Width), row.Thickness, Angle.Zero);
            return new PartsPicture(PartsPose.FaceOn, face.Outline(), false, face.Width, face.Height, Thick(row));
        }

        PartsPose pose = !lengthInPlan ? PartsPose.OnEnd
            : axes.OutOfPlane == PartDimension.Width ? PartsPose.OnEdge
            : PartsPose.Flat;
        Length across = cell.Blank.Width, up = cell.Blank.Height;
        bool turn = up > across;
        string caption = pose switch
        {
            PartsPose.OnEdge => $"{CutListCsv.Text(row.Width)} wide — shown on edge",
            PartsPose.OnEnd => $"{row.LengthText} long — shown on end",
            _ => Thick(row),
        };
        return new PartsPicture(pose, cell.Outline, turn, turn ? up : across, turn ? across : up, caption);
    }

    /// <summary>
    /// Where along a strut blank's length a compound end's bevel leaves the far face: Depth·tan β in
    /// from each such end, west ends from the start and east ends from the far end, in the blank's own
    /// frame (<c>docs/design/angled-parts.md</c> &#xA7;4). Display only, rounded once; none for a box.
    /// </summary>
    /// <param name="cell">The cell.</param>
    public static IEnumerable<Length> BevelOffsets(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        double depth = cell.Blank.Depth.ToInches(), length = cell.Blank.Width.ToInches();
        foreach (DerivedCompoundEnd end in cell.Row.CompoundEnds)
        {
            double inset = depth * Math.Tan(end.Bevel.Degrees * Math.PI / 180);
            yield return Length.FromInches(end.End == BlankEnd.West ? inset : length - inset, Rounding.HalfToEven);
        }
    }

    static string Thick(CutListRow row) => $"{CutListCsv.Text(row.Thickness)} thick";

    static Length Max(Length a, Length b) => a >= b ? a : b;

    static Length Min(Length a, Length b) => a <= b ? a : b;
}

/// <summary>
/// The Parts view's one common scale, with a legibility floor (docs/design/parts-view.md §2.1): the
/// largest piece fills its cell, every piece is drawn to that scale, and one too small to read at it
/// is drawn larger and says it is not to scale.
/// </summary>
/// <remarks>All sizes are pixels at 100 % zoom; a zoom multiplies both scales.</remarks>
public static class PartsScale
{
    /// <summary>A cell's width.</summary>
    public const double CellWidth = 256;

    /// <summary>The space kept at each side of a cell's drawing.</summary>
    public const double Inset = 16;

    /// <summary>The height a cell's drawing gets.</summary>
    public const double DrawingHeight = 100;

    /// <summary>The shortest a piece's longer edge is ever drawn.</summary>
    public const double FloorPixels = 48;

    /// <summary>The width a cell's drawing gets: the cell less its insets.</summary>
    public const double DrawingWidth = CellWidth - (2 * Inset);

    /// <summary>
    /// The sheet's scale, in pixels per inch: the largest at which every picture's longer extent fits
    /// the drawing's width and its shorter the drawing's height. Null for a sheet with nothing to draw.
    /// </summary>
    /// <param name="pictures">Every cell's picture.</param>
    public static double? Sheet(IEnumerable<PartsPicture> pictures)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        double? scale = null;
        foreach (PartsPicture picture in pictures)
        {
            double across = picture.Horizontal.ToInches(), up = picture.Vertical.ToInches();
            double fits = Math.Min(
                across > 0 ? DrawingWidth / across : double.PositiveInfinity,
                up > 0 ? DrawingHeight / up : double.PositiveInfinity);
            if (double.IsFinite(fits))
            {
                scale = scale is { } smaller ? Math.Min(smaller, fits) : fits;
            }
        }

        return scale;
    }

    /// <summary>
    /// The scale one cell draws at: the sheet's, unless its longer edge would come out under the
    /// floor, in which case the floor's — and then it is not to scale.
    /// </summary>
    /// <param name="picture">The cell's picture.</param>
    /// <param name="sheet">The sheet's scale.</param>
    public static (double PixelsPerInch, bool NotToScale) Cell(PartsPicture picture, double sheet)
    {
        ArgumentNullException.ThrowIfNull(picture);
        double longer = picture.Horizontal.ToInches();
        return longer > 0 && longer * sheet < FloorPixels
            ? (FloorPixels / longer, true)
            : (sheet, false);
    }
}

/// <summary>What a Parts view cell says in words (docs/design/parts-view.md §2.2).</summary>
public static class PartsCellText
{
    /// <summary>How many cut lines a cell shows before it says how many more there are.</summary>
    public const int CutLinesShown = 2;

    /// <summary>The count badge: "×4". Always shown — "×1" says it is the only one.</summary>
    public static string Badge(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return "×" + cell.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The line under the picture: the caption, the material if there is one, and "not to scale" when so.</summary>
    public static string Details(PartsCell cell, PartsPicture picture, bool notToScale)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(picture);
        IEnumerable<string> parts = [picture.Caption, cell.Row.MaterialText, notToScale ? "not to scale" : string.Empty];
        return string.Join(" · ", parts.Where(part => part.Length > 0));
    }

    /// <summary>
    /// What a screen reader says for a cell (§6): its name, its count, its three sizes and its material,
    /// e.g. "Leg, ×4, 1'-4 1/4" × 2 1/2" × 2 1/2"" — the same facts as its row in the cut list.
    /// </summary>
    public static string AutomationName(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        CutListRow row = cell.Row;
        string sizes = $"{row.LengthText} × {row.WidthText} × {row.ThicknessText}";
        string material = row.MaterialText;
        return material.Length == 0 ? $"{row.Label}, {Badge(cell)}, {sizes}" : $"{row.Label}, {Badge(cell)}, {sizes}, {material}";
    }

    /// <summary>
    /// When the badge and the selection count different things (§5.3): "4 pieces from 1 box" for one
    /// box that stands for four, "4 pieces from 2 boxes"; null when every box is one piece.
    /// </summary>
    public static string? PiecesFrom(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        int boxes = cell.Members.Length;
        return cell.Count == boxes ? null : $"{cell.Count} pieces from {boxes} {(boxes == 1 ? "box" : "boxes")}";
    }

    /// <summary>The cuts, the first two sentences, then "(+n more)" when there are more.</summary>
    public static ImmutableArray<string> Cuts(PartsCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ImmutableArray<string> all = cell.Row.CutText;
        if (all.Length <= CutLinesShown)
        {
            return all;
        }

        return [.. all.Take(CutLinesShown), $"(+{all.Length - CutLinesShown} more)"];
    }
}
