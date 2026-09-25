using System.Collections.Immutable;
using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The 3D view's polygons, its painter's order and its picker, without a window
/// (<c>docs/design/assembly-model.md</c> &#xA7;8.2, &#xA7;8.4).
/// </summary>
public class ModelSceneTests
{
    static readonly Size Viewport = new(900, 600);
    static readonly LayerId Layer = LayerId.New();

    /// <summary>A 48″ × 24″ top ¾″ thick at 16¼″, over a 40″ apron 3½″ deep under its south edge.</summary>
    static (Sketch Sketch, Box Top, Box Apron) TopAndApron()
    {
        Box top = new(EntityId.New(), Layer, Point3.Inches(0, 0, 0) with { Z = new Length(16640) },
            Length.Inches(48), Length.Inches(24), new Length(768), BoxFace.Top, Angle.Zero);
        Box apron = new(EntityId.New(), Layer, new Point3(Length.Inches(4), new Length(1536), new Length(13056)),
            Length.Inches(40), new Length(768), new Length(3584), BoxFace.Top, Angle.Zero);
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Layer, "Parts")).WithEntity(top).WithEntity(apron);
        return (sketch, top, apron);
    }

    static Camera Iso(Sketch sketch) => Camera.Isometric().FitTo(Bounds3.Of(sketch), Viewport);

    [Fact]
    public void A_plain_box_is_six_quads_each_facing_out()
    {
        (Sketch sketch, Box top, _) = TopAndApron();
        ModelScene scene = ModelScene.Of(sketch);

        ScenePolygon[] faces = [.. scene.Polygons.Where(polygon => polygon.Box == top.Id)];
        Assert.Equal(6, faces.Length);
        Assert.All(faces, face => Assert.Equal(4, face.Points.Length));

        // Each outward normal points away from the box's middle.
        Vector3d middle = (Vector3d.From(top.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom)) + Vector3d.From(top.Vertex(BoxCorner.NorthEast, BoxLevel.Top))) / 2;
        foreach (ScenePolygon face in faces)
        {
            Vector3d centre = face.Points.Aggregate(Vector3d.Zero, (sum, point) => sum + point) / face.Points.Length;
            Assert.True(Vector3d.Dot(face.Normal, centre - middle) > 0, $"{face.Of} faces inward.");
        }

        Assert.Equal(new Vector3d(0, 0, 1), faces.Single(face => face.Of == BoxFace.Top).Normal);
        Assert.Equal(new Vector3d(0, -1, 0), faces.Single(face => face.Of == BoxFace.South).Normal);
    }

    [Fact]
    public void From_above_and_the_south_east_three_faces_of_a_box_survive_the_cull()
    {
        (Sketch sketch, Box top, _) = TopAndApron();
        Camera camera = Iso(sketch);

        BoxFace?[] seen = [.. ModelScene.Of(sketch).BackToFront(camera).Where(polygon => polygon.Box == top.Id).Select(polygon => polygon.Of)];

        Assert.Equal(3, seen.Length);
        Assert.Contains(BoxFace.Top, seen);
        Assert.Contains(BoxFace.South, seen);
        Assert.Contains(BoxFace.East, seen);
    }

    [Fact]
    public void Everything_under_the_top_is_painted_before_the_tops_upper_face()
    {
        // The case the furthest-point key alone gets wrong: the top's upper face reaches far back,
        // and on that key alone the apron under it would be painted over it.
        (Sketch sketch, Box top, Box apron) = TopAndApron();
        Camera camera = Iso(sketch);

        IReadOnlyList<ScenePolygon> order = ModelScene.Of(sketch).BackToFront(camera);
        int upper = order.ToList().FindIndex(polygon => polygon.Box == top.Id && polygon.Of == BoxFace.Top);

        Assert.All(
            order.Select((polygon, index) => (polygon, index)).Where(entry => entry.polygon.Box == apron.Id),
            entry => Assert.True(entry.index < upper, $"the apron's {entry.polygon.Of} face is painted over the top."));
    }

    [Fact]
    public void The_plane_tests_say_which_of_two_faces_is_behind_and_decline_when_they_cannot()
    {
        (Sketch sketch, Box top, Box apron) = TopAndApron();
        ModelScene scene = ModelScene.Of(sketch);
        ScenePolygon upper = scene.Polygons.Single(polygon => polygon.Box == top.Id && polygon.Of == BoxFace.Top);
        ScenePolygon apronSouth = scene.Polygons.Single(polygon => polygon.Box == apron.Id && polygon.Of == BoxFace.South);
        ScenePolygon topSouth = scene.Polygons.Single(polygon => polygon.Box == top.Id && polygon.Of == BoxFace.South);

        Assert.Equal(-1, ModelScene.Behind(apronSouth, upper));
        Assert.Equal(1, ModelScene.Behind(upper, apronSouth));

        // Two faces of one convex box: each is behind the other's plane, which says nothing.
        Assert.Equal(0, ModelScene.Behind(upper, topSouth));
    }

    [Fact]
    public void A_rounded_corner_is_chords_and_the_cap_and_the_side_share_its_points()
    {
        Box rounded = new Box(EntityId.New(), Layer, Point3.Origin, Length.Inches(10), Length.Inches(6), Length.Inches(1), BoxFace.Top, Angle.Zero)
        {
            Cuts = [new RoundedCorner(BoxCorner.NorthEast, Length.Inches(2))],
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Layer, "Parts")).WithEntity(rounded);

        ImmutableArray<ScenePolygon> polygons = ModelScene.Of(sketch).Polygons;
        ScenePolygon cap = polygons.Single(polygon => polygon.Of == BoxFace.Top);
        ScenePolygon[] strips = [.. polygons.Where(polygon => polygon.Of is null)];

        // A quarter turn at 7.5° a chord is twelve strips.
        Assert.Equal(12, strips.Length);

        // Every strip's top edge is two consecutive points of the cap's boundary, exactly.
        HashSet<Vector3d> capPoints = [.. cap.Points];
        Assert.All(strips, strip =>
        {
            Assert.Contains(strip.Points[2], capPoints);
            Assert.Contains(strip.Points[3], capPoints);
        });

        // Every chord point is on the circle, in double.
        Vector3d centre = new(8, 4, 1);
        Assert.All(strips.SelectMany(strip => strip.Points).Where(point => point.Z == 1), point =>
            Assert.Equal(2, (point - centre).Length, 9));

        // Only the outermost rulings are edges of the solid.
        Assert.Equal(2, strips.Count(strip => strip.EdgeDrawn[1] || strip.EdgeDrawn[3]));
    }

    [Fact]
    public void A_curved_edge_is_walked_through_its_middle_point()
    {
        IReadOnlyList<Vector3d> points = ModelScene.ChordPoints(new ArcThrough3(
            Point3.Inches(0, 0, 0),
            Point3.Inches(5, 1, 0),
            Point3.Inches(10, 0, 0)));

        Assert.Equal(new Vector3d(0, 0, 0), points[0]);
        Assert.Equal(new Vector3d(10, 0, 0), points[^1]);

        // A shallow bow: every point between the ends is on the bowed side, none beyond an inch.
        Assert.All(points.Skip(1).SkipLast(1), point => Assert.InRange(point.Y, 0, 1 + 1e-9));

        // The same arc walked the other way is the same points in the other order.
        IReadOnlyList<Vector3d> back = ModelScene.ChordPoints(new ArcThrough3(
            Point3.Inches(10, 0, 0),
            Point3.Inches(5, 1, 0),
            Point3.Inches(0, 0, 0)));
        Assert.Equal(points.Reverse(), back);
    }

    [Fact]
    public void Picking_names_the_part_and_the_face_the_ray_entered()
    {
        (Sketch sketch, Box top, Box apron) = TopAndApron();
        Camera camera = Iso(sketch);
        ModelScene scene = ModelScene.Of(sketch);

        ModelPick upper = ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(24, 12, 17)), 5)!;
        Assert.Equal(top.Id, upper.Box);
        Assert.Equal(BoxFace.Top, upper.Face);
        Assert.Equal(Axis.Z, upper.NormalAxis);
        Assert.Equal(BoxFeature.Face(BoxFace.Top), upper.Feature);

        ModelPick front = ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(24, 1.5, 14)), 5)!;
        Assert.Equal(apron.Id, front.Box);
        Assert.Equal(BoxFace.South, front.Face);
        Assert.Equal(Axis.Y, front.NormalAxis);

        Assert.Null(ModelPicker.Pick(sketch, scene, camera, new Point(2, 2), 5));
    }

    [Fact]
    public void Near_a_corner_the_vertex_wins_over_the_edge_and_the_face()
    {
        (Sketch sketch, Box top, _) = TopAndApron();
        Camera camera = Iso(sketch);
        ModelScene scene = ModelScene.Of(sketch);
        Point corner = camera.Project(top.Vertex(BoxCorner.SouthEast, BoxLevel.Top));

        ModelPick vertex = ModelPicker.Pick(sketch, scene, camera, corner + new Vector(1, 1), 5)!;
        Assert.Equal(BoxFeature.Vertex(BoxCorner.SouthEast, BoxLevel.Top), vertex.Feature);

        Point alongEdge = camera.Project(new Vector3d(30, 0, 17));
        ModelPick edge = ModelPicker.Pick(sketch, scene, camera, alongEdge, 5)!;
        Assert.Equal(BoxFeature.Edge(BoxFace.South, BoxFace.Top), edge.Feature);
        Assert.Equal(top.Id, edge.Box);
    }

    [Fact]
    public void A_click_on_a_face_just_under_another_parts_edge_picks_the_face_under_it(/* #89 */)
    {
        // The top overhangs the apron by 1 1/2", so on the screen the apron shows just under the
        // top's front edge. Two pixels under that edge the ray passes beneath the top and meets the
        // apron's south face; the top's edge, two pixels away, used to win the click.
        (Sketch sketch, Box top, Box apron) = TopAndApron();
        Camera camera = Iso(sketch);
        ModelScene scene = ModelScene.Of(sketch);
        Point underTheEdge = camera.Project(new Vector3d(24, 0, 16.25)) + new Vector(0, 2);

        ModelPick pick = ModelPicker.Pick(sketch, scene, camera, underTheEdge, 5)!;

        Assert.Equal(apron.Id, pick.Box);
        Assert.Equal(BoxFace.South, pick.Face);
        Assert.NotEqual(top.Id, pick.Box);
    }

    [Fact]
    public void A_feature_on_the_far_side_of_a_thin_board_is_not_picked_through_it(/* #89 */)
    {
        // Low on the apron's south face, near its bottom: the edge nearest the pointer may be the
        // bottom north edge, 3/4" behind the face, which the ray could reach only through the board.
        (Sketch sketch, _, Box apron) = TopAndApron();
        Camera camera = Iso(sketch);
        ModelScene scene = ModelScene.Of(sketch);

        for (double z = 12.8; z < 14; z += 0.05)
        {
            ModelPick pick = ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(24, 1.5, z)), 5)!;
            Assert.Equal(apron.Id, pick.Box);
            Assert.Contains(BoxFace.South, pick.Feature.Faces);
        }
    }

    [Fact]
    public void Just_off_a_parts_silhouette_with_nothing_behind_its_edge_is_still_picked(/* §3a.7 */)
    {
        // The top alone, high above the floor: two pixels under its front edge the ray meets no
        // part, and the edge — a rail's lower edge, for the strut tool — is what is aimed at.
        (Sketch withApron, Box top, _) = TopAndApron();
        Sketch sketch = withApron.WithoutEntity(withApron.Entities.Values.OfType<Box>().Single(box => box.Id != top.Id).Id);
        Camera camera = Iso(withApron);
        ModelScene scene = ModelScene.Of(sketch);
        Point underTheEdge = camera.Project(new Vector3d(24, 0, 16.25)) + new Vector(0, 2);

        ModelPick pick = ModelPicker.Pick(sketch, scene, camera, underTheEdge, 5)!;

        Assert.Equal(top.Id, pick.Box);
        Assert.Equal(BoxFeature.Edge(BoxFace.South, BoxFace.Bottom), pick.Feature);
    }

    [Fact]
    public void A_click_where_a_cut_took_the_corner_away_picks_what_is_behind_it()
    {
        Box clipped = new Box(EntityId.New(), Layer, Point3.Inches(0, 0, 10), Length.Inches(10), Length.Inches(10), Length.Inches(1), BoxFace.Top, Angle.Zero)
        {
            Cuts = [new CornerCut(BoxCorner.SouthEast, Length.Inches(4), Length.Inches(4))],
        };
        Box floor = new(EntityId.New(), Layer, Point3.Inches(-10, -10, 0), Length.Inches(40), Length.Inches(40), Length.Inches(1), BoxFace.Top, Angle.Zero);
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Layer, "Parts")).WithEntity(clipped).WithEntity(floor);
        Camera camera = Camera.Plan(5, 5, 20, Viewport);
        ModelScene scene = ModelScene.Of(sketch);

        // Straight down on the clipped-off corner: the part below.
        Assert.Equal(floor.Id, ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(9.5, 0.5, 11)), 2)!.Box);

        // Straight down on what is left: the clipped part, its top face.
        ModelPick onIt = ModelPicker.Pick(sketch, scene, camera, camera.Project(new Vector3d(3, 5, 11)), 2)!;
        Assert.Equal(clipped.Id, onIt.Box);
        Assert.Equal(BoxFace.Top, onIt.Face);
    }

    [Theory]
    [InlineData(BoxFace.Top)]
    [InlineData(BoxFace.North)]
    [InlineData(BoxFace.East)]
    [InlineData(BoxFace.Bottom)]
    public void The_slab_test_names_the_face_in_the_boxs_own_frame_whichever_way_it_is_turned(BoxFace faceUp)
    {
        Box box = new(EntityId.New(), Layer, Point3.Inches(2, 3, 4), Length.Inches(5), Length.Inches(6), Length.Inches(7), faceUp, Angle.Right);

        // Straight down on the box's middle: whatever is up is what the ray enters.
        (Point3 low, Point3 high) = Napkin.Modules.Editing.SpaceSnapResolver.Extent(box);
        Vector3d over = (Vector3d.From(low) + Vector3d.From(high)) / 2 + new Vector3d(0, 0, 100);

        (double distance, BoxFace face) = ModelPicker.SlabEntry(box, over, new Vector3d(0, 0, -1))!.Value;

        Assert.Equal(faceUp, face);
        Assert.Equal(100 - ((high.Z - low.Z).ToInches() / 2), distance, 9);
    }
}
