using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;
using Napkin.Modules.Editing;

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
        app.Expect("4 shows Back, and says it is read-only", () =>
        {
            AssertShowing(window, DesignView.Back, "Back");
            Assert.StartsWith("Back view: read-only for now", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Press(Key.O);
        app.Expect("O is inert in Back: still orthographic, still Back", () =>
        {
            Assert.Equal(CameraProjection.Orthographic, window.Model.Camera.Projection);
            AssertShowing(window, DesignView.Back, "Back");
        });

        app.Press(Key.D7);
        app.Expect("and 3D kept its own perspective through all that", () => AssertShowing(window, DesignView.Model, "3D, perspective"));
    });

    /// <summary>The drawing's pixels in a view: the 3D control's left part, clear of the side panels and the status bar.</summary>
    static List<Avalonia.Media.Color> DrawingPixels(AppDriver app, MainWindow window)
    {
        Point origin = window.Model.TranslatePoint(new Point(0, 0), window)!.Value;
        return FrameSampling.Patch(app, (int)origin.X + 8, (int)origin.Y + 8, (int)(window.Model.Bounds.Width * 0.5), (int)(window.Model.Bounds.Height * 0.8));
    }

    [GuiWorkflow("GUI-VIEW-12")]
    public void All_six_views_by_key_chip_and_menu_each_keeps_its_own_camera_and_reads_two_coordinates() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");

        // Hand-derived from the sample (standard-views §1.3): the lug is y 0–½, the rib y 4½–5, the nub
        // x 0–½, the tab x 6½–7½, the boss y ½–1½ against the upright's y ½–4½.
        Vector3d lug = new(0.75, 0.25, 4.5), rib = new(0.75, 4.75, 3), nub = new(0.25, 2.5, 3.375), tab = new(7, 2.5, 0.5);
        Vector3d boss = new(6, 1, 1.25), upright = new(0.75, 2.5, 3.375);

        app.Press(Key.D2);
        app.Expect("2 shows Bottom: X right, south up, so the lug is above the rib", () =>
        {
            AssertShowing(window, DesignView.Bottom, "Bottom");
            Camera camera = window.Model.Camera;
            Assert.Equal(new Vector3d(0, -1, 0), camera.Up);
            Assert.True(camera.Project(lug).Y < camera.Project(rib).Y);
            Assert.True(camera.Project(nub).X < camera.Project(tab).X);
            Assert.Equal("x —   y —", window.CursorReadout.Text);
        });
        app.SaveFrame("bottom-l-bracket");

        app.Click(CentreOf(window, window.ViewChip(DesignView.Back)));
        app.Expect("the Back chip shows Back: west to the right, the nub right of the tab", () =>
        {
            AssertShowing(window, DesignView.Back, "Back");
            Camera camera = window.Model.Camera;
            Assert.Equal(new Vector3d(-1, 0, 0), camera.Right);
            Assert.True(camera.Project(nub).X > camera.Project(tab).X);
            Assert.True(camera.Project(lug).X > camera.Project(boss).X);
            Assert.Equal("x —   z —", window.CursorReadout.Text);
        });
        app.SaveFrame("back-l-bracket");

        app.Press(Key.D5);
        app.Expect("5 shows Left: north to the left, the rib left of the lug", () =>
        {
            AssertShowing(window, DesignView.Left, "Left");
            Camera camera = window.Model.Camera;
            Assert.Equal(new Vector3d(0, -1, 0), camera.Right);
            Assert.True(camera.Project(rib).X < camera.Project(lug).X);
        });
        app.SaveFrame("left-l-bracket");

        // The pointer on the nub's face, which in Left faces the viewer: y 2½ and z 3⅜, and no x.
        app.MoveTo(InModel(window, window.Model.Camera.Project(nub)));
        app.Expect("over the nub the readout in Left is its y and z, and never an x", () =>
        {
            Assert.Equal("y 2 1/2\"   z 3 3/8\"", window.CursorReadout.Text);
            Assert.Equal(BoxNamed(window, "Nub, west").Id, window.Model.HoveredPart);
        });

        // Over empty paper too: the point on the plane through the view's centre, y 5½, north of everything, and z 1 up.
        app.MoveTo(InModel(window, window.Model.Camera.Project(new Vector3d(0, 5.5, 1))));
        app.Expect("over empty paper in Left the readout still reads y and z", () =>
        {
            Assert.Equal("y 5 1/2\"   z 1\"", window.CursorReadout.Text);
            Assert.Null(window.Model.HoveredPart);
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("StandardViewsMenu")!));
        app.Click(CentreOf(window, window.ViewMenuEntry(DesignView.Right)));
        app.Expect("View > Standard views > Right shows Right: south to the left, the boss left of the upright", () =>
        {
            AssertShowing(window, DesignView.Right, "Right");
            Camera camera = window.Model.Camera;
            Assert.Equal(new Vector3d(0, 1, 0), camera.Right);
            Assert.True(camera.Project(boss).X < camera.Project(upright).X);
        });
        app.SaveFrame("right-l-bracket");

        // Each view keeps its own centre and scale for the session (§5.1): pan Right, go away, come back.
        app.Press(Key.Right);
        app.Press(Key.Right);
        Camera rightLeftAt = window.Model.Camera;
        app.Press(Key.D3);
        app.Expect("Front is its own camera, not Right's pan", () =>
        {
            AssertShowing(window, DesignView.Front, "Front");
            Assert.NotEqual(rightLeftAt.Center, window.Model.Camera.Center);
        });
        app.SaveFrame("front-l-bracket");

        app.Press(Key.D6);
        app.Expect("6 comes back to Right exactly where it was panned to", () =>
        {
            AssertShowing(window, DesignView.Right, "Right");
            Assert.Equal(rightLeftAt.Center, window.Model.Camera.Center);
            Assert.Equal(rightLeftAt.PixelsPerInch, window.Model.Camera.PixelsPerInch);
        });

        app.Press(Key.D7);
        app.Expect("7 shows 3D, and its readout has all three coordinates again", () =>
        {
            AssertShowing(window, DesignView.Model, "3D, perspective");
            Assert.Equal("x —   y —   z —", window.CursorReadout.Text);
        });

        // Six views, six different pictures of the bracket (§7.3): the drawing itself, not the status bar.
        List<List<Avalonia.Media.Color>> pictures = [];
        foreach (Key key in (Key[])[Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6])
        {
            app.Press(key);
            pictures.Add(DrawingPixels(app, window));
        }

        app.Expect("the six views of the l-bracket are six pairwise different pictures", () =>
        {
            Assert.Equal(DesignView.Right, window.CurrentView);
            for (int i = 0; i < pictures.Count; i++)
            {
                for (int j = i + 1; j < pictures.Count; j++)
                {
                    Assert.False(pictures[i].SequenceEqual(pictures[j]), $"views {i + 1} and {j + 1} drew the same picture");
                }
            }
        });
    }, defaultLook: true);

    /// <summary>The whole drawing area's pixels, with where the patch starts in the window.</summary>
    static (List<Avalonia.Media.Color> Pixels, int Width, Point Origin) WholeDrawing(AppDriver app, MainWindow window)
    {
        Point origin = window.Model.TranslatePoint(new Point(0, 0), window)!.Value;
        int width = (int)window.Model.Bounds.Width, height = (int)window.Model.Bounds.Height;
        return (FrameSampling.Patch(app, (int)origin.X, (int)origin.Y, width, height), width, origin);
    }

    [GuiWorkflow("GUI-VIEW-13")]
    public void Hidden_edges_toggle_by_menu_and_by_H_only_the_ribs_dash_changes_and_the_choice_is_kept() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");
        EntityId rib = BoxNamed(window, "Rib, north").Id;

        app.Expect("in the plan Hidden edges is not offered", () => Assert.False(window.FindControl<MenuItem>("HiddenEdgesMenuItem")!.IsEnabled));
        app.Press(Key.H);
        app.Expect("H in the plan says where hidden edges are drawn and changes nothing", () =>
        {
            Assert.Equal(MainWindow.HiddenEdgesElsewhere, window.MessageOnScreen);
            Assert.True(window.Settings.Current.ShowHiddenEdges);
        });

        app.Press(Key.D3);
        app.Expect("in Front hidden edges are on, ticked, and the rib behind the upright is dashed", () =>
        {
            // And the readout, coming from the plan, is Front's two coordinates, not the plan's (#129 found it).
            Assert.Equal("x —   z —", window.CursorReadout.Text);
            MenuItem item = window.FindControl<MenuItem>("HiddenEdgesMenuItem")!;
            Assert.True(item.IsEnabled);
            Assert.NotNull(item.Icon);
            Assert.True(window.Model.ShowHiddenEdges);
            StandardViewEdges edges = window.Model.StandardEdges!;
            Assert.Contains(edges.Edges.Hidden, segment => edges.Polygons[segment.Face].Box == rib);
        });
        (List<Avalonia.Media.Color> with, int width, _) = WholeDrawing(app, window);
        app.SaveFrame("front-hidden-edges-on");

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("HiddenEdgesMenuItem")!));
        app.Expect("View > Hidden edges turns them off, unticks it and remembers it", () =>
        {
            Assert.False(window.Model.ShowHiddenEdges);
            Assert.Null(window.FindControl<MenuItem>("HiddenEdgesMenuItem")!.Icon);
            Assert.False(window.Settings.Current.ShowHiddenEdges);
        });

        // Hand-derived (standard-views §1.3): the rib's one dash is its bottom edge, x ½–1 at z 2.
        (List<Avalonia.Media.Color> without, _, _) = WholeDrawing(app, window);
        app.SaveFrame("front-hidden-edges-off");
        app.Expect("only pixels on the rib's dash changed: none anywhere else in the drawing", () =>
        {
            Camera camera = window.Model.Camera;
            Point from = camera.Project(new Vector3d(0.5, 4.5, 2)), to = camera.Project(new Vector3d(1, 4.5, 2));
            int changed = 0;
            for (int i = 0; i < with.Count; i++)
            {
                if (with[i] == without[i])
                {
                    continue;
                }

                changed++;
                double x = i % width, y = i / width;
                Assert.True(
                    x >= Math.Min(from.X, to.X) - 3 && x <= Math.Max(from.X, to.X) + 3 && Math.Abs(y - from.Y) <= 3,
                    $"pixel ({x}, {y}) changed, away from the rib's dash from {from} to {to}");
            }

            Assert.True(changed > 0, "turning hidden edges off changed nothing");
        });

        app.Press(Key.H);
        app.Expect("H turns them back on, and the picture is the one before", () =>
        {
            Assert.True(window.Model.ShowHiddenEdges);
            Assert.True(window.Settings.Current.ShowHiddenEdges);
            Assert.Equal("Hidden edges: shown as light dashes.", window.MessageOnScreen);
            Assert.True(with.SequenceEqual(WholeDrawing(app, window).Pixels));
        });

        app.Press(Key.H);
        app.Expect("off again, and a new window on the same settings opens with them off", () =>
        {
            Assert.False(window.Settings.Current.ShowHiddenEdges);
            var next = new MainWindow(new SettingsStore(window.Settings.Location));
            try
            {
                next.Show();
                Assert.False(next.Model.ShowHiddenEdges);
                Assert.Null(next.FindControl<MenuItem>("HiddenEdgesMenuItem")!.Icon);
            }
            finally
            {
                next.Close();
            }
        });
    }, defaultLook: true);

    [GuiWorkflow("GUI-VIEW-09")]
    public void Front_is_read_only_pan_zoom_and_select_and_the_selection_commands_still_work() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "L-bracket");
        Box lug = BoxNamed(window, "Lug, south");
        Napkin.Modules.Editing.Design before = window.CurrentDesign!;

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

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ProjectMenu")!));
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
