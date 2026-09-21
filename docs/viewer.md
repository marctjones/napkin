# The viewer

napkin's first window (issue #36, milestone **M1 Look**): it draws a design in plan view and lets
you move around it. Nothing in it edits anything — there is no tool, no handle and no save. That is
the whole of M1 on purpose: the drawing and the navigation have to feel right before anything is
allowed to change the model.

![The coffee-table sample](screenshots/m1-coffee-table.png)

## Running it

```sh
dotnet run --project src/Napkin.App
```

It opens on the coffee-table sample, framed to fit. There is nothing to install and nothing to
configure; the samples ship inside the build.

## What you are looking at

- **Parts** are drawn as outlined, lightly filled rectangles, named where the name fits. A wall is
  a thicker grey rectangle; an opening cut into one is drawn dashed, in the colour of the paper,
  because it is a hole rather than a part.
- **Dimensions** are real dimension graphics — extension lines, a dimension line with arrowheads,
  and the value centred on it — not floating text. The value is computed from the geometry the
  dimension measures, every time it is drawn, and formatted as feet, inches and sixteenths by
  `Core.Geometry`'s `Format`. A value that cannot be shown exactly at that precision is marked with
  a leading `≈`, so a number on screen is never quietly rounded
  (`docs/design/geometry-model.md` §1.4).
- **Y is up**, as it is on a drawing and in DXF and PDF. The flip to the screen's downward Y lives
  in the view transform and nowhere else in the drawing code.
- **Line weights and text sizes are in pixels**, not inches, so the drawing reads the same zoomed
  out to the whole wall or in to a single joint.
- The **grid** steps through plain numbers — a quarter inch, an inch, three, six, a foot, two feet,
  and so on up — choosing the finest step that still leaves its lines far enough apart to see.

## Controls

| Gesture | What it does |
|---|---|
| **Wheel** | Zoom about the pointer. The model point under the cursor stays under it. |
| **Shift + wheel** | Pan. A trackpad reports both axes, so it pans in both. |
| **Drag**, left or middle button | Pan. The drawing follows the hand. |
| **Arrow keys** | Pan by a tenth of the window; hold **Shift** for half. |
| **+** / **-** | Zoom in and out about the centre of the window. |
| **Ctrl/Cmd + 0** | Zoom to fit: frame everything, dimension lines included, with a margin. |
| **Ctrl/Cmd + 1**, **Ctrl/Cmd + 2** | Open the first or second sample. |

Left-drag pans because M1 has nothing to select. When editing lands (#10) the left button becomes
the selection gesture and panning keeps the middle button; the wheel and the keyboard do not
change.

Zoom is limited at both ends — from an inch drawn at a fiftieth of a pixel, which fits a
1,500-foot site in a window, to an inch drawn across 2,400 pixels, which is finer than the
1/1024″ grid anything is stored on.

## The status line

Three things, left to right: which sample is open and what it is, where the pointer is in the
model as feet, inches and sixteenths, and the zoom as a percentage of life size (100% is a model
inch drawn at 96 pixels, Avalonia's device-independent inch). The pointer readout is marked `≈`
whenever the pixel it is over does not land exactly on a sixteenth — which is most of the time, and
is the point of the marker.

## The samples

![The wall-with-window sample](screenshots/m1-wall-with-window.png)

Two drawings, in the **Samples** menu:

- **Coffee table** — a 4′-0″ × 1′-8″ top, four 2½″ legs inset 1″ from the edges, and four ¾″
  aprons set back from the legs' outer faces.
- **Wall with window** — 12′-0″ of 2×4 wall, 3½″ thick, with a 3′-0″ opening 4′-2½″ from the end,
  framed by jack and king studs.

They are built in code, through the `Core.Geometry` API, behind `IDesignSource`
(`src/Napkin.App/Designs/`). Every number in them is a real shop or framing number and lands
exactly on the 1/1024″ grid; nothing in either sample rounds.

**Opening a file** is the menu item that is deliberately disabled. The scene reader is #6 and the
sample files are #37, both in flight beside this viewer; when they land, a file source implements
the same `IDesignSource` interface, `File → Open…` is enabled, and nothing in the canvas changes.
A source that refuses a file already has its path through the window: the message names what was
wrong and whatever is on screen is left untouched.

## How it is tested

- The transform arithmetic — world/screen round trips, zoom about a cursor, fit-to-extents, the
  zoom limits — and the dimension labels are unit tests in
  `tests/Napkin.App.GuiTests/Unit/`.
- The gestures are GUI workflows in `tests/Napkin.App.GuiTests/Workflows/ViewerWorkflows.cs`,
  which drive the real window with simulated keyboard and mouse input on the headless platform.
  See [gui-automation.md](testing/gui-automation.md).
- The screenshots on this page are frames those workflows rendered.
