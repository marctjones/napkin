using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The plan names the tests were written in, as the features the model refers to since
/// docs/design/assembly-model.md §10 step 3: a corner of the blank is its local upright, and an edge
/// of the blank is the side face of the same name. For a box lying as drawn both mean what they
/// always meant.
/// </summary>
internal static class TestRefs
{
    /// <summary>A corner of the blank: its local upright.</summary>
    public static FeatureRef Corner(EntityId box, BoxCorner corner) => new(box, BoxFeature.LocalUpright(corner));

    /// <summary>An edge of the blank: the side face of the same name.</summary>
    public static FeatureRef Edge(EntityId box, BoxEdge edge) => new(box, BoxFeature.Face(FaceOf(edge)));

    /// <summary>The side face a plan edge of a box lying as drawn is.</summary>
    public static BoxFace FaceOf(BoxEdge edge) => edge switch
    {
        BoxEdge.South => BoxFace.South,
        BoxEdge.East => BoxFace.East,
        BoxEdge.North => BoxFace.North,
        BoxEdge.West => BoxFace.West,
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Not a plan edge."),
    };

    /// <summary>Where a place is in the plan; it must fix both X and Y.</summary>
    public static Point2 PlanPoint(this Sketch sketch, PlaceRef reference)
    {
        Place place = sketch.PlaceOf(reference);
        return new Point2(place[Axis.X], place[Axis.Y]);
    }

    /// <summary>
    /// The plan line an edge reference draws: a segment's two nodes, or the two corners of a box
    /// lying as drawn that a side face runs between.
    /// </summary>
    public static (Point2 From, Point2 To) PlanLine(this Sketch sketch, PlaceRef reference)
    {
        switch (reference)
        {
            case SegmentRef segmentRef:
            {
                Segment segment = sketch.Find<Segment>(segmentRef.Segment)!;
                return (sketch.Find<Node>(segment.Start)!.Position, sketch.Find<Node>(segment.End)!.Position);
            }

            case FeatureRef { Feature.Faces: [var face] } feature:
            {
                Box box = sketch.Find<Box>(feature.Box)!;
                BoxEdge side = face switch
                {
                    BoxFace.South => BoxEdge.South,
                    BoxFace.East => BoxEdge.East,
                    BoxFace.North => BoxEdge.North,
                    BoxFace.West => BoxEdge.West,
                    _ => throw new ArgumentException($"{reference} is not a side face.", nameof(reference)),
                };

                (BoxCorner from, BoxCorner to) = Box.Ends(side);
                return (box.Corner(from), box.Corner(to));
            }

            default:
                throw new ArgumentException($"{reference} draws no line in the plan.", nameof(reference));
        }
    }
}
