using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// How much room a sketch takes up in model space.
/// </summary>
/// <remarks>
/// This is what zoom-to-fit frames, so it counts a dimension's graphics as well as the parts they
/// measure: a dimension line sitting six inches off the bottom edge is part of the drawing, and a
/// fit that clipped it would be a fit a person immediately undid.
/// </remarks>
public static class SketchExtents
{
    /// <summary>The bounding box of everything drawable in a sketch.</summary>
    public static WorldBounds Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        WorldBounds bounds = WorldBounds.Empty;
        foreach (Entity entity in sketch.Entities.Values.OrderBy(item => item.Id))
        {
            switch (entity)
            {
                case Box box:
                {
                    // What the plan shows of a box is its footprint (assembly-model §7.1).
                    Footprint footprint = box.Footprint();
                    bounds = bounds.Including(
                    [
                        footprint.Corner(BoxCorner.SouthWest),
                        footprint.Corner(BoxCorner.SouthEast),
                        footprint.Corner(BoxCorner.NorthEast),
                        footprint.Corner(BoxCorner.NorthWest),
                    ]);
                    break;
                }

                case Node node:
                    bounds = bounds.Including(node.Position);
                    break;

                case Note note:
                    bounds = bounds.Including(note.Position);
                    break;

                // An angled part's plan outline, which is where its ends and its thickness reach.
                case Strut strut:
                    foreach ((double x, double y) in StrutSolid.PlanOutline(strut))
                    {
                        bounds = bounds.Including(new Point2(Length.FromInches(x, Rounding.HalfToEven), Length.FromInches(y, Rounding.HalfToEven)));
                    }

                    break;

                case Segment segment
                    when sketch.Find<Node>(segment.Start) is { } start
                         && sketch.Find<Node>(segment.End) is { } end:
                    bounds = bounds.Including([start.Position, end.Position]);
                    break;

                case Dimension dimension
                    when DimensionLayout.TryMeasure(sketch, dimension, out DimensionMeasurement? measurement):
                    bounds = bounds.Including([measurement.LineFrom, measurement.LineTo]);
                    break;
            }
        }

        return bounds;
    }
}
