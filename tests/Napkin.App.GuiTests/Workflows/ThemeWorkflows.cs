using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.App.Viewing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>The View > Theme menu changes the whole window at once, and the drawing's paper with it (#112).</summary>
public class ThemeWorkflows
{
    [GuiWorkflow("GUI-SET-02")]
    public void Choosing_a_theme_from_the_View_menu_recolours_the_plan_and_the_3D_view() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        Control plan = window.Canvas;

        OpenSample(app, window, "Coffee table");

        app.Expect("the window follows the system by default, and the menu says so", () =>
        {
            Assert.Equal(ThemeChoice.FollowSystem, window.Settings.Current.Theme);
            Assert.NotNull(window.FindControl<MenuItem>("ThemeSystemMenuItem")!.Icon);
            Assert.Null(window.FindControl<MenuItem>("ThemeDarkMenuItem")!.Icon);
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
        });

        PickTheme(app, window, "ThemeDarkMenuItem");

        app.Expect("dark: the window and the plan's paper are dark, and it is kept", () =>
        {
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.Equal(CanvasPalette.Dark.Background, PaperAt(app, window, plan));
            Assert.Equal(ThemeChoice.Dark, window.Settings.Current.Theme);
            Assert.NotNull(window.FindControl<MenuItem>("ThemeDarkMenuItem")!.Icon);
            Assert.Null(window.FindControl<MenuItem>("ThemeSystemMenuItem")!.Icon);
        });

        app.SaveFrame("plan-dark");
        app.Press(Key.V);

        app.Expect("the 3D view is dark too", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.Equal(CanvasPalette.Dark.Background, PaperAt(app, window, window.Model));
        });

        app.SaveFrame("3d-dark");
        app.Press(Key.V);
        PickTheme(app, window, "ThemeLightMenuItem", "theme-menu-in-dark");

        app.Expect("light: the same plan is on light paper again", () =>
        {
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(CanvasPalette.Light.Background, PaperAt(app, window, plan));
        });

        app.SaveFrame("plan-light");
        PickTheme(app, window, "ThemeDarkMenuItem", "theme-menu-in-light");
        PickTheme(app, window, "ThemeSystemMenuItem");

        app.Expect("following the system, the theme is the system's (light here) and the choice is remembered as such", () =>
        {
            Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(CanvasPalette.Light.Background, PaperAt(app, window, plan));
            Assert.Equal(ThemeChoice.FollowSystem, window.Settings.Current.Theme);
        });
    });

    static void PickTheme(AppDriver app, MainWindow window, string itemName, string? frame = null)
    {
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ThemeMenuItem")!));
        if (frame is not null)
        {
            app.SaveFrame(frame);
        }

        app.Click(CentreOf(window, window.FindControl<MenuItem>(itemName)!));
    }

    /// <summary>The colour most of a patch of the drawing's ground is: the paper, with a grid line or two ignored.</summary>
    static Color PaperAt(AppDriver app, MainWindow window, Control canvas)
    {
        Bitmap frame = app.CaptureFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        Point corner = canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        int x = (int)corner.X + (int)canvas.Bounds.Width - 260;
        int y = (int)corner.Y + (int)canvas.Bounds.Height - 60;

        const int size = 24;
        byte[] pixels = new byte[size * size * 4];
        GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(new PixelRect(x, y, size, size), pin.AddrOfPinnedObject(), pixels.Length, size * 4);
        }
        finally
        {
            pin.Free();
        }

        (byte c0, byte g, byte c2, byte a) = Enumerable.Range(0, size * size)
            .Select(i => (pixels[(i * 4) + 0], pixels[(i * 4) + 1], pixels[(i * 4) + 2], pixels[(i * 4) + 3]))
            .GroupBy(p => p)
            .OrderByDescending(group => group.Count())
            .First().Key;

        // The frame is four bytes a pixel, in whichever order this platform's bitmaps keep them.
        bool bgra = frame.Format == Avalonia.Platform.PixelFormat.Bgra8888;
        return bgra ? Color.FromArgb(a, c2, g, c0) : Color.FromArgb(a, c0, g, c2);
    }
}
