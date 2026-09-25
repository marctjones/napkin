using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>
/// Putting a window or door into a wall: the opening box and the relationships that bind it there
/// (issue #18, BLD-002).
/// </summary>
/// <remarks>
/// The opening is held <see cref="Flush"/> on both of the wall's long faces, its plan thickness
/// equal to the wall's, and an <see cref="AxisDistance"/> from the wall's start corner to its own —
/// so it moves with the wall, follows a change in the wall's thickness, and slides along the wall
/// when that distance changes. Nothing ties its sill or height; those are the person's.
/// </remarks>
public static class OpeningPlacement
{
    /// <summary>
    /// The request that adds an opening to a wall, its near side <paramref name="offset"/> along it.
    /// </summary>
    /// <param name="wall">The wall.</param>
    /// <param name="layer">The layer the opening goes on.</param>
    /// <param name="id">The new opening's id.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="offset">How far along the wall, from its start, the opening's near side is.</param>
    /// <param name="width">The rough opening's width.</param>
    /// <param name="sill">Its sill above the wall's bottom; zero for a door.</param>
    /// <param name="height">The rough opening's height.</param>
    public static Request Request(
        Wall wall,
        LayerId layer,
        EntityId id,
        string name,
        Length offset,
        Length width,
        Length sill,
        Length height)
    {
        ArgumentNullException.ThrowIfNull(wall);
        ArgumentNullException.ThrowIfNull(name);

        Box w = wall.Box;
        Point3 anchor = w.World(new Vector3(offset, Length.Zero, sill));
        Box opening = new Box(id, layer, anchor, width, w.Height, height, w.FaceUp, w.Rotation) with { Name = name };

        (Axis along, bool positive) = w.Orientation.Image(Axis.X);
        BoxFeature corner = BoxFeature.Edge(BoxFace.South, BoxFace.West);

        ImmutableList<Request> requests =
        [
            new AddEntity(opening),
            new AddRelationship(new Flush(
                RelationshipId.New(),
                new FeatureRef(w.Id, BoxFeature.Face(BoxFace.South)),
                new FeatureRef(id, BoxFeature.Face(BoxFace.South)))),
            new AddRelationship(new Flush(
                RelationshipId.New(),
                new FeatureRef(w.Id, BoxFeature.Face(BoxFace.North)),
                new FeatureRef(id, BoxFeature.Face(BoxFace.North)))),
            new AddRelationship(new EqualParam(RelationshipId.New(), new BoxHeightRef(w.Id), new BoxHeightRef(id))),
            new AddRelationship(new AxisDistance(
                RelationshipId.New(),
                new FeatureRef(w.Id, corner),
                new FeatureRef(id, corner),
                along,
                positive ? offset : -offset)),
        ];

        return new Batch(requests);
    }

    /// <summary>
    /// Where along a wall a click lands, as the near side of an opening of this width centred on it,
    /// kept inside the wall; null when the click is not on the wall's plan.
    /// </summary>
    public static Length? OffsetAt(Wall wall, Point2 click, Length width)
    {
        ArgumentNullException.ThrowIfNull(wall);
        if (wall.Local(wall.Box) is null)
        {
            return null;
        }

        Vector3 local = wall.Box.Orientation.Unapply(new Point3(click.X, click.Y, wall.Box.Anchor.Z) - wall.Box.Anchor);
        if (local.Dx < Length.Zero || local.Dx > wall.Length || local.Dy < Length.Zero || local.Dy > wall.Thickness)
        {
            return null;
        }

        Length offset = local.Dx - width.Divide(2, Rounding.HalfToEven);
        return Length.Max(Length.Zero, Length.Min(offset, wall.Length - width));
    }
}
