using Avalonia;
using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The 3D view's camera, tested without a window (<c>docs/design/assembly-model.md</c> &#xA7;8.1,
/// &#xA7;8.4: "the projection is pure and testable").
/// </summary>
/// <remarks>
/// The same stance as <see cref="ViewTransformTests"/>: the camera is a value with no dependency on
/// Avalonia beyond <see cref="Point"/>, <see cref="Vector"/> and <see cref="Size"/>, so what it does
/// to a point is a statement about a function. The gestures that drive it are the GUI workflows'.
/// </remarks>
public class CameraTests
{
    static readonly Size Viewport = new(900, 600);

    static Camera SomeCamera => new(30, 20, 12, -6, 4, 9, Viewport);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(48, 20)]
    [InlineData(-144.5, 96.25)]
    [InlineData(1200, -1200)]
    public void The_plan_camera_is_the_view_transform(double x, double y)
    {
        // §7.1: the plan canvas is the camera at elevation 90°, azimuth 0°. Every point at the plan
        // datum lands where the plan's own transform puts it — the basis and the Y flip together.
        ViewTransform plan = new(30, 12, 8, Viewport);
        Camera camera = Camera.Plan(30, 12, 8, Viewport);

        Point fromPlan = plan.ToScreen(x, y);
        Point fromCamera = camera.Project(new Vector3d(x, y, 0));

        Assert.Equal(fromPlan.X, fromCamera.X, 9);
        Assert.Equal(fromPlan.Y, fromCamera.Y, 9);
    }

    [Fact]
    public void Looking_straight_down_height_does_not_move_a_point()
    {
        Camera camera = Camera.Plan(0, 0, 10, Viewport);

        Point low = camera.Project(new Vector3d(5, 7, 0));
        Point high = camera.Project(new Vector3d(5, 7, 30));

        Assert.Equal(low.X, high.X, 9);
        Assert.Equal(low.Y, high.Y, 9);
    }

    [Fact]
    public void The_isometric_default_is_forty_five_degrees_round_and_arctan_one_over_root_two_up()
    {
        Camera camera = Camera.Isometric(Viewport);

        Assert.Equal(45, camera.AzimuthDegrees);
        Assert.Equal(35.264, camera.ElevationDegrees, 3);

        // From the south-east and above: the eye is east, south and up of the drawing.
        Vector3d eye = camera.TowardViewer;
        Assert.True(eye.X > 0 && eye.Y < 0 && eye.Z > 0, $"the eye should be south-east and above, not {eye}.");
    }

    [Fact]
    public void In_the_isometric_view_the_three_axes_project_to_equal_lengths()
    {
        Camera camera = Camera.Isometric(Viewport) with { PixelsPerInch = 10 };

        double x = PixelLength(camera.ProjectDirection(Vector3d.UnitX));
        double y = PixelLength(camera.ProjectDirection(Vector3d.UnitY));
        double z = PixelLength(camera.ProjectDirection(Vector3d.UnitZ));

        Assert.Equal(x, y, 9);
        Assert.Equal(x, z, 9);

        // √(2/3) of the scale: the classic isometric foreshortening.
        Assert.Equal(10 * Math.Sqrt(2.0 / 3.0), x, 9);
    }

    [Fact]
    public void In_the_isometric_view_up_is_up_and_the_two_plan_axes_run_down_and_up_to_the_right()
    {
        Camera camera = Camera.Isometric(Viewport) with { PixelsPerInch = 10 };

        Vector z = camera.ProjectDirection(Vector3d.UnitZ);
        Assert.Equal(0, z.X, 9);
        Assert.True(z.Y < 0, "world +Z should be drawn up the screen.");

        // The nearest corner is the south-east one: +X runs right and down towards it, +Y right
        // and up away from it.
        Vector x = camera.ProjectDirection(Vector3d.UnitX);
        Vector y = camera.ProjectDirection(Vector3d.UnitY);
        Assert.True(x.X > 0 && x.Y > 0, $"+X should run right and down, not {x}.");
        Assert.True(y.X > 0 && y.Y < 0, $"+Y should run right and up, not {y}.");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 35.264)]
    [InlineData(200, -40)]
    [InlineData(310, 89)]
    public void The_screen_basis_is_right_handed_and_orthonormal(double azimuth, double elevation)
    {
        Camera camera = new(azimuth, elevation, 0, 0, 0, 10, Viewport);
        Vector3d right = camera.Right;
        Vector3d up = camera.Up;
        Vector3d toward = camera.TowardViewer;

        Assert.Equal(1, right.Length, 12);
        Assert.Equal(1, up.Length, 12);
        Assert.Equal(1, toward.Length, 12);
        Assert.Equal(0, Vector3d.Dot(right, up), 12);
        Assert.Equal(0, Vector3d.Dot(right, toward), 12);
        Assert.Equal(0, Vector3d.Dot(up, toward), 12);

        // Right × Up is towards the eye: x right, y up, z out of the screen.
        Vector3d cross = Vector3d.Cross(right, up);
        Assert.Equal(toward.X, cross.X, 12);
        Assert.Equal(toward.Y, cross.Y, 12);
        Assert.Equal(toward.Z, cross.Z, 12);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12, -4, 30)]
    [InlineData(-100, 250, -7.5)]
    public void The_ray_through_a_projected_point_passes_through_that_point(double x, double y, double z)
    {
        Camera camera = SomeCamera;
        Vector3d point = new(x, y, z);

        (Vector3d origin, Vector3d direction) = camera.Ray(camera.Project(point));

        // The point is on the line: what is left after taking out the part along the direction is
        // nothing.
        Vector3d offset = point - origin;
        Vector3d across = offset - (direction * Vector3d.Dot(offset, direction));
        Assert.Equal(0, across.Length, 9);
        Assert.Equal(1, direction.Length, 12);
        Assert.Equal(camera.ViewDirection, direction);
    }

    [Fact]
    public void A_ray_is_the_same_direction_everywhere_because_the_camera_is_orthographic()
    {
        Camera camera = SomeCamera;

        Assert.Equal(camera.Ray(new Point(0, 0)).Direction, camera.Ray(new Point(800, 500)).Direction);
    }

    [Fact]
    public void A_point_further_along_the_view_is_deeper()
    {
        Camera camera = SomeCamera;
        Vector3d near = new(1, 2, 3);
        Vector3d far = near + (camera.ViewDirection * 10);

        Assert.Equal(10, camera.DepthOf(far) - camera.DepthOf(near), 9);
    }

    [Theory]
    [InlineData(450, 300)]
    [InlineData(100, 80)]
    [InlineData(870, 590)]
    public void Zooming_keeps_the_model_point_under_the_anchor(double anchorX, double anchorY)
    {
        Camera camera = SomeCamera;
        Point anchor = new(anchorX, anchorY);
        (Vector3d before, _) = camera.Ray(anchor);

        Camera zoomed = camera.ZoomAt(anchor, 2.5);
        Point after = zoomed.Project(before);

        Assert.Equal(anchor.X, after.X, 6);
        Assert.Equal(anchor.Y, after.Y, 6);
        Assert.Equal(camera.PixelsPerInch * 2.5, zoomed.PixelsPerInch, 9);
    }

    [Fact]
    public void Zooming_by_nothing_useful_changes_nothing()
    {
        Camera camera = SomeCamera;

        Assert.Equal(camera, camera.ZoomAt(new Point(10, 10), 0));
        Assert.Equal(camera, camera.ZoomAt(new Point(10, 10), double.NaN));
    }

    [Fact]
    public void The_scale_is_clamped_like_the_plans()
    {
        Assert.Equal(ViewTransform.MaxPixelsPerInch, (SomeCamera with { PixelsPerInch = 1e9 }).PixelsPerInch);
        Assert.Equal(ViewTransform.MinPixelsPerInch, SomeCamera.ZoomAtCenter(1e-12).PixelsPerInch);
    }

    [Fact]
    public void Panning_moves_the_drawing_with_the_hand()
    {
        Camera camera = SomeCamera;
        Vector3d point = new(3, 4, 5);
        Point before = camera.Project(point);

        Point after = camera.Pan(new Vector(40, -25)).Project(point);

        Assert.Equal(before.X + 40, after.X, 9);
        Assert.Equal(before.Y - 25, after.Y, 9);
    }

    [Fact]
    public void Orbiting_turns_the_view_and_leaves_the_centre_where_it_was()
    {
        Camera camera = SomeCamera;
        Point centre = camera.Project(camera.Center);

        Camera turned = camera.Orbit(new Vector(50, 25));

        Assert.Equal(30 - (50 * Camera.DegreesPerOrbitPixel), turned.AzimuthDegrees, 9);
        Assert.Equal(20 + (25 * Camera.DegreesPerOrbitPixel), turned.ElevationDegrees, 9);
        Point stillCentre = turned.Project(camera.Center);
        Assert.Equal(centre.X, stillCentre.X, 9);
        Assert.Equal(centre.Y, stillCentre.Y, 9);
        Assert.Equal(camera.PixelsPerInch, turned.PixelsPerInch);
    }

    [Fact]
    public void Orbiting_composes_and_undoes_itself()
    {
        Camera camera = SomeCamera;

        Camera there = camera.Orbit(new Vector(30, 10)).Orbit(new Vector(20, 15));
        Camera back = there.Orbit(new Vector(-50, -25));

        Assert.Equal(camera.AzimuthDegrees, back.AzimuthDegrees, 9);
        Assert.Equal(camera.ElevationDegrees, back.ElevationDegrees, 9);
    }

    [Fact]
    public void The_elevation_stops_at_straight_up_and_straight_down_and_the_azimuth_wraps()
    {
        Camera camera = SomeCamera;

        Assert.Equal(90, camera.OrbitBy(0, 500).ElevationDegrees);
        Assert.Equal(-90, camera.OrbitBy(0, -500).ElevationDegrees);
        Assert.Equal(10, camera.OrbitBy(340, 0).AzimuthDegrees, 9);
        Assert.Equal(350, camera.OrbitBy(-40, 0).AzimuthDegrees, 9);
    }

    [Fact]
    public void Fitting_beside_covered_panels_frames_every_corner_in_what_is_left()
    {
        Bounds3 bounds = Bounds3.Of(Point3.Inches(0, 0, 0)).Including(Point3.Inches(48, 24, 17));
        Camera fitted = Camera.Isometric().FitTo(bounds, Viewport, coveredRight: 280);

        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        foreach (Vector3d corner in bounds.CornersInInches())
        {
            Point at = fitted.Project(corner);
            minX = Math.Min(minX, at.X);
            maxX = Math.Max(maxX, at.X);
        }

        Assert.True(maxX <= 620 + 1e-6, $"a corner is at {maxX}, under the panels.");
        Assert.Equal(310, (minX + maxX) / 2, 6);
    }

    [Fact]
    public void Fitting_frames_every_corner_inside_the_margin()
    {
        Bounds3 bounds = Bounds3.Of(Point3.Inches(0, 0, 0)).Including(Point3.Inches(48, 24, 17));
        Camera fitted = Camera.Isometric().FitTo(bounds, Viewport);

        double margin = ViewTransform.FitMarginFraction;
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (Vector3d corner in bounds.CornersInInches())
        {
            Point at = fitted.Project(corner);
            minX = Math.Min(minX, at.X);
            maxX = Math.Max(maxX, at.X);
            minY = Math.Min(minY, at.Y);
            maxY = Math.Max(maxY, at.Y);
        }

        Assert.True(minX >= (Viewport.Width * margin) - 1e-6);
        Assert.True(maxX <= (Viewport.Width * (1 - margin)) + 1e-6);
        Assert.True(minY >= (Viewport.Height * margin) - 1e-6);
        Assert.True(maxY <= (Viewport.Height * (1 - margin)) + 1e-6);

        // Centred, and touching the margin one way or the other.
        Assert.Equal(Viewport.Width / 2, (minX + maxX) / 2, 6);
        Assert.Equal(Viewport.Height / 2, (minY + maxY) / 2, 6);
        Assert.True(
            Math.Abs((maxX - minX) - (Viewport.Width * (1 - (2 * margin)))) < 1e-6
            || Math.Abs((maxY - minY) - (Viewport.Height * (1 - (2 * margin)))) < 1e-6);

        // Fitting does not change which way the camera looks.
        Assert.Equal(Camera.IsometricAzimuthDegrees, fitted.AzimuthDegrees);
    }

    [Fact]
    public void Fitting_nothing_keeps_the_view_and_takes_the_viewport()
    {
        Camera camera = SomeCamera;

        Camera fitted = camera.FitTo(Bounds3.Empty, new Size(400, 300));

        Assert.Equal(camera with { Viewport = new Size(400, 300) }, fitted);
    }

    [Fact]
    public void The_bounds_of_a_sketch_take_in_every_vertex_of_a_turned_box()
    {
        LayerId layer = LayerId.New();
        Box leg = new(
            EntityId.New(),
            layer,
            Point3.Inches(10, 10, 0),
            Length.Inches(3),
            Length.Inches(3),
            Length.Inches(28),
            BoxFace.North,
            Angle.Zero);
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(layer, "Parts"))
            .WithEntity(leg);

        Bounds3 bounds = Bounds3.Of(sketch);

        // Tipped back: local Y stands up and local Z lies along −Y (§1.3's table).
        Assert.Equal(Point3.Inches(10, -18, 0), bounds.Min);
        Assert.Equal(Point3.Inches(13, 10, 3), bounds.Max);
    }

    // ---- Equal legs look equal (#93) -------------------------------------------------------
    //
    // An orthographic view has no vanishing point, so nothing shrinks with distance: a table's four
    // legs must project to exactly the same length on screen. They can still *look* unequal, because
    // the top and aprons hide different amounts of each, but that is occlusion and not projection.
    // These tests hold the projection to the formula on a real design, from every angle.

    static readonly string[] LegNames = ["Leg, south-west", "Leg, south-east", "Leg, north-west", "Leg, north-east"];

    /// <summary>The bottom and top of a leg's first vertical edge, in inches, read from the scene.</summary>
    static (Vector3d Foot, Vector3d Head) VerticalEdgeOf(string legName)
    {
        Design design = SampleExpectations.Sample("coffee-table").Load();
        EntityId leg = SampleExpectations.For("coffee-table").Box(legName).EntityId;
        List<Vector3d> corners = [.. ModelScene.Of(design.Sketch).Polygons.Where(p => p.Box == leg).SelectMany(p => p.Points)];
        double low = corners.Min(c => c.Z);
        double high = corners.Max(c => c.Z);
        Vector3d foot = corners.First(c => c.Z == low);
        return (foot, foot with { Z = high });
    }

    static double PixelsBetween(Camera camera, Vector3d from, Vector3d to)
    {
        Point a = camera.Project(from);
        Point b = camera.Project(to);
        return Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
    }

    public static TheoryData<double, double, double> EveryView()
    {
        TheoryData<double, double, double> views = [];
        foreach (double zoom in new[] { 0.5, 4, 37.5 })
        {
            for (double azimuth = 0; azimuth < 360; azimuth += 15)
            {
                foreach (double elevation in new double[] { -80, -35.264, 0, 10, 35.264, 60, 80 })
                {
                    views.Add(azimuth, elevation, zoom);
                }
            }
        }

        return views;
    }

    [Theory]
    [MemberData(nameof(EveryView))]
    public void The_four_legs_project_to_the_same_length_and_the_formula_gives_it(double azimuth, double elevation, double pixelsPerInch)
    {
        Camera camera = new(azimuth, elevation, 24, 12, 8, pixelsPerInch, Viewport);

        double[] lengths = [.. LegNames.Select(name =>
        {
            (Vector3d foot, Vector3d head) = VerticalEdgeOf(name);
            return PixelsBetween(camera, foot, head);
        })];

        // The coffee table's legs are 16 1/4" long (samples/coffee-table.expected.json), and a
        // vertical edge is foreshortened by cos(elevation): that, not the other legs, is the check.
        double expected = pixelsPerInch * 16.25 * Math.Cos(elevation * Math.PI / 180);
        foreach (double length in lengths)
        {
            Assert.Equal(expected, length, 7);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(200)]
    public void A_leg_is_its_full_length_from_the_side_and_a_point_from_above(double azimuth)
    {
        (Vector3d foot, Vector3d head) = VerticalEdgeOf("Leg, north-east");

        Camera side = new(azimuth, 0, 24, 12, 8, 10, Viewport);
        Camera top = new(azimuth, 90, 24, 12, 8, 10, Viewport);

        Assert.Equal(162.5, PixelsBetween(side, foot, head), 7);
        Assert.Equal(0, PixelsBetween(top, foot, head), 7);
    }

    [Theory]
    [InlineData(20, 35.264)]
    [InlineData(200, 10)]
    public void Parallel_edges_stay_parallel_on_screen(double azimuth, double elevation)
    {
        Camera camera = new(azimuth, elevation, 24, 12, 8, 10, Viewport);

        Vector Screen(string leg)
        {
            (Vector3d foot, Vector3d head) = VerticalEdgeOf(leg);
            return camera.Project(head) - camera.Project(foot);
        }

        Vector first = Screen(LegNames[0]);
        foreach (string other in LegNames.Skip(1))
        {
            Vector v = Screen(other);
            Assert.Equal(first.X, v.X, 7);
            Assert.Equal(first.Y, v.Y, 7);
        }
    }

    [Fact]
    public void The_test_would_notice_a_wrong_foreshortening()
    {
        // Guards the guard: were the vertical edge foreshortened by sin(elevation) instead of
        // cos(elevation), the formula above would disagree at the isometric elevation.
        (Vector3d foot, Vector3d head) = VerticalEdgeOf("Leg, south-west");
        Camera camera = new(30, 35.264, 24, 12, 8, 10, Viewport);

        double wrong = 10 * 16.25 * Math.Sin(35.264 * Math.PI / 180);
        Assert.NotEqual(wrong, PixelsBetween(camera, foot, head), 3);
    }

    static double PixelLength(Vector vector) => Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
}
