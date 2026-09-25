using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The handles a part is dragged by in the 3D view (<c>docs/design/assembly-model.md</c> &#xA7;8.3).
/// Split from SpaceSnapResolverTests when the resolver moved to Napkin.Modules.Editing (#166).
/// </summary>
public class ModelHandleArrowTests
{
    static readonly LayerId Layer = LayerId.New();

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
}
