using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// The default look (napkin sheet, carpenter's pencil) with the bench chrome, in both themes, and
/// the clean screen look still there as the regression (docs/design/napkin-look.md).
/// </summary>
public class NapkinLookWorkflows
{
    [GuiWorkflow("GUI-SET-06")]
    public void The_default_napkin_look_keeps_the_bench_chrome_and_the_sheet_the_same_in_light_and_dark() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Color menuLight = MenuBarAt(app, window);
        Color statusLight = StatusBarAt(app, window);

        app.Expect("a fresh window wears the napkin and carpenter's pencil, on the bench chrome", () =>
        {
            Assert.Equal(new SketchLook(SketchPaper.Napkin, SketchLine.Carpenter), window.Canvas.Look);
            Assert.Equal(SketchColours.Napkin, PaperAt(app, window, window.Canvas));
            Assert.Equal(ChromeColours.Bench, menuLight);
            Assert.Equal(ChromeColours.Bench, statusLight);
        });

        app.SaveFrame("plan-light");
        app.Press(Key.V);
        app.SaveFrame("3d-light");
        app.Press(Key.V);
        SaveCutLists(app, window, "light");

        PickTheme(app, window, "ThemeDarkMenuItem");

        app.Expect("dark: the bench does not change, and the napkin stays on the desk", () =>
        {
            Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.Equal(menuLight, MenuBarAt(app, window));
            Assert.Equal(statusLight, StatusBarAt(app, window));
            Assert.Equal(SketchColours.Napkin, PaperAt(app, window, window.Canvas));
        });

        app.SaveFrame("plan-dark");
        app.Press(Key.V);
        app.SaveFrame("3d-dark");
        app.Press(Key.V);
        SaveCutLists(app, window, "dark");

        PickTheme(app, window, "ThemeLightMenuItem");
        Pick(app, window, "PaperScreenMenuItem");
        Pick(app, window, "LineCleanMenuItem");

        app.Expect("screen and clean still bring the theme-following ground back, under the same bench", () =>
        {
            Assert.Equal(CanvasPalette.Light.Background, PaperAt(app, window, window.Canvas));
            Assert.Equal(menuLight, MenuBarAt(app, window));
        });

        app.SaveFrame("plan-screen-clean");
    }, defaultLook: true);

    static void SaveCutLists(AppDriver app, MainWindow window, string theme)
    {
        app.Chord(Key.L);
        AppDriver list = AppDriver.Attach(window.CutList!, "cut-list-" + theme);
        list.SaveFrame("cut-list-" + theme);
        app.Chord(Key.L, KeyModifiers.Shift);
        AppDriver.Attach(window.CutList!, "shopping-list-" + theme).SaveFrame("shopping-list-" + theme);
        window.CutList!.Close();
    }

    static void PickTheme(AppDriver app, MainWindow window, string itemName)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }

    static void Pick(AppDriver app, MainWindow window, string itemName)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("SketchMenuItem")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }

    static Color Dominant(AppDriver app, int x, int y) =>
        FrameSampling.Patch(app, x, y, 8, 4).GroupBy(color => color).OrderByDescending(group => group.Count()).First().Key;

    /// <summary>The menu bar, right of its last item, where only the bar's own colour is.</summary>
    static Color MenuBarAt(AppDriver app, MainWindow window)
    {
        Control bar = window.FindControl<Menu>("MainMenu")!;
        return Dominant(app, (int)window.Bounds.Width - 40, (int)bar.Bounds.Height / 2);
    }

    /// <summary>The status bar, at its far right edge.</summary>
    static Color StatusBarAt(AppDriver app, MainWindow window)
    {
        Control bar = window.FindControl<Border>("StatusBar")!;
        Point top = bar.TranslatePoint(new Point(0, 0), window)!.Value;
        return Dominant(app, (int)window.Bounds.Width - 12, (int)(top.Y + bar.Bounds.Height) - 6);
    }

    static Color PaperAt(AppDriver app, MainWindow window, Control canvas)
    {
        Point corner = canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        int x = (int)corner.X + (int)canvas.Bounds.Width - 260;
        int y = (int)corner.Y + (int)canvas.Bounds.Height - 60;
        return FrameSampling.Patch(app, x, y, 24, 24)
            .GroupBy(color => color)
            .OrderByDescending(group => group.Count())
            .First().Key;
    }
}
