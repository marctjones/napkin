using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>The 3D view's quick view snaps (#132): axis angles, the flip, and the face cycle.</summary>
public class ViewSnapTests
{
    static readonly LayerId Layer = LayerId.New();
    static readonly Size Viewport = new(800, 600);

    static Camera Start() => Camera.Isometric(Viewport);

    static void AssertVector(Vector3d expected, Vector3d actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Z, actual.Z, 9);
    }

    public static TheoryData<double, double, double, double, double> Directions() => new()
    {
        { 0, 0, 1, 0, 90 },
        { 0, 0, -1, 0, -90 },
        { 0, -1, 0, 0, 0 },
        { 0, 1, 0, 180, 0 },
        { 1, 0, 0, 90, 0 },
        { -1, 0, 0, 270, 0 },
    };

    [Theory]
    [MemberData(nameof(Directions))]
    public void Each_of_the_six_directions_maps_to_its_angles_and_back(double x, double y, double z, double azimuth, double elevation)
    {
        Vector3d direction = new(x, y, z);
        (double az, double el) = ViewSnap.AnglesFor(direction);

        Assert.Equal(azimuth, az, 9);
        Assert.Equal(elevation, el, 9);
        AssertVector(direction, (Start() with { AzimuthDegrees = az, ElevationDegrees = el }).TowardViewer);
    }

    [Fact]
    public void An_oblique_direction_round_trips()
    {
        Vector3d direction = new Vector3d(1, -2, 3);
        double length = Math.Sqrt(14);
        (double az, double el) = ViewSnap.AnglesFor(direction);
        AssertVector(new Vector3d(1 / length, -2 / length, 3 / length), (Start() with { AzimuthDegrees = az, ElevationDegrees = el }).TowardViewer);
    }

    [Fact]
    public void LooksAlong_is_true_within_half_a_degree_and_false_beyond()
    {
        Vector3d south = new(0, -1, 0);
        Assert.True(ViewSnap.LooksAlong(Start() with { AzimuthDegrees = 0, ElevationDegrees = 0 }, south));
        Assert.True(ViewSnap.LooksAlong(Start() with { AzimuthDegrees = 0.4, ElevationDegrees = 0 }, south));
        Assert.False(ViewSnap.LooksAlong(Start() with { AzimuthDegrees = 0.7, ElevationDegrees = 0 }, south));
        Assert.False(ViewSnap.LooksAlong(Start() with { AzimuthDegrees = 180, ElevationDegrees = 0 }, south));
    }

    [Fact]
    public void The_same_axis_twice_flips_and_a_third_press_returns()
    {
        (Axis Axis, double Az1, double El1, double Az2, double El2)[] cases =
        [
            (Axis.Z, 0, 90, 0, -90),
            (Axis.Y, 0, 0, 180, 0),
            (Axis.X, 90, 0, 270, 0),
        ];
        foreach (var c in cases)
        {
            Camera first = ViewSnap.LookAlong(Start(), c.Axis);
            Assert.Equal((c.Az1, c.El1), (first.AzimuthDegrees, first.ElevationDegrees));
            Camera second = ViewSnap.LookAlong(first, c.Axis);
            Assert.Equal((c.Az2, c.El2), (second.AzimuthDegrees, second.ElevationDegrees));
            Camera third = ViewSnap.LookAlong(second, c.Axis);
            Assert.Equal((c.Az1, c.El1), (third.AzimuthDegrees, third.ElevationDegrees));
        }
    }

    [Fact]
    public void Another_axis_resets_to_the_positive_side_and_keeps_the_projection()
    {
        Camera below = ViewSnap.LookAlong(ViewSnap.LookAlong(Start(), Axis.Z), Axis.Z);
        Camera y = ViewSnap.LookAlong(below with { Projection = CameraProjection.Perspective }, Axis.Y);

        Assert.Equal((0.0, 0.0), (y.AzimuthDegrees, y.ElevationDegrees));
        Assert.True(y.IsPerspective);
    }

    static Box Part(BoxFace faceUp, int rotationDegrees) => Box.AsDrawn(
        EntityId.New(), Layer, Point2.Inches(10, 20), Length.Inches(12), Length.Inches(8), Length.Inches(2), Angle.Zero)
        with { FaceUp = faceUp, Rotation = Angle.Degrees(rotationDegrees) };

    public static TheoryData<BoxFace, int> Orientations()
    {
        TheoryData<BoxFace, int> data = [];
        foreach (BoxFace up in Enum.GetValues<BoxFace>())
        {
            foreach (int rotation in (int[])[0, 90])
            {
                data.Add(up, rotation);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Orientations))]
    public void The_face_cycle_looks_square_at_each_face_of_a_part_in_any_orientation(BoxFace faceUp, int rotation)
    {
        Box box = Part(faceUp, rotation);
        Camera camera = Start();
        HashSet<string> seen = [];
        BoxFace[] visited = new BoxFace[7];

        for (int step = 0; step < 7; step++)
        {
            BoxFace face = ViewSnap.FaceAt(step);
            visited[step] = face;
            camera = ViewSnap.FaceOn(camera, box, face);

            Vector3 world = box.Orientation.Apply(new Vector3(
                Length.Inches(face switch { BoxFace.East => 1, BoxFace.West => -1, _ => 0 }),
                Length.Inches(face switch { BoxFace.North => 1, BoxFace.South => -1, _ => 0 }),
                Length.Inches(face switch { BoxFace.Top => 1, BoxFace.Bottom => -1, _ => 0 })));
            AssertVector(new Vector3d(world.Dx.ToInches(), world.Dy.ToInches(), world.Dz.ToInches()), camera.TowardViewer);
            if (step < 6)
            {
                seen.Add($"{camera.TowardViewer.X:F3},{camera.TowardViewer.Y:F3},{camera.TowardViewer.Z:F3}");
            }

            // Centred on the part: its middle projects to the middle of the view.
            Point at = camera.Project(ModelHandles.Centre(box));
            Assert.Equal(Viewport.Width / 2, at.X, 6);
            Assert.Equal(Viewport.Height / 2, at.Y, 6);
        }

        Assert.Equal(6, seen.Count);
        Assert.Equal(6, visited.Take(6).Distinct().Count());
        Assert.Equal(visited[0], visited[6]);
    }

    [Fact]
    public void A_deep_part_is_framed_in_perspective_beside_a_covered_strip_and_fills_the_view()
    {
        Box leg = Part(BoxFace.Top, 0) with { Depth = Length.Inches(17), Width = Length.Inches(2), Height = Length.Inches(2) };
        Camera start = Start() with { Projection = CameraProjection.Perspective };

        foreach (BoxFace face in ViewSnap.FaceOrder)
        {
            Camera camera = ViewSnap.FaceOn(start, leg, face, coveredRight: 300);
            Point at = camera.Project(ModelHandles.CentreOf(leg, face));

            Assert.True(camera.IsPerspective);
            Assert.Equal((Viewport.Width - 300) / 2, at.X, 3);
            Assert.Equal(Viewport.Height / 2, at.Y, 3);
            Assert.True(camera.PixelsPerInch > 5, $"{face}: framed at {camera.PixelsPerInch} px/in.");
        }
    }

    [Fact]
    public void A_tipped_part_shows_its_north_face_up_from_above()
    {
        Box box = Part(BoxFace.North, 0);
        Camera camera = ViewSnap.FaceOn(Start(), box, BoxFace.North);

        Assert.Equal((0.0, 90.0), (camera.AzimuthDegrees, camera.ElevationDegrees));
    }

    [Fact]
    public void With_nothing_selected_the_axes_step_above_south_east_north_west_below_and_wrap()
    {
        Vector3d[] expected =
        [
            new(0, 0, 1), new(0, -1, 0), new(1, 0, 0), new(0, 1, 0), new(-1, 0, 0), new(0, 0, -1),
            new(0, 0, 1),
        ];
        for (int step = 0; step < expected.Length; step++)
        {
            (double az, double el) = ViewSnap.AnglesFor(ViewSnap.AxisStep(step));
            AssertVector(expected[step], (Start() with { AzimuthDegrees = az, ElevationDegrees = el }).TowardViewer);
        }

        AssertVector(new Vector3d(0, 0, -1), ViewSnap.AxisStep(-1));
    }
}
