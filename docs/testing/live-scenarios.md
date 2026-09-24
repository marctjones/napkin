# Live GUI scenarios: the same test, watched in a real window (#151)

The GUI workflow suite ([gui-automation.md](gui-automation.md)) drives napkin on Avalonia.Headless:
real input, real assertions, nothing to see. A **shared scenario** is a workflow body written once
against `IGuiDriver` that also runs **live**: `tools/Napkin.Demo` opens a real napkin window and
plays it with simulated mouse and keyboard input, paced so a person can follow it, checking the same
`Expect`s.

## Run it

```sh
dotnet run --project tools/Napkin.Demo -- list                      # ids and titles
dotnet run --project tools/Napkin.Demo -- run GUI-ASSEM-16          # play one, close when done
dotnet run --project tools/Napkin.Demo -- run GUI-ASSEM-16 --speed 2 --hold --keep-going
```

`--speed N` multiplies the pointer speed (about 750 px/s at 1) and divides every pause; `--hold`
leaves the window open afterwards (close it to end); `--keep-going` carries on past a failed
`Expect` (the default stops at the first).
- Exit code: 0 every `Expect` passed, 1 a failure (an `Expect`, or the scenario threw), 2 the host
  could not run it (bad arguments, unknown id, the window closed early, Avalonia refused).

What you see: a drawn pointer gliding on eased paths (injected input does not move the OS cursor),
a ripple where a button goes down, drags in per-frame steps, typing one character at a time, and a
bench-coloured caption bar with the scenario's `Say(...)` text, the last key, and each `Expect`
(a tick, or the failure in rust). A throw-away settings file gives the app's defaults (napkin
sheet, carpenter's pencil); your own settings are never touched.

## Write a scenario once for both hosts

```csharp
[GuiWorkflow("GUI-ASSEM-16")]                        // headless: CI, workflow rule, ratchet
public void The_3D_view_opens_in_perspective() =>
    GuiWorkflow.Run(PerspectiveSwitchesFromTheMenu);

[GuiScenario("GUI-ASSEM-16", "The 3D view opens in perspective, switches from the menu")]
public static void PerspectiveSwitchesFromTheMenu(IGuiDriver app)   // live: tools/Napkin.Demo
{
    MainWindow window = (MainWindow)app.Target;
    app.Say("Open the coffee table sample");
    OpenSample(app, window, "Coffee table");
    app.Press(Key.V);
    app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
    app.Click(CentreOf(window, window.FindControl<MenuItem>("OrthographicMenuItem")!));
    app.Expect("it is orthographic now", () => Assert.False(window.Model.Camera.IsPerspective));
}
```

A scenario is a `static void (IGuiDriver)` in a `Workflows/` class with its `[GuiWorkflow]`'s id
(a self-test insists). The body runs on the UI thread in both hosts. Assert state, not pixels:
`FrameSampling` is headless-only and live `SaveFrame` is a no-op. Shared helpers take `IGuiDriver`;
`AppDriver` implements it, so old callers still compile.

## How the live host works

- Real platform, napkin's own `App`, set up without a desktop lifetime (which would open a window
  on your settings). `Chord` uses the platform's command key: Cmd on macOS live, Ctrl headless.
- Input is fed where the OS backend feeds it: `ITopLevelImpl.Input` with `RawPointerEventArgs`,
  `RawKeyEventArgs`, `RawTextInputEventArgs` and `RawMouseWheelEventArgs`, against the window's
  input root, exactly as Avalonia.Headless does. Hit-testing, capture, focus, menus and key
  bindings run for real. The pointer is its own `MouseDevice`.
- Those members are `[PrivateApi]`: public at run time, stripped from Avalonia 12's reference
  assemblies. `tools/Napkin.Demo/RawInput.cs` binds them by reflection and fails loudly if an
  Avalonia upgrade renames one.
- Popups open in the window's overlay layer (`OverlayPopups = true`, live host only), as they do
  headless, so menu items sit at the same window coordinates in both hosts and the pointer is drawn
  above an open menu. The shipped app keeps native popups.
- Pacing never sleeps: each verb pumps the dispatcher in a nested `DispatcherFrame` while it
  animates, so the window keeps rendering and responding.

## Limits

- Verified on macOS only. Windows and Linux use the same code path but have not been watched.
- Your real mouse over the window is a second pointer and can disturb hover and menus; keep it away.
- Pointer paths are straight lines: walking nested submenus may need a `MoveTo` via the parent.

## The ratchet

Unchanged: only the headless `[GuiWorkflow]` counts, so sharing an existing workflow leaves the
count alone and a new shared scenario counts once. CI builds `Napkin.Demo` and never runs it.
