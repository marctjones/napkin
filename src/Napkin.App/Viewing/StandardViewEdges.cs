using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// The faces a standard view draws, in the painter's order, with their edges split into what the eye
/// sees and what a nearer part hides (docs/design/standard-views.md §2.3): <see cref="ModelScene"/>'s
/// polygons put on the view plane for <see cref="HiddenEdges"/>, and its pieces put back in the world
/// for the camera to project.
/// </summary>
/// <param name="Polygons">The faces turned to the eye, back to front; a segment's face indexes this list.</param>
/// <param name="Edges">Every real edge of them, visible or hidden.</param>
/// <param name="View">The view they were split for.</param>
public sealed record StandardViewEdges(IReadOnlyList<ScenePolygon> Polygons, EdgeSplit Edges, StandardView View)
{
    /// <summary>The faces and edges of a scene as a standard view, looking along the camera, sees them.</summary>
    public static StandardViewEdges Of(ModelScene scene, Camera camera, StandardView view)
    {
        (Vector3d right, Vector3d up, Vector3d toward) = StandardViews.Axes(view);
        IReadOnlyList<ScenePolygon> polygons = scene.BackToFront(camera);
        FlatFace[] faces =
        [
            .. polygons.Select(polygon => new FlatFace(
                [.. polygon.Points.Select(point => new FlatVertex(Vector3d.Dot(point, right), Vector3d.Dot(point, up), Vector3d.Dot(point, toward)))],
                polygon.EdgeDrawn)),
        ];
        return new StandardViewEdges(polygons, HiddenEdges.Split(faces), view);
    }

    /// <summary>A point of the view plane back in the world, where the camera projects it.</summary>
    public Vector3d InWorld(FlatVertex vertex)
    {
        (Vector3d right, Vector3d up, Vector3d toward) = StandardViews.Axes(View);
        return (right * vertex.U) + (up * vertex.V) + (toward * vertex.Nearness);
    }
}
