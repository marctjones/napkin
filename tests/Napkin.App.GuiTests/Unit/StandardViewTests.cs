using Avalonia;
using Avalonia.Input;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The six standard views as cameras (docs/design/standard-views.md §1, §4.1, §7.1), pinned with the
/// l-bracket — one feature on each side, so no two views can be confused — and the coffee table.
/// </summary>
/// <remarks>
/// Every expectation is derived by hand from the sample's coordinates in the design note's §1.3
/// table, not read back from the code: in Front the eye is due south, so screen right is +X and
/// screen up is +Z, and a feature's screen position is its X (and Z) straight off the table.
/// </remarks>
public class StandardViewTests
{
    const double Tolerance = 1e-9;
    static readonly Size Viewport = new(800, 600);

    /// <summary>A camera at 1 px/inch centred on the origin, so a screen distance is inches.</summary>
    static Camera Unit(StandardView view) => StandardViews.CameraFor(view, new Camera(33, 21, 0, 0, 0, 1, Viewport));

    /// <summary>The screen rectangle a box's solid projects to.</summary>
    static Rect ScreenRect(Camera camera, Sketch sketch, EntityId box)
    {
        Point[] points = [.. ModelScene.Of(sketch).Polygons.Where(p => p.Box == box).SelectMany(p => p.Points).Select(camera.Project)];
        double left = points.Min(p => p.X), right = points.Max(p => p.X);
        double top = points.Min(p => p.Y), bottom = points.Max(p => p.Y);
        return new Rect(left, top, right - left, bottom - top);
    }

    static Rect Extent(Camera camera, Sketch sketch) =>
        sketch.Entities.Values.OfType<Box>().Select(box => ScreenRect(camera, sketch, box.Id)).Aggregate((a, b) => a.Union(b));

    static Sketch Bracket() => SampleExpectations.Sample("l-bracket").Load().Sketch;

    static Sketch Table() => SampleExpectations.Sample("coffee-table").Load().Sketch;

    static EntityId Bracket(string name) => SampleExpectations.For("l-bracket").Box(name).EntityId;

    static EntityId Table(string name) => SampleExpectations.For("coffee-table").Box(name).EntityId;

    static void AssertExactly(Vector3d expected, Vector3d actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
    }

    // §1.1's table, written out again here rather than read from the code.
    public static TheoryData<StandardView, double[], double[], double[]> Table11() => new()
    {
        { StandardView.Top, [1, 0, 0], [0, 1, 0], [0, 0, 1] },
        { StandardView.Bottom, [1, 0, 0], [0, -1, 0], [0, 0, -1] },
        { StandardView.Front, [1, 0, 0], [0, 0, 1], [0, -1, 0] },
        { StandardView.Back, [-1, 0, 0], [0, 0, 1], [0, 1, 0] },
        { StandardView.Left, [0, -1, 0], [0, 0, 1], [-1, 0, 0] },
        { StandardView.Right, [0, 1, 0], [0, 0, 1], [1, 0, 0] },
    };

    static Vector3d V(double[] v) => new(v[0], v[1], v[2]);

    [Theory]
    [Trait("Feature", "VIEW-001")]
    [MemberData(nameof(Table11))]
    public void Each_view_looks_along_its_exact_axes_right_handed(StandardView view, double[] right, double[] up, double[] toward)
    {
        (Vector3d r, Vector3d u, Vector3d t) = StandardViews.Axes(view);
        AssertExactly(V(right), r);
        AssertExactly(V(up), u);
        AssertExactly(V(toward), t);
        AssertExactly(t, Vector3d.Cross(r, u));

        // The camera built for it is exactly on the axes too — no 1e-16 left over from cos 90°.
        Camera camera = Unit(view);
        AssertExactly(V(right), camera.Right);
        AssertExactly(V(up), camera.Up);
        AssertExactly(V(toward), camera.TowardViewer);
        Assert.Equal(CameraProjection.Orthographic, camera.Projection);
    }

    [Fact]
    [Trait("Feature", "VIEW-001")]
    public void A_view_keeps_the_centre_and_scale_and_forces_orthographic()
    {
        Camera from = new Camera(33, 21, 4, 5, 6, 12, Viewport) with { Projection = CameraProjection.Perspective };
        Camera front = StandardViews.CameraFor(StandardView.Front, from);
        Assert.Equal((4.0, 5.0, 6.0, 12.0), (front.CenterX, front.CenterY, front.CenterZ, front.PixelsPerInch));
        Assert.Equal(CameraProjection.Orthographic, front.Projection);
    }

    [Theory]
    [Trait("Feature", "VIEW-001")]
    [InlineData(StandardView.Top, Axis.Z, true)]
    [InlineData(StandardView.Bottom, Axis.Z, false)]
    [InlineData(StandardView.Front, Axis.Y, true)]
    [InlineData(StandardView.Back, Axis.Y, false)]
    [InlineData(StandardView.Right, Axis.X, true)]
    [InlineData(StandardView.Left, Axis.X, false)]
    public void A_view_and_the_matching_3D_snap_button_look_the_same_way(StandardView view, Axis axis, bool positive) =>
        AssertExactly(ViewSnap.AxisDirection(axis, positive), StandardViews.Axes(view).TowardViewer);

    [Fact]
    [Trait("Feature", "VIEW-002")]
    public void Front_of_the_l_bracket_is_seven_and_a_half_wide_by_six_tall()
    {
        Rect extent = Extent(Unit(StandardView.Front), Bracket());
        Assert.Equal(7.5, extent.Width, Tolerance);
        Assert.Equal(6, extent.Height, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-002")]
    public void In_Front_the_lug_is_left_of_the_boss_the_nub_leftmost_and_the_tab_rightmost()
    {
        Sketch sketch = Bracket();
        Camera front = Unit(StandardView.Front);
        Rect lug = ScreenRect(front, sketch, Bracket("Lug, south"));
        Rect boss = ScreenRect(front, sketch, Bracket("Boss, top"));
        Rect nub = ScreenRect(front, sketch, Bracket("Nub, west"));
        Rect tab = ScreenRect(front, sketch, Bracket("Tab, east"));
        Rect all = Extent(front, sketch);

        Assert.True(lug.Center.X < boss.Center.X);
        Assert.Equal(all.Left, nub.Left, Tolerance);
        Assert.Equal(all.Right, tab.Right, Tolerance);

        // Hand-derived: the lug is x ½–1, z 4–5 and the boss x 5½–6½, z ¾–1¾ — so the lug's right edge
        // is 4½ inches left of the boss's left edge, and its top 3¼ inches higher up the screen.
        Assert.Equal(4.5, boss.Left - lug.Right, Tolerance);
        Assert.Equal(3.25, boss.Top - lug.Top, Tolerance);
        Assert.Equal(1, lug.Height, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-002")]
    public void Front_of_the_coffee_table_is_48_by_17_and_every_leg_is_16_and_a_quarter_tall()
    {
        Sketch sketch = Table();
        Camera front = StandardViews.CameraFor(StandardView.Front, new Camera(0, 0, 24, 12, 8, 10, Viewport));
        Rect extent = Extent(front, sketch);
        Assert.Equal(480, extent.Width, Tolerance);
        Assert.Equal(170, extent.Height, Tolerance);

        foreach (string leg in (string[])["Leg, south-west", "Leg, south-east", "Leg, north-west", "Leg, north-east"])
        {
            Assert.Equal(162.5, ScreenRect(front, sketch, Table(leg)).Height, Tolerance);
        }
    }

    [Theory]
    [Trait("Feature", "VIEW-003")]
    [InlineData(Key.D1, KeyModifiers.None, DesignView.Top)]
    [InlineData(Key.D2, KeyModifiers.None, DesignView.Bottom)]
    [InlineData(Key.D3, KeyModifiers.None, DesignView.Front)]
    [InlineData(Key.D4, KeyModifiers.None, DesignView.Back)]
    [InlineData(Key.D5, KeyModifiers.None, DesignView.Left)]
    [InlineData(Key.D6, KeyModifiers.None, DesignView.Right)]
    [InlineData(Key.D7, KeyModifiers.None, DesignView.Model)]
    [InlineData(Key.NumPad3, KeyModifiers.None, DesignView.Front)]
    [InlineData(Key.NumPad7, KeyModifiers.None, DesignView.Model)]
    public void The_number_keys_one_to_seven_name_the_views(Key key, KeyModifiers modifiers, DesignView view) =>
        Assert.Equal(view, StandardViews.ForKey(key, modifiers));

    [Theory]
    [Trait("Feature", "VIEW-003")]
    [InlineData(Key.D3, KeyModifiers.Meta)]
    [InlineData(Key.D3, KeyModifiers.Control)]
    [InlineData(Key.D3, KeyModifiers.Shift)]
    [InlineData(Key.D8, KeyModifiers.None)]
    [InlineData(Key.D0, KeyModifiers.None)]
    [InlineData(Key.NumPad8, KeyModifiers.None)]
    [InlineData(Key.V, KeyModifiers.None)]
    public void Other_keys_and_modified_digits_name_no_view(Key key, KeyModifiers modifiers) =>
        Assert.Null(StandardViews.ForKey(key, modifiers));

    [Fact]
    [Trait("Feature", "VIEW-003")]
    public void Views_and_design_views_map_one_to_one_by_name()
    {
        foreach (StandardView view in StandardViews.All)
        {
            DesignView design = StandardViews.ToDesignView(view);
            Assert.Equal(view, StandardViews.Of(design));
            Assert.Equal(view.ToString(), StandardViews.Name(view));
            Assert.Equal(view.ToString(), design.ToString());
        }

        Assert.Null(StandardViews.Of(DesignView.Model));
        Assert.Equal("3D", StandardViews.Name(DesignView.Model));
    }
}
