using System.Diagnostics.CodeAnalysis;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>A point of a standard view's plane: along screen right and along screen up, in inches of world coordinate.</summary>
/// <param name="Along">The world coordinate along the view's screen-right axis, signed as the screen runs.</param>
/// <param name="Across">The world coordinate along the view's screen-up axis, signed likewise.</param>
public readonly record struct ViewPoint(double Along, double Across);

/// <summary>
/// A dimension as a standard view draws it (docs/design/standard-views.md §3.2): the plan's
/// measurement — its value and its label, unchanged — laid out in the view's own coordinates.
/// </summary>
/// <param name="Measurement">The plan's measurement: what is measured, its value, its label.</param>
/// <param name="View">The view it is laid out for.</param>
/// <param name="From">One measured end, at the edge of what is measured nearest the line.</param>
/// <param name="To">The other measured end.</param>
/// <param name="LineFrom">Where the dimension line starts.</param>
/// <param name="LineTo">Where it ends.</param>
public sealed record ViewDimension(
    DimensionMeasurement Measurement,
    StandardView View,
    ViewPoint From,
    ViewPoint To,
    ViewPoint LineFrom,
    ViewPoint LineTo)
{
    /// <summary>What it reads: the plan's label, the same line of code (§3.2).</summary>
    public string Label(LengthFormat format) => Measurement.Label(format);

    /// <summary>A point of the view's plane in the world, where an orthographic camera looking along the view projects it.</summary>
    public Vector3d InWorld(ViewPoint point)
    {
        (Vector3d right, Vector3d up, _) = StandardViews.Axes(View);
        return (right * point.Along) + (up * point.Across);
    }
}

/// <summary>
/// Turns the <see cref="Dimension"/> entities of a sketch into drawable measurements.
/// </summary>
/// <remarks>
/// Pure and UI-free on purpose. The canvas calls it to draw; a test calls it to read what a label
/// says without rasterising anything and without a second implementation of the same arithmetic.
/// </remarks>
public static class DimensionLayout
{
    /// <summary>Every dimension in a sketch that can be drawn, in entity id order (<see cref="PlanDimensions.Measure"/>).</summary>
    public static IEnumerable<DimensionMeasurement> Measure(Sketch sketch) => PlanDimensions.Measure(sketch);

    /// <summary>Works out one dimension's geometry and value (<see cref="PlanDimensions.TryMeasure"/>).</summary>
    public static bool TryMeasure(Sketch sketch, Dimension dimension, [NotNullWhen(true)] out DimensionMeasurement? measurement)
        => PlanDimensions.TryMeasure(sketch, dimension, out measurement);

    /// <summary>
    /// The dimensions a standard view shows, laid out in its coordinates (standard-views §3): those
    /// along its screen-right or screen-up axis. Top and Bottom lay the plan's own layout out as seen
    /// from their side; an elevation puts the line <see cref="DimensionPlacement.Offset"/> below or
    /// above the measured thing's extent in Z, as its side says.
    /// </summary>
    public static IEnumerable<ViewDimension> Measure(Sketch sketch, StandardView view)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        (Vector3d right, Vector3d up, _) = StandardViews.Axes(view);
        ViewPoint Seen(Point2 point)
        {
            Vector3d world = new(point.X.ToInches(), point.Y.ToInches(), 0);
            return new ViewPoint(Vector3d.Dot(world, right), Vector3d.Dot(world, up));
        }

        foreach (DimensionMeasurement measurement in Measure(sketch))
        {
            if (StandardViewFrame.AcrossFor(view, measurement.Axis) is not { } across)
            {
                continue;
            }

            if (across != Axis.Z)
            {
                yield return new ViewDimension(measurement, view, Seen(measurement.From), Seen(measurement.To), Seen(measurement.LineFrom), Seen(measurement.LineTo));
                continue;
            }

            // An elevation: screen up is Z. Along is the measured coordinate, signed as the screen runs.
            (double low, double high) = HeightOf(sketch, measurement.Dimension);
            bool lowSide = StandardViewFrame.OnLowSide(measurement.Axis, measurement.Dimension.Placement.Side);
            double offset = measurement.Dimension.Placement.Offset.ToInches();
            double edge = lowSide ? low : high;
            double line = lowSide ? low - offset : high + offset;
            double a = Seen(measurement.From).Along, b = Seen(measurement.To).Along;
            yield return new ViewDimension(measurement, view, new(a, edge), new(b, edge), new(a, line), new(b, line));
        }
    }

    /// <summary>
    /// How far down and up in Z the thing a dimension measures reaches: a box's own height for its size;
    /// for a distance between two places, each place's Z where it fixes one and its box's height where
    /// it does not (an upright edge); nothing above the floor for a plan segment.
    /// </summary>
    static (double Low, double High) HeightOf(Sketch sketch, Dimension dimension)
    {
        List<double> heights = [];
        void AddBox(EntityId id)
        {
            if (sketch.Find<Box>(id) is { } box)
            {
                foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
                {
                    heights.Add(box.Vertex(corner, BoxLevel.Bottom).Z.ToInches());
                    heights.Add(box.Vertex(corner, BoxLevel.Top).Z.ToInches());
                }
            }
        }

        switch (dimension.Measures)
        {
            case ParamMeasurand { Param: BoxWidthRef width }:
                AddBox(width.Box);
                break;
            case ParamMeasurand { Param: BoxHeightRef height }:
                AddBox(height.Box);
                break;
            case AxisMeasurand measurand:
                foreach (PlaceRef end in (PlaceRef[])[measurand.From, measurand.To])
                {
                    if (sketch.PlaceOf(end) is { Z: { } z })
                    {
                        heights.Add(z.ToInches());
                    }
                    else
                    {
                        AddBox(end.Owner);
                    }
                }

                break;
        }

        return heights.Count == 0 ? (0, 0) : (heights.Min(), heights.Max());
    }
}
