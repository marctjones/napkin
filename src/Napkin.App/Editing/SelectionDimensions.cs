using System.Diagnostics.CodeAnalysis;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Editing;

/// <summary>Which of a part's two sizes a dimension on screen is about.</summary>
public enum SizeAxis
{
    /// <summary>The part's width, along its own X.</summary>
    Width,

    /// <summary>The part's height, along its own Y.</summary>
    Height,
}

/// <summary>
/// The width and height a selected part shows, as real dimension graphics.
/// </summary>
/// <remarks>
/// <para>
/// A selected part is measured whether or not anybody has annotated it. When the drawing already
/// has a <see cref="Dimension"/> over that size, <em>that</em> is what is shown and what typing
/// edits; otherwise a dimension is worked out on the fly and shown in the selection's own colour.
/// The on-the-fly one is never added to the sketch — it is a way of showing a size, not a
/// statement about the drawing, and the drawing states nothing it was not told (design &#xA7;3.2).
/// </para>
/// <para>
/// Both cases go through <see cref="DimensionLayout.TryMeasure"/>, so the label a person clicks
/// and the label the canvas draws are the same arithmetic, and the value is read from the
/// geometry every time — which is CVS-007 for free: while a drag is running the sketch changes on
/// every pointer move, so the text changes with it.
/// </para>
/// </remarks>
public static class SelectionDimensions
{
    /// <summary>The id an on-the-fly width dimension carries, so tests can name it.</summary>
    public static readonly EntityId WidthMarker = new(new Guid("00000000-0000-0000-0000-00000000da01"));

    /// <summary>The id an on-the-fly height dimension carries.</summary>
    public static readonly EntityId HeightMarker = new(new Guid("00000000-0000-0000-0000-00000000da02"));

    /// <summary>The size a selected part's dimension measures, and the reference that names it.</summary>
    public static ParamRef ParamFor(EntityId box, SizeAxis axis) =>
        axis == SizeAxis.Width ? new BoxWidthRef(box) : new BoxHeightRef(box);

    /// <summary>
    /// The dimension shown for one of a selected part's sizes.
    /// </summary>
    /// <param name="sketch">The drawing.</param>
    /// <param name="boxId">The selected part.</param>
    /// <param name="axis">Which size.</param>
    /// <param name="offset">How far off the part the dimension line sits.</param>
    /// <param name="measurement">What to draw, and what it reads.</param>
    /// <param name="annotated">
    /// Whether the dimension is one the drawing holds (true) or one worked out for the selection
    /// (false).
    /// </param>
    /// <returns><see langword="false"/> when the part is not a box, or has no size to show.</returns>
    public static bool TryFor(
        Sketch sketch,
        EntityId boxId,
        SizeAxis axis,
        Length offset,
        [NotNullWhen(true)] out DimensionMeasurement? measurement,
        out bool annotated)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        measurement = null;
        annotated = false;

        if (sketch.Find<Box>(boxId) is null)
        {
            return false;
        }

        ParamRef size = ParamFor(boxId, axis);

        foreach (Dimension existing in sketch.Entities.Values.OfType<Dimension>().OrderBy(entity => entity.Id))
        {
            if (existing.Measures is ParamMeasurand param
                && param.Param == size
                && DimensionLayout.TryMeasure(sketch, existing, out DimensionMeasurement? found))
            {
                measurement = found;
                annotated = true;
                return true;
            }
        }

        Dimension transient = new(
            axis == SizeAxis.Width ? WidthMarker : HeightMarker,
            sketch.Layers[0].Id,
            new ParamMeasurand(size),
            Drives: DimensionEntry.DrivingRelationship(sketch, size),
            new DimensionPlacement(offset, axis == SizeAxis.Width ? DimensionSide.South : DimensionSide.West));

        return DimensionLayout.TryMeasure(sketch, transient, out measurement);
    }
}
