using Avalonia;
using Avalonia.Headless;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// The Avalonia application under test. This is the real <see cref="Napkin.App.App"/> — the same
/// <c>Application</c> subclass, styles and theme the shipped executable uses — hosted on the
/// headless platform instead of the OS windowing system.
/// </summary>
/// <remarks>
/// Headless drawing is turned <em>off</em> and Skia turned on, so the visual tree is really
/// rasterised and <see cref="AppDriver.SaveFrame"/> can write a PNG. Keep this builder as close to
/// <c>Napkin.App.Program.BuildAvaloniaApp</c> as the headless platform allows: the point of the
/// suite is to exercise what ships, not a stand-in.
/// </remarks>
public static class GuiTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Napkin.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
