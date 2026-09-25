using Avalonia;
using Avalonia.Input;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
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

    // §1.3: the l-bracket spans x 0–7½, y 0–5, z 0–6; each view shows two of those.
    [Theory]
    [Trait("Feature", "VIEW-006")]
    [InlineData(StandardView.Top, 7.5, 5)]
    [InlineData(StandardView.Bottom, 7.5, 5)]
    [InlineData(StandardView.Front, 7.5, 6)]
    [InlineData(StandardView.Back, 7.5, 6)]
    [InlineData(StandardView.Left, 5, 6)]
    [InlineData(StandardView.Right, 5, 6)]
    public void Each_view_of_the_l_bracket_has_its_extent(StandardView view, double width, double height)
    {
        Rect extent = Extent(Unit(view), Bracket());
        Assert.Equal(width, extent.Width, Tolerance);
        Assert.Equal(height, extent.Height, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-006")]
    public void In_Back_everything_is_the_mirror_of_Front_the_nub_rightmost_and_the_tab_leftmost()
    {
        Sketch sketch = Bracket();
        Camera back = Unit(StandardView.Back);
        Rect lug = ScreenRect(back, sketch, Bracket("Lug, south"));
        Rect boss = ScreenRect(back, sketch, Bracket("Boss, top"));
        Rect upright = ScreenRect(back, sketch, Bracket("Upright"));
        Rect all = Extent(back, sketch);

        Assert.True(lug.Center.X > boss.Center.X);
        Assert.Equal(all.Right, ScreenRect(back, sketch, Bracket("Nub, west")).Right, Tolerance);
        Assert.Equal(all.Left, ScreenRect(back, sketch, Bracket("Tab, east")).Left, Tolerance);

        // Hand-derived: screen right is west, so the boss (x 5½–6½) is 4½" left of the lug (x ½–1)
        // and the upright (x ½–1) is at the right, next to the nub.
        Assert.Equal(4.5, lug.Left - boss.Right, Tolerance);
        Assert.Equal(0.5, all.Right - upright.Right, Tolerance);
        Assert.Equal(3.25, boss.Top - lug.Top, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-006")]
    public void In_Right_the_boss_is_left_of_the_upright_the_lug_leftmost_and_the_rib_rightmost()
    {
        Sketch sketch = Bracket();
        Camera right = Unit(StandardView.Right);
        Rect boss = ScreenRect(right, sketch, Bracket("Boss, top"));
        Rect upright = ScreenRect(right, sketch, Bracket("Upright"));
        Rect all = Extent(right, sketch);

        Assert.True(boss.Center.X < upright.Center.X);
        Assert.Equal(all.Left, ScreenRect(right, sketch, Bracket("Lug, south")).Left, Tolerance);
        Assert.Equal(all.Right, ScreenRect(right, sketch, Bracket("Rib, north")).Right, Tolerance);

        // South is at the left: the boss (y ½–1½) centres 1½" left of the upright (y ½–4½).
        Assert.Equal(1.5, upright.Center.X - boss.Center.X, Tolerance);
        Assert.Equal(4, upright.Width, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-006")]
    public void In_Left_the_rib_is_leftmost_and_the_lug_rightmost()
    {
        Sketch sketch = Bracket();
        Camera left = Unit(StandardView.Left);
        Rect rib = ScreenRect(left, sketch, Bracket("Rib, north"));
        Rect lug = ScreenRect(left, sketch, Bracket("Lug, south"));
        Rect all = Extent(left, sketch);

        Assert.Equal(all.Left, rib.Left, Tolerance);
        Assert.Equal(all.Right, lug.Right, Tolerance);

        // North is at the left: the rib (y 4½–5) and the lug (y 0–½) are 4" apart edge to edge.
        Assert.Equal(4, lug.Left - rib.Right, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-006")]
    public void In_Bottom_south_is_up_the_lug_above_the_rib_and_left_to_right_as_Top()
    {
        Sketch sketch = Bracket();
        Camera bottom = Unit(StandardView.Bottom);
        Rect lug = ScreenRect(bottom, sketch, Bracket("Lug, south"));
        Rect rib = ScreenRect(bottom, sketch, Bracket("Rib, north"));
        Rect all = Extent(bottom, sketch);

        Assert.True(lug.Center.Y < rib.Center.Y);
        Assert.Equal(all.Top, lug.Top, Tolerance);
        Assert.Equal(all.Bottom, rib.Bottom, Tolerance);
        Assert.Equal(all.Left, ScreenRect(bottom, sketch, Bracket("Nub, west")).Left, Tolerance);
        Assert.Equal(all.Right, ScreenRect(bottom, sketch, Bracket("Tab, east")).Right, Tolerance);

        // And Top is the other way up: the lug below the rib, by the same 4½" centre to centre.
        Camera top = Unit(StandardView.Top);
        Assert.Equal(4.5, ScreenRect(top, sketch, Bracket("Lug, south")).Center.Y - ScreenRect(top, sketch, Bracket("Rib, north")).Center.Y, Tolerance);
        Assert.Equal(4.5, rib.Center.Y - lug.Center.Y, Tolerance);
    }

    /// <summary>Every box's rectangle measured from the view's own left edge, or mirrored from its right.</summary>
    static List<(double, double, double, double)> Rects(Camera camera, Sketch sketch, bool mirrored)
    {
        Rect all = Extent(camera, sketch);
        return [.. sketch.Entities.Values.OfType<Box>()
            .Select(box => ScreenRect(camera, sketch, box.Id))
            .Select(r => mirrored
                ? (Math.Round(all.Right - r.Right, 9), Math.Round(all.Right - r.Left, 9), Math.Round(r.Top - all.Top, 9), Math.Round(r.Bottom - all.Top, 9))
                : (Math.Round(r.Left - all.Left, 9), Math.Round(r.Right - all.Left, 9), Math.Round(r.Top - all.Top, 9), Math.Round(r.Bottom - all.Top, 9)))
            .Order()];
    }

    [Fact]
    [Trait("Feature", "VIEW-006")]
    public void The_coffee_table_is_the_same_from_front_and_back_and_from_either_end_but_not_both()
    {
        Sketch sketch = Table();
        Assert.Equal(Rects(Unit(StandardView.Front), sketch, mirrored: false), Rects(Unit(StandardView.Back), sketch, mirrored: true));
        Assert.Equal(Rects(Unit(StandardView.Left), sketch, mirrored: false), Rects(Unit(StandardView.Right), sketch, mirrored: true));
        Assert.Equal(48, Extent(Unit(StandardView.Front), sketch).Width, Tolerance);
        Assert.Equal(24, Extent(Unit(StandardView.Right), sketch).Width, Tolerance);
        Assert.Equal(17, Extent(Unit(StandardView.Right), sketch).Height, Tolerance);
    }

    [Theory]
    [Trait("Feature", "VIEW-006")]
    [InlineData(StandardView.Front)]
    [InlineData(StandardView.Back)]
    [InlineData(StandardView.Left)]
    [InlineData(StandardView.Right)]
    public void In_every_elevation_each_leg_is_16_and_a_quarter_tall(StandardView view)
    {
        Sketch sketch = Table();
        Camera camera = StandardViews.CameraFor(view, new Camera(0, 0, 24, 12, 8, 10, Viewport));
        foreach (string leg in (string[])["Leg, south-west", "Leg, south-east", "Leg, north-west", "Leg, north-east"])
        {
            Rect rect = ScreenRect(camera, sketch, Table(leg));
            Assert.Equal(162.5, rect.Height, Tolerance);
            Assert.Equal(25, rect.Width, Tolerance);
        }
    }

    /// <summary>How much of a box's outline a view draws solid, and how much dashed, in inches.</summary>
    static (double Visible, double Hidden) Outline(StandardViewEdges edges, EntityId box)
    {
        double Sum(IEnumerable<FlatSegment> segments) =>
            segments.Where(segment => edges.Polygons[segment.Face].Box == box).Sum(segment => segment.Length);
        return (Sum(edges.Edges.Visible), Sum(edges.Edges.Hidden));
    }

    static StandardViewEdges EdgesOf(Sketch sketch, StandardView view) => StandardViewEdges.Of(ModelScene.Of(sketch), Unit(view), view);

    // §7.1, hand-derived from §1.3: in each view one feature faces the eye and one is behind a part.
    [Theory]
    [Trait("Feature", "VIEW-009")]
    [InlineData(StandardView.Front, "Lug, south", "Rib, north")]
    [InlineData(StandardView.Back, "Rib, north", "Lug, south")]
    [InlineData(StandardView.Right, "Tab, east", "Nub, west")]
    [InlineData(StandardView.Left, "Nub, west", "Tab, east")]
    [InlineData(StandardView.Bottom, "Skid, bottom", "Boss, top")]
    public void Each_view_of_the_l_bracket_sees_one_feature_whole_and_dashes_another(StandardView view, string seen, string behind)
    {
        StandardViewEdges edges = EdgesOf(Bracket(), view);
        (double seenVisible, double seenHidden) = Outline(edges, Bracket(seen));
        (double behindVisible, _) = Outline(edges, Bracket(behind));

        Assert.Equal(0, seenHidden, Tolerance);
        Assert.True(seenVisible >= 2, $"{seen} shows only {seenVisible} inches of outline in {view}");
        Assert.Equal(0, behindVisible, Tolerance);
    }

    [Fact]
    [Trait("Feature", "VIEW-009")]
    public void In_Front_the_rib_behind_the_upright_is_one_dashed_line_at_z_2()
    {
        // The rib (x ½–1, z 2–4) is exactly as wide as the upright in front of it, so its sides lie under
        // the upright's solid sides and its top under the lug's solid bottom (z 4): only its bottom edge,
        // ½" long at z 2, is left to dash.
        StandardViewEdges edges = EdgesOf(Bracket(), StandardView.Front);
        FlatSegment dash = Assert.Single(edges.Edges.Hidden, segment => edges.Polygons[segment.Face].Box == Bracket("Rib, north"));
        Assert.Equal(0.5, dash.Length, Tolerance);
        Assert.Equal((2.0, 2.0), (dash.From.V, dash.To.V));
    }

    [Fact]
    [Trait("Feature", "VIEW-009")]
    public void In_Front_the_coffee_table_dashes_a_short_aprons_end_inside_each_near_leg_and_nothing_else()
    {
        Sketch sketch = Table();
        StandardViewEdges edges = EdgesOf(sketch, StandardView.Front);
        EntityId[] shortAprons = [Table("Apron, short, east"), Table("Apron, short, west")];

        // The far legs and the far long apron stand exactly behind the near ones: no dash for them.
        Assert.All(edges.Edges.Hidden, segment => Assert.Contains(edges.Polygons[segment.Face].Box, shortAprons));
        foreach (EntityId apron in shortAprons)
        {
            FlatSegment[] dashes = [.. edges.Edges.Hidden.Where(segment => edges.Polygons[segment.Face].Box == apron)];
            double low = dashes.Min(d => Math.Min(d.From.V, d.To.V)), high = dashes.Max(d => Math.Max(d.From.V, d.To.V));
            double left = dashes.Min(d => Math.Min(d.From.U, d.To.U)), right = dashes.Max(d => Math.Max(d.From.U, d.To.U));
            Assert.Equal((12.75, 16.25), (low, high));
            Assert.Equal(0.75, right - left, Tolerance);

            // Inside a near leg's 2½ (the legs are inset 1½: x 1½–4 and 44–46½).
            Assert.True((left >= 1.5 && right <= 4) || (left >= 44 && right <= 46.5), $"the dash u {left}–{right} is not inside a leg");

            // Its inner side and its bottom: the apron is flush with the leg's outer face, so its outer
            // side lies under the leg's solid edge, and its top under the table top's at z 16¼.
            Assert.Equal(3.5 + 0.75, dashes.Sum(d => d.Length), Tolerance);
        }
    }

    /// <summary>A view of the l-bracket at 30 px/in, where the fine step is ½" and ticks fall on its features.</summary>
    static StandardViewRulers RulersOf(StandardView view) =>
        StandardViewRulers.Of(StandardViews.CameraFor(view, new Camera(0, 0, 3.75, 2.5, 3, 30, Viewport)), view);

    // §7.2, hand-derived from §1.1's table: which way each ruler's numbers run on the screen.
    [Theory]
    [Trait("Feature", "VIEW-011")]
    [InlineData(StandardView.Front, true, false)]
    [InlineData(StandardView.Back, false, false)]
    [InlineData(StandardView.Left, false, false)]
    [InlineData(StandardView.Right, true, false)]
    [InlineData(StandardView.Top, true, false)]
    [InlineData(StandardView.Bottom, true, true)]
    public void A_rulers_world_values_count_the_way_the_view_looks(StandardView view, bool acrossRises, bool downwardRises)
    {
        StandardViewRulers rulers = RulersOf(view);
        double[] across = [.. rulers.Top.Select(placed => placed.Mark.Inches)];
        double[] downward = [.. rulers.Left.Select(placed => placed.Mark.Inches)];
        Assert.True(across.Length > 5 && downward.Length > 5);
        Assert.Equal(acrossRises ? across.Order() : across.OrderDescending(), across);
        Assert.Equal(downwardRises ? downward.Order() : downward.OrderDescending(), downward);
        Assert.Equal(across.Length, across.Distinct().Count());

        // The marks cover the screen edge to edge, half an inch (15 px) apart, and none is off it.
        Assert.All(rulers.Top, placed => Assert.InRange(placed.Screen, 0, Viewport.Width));
        Assert.All(rulers.Left, placed => Assert.InRange(placed.Screen, 0, Viewport.Height));
        Assert.True(rulers.Top[0].Screen < 15 && rulers.Top[^1].Screen > Viewport.Width - 15);
        Assert.True(rulers.Left[0].Screen < 15 && rulers.Left[^1].Screen > Viewport.Height - 15);
    }

    [Fact]
    [Trait("Feature", "VIEW-011")]
    public void The_boss_edge_at_x_6_and_a_half_reads_6_and_a_half_on_the_horizontal_ruler_in_Front_and_Back()
    {
        foreach (StandardView view in (StandardView[])[StandardView.Front, StandardView.Back])
        {
            Camera camera = StandardViews.CameraFor(view, new Camera(0, 0, 3.75, 2.5, 3, 30, Viewport));
            PlacedTick tick = Assert.Single(StandardViewRulers.Of(camera, view).Top, placed => placed.Mark.Inches == 6.5);
            Assert.Equal(camera.Project(new Vector3d(6.5, 1, 1.75)).X, tick.Screen, 6);
        }

        // And in Right the vertical ruler reads z: the boss's top, z 1¾, sits where the camera puts it.
        Camera right = StandardViews.CameraFor(StandardView.Right, new Camera(0, 0, 3.75, 2.5, 3, 30, Viewport));
        PlacedTick top = Assert.Single(StandardViewRulers.Of(right, StandardView.Right).Left, placed => placed.Mark.Inches == 1.5);
        Assert.Equal(right.Project(new Vector3d(6, 1, 1.5)).Y, top.Screen, 6);
    }

    [Fact]
    [Trait("Feature", "VIEW-011")]
    public void The_grid_in_an_elevation_has_the_floor_heavy_and_lines_where_the_camera_puts_them()
    {
        Camera camera = StandardViews.CameraFor(StandardView.Front, new Camera(0, 0, 3.75, 2.5, 3, 30, Viewport));
        StandardViewRulers rulers = StandardViewRulers.Of(camera, StandardView.Front);
        PlacedGridLine floor = Assert.Single(rulers.Down, placed => placed.Line.World == 0);
        Assert.True(floor.Line.Major);
        Assert.Equal(camera.Project(new Vector3d(0, 0, 0)).Y, floor.Screen, 6);
        Assert.Contains(rulers.Across, placed => placed.Line.World == 7.5 && Math.Abs(placed.Screen - camera.Project(new Vector3d(7.5, 0, 0)).X) < 1e-6);
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
