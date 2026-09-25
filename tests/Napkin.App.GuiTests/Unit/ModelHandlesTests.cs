using Avalonia;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Where the selected part's handles are in the 3D view: arrows from its centre (#83), face
/// handles stood out from their faces and never on top of one another (#84).
/// </summary>
public class ModelHandlesTests
{
    static readonly Size Viewport = new(900, 600);

    static Camera FittedTo(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty;
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
        }

        return Camera.Isometric(Viewport).FitTo(Bounds3.Of(sketch), Viewport, coveredRight: 280);
    }

    [Theory]
    [InlineData(BoxFace.Top, 0)]
    [InlineData(BoxFace.South, 0)]
    [InlineData(BoxFace.East, 1)]
    [InlineData(BoxFace.Bottom, 3)]
    public void The_move_arrows_start_at_the_centre_of_the_part_however_it_is_turned(BoxFace faceUp, int quarters)
    {
        Box block = new(EntityId.New(), LayerId.Default, Point3.Inches(10, 20, 30), Length.Inches(40), Length.Inches(3, 1, 2), Length.Inches(0, 3, 4), faceUp, Angle.Right * quarters);
        Camera camera = FittedTo(block);

        (Point3 low, Point3 high) = Napkin.Modules.Editing.SpaceSnapResolver.Extent(block);
        Point middle = camera.Project((Vector3d.From(low) + Vector3d.From(high)) / 2);

        Assert.All(
            ModelHandles.Of(block, camera).Where(handle => handle.Kind == ModelHandleKind.Move),
            arrow => Assert.True(Apart(arrow.Base, middle) < 1e-6, $"the {arrow.Axis} arrow starts at {arrow.Base}, not {middle}."));
    }

    [Theory]
    [InlineData(BoxFace.Top)]
    [InlineData(BoxFace.North)]
    [InlineData(BoxFace.East)]
    public void On_a_thin_board_no_two_handles_are_within_two_grab_distances(BoxFace faceUp)
    {
        // The coffee table's apron: 40" x 3/4" x 3 1/2", framed with the top it hangs under.
        Box top = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0) with { Z = Length.Inches(16, 1, 4) }, Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
        Box board = new(EntityId.New(), LayerId.Default, Point3.Inches(4, 1, 12), Length.Inches(40), Length.Inches(0, 3, 4), Length.Inches(3, 1, 2), faceUp, Angle.Zero);
        Camera camera = FittedTo(top, board);

        ModelHandle[] handles = [.. ModelHandles.Of(board, camera)];
        Assert.Contains(handles, handle => handle.Kind == ModelHandleKind.Face);

        for (int i = 0; i < handles.Length; i++)
        {
            for (int j = i + 1; j < handles.Length; j++)
            {
                double apart = Apart(handles[i].At, handles[j].At);
                Assert.True(
                    apart >= ModelHandles.Separation - 1e-6,
                    $"{Name(handles[i])} and {Name(handles[j])} are {apart:0.0} px apart.");
            }
        }
    }

    [Fact]
    public void A_face_handle_stands_out_from_its_face_and_is_drawn_from_it()
    {
        Box board = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(20), Length.Inches(10), Length.Inches(1), BoxFace.Top, Angle.Zero);
        Camera camera = FittedTo(board);

        ModelHandle south = ModelHandles.Of(board, camera).Single(handle => handle.Face == BoxFace.South);
        Point centre = camera.Project(ModelHandles.CentreOf(board, BoxFace.South));

        Assert.Equal(centre, south.Base);
        Assert.True(Apart(south.At, centre) > 4, "the handle sits on the face.");

        // Out, not in: along the south face's normal (-Y) as the screen shows it.
        Vector outward = camera.ProjectDirection(new Vector3d(0, -1, 0));
        Vector standing = new(south.At.X - centre.X, south.At.Y - centre.Y);
        Assert.True((standing.X * outward.X) + (standing.Y * outward.Y) > 0, "the handle stands inside the part.");
    }

    static double Apart(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    static string Name(ModelHandle handle) =>
        handle.Kind == ModelHandleKind.Move ? $"the {handle.Axis} arrow" : $"the {handle.Face} face handle";
}
