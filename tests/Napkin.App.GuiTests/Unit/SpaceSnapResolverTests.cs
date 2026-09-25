using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Where a part dragged in the 3D view lands, and what the drop would say
/// (<c>docs/design/assembly-model.md</c> &#xA7;8.3), plus the handles it is dragged by.
/// </summary>
public class SpaceSnapResolverTests
{
    static readonly LayerId Layer = LayerId.New();
    static readonly Length Radius = new(512);

    static Box Block(double x, double y, double z, double w, double h, double d, BoxFace faceUp = BoxFace.Top) => new(
        EntityId.New(),
        Layer,
        new Point3(Inches(x), Inches(y), Inches(z)),
        Inches(w),
        Inches(h),
        Inches(d),
        faceUp,
        Angle.Zero);

    static Length Inches(double inches) => Length.FromInches(inches, Rounding.HalfToEven);

    static Sketch SketchOf(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Layer, "Parts"));
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
        }

        return sketch;
    }

    [Fact]
    public void Rising_under_a_top_catches_its_underside_and_says_flush()
    {
        Box top = Block(0, 0, 16.25, 48, 24, 0.75);
        Box apron = Block(4, 1.5, 9, 40, 0.75, 3.5);
        Sketch sketch = SketchOf(top, apron);

        // Wanted: the apron's top a quarter inch short of the underside.
        Point3 wanted = apron.Anchor with { Z = Inches(16.25 - 3.5 - 0.25) };
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(sketch, apron, wanted, [Axis.Z], 1, Radius);

        Assert.Equal(Inches(12.75), plan.Anchor.Z);
        Assert.Equal(apron.Anchor.X, plan.Anchor.X);
        SpaceSnapHit hit = Assert.Single(plan.Hits);
        Assert.Equal(SnapKind.Edge, hit.Kind);
        Assert.Equal(top.Id, hit.Target);
        Assert.Equal(BoxFace.Bottom, hit.TargetFace);
        Assert.Equal(BoxFace.Top, hit.MovingFace);

        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)), flush.A);
        Assert.Equal(new FeatureRef(apron.Id, BoxFeature.Face(BoxFace.Top)), flush.B);
    }

    [Fact]
    public void A_face_to_meet_beats_a_face_to_be_level_with_at_the_same_height()
    {
        // A leg's top is at the very height of the top's underside, touching the apron at its end.
        // The apron's top face would be level with the one and meet the other: meeting wins.
        Box top = Block(0, 0, 16.25, 48, 24, 0.75);
        Box leg = Block(1.5, 1.5, 0, 2.5, 2.5, 16.25);
        Box apron = Block(4, 1.5, 12, 40, 0.75, 3.5);
        Sketch sketch = SketchOf(leg, top, apron);

        Point3 wanted = apron.Anchor with { Z = Inches(12.75) };
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(sketch, apron, wanted, [Axis.Z], 1, Radius);

        Assert.Equal(top.Id, Assert.Single(plan.Hits).Target);
    }

    [Fact]
    public void Nothing_near_lands_on_the_grid_and_says_nothing()
    {
        Box top = Block(0, 0, 16.25, 48, 24, 0.75);
        Box apron = Block(4, 1.5, 9, 40, 0.75, 3.5);

        Point3 wanted = apron.Anchor with { Z = Inches(7.4) };
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(SketchOf(top, apron), apron, wanted, [Axis.Z], 1, Radius);

        Assert.Equal(Inches(7), plan.Anchor.Z);
        Assert.Equal(SnapKind.Grid, Assert.Single(plan.Hits).Kind);
        Assert.Empty(plan.Relationships);
        Assert.False(plan.CaughtSomething);
    }

    [Fact]
    public void A_face_across_the_room_at_the_same_height_is_not_caught()
    {
        // Same Z as a shelf three feet away, but they do not overlap in the plan.
        Box shelf = Block(80, 0, 16.25, 10, 10, 0.75);
        Box apron = Block(4, 1.5, 12.5, 40, 0.75, 3.5);

        Point3 wanted = apron.Anchor with { Z = Inches(12.75) };
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(SketchOf(shelf, apron), apron, wanted, [Axis.Z], 1, Radius);

        Assert.False(plan.CaughtSomething);
    }

    [Fact]
    public void Only_the_dragged_axes_move()
    {
        Box block = Block(0, 0, 0, 2, 2, 2);

        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(
            SketchOf(block),
            block,
            new Point3(Inches(5), Inches(6), Inches(7)),
            [Axis.X],
            1,
            Radius);

        Assert.Equal(new Point3(Inches(5), Length.Zero, Length.Zero), plan.Anchor);
    }

    [Fact]
    public void Two_axes_caught_on_one_part_meet_at_an_edge_and_say_coincident()
    {
        // Sliding a block on a table top until its north-east upright meets another block's
        // south-west one: X and Y both caught on the same part.
        Box fixedBlock = Block(10, 10, 0, 4, 4, 4);
        Box moving = Block(0, 0, 0, 4, 4, 4);

        Point3 wanted = new(Inches(5.8), Inches(6.1), Length.Zero);
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(SketchOf(fixedBlock, moving), moving, wanted, [Axis.X, Axis.Y], 1, Radius);

        Assert.Equal(new Point3(Inches(6), Inches(6), Length.Zero), plan.Anchor);
        Assert.All(plan.Hits, hit => Assert.Equal(SnapKind.Corner, hit.Kind));
        Coincident coincident = Assert.IsType<Coincident>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(fixedBlock.Id, BoxFeature.Edge(BoxFace.South, BoxFace.West)), coincident.A);
        Assert.Equal(new FeatureRef(moving.Id, BoxFeature.Edge(BoxFace.North, BoxFace.East)), coincident.B);
    }

    [Fact]
    public void Three_axes_caught_on_one_part_put_a_vertex_on_a_vertex()
    {
        Box fixedBlock = Block(10, 10, 10, 4, 4, 4);
        Box moving = Block(0, 0, 0, 4, 4, 4);

        Point3 wanted = new(Inches(6.2), Inches(5.9), Inches(6.1));
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(SketchOf(fixedBlock, moving), moving, wanted, [Axis.X, Axis.Y, Axis.Z], 1, Radius);

        Assert.Equal(new Point3(Inches(6), Inches(6), Inches(6)), plan.Anchor);
        Coincident coincident = Assert.IsType<Coincident>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(fixedBlock.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom)), coincident.A);
        Assert.Equal(new FeatureRef(moving.Id, BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)), coincident.B);
    }

    [Fact]
    public void A_turned_part_is_caught_by_the_face_that_now_faces_that_way()
    {
        // A leg tipped back (north face up): its north face is its top in the world.
        Box top = Block(0, 0, 16.25, 48, 24, 0.75);
        Box lying = Block(10, 10, 0, 2.5, 2.5, 16.25, BoxFace.North);
        Sketch sketch = SketchOf(top, lying);

        // Its world top is at Z = Height = 2.5; lift it so that is just under the underside.
        Point3 wanted = lying.Anchor with { Z = Inches(16.25 - 2.5 - 0.2) };
        SpaceSnapPlan plan = SpaceSnapResolver.Resolve(sketch, lying, wanted, [Axis.Z], 1, Radius);

        Assert.Equal(BoxFace.North, Assert.Single(plan.Hits).MovingFace);
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(lying.Id, BoxFeature.Face(BoxFace.North)), flush.B);
    }

    [Fact]
    public void The_handles_are_three_arrows_and_the_three_faces_the_eye_can_see()
    {
        Box block = Block(0, 0, 0, 4, 4, 4);
        Camera camera = Camera.Isometric(new Size(900, 600)) with { PixelsPerInch = 20 };

        IReadOnlyList<ModelHandle> handles = ModelHandles.Of(block, camera);

        Assert.Equal([Axis.X, Axis.Y, Axis.Z], handles.Where(handle => handle.Kind == ModelHandleKind.Move).Select(handle => handle.Axis));
        Assert.Equal(
            [BoxFace.South, BoxFace.East, BoxFace.Top],
            handles.Where(handle => handle.Kind == ModelHandleKind.Face).Select(handle => handle.Face!.Value));

        // Every arrow is the same length on the screen, from the part's centre (#83).
        Point middle = camera.Project(ModelHandles.Centre(block));
        Assert.All(handles.Where(handle => handle.Kind == ModelHandleKind.Move), arrow =>
        {
            Assert.Equal(middle, arrow.Base);
            Assert.Equal(ModelHandles.ArrowPixels, ((Vector)(arrow.At - arrow.Base)).Length, 9);
        });

        // Grabbing the Z arrow's tip gets the Z arrow; grabbing nothing gets nothing.
        ModelHandle z = handles.Single(handle => handle.Kind == ModelHandleKind.Move && handle.Axis == Axis.Z);
        Assert.Equal(z, ModelHandles.At(block, camera, z.At + new Vector(2, 1), 8));
        Assert.Null(ModelHandles.At(block, camera, new Point(-500, -500), 8));
    }

    [Fact]
    public void Looking_straight_down_there_is_no_arrow_along_the_line_of_sight()
    {
        Box block = Block(0, 0, 0, 4, 4, 4);
        Camera camera = Camera.Plan(0, 0, 20, new Size(900, 600));

        IReadOnlyList<ModelHandle> handles = ModelHandles.Of(block, camera);

        Assert.DoesNotContain(handles, handle => handle.Kind == ModelHandleKind.Move && handle.Axis == Axis.Z);
        ModelHandle face = Assert.Single(handles, handle => handle.Kind == ModelHandleKind.Face);
        Assert.Equal(BoxFace.Top, face.Face);
    }

    [Fact]
    public void Turning_the_selected_part_is_one_undo_and_turning_back_restores_it()
    {
        Box block = Block(0, 0, 0, 4, 2, 1);
        DesignEditor editor = new();
        editor.Open(new Napkin.Modules.Editing.Design("turning", SketchOf(block), System.Collections.Immutable.ImmutableDictionary<EntityId, string>.Empty));
        editor.Select(block.Id);

        UpdateResult? result = SelectionTurn.Turn(editor, Axis.X, 1);

        Assert.IsType<Solved>(result);
        Box turned = editor.Sketch.Find<Box>(block.Id)!;
        Assert.Equal(new Orientation(BoxFace.North, Angle.Zero), turned.Orientation);

        // In place (#75): the low corner of the extent stays, so the anchor is what moves.
        Assert.Equal(SpaceSnapResolver.Extent(block).Low, SpaceSnapResolver.Extent(turned).Low);

        // Turned back, it is the block it was, anchor and all.
        SelectionTurn.Turn(editor, Axis.X, -1);
        Assert.Equal(block, editor.Sketch.Find<Box>(block.Id));

        Assert.True(editor.Undo());
        Assert.Equal(BoxFace.North, editor.Sketch.Find<Box>(block.Id)!.FaceUp);
    }

    [Fact]
    public void Turning_with_nothing_selected_asks_for_a_part_and_changes_nothing()
    {
        DesignEditor editor = new();

        Assert.Null(SelectionTurn.Turn(editor, Axis.Z, 1));
        Assert.Contains("Select a part", editor.LastMessage!.Text, StringComparison.Ordinal);
    }
}
