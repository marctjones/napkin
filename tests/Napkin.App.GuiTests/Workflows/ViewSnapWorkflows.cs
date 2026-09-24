using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>The 3D view's quick view-snap buttons (#132), clicked and read back from the camera.</summary>
public class ViewSnapWorkflows
{
    static (double, double) Angles(MainWindow window) =>
        (Math.Round(window.Model.Camera.AzimuthDegrees, 6), Math.Round(window.Model.Camera.ElevationDegrees, 6));

    [GuiWorkflow("GUI-VIEW-07")]
    public void The_snap_buttons_look_along_each_axis_and_step_through_a_selected_parts_faces() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        Button x = window.FindControl<Button>("LookXToolButton")!;
        Button y = window.FindControl<Button>("LookYToolButton")!;
        Button z = window.FindControl<Button>("LookZToolButton")!;
        Button surface = window.FindControl<Button>("NextSurfaceToolButton")!;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");

        app.Expect("in the plan the snap buttons are hidden", () =>
            Assert.All(window.ViewSnapButtons, button => Assert.False(button.IsEffectivelyVisible)));

        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.V);

        app.Expect("in 3D the four buttons show", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.All(window.ViewSnapButtons, button => Assert.True(button.IsEffectivelyVisible));
        });

        app.Click(CentreOf(window, z));
        app.Expect("Z looks from above", () => Assert.Equal((0.0, 90.0), Angles(window)));

        app.Click(CentreOf(window, z));
        app.Expect("Z again looks from below", () => Assert.Equal((0.0, -90.0), Angles(window)));

        app.Click(CentreOf(window, y));
        app.Expect("Y looks from the south", () => Assert.Equal((0.0, 0.0), Angles(window)));

        app.Click(CentreOf(window, x));
        app.Expect("X looks from the east", () => Assert.Equal((90.0, 0.0), Angles(window)));

        app.Click(CentreOf(window, x));
        app.Expect("X again looks from the west", () => Assert.Equal((270.0, 0.0), Angles(window)));
        app.SaveFrame("3d-snap-x-west");

        // The selected leg's faces, top first, centred on it.
        (double, double)[] faces = [(0, 90), (0, 0), (90, 0), (180, 0), (270, 0), (0, -90)];
        for (int i = 0; i < faces.Length; i++)
        {
            app.Click(CentreOf(window, surface));
            (double az, double el) = faces[i];
            app.Expect($"surface step {i + 1} looks at the leg's face {i + 1}, with that face centred", () =>
            {
                Assert.Equal((az, el), Angles(window));
                Point at = window.Model.Camera.Project(ModelHandles.CentreOf(BoxNamed(window, "Leg, north-east"), ViewSnap.FaceAt(i)));
                Camera camera = window.Model.Camera;
                Assert.Equal((camera.Viewport.Width - window.Model.FitReserveRight) / 2, at.X, 3);
                Assert.Equal(camera.Viewport.Height / 2, at.Y, 3);
            });
        }

        app.Click(CentreOf(window, surface));
        app.Expect("the cycle wraps back to the top face", () => Assert.Equal((0.0, 90.0), Angles(window)));
        app.SaveFrame("3d-snap-leg-top");

        // Dark theme frame of a snapped view.
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeDarkMenuItem")!));
        app.Click(CentreOf(window, x));
        app.SaveFrame("3d-snap-dark");
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeLightMenuItem")!));

        // Nothing selected: the six axes frame the whole design, restarting at the top.
        app.Click(InModel(window, new Point(window.Model.Bounds.Width * 0.5, window.Model.Bounds.Height * 0.9)));
        app.Expect("nothing is selected", () => Assert.Empty(window.Editor.Selection));
        app.Click(CentreOf(window, surface));
        app.Expect("the first step with nothing selected is from above", () => Assert.Equal((0.0, 90.0), Angles(window)));
        app.Click(CentreOf(window, surface));
        app.Expect("the second is from the south", () => Assert.Equal((0.0, 0.0), Angles(window)));

        // Back in the plan the buttons go again.
        app.Press(Key.V);
        app.Expect("the plan hides the snap buttons", () =>
            Assert.All(window.ViewSnapButtons, button => Assert.False(button.IsEffectivelyVisible)));
    });
}
