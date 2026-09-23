using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// What a box looks like from above: the one rule the plan view draws and hit-tests a box by
/// (<c>docs/design/assembly-model.md</c> &#xA7;7.2).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Box.Outline"/> is in the blank's own frame. The plan places it by the box's
/// orientation, three ways: with <see cref="BoxFace.Top"/> up the plan sees the cap as drawn,
/// moved to the anchor and spun; with <see cref="BoxFace.Bottom"/> up it sees the same cap
/// mirrored north to south, which is what turning a board over does; with a side up it sees the
/// solid's silhouette, the footprint rectangle, on which no cut shows, because every cut runs the
/// full depth and the plan is now looking along one.
/// </para>
/// <para>
/// A point is tested against a cap in the blank's own frame, not the plan's, so that the outline
/// keeps the counter-clockwise walk <see cref="OutlineHitTest"/> reads curves by — a mirrored walk
/// would turn every inward curve outward.
/// </para>
/// </remarks>
public static class PlanShape
{
    /// <summary>
    /// Whether the plan sees the blank's cap — <see cref="BoxFace.Top"/> or
    /// <see cref="BoxFace.Bottom"/> up — and so its cuts, rather than the rectangle of a side.
    /// </summary>
    public static bool ShowsCap(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box.FaceUp is BoxFace.Top or BoxFace.Bottom;
    }

    /// <summary>Where a point of the blank's own XY frame is seen in the plan.</summary>
    public static Point2 ToPlan(Box box, Point2 local)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box.World(new Vector3(local.X, local.Y, Length.Zero)).XY;
    }

    /// <summary>
    /// A plan point in the blank's own XY frame — the inverse of <see cref="ToPlan"/> for a box
    /// whose cap the plan sees.
    /// </summary>
    public static Point2 ToLocal(Box box, Point2 plan)
    {
        ArgumentNullException.ThrowIfNull(box);
        Vector3 local = box.Orientation.Unapply(new Vector3(plan.X - box.Anchor.X, plan.Y - box.Anchor.Y, Length.Zero));
        return new Point2(local.Dx, local.Dy);
    }

    /// <summary>
    /// The boundary the plan draws for a box, in plan coordinates: the cap placed by the
    /// orientation, or the footprint rectangle when a side is up.
    /// </summary>
    public static Outline Outline(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (!ShowsCap(box))
        {
            Footprint footprint = box.Footprint();
            Point2[] corners =
            [
                footprint.Corner(BoxCorner.SouthWest),
                footprint.Corner(BoxCorner.SouthEast),
                footprint.Corner(BoxCorner.NorthEast),
                footprint.Corner(BoxCorner.NorthWest),
            ];

            return new Outline([.. corners.Select((corner, i) => (OutlineSegment)new StraightSegment(corner, corners[(i + 1) % 4]))]);
        }

        Point2 Place(Point2 local) => ToPlan(box, local);

        ImmutableArray<OutlineSegment> placed =
        [
            .. box.Outline().Segments.Select(segment => segment switch
            {
                ArcByCenter arc => (OutlineSegment)new ArcByCenter(Place(arc.From), Place(arc.To), Place(arc.Center)),
                ArcThrough arc => new ArcThrough(Place(arc.From), Place(arc.Through), Place(arc.To)),
                _ => new StraightSegment(Place(segment.From), Place(segment.To)),
            }),
        ];

        return new Outline(placed);
    }

    /// <summary>
    /// Whether a plan point is on what the plan sees of the box: inside the cut outline when the
    /// cap is up, inside the footprint when a side is.
    /// </summary>
    public static bool Contains(Box box, Point2 plan)
    {
        ArgumentNullException.ThrowIfNull(box);

        return ShowsCap(box) && !box.Cuts.IsEmpty
            ? OutlineHitTest.Contains(box.Outline(), ToLocal(box, plan))
            : BoxGeometry.Contains(box, plan);
    }
}
