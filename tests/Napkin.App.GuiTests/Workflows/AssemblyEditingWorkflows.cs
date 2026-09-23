using Avalonia;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Adding and changing parts in the 3D view, driven the way a person drives it: the workflows
/// behind the 3D editing review's issues (#68).
/// </summary>
public class AssemblyEditingWorkflows
{
    [GuiWorkflow("GUI-ASSEM-03")]
    public void The_part_panel_follows_a_resize_in_the_3D_view_and_apply_keeps_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box apron = BoxNamed(window, "Apron, long, south");

        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.V);

        // Grow the apron upward by its top face's handle, two grid steps.
        ModelHandle handle = window.Model.FaceHandle(BoxFace.Top)
            ?? throw new InvalidOperationException("The apron's top face has no handle.");
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        double up = perInch * window.Model.GridStepInches * 2;
        app.Drag(
            InModel(window, handle.At),
            InModel(window, handle.At - new Vector(0, up / 2)),
            InModel(window, handle.At - new Vector(0, up)));

        Box grown = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
        string grownText = grown.Depth.Format(window.Editor.LabelFormat).Text;

        app.Expect("the apron grew, and the panel shows the depth it has now, not the one it had", () =>
        {
            Assert.True(grown.Depth > apron.Depth, "the resize did not make the apron deeper.");
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            Assert.Equal(grownText, window.OutOfPlaneField.Text);
        });

        // Rename it in the panel and apply: the resize must survive, and nothing may pin the depth.
        app.Click(CentreOf(window, window.PartNameField));
        app.Chord(Key.A);
        app.Type("Apron, front");
        app.Click(CentreOf(window, window.ApplyPart));

        app.Expect("the rename kept the resized depth and stated no typed depth", () =>
        {
            Box renamed = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal("Apron, front", renamed.Name);
            Assert.Equal(grown.Depth, renamed.Depth);
            Assert.DoesNotContain(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>(),
                value => value.Param == new BoxDepthRef(apron.Id));
        });

        app.SaveFrame("renamed-after-resize");

        // Two undos: the name, then the resize. The panel follows both without a reselect.
        app.Chord(Key.Z);
        app.Chord(Key.Z);

        app.Expect("after both undos the panel shows the original name and depth", () =>
        {
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            Assert.Equal(apron, window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!);
            Assert.Equal(apron.Name, window.PartNameField.Text);
            Assert.Equal(apron.Depth.Format(window.Editor.LabelFormat).Text, window.OutOfPlaneField.Text);
        });
    });
}
