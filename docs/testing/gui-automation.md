# GUI automation: driving the real UI with real input

`tests/Napkin.App.GuiTests` drives napkin's actual Avalonia application with simulated keyboard and
mouse input and checks what the application does in response. It is a separate suite from the unit
tests, with its own rules and its own ratchet, because it answers a different question: not "does
this function return the right number" but "does a person, doing a sequence of things with a mouse
and a keyboard, get the right result".

Everything here runs headless on both CI runners (Windows and macOS). No windows appear.

## How it works

The suite hosts the real `Napkin.App.App` — the same `Application` subclass, styles and theme the
shipped executable uses — on `Avalonia.Headless`, with headless drawing turned *off* and Skia
turned on. That last part matters: the visual tree is really rasterised, so a workflow can save the
frame it produced as a PNG.

Input goes through the headless platform's input simulation, which feeds the same raw-input
pipeline the Windows and macOS backends feed. Hit-testing, pointer capture, focus, routed events
and keyboard shortcuts therefore all run for real.

**Nothing in this suite pokes a view model to stand in for a gesture.** If a test wants a button
pressed, it moves the pointer onto the button and presses the mouse. If a verb cannot be expressed
as input — resizing a window, for instance, which has no headless chrome to drag — it is recorded
as a `Window` action and does not count towards the workflow rule.

## Writing a workflow

A workflow is a test method marked `[GuiWorkflow("<feature id>")]` whose body is a single call to
`GuiWorkflow.Run`. `Run` starts the application on the headless dispatcher, opens its main window
at 900x600, and hands the scenario an `AppDriver`.

```csharp
[GuiWorkflow("GUI-SHELL-02")]
public void Resizing_the_shell_re_lays_out_and_re_renders_its_content() => GuiWorkflow.Run(app =>
{
    var window = (Window)app.Target;
    var text = window.GetVisualDescendants().OfType<TextBlock>().Single();

    app.Click(new Point(200, 150));          // pointer input
    app.Type("before the resize");           // text input

    app.ResizeWindow(640, 400);              // environment, not input
    app.Expect("content fills the smaller window and the frame follows it", () =>
    {
        Assert.Equal(640, window.ClientSize.Width);
        Assert.Equal(640, text.Bounds.Width);
        Assert.Equal(640, app.CaptureFrame()!.PixelSize.Width);
    });
    app.SaveFrame("640x400");

    app.MoveTo(new Point(320, 200));         // pointer input
    app.Press(Key.Tab);                      // keyboard input

    app.ResizeWindow(1024, 720);
    app.Expect("content fills the larger window and the frame follows it", /* … */);

    app.Wheel(new Point(500, 360), new Vector(0, 1));   // wheel input
    app.Expect("the content survived both resizes and the input in between", /* … */);
});
```

The feature id comes from the attribute, not from an argument, so a scenario cannot claim a feature
it was not declared for. The id is also published as an xunit trait (`Feature`), so a run can be
filtered to one feature.

### The driver's verbs

| Verb | What it simulates | Counts as |
|---|---|---|
| `MoveTo(point)` | A hover | pointer |
| `Click(point, button, modifiers)` | Move onto the point, press, release | pointer |
| `RightClick(point)` | The context-menu gesture | pointer |
| `DoubleClick(point)` | Two presses with no move between them | pointer |
| `Drag(p0, p1, …)` | Press at `p0`, move through each later point with the left button held, release at the last | pointer |
| `Wheel(point, delta, modifiers)` | A wheel turn at a point | pointer |
| `Press(key, modifiers)` | Key down and key up | keyboard |
| `Chord(key, extra)` | The same, with this platform's command modifier held | keyboard |
| `Tab(n)` / `ShiftTab(n)` | Focus traversal forward and backward | keyboard |
| `Type(text)` | A text-input event — what a keyboard layout or IME produces | keyboard |
| `ResizeWindow(w, h)` | A windowing-API call, not a gesture | not input |
| `WaitForIdle()` | Letting layout and rendering settle | not input |
| `Expect(what, assertion)` | An assertion about observable state | not input |
| `SaveFrame(step)` | Writes a PNG to `artifacts/gui-frames/` | not input |

Two details worth knowing:

- **`Click` moves the pointer before pressing**, because a real mouse is somewhere before it is
  pressed, and a control that only reacts to hover would otherwise never see it.
- **`Chord` reads the command modifier from Avalonia's platform settings** rather than hardcoding
  Ctrl or Cmd, so `app.Chord(Key.Z)` means "the undo shortcut on this machine". Note that under the
  *headless* platform this resolves to Control on macOS as well as on Windows, because there is no
  macOS windowing backend to say otherwise. Real Cmd-key behaviour is something only the real-OS
  smoke layer described below can cover.

Each verb lets layout and the render pass settle before returning, so the next verb sees the state
the previous one produced — exactly as the next gesture from a hand would.

### Asserting what the application received

`app.Probe` records the input events that actually reached the top level, in order, with positions,
click counts, modifiers, wheel deltas and whether the left button was held. That is how a workflow
checks that a drag arrived *as a drag* rather than as three unrelated moves. `app.Probe.Clear()`
starts a fresh phase so each part of a long scenario can be asserted on its own.

The probe never handles an event, so the application sees input exactly as it would without it.

### Frames

`app.SaveFrame("after-input")` renders the visual tree and writes
`artifacts/gui-frames/<feature-id>-<nn>-<step>.png`. Frames are CI artifacts for a human to look at.
**Nothing compares them pixel by pixel**, and nothing should for now: font rasterisation and theme
resolution differ between the Windows and macOS runners, so a pixel comparison would either fail
constantly or be loosened until it proved nothing. Visual regression testing is a later decision,
and would need per-platform baselines.

## The workflow rule

The harness fails a workflow that is not a workflow. A scenario must:

1. perform **at least five simulated input actions**;
2. use **both keyboard and pointer** input, not only one of the two;
3. make **at least one `Expect(...)` assertion after input has already changed something**.

A test that clicks one button once and asserts a label cannot satisfy this, and the harness's own
tests prove it — `WorkflowRuleSelfTests.A_one_click_scenario_is_rejected` runs exactly that
scenario and requires the rule to throw. There are matching tests for a scenario that only types,
one that never asserts, and one that asserts only before any input.

### Why

Because a GUI suite that clicks one control at a time is a slow, fragile way of testing what a unit
test already covers. The defects that only a GUI suite can find live in *sequences*: state that is
right after one action and wrong after the third, focus that lands somewhere unexpected after a
dialog closes, an undo that restores nine tenths of a change, a snap that is correct until the view
is panned. A rule of "five actions, both input devices, assert after a state change" does not
guarantee a good test, but it does make the cheapest bad test impossible, and it is enforced by the
harness rather than by review, so it cannot quietly rot.

The failure message lists everything the scenario did, in order, so an author can see immediately
why it was rejected:

```
GUI-TEMP-02 is not a workflow: performed 1 input action(s), but a workflow needs at least 5;
used no keyboard or text input.
  A workflow is a multi-step sequence a person could really perform.
  What it did:
     1. Pointer: left click at (100, 100)
     2. Expect: clicked
```

An assembly-level hook closes the one remaining hole: a `[GuiWorkflow]` test that never calls
`GuiWorkflow.Run` at all is failed too, so a workflow cannot claim a feature id without doing
anything.

## The ratchet

At the end of a run, `artifacts/gui-metrics.json` holds:

```json
{
  "workflowsPassed": 4,
  "actionsExecuted": 22,
  "workflowIds": [
    "GUI-SHELL-01",
    "GUI-SHELL-02",
    "GUI-SHELL-03",
    "GUI-SHELL-04"
  ]
}
```

Only workflows that ran to completion *and* satisfied the rule are recorded; the harness's own
self-tests claim no feature id and never appear. Ids are sorted and counted once each, and the file
is rewritten after every workflow, so the same set of tests always produces the same bytes. The
ratchet tool compares this against its committed baseline: the count and the set of ids may grow,
never shrink.

`features/gui-shell.json` is the catalog entry for the workflows implemented here. Each entry's
`acceptance` is one testable sentence, and the test that claims it carries the matching
`[Trait("Feature", …)]` through its `[GuiWorkflow]` attribute.

## What the shell workflows can honestly assert today

napkin's application is still the Avalonia scaffold: one window whose content is a line of text,
with no menus, no canvas and no focusable controls. The four `GUI-SHELL-*` workflows drive real
input into that real window and assert what is really observable — that it opens and renders at its
client size, that resizing re-lays-out its content, that every kind of input reaches it in the
right order, and that nothing in it takes focus.

That last one, `GUI-SHELL-03`, is deliberately a guard: it asserts that a full Tab traversal focuses
nothing, so it **fails the moment the shell gains its first focusable control**. That is not a
nuisance, it is the signal to replace it with a real traversal workflow. A suite that has to be
rewritten as the product grows is doing its job.

## Planned workflows

These are the scenarios the suite should grow into as each milestone lands. They are written as
sequences on purpose: each one is several minutes of a person's real use, not a feature checklist.
Their feature ids live in `features/catalog.json`, which the planner owns.

### M1 — viewing a design

- **Open a sample design and read it.** Open a project file from disk; wait for the drawing to
  appear; press the zoom-to-fit shortcut and expect the whole drawing inside the viewport; drag with
  the middle button to pan and expect the view origin to move by the drag delta, not by more;
  wheel-zoom at a point and expect that point to stay under the pointer; pan back with the arrow
  keys and expect the view to return; read a dimension label off the canvas and expect the feet,
  inches and fraction it shows to match the model.

### M2 — drawing and editing

- **Draw a rectangle by dragging.** Pick the rectangle tool by keyboard shortcut; drag from one
  corner to the other through intermediate points; expect a live dimension readout during the drag
  and a part of the dragged size when the button is released.
- **Type a dimension in feet-inch-fraction text.** Select the part; Tab to the width field; type
  `2' 6 1/2"`; press Enter; expect the geometry to change to exactly that length and the label to
  round-trip the same text.
- **Move a part with snapping.** Drag the part towards an edge of another; expect it to snap, and
  expect the snap indicator to appear; hold the snap-override modifier and expect it not to snap.
- **Undo and redo a chain.** Perform four edits; press the undo chord four times, expecting the
  state after each; redo three times and expect the state to match what was undone, step for step.
- **Save, reopen and compare.** Save with the shortcut; close the document; reopen it; expect the
  same parts, positions and dimensions.
- **Recover from invalid dimension text.** Type `2' 6 1/0"` into a dimension field; expect a
  visible error and no change to the geometry; correct it and expect the edit to apply.

### M3 — furniture and cut lists

- **Build the coffee table.** Draw the top, then the four legs, then the aprons, snapping each to
  the last; assign a material to each part; open the cut list and expect a row per part with the
  finished dimensions; open the shopping list and expect the board feet and sheet goods to account
  for every part.

### M4 — walls and openings

- **Place and resize a window on a wall.** Draw a wall; place a window opening in it by dragging;
  expect the header result to appear with its citation; drag the opening wider and expect the header
  size and the citation row to change with it.
- **Hit an out-of-scope case.** Widen the opening past the prescriptive tables and expect the
  application to say plainly that the tables do not cover it, rather than showing a number.

### M5 — code rules

- **Trip the bracing check.** Enlarge an opening in a braced wall line until the bracing check
  flags it; expect the failure to name the table and row it comes from.
- **Change the adopted code.** Switch the project's adopted code edition; expect the header and
  bracing results to recompute and their citations to point at the new edition.

## What headless cannot cover

This suite tests the application. It does not test the operating system's relationship with the
application, and it should not be read as if it did. Out of reach:

- **OS-level focus and window management** — activation, focus follows clicks between applications,
  minimise and restore, multiple monitors, DPI changes on the fly.
- **Input methods** — IME composition, dead keys, non-QWERTY layouts. `Type` delivers finished text;
  it does not exercise a composition session.
- **Native menus and system chrome** — the macOS menu bar, window title bars, system context menus,
  and the platform file dialogs that a save or open workflow really goes through.
- **Drag and drop from Finder or Explorer**, and the clipboard as other applications see it.
- **Real modifier behaviour per platform** — as noted above, the headless platform reports Control
  as the command modifier on macOS too, so Cmd-key shortcuts are not genuinely exercised here.
- **Anything about how it looks** — fonts render differently on each platform, so frames are
  artifacts to look at, not baselines to compare.

A small real-OS smoke layer is the right place for those, and is a later addition: a handful of
scenarios on the actual windowing backends — launch, open a file through the platform dialog, use
one Cmd/Ctrl shortcut, quit — run on the same two CI runners. It stays small deliberately. The
value of the headless suite is that it is fast and deterministic enough to run on every pull
request; the value of a smoke layer is that it catches the things the headless suite cannot see, and
those are few.
