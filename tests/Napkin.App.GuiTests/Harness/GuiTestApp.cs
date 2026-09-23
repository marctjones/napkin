using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Headless;

namespace Napkin.App.GuiTests.Harness;

/// <summary>
/// The Avalonia application under test. This is the real <see cref="Napkin.App.App"/> — the same
/// <c>Application</c> subclass, styles and theme the shipped executable uses — hosted on the
/// headless platform instead of the OS windowing system.
/// </summary>
/// <remarks>
/// <para>
/// Headless drawing is turned <em>off</em> and Skia turned on, so the visual tree is really
/// rasterised and <see cref="AppDriver.SaveFrame"/> can write a PNG. Keep this builder as close to
/// <c>Napkin.App.Program.BuildAvaloniaApp</c> as the headless platform allows: the point of the
/// suite is to exercise what ships, not a stand-in.
/// </para>
/// <para>
/// The one departure is the menu's hover delay, which is zero here. Avalonia's menu handler arms
/// 400 ms wall-clock timers as the pointer moves between items, and one of them, armed when the
/// pointer leaves an item for its own submenu, closes that submenu when it fires unless the
/// pointer is over the submenu <em>then</em>. It checks nothing else, so it can close the
/// submenu of a later visit to the same menu. A person is never back in a menu 400 ms after
/// leaving it; this suite, which runs one gesture after another as fast as the machine allows,
/// can be — on a slow or busy runner it was, and <c>Draw → Stock</c> closed under the second
/// visit's click. With no delay every such timer fires within the gesture that armed it, while
/// the pointer is where that gesture left it, as it does for a person, who is never quicker than
/// the delay. <see cref="AppDriver"/>'s verbs cannot otherwise wait on a timer: they pump the
/// dispatcher, they do not let time pass.
/// </para>
/// </remarks>
public static class GuiTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Napkin.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .AfterSetup(_ => DefaultMenuInteractionHandler.MenuShowDelay = TimeSpan.Zero);
}
