# The Parts view: every distinct piece, drawn once, with its count

Status: **DRAFT, awaiting Marc's sign-off.** Written by Fable for issue #110; it unblocks #111
(now split into the slices in §8). Nothing here is implemented. It belongs to the **Views**
milestone and sits beside the standard-views note (#106, being written concurrently — this note
depends only on the idea both share: one canvas, one view at a time, switched by the mechanism
#108 lands; it never cites that note's text).

Settled decisions taken as given and not re-argued (Marc, 2026-09-23, "use the recommended
default", and the brief of 2026-09-24): the Parts view is **the same data as the cut list** — one
shared parts-sheet model computed from `CutList.Of`'s rows, so a count can never disagree between
the two; the default presentation is **2D**, a flat dimensioned outline per distinct piece, which
is the shop-drawing convention; the **3D option is isometric and orthographic** — it is for
reading size, never perspective; the layout is a **wrapped grid of cells with a count badge**,
sorted as the cut list is (largest first) and groupable by stock; **selection links both ways**
with the model views; it is a **view in the main canvas** next to the standard views, not a
window; print and export are out of scope (PDF sheets are #25). Also taken as given: exact
lengths on the 1/1024″ grid ([`geometry-model.md`](./geometry-model.md) §1); a cut-list row is a
value with no formatting decisions in it ([`parts-and-cut-list.md`](./parts-and-cut-list.md)
§3.1); a shaped part's row is its blank, with the cuts carried by value
([`shaped-parts-model.md`](./shaped-parts-model.md) §4).

§1 is the shared model, §2 the 2D drawing, §3 the 3D option, §4 the layout, §5 selection, §6
keyboard and accessibility, §7 the tests, §8 the slices, §9 risks, §10 the decisions that are
Marc's.

---

## 0. What exists today, and what this note does with it

Read for this note (all in `src/`):

- `Napkin.Modules.Furniture/CutList.cs`, `CutListRow.cs`: `CutList.Of(sketch, library)` is pure
  and total-ordered; a `CutListRow` carries `Label`, `Quantity` (= Σ `Part.Quantity` over its
  members), `Length`, `Width`, `Thickness`, `Material`, `Unresolved`, `Stock`, `Cuts` (by value,
  in the grouping key), `PlanAxes`, `Species`, `Members` (entity ids, ascending) and a derived
  `CutText`. The row has **everything a picture needs**; it never needs the sketch again.
- `Napkin.App/Viewing/CutListTable.cs` `Thumbnail(row)`: builds a blank
  `Box.AsDrawn(origin, Along(PlanAxes.X), Along(PlanAxes.Y), Along(OutOfPlane), Angle.Zero) with
  { Cuts }` and hands it to `CutThumbnail` — **only for rows with cuts**; a plain rectangle gets no
  thumbnail. `CutThumbnail` (44 × 28 px) fits each blank to its own cell, by design, because at a
  shared scale a 2½″ leg beside a 48″ top would be a dot.
- `Napkin.App/CutListWindow.axaml` now has two tabs, Cut list and Shopping list. The window holds
  a `Design`, not the `DesignEditor`; nothing in `Napkin.App` reads `CutListRow.Members` yet, so
  **clicking a row does not select its parts today** (the row was built for it; the wiring is
  absent).
- `Napkin.App/Viewing/ModelView.cs`, `ModelScene.cs`, `Camera.cs`: `Camera.Isometric(viewport)`
  exists (45° azimuth, atan(1/√2) elevation), `Projection` can be `Orthographic`, `FitTo(Bounds3)`
  fits; `ModelScene.PolygonsOf(EntityId, Solid)` is public static, `BackToFront(camera)` is an
  instance method and the only factory is `ModelScene.Of(Sketch)`; the three-tone face shading
  `Tone(palette, style, normal)` is a private static of `ModelView`.
- `Napkin.App/Viewing/CanvasPalette.cs`: `For(themeVariant)` gives `Background`, `Dimension`,
  `Label`, `Selection`, `Snap` (the problem colour) and `StyleFor(DesignLayers.Parts)` (fill,
  stroke, stroke width) in both themes.
- `Napkin.App/Settings/UserSettings.cs`: `DesignView { Plan, Model }`, `LastView`, `OpenIn`; a
  field a file does not mention takes its default, so adding fields needs no version bump.
- `Napkin.App/MainWindow.axaml(.cs)`: the canvas is a `Panel` holding `DrawingCanvas`
  (`CanvasView`) and `ModelDrawing` (`ModelView`), one visible at a time; `ShowModelView` /
  `ShowPlanView` flip `IsVisible` and `ShowViewChrome` writes `LastView`. `Editor.Selection` is an
  `IReadOnlySet<EntityId>` with `Select(ids)`, `ToggleSelection(id)`, `ClearSelection()` and a
  `SelectionChanged` event.

**The cut-list window question, answered.** Should the Parts view also be a third tab there, or
reuse `CutThumbnail`'s drawing? *Not a tab.* The window has no editor, so selection linking from a
tab would mean plumbing the main window's `DesignEditor` across to a second window — workable, but
it is the wrong direction for a view whose point is to select in the model; and a second place to
find the same pictures is a second thing to keep in step. The canvas decision stands. *Reuse, yes,
in the other direction:* the blank the table builds for its thumbnail becomes `PartsCell.Blank`
(§1.2) and `CutListTable.Thumbnail` reads it from the sheet, so the thumbnail and the Parts cell
are one construction; the drawing itself is not shared, because the thumbnail's fit-each-cell
scale is the right rule for a 44-pixel table cell and the wrong one for a sheet (§2.1).

---

## 1. The shared model: `PartsSheet`

```
PartsSheet.Of(rows)            -> ImmutableArray<PartsCell>       // rows: CutList.Of(sketch, library)
PartsSheet.GroupedByStock(cells) -> ImmutableArray<PartsGroup>
```

Both live in `Napkin.Modules.Furniture` beside `ShoppingList`, are pure, and take the cut list's
rows — never the sketch — so "the same data" is a type signature, not a promise. `Box` and
`Outline` come from `Napkin.Core.Geometry`, which the module already references.

### 1.1 A cell

| Field | Type | What it is |
|---|---|---|
| `Row` | `CutListRow` | The row this cell stands for. Row identity **is** cell identity; a cell adds nothing the row does not carry. |
| `Count` | `int` | `Row.Quantity`: Σ `Part.Quantity` over the members. The badge. |
| `Blank` | `Box` | The piece to draw, in its own frame: anchored at the origin, unrotated, `Width = Along(PlanAxes.X)`, `Height = Along(PlanAxes.Y)`, `Depth = Along(OutOfPlane)`, `Cuts = Row.Cuts`, a fresh id and layer. Exactly what `CutListTable.Thumbnail` builds today; that code moves here. |
| `Outline` | `Outline` | `Blank.Outline()`, memoised — what 2D draws. |
| `Solid` | `Solid` | `Blank.Solid()`, memoised — what 3D draws. |
| `Members` | `ImmutableArray<EntityId>` | `Row.Members`, for selection (§5). |
| `Caption` | derived | `Row.Label`; `Row.Length × Row.Width × Row.Thickness` through `LengthFormat.Default`; `Row.MaterialText`; `Row.CutText`. Derived, not stored, as `CutText` is. |

**Equality is the row's.** `Blank` carries a fresh `EntityId` and `LayerId`, and `Outline` and
`Solid` wrap `ImmutableArray`s that compare by reference, so `PartsCell` writes `Equals` and
`GetHashCode` over `Row` alone — the same reason `CutListRow` writes its own. The blank's
construction is a public static `PartsCell.BlankFor(CutListRow)`, so `CutListTable`, which holds
rows rather than a sheet, calls the one seam in one line.

**The row's first member is the piece drawn, by construction, not by lookup.** Every member of a
row has the same three dimensions, the same `PlanAxes` and the same cuts by value (they are the
grouping key), so the blank built from the row *is* the first member's blank in its own frame.
`Members[0]` is never fetched from the sketch to draw; it is used only as identity.

**Turned parts.** A box in any of the 24 orientations lists the same three dimensions (`CUT-003`,
#95: `Part.SizeOn` reads stored `Width`/`Height`/`Depth` by `PlanAxes`, never rotation), so its
cell is the same cell, drawn in the piece's own frame. The model's orientation never reaches the
sheet.

**Shaped parts.** The blank carries its cuts; `Blank.Outline()` is the shaped outline the canvas
draws, through the same `OutlineBuilder`, so a cell cannot show a shape the drawing does not.

### 1.2 Which plane a cell draws, and where the thickness goes

The brief says "length × width, face-on". The data says: a box's cuts exist only in the box's
**plan plane**, the plane `PlanAxes` names. For most furniture parts that plane is length × width
and the two agree. For a part drawn on edge — `PlanAxes = (Length, Thickness)`, a 1×4 apron seen
from the side with a miter on it — a face-on drawing would have no cut geometry to show.

**Rule:** a cell draws the row's plan plane, the one frame its cuts are defined in. It is turned by
an exact quarter turn when needed so that the **longer in-plane dimension lies horizontal** (a
swap of the two axes, never trigonometry). The third dimension is a caption, not a picture:
"¾″ thick" when the plane is length × width; "3½″ wide — shown on edge" when it is not, so a
reader is told the picture is an edge view rather than left to notice. No edge strip is drawn: a
strip ¾″ tall at sheet scale is a line, and the number carries the fact better. (Decision 2, §10.)

### 1.3 Groups

`GroupedByStock(cells)` partitions the cells by `(Row.Material, Row.Unresolved)`, preserving the
cut list's order inside each group. Groups are ordered by their **first cell's position in the
sheet**, so the group holding the largest piece comes first, then resolved stock only;
**unresolved names next** (one group each, titled with `CutListRow.UnresolvedText`), and
**"No stock"** last. A `PartsGroup` is `(string Title, ImmutableArray<PartsCell> Cells)`. Ungrouped
is simply the flat array; nothing is re-sorted by the view (§4.4).

### 1.4 Invariants (the tests in §7.1 are these, one each)

1. `PartsSheet.Of(rows).Length == rows.Length`, cell *i* is row *i*, in the row order.
2. Σ `Count` over the sheet = Σ `Part.Quantity` over every box with a part. **Not** the number of
   boxes: one box with `Quantity = 4` is one member and four pieces. (#111's wording "equals the
   number of parts in the design" is corrected to this.)
3. Grouping is the cut list's, unchanged, because there is no grouping code in the sheet — a
   1/1024″ edit that splits a row splits a cell for exactly the same reason.
4. `Blank`'s three stored dimensions read back as the row's, through `Part.SizeOn`-style access,
   never through corners.
5. `Of(rows)` is deterministic: the same rows give equal cells (record equality over `Row`).

---

## 2. The 2D drawing, stated exactly

### 2.1 The scale rule: one common scale, with a legibility floor

Two candidates. **Fit each cell** (what `CutThumbnail` does): every piece fills its cell, so
every piece is legible and no piece's size can be read against another's. **One common scale**:
the sheet is a drawing, a 48″ top is sixteen times a 3″ block on the page as on the bench, which
is what "drawn to scale" means and what the brief is for.

**Recommendation: one common scale per sheet, with a floor.**

```
drawable = (CellWidth − 2·Inset, DrawingHeight)            // 224 × 100 px at 100 % zoom
scale    = min over cells of min(drawable.W / longer(cell), drawable.H / shorter(cell))
                                                            // px per inch: the largest piece fills its cell
floorPx  = 48 px at 100 % zoom                              // the longer edge never draws shorter than this
cell scale = longer(cell) · scale < floorPx ? floorPx / longer(cell) : scale
```

`longer`/`shorter` are the two in-plane dimensions of the blank after the quarter turn of §1.2.
A cell drawn at its own floor scale is captioned **"not to scale"** in the dimension colour, the
same honesty the `≈` marker gives a length that is not exact at 1/16″: the picture says less than
the numbers, and says so. Both scales are multiplied by the sheet zoom (§4.3).

Checked against `samples/scale-extremes` (a 40′-0″ × 3½″ × 1½″ plate and a 6″ × 3½″ × 1/32″ shim,
two rows): scale = 224 / 480 = 0.467 px/in; the plate draws 224 × 1.6 px — a line with a
dimension under it, which is what a forty-foot 2×4 is at that scale; the shim's longer edge would
be 2.8 px, under the floor, so it draws at 48 / 6 = 8 px/in, 48 × 28 px, captioned "not to scale".
Against `coffee-table` (a 48″ × 24″ top, 40″ and 16″ aprons, 16¼″ × 2½″ × 2½″ legs): here the
**height** binds, 100 / 24 = 4.17 px/in; the top draws 200 × 100 px, a leg 68 × 10 px, legible
with no floor. A stroke is always at least the palette's stroke width, so a
piece drawn thinner than its stroke is still a visible line.

### 2.2 What a cell shows, at 100 % zoom

```
┌──────────────────────────────── 256 ────────────────────────────────┐
│  Leg                                                        [ ×4 ]  │  ← label, ordinal; count badge
│                                                                     │
│          ┌────────────────────────────────┐                         │
│          │        outline, Parts style    │ ─┐                      │  ← DrawingHeight 100
│          └────────────────────────────────┘  │ 2 1/2"               │  ← width dimension, right
│          ├─────────── 16" ─────────────────┤                        │  ← length dimension, below
│  2 1/2" thick · Oak, 8/4 · not to scale (when it applies)           │
│  Round the north-east corner, 1" radius  (+1 more)                  │  ← Row.CutText, 2 lines max
└─────────────────────────────────────────────────────────────────────┘  200 tall
```

- **Label**: `Row.Label`, `CanvasPalette.Label`, bold. Trimmed with an ellipsis; the full text is
  the tooltip and the automation name (§6).
- **Count badge**: `"×" + Count`, always shown (a `×1` is information: it says this is the only
  one), top-right, a rounded rectangle in the `Dimension` colour on the `Background` colour.
- **Outline**: `OutlineDrawing.GeometryOf(cell.Outline, toCell)`, north up as on the canvas, fill
  and stroke from `StyleFor(DesignLayers.Parts)`; centred in the drawing area.
- **Dimensions**: length below, width to the right, `LengthFormat.Default` (feet-inches at 1/16″
  with the `≈` marker), in the `Dimension` colour, drawn as the canvas draws dimension lines
  (`DimensionLayout` if its API fits a rectangle; otherwise a local routine with the same look).
- **Third dimension and material**: one line, "¾″ thick · Pine" or "3½″ wide — shown on edge ·
  Pine"; an unresolved material reads `MaterialText` in the `Snap` (problem) colour, as the table
  shows it; "not to scale" is appended when §2.1 says so.
- **Cuts**: `Row.CutText`, first two sentences, then "(+*n* more)"; the full list in the tooltip.
- **Selected**: a 2 px border in the `Selection` colour; **partly selected** (§5.2): the same
  border dashed; **focused** (§6): a 1 px `Dimension` ring inside the border.

Every colour is a `CanvasPalette` value, so both themes are covered by `For(ActualThemeVariant)`
and nothing is hard-coded; the palette's own comment records that the Parts layer colours are
napkin's until the shared design system grows a drawing section.

### 2.3 Pure parts and drawing parts

`PartsCellDrawing` (`Napkin.App/Viewing`) is a static builder: given a cell, a scale and a cell
rectangle it returns the outline geometry, the dimension line endpoints and the text runs — no
`DrawingContext`. `PartsView` paints what it returns. The builder is what §7.3's extents tests
call; `CutThumbnail` keeps its own tiny renderer.

---

## 3. The 3D option

One isometric, orthographic drawing of `cell.Solid` per cell, in the same drawing area. It answers
"which way do the cuts go through the thickness" in a way a flat outline cannot; it does not
replace the dimensions, which stay in the caption exactly as in 2D (no dimension lines in 3D —
they would need the assembly model's 3D dimension work, which is not this issue).

**Reuse `ModelScene` and `Camera`, not `CutThumbnail`.** `CutThumbnail` is a 2D outline renderer
with nothing to offer here. The 3D view's machinery is almost enough:

- `ModelScene.PolygonsOf(id, cell.Solid)` gives the faces with normals (public static today).
- `ModelScene.BackToFront(camera)` needs an instance; add **`ModelScene.Of(IEnumerable<ScenePolygon>)`**,
  a one-line factory beside `Of(Sketch)`, so a cell does not build a throw-away sketch.
- `Camera.Isometric(cellDrawable) with { Projection = Orthographic }`, then `FitTo(bounds, …)`
  where `bounds` is `Bounds3.Empty.Including(...)` folded over the solid's segment endpoints (a
  small `Bounds3.Of(Solid)` overload).
- Face shading: the three tones by axis, `ModelView.Tone(palette, style, normal)` — make it
  `internal static` (or move it onto `CanvasPalette`), one edit, so a cell and the model shade a
  face the same colour.

**Scale in 3D** is the same common scale as 2D: the camera's `PixelsPerInch` is set to §2.1's
`scale` (or the cell's floor scale) rather than fitting each cell, so relative sizes stay true;
the isometric foreshortening is the same for every cell and cancels out of the comparison. A cell
at its floor scale carries "not to scale" as in 2D.

**The toggle** is a checkable View-menu item, "Parts in 3D", and the key **`I`** while the Parts
view is focused (isometric; `O` is taken by the 3D view's projection toggle and the two should
not be confused). It **persists** in `UserSettings.PartsDrawing`, an enum `PartsDrawing { Flat,
Isometric }` defaulting to `Flat` (§8, slice F). Cost: slice E is the one slice that touches
`ModelScene`, `ModelView` and `Bounds3`; each edit is an added overload or a visibility change,
nothing existing changes behaviour.

---

## 4. Layout

### 4.1 The algorithm, pure

```
PartsSheetLayout.Arrange(cells | groups, viewportWidth, zoom) -> Placement[]
  Placement = (PartsCell cell, Rect bounds)  |  (string groupTitle, Rect bounds)
```

- `cellW = 256·zoom`, `cellH = 200·zoom`, `gutter = 12·zoom`, `margin = 16·zoom`.
- `columns = max(1, ⌊(viewportWidth − 2·margin + gutter) / (cellW + gutter)⌋)`.
- Cells fill rows left to right, top to bottom, in sheet order; a group starts a new row with a
  title band (`24·zoom` tall) above it.
- The sheet's height is the last placement's bottom plus `margin`; the view scrolls vertically
  only. **Never horizontally**: the column count is what a width buys.
- Uniform cells (not content-sized) so that Up/Down means "one row" and a wrapped grid stays a
  grid; the caption is clipped at its line budget (§2.2) rather than growing the cell.

**Determinism:** same cells, width and zoom give equal placements (a test, §7.2). Cell bounds are
snapped to whole device pixels.

### 4.2 Scrolling

Wheel scrolls vertically; PageUp/PageDown by a viewport; Home/End to the top/bottom when no cell
is focused (§6). The focused cell is scrolled into view when focus moves. The view keeps the
scroll offset while the design is edited, clamped to the new height.

### 4.3 Zoom

Ctrl+wheel and the existing Zoom In / Zoom Out / Zoom to Fit menu items and keys, routed to the
Parts view when it is the one showing (as they are to `ModelDrawing` when it is). Zoom scales
`cellW`, `cellH`, gutters and the drawing scale together, between 50 % and 300 %; "Zoom to Fit"
means 100 %, top of the sheet. The zoom readout in the status bar shows the sheet's percentage.

### 4.4 Sort and group

**Sort: the cut list's order, only.** "Sorted as the cut list is" is taken literally: the sheet
draws the rows in the order `CutList.Of` returns them, and offers no re-sort of its own; the
window's sortable table is where a person re-orders a list. **Group by stock** is a checkable
View-menu item, "Group parts by stock", persisted in `UserSettings.GroupPartsByStock` (default
off). Whether the sheet should also offer a sort is decision 4, §10.

### 4.5 Empty states

The two messages the cut-list window shows — "Nothing in this design is a part yet…" when the
design has boxes and none is a part, and "This design has nothing in it to cut." when it has no
boxes — move to one shared helper (`CutListEmptyText.For(sketch)` in `Napkin.App`) that both the
window and the view call, so the wording cannot diverge. With no design open the view says "No
design is open." — the window's headline text, the same way. An empty sheet still takes focus and
still answers the keyboard (§6) with nothing to move to.

### 4.6 When the design changes

`Editor.DesignChanged` → recompute `CutList.Of(sketch, MaterialsLibrary.Shipped)` →
`PartsSheet.Of` → re-arrange. The cut list is cheap (one pass over the boxes); no caching beyond
the memoised `Outline`/`Solid` on a cell, which are rebuilt with the cell. The focused cell is
re-found by row equality after a change (the cell for the same row keeps focus; a row that
vanished drops focus to none).

---

## 5. Selection, both ways

There is one selection: `DesignEditor.Selection`. The Parts view neither stores nor mirrors one.

### 5.1 Cell → model

Clicking a cell (or Enter/Space on the focused cell) calls `editor.Select(cell.Members)` — every
member, because the cell stands for all of them. Ctrl+click / Ctrl+Enter toggles each member's
membership (`ToggleSelection` per id); Shift+click adds the members. Clicking empty sheet calls
`ClearSelection()`, as clicking empty canvas does. Escape clears.

**"Highlight in the model" is nothing new.** `CanvasView` and `ModelView` already draw
`editor.Selection` — the plan's selection outline and handles, the 3D view's selection fill, edge
pen and handles in the `Selection` colour. The person switches back to Plan or 3D and the pieces
are selected. No new drawing, no `Attention` set (that is for problems and hover, and it would
paint a selected leg in the problem colour).

### 5.2 Model → cell

On `editor.SelectionChanged`, a cell is **selected** when every one of its `Members` is in the
selection, **partly selected** when at least one is and not all, and plain otherwise. Both states
draw as §2.2 says. Partial is a real state, not a rounding: a design with four separately drawn
legs and one selected shows the `×4` cell dashed, which tells the person what they will get if
they press Delete.

### 5.3 A box that stands for several pieces

`Part.Quantity` is the number of copies one box stands for. A cell whose one member has
`Quantity = 4` reads `×4` and selects one box; a cell with four members of quantity 1 reads `×4`
and selects four. The badge counts pieces; the selection counts boxes; both are right, and the
tooltip says which: "4 pieces from 1 box" / "4 pieces from 4 boxes" when they differ.

### 5.4 Deleting, and undo

Delete on a selected cell runs the ordinary `SelectionCommand.Delete` through `MainWindow`'s
`RunSelectionCommand` — the same path as the 3D view's `SelectionCommandRequested` — so undo,
redo and the cut-list invariants (#96) hold for free. After a delete the sheet recomputes (§4.6):
delete one of four legs and the cell reads `×3`; undo and it reads `×4`. That is the GUI-PARTS-03
workflow.

### 5.5 Follow-up outside this note

`CutListRow.Members` was built so that clicking a row in the cut-list window selects its parts,
and nothing wires it. Slice D's cell→model code is exactly that wiring; a one-line follow-up issue
reuses it for the window's table (the window needs a reference to the editor, or an event the
main window listens to).

---

## 6. Keyboard and accessibility

- The Parts view is a focusable control, focused when shown (as the canvas is). It has one
  automation peer, in the pattern of `CanvasAutomationPeer`, whose name is "Parts view: *n*
  pieces in *m* cells" and whose children are the cells.
- Each cell's **automation name** is its caption in one line: "Leg, ×4, 16″ × 2½″ × 2½″, Oak"
  (with "shown on edge" and "not to scale" appended when they apply). A test-facing
  `CellsOnScreen` property returns those strings in sheet order, the way `CutListTable.LinesOnScreen`
  does, so a workflow reads counts without reading pixels.
- **Focus cursor**: one cell at a time, drawn as §2.2 says. **Left/Right** move by one cell in
  sheet order (wrapping across rows); **Up/Down** move by one grid row (by the column count;
  across a group boundary they land on the nearest cell in the same column, or the row's last);
  **Home/End** to the first/last cell; **Enter/Space** select the focused cell's members
  (Ctrl toggles, Shift adds); **Escape** clears the selection, then, pressed again, clears focus;
  **Delete** runs the delete command; **I** toggles 2D/3D; **Tab** leaves the view.
- Pointer: click, Ctrl+click and Shift+click as §5.1; hover shows the tooltip; wheel scrolls,
  Ctrl+wheel zooms; nothing drags.
- Contrast is the palette's: the count badge and dimension text use `Dimension` on `Background`,
  which are the same pairs the rulers and dimension labels already use in both themes.

---

## 7. Test plan

Catalog ids follow the existing shapes: `CUT-NNN` (area furniture) for the model, `GUI-PARTS-NN`
(area ui) for workflows; the slice that lands a test adds its catalog row. The scorecard measures,
it never gates (CLAUDE.md).

### 7.1 The model, against every sample (`Napkin.Modules.Furniture.Tests`, `PartsSheetTests.cs`)

- `CUT-007` **The sheet is the cut list.** For every `samples/*.scene.json`: `PartsSheet.Of(rows)`
  has one cell per row, in row order, with `cell.Row == rows[i]`; `cell.Count == rows[i].Quantity`.
- `CUT-008` **Counts sum to pieces.** For every sample: Σ `Count` = Σ `Part.Quantity` over boxes
  with a part. Also on `coffee-table` by name: the four legs are one cell `×4`; each apron length
  is one cell `×2` (the same facts the fixture's expected cut list states).
- **Same grouping rule, shared code.** Take `coffee-table`, duplicate a leg: the leg cell's count
  rises by one and the cell count does not; move one leg's length by 1/1024″: the sheet gains a
  cell and the cut list gains a row, and the sheet is again one cell per row. (Extends
  `CutListPlacementTests`' edits; asserts through both `CutList.Of` and `PartsSheet.Of`.)
- **Blank in its own frame.** For a turned part (`CutListOrientationTests`' fixtures): the cell's
  `Blank` has the row's `Length`/`Width`/`Thickness` along the axes `PlanAxes` names, `Angle.Zero`
  rotation, anchored at the origin, in every one of the 24 orientations.
- **Shaped.** `rounded-corner-table`: the cell's `Outline` has the row's arcs; its blank's stored
  size is the row's (a taper never shrinks it, shaped-parts §4.1).
- **Groups.** `stocked-bench`: groups ordered by first appearance; a part with an unresolved stock
  name lands in its own titled group after the resolved ones; a part with no stock in "No stock",
  last; every cell appears in exactly one group.
- Determinism: `Of(rows)` twice gives sequence-equal cells.

### 7.2 Layout (`Napkin.App.GuiTests/Unit`, pure — no window)

- Column count at widths 300, 560, 1100 px at 100 % and 200 % zoom; one column below one cell's
  width; never zero.
- Placements do not overlap, are in sheet order, and grow downward only; group title bands start
  a row.
- Same input twice gives equal placements; the height is the last bottom plus the margin.

### 7.3 Drawing (`Napkin.App.GuiTests/Unit`, on the pure builder)

- Common scale: for `scale-extremes` the plate's outline extents equal the drawable width (±1 px);
  the shim is at the floor and flagged; for `coffee-table` the top's drawn height equals the
  drawable height (the binding side), a leg's drawn length is the top's drawn length × (16¼ / 48)
  (±1 px), unflagged.
- An on-edge part's cell has the "shown on edge" caption and a horizontal longer edge; a
  length × width part has "thick".
- The dimension strings equal `LengthFormat.Default` of the row's values; the count badge text is
  `×` + quantity; the cut lines are the first two of `CutText` with "(+*n* more)" when there are
  more.
- 3D (slice E): the projected extents of every cell's polygons lie inside the drawable; two cells
  of the same solid at the same scale have equal extents; a cell at the floor scale is flagged.

### 7.4 GUI workflows (`Napkin.App.GuiTests/Workflows/PartsViewWorkflows.cs`, `GuiWorkflow.Run`)

Each ≥ 5 actions, both keyboard and pointer, an assertion after a state change; they raise the
passing-workflow count, which is the gate that rises.

- `GUI-PARTS-01` **Open the view and read it.** `OpenSample("Coffee table")` (pointer, Samples
  menu); View → Parts (pointer); `CellsOnScreen` reads "Leg, ×4, …" and two apron cells "×2";
  Right, Right, Down (keyboard) and the focused cell's name changes; press `I` and the view
  reports 3D; press `I` again, 2D.
- `GUI-PARTS-02` **Select from a cell, see it in the model.** Open a sample; open the view;
  click the leg cell (pointer); `Editor.Selection` equals the four leg ids; press `V`/View → 3D
  (whichever #108 lands; until then the menu); the model view reports the four selected; back to
  Parts (menu); the cell is selected. Then, in Plan, click one leg (pointer); back to Parts: the
  cell is partly selected.
- `GUI-PARTS-03` **Delete a leg, watch the count, undo.** Open a sample; open the view; Right to
  the leg cell; Enter; Delete (keyboard); `CellsOnScreen` shows "Leg, ×3"; Ctrl+Z; "Leg, ×4"; the
  cut list window, opened afterwards (pointer), agrees.
- `GUI-PARTS-04` **Group and remember.** `stocked-bench`; open the view; View → Group parts by
  stock (pointer); the first group title is the stock of the largest piece and "No stock" (if
  present) is last; View → Parts in 3D; close and reopen the window (`SettingsWorkflows` pattern);
  the view opens grouped and in 3D, and `LastView` is `Parts`.

---

## 8. Implementation slices

Each lands alone on `main` per CLAUDE.md (build, test with coverage, `ratchet check`, `scorecard
stubs` when a catalog row is added, version bump by the lander). Default model **Sonnet**; Opus
only where said. Files are disjoint between concurrently running slices except `MainWindow.axaml(.cs)`
and `features/catalog.json`, which C, D, E and F all touch — run those in sequence.

| | Slice | Files | Tests | Depends on |
|---|---|---|---|---|
| **A** | `PartsSheet` model, blank construction moved from the table | `src/Napkin.Modules.Furniture/PartsSheet.cs`, `PartsCell.cs`, `PartsGroup.cs`; `src/Napkin.App/Viewing/CutListTable.cs` (`Thumbnail` reads `cell.Blank`); `tests/Napkin.Modules.Furniture.Tests/PartsSheetTests.cs`; catalog rows `CUT-007`, `CUT-008` | §7.1 | — |
| **B** | 2D cell drawing: pure builder, scale rule, captions | `src/Napkin.App/Viewing/PartsCellDrawing.cs`, `PartsScale.cs`; `tests/Napkin.App.GuiTests/Unit/PartsCellDrawingTests.cs` | §7.3 (2D) | A |
| **C** | Layout and the view in the canvas: `PartsView`, `DesignView.Parts`, `ShowPartsView`, empty states, scroll, zoom, keyboard focus, automation peer, `CellsOnScreen` | `src/Napkin.App/Viewing/PartsSheetLayout.cs`, `PartsView.cs`, `PartsViewAutomationPeer.cs`, `CutListEmptyText.cs`; `src/Napkin.App/Settings/UserSettings.cs` (`DesignView.Parts`); `src/Napkin.App/MainWindow.axaml(.cs)`; `src/Napkin.App/CutListWindow.axaml.cs` (uses the shared empty text); `tests/Napkin.App.GuiTests/Unit/PartsSheetLayoutTests.cs`, `Workflows/PartsViewWorkflows.cs`; catalog `GUI-PARTS-01` | §7.2, GUI-PARTS-01 | A, B. **Opus** if `MainWindow`'s view-chrome wiring turns out to fight #108's switcher; otherwise Sonnet |
| **D** | Selection both ways, delete and undo through the view | `PartsView.cs`, `MainWindow.axaml.cs` (route `SelectionCommandRequested`); `Workflows/PartsViewWorkflows.cs`; catalog `GUI-PARTS-02`, `GUI-PARTS-03` | GUI-PARTS-02, -03 | C |
| **E** | The 3D option: `ModelScene.Of(polygons)`, `Bounds3.Of(Solid)`, `Tone` made internal, isometric cell builder, the `I` key and menu item | `src/Napkin.App/Viewing/ModelScene.cs`, `Bounds3.cs`, `ModelView.cs` (visibility only), `PartsCellDrawing.cs`, `PartsView.cs`, `MainWindow.axaml(.cs)`; `Unit/PartsCellDrawingTests.cs` (3D extents) | §7.3 (3D) | C |
| **F** | Persistence: `PartsDrawing`, `GroupPartsByStock`, `LastView = Parts`, `OpenDesignsIn.LastUsed` may open in Parts; the group-by-stock menu item | `UserSettings.cs`, `MainWindow.axaml(.cs)`, `PartsView.cs`; `Workflows/PartsViewWorkflows.cs`; catalog `GUI-PARTS-04` | GUI-PARTS-04 | E (for the 3D half; the group half needs only C) |

A is a runnable, testable slice with no GUI in it and lands first (CLAUDE.md, "core functionality
first"). B is pure and lands second. C is the first thing a person sees. D, E and F are each a
day's work for Sonnet with the note in hand.

**Versioning:** each slice bumps the minor. **Ratchet:** A raises the furniture module's floor and
must not touch another area's; C, D and F raise the workflow count. `Napkin.App` is excluded from
line coverage, so B's and E's unit tests raise nothing and are still required.

---

## 9. Risks

1. **The view switcher (#108) lands before or after C.** C adds a plain `PartsViewMenuItem`
   beside `ModelViewMenuItem` and a `ShowPartsView` in the shape of `ShowModelView`; if #108's
   switcher is already in, C registers Parts with it instead. Either way `DesignView.Parts` is the
   contract, and the view keeps "one canvas, one view at a time" by flipping `IsVisible` like the
   other two. The collision is one file, `MainWindow`, and is why C–F run in sequence.
2. **Common scale disappoints on a real sheet.** A sheet of one door and forty small blocks draws
   forty cells at the floor, every one "not to scale". The floor keeps them legible; the mark keeps
   them honest; and if it is wrong in practice, "fit each piece" is one more value of a setting
   (decision 1, §10), not a redesign, because `PartsScale` is a pure function of the cells.
3. **Dimension-line reuse.** `DimensionLayout` was written for the canvas; if its inputs do not
   fit a rectangle in a cell, B writes a small local routine rather than bending it — a duplicated
   tick and arrow is cheaper than a shared abstraction with two masters.
4. **Painter's order for a single shaped solid.** `BackToFront` already handles one box's faces
   in the model; a lone solid is the easy case. A rounded corner is strips of chords
   (`DegreesPerChord`), fine at cell size.
5. **A very large design.** Hundreds of rows draw hundreds of cells; the sheet recomputes on every
   edit. `CutList.Of` is one pass and `Outline()` is memoised per cell; if a profile ever shows the
   re-arrange, virtualise the rows off-screen — not before.
6. **Focus and the plan's key handling.** `MainWindow` routes keys by which view is showing; the
   Parts view's `I`, arrows and Enter must not reach the canvas's handlers. C mirrors how
   `ModelDrawing.HandleViewKey` is routed only when the model is showing.

---

## 10. Decisions for Marc

Only real scope choices; everything else above is the recommendation implemented as written.

1. **Common scale with a legibility floor** (§2.1), rather than fitting every piece to its cell as
   the table's thumbnails do. Recommended: common scale — a sheet is a drawing, and "to scale" is
   its point; the floor and the "not to scale" mark keep small pieces readable and honest. The
   alternative can be added as a setting later without redrawing anything.
2. **On-edge parts are drawn in their plan plane, captioned "shown on edge"** (§1.2), rather than
   always face-on. Recommended as written: the plan plane is the only frame the cuts exist in, so
   face-on would show a miter-less rectangle for a mitered edge-on apron. Thickness is a caption,
   never an edge strip.
3. **Not a third tab in the cut-list window** (§0). Recommended: no tab; the main-canvas decision
   stands, and the window's thumbnails read the shared cell's blank so the two cannot disagree.
4. **No sort of the sheet's own** (§4.4): the cut list's order only, plus group-by-stock. If a sort
   (by count, by label) is wanted, it is a menu with three items and a setting; not recommended
   until someone asks.
5. **The `I` key for 2D/3D in the Parts view** and no key for group-by-stock (§3, §4.4). #108
   owns the view-switch keys; these two are the Parts view's own and can move if #108 wants them.
