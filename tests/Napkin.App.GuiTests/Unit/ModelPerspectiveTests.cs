using System.Collections.Immutable;
using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What the 3D view's picking, culling, clipping and handles do under a perspective camera (#104):
/// the consumers of <see cref="Camera"/> that used to lean on every ray being parallel.
/// </summary>
/// <remarks>
/// <see cref="CameraPerspectiveTests"/> holds the projection itself. These hold what is built on it,
/// on the coffee table's real parts.
/// </remarks>
public class ModelPerspectiveTests
{
    static readonly Size Viewport = new(900, 600);

    static (Sketch Sketch, Camera Camera, ModelScene Scene) Table(double azimuth = 45, double elevation = 30)
    {
        Sketch sketch = SampleExpectations.Sample("coffee-table").Load().Sketch;
        Camera camera = (new Camera(azimuth, elevation, 0, 0, 0, 5, Viewport) with { Projection = CameraProjection.Perspective })
            .FitTo(Bounds3.Of(sketch), Viewport);
        return (sketch, camera, ModelScene.Of(sketch));
    }

    static Box Named(Sketch sketch, string name) => sketch.Entities.Values.OfType<Box>().Single(b => b.Name == name);

    [Theory]
    [InlineData(45, 30)]
    [InlineData(200, 20)]
    [InlineData(320, 45)]
    public void Picking_names_the_top_where_the_top_is_drawn_in_perspective(double azimuth, double elevation)
    {
        (Sketch sketch, Camera camera, ModelScene scene) = Table(azimuth, elevation);

        ModelPick pick = ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(24, 12, 17)), 5)!;

        Assert.Equal(Named(sketch, "Top").Id, pick.Box);
        Assert.Equal(BoxFace.Top, pick.Face);
    }

    [Fact]
    public void Picking_finds_the_leg_nearest_the_eye_and_not_one_behind_it()
    {
        // From the south-east the south-east leg is nearest; a click on its outer face is that leg's,
        // however the far legs line up behind it.
        (Sketch sketch, Camera camera, ModelScene scene) = Table(45, 20);
        Box leg = Named(sketch, "Leg, south-east");
        Vector3d onFace = new(46.5, 2.75, 8);      // the east face's middle, 16 1/4" tall

        ModelPick pick = ModelPicker.Pick(sketch, scene, camera, camera.Project(onFace), 5)!;

        Assert.Equal(leg.Id, pick.Box);
        Assert.Equal(BoxFace.East, pick.Face);
    }

    [Fact]
    public void A_pick_is_where_the_eye_ray_meets_the_face_not_a_point_on_the_centre_plane()
    {
        (Sketch sketch, Camera camera, ModelScene scene) = Table(45, 30);
        Vector3d target = new(24, 12, 17);

        ModelPick pick = ModelPicker.Pick(sketch, scene, camera, camera.Project(target), 5)!;

        Assert.Equal(target.X, pick.Point.X, 4);
        Assert.Equal(target.Y, pick.Point.Y, 4);
        Assert.Equal(target.Z, pick.Point.Z, 4);
        Assert.Equal(camera.DistanceAlongRay(target), pick.Distance, 4);
    }

    [Fact]
    public void Nothing_at_or_behind_the_eye_is_picked()
    {
        // A 10" cube at the origin, and an eye 25" north of it looking further north. The ray from the
        // eye, run backwards, goes through the cube; run forwards it meets nothing. An orthographic
        // ray would pick the cube (it starts on the centre plane, and parts in front of that are at
        // a negative distance); a ray from an eye must not.
        Box cube = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(10), Length.Inches(10), Length.Inches(10), BoxFace.Top, Angle.Zero);
        Sketch sketch = Sketch.Empty.WithEntity(cube);
        ModelScene scene = ModelScene.Of(sketch);
        Camera lookingNorth = new Camera(0, 0, 5, 60, 5, 40, Viewport) with { Projection = CameraProjection.Perspective };

        Assert.True(lookingNorth.Eye.Y > 10, "the eye must be north of the cube.");
        Assert.Equal(1.0, lookingNorth.ViewDirection.Y, 9);
        Assert.Null(ModelPicker.Pick(sketch, scene, lookingNorth, new Point(450, 300), 5));
        Assert.Null(ModelPicker.SurfaceAt(sketch, scene, lookingNorth, new Point(450, 300)));

        // Turned round to face it, the same click picks it.
        Camera facing = lookingNorth with { AzimuthDegrees = 180 };
        Assert.Equal(cube.Id, ModelPicker.Pick(sketch, scene, facing, new Point(450, 300), 5)!.Box);
    }

    [Fact]
    public void The_floor_under_the_pointer_is_found_from_above_and_not_from_below()
    {
        (_, Camera camera, _) = Table(45, 30);
        Vector3d ground = new(20, 10, 0);

        Vector3d? found = ModelPicker.FloorAt(camera, camera.Project(ground));

        Assert.NotNull(found);
        Assert.Equal(20, found!.Value.X, 5);
        Assert.Equal(10, found.Value.Y, 5);
        Assert.Equal(0, found.Value.Z, 5);

        // From below the floor there is no floor to put a point on, in front of the eye.
        Camera below = camera with { ElevationDegrees = -30 };
        Assert.Null(ModelPicker.FloorAt(below, below.Project(ground)));
    }

    [Fact]
    public void A_plane_hit_is_ahead_of_the_eye_or_it_is_not_a_hit()
    {
        (_, Camera camera, _) = Table(45, 30);
        Vector3d target = new(24, 12, 17);

        Vector3d? found = ModelPicker.PlaneAt(camera, camera.Project(target), Axis.Z, 17);
        Assert.NotNull(found);
        Assert.Equal(24, found!.Value.X, 5);

        // A plane far behind the eye, met only by the ray's extension backwards.
        Vector3d behind = camera.Eye + (camera.TowardViewer * 50);
        Assert.Null(ModelPicker.PlaneAt(camera, new Point(450, 300), Axis.Z, behind.Z + 10));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(6.5)]
    [InlineData(-4.0)]
    [InlineData(19.75)]
    public void A_drag_along_an_axis_reads_the_inches_back_out_of_the_pointer(double inches)
    {
        // Where the pointer is when a point has moved `inches` along the axis, read back through the
        // eye ray's closest approach to the axis line, is `inches` from the press: the drag is exact
        // however far from the centre it is, which a fixed pixels-per-inch is not in perspective.
        (_, Camera camera, _) = Table(45, 30);
        Vector3d through = new(10, 6, 4);
        Vector3d along = Vector3d.Along(Axis.X);

        double pressed = ModelPicker.ParameterAlongLine(camera, camera.Project(through), through, along)!.Value;
        double now = ModelPicker.ParameterAlongLine(camera, camera.Project(through + (along * inches)), through, along)!.Value;

        Assert.Equal(0, pressed, 6);
        Assert.Equal(inches, now - pressed, 6);
    }

    [Fact]
    public void A_drag_along_an_axis_that_points_at_the_eye_says_nothing()
    {
        (_, Camera camera, _) = Table(45, 30);

        // The direction the camera looks along: the pointer cannot say where on that line it is.
        Assert.Null(ModelPicker.ParameterAlongLine(camera, new Point(450, 300), camera.Center, camera.ViewDirection));
    }

    [Fact]
    public void A_polygon_that_crosses_the_near_plane_is_cut_and_the_cut_edge_is_not_drawn()
    {
        (_, Camera camera, _) = Table(0, 0);

        // A vertical square standing across the eye's line of sight, half of it in front of the eye and
        // half behind. The eye sits on the viewer's side of the centre, so put it there.
        Vector3d eye = camera.Eye;
        Vector3d across = camera.Right * 10;
        Vector3d ahead = camera.ViewDirection * 20;      // in front of the eye
        Vector3d behind = camera.TowardViewer * 20;      // behind it
        ScenePolygon polygon = new(
            EntityId.New(),
            null,
            [eye + behind - across, eye + ahead - across, eye + ahead + across, eye + behind + across],
            camera.Right,
            [true, true, true, true]);

        ScenePolygon? clipped = polygon.ClippedFor(camera);

        Assert.NotNull(clipped);
        Assert.All(clipped!.Points, point => Assert.True(camera.BeyondNearPlane(point) >= -1e-9, $"{point} is behind the near plane."));
        Assert.Equal(clipped.Points.Length, clipped.EdgeDrawn.Length);
        Assert.Contains(false, clipped.EdgeDrawn);      // the edge along the near plane is not a real edge
        Assert.Contains(true, clipped.EdgeDrawn);

        // Wholly behind the eye: nothing is left. Wholly ahead: the same polygon.
        ScenePolygon gone = polygon with { Points = [.. polygon.Points.Select(p => p + (camera.TowardViewer * 100))] };
        Assert.Null(gone.ClippedFor(camera));
        ScenePolygon fine = polygon with { Points = [.. polygon.Points.Select(p => p + (camera.ViewDirection * 100))] };
        Assert.Same(fine, fine.ClippedFor(camera));

        // An orthographic camera has no near plane and clips nothing.
        Assert.Same(polygon, polygon.ClippedFor(camera with { Projection = CameraProjection.Orthographic }));
    }

    [Fact]
    public void Whether_a_face_is_towards_the_eye_depends_on_where_the_eye_is_in_perspective_only()
    {
        // A wall well off to the west of the view whose face points east. Looking north-ish from the
        // south, an orthographic camera sees it edge-on and culls it; a perspective eye, east of the
        // wall, is in front of it and sees its face.
        Camera ortho = new(0, 10, 0, 0, 0, 5, Viewport);
        Camera persp = ortho with { Projection = CameraProjection.Perspective };
        ScenePolygon wall = new(
            EntityId.New(),
            null,
            [new(-100, 0, 0), new(-100, 40, 0), new(-100, 40, 20), new(-100, 0, 20)],
            new Vector3d(1, 0, 0),
            [true, true, true, true]);

        Assert.False(wall.FacesTowards(ortho));
        Assert.True(wall.FacesTowards(persp));

        // And the wall to the east, facing east, is turned away from that same eye.
        ScenePolygon east = wall with { Points = [.. wall.Points.Select(p => p + new Vector3d(200, 0, 0))] };
        Assert.False(east.FacesTowards(persp));
    }

    [Fact]
    public void The_scene_paints_far_to_near_in_perspective_too_the_near_leg_after_the_far_apron()
    {
        (Sketch sketch, Camera camera, ModelScene scene) = Table(45, 30);
        IReadOnlyList<ScenePolygon> order = scene.BackToFront(camera);

        Box nearLeg = Named(sketch, "Leg, south-east");
        Box farApron = Named(sketch, "Apron, long, north");
        int lastOfNear = order.Select((p, i) => (p, i)).Where(x => x.p.Box == nearLeg.Id).Max(x => x.i);
        int firstOfFar = order.Select((p, i) => (p, i)).Where(x => x.p.Box == farApron.Id).Min(x => x.i);

        Assert.True(firstOfFar < lastOfNear, "the far apron must be painted before the near leg's faces.");
        Assert.All(order, polygon => Assert.All(polygon.Points, p => Assert.True(camera.BeyondNearPlane(p) >= -1e-9)));
    }

    [Fact]
    public void Handles_follow_the_part_in_perspective_and_vanish_when_it_is_behind_the_eye()
    {
        (Sketch sketch, Camera camera, _) = Table(45, 30);
        Box leg = Named(sketch, "Leg, south-east");

        ImmutableArray<ModelHandle> handles = ModelHandles.Of(leg, camera);
        Assert.Equal(3, handles.Count(h => h.Kind == ModelHandleKind.Move));
        Assert.All(handles, h => Assert.True(h.At.X > -50 && h.At.X < 1000));

        // An eye placed 20" beyond the leg, on the viewer's side, looking away from it: the leg is
        // behind the eye, so it has no picture and no handles.
        Camera probe = camera with { AzimuthDegrees = 225, ElevationDegrees = 0, PixelsPerInch = 300 };
        Vector3d legCentre = ModelHandles.Centre(leg);
        Vector3d centre = legCentre - (probe.TowardViewer * (probe.EyeDistance + 20));
        Camera behindEye = probe with { CenterX = centre.X, CenterY = centre.Y, CenterZ = centre.Z };

        Assert.False(behindEye.IsInFront(legCentre));
        Assert.True(ModelHandles.Of(leg, behindEye).IsEmpty);
    }
}
