using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The regrouped menus (#169) in the default look, in a small window: File, Edit, Draw (tools
/// only), View, Project and Lists, each opened with the mouse and its frame saved to be read, and
/// every item of every open menu and submenu inside a 900×600 window.
/// </summary>
public class MenuLayoutWorkflows
{
    [GuiWorkflow("GUI-SHELL-06")]
    public void Every_menu_and_submenu_opens_inside_a_900_by_600_window() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        app.ResizeWindow(900, 600);
        OpenSample(app, window, "Coffee table");

        app.Expect("the menu bar reads File, Edit, Draw, View, Project, Lists", () =>
            Assert.Equal(
                ["FileMenu", "EditMenu", "DrawMenu", "ViewMenu", "ProjectMenu", "ListsMenu"],
                window.MenuBar.Items.OfType<MenuItem>().Select(item => item.Name)));

        Open(app, window, "file", "FileMenu");
        Open(app, window, "file-samples", "FileMenu", "SamplesMenu");
        Open(app, window, "edit", "EditMenu");
        Open(app, window, "draw", "DrawMenu");
        Open(app, window, "draw-walls", "DrawMenu", "WallsMenu");
        Open(app, window, "draw-stock", "DrawMenu", "StockMenu");
        Open(app, window, "view", "ViewMenu");
        Open(app, window, "view-standard-views", "ViewMenu", "StandardViewsMenu");
        Open(app, window, "view-look-along", "ViewMenu", "LookAlongMenu");
        Open(app, window, "view-appearance-theme", "ViewMenu", "AppearanceMenu", "ThemeMenuItem");
        Open(app, window, "view-appearance-paper", "ViewMenu", "AppearanceMenu", "SketchMenuItem");
        Open(app, window, "project", "ProjectMenu");
        Open(app, window, "project-open-in", "ProjectMenu", "OpenInMenuItem");
        Open(app, window, "lists", "ListsMenu");

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ListsMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("FastenerSizesMenuItem")!));
        app.Expect("Lists → Fastener sizes and supplies opens the lists on that tab", () =>
            Assert.True(window.OpenCutList().IsShowingSizes, "the sizes tab is not showing."));
        window.OpenCutList().Close();

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ProjectMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("SawKerfMenuItem")!));
        app.Expect("Project → Saw kerf opens the cut layout with the kerf field ready to type into", () =>
        {
            CutListWindow lists = window.OpenCutList();
            Assert.True(lists.IsShowingCutLayout, "the cut layout is not showing.");
            Assert.True(lists.KerfField.IsFocused, "the kerf field does not have the keyboard.");
        });
        window.OpenCutList().Close();
    }, defaultLook: true);

    /// <summary>
    /// Opens a menu and its submenus in turn by clicking each, checks that every item now showing
    /// lies inside the window, saves the frame, and closes the menu again with Escape.
    /// </summary>
    static void Open(AppDriver app, MainWindow window, string frame, params string[] path)
    {
        foreach (string name in path)
        {
            app.Click(CentreOf(window, window.FindControl<MenuItem>(name)!));
        }

        app.Expect($"{string.Join(" → ", path)} fits in the window", () =>
        {
            MenuItem last = window.FindControl<MenuItem>(path[^1])!;
            Assert.True(last.IsSubMenuOpen, $"{path[^1]} did not open.");
            List<MenuItem> showing = [.. window.GetVisualDescendants().OfType<MenuItem>().Where(item => item.IsEffectivelyVisible)];
            Assert.NotEmpty(showing);
            foreach (MenuItem item in showing)
            {
                Point? topLeft = item.TranslatePoint(new Point(0, 0), window);
                Assert.True(topLeft.HasValue, $"{item.Name ?? item.Header} is not in the window.");
                Rect bounds = new(topLeft!.Value, item.Bounds.Size);
                Assert.True(
                    bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= window.Bounds.Width && bounds.Bottom <= window.Bounds.Height,
                    $"{item.Name ?? item.Header} at {bounds} runs outside the {window.Bounds.Width}×{window.Bounds.Height} window.");
            }
        });

        app.SaveFrame($"menu-{frame}");
        foreach (string _ in path)
        {
            app.Press(Key.Escape);
        }
    }
}
