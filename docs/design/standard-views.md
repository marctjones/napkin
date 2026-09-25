# The six standard 2D views

Status: **Signed off by Marc 2026-09-25 (by instruction); implementation began.** §10's three
recommendations are taken as decided: Top is the plan canvas; height dimensions are deferred; read-only
keeps the selection's menu commands live. Slice A (#107) deviates in two small places, both for
room: the seven views sit in a View ▸ Standard views submenu (the View menu no longer fits a small
window otherwise), and the chips sit in the status bar beside the view's name rather than in the
drawing's top-right corner (the side panels own that corner, and chips over the paper take its clicks).
Slice B (#128) enabled the other four; the views' pure frame (axes, names, readable coordinates) is
`StandardViewFrame` in `Napkin.Modules.Editing`, and per-view camera memory stays session state in
`ModelView` (never scene data, docs/file-format.md rule 6; not persisted across runs). Slice C (#129,
with #134's line table `DrawingLines`) put `HiddenEdges` in the Module too; H toggles it beside the
menu item; a hidden piece lying along a solid edge is dropped rather than overdrawn.

Written for issue #106 (the design) and owed to
#107 (the read-only implementation) and #108 (the view switcher), both in milestone Views, both
in umbrella #116. §1–§6 decide what each view is and how it is drawn, dimensioned, switched and
navigated, exactly enough to implement from; §7 is the test plan; §8 is the plan in slices, each
landable alone; §9 is what could go wrong; §10 lists the decisions that are Marc's, not an agent's
(CLAUDE.md, "Decisions that are Marc's").

Settled decisions taken as given and not re-argued (2026-09-23/24, accepted as recommended in
#116): the 2D views are **orthographic only** — perspective has no meaning in a flat elevation;
they are **read-only first**, editing follows (§6); there is **one drawing area that changes view**,
with a view switcher in the View menu, on the number keys and in a small widget, not separate
windows or tabs; hidden edges are **light dashes with a toggle in View**; rulers are 2D-only and
follow the grid ladder (`CanvasView.ShowRulers`, `RulerTicks`, `SnapGrid`); the theme follows the
OS with Light/Dark/System already built; settings persist through `src/Napkin.App/Settings`
(`UserSettings.OpenIn`, `UserSettings.LastView`). Sections and cutaways are out of scope, as #106
notes. The coordinate frame is [`assembly-model.md`](./assembly-model.md) §1: X east, Y north, Z
up, right-handed; a part is a `Box` with 24 orientations (§1.3) whose `Solid()` the 3D view already
draws (§8.4); [`shaped-parts-model.md`](./shaped-parts-model.md) gives cut parts their outline.

Two things exist today that this note builds on and does not redesign:

- **`Camera`** (`src/Napkin.App/Viewing/Camera.cs`): azimuth about Z (0 = the eye due south,
  looking north; 90 = due east, looking west), elevation above the horizon (90 = from above,
  −90 = from below), a centre point, `PixelsPerInch`, `Project`, `Ray`, `Pan`, `ZoomAt`, `FitTo`,
  and an orthographic/perspective switch. Its `Right` is `(cos az, sin az, 0)`, its `Up` is
  `(−sin az·sin el, cos az·sin el, cos el)`, and `TowardViewer` is
  `(sin az·cos el, −cos az·cos el, sin el)`. It already expresses all six views; the plan canvas is
  the case azimuth 0, elevation 90, which a test holds (assembly-model §7.1).
- **`ModelView`** (`Viewing/ModelView.cs`): the 3D view — `ModelScene` builds every box's solid
  as polygons, `BackToFront` culls the faces turned away and sorts the rest, `Tone` shades by the
  axis of each face's normal, `ModelPicker` picks along a ray, and the editing gestures (body
  drag in the plane of the face hit, per-axis arrows, face handles, placement on a face, X/Y/Z to
  turn, a typed length during a drag) are already per-face and per-axis.
- **`CanvasView`** (`Viewing/CanvasView.cs`): the plan — the editable one. It is welded to the plan:
  it draws `Box.Footprint()`, maps through the 2D `ViewTransform`, resizes by `Footprint().FaceAt(side)`,
  and lays dimensions out as `Point2`s (`DimensionLayout` throws on a Z axis).

---

## 1. The six views: names, orientation, handedness

### 1.1 Stated as cameras

A standard view is **a fixed orthographic `Camera` direction**, nothing more. Each is named for
the side of the model the eye is on, which is how a woodworker names a drawing — "the front view"
is what you see standing in front. Screen right and screen up follow from `Camera`'s formulas:

| View | Eye is | Azimuth | Elevation | Screen right | Screen up | Looks along |
|---|---|---|---|---|---|---|
| Top | above | 0 | 90 | +X (east) | +Y (north) | −Z |
| Bottom | below | 0 | −90 | +X (east) | −Y (south) | +Z |
| Front | south | 0 | 0 | +X (east) | +Z (up) | +Y |
| Back | north | 180 | 0 | −X (west) | +Z (up) | −Y |
| Left | west | 270 | 0 | −Y (south) | +Z (up) | +X |
| Right | east | 90 | 0 | +Y (north) | +Z (up) | −X |

Top is the plan: north up, east right, exactly what `CanvasView` draws today. The number in
the View menu and on the keys follows this table's order, 1–6, with 3D as 7 (§4).

### 1.2 Nothing mirrors

**Every one of the six is what a person standing there sees.** None is reflected. Back and Left are
left–right reversed relative to Front and Right *because they are* — walk round the table and the
leg that was on your left is on your right. Bottom is the table rolled over towards you about its
front edge: X stays to the right and south is now up, which is the drafting convention for a
bottom view. The one free choice in the table is Bottom's azimuth: 0 (chosen: X unchanged, south
up) rather than 180 (X mirrored, north up, as if the plan were seen through the floor). The chosen
one is the drafting convention and is what the camera gives for free.

First-angle versus third-angle projection is a question of where the views sit on one sheet; on
one canvas showing one view at a time it does not arise, and the six views are identical in both
conventions. That closes #106's question 1.

### 1.3 Pinned down with the l-bracket

`samples/l-bracket` has one feature per side, all `faceUp: top`, `rotation 0`, in inches:

| Part | x | y | z | Faces |
|---|---|---|---|---|
| Foot | ½–6½ | ½–4½ | ¼–¾ | the base plate |
| Upright | ½–1 | ½–4½ | ¾–6 | on the foot's west edge |
| Boss | 5½–6½ | ½–1½ | ¾–1¾ | top, at the foot's south-east corner |
| Skid | 3–4 | 2–3 | 0–¼ | bottom, under the foot |
| Tab | 6½–7½ | 2–3 | ¼–¾ | east, beyond the foot |
| Nub | 0–½ | 2–3 | 3–3¾ | west, on the upright |
| Rib | ½–1 | 4½–5 | 2–4 | north, on the upright |
| Lug | ½–1 | 0–½ | 4–5 | south, on the upright |

What each view shows, which §7.1 turns into assertions (extents in inches, "left" and "right" are
screen positions, "visible" means the feature's facing surface is drawn solid, "hidden" means
only its dashed outline shows behind another part, §2.3):

- **Top** — 7½ wide × 5 tall. Upright at the left as a thin strip; boss at the lower right (south-east);
  tab sticks out at the right, nub at the left, rib at the top edge, lug at the bottom edge; the
  skid is hidden under the foot. (This is the plan, §2.1, which draws footprints: the skid shows
  through the foot's translucent fill as today, not dashed.)
- **Bottom** — 7½ × 5, south up. The skid faces the viewer, visible; the lug is at the **top** of the
  screen and the rib at the bottom; nub left, tab right — the same left–right as Top; the boss is
  hidden, at the upper right.
- **Front** — 7½ wide × 6 tall. The upright is at the left, the lug faces the viewer high on it,
  visible; the nub sticks out at the far left; the boss stands on the foot at the right, the tab
  beyond it at the far right, the skid below the foot; the rib is hidden behind the upright
  (its dashed outline lies inside the upright's silhouette).
- **Back** — 7½ × 6, left–right reversed: the upright at the right, the rib on it visible, the nub
  beyond it at the far right, the boss and then the tab at the left; the lug is hidden.
- **Right** — 5 wide × 6 tall, south at the left. The tab faces the viewer at the foot's height,
  visible; the boss at the left in front of the upright, which fills y ½–4½ behind it; the lug
  sticks out at the far left and the rib at the far right, both visible; the nub is hidden behind
  the upright; the skid shows below.
- **Left** — 5 × 6, north at the left. The upright's west face is the whole silhouette, the nub on
  it faces the viewer, visible; rib at the left, lug at the right; the tab is hidden behind the foot
  and the boss behind the upright.

### 1.4 Pinned down with the coffee table

`samples/coffee-table`: a 48 × 24 top ¾ thick on four 2½-square legs 16¼ long inset 1½, aprons
3½ deep flush with the legs' outer faces. **Front** and **Back** are 48 wide × 17 tall and, the table
being symmetric, identical pictures: two legs, the long apron between them from z 12¾ to 16¼, the
top across everything. The far legs stand exactly behind the near ones, so their edges coincide
with solid ones and no dash shows for them; each short apron's end is inside a leg's silhouette
(its ¾ lies within the leg's 2½), so a small dashed rectangle appears inside each leg from z 12¾ to
16¼. **Left** and **Right** are 24 × 17 and identical to each other, and are not Front: the short
apron spans between the legs. In every elevation each leg is drawn exactly 16¼ tall — #107's
acceptance test, restated in §7.1.

---

## 2. Rendering

### 2.1 Which renderer: `ModelView` with a locked camera; Top stays the plan

**Decision: the five non-plan views are `ModelView` with its camera locked to the table in §1.1
and its editing gated off; Top is the existing plan canvas.** Not a new 2D drawing path, and not
the plan canvas generalised.

The reason is §6, not §2. When the elevations become editable, the gestures a person needs are the
ones `ModelView` already has: a body drag moves in the plane of the face hit — with a Front camera
that is precisely "X and Up change, Y is held", #106's own description of an editable Front; arrows
and face handles are per-axis; placement lands on the face under the pointer; X, Y, Z turn. Making
the elevations a gated mode of `ModelView` means editing later is *ungating*, not building.
`CanvasView`, by contrast, is welded to the plan at every level a drawing touches (`Footprint()`,
`ViewTransform`, `FaceAt(side)`, `Point2` dimensions); generalising it to a view frame is the
corner the brief warns against painting into. A third control would duplicate `ModelScene`,
picking, selection and the frame-capture harness for no gain.

Why Top is not also `ModelView` at elevation 90: Top is the view people edit in, and the plan canvas
is where editing exists. The two renderers draw the same geometry at the same projection
(assembly-model §7.1 holds `Camera.Plan` to `ViewTransform`), so nothing measured differs. The
one visible difference is occlusion: the plan draws every footprint in id order with a translucent
fill, so a part under another shows through rather than dashed (§1.3, Top). That is accepted for
now and is §10 decision 1. The direction after editing arrives — the plan becoming `ModelView` at
Top, so the six are one — is noted in §6 and not decided here.

Concretely, `ModelView` gains a `StandardView? Locked` state (§4.1 gives the type). When locked:

- the camera's azimuth, elevation and projection are fixed by the view; `Projection` is forced
  orthographic and the `O` key and the View ▸ Orthographic/Perspective items are inert (the
  settled decision);
- `Orbit`/`OrbitBy` are no-ops; a drag on empty space pans, as the plan's does; the arrow keys pan
  (as `CanvasView`'s do) instead of orbiting; `Home` fits;
- pan, wheel zoom, `+`/`−`, Cmd+0 fit, and `FitTo(bounds, viewport, coveredRight)` work unchanged;
- the editing gestures are gated (§5.4); selection and hover are not;
- the drawing style is §2.2, hidden edges §2.3, dimensions §3, rulers and grid §5.2.

### 2.2 Style: flat, the plan's look

In an axis-aligned orthographic view every face that survives the cull has the same normal — it
points straight at the viewer — so `Tone`'s three steps (light up-facing, medium X, dark Y) would
collapse to one tone anyway. A locked view therefore fills each polygon with **the layer style's
fill exactly as the plan draws it** (`EntityStyle.Fill` at its own alpha over the background, no
lift, no shade) and strokes edges with the style's stroke, so that all six views read as one family
and Front looks like the plan of a wall. Faces edge-on to the viewer (normal ⟂ view) are culled by
`FacesTowards`'s `> 1e-9` test, which is what makes an elevation an elevation; their outline is
still drawn, because it coincides with the edges of the faces they meet.

Painter's order is `BackToFront` unchanged. In an axis view every drawn polygon is parallel to the
screen with a single depth, so the sort is exact and cycles cannot occur; coplanar faces (a leg's
outer face flush with an apron's) tie on depth and fall to id order, and since both draw the same
fill and both outlines are real edges, either order is right.

**Cut and turned parts** come from `Solid()` through `ModelScene` unchanged. A turned part is
already in world coordinates. A cut face perpendicular to the view (a mitre seen in the plan) is
edge-on and culled, its line appearing as an edge of the cap; a cut face facing the view is a filled
polygon in the flat fill; a rounded corner is chords, as in 3D. Nothing here is view-specific.

### 2.3 Hidden edges: light dashes, one pure class

**Decision: hidden edges are drawn as light dashes (the style's stroke at 0.35 alpha, dash `[3, 3]`,
one pixel), beneath the solid edges; View ▸ Hidden edges toggles them, default on, persisted as
`UserSettings.ShowHiddenEdges`.** Only in a locked standard view; the free 3D view keeps
assembly-model §8.4's cull-and-paint with no hidden pass.

The computation, in a pure class `HiddenEdges` beside `ModelScene`, valid only when every drawn
polygon is parallel to the screen (an axis view):

1. Take the polygons `BackToFront` returns — the front-facing ones, each with one depth
   `DepthOf(Points[0])`.
2. For each polygon P and each of its edges with `EdgeDrawn` true (rulings between the strips of a
   curved face get neither a solid nor a dashed line), project the edge to a screen segment.
3. Subtract from that segment the screen polygon of every Q **strictly nearer** than P — nearer by
   more than 1/2048″ in depth. Equal depth never hides: two flush faces are both fully drawn.
   Subtraction is general segment-against-polygon clipping — find every parameter where the
   segment crosses an edge of Q, sort, and test each sub-segment's midpoint for inside Q by
   point-in-polygon — because an outline with a notch is not convex.
4. What remains is visible and is drawn solid; what was subtracted is hidden and is drawn dashed.

Why this is complete: an extruded prism seen along an axis has no self-hidden edge — its back face
projects onto the outline of its front face — so hidden edges arise only from *other* parts, and
the front-facing list is every edge there is. A part entirely behind another (the l-bracket's rib
behind the upright in Front) becomes a dashed rectangle inside a solid one; a part whose hidden
edges coincide with a nearer part's solid edges (the tab's top and bottom behind the foot in
Left, the coffee table's far legs in Front) shows nothing extra, because solid is drawn over the
dash. Cost is O(edges × polygons) with a few hundred of each: nothing.

Selection, the attention outline and hover draw last and on top, as in 3D; a selected part that
is hidden shows its selection outline through what hides it, which is how `ModelView` already
behaves and is useful in an elevation.

---

## 3. Dimensions per view

### 3.1 The rule

A `Dimension` today measures along world X or Y — a `ParamMeasurand` (a box's width, height or
depth, drawn when that size lies along a plan axis in the box's orientation) or an `AxisMeasurand`
whose `Axis` is X or Y (assembly-model invariant 13). **A dimension shows in a view when the world
axis it measures is that view's screen-right or screen-up axis, ignoring sign.** So:

| View | Shows dimensions along | Coffee table's six |
|---|---|---|
| Top, Bottom | X and Y | all 6 |
| Front, Back | X | 4: Top width, Leg width, Long apron length, Leg inset from the top's west edge |
| Left, Right | Y | 2: Top depth, Short apron length |

Heights — a leg's length, an apron's face — have no measurand today; an elevation is exactly
where a person wants one. That is scope, §10 decision 2, and not built here.

### 3.2 Where it is drawn

A dimension is a world object the view projects, not a plan drawing re-used. `DimensionLayout`
gains `Measure(Sketch, StandardView)` returning measurements in **view coordinates** — `Along`
(screen right, in inches of the measured axis) and `Across` (screen up) — from which the existing
`Point2`-based `Measure(Sketch)` is the Top case. The rules:

- The two measured places are projected: their coordinate along the view's right axis gives the
  ends; `Place` carries X, Y and Z, and a box param's ends are the box's vertices.
- The dimension line lies `Placement.Offset` beyond **the measured entity's extent along screen
  up**, on the side `Placement.Side` maps to: South and West map to *below* (the low side), North
  and East to *above*. For an X dimension in Front that is below the box's minimum Z or above its
  maximum Z; in Top it is today's rule. Extension lines run from that extent to the line.
- In Bottom, up is south, so a dimension placed South appears above the part on screen. That is
  the projection being honest, not a bug; a test in §7.2 pins it.
- In Back and Left the measured axis runs right-to-left on screen; the label reads the same
  value and the arrowheads point outwards as always.
- A side parallel to the measurement falls back exactly as `DimensionLine` does today.

The label string is `DimensionMeasurement.Label` unchanged — one line of code for the person and
the test — with the ≈ marker rule intact.

---

## 4. The view switcher

### 4.1 One source of truth

```csharp
/// Which way the drawing is looked at. The number keys and menu follow this order.
public enum StandardView { Top = 1, Bottom, Front, Back, Left, Right }

public static class StandardViews
{
    /// The fixed direction of a view, with exact ±axis vectors (no trig at right angles).
    public static (Vector3d Right, Vector3d Up, Vector3d TowardViewer) Axes(StandardView view);
    public static Camera CameraFor(StandardView view, Camera keepingCentreAndScale);
    public static string Name(StandardView view);          // "Top", "Front", …
}
```

`UserSettings.DesignView` becomes `{ Top = 1, Bottom, Front, Back, Left, Right, Model }` — `Plan`
renamed to `Top`, five values added — and `UserSettings.CurrentVersion` goes to 2, so a version-1
`settings.json` is not read and the defaults apply (beta policy: no converter). `OpenDesignsIn`
stays three-valued — `Plan` (opens in Top), `Model`, `LastUsed` — and `LastUsed` restores any of
the seven. `ViewForNewDesign()` maps accordingly.

**The trig trap.** `cos 90°` and `sin 180°` are ~1e-16 in `double`, so a `Camera` built from the
azimuth table is not exactly axis-aligned. `FacesTowards`'s 1e-9 threshold culls correctly
regardless, but `Axes` returns exact `±1` vectors, `CameraFor` snaps the camera's derived vectors
at right-angle multiples, and every extents test in §7 compares with a tolerance of 1e-9 inch.

`MainWindow` keeps one `ShowView(DesignView)` that every entry point calls — menu item, key,
widget chip, `V`, the open-in setting — and that updates all of them: the menu ticks, the chip
pressed state, the status text, `LastView`, the chrome (§5.4). `ShowPlanView`/`ShowModelView`
become the Top and Model cases of it.

### 4.2 Menu, keys, widget, status

**View menu**, top group, in this order with these gestures shown:
`Top 1 · Bottom 2 · Front 3 · Back 4 · Left 5 · Right 6 · 3D 7`, then the existing
Orthographic/Perspective pair (enabled only in 3D), then `Hidden edges` (checkable, enabled only in
Bottom–Right), Rulers, Grid, Snap to grid, Open designs in, Theme, zoom items, panels.

**Keys: unmodified `1`–`7` and `NumPad1`–`NumPad7`.** Verified against everything handled today:
the plan and 3D views handle no unmodified digit (only Cmd+0 = fit), and the window binds only
Cmd+1…9 (the Samples menu) and the letter gestures. The one real clash is `ModelView.OnTextInput`,
which collects digits into a typed length while `_typeable` is live — during or just after an
arrow or face-handle drag. Rule: **a view digit yields to a pending typed length**; the view keys
are handled in the focused control's key path (`HandleEditKey` in `CanvasView`, the key switch
in `ModelView`) only when no typed length is pending, exactly as `R`, `S` and `V` are handled
there so that a `3` typed into a dimension field types a `3`. The menu `InputGesture` is display,
as it is for `V` today.

**`V`** keeps its meaning and gains one memory: from any 2D view it goes to 3D; from 3D it returns
to **the 2D view last shown** (Top until another was). `O` is inert outside 3D (§2.1). `Home`
fits in every view (in Top it is unbound today and gains fit for consistency).

**Widget:** a row of seven small toggle chips — `Top Bottom Front Back Left Right 3D` — in the
top-right corner of the drawing area, one pressed, each with an automation name `View: Front` and
a tooltip carrying the key. A labelled cube was considered and set aside: it needs its own
picking and drawing and shows the same seven choices less legibly at the size a corner allows.

**Status bar:** a new `ViewText` block between `DesignText` and `CursorText` reads the view name —
`Top`, `Front`, `3D`, `3D, perspective`. The hint `Editor.Say`s on entering a locked view is
"Front view: read-only for now — pan, zoom and select; 1 for the plan or 7 for 3D to edit."

---

## 5. Navigation and what read-only means

### 5.1 Zoom, pan, fit

Each standard view **remembers its own centre and scale for the session**; returning to Front finds
it where it was left. The first entry to a view fits the drawing (`FitTo` with the side panels'
`coveredRight`, as 3D does), as does `Home` and Cmd+0. Wheel zoom about the pointer, `+`/`−` about
the centre, drag on empty space / middle button / Shift-drag to pan, arrow keys to pan by the
plan's step. The zoom readout is the same `ZoomPercent`.

### 5.2 Rulers and grid

**Rulers carry world values along the two visible axes**, so a feature at x = 6½″ reads 6½″ on the
horizontal ruler in Top, Bottom, Front and Back alike, and on the vertical ruler in Left and Right
it reads its y; the vertical ruler in the elevations reads z. Consequences that are correct, not
bugs: Back's horizontal ruler counts **down** left to right (−X is right), Left's likewise (−Y is
right), and Bottom's vertical ruler counts down going up (−Y is up). `RulerTicks.Ticks(low, high,
ppi)` is range-only and unchanged; only the placement mirrors, through the camera's
`ProjectDirection`. The same `ShowRulers` setting governs all six; the 3D scale bar stays 3D-only.

**The grid** in a locked view is drawn in the view plane: lines at every minor step along screen
right and screen up, at world-axis values, on the same ladder as the plan (`SnapGrid.StepInches`
minor, `CoarserStepInches` major). In the elevations the line at z = 0 is drawn with the major
pen whatever the ladder — the floor, which is the cheapest depth cue there is (#105 asks for a
ground line). `ShowGrid` governs all six.

### 5.3 Cursor readout, picking, selection, hover

The cursor readout shows the two visible world coordinates of the point under the pointer on the
plane through the camera's centre — `x 12 1/4″  z 3″` in Front, `y …  z …` in Left/Right,
`x …  y …` in Top/Bottom — and never the third, which the view cannot know. Picking is
`ModelPicker` along the orthographic ray, unchanged; a click selects, Shift-click extends, as in
3D; the selection is the document's, shared with every view. **Selection highlight: yes**, the
edges in the selection colour as 3D draws them. **Hover: yes**, the hover outline and the Part
panel readouts follow the pointer as they do in 3D.

### 5.4 The tools in a read-only view

Entering a locked view does what entering 3D does with what the plan's tools hold, then puts it
down: `Disarm()`, `Tool = Select`, and the toolbar's Rectangle, Shape and Stock buttons are
disabled with a tooltip "Not in a Front view yet — 1 for the plan or 7 for 3D"; the turn buttons
are hidden as they are in the plan; the dimension editor is closed. No gesture in a locked view
changes the model: no body drag, no arrows, no face handles, no placement, no rectangle, no typed
length. Snapping therefore does not arise. **Menu commands that act on the selection — Delete,
Duplicate, Mirror, Pin, Turn about X/Y/Z, Undo/Redo — stay enabled**, because they are requests
to the editor that no view gesture is needed for, and taking them away would make the elevation a
dead end for a person who has just selected a part there. That reading of "read-only" is §10
decision 3.

---

## 6. Editing later: the generalisation, so read-only does not paint us in

Not built here; stated so that slices A–E leave the door open.

- **The gate, not a fork.** `ModelView.Locked` gates gestures; it must not remove them, replace
  the control, or grow a second drawing path. Ungating is the first editing slice: a body drag in
  Front already moves in the plane of the face hit (X and Z, Y held), arrows and face handles
  already are per-axis, placement already lands on the face under the pointer, the typed length
  already works. The camera stays locked; only `Orbit` stays off.
- **The rectangle tool in an elevation** is the one new thing: a drag on empty space in Front draws
  a rectangle in the view plane and needs a depth along the view axis (the held stock's thickness,
  or a default) and a Y to sit at (the plane through the camera centre, or a face snapped to). It
  is the same shape as `PlacementTool`'s plain board on a face, placed on a plane instead.
- **Snap** is `SpaceSnapResolver` with the dragged axes restricted to the view plane — the 3D
  snap already handles one or two axes at a time.
- **What in `CanvasView` assumes the plan** is listed in this note's opening (footprints,
  `ViewTransform`, `FaceAt(side)`, `Point2` dimensions, and `Up` treated as a field the drawing
  cannot show). None of it is touched by A–E, and none of it needs to be for editable elevations
  under §2.1. The eventual question — whether the plan itself becomes `ModelView` at Top so that
  Top gains dashes and one renderer draws all six, retiring the plan-specific paths — is for the
  editing design note, with the plan canvas's GUI suite as the regression net.
- **Dimensions along Z** (§10 decision 2) need a `ParamMeasurand` for a depth that stands vertical
  and an `AxisMeasurand` with `Axis.Z`, relaxing invariant 13, and a dimension tool that works in
  an elevation — the same design note.

---

## 7. Test plan

All in `tests/Napkin.App.GuiTests` (unit tests beside `CameraTests.cs`, workflows beside
`ViewerWorkflows.cs`), claiming catalog features with `[Trait("Feature", …)]`; new catalog entries
are `VIEW-<NNN>` (unit) and `GUI-VIEW-07` onwards (workflow — 06 is the rulers), regenerated with
`scorecard stubs`. Extents compare with a tolerance of 1e-9 inch (§4.1).

### 7.1 Unit: cameras and projections on the l-bracket and the coffee table

- **Axes.** For each of the six, `StandardViews.Axes` equals §1.1's table exactly, and
  `CameraFor(view)`'s `Right`, `Up`, `TowardViewer` equal them within 1e-12; `Right × Up ==
  TowardViewer` (right-handed) for all six.
- **Extents.** Projecting every vertex of `l-bracket.scene.json`: Top and Bottom span 7½ × 5, Front
  and Back 7½ × 6, Left and Right 5 × 6. Coffee table: Front and Back 48 × 17, Left and Right 24 × 17.
- **Where a feature sits.** In screen coordinates at 1 px/inch: Front — the lug's centre is left of
  the boss's, the nub is leftmost, the tab rightmost; Back — the mirror of each; Right — the boss
  is left of the upright's centre, the lug leftmost, the rib rightmost; Left — the rib leftmost,
  the lug rightmost; Bottom — the lug's centre is *above* the rib's, the nub left, the tab right;
  Top — the lug's centre is below the rib's.
- **Symmetry.** Coffee table: the sorted projected extents of every box in Front equal those in
  Back (mirrored in x), Left equal Right likewise, and Front's overall width is not Right's.
- **Leg height.** In Front, Back, Left and Right each leg's projected height is exactly
  16¼ × PixelsPerInch — #107's acceptance, and the projection test #93 restated per view.
- **Hidden edges.** `HiddenEdges` on the l-bracket: in Front the lug's facing edges are all
  visible and the rib's are all hidden; Back the reverse; Right — tab visible, nub hidden; Left —
  nub visible, tab hidden; Bottom — skid visible, boss hidden. Equal-depth never hides: two
  flush 1″ cubes side by side hide nothing of each other. Coffee table Front: each leg contains
  one hidden rectangle whose z-range is 12¾–16¼.

### 7.2 Unit: dimensions, rulers, settings

- `DimensionLayout.Measure(sketch, view)` on the coffee table returns 6, 6, 4, 4, 2, 2 measurements
  for Top, Bottom, Front, Back, Left, Right, naming the ones in §3.1's table; every label string
  equals the Top label for the same dimension.
- In Front, "Top width" (placed South, offset 4″) lies 4″ below the top's minimum z; in Bottom the
  same dimension lies on the screen-up side of the top (south is up).
- Ruler labels: Back's horizontal ruler labels strictly decrease left to right; Bottom's vertical
  labels strictly decrease upwards; Front's increase both ways.
- `UserSettings` version 2 round-trips every `DesignView`; a version-1 file is refused and the
  defaults apply (`SettingsStoreTests`).

### 7.3 Frames

With `CaptureFrame`/`FrameSampling`: the six views of the l-bracket produce six pairwise
different frames; in Front, a pixel inside the lug's rectangle is the layer fill and a pixel in
empty paper is the background; toggling Hidden edges changes at least one pixel on the rib's
outline in Front and none elsewhere outside it; Top rendered by the plan and the same drawing at
`Camera.Plan` agree on every box's screen rectangle (the §7.1 test of assembly-model, kept).

### 7.4 GUI workflows (≥ 5 actions, keyboard and pointer, an assertion after each state change)

- **GUI-VIEW-07 Switch through the six views and 3D.** Open the l-bracket; press `3`, `4`, `5`,
  `6`, `2`, `1`, `7` and after each assert `ViewText`, the pressed chip and the menu tick agree;
  click each chip with the pointer and assert the same; press `V` twice and land back where you
  were.
- **GUI-VIEW-08 An edit in the plan is seen in Front.** Open the coffee table, press `3`, read a
  leg's projected height (17 tall overall), press `1`, select a leg, type a new depth in the Part
  panel, press `3` and assert the new height with no refresh; Undo in Front and assert it reverts.
- **GUI-VIEW-09 Hidden edges toggle.** Front of the l-bracket; assert the rib's dashed outline is
  in the frame; View ▸ Hidden edges by menu, assert gone; toggle by keyboard through the menu
  mnemonic, assert back; quit and relaunch, assert the setting held.
- **GUI-VIEW-10 Rulers and grid in Back.** Press `4` with rulers on; assert the horizontal ruler's
  labels descend; pan by keyboard and by pointer drag and assert the labels shift; zoom and assert
  the ladder step changes as the plan's does.
- **GUI-SET-05 Last view is remembered.** Open designs in ▸ Last used; press `5`; quit; relaunch
  and open a design; assert Left; set Open designs in ▸ 2D plan; New; assert Top.

---

## 8. Implementation plan

Five slices, each landable alone on `main` behind the local gate, in this order because every one
touches `ModelView.cs` and `MainWindow.axaml(.cs)`: **sequential, not parallel.** Each slice adds
its catalog entries and runs `scorecard stubs`; the GUI count and the `Napkin.App.GuiTests`
floors only rise. Models per CLAUDE.md: Sonnet unless said otherwise.

**A. The mode, the switcher and two views — Opus** (the mode plumbing lives in a 3,100-line
`MainWindow` and a 2,000-line `ModelView`; #107 is already labelled `model/opus`). `StandardView`,
`StandardViews` (§4.1) in `Viewing/StandardView.cs`; `ModelView.Locked` with orbit off, pan on
arrows and empty drag, projection forced orthographic, `O` inert, gestures gated (§5.4), flat
style (§2.2); `MainWindow.ShowView` replacing `ShowPlanView`/`ShowModelView`, the View menu
entries, keys 1–7 yielding to a typed length, the chip widget, `ViewText`, `V`'s memory;
`DesignView` renamed and extended, `UserSettings.CurrentVersion = 2`, `LastView` persisted for
every view. **Only Top (= plan), Front and 3D are offered**; the other four menu items and chips
exist but are disabled. Tests: §7.1 axes and Front extents/positions on the l-bracket, §7.2
settings, GUI-VIEW-07 with the views A offers, GUI-SET-05. Touches: `StandardView.cs` (new),
`ModelView.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`, `Settings/UserSettings.cs`,
`SettingsStoreTests.cs`, `OpenInWorkflows.cs` (names `DesignView.Plan`), `features/catalog.json`.

**B. The remaining four views — Sonnet.** Enable Bottom, Back, Left, Right; per-view camera memory
(§5.1); the cursor readout per view (§5.3). Tests: the rest of §7.1 extents, positions and
symmetry, leg height in all four elevations, §7.3's six-different-frames, GUI-VIEW-07 completed.
Touches: `StandardView.cs`, `ModelView.cs`, `MainWindow.axaml.cs`, tests, catalog.

**C. Hidden edges — Sonnet.** `Viewing/HiddenEdges.cs` (new, pure, §2.3), the dashed pass under
the solid one, View ▸ Hidden edges, `UserSettings.ShowHiddenEdges`. Tests: §7.1 hidden-edge
cases, §7.3 toggle frame, GUI-VIEW-09. Touches: `HiddenEdges.cs`, `ModelView.cs`,
`MainWindow.axaml(.cs)`, `UserSettings.cs`, tests, catalog.

**D. Dimensions per view — Sonnet.** `DimensionLayout.Measure(Sketch, StandardView)` in view
coordinates with the Top case delegating to today's (§3.2); drawing in a locked `ModelView` with
the plan's `DrawDimension` look. Tests: §7.2 counts, names, labels and placement. Touches:
`DimensionLayout.cs`, `ModelView.cs`, `DimensionLabelTests.cs`, catalog.

**E. Rulers, grid, ground line — Sonnet.** Rulers with world values on the two visible axes,
mirrored placement (§5.2), the view-plane grid on the plan's ladder, the z = 0 major line,
`ShowRulers`/`ShowGrid` honoured. Tests: §7.2 ruler ordering, GUI-VIEW-10. Touches:
`ModelView.cs`, `RulerTicksTests.cs`, `RulerWorkflows.cs`, catalog.

Then the editing design note (§6), which is its own issue and its own sign-off.

---

## 9. Risks and unknowns

- **`MainWindow` is the collision point.** Every slice edits it; the GUI suite (`OpenInWorkflows`,
  `SettingsStoreTests`) names `DesignView.Plan` and the view menu items — grep before renaming.
  Sequential slices, not parallel ones, are the mitigation.
- **Trig at right angles** (§4.1): a camera that is 1e-16 off-axis passes the cull but fails an
  exact extents assertion; exact axes in `StandardViews` and tolerances in tests.
- **Coincident edges and dash order.** A hidden edge under a solid one must vanish, which depends
  on drawing dashes first; a test in §7.3 pins the coffee table's far legs.
- **Non-convex outlines** in the hidden-edge clip: the midpoint-inside test is correct for simple
  polygons; a self-intersecting outline is not a valid `Outline` today, so it is not handled.
- **Digits and typed lengths** (§4.2): the rule is stated; when editing arrives in elevations the
  same rule must hold or `3` during a drag will switch views mid-gesture.
- **The Top view differs in occlusion** from the other five (§2.1) until the plan becomes
  `ModelView`. A person comparing Top with Bottom sees translucency in one and dashes in the other.
- **Settings file version 2** discards a person's version-1 preferences once. Beta policy accepts
  it; the change is called out in the slice A commit message.

---

## 10. Decisions for Marc

1. **Top is the editable plan canvas, not a sixth read-only rendering.** Recommendation: yes —
   the plan is where editing exists, and the projection is identical; the cost is that Top shows a
   covered part as translucent while the others dash it (§2.1), until editing arrives and the
   renderers can become one.
2. **Height dimensions in elevations are a follow-up, not this milestone.** Recommendation: a
   separate issue after the read-only views land, alongside the editing note (§3.1, §6); it
   relaxes invariant 13 and needs a dimension tool that works in an elevation.
3. **"Read-only" means no view gesture changes the model; selection-based menu commands stay
   live** (§5.4). Recommendation: yes; if Marc means read-only literally, slice A disables the
   Draw menu's selection commands while a locked view shows and nothing else changes.
