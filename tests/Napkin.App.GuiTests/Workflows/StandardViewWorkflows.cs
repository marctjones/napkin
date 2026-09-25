using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The standard 2D views (docs/design/standard-views.md §4, §5, §7.4): switched by key, chip and
/// menu, remembered, and read-only — pan, zoom and select, with the selection's menu commands live.
/// </summary>
public class StandardViewWorkflows
{
    /// <summary>The window's three signs of the view agree: the status text, the pressed chip, the menu tick.</summary>
    static void AssertShowing(MainWindow window, DesignView view, string readout)
    {
        Assert.Equal(view, window.CurrentView);
        Assert.Equal(readout, window.ViewReadout);
        foreach (DesignView each in Enum.GetValues<DesignView>())
        {
            Assert.Equal(each == view, window.ViewChip(each).IsChecked == true);
            Assert.Equal(each == view, window.ViewMenuEntry(each).Icon is not null);
        }

        // Top is the plan canvas; every other view is the 3D view's control, locked or free.
        Assert.Equal(view != DesignView.Top, window.IsShowingModel);
        if (view != DesignView.Top)
        {
            Assert.Equal(StandardViews.Of(view), window.Model.Locked);
        }
    }

    [GuiWorkflow("GUI-VIEW-08")]
    public void Switch_between_Top_Front_and_3D_by_key_chip_and_menu_and_V_goes_back() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");

        app.Expect("the L-bracket opens on the plan, Top, and every sign says so", () => AssertShowing(window, DesignView.Top, "Top"));

        app.Press(Key.D3);
        app.Expect("3 shows Front: locked, orthographic, looking north with up up", () =>
        {
            AssertShowing(window, DesignView.Front, "Front");
            Camera camera = window.Model.Camera;
            Assert.Equal(CameraProjection.Orthographic, camera.Projection);
            Assert.Equal(new Vector3d(1, 0, 0), camera.Right);
            Assert.Equal(new Vector3d(0, 0, 1), camera.Up);
            Assert.StartsWith("Front view: read-only for now", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // Hand-derived from the sample: in Front the lug (x ½–1, z 4–5) is left of and above the boss
        // (x 5½–6½, z ¾–1¾); the nub (x 0–½) is the leftmost thing and the tab (x 6½–7½) the rightmost.
        app.Expect("the picture is the front of the bracket: lug up left, boss low right, nub and tab at the ends", () =>
        {
            Camera camera = window.Model.Camera;
            Point lug = camera.Project(new Vector3d(0.75, 0.25, 4.5));
            Point boss = camera.Project(new Vector3d(6, 1, 1.25));
            Point nub = camera.Project(new Vector3d(0, 2.5, 3.375));
            Point tab = camera.Project(new Vector3d(7.5, 2.5, 0.5));
            Assert.True(lug.X < boss.X && lug.Y < boss.Y);
            Assert.True(nub.X < lug.X && tab.X > boss.X);
            Assert.Equal(1.0, (boss.X - lug.X) / (5.25 * camera.PixelsPerInch), 9);
        });
        app.SaveFrame("front-l-bracket");

        app.Press(Key.D7);
        app.Expect("7 shows 3D, in the perspective the person left it in", () => AssertShowing(window, DesignView.Model, "3D, perspective"));

        app.Press(Key.V);
        app.Expect("V from 3D goes back to the 2D view last shown: Front, not the plan", () => AssertShowing(window, DesignView.Front, "Front"));

        app.Press(Key.V);
        app.Press(Key.V);
        app.Expect("V twice lands back where it was", () => AssertShowing(window, DesignView.Front, "Front"));

        app.Click(CentreOf(window, window.ViewChip(DesignView.Top)));
        app.Expect("the Top chip shows the plan", () => AssertShowing(window, DesignView.Top, "Top"));

        app.Click(CentreOf(window, window.ViewChip(DesignView.Model)));
        app.Expect("the 3D chip shows 3D", () => AssertShowing(window, DesignView.Model, "3D, perspective"));

        app.Click(CentreOf(window, window.ViewChip(DesignView.Front)));
        app.Expect("the Front chip shows Front", () => AssertShowing(window, DesignView.Front, "Front"));

        app.Click(CentreOf(window, window.ViewChip(DesignView.Top)));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("StandardViewsMenu")!));
        app.Click(CentreOf(window, window.ViewMenuEntry(DesignView.Front)));
        app.Expect("View > Standard views > Front shows Front", () => AssertShowing(window, DesignView.Front, "Front"));

        app.Press(Key.D4);
        app.Expect("Back is not offered yet: 4 leaves Front showing and says so", () =>
        {
            AssertShowing(window, DesignView.Front, "Front");
            Assert.False(window.ViewChip(DesignView.Back).IsEnabled);
            Assert.StartsWith("The Back view is not built yet", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Press(Key.O);
        app.Expect("O is inert in Front: still orthographic, still Front", () =>
        {
            Assert.Equal(CameraProjection.Orthographic, window.Model.Camera.Projection);
            AssertShowing(window, DesignView.Front, "Front");
        });

        app.Press(Key.D7);
        app.Expect("and 3D kept its own perspective through all that", () => AssertShowing(window, DesignView.Model, "3D, perspective"));
    });

    [GuiWorkflow("GUI-VIEW-09")]
    public void Front_is_read_only_pan_zoom_and_select_and_the_selection_commands_still_work() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");
        Box lug = BoxNamed(window, "Lug, south");
        Napkin.App.Designs.Design before = window.CurrentDesign!;

        app.Press(Key.D3);
        app.Expect("in Front the drawing tools are put down and say why", () =>
        {
            Assert.False(window.FindControl<ToggleButton>("RectangleToolButton")!.IsEnabled);
            Assert.StartsWith("Not in a Front view yet", ToolTip.GetTip(window.FindControl<ToggleButton>("RectangleToolButton")!) as string, StringComparison.Ordinal);
            Assert.All(window.TurnButtons, button => Assert.False(button.IsVisible));
            Assert.All(window.ViewSnapButtons, button => Assert.False(button.IsEffectivelyVisible));
        });

        // A click on the lug's face selects it; no handles appear, since nothing here can drag them.
        Point onLug = InModel(window, window.Model.Camera.Project(new Vector3d(0.75, 0.25, 4.5)));
        app.Click(onLug);
        app.Expect("a click on the lug selects it, and no handle is offered", () =>
        {
            Assert.Equal([lug.Id], window.Editor.Selection.Order());
            Assert.Empty(window.Model.Handles);
        });

        // A drag on the selected lug pans; it does not move the lug.
        Camera beforeDrag = window.Model.Camera;
        app.Drag(onLug, onLug + new Vector(30, 10), onLug + new Vector(60, 20));
        app.Expect("a drag on the lug pans the view and leaves the drawing alone", () =>
        {
            Assert.Same(before.Sketch, window.CurrentDesign!.Sketch);
            Assert.Equal(beforeDrag.PixelsPerInch, window.Model.Camera.PixelsPerInch);
            Point moved = window.Model.Camera.Project(new Vector3d(0.75, 0.25, 4.5));
            Point was = beforeDrag.Project(new Vector3d(0.75, 0.25, 4.5));
            Assert.Equal(60, moved.X - was.X, 6);
            Assert.Equal(20, moved.Y - was.Y, 6);
            Assert.Equal(new Vector3d(0, 0, 1), window.Model.Camera.Up);
        });

        app.Press(Key.Left);
        app.Expect("the left arrow pans rather than orbits", () =>
        {
            Assert.Equal(new Vector3d(1, 0, 0), window.Model.Camera.Right);
            Assert.True(window.Model.Camera.CenterX < beforeDrag.CenterX);
        });

        app.Press(Key.Home);
        app.Expect("Home fits the bracket again", () =>
        {
            Assert.Equal(beforeDrag.CenterX, window.Model.Camera.CenterX, 6);
            Assert.Equal(beforeDrag.CenterZ, window.Model.Camera.CenterZ, 6);
        });

        // The selection's commands are requests to the editor, not view gestures: they stay live.
        app.Press(Key.Delete);
        app.Expect("Delete removes the selected lug from Front", () => Assert.Null(window.CurrentDesign!.Sketch.Find<Box>(lug.Id)));
        app.SaveFrame("front-lug-deleted");

        app.Chord(Key.Z);
        app.Expect("and Undo puts it back, still in Front", () =>
        {
            Assert.Equal(lug, window.CurrentDesign!.Sketch.Find<Box>(lug.Id));
            Assert.Equal(DesignView.Front, window.CurrentView);
        });
    });

    [GuiWorkflow("GUI-VIEW-10")]
    public void A_number_typed_as_a_length_after_a_drag_is_a_length_not_a_view() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Coffee table");
        Box apron = BoxNamed(window, "Apron, long, south");
        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.D7);
        app.Expect("7 shows 3D with the apron selected", () =>
        {
            Assert.Equal(DesignView.Model, window.CurrentView);
            Assert.Equal([apron.Id], window.Editor.Selection.Order());
        });

        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        ModelHandle arrow = window.Model.MoveHandle(Axis.Z)!;
        app.Drag(InModel(window, arrow.At), InModel(window, arrow.At + new Vector(0, perInch)), InModel(window, arrow.At + new Vector(0, perInch * 2)));

        // A real keyboard sends the key and then its text: 3 would be Front, but a length is pending.
        app.Press(Key.D3);
        app.Type("3");
        app.Expect("3 after an arrow drag goes into the length, and the view stays 3D", () =>
        {
            Assert.Equal(DesignView.Model, window.CurrentView);
            Assert.StartsWith("3 — Enter", window.Model.LiveReadout, StringComparison.Ordinal);
        });

        app.Press(Key.Enter);
        app.Expect("Enter moves the apron exactly 3\" down", () =>
            Assert.Equal(apron.Anchor.Z - Length.Inches(3), window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!.Anchor.Z));

        // With nothing pending the same key is the view again.
        app.Press(Key.D3);
        app.Expect("with no length pending, 3 shows Front", () => Assert.Equal(DesignView.Front, window.CurrentView));
    });

    [GuiWorkflow("GUI-SET-07")]
    public void The_last_view_is_remembered_and_the_2D_plan_choice_opens_Top() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");
        app.Press(Key.D3);
        app.Expect("Front is remembered as the last view", () => Assert.Equal(DesignView.Front, window.Settings.Current.LastView));

        OpenSample(app, window, "Coffee table");
        app.Expect("with Last used, the next design opens in Front too", () =>
        {
            Assert.Equal(DesignView.Front, window.CurrentView);
            Assert.Equal(DesignView.Front, window.Model.Locked is { } locked ? StandardViews.ToDesignView(locked) : DesignView.Model);
        });

        app.Expect("a new window on the same settings opens a design in Front", () =>
        {
            var next = new MainWindow(new SettingsStore(window.Settings.Location));
            try
            {
                next.Show();
                Assert.True(next.ShowDesign(new NewSheet()));
                Assert.Equal(DesignView.Front, next.CurrentView);
                Assert.Equal("Front", next.ViewReadout);
            }
            finally
            {
                next.Close();
            }
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("OpenInMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("OpenInPlanMenuItem")!));
        OpenSample(app, window, "L-bracket");
        app.Expect("Open designs in > 2D plan opens the next design on Top", () =>
        {
            Assert.Equal(DesignView.Top, window.CurrentView);
            Assert.Equal("Top", window.ViewReadout);
            Assert.Equal(DesignView.Top, window.Settings.Current.LastView);
        });
    });
}
