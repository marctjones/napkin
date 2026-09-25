using System.Diagnostics.CodeAnalysis;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// A dimension worked out in model space: what it measures, where its graphics go, and what it
/// says.
/// </summary>
/// <remarks>
/// Every coordinate here is an exact <see cref="Length"/>; nothing has met the screen yet. The
/// canvas converts these points through <see cref="ViewTransform"/> when it draws them, and the
/// same measurement is what a test reads to check a label — so the string a person sees and the
/// string a test asserts are produced by one line of code, not two.
/// </remarks>
/// <param name="Dimension">The dimension entity this came from.</param>
/// <param name="From">One end of what is measured.</param>
/// <param name="To">The other end of what is measured.</param>
/// <param name="LineFrom">Where the dimension line starts, offset from <paramref name="From"/>.</param>
/// <param name="LineTo">Where the dimension line ends, offset from <paramref name="To"/>.</param>
/// <param name="Axis">The axis the measurement runs along.</param>
/// <param name="Value">
/// The measured length, always positive. Taken from the geometry every time it is asked for; a
/// dimension never stores a length of its own (docs/design/geometry-model.md &#xA7;3.3).
/// </param>
public sealed record DimensionMeasurement(
    Dimension Dimension,
    Point2 From,
    Point2 To,
    Point2 LineFrom,
    Point2 LineTo,
    Axis Axis,
    Length Value)
{
    /// <summary>The midpoint of the dimension line, where the label is centred.</summary>
    public Point2 LabelAnchor => new(
        Midpoint(LineFrom.X, LineTo.X),
        Midpoint(LineFrom.Y, LineTo.Y));

    /// <summary>
    /// What the dimension reads, at a given precision, and whether that text is the whole truth.
    /// </summary>
    public FormattedLength Format(LengthFormat format) => Value.Format(format);

    /// <summary>
    /// What the dimension reads, with the &#x2248; marker when the text is not the stored value.
    /// </summary>
    /// <remarks>
    /// Nothing rounds silently: <see cref="FormattedLength.IsExact"/> false means the displayed
    /// value is not the stored one, and the canvas says so with a leading &#x2248;
    /// (docs/design/geometry-model.md &#xA7;1.4). With the rectilinear geometry M1 draws this never
    /// fires; it fires the day a solver or decimal entry puts a value between two 1/16ths.
    /// </remarks>
    public string Label(LengthFormat format)
    {
        FormattedLength formatted = Format(format);
        return formatted.IsExact ? formatted.Text : "≈" + formatted.Text;
    }

    static Length Midpoint(Length a, Length b) => a + (b - a).Divide(2, Rounding.HalfToEven);
}

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
    /// <summary>Every dimension in a sketch that can be drawn, in entity id order.</summary>
    /// <remarks>
    /// A dimension whose measurand dangles is skipped rather than thrown over: a viewer's job when
    /// handed a sketch it cannot fully draw is to draw what it can. The reader (#6) refuses such a
    /// file long before it reaches the canvas, so this is a belt on top of braces.
    /// </remarks>
    public static IEnumerable<DimensionMeasurement> Measure(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        foreach (Dimension dimension in sketch.Entities.Values
                     .OfType<Dimension>()
                     .OrderBy(entity => entity.Id))
        {
            if (TryMeasure(sketch, dimension, out DimensionMeasurement? measurement))
            {
                yield return measurement;
            }
        }
    }

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

    /// <summary>Works out one dimension's geometry and value from the sketch it lives in.</summary>
    /// <returns><see langword="false"/> when the dimension cannot be drawn.</returns>
    public static bool TryMeasure(
        Sketch sketch,
        Dimension dimension,
        [NotNullWhen(true)] out DimensionMeasurement? measurement)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(dimension);

        measurement = null;
        if (!TryResolve(sketch, dimension, out Point2 from, out Point2 to, out Axis axis, out Length value))
        {
            return false;
        }

        (Point2 lineFrom, Point2 lineTo) = DimensionLine(from, to, axis, dimension.Placement);
        measurement = new DimensionMeasurement(dimension, from, to, lineFrom, lineTo, axis, value);
        return true;
    }

    static bool TryResolve(
        Sketch sketch,
        Dimension dimension,
        out Point2 from,
        out Point2 to,
        out Axis axis,
        out Length value)
    {
        from = to = Point2.Origin;
        axis = Axis.X;
        value = Length.Zero;

        switch (dimension.Measures)
        {
            case ParamMeasurand { Param: BoxWidthRef width }:
            {
                // A width the box's orientation stands vertical has nowhere to be drawn in the plan
                // (docs/design/assembly-model.md §7.3).
                if (sketch.Find<Box>(width.Box) is not { } box || !LiesAlong(box, Axis.X, Axis.X))
                {
                    return false;
                }

                // Measured along whichever side of the footprint the dimension sits beside, so its
                // extension lines run from the near side outwards instead of across the part.
                Footprint footprint = box.Footprint();
                bool above = dimension.Placement.Side == DimensionSide.North;
                from = footprint.Corner(above ? BoxCorner.NorthWest : BoxCorner.SouthWest);
                to = footprint.Corner(above ? BoxCorner.NorthEast : BoxCorner.SouthEast);
                value = box.Width;
                break;
            }

            case ParamMeasurand { Param: BoxHeightRef height }:
            {
                if (sketch.Find<Box>(height.Box) is not { } box || !LiesAlong(box, Axis.Y, Axis.Y))
                {
                    return false;
                }

                Footprint footprint = box.Footprint();
                bool right = dimension.Placement.Side == DimensionSide.East;
                from = footprint.Corner(right ? BoxCorner.SouthEast : BoxCorner.SouthWest);
                to = footprint.Corner(right ? BoxCorner.NorthEast : BoxCorner.NorthWest);
                value = box.Height;
                break;
            }

            case ParamMeasurand { Param: SegmentLengthRef length }:
            {
                if (sketch.Find<Segment>(length.Segment) is not { } segment
                    || sketch.Find<Node>(segment.Start) is not { } start
                    || sketch.Find<Node>(segment.End) is not { } end)
                {
                    return false;
                }

                from = start.Position;
                to = end.Position;
                value = (to - from).Magnitude();
                break;
            }

            case AxisMeasurand measurand:
            {
                if (!TryPoint(sketch, measurand.From, out from) || !TryPoint(sketch, measurand.To, out to))
                {
                    return false;
                }

                axis = measurand.Axis;
                value = Length.Abs((to - from).Component(measurand.Axis));
                return value > Length.Zero;
            }

            default:
                return false;
        }

        // For a size, the axis is whichever way the two ends actually lie — which is how a box
        // rotated a quarter turn gets a vertical width dimension without a special case.
        Vector2 span = to - from;
        axis = Length.Abs(span.Dx) >= Length.Abs(span.Dy) ? Axis.X : Axis.Y;
        return value > Length.Zero;
    }

    /// <summary>
    /// Whether a size along a local axis lies along a plan axis of the footprint, before the spin —
    /// which is what lets it be drawn between the footprint's corners.
    /// </summary>
    static bool LiesAlong(Box box, Axis local, Axis plan)
        => new Orientation(box.FaceUp, Angle.Zero).Image(local).Axis == plan;

    // A place the plan can draw a dimension between: one that fixes both X and Y — a node, a centre,
    // an upright edge (docs/design/assembly-model.md §2.2).
    static bool TryPoint(Sketch sketch, PlaceRef reference, out Point2 point)
    {
        point = Point2.Origin;
        bool resolves = reference switch
        {
            NodeRef node => sketch.Find<Node>(node.Node) is not null,
            CenterRef or FeatureRef => sketch.Find<Box>(reference.Owner) is not null,
            _ => false,
        };

        if (resolves
            && (reference is not FeatureRef feature || !feature.Feature.Faces.IsEmpty)
            && sketch.PlaceOf(reference) is { X: { } x, Y: { } y })
        {
            point = new Point2(x, y);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Where the dimension line sits: parallel to what is measured, offset to the side the
    /// placement names.
    /// </summary>
    /// <remarks>
    /// A side that is parallel to the measurement — "north" on a horizontal measurement's twin,
    /// say — cannot be honoured without drawing the line through the geometry, so it falls back to
    /// the sensible side for that axis. Placement is canvas-only data; getting it wrong moves a
    /// line, never a part.
    /// </remarks>
    static (Point2 From, Point2 To) DimensionLine(
        Point2 from,
        Point2 to,
        Axis axis,
        DimensionPlacement placement)
    {
        Length offset = placement.Offset;
        if (axis is not (Axis.X or Axis.Y))
        {
            // A dimension is drawn in the plan (docs/design/assembly-model.md §7.3); one along Z
            // has nowhere to go, and drawing it as a Y dimension would be a silent lie.
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "A plan dimension runs along X or Y.");
        }

        if (axis == Axis.X)
        {
            bool below = StandardViewFrame.OnLowSide(Axis.X, placement.Side);
            Length baseline = below
                ? Length.Min(from.Y, to.Y) - offset
                : Length.Max(from.Y, to.Y) + offset;
            return (new Point2(from.X, baseline), new Point2(to.X, baseline));
        }
        else
        {
            bool left = StandardViewFrame.OnLowSide(Axis.Y, placement.Side);
            Length baseline = left
                ? Length.Min(from.X, to.X) - offset
                : Length.Max(from.X, to.X) + offset;
            return (new Point2(baseline, from.Y), new Point2(baseline, to.Y));
        }
    }
}
