using Avalonia;
using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The perspective camera (#104, <c>docs/design/assembly-model.md</c> §11 decision 10), tested
/// without a window: what a pinhole projection must do that an orthographic one must not.
/// </summary>
/// <remarks>
/// <see cref="CameraTests"/> holds the orthographic camera and is untouched by perspective: a
/// camera is orthographic unless it is asked otherwise. These are the tests that fail when the eye
/// distance, the divide by depth or a sign is wrong, and the ones that say what the two projections
/// share (the centre plane) and where they part (everything off it).
/// </remarks>
public class CameraPerspectiveTests
{
    static readonly Size Viewport = new(900, 600);

    static Camera Ortho(double azimuth = 30, double elevation = 20, double ppi = 9) =>
        new(azimuth, elevation, 12, -6, 4, ppi, Viewport);

    static Camera Persp(double azimuth = 30, double elevation = 20, double ppi = 9) =>
        Ortho(azimuth, elevation, ppi) with { Projection = CameraProjection.Perspective };

    static double Pixels(Vector v) => Math.Sqrt((v.X * v.X) + (v.Y * v.Y));

    static double DistanceFromLine(Vector3d point, Vector3d origin, Vector3d direction)
    {
        Vector3d toPoint = point - origin;
        double along = Vector3d.Dot(toPoint, direction);
        Vector3d perpendicular = toPoint - (direction * along);
        return Math.Sqrt(Vector3d.Dot(perpendicular, perpendicular));
    }

    [Fact]
    public void A_camera_is_orthographic_unless_it_is_asked_otherwise()
    {
        Assert.Equal(CameraProjection.Orthographic, Ortho().Projection);
        Assert.False(Ortho().IsPerspective);
        Assert.Equal(CameraProjection.Orthographic, Camera.Isometric(Viewport).Projection);
        Assert.Equal(double.PositiveInfinity, Ortho().EyeDistance);
    }

    [Fact]
    public void The_eye_is_where_the_field_of_view_says_and_is_on_the_viewers_side()
    {
        Camera camera = Persp();
        double halfViewportInches = (Viewport.Height / 2.0) / camera.PixelsPerInch;

        // The half field of view is the angle at the eye that the viewport's half height subtends at
        // the centre plane: tan(fov/2) = (half height in inches) / (eye distance).
        Assert.Equal(halfViewportInches / Math.Tan(22.5 * Math.PI / 180), camera.EyeDistance, 9);
        Assert.Equal(camera.Center + (camera.TowardViewer * camera.EyeDistance), camera.Eye);

        // The eye is nearer the viewer than the centre is: further along TowardViewer.
        Assert.True(camera.DepthOf(camera.Eye) < 0, "the eye must be in front of the centre plane, at negative depth.");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(90)]
    public void A_wider_field_of_view_brings_the_eye_closer(double fov)
    {
        Camera narrow = Persp() with { FieldOfViewDegrees = fov };
        Camera wider = Persp() with { FieldOfViewDegrees = fov + 10 };

        Assert.True(wider.EyeDistance < narrow.EyeDistance);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(48, 20, 5)]
    [InlineData(-30.5, 8, -4)]
    public void On_the_centre_plane_the_two_projections_agree(double a, double b, double c)
    {
        // A point whose depth is zero: in the plane through the centre, perpendicular to the view.
        Camera ortho = Ortho();
        Vector3d onPlane = ortho.Center + (ortho.Right * a) + (ortho.Up * b);
        Camera persp = ortho with { Projection = CameraProjection.Perspective };

        Assert.Equal(0, ortho.DepthOf(onPlane), 9);
        Assert.Equal(ortho.Project(onPlane).X, persp.Project(onPlane).X, 7);
        Assert.Equal(ortho.Project(onPlane).Y, persp.Project(onPlane).Y, 7);
        Assert.Equal(ortho.Project(ortho.Center), persp.Project(persp.Center));
        _ = c;
    }

    [Fact]
    public void A_point_twice_as_far_as_the_centre_plane_from_the_eye_projects_at_half_the_offset()
    {
        Camera camera = Persp();
        double distance = camera.EyeDistance;

        // Same offset across the screen, once on the centre plane and once one eye-distance behind it
        // (so twice as far from the eye).
        Vector3d near = camera.Center + (camera.Right * 10);
        Vector3d far = near + (camera.ViewDirection * distance);

        double onNear = camera.Project(near).X - camera.Project(camera.Center).X;
        double onFar = camera.Project(far).X - camera.Project(camera.Center).X;

        Assert.Equal(10 * camera.PixelsPerInch, onNear, 7);
        Assert.Equal(onNear / 2, onFar, 7);
        Assert.Equal(0.5, camera.ScaleAtDepth(distance), 12);
        Assert.Equal(1.0, Ortho().ScaleAtDepth(distance), 12);
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(30, 20)]
    [InlineData(200, -35)]
    [InlineData(45, 35.264)]
    public void The_ray_through_a_projected_point_passes_through_that_point(double azimuth, double elevation)
    {
        Camera camera = Persp(azimuth, elevation);
        Random random = new(7);

        for (int i = 0; i < 200; i++)
        {
            Vector3d point = camera.Center + new Vector3d(random.NextDouble() * 60 - 30, random.NextDouble() * 60 - 30, random.NextDouble() * 40 - 20);
            if (!camera.IsInFront(point))
            {
                continue;
            }

            (Vector3d origin, Vector3d direction) = camera.Ray(camera.Project(point));

            Assert.Equal(camera.Eye, origin);
            Assert.Equal(1.0, Vector3d.Dot(direction, direction), 9);
            Assert.True(DistanceFromLine(point, origin, direction) < 1e-6, $"the ray missed {point} by {DistanceFromLine(point, origin, direction)}.");
        }
    }

    [Fact]
    public void Rays_in_perspective_fan_out_from_the_eye_where_orthographic_rays_are_parallel()
    {
        Camera persp = Persp();
        Camera ortho = Ortho();
        Point left = new(50, 300);
        Point right = new(850, 300);

        Assert.Equal(ortho.Ray(left).Direction, ortho.Ray(right).Direction);
        Assert.NotEqual(persp.Ray(left).Direction, persp.Ray(right).Direction);
        Assert.Equal(persp.Ray(left).Origin, persp.Ray(right).Origin);
    }

    [Fact]
    public void The_ray_through_the_top_of_the_viewport_leaves_at_half_the_field_of_view()
    {
        Camera camera = Persp() with { FieldOfViewDegrees = 60 };

        (_, Vector3d direction) = camera.Ray(new Point(Viewport.Width / 2.0, 0));
        double angle = Math.Acos(Math.Clamp(Vector3d.Dot(direction, camera.ViewDirection), -1, 1)) * 180 / Math.PI;

        Assert.Equal(30.0, angle, 7);
    }

    [Fact]
    public void Parallel_verticals_converge_in_perspective_and_stay_parallel_in_orthographic()
    {
        // Four vertical edges of a table's legs, tilted away so they visibly converge on the screen.
        Vector3d[] feet = [new(0, 0, 0), new(40, 0, 0), new(0, 20, 0), new(40, 20, 0)];
        Vector3d up = new(0, 0, 16);

        (Point Foot, Point Head)[] Edges(Camera camera) => [.. feet.Select(f => (camera.Project(f), camera.Project(f + up)))];

        // Orthographic: every leg's on-screen direction is the same.
        Camera ortho = Ortho(30, 15);
        (Point Foot, Point Head)[] parallel = Edges(ortho);
        Vector first = parallel[0].Head - parallel[0].Foot;
        foreach ((Point foot, Point head) in parallel)
        {
            Vector v = head - foot;
            Assert.Equal(first.X, v.X, 7);
            Assert.Equal(first.Y, v.Y, 7);
        }

        // Perspective: the four lines meet at one point (the vertical vanishing point).
        (Point Foot, Point Head)[] edges = Edges(Persp(30, 15));
        Point? meeting = null;
        for (int i = 0; i < 4; i++)
        {
            for (int j = i + 1; j < 4; j++)
            {
                Point? at = Intersect(edges[i], edges[j]);
                Assert.NotNull(at);
                meeting ??= at;
                Assert.Equal(meeting.Value.X, at!.Value.X, 3);
                Assert.Equal(meeting.Value.Y, at.Value.Y, 3);
            }
        }
    }

    static Point? Intersect((Point Foot, Point Head) a, (Point Foot, Point Head) b)
    {
        Vector da = a.Head - a.Foot;
        Vector db = b.Head - b.Foot;
        double cross = (da.X * db.Y) - (da.Y * db.X);
        if (Math.Abs(cross) < 1e-9)
        {
            return null;
        }

        Vector diff = b.Foot - a.Foot;
        double t = ((diff.X * db.Y) - (diff.Y * db.X)) / cross;
        return new Point(a.Foot.X + (t * da.X), a.Foot.Y + (t * da.Y));
    }

    [Fact]
    public void Equal_legs_are_not_equal_on_screen_in_perspective_the_nearer_one_is_longer()
    {
        // The orthographic test (#93) asserts equal lengths; this is its opposite, so that mixing the
        // two projections up fails one of them.
        Camera camera = Persp(45, 35.264);
        Vector3d nearFoot = new(48, 0, 0);     // south-east: nearest the eye at azimuth 45
        Vector3d farFoot = new(0, 24, 0);      // north-west: furthest
        Vector3d up = new(0, 0, 16.25);

        double near = Pixels(camera.Project(nearFoot + up) - camera.Project(nearFoot));
        double far = Pixels(camera.Project(farFoot + up) - camera.Project(farFoot));
        Camera ortho = camera with { Projection = CameraProjection.Orthographic };

        Assert.True(near > far * 1.02, $"the near leg ({near}) should be clearly longer than the far one ({far}).");
        Assert.Equal(
            Pixels(ortho.Project(nearFoot + up) - ortho.Project(nearFoot)),
            Pixels(ortho.Project(farFoot + up) - ortho.Project(farFoot)),
            7);
    }

    [Fact]
    public void Switching_projection_keeps_the_centre_and_the_scale_there()
    {
        Camera ortho = Ortho();
        Camera persp = ortho with { Projection = CameraProjection.Perspective };

        Assert.Equal(ortho.Center, persp.Center);
        Assert.Equal(ortho.PixelsPerInch, persp.PixelsPerInch);
        Assert.Equal(ortho.Project(ortho.Center), persp.Project(persp.Center));
        Assert.Equal(ortho.OnCenterPlane(new Point(100, 100)), persp.OnCenterPlane(new Point(100, 100)));
    }

    [Fact]
    public void A_point_behind_the_eye_is_not_in_front_and_one_beyond_the_centre_is()
    {
        Camera camera = Persp();

        Assert.True(camera.IsInFront(camera.Center));
        Assert.True(camera.IsInFront(camera.Center + (camera.ViewDirection * 500)));
        Assert.False(camera.IsInFront(camera.Eye));
        Assert.False(camera.IsInFront(camera.Eye + (camera.TowardViewer * 5)));
        Assert.True(Ortho().IsInFront(Ortho().Center + (Ortho().TowardViewer * 1e6)));
    }

    [Fact]
    public void Which_way_is_towards_the_eye_depends_on_where_you_stand_in_perspective_only()
    {
        Vector3d a = new(-40, 30, 0);
        Vector3d b = new(60, -10, 12);

        Camera ortho = Ortho();
        Assert.Equal(ortho.TowardViewer, ortho.TowardViewerAt(a));
        Assert.Equal(ortho.TowardViewer, ortho.TowardViewerAt(b));

        Camera persp = Persp();
        Assert.NotEqual(persp.TowardViewerAt(a), persp.TowardViewerAt(b));
        Assert.Equal(1.0, Vector3d.Dot(persp.TowardViewerAt(a), persp.TowardViewerAt(a)), 9);
        Vector3d atCentre = persp.TowardViewerAt(persp.Center);
        Assert.Equal(persp.TowardViewer.X, atCentre.X, 12);
        Assert.Equal(persp.TowardViewer.Y, atCentre.Y, 12);
        Assert.Equal(persp.TowardViewer.Z, atCentre.Z, 12);
    }

    [Fact]
    public void An_inch_along_a_direction_moves_a_near_point_further_than_a_far_one_in_perspective_only()
    {
        Vector3d direction = new(1, 0, 0);

        Camera ortho = Ortho(0, 20);
        Vector3d nearPoint = ortho.Center + (ortho.TowardViewer * 20);
        Vector3d farPoint = ortho.Center - (ortho.TowardViewer * 20);
        Assert.Equal(Pixels(ortho.ProjectDirection(direction, nearPoint)), Pixels(ortho.ProjectDirection(direction, farPoint)), 9);
        Assert.Equal(ortho.ProjectDirection(direction), ortho.ProjectDirection(direction, farPoint));

        Camera persp = Persp(0, 20);
        Assert.True(Pixels(persp.ProjectDirection(direction, nearPoint)) > Pixels(persp.ProjectDirection(direction, farPoint)));
    }

    [Fact]
    public void Zooming_in_perspective_keeps_the_point_under_the_cursor_and_does_not_fly_to_the_eye()
    {
        Camera camera = Persp();
        Point anchor = new(300, 200);
        Vector3d under = camera.OnCenterPlane(anchor);

        Camera zoomed = camera.ZoomAt(anchor, 2);

        Assert.Equal(anchor.X, zoomed.Project(under).X, 6);
        Assert.Equal(anchor.Y, zoomed.Project(under).Y, 6);
        Assert.Equal(camera.PixelsPerInch * 2, zoomed.PixelsPerInch, 9);

        // Zooming in dollies the eye in: it is closer to the centre, and the centre is still ahead of it.
        Assert.True(zoomed.EyeDistance < camera.EyeDistance);
        Assert.True(zoomed.IsInFront(zoomed.Center));
    }

    [Theory]
    [InlineData(30, 20)]
    [InlineData(45, 35.264)]
    [InlineData(200, -10)]
    [InlineData(0, 90)]
    public void Fitting_in_perspective_frames_every_corner_inside_the_margin(double azimuth, double elevation)
    {
        Bounds3 bounds = Bounds3.Of(Point3.Origin).Including(new Point3(Length.Inches(48), Length.Inches(24), Length.Inches(17)));
        Camera camera = Persp(azimuth, elevation).FitTo(bounds, Viewport);

        double margin = ViewTransform.FitMarginFraction;
        Point[] shown = [.. bounds.CornersInInches().Select(camera.Project)];

        Assert.All(bounds.CornersInInches(), corner => Assert.True(camera.IsInFront(corner)));
        Assert.True(shown.Min(p => p.X) >= (Viewport.Width * margin) - 1);
        Assert.True(shown.Max(p => p.X) <= (Viewport.Width * (1 - margin)) + 1);
        Assert.True(shown.Min(p => p.Y) >= (Viewport.Height * margin) - 1);
        Assert.True(shown.Max(p => p.Y) <= (Viewport.Height * (1 - margin)) + 1);

        // One of the two dimensions fills what is usable.
        double fillX = (shown.Max(p => p.X) - shown.Min(p => p.X)) / (Viewport.Width * (1 - (2 * margin)));
        double fillY = (shown.Max(p => p.Y) - shown.Min(p => p.Y)) / (Viewport.Height * (1 - (2 * margin)));
        Assert.True(Math.Max(fillX, fillY) > 0.99, $"the fit left the drawing at {fillX:P0} by {fillY:P0} of the usable space.");
    }

    [Fact]
    public void Fitting_a_drawing_that_would_enclose_the_eye_backs_off_until_it_is_all_in_front()
    {
        // A deep box seen nearly end-on: at the orthographic scale the eye would be inside it.
        Bounds3 bounds = Bounds3.Of(Point3.Origin).Including(new Point3(Length.Inches(20), Length.Inches(400), Length.Inches(20)));
        Camera camera = Persp(0, 5).FitTo(bounds, Viewport);

        Assert.All(bounds.CornersInInches(), corner => Assert.True(camera.IsInFront(corner)));
    }

    [Fact]
    public void An_orthographic_fit_is_unchanged_by_perspective_existing()
    {
        Bounds3 bounds = Bounds3.Of(Point3.Origin).Including(new Point3(Length.Inches(48), Length.Inches(24), Length.Inches(17)));
        Camera fitted = Ortho().FitTo(bounds, Viewport);

        Assert.Equal(CameraProjection.Orthographic, fitted.Projection);
        Assert.Equal(1.0, fitted.ScaleAtDepth(50), 12);
    }
}
