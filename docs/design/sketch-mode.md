# Rough sketching: get it down fast, firm it up later

Status: **Signed off by Marc 2026-09-25 with the recommended decisions (#159).** Written by Fable
for issue #159 (the umbrella; the slices are #170, #172, #174, #176, #178 and #180 — §8 — in
milestone **M7 Sketch mode**).

> **Format-version coordination (2026-09-25):** this note and `renovation-sketches.md` each asked
> for one scene-format bump. Sketch mode was built first and took **format 9** (slice A, #170);
> renovation (#160) takes **10**.

The idea, as proposed and asked for: napkin already *looks* like a napkin — the paper and the
carpenter's pencil of [`napkin-look.md`](./napkin-look.md) are the defaults. This note is the
counterpart on the *entry* side: a way of drawing that behaves like pencil on a napkin. Big round
numbers, parts that just touch, nothing stated, no stock chosen — and then, when the idea is worth
it, **firm it up**: type the exact sizes, let napkin propose the relationships the touching parts
imply and the stock their sizes are nearest, and accept each one or all of them. Rough first,
precise when it matters, and the person decides when.

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024″ grid and
relationships stored, never inferred ([`geometry-model.md`](./geometry-model.md) §1, §3.2); one
snap ladder for what is drawn and what is landed on (`SnapGrid`); a part is fields on a box and a
box with no `Part` is not on the cut list ([`parts-and-cut-list.md`](./parts-and-cut-list.md) §2.2,
§3 step 1); the stock tool's "a part already *is* that stock" (`StockTool`,
`StockAssignment.RequestsFor`); typing a size states it — `ParamValue`, or `SetParameter` on the
one that owns it (`DimensionEntry.RequestFor`); a joint is proposed and confirmed in a popover,
join-all with a tick per pair, one undo step ([`joinery-and-fasteners.md`](./joinery-and-fasteners.md)
§5.1, `MainWindow.Joinery.cs`); the beta policy (a format bump refuses old files, never converts).

§0 is what exists and what this note does with it, §1 the mode, §2 what drawing rough means,
§3 firming up, §4 what "rough" is in the model, §5 the cut list and shopping list, §6 the
interactions, §7 the tests, §8 the slices, §9 the risks, §10 the decisions that are Marc's.

---

## 0. What exists today, and what this note does with it

| Today | Where | This note |
|---|---|---|
| One snap ladder, ¼″ … 12 000″, the finest step whose lines are ≥ 14 px apart | `SnapGrid.Ladder`, `StepInches` | Adds a *rough* step derived from the same ladder (§2.1); the ladder itself is untouched |
| A drag lands on the grid, or on another part's edge or corner, and the drop **states** `Flush`/`Coincident` | `SnapResolver.Resolve` → `SnapPlan.Relationships` | Rough mode keeps the catching and discards the statements (§2.2) |
| A rectangle is a `Box` with `Part == null`, so it is not on the cut list until the Part panel gives it plan axes | `RectangleTool`, `Box.AsDrawn`, `CutList.Of` step 1 | The rough rectangle is a **plank**: a box with a `Part` and no stock, so it *is* on the cut list (§2.3) |
| A selected part shows its width and height as dimension labels; clicking one types it | `SelectionDimensions`, `DimensionLayout`, GUI-DRAW-02 | Rough mode hides the labels until the pointer is over the selected part; the size lives in the status bar (§2.4) |
| Join proposes; Join all touching lists every touching pair with a tick each; Enter makes them in one undo step | `JointProposals`, `PairDraft.Ticked`, `BeginJoin(all: true)` | Firm up is the same shape for relationships and stock (§3) |
| View ▸ Sketch chooses the **look**: paper and line | `SketchPaper`, `SketchLine`, `SketchInk` | Untouched. The word "sketch" stays the look's; the entry mode is called **Rough** so the two are never one menu word (§1.1, decision 1) |
| The Part panel makes a part, sets stock, species, quantity, depth | `MainWindow.axaml.cs` (`Assignment`) | Gains the rough tick-box (§4.2) |

Nothing in the geometry kernel changes. Rough is a way of *entering* a design; the design it
produces is ordinary exact geometry, which is why firming up is a sequence of the requests the
app already makes.

---

## 1. The mode

### 1.1 Two modes, one word each: Rough and Precise

The editor is in one of two entry modes. **Precise** is everything napkin does today. **Rough** is
this note. The mode is a property of the editor session (`DesignEditor.EntryMode`, an enum
`EntryMode { Precise, Rough }`), not of the design file — a design has no mode, a person does.

Where it is switched, all reaching one `SetEntryMode(EntryMode)` in `MainWindow`:

- **Draw ▸ Rough sketching**, a checkable menu item (it sits with the tools because it changes
  what the tools do, not what is shown; View ▸ Sketch is the look).
- A **toolbar toggle** at the left end of the tool row, two words, `Rough | Precise`, the active
  one in the moss wash the armed tool uses.
- The key **`Q`** (free today: the letters taken are C D G J L M N O P R S V W X Y Z, the digits
  1–6 and `Home` are the standard views', `I` is the Parts view's). `Q` is "quick"; §10 decision 2
  if Marc wants another.
- The **status bar** always names the mode: `ROUGH` or `PRECISE` in the mono bench text, first
  thing on the row (#156 is making the status bar one row; this is one word on it). A mode that is
  not visible is a mode error waiting to happen (§9.1).

Switching mode is **not an undo step**. It is like Snap to grid: it changes what the next gesture
does, never the design. Ctrl+Z after a switch undoes the last edit, in whatever mode it was made.

### 1.2 What the mode starts as

Precise, every launch. The mode is not in `UserSettings`. Recommended because the cost of the two
mistakes is unequal: a person who wanted Rough and got Precise notices at the first drag (the
readout shows 12 1/4″ instead of 12″) and presses `Q`; a person who wanted Precise and got Rough
because last Tuesday's session left it on draws a whole precise part at 3″ steps with nothing
stated and does not notice until the cut list. §10 decision 3 offers the alternative (remember it).

---

## 2. Drawing rough

Everything here applies to the plan canvas and the 3D view alike (one editor, one mode); the 3D
specifics are in §6.4.

### 2.1 Big round steps: the rough ladder

```csharp
public static class SnapGrid
{
    /// The rough step at a zoom: the rung above the precise step, never below an inch.
    public static double RoughStepInches(double pixelsPerInch)
    {
        double fine = StepInches(pixelsPerInch);
        int i = Ladder.IndexOf(fine);
        double above = i + 1 < Ladder.Length ? Ladder[i + 1] : fine;
        return Math.Max(1, above);
    }
}
```

One rung coarser than what the grid draws, floored at a whole inch, from the same ladder. So the
rough steps are whole inches close up, then 3″, 6″, 12″, 24″ … — round numbers a person would say
out loud, and — at every zoom a piece of furniture or a wall is drawn at — a whole multiple of
the drawn grid, so a rough drag still lands on a line that is on the screen (the `SnapGrid`
rule: never snap to a step you did not draw). That holds for every precise step up to 48″; the
ladder's site-plan rungs are not doublings (96 → 144, 288 → 600, 2400 → 6000), so at those three
zooms the rough step is coarser but not on the drawn lines. Stated rather than fixed: no rung of
the existing ladder is a multiple there, and a rough site plan is not what this note is for.
Hand-derived table, from `MinimumSpacingPixels = 14`:

| pixels per inch | precise step | rough step |
|---|---|---|
| 60 | ¼″ | 1″ (floored: ½″ is the rung above) |
| 30 | ½″ | 1″ |
| 14 | 1″ | 3″ |
| 5 | 3″ | 6″ |
| 2.5 | 6″ | 12″ |
| 1.2 | 12″ | 24″ |

Two rungs was weighed and set aside: at 30 ppi it gives 3″, which is coarser than "whole inches
up close" and makes a 10″ shelf undrawable without zooming in. One rung with the inch floor is
the smallest change that is still round everywhere.

Where it is read: `CanvasView.SnapStepInches` and `ModelView.SnapStepInches` become
`SnapToGrid ? (mode == Rough ? RoughStepInches(ppi) : StepInches(ppi)) : 1/1024`. Every tool that
snaps a press or a move — rectangle, stock, wall, the handle resize at `CanvasView` line ~1533,
the arrow-key nudge (`GridStepInches`, and Shift ×4 stays) — reads that one property and needs no
change of its own. **Snap to grid off** in rough mode means what it means today: no grid snap at
all; the mode does not fight the setting.

### 2.2 Parts just touch: catching without stating

`SnapResolver.Resolve` is unchanged and still runs: a rough drag towards another part's edge
still catches on it at the same 10 px radius, because lining two planks up is what the person
is doing and a mode that let them land 1″ apart would make firm-up (§3) find nothing to propose.
What changes is the drop: in rough mode the canvas **discards `SnapPlan.Relationships`** and
puts only the move to the updater. The snap indicator line still draws during the drag (it is
telling the truth: the edges line up); nothing is stored. The same for `PlacementTool` in 3D: the
`Flush` against the face a part is set down on, and whatever its edges caught, are dropped.

Deliberately not a switch in the resolver: it stays pure and mode-free, and the discarding is one
`if` at the two drop sites, which is also where a test can see it (§7.1).

### 2.3 The plank: a part with no stock

In rough mode the rectangle tool draws a **plank**: `Box.AsDrawn(...)` with the default ¾″ depth
as today, *plus* `Part = new Part(Stock: null, Species: null, Quantity: 1, PlanAxes(Length, Width))
{ Rough = true }`. The longer plan side is the length (the stock tool's rule for two free
dimensions). That one difference — a `Part` — is what puts a plank on the cut list (§5) with an
empty material column, which the cut list already renders as "no stock", and what lets firm-up
suggest stock for it (§3.3). It is named `Part N` by `NextPartName` as every part is.

The **stock tool stays available** in rough mode. A person who knows it is a 2×4 says so; the
drag's free length still lands on the rough step, and the part is marked rough (§4) because its
length is a rough number. Walls and openings are unchanged except for the step; a wall is never
rough (§4.1).

**No freehand geometry.** A "rough rectangle drawn with the wobble" was weighed and set aside:
the wobble is rendering (`SketchStroke.Wobble`, seeded per edge) and the model stays exact
rectangles, so there is nothing for a freehand stroke to be *of*. What a rough part gets instead
is a look of its own (§6.3): light pencil, the drafting convention for a construction line.

### 2.4 Labels off, size in the status bar

In rough mode the on-the-fly width and height labels of `SelectionDimensions` are **not drawn**
for the selection, so the sheet reads like a napkin — outlines and nothing else. Annotated
`Dimension` entities the drawing holds are still drawn (they are statements, not on-the-fly
measurements). The live size goes to the **status bar**: during a drag `48 × 12` (mono, the
label format), and with a part selected its `name  48 × 12 × ¾` — the three finished sizes in
cut-list order, so the readout and the cut list say the same thing.

Typing still has to be reachable, because typing is how a size gets stated (§3.1). Two ways in,
both existing gestures: the selected part's labels **appear while the pointer is over it** (and
while a dimension is open for editing), so GUI-DRAW-02's click-the-label works after a hover;
and the Part panel's fields work as they always have. Nothing new to learn; the labels are quiet
rather than gone.

---

## 3. Firming up

A rough sketch becomes a design in three ways, each usable alone, in any order, on any subset.
They are ordered here from the one that exists to the one that is new.

### 3.1 Type a size (exists)

Click a label or a Part-panel field and type `2'-6 1/2"`. This is `DimensionEntry.RequestFor` —
`AddRelationship(ParamValue)` or `SetParameter` — exactly as in Precise. It is legal on a rough
part and it is *itself* a firming: napkin has been told what this number is meant to be. Typing
either size, or a depth in the Part panel, **clears the part's rough mark** (§4.1) in the same
gesture (a `SetPart` alongside the size request, one undo step). No "round or keep" question
arises: a rough part's sizes are already round numbers (§2.1), and anything else on it was typed.

### 3.2 Firm up: proposed relationships

**Edit ▸ Firm up…**, key **`F`** (free), with a selection or, with nothing selected, over every
part on the sheet. It opens the join popover's sibling: a sheet over the paper (not a window),
`FirmUpPanel`, listing what napkin proposes with a tick per line, all ticked; Enter accepts the
ticked ones, Escape cancels, a click on a line's text selects the two parts so the person can see
which pair it is. The whole acceptance is **one undo step**, "Firm up", as join-all is.

**What is proposed** is a pure function in `Napkin.Core.Geometry` beside `JointProposals`:

```csharp
public static class FirmUpProposals
{
    /// Every relationship the touching parts imply and the sketch does not yet hold, in a
    /// stated order, each one holding exactly as the parts stand — so the updater accepts it
    /// without moving anything.
    public static ImmutableArray<FirmUpProposal> For(Sketch sketch, IReadOnlyCollection<EntityId> parts);
}
public sealed record FirmUpProposal(Relationship Relationship, string Sentence);
```

Two kinds, for each unordered pair of the given boxes (both must be parts — `Part != null`; a
wall is never firmed), lower id first:

1. **Against.** For every contact `JointGeometry.TouchingFaces(sketch, a, b)` reports — two faces
   facing each other with a contact of non-zero area — one `Flush(FeatureRef(a, face), FeatureRef(b,
   face))`. This is the relationship `PlacementTool` states when a part is set down on another, so
   the vocabulary is the assembly model's own (§2.3 there). Sentence: *"Leg 1's top against Top's
   underside"*, in `SceneWords`.
2. **Alongside.** For a pair that has at least one contact, every pair of faces that face the same
   way along **X or Y**, lie at exactly the same coordinate, and whose extents on the other two
   axes overlap or abut — one `Flush` of those two faces. Sentence: *"Shelf's front flush with
   Side's front"*. Z is left out on purpose: two parts drawn in the plan both lie on the datum
   with the default depth, and "their undersides are coplanar" says nothing the person meant.
   A Z alignment that *is* meant is stated by setting one part on another, which is kind 1.

Rules that keep the list short and honest:

- Only pairs that touch. Two parts sharing an X three feet apart get nothing (the
  `SnapResolver.Overlaps` principle, with zero slack: exact geometry, exact test).
- Overlapping is not touching. `samples/overlap` produces no proposal (a contact of non-zero area
  requires the faces to face each other at one coordinate; interpenetrating boxes have none).
- Already stated is skipped: a proposal structurally identical to a relationship the sketch holds,
  in either order of its two references, is not offered (invariant 4 of the geometry model would
  refuse it anyway; better not to list it).
- Direction: `Flush(a, b)` reads "b follows a"; a is the **older** part (lower id). Deterministic;
  and if the person wanted the other way round, the join popover's Swap button has the precedent.
- No `Coincident`. Two `Flush` on the two axes say the same thing exactly and each can be accepted
  alone; one kind fewer to explain.
- No `EqualParam`. Four legs of equal width are a real case and a later slice (§8, "later"): it
  needs a rule for which pairs to offer that this note does not want to guess at.
- Order: by pair (lower id, then higher), then against before alongside, then axis X, Y, Z, then
  face order South, East, North, West, Bottom, Top. Tests pin it (§7.2).

**Accepting.** Each ticked proposal is one `AddRelationship` put to the updater in order, inside
one gesture. Every proposal holds by construction (the faces are already there), so a rejection
can only be a conflict with something the design already states; a rejected one is reported in
the popover's message line in the updater's words and the rest still land — the person is told,
never silently short-changed. Parts whose proposals were all accepted or that had none are not
thereby un-rough: rough is about sizes and stock (§4.1), and those are the next two steps.

### 3.3 Firm up: suggested stock

The same popover has a second section, **Stock**, one line per rough part with no stock: the
part's finished sizes and the nearest stock, e.g. *"Part 3, 48 × 3 1/2 × 1 1/2 — 2x4"*, with a
drop-down of the top three and "none", ticked when there is a candidate. A pure function in
`Napkin.Modules.Furniture` beside `StockAssignment`:

```csharp
public static class StockSuggestion
{
    /// Stock items whose fixed dimensions are each within Tolerance of the part's finished
    /// sizes, best first; empty when nothing is near.
    public static ImmutableArray<StockItem> For(FinishedSize size, MaterialsLibrary library);
    public static readonly Length Tolerance = Length.Inches(1);
}
```

The rule: a candidate is any item `StockAssignment.Fixes` reports at least one dimension for, where
every fixed dimension is within 1″ of the part's finished value for that dimension name. Ranking:
**items that fix more dimensions first** (a 2×4 claims the width and the thickness; a ¾″ panel
claims the thickness only, and a panel would otherwise "win" for every ¾″-thick strip by matching
one number exactly), then by the sum of the absolute differences over the fixed dimensions,
smallest first, then by name, ordinal. Fasteners fix nothing and never appear. Species is never
suggested. The values themselves come from the materials library at run time; this note names no
dimension from memory.

**Accepting** a stock line is `StockAssignment.RequestsFor(sketch, box, part with { Stock = name,
Rough = false }, item)` — the same batch the stock tool and the Part panel make — so the fixed
plan dimensions are set to the stock's exactly, the free one keeps its rough value, and the part is
no longer rough. Rounding never enters: the yard's sizes are exact, and the free dimension is
whatever the person drew or typed.

### 3.4 Firm up: the sizes

A third section, **Sizes**, one line per rough part whose width or height nothing states:
*"Part 3, keep 48 × 12 as drawn"*, ticked. Accepting states `ParamValue` for each of the two plan
sizes that has no owner (`DimensionEntry.DrivingRelationship` is null) and clears rough. This is
what "firm" means for a part nobody typed a number on: the drawn number becomes a stated one. A
part that gets stock in §3.3 has its fixed dimensions stated by the assignment already, and this
section states only the remaining free one.

**Everything ticked and Enter** is therefore: relationships in, stock assigned, sizes stated,
nothing rough left in the selection — one undo step. The popover closes with a one-line summary
in the status bar: *"Firmed up 4 parts: 4 relationships, 3 stocks, 1 size. Next: Join all
touching."* The last clause is the only mention of joinery (§6.1).

---

## 4. What "rough" is in the model

### 4.1 Weighed: derive it, or store it

**Derive it — no flag.** "Rough" would mean: a part whose width or height no relationship states,
or whose stock is null. Attractive: nothing new in the file, undo trivially consistent, and
`DimensionEntry.DrivingRelationship` and `Part.Stock` already answer both questions. **Rejected,
on evidence:** a rectangle dragged out in Precise mode also has no `ParamValue` on its sizes (a
drag lands a size, it does not state one — that is design §3.2), and the coffee-table fixture has
no stock on any part and is precise by every intention. Under the derivation, every sample and
every precise drawing made with drags would read "rough" on the cut list. The derivation confuses
*unstated* with *unmeant*, and those are different: Rough is a fact about how a part was entered,
which nothing in its geometry can recover.

**Store it — one boolean on `Part`.** `Part.Rough`, `part.rough` in the file, required, format
**8 → 9** (beta policy: a version-8 file is refused with the existing message; `SceneWriter`
writes 9 only; the fifteen sample scenes gain `"rough": false` by a scripted edit in the same
commit). Fields on the part, like stock and quantity, so undo, copy, mirror-copy, delete, save
and load carry it for free and nothing in the kernel reads it. **Recommended.**

Set true by: the rectangle tool and the stock tool in rough mode, and `PlacementTool` in rough
mode. Never by a wall or an opening (they have no `Part`). Copies of a rough part are rough.

Cleared by: typing either plan size or the depth (§3.1); accepting the part's stock or sizes in
Firm up (§3.3, §3.4); the Part panel's tick-box (§4.2). Never cleared by a move, a snap, or a
relationship: those are about where it is, not what it is.

Never set by anything in Precise mode, so a person who never presses `Q` never sees the word.

### 4.2 The Part panel

One checkbox, **Rough**, under the stock field, reflecting `Part.Rough`; changing it is a
`SetPart` in one undo step, "Mark rough" / "Mark firm". It is the escape hatch for both directions:
a plank the person is happy with as drawn ("it's a scrap, leave it"), and a precise part they want
to think of as rough again.

---

## 5. The cut list and the shopping list

`CutListRow` gains **`Rough: bool`** — true when any member is rough. It is **not in the grouping
key** (§3 step 4 of the cut-list note): a rough leg and a firm leg of the same sizes and stock are
one row of two, and the row is rough because one of them is. Otherwise firming one leg of four
would split the row, which is a lie about what to cut.

- **Table:** a `rough` tag after the label, in the pencil colour, and the row's size cells in
  the lighter ink of §6.3; a footer line *"3 rows are rough — sizes as drawn, stock not chosen"*
  when any are. Sizes are the drawn sizes, formatted as every size is; a rough number is exact on
  the grid, so no `≈` arises from roughness.
- **CSV:** a column `Rough` (`yes` or empty) after `Material`, before `Cuts`; the header sentence
  gains ", rough rows are as drawn". The export is the rows, as always.
- **Material:** a rough part with no stock is what the cut list already calls an empty material;
  it is not marked `Unresolved` (that word means "a name this build does not carry").
- **Shopping list:** unchanged in its arithmetic — a rough part with stock is counted as its stock,
  a rough part without stock is not on the shopping list, as today. It gains the same footer:
  *"N parts are rough and have no stock; they are not on this list."* So the total never quietly
  omits half a bench.
- **Parts view** ([`parts-view.md`](./parts-view.md)): a rough cell's caption gets the same tag;
  the cell draws the same blank. Nothing else.

`samples/stocked-bench.expected.json` and the others gain nothing: no sample is rough. A new
sample is not added; the GUI workflow (§7.4) draws its bench.

---

## 6. Interactions

### 6.1 Joinery

No joint is made in rough mode unless asked: Join (`J`) and Join all touching stay in the menu
and work on rough parts exactly as on firm ones (asking is using them). Firm up proposes no
joints — a joint is a decision about *how* two parts meet, which a rough sketch has not made — and
its closing line points at Join all touching as the next step. `JointProposals` and
`FirmUpProposals` share `JointGeometry.TouchingFaces` and nothing else.

### 6.2 Undo

Mode switch: not a step (§1.1). Each rough drag: one step, as now. Firm up: one step for the
whole acceptance, named "Firm up". Typing a size on a rough part: one step carrying the size and
the clearing of the mark. Undo of a firm-up restores `Rough = true`, the removed statements and
the stock-less parts at once, because it restores the `Sketch` value.

### 6.3 The look

View ▸ Sketch is untouched: paper and line are chosen there whatever the entry mode. A **rough
part** is drawn in the **light pencil** — the same stroke, at the second pass's weight (`SketchInk`
already draws a heavier and a lighter pass; a rough part gets the lighter alone), under every Line
setting including Clean. A firm part is drawn as today. So on the default napkin the sheet reads as
one sketch, with the firmed parts the ones the pencil went over again — which is what a person does
on paper. Precise-mode drawings never contain rough parts and look exactly as they do now.

### 6.4 The plan and 3D views

One editor, one mode, both views. In 3D, `ModelView.SnapStepInches` reads the rough step (§2.1);
`PlacementTool`'s click-to-place default of 24″ is already round; a drop discards its snaps
(§2.2) and marks the placed part rough. The standard 2D views ([`standard-views.md`](./standard-views.md))
share the plan's tools and so the plan's behaviour. The status bar readout is the same three
numbers in every view.

### 6.5 Save and load

A design saved mid-sketch is saved with its rough marks (format 9) and opens with them, in
whatever mode the app is in. The marks are the design's; the mode is the session's. A file with
rough parts opened by someone who then draws precisely simply has some rough parts and some firm
ones, and the cut list says which.

---

## 7. Test plan

### 7.1 Pure snapping (`Napkin.App.GuiTests/Unit/SnapGridTests.cs`, `CanvasSnapModeTests.cs`)

- `RoughStepInches` reproduces §2.1's table exactly, and for every ladder entry: the rough step is
  ≥ 1″, is on the ladder and ≥ the precise step; for every precise step up to 48″ it is also an
  integer multiple of it (the three site-plan rungs above are named in §2.1 and are exempt).
- At 60 ppi a drag from 0.3″ to 10.6″ lands a plank 11″ wide in Rough (0 → 11) and 10 1/4″ wide
  in Precise (1/4 → 10 1/2); the same `SnapGrid.Snap` with the two steps.
- A rough drop beside a part: `SnapPlan.CaughtSomething` is true, the landed anchor equals the
  precise-mode anchor, and the sketch gains **no** relationship; in Precise the same drop gains one
  `Flush`. (Catalog `CVS-013`.)
- The rectangle tool in Rough makes a box whose `Part` is `(null, null, 1, Length×Width)` with
  `Rough == true` and depth ¾″; in Precise `Part == null` as today. (`CVS-014`.)

### 7.2 Firm-up proposals (`Napkin.Core.Geometry.Tests/FirmUpProposalsTests.cs`, hand-derived)

The **quick bench**, drawn in the plan as its side elevation — what a person sketches on a napkin
— all parts lying as drawn, depth ¾″:

| Part | Anchor | Width × Height |
|---|---|---|
| Top | (0, 16) | 48 × 2 |
| Leg 1 | (2, 0) | 4 × 16 |
| Leg 2 | (42, 0) | 4 × 16 |
| Stretcher | (6, 4) | 36 × 3 |

Expected, in order (ids ascending in the order above):

1. Top – Leg 1, against: `Flush(Top.South, Leg 1.North)` — contact x 2..6 at y = 16.
2. Top – Leg 2, against: `Flush(Top.South, Leg 2.North)` — x 42..46.
3. Leg 1 – Stretcher, against: `Flush(Leg 1.East, Stretcher.West)` — y 4..7 at x = 6.
4. Leg 2 – Stretcher, against: `Flush(Leg 2.West, Stretcher.East)` — x = 42.

And nothing else: Leg 1 and Leg 2 share y = 0 and y = 16 but do not touch (no alongside without a
contact); no Z proposal though all four share z = 0 and z = ¾ (§3.2 kind 2). Further cases:

- Two planks edge to edge, 48 × 6 at (0,0) and 48 × 6 at (0,6): one against (`North`/`South`) and
  two alongside (`West`/`West` at x = 0, `East`/`East` at x = 48) — three proposals.
- The same with the second plank at (12, 6): one against, no alongside (x = 12 ≠ 0, x = 60 ≠ 48).
- `samples/overlap`: no proposals. `samples/l-bracket`, `samples/chain-of-five`: every proposal
  the sketch already holds is skipped, and the expected residue is written into the test by hand.
- A wall in the set: ignored. A part with itself: never a pair.
- Every proposal, added to the sketch through the updater, is `Accepted` and moves nothing
  (`RelationshipChecker.Check` reports no violation; every anchor unchanged).

### 7.3 Stock suggestion and the cut list (`Napkin.Modules.Furniture.Tests`)

- `StockSuggestion.For` on the library this build ships: a part whose width and thickness equal
  the library's 2x4 cross-section ranks the 2x4 first with a difference of zero; a ¾″-thick
  12″-wide plank ranks a two-dimension lumber match (if one is within 1″) above the ¾″ panel;
  a 7 × 2 × 2 part with nothing within 1″ on every fixed dimension returns empty; a fastener is
  never returned. Values are read from the library in the test, never typed into it.
- `CutList.Of`: one rough leg and one firm leg of equal sizes and stock → one row, `Rough == true`,
  quantity 2. All-firm → `Rough == false` on every row of every sample (`SampleExpectations`
  unchanged). CSV header and `Rough` column round-trip.
- Scene v9: `part.rough` written, read, refused when not a boolean; a v8 file is refused with the
  existing unsupported-version message (`BadScenes`).

### 7.4 GUI workflows (`Napkin.App.GuiTests/Workflows/RoughWorkflows.cs`)

Each ≥ 5 actions, keyboard and pointer both, an assertion after a state change; each raises the
passing-workflow count.

- **GUI-SKETCH-01 Sketch a quick bench.** New sheet; press `Q`; the status bar reads `ROUGH` and
  the toolbar toggle shows Rough; press `R`; drag the four parts of §7.2 at world coordinates
  slightly off their round values (e.g. from (0.3, 15.7)); assert each box's sizes and anchors are
  the round values, `Part.Rough` is true, no relationship exists though Leg 1 was dragged to touch
  the top (the indicator was shown: `canvas.LastSnap.CaughtSomething`); assert no label is drawn
  for the selected leg until the pointer moves over it; assert the status readout says `4 × 16 × ¾`.
- **GUI-SKETCH-02 Firm it up and read the cut list.** Continue from a sheet built as in -01
  (a helper); press `F`; the popover lists the four relationships of §7.2, four stock lines and
  four size lines; click the tick of the Leg 2 – Stretcher line off (pointer); Enter; assert three
  `Flush` relationships, every part `Rough == false`, and stocks assigned where the library had a
  candidate; Ctrl+Z; assert all four rough again and no relationships; Ctrl+Y; open the cut list
  (menu, pointer); assert no row is rough and the rows equal the hand-written expectation in the
  test.
- **GUI-SKETCH-03 Type a size on a rough part.** Draw one plank rough; hover it (pointer) and the
  width label appears; click it; type `3'-0"`; Enter; assert width exactly 36″, `Rough == false`,
  and one undo restores both the width and the rough mark.
- **GUI-SKETCH-04 Rough on the cut list and the mark by hand.** Draw two planks rough; open the
  cut list; assert both rows carry `rough` and the footer sentence; back in the plan select one,
  untick Rough in the Part panel (pointer), Tab to the next field, Enter; the cut list shows one
  rough row; the CSV text (`CutListCsv`) has `yes` on that row only.

---

## 8. Implementation slices

Each lands alone on `main` per CLAUDE.md (build, test with coverage, `ratchet check`, `scorecard
stubs` when a catalog row is added, version bump by the lander). Default model **Sonnet**. Pure
slices first, so that something real and testable lands before any chrome (CLAUDE.md, core first).
Files are disjoint between slices except `MainWindow.axaml(.cs)`, `CanvasView.cs` and
`features/catalog.json`, which C, E and F touch — run those in sequence.

| | Slice | Files | Tests | Depends on | Issue |
|---|---|---|---|---|---|
| **A** | `Part.Rough` and scene v9: the field, the writer and reader, the fifteen samples, the Part panel tick-box | `src/Napkin.Core.Geometry/Part.cs`; `src/Napkin.Core.Project/FormatStamp.cs`, `SceneWriter.cs`, `SceneReader.cs`, `SceneNames.cs`; `docs/file-format.md`; `samples/*.scene.json`; `src/Napkin.App/MainWindow.axaml(.cs)` (the checkbox only); `tests/Napkin.Core.Project.Tests` | §7.3 (scene) | — | #170 |
| **B** | `FirmUpProposals` — pure, in Core.Geometry | `src/Napkin.Core.Geometry/FirmUpProposals.cs`; `tests/Napkin.Core.Geometry.Tests/FirmUpProposalsTests.cs`; catalog `REL-005` | §7.2 | — | #172 |
| **C** | Rough ladder and the mode in the editor: `RoughStepInches`, `EntryMode`, `SetEntryMode`, both `SnapStepInches`, discarding the drop's statements, the plank, labels on hover, the status readout, the menu item, toolbar toggle and `Q` | `src/Napkin.Modules.Editing/SnapGrid.cs`, `DesignEditor.cs`, `RectangleTool.cs`, `StockTool.cs`, `PlacementTool.cs` (moved from `Napkin.App/Editing` in #166); `src/Napkin.App/Viewing/CanvasView.cs`, `ModelView.cs`; `MainWindow.axaml(.cs)`; `tests/Napkin.App.GuiTests/Unit/SnapGridTests.cs`, `CanvasSnapModeTests.cs`, `Workflows/RoughWorkflows.cs`; catalog `CVS-013`, `CVS-014`, `GUI-SKETCH-01` | §7.1, GUI-SKETCH-01 | A | #174 |
| **D** | `StockSuggestion` and `CutListRow.Rough`: the row field, the table tag and footer, the CSV column, the shopping-list footer, the Parts-view caption | `src/Napkin.Modules.Furniture/StockSuggestion.cs`, `CutList.cs`, `CutListRow.cs`, `CutListCsv.cs`, `ShoppingList.cs`; `src/Napkin.App/Viewing/CutListTable.cs`, `ShoppingListTable.cs`, `PartsCellDrawing.cs` (if landed); `tests/Napkin.Modules.Furniture.Tests`; catalog `CUT-017`, `CUT-018` | §7.3 | A | #176 |
| **E** | The Firm up popover: `FirmUpPanel`, the three sections, ticks, Enter/Escape, one gesture, the rejection line, the summary, `F` and Edit ▸ Firm up… | `src/Napkin.App/MainWindow.FirmUp.cs` (new partial, the joinery partial's shape), `MainWindow.axaml`; `Workflows/RoughWorkflows.cs`; catalog `GUI-SKETCH-02`, `GUI-SKETCH-03` | GUI-SKETCH-02, -03 | B, C, D | #178 |
| **F** | Rough parts in the light pencil; typing clears the mark; the cut-list workflow | `src/Napkin.App/Viewing/SketchInk.cs`, `CanvasView.cs`, `ModelView.cs`; `MainWindow.axaml.cs` (dimension commit); `Workflows/RoughWorkflows.cs`; catalog `GUI-SKETCH-04` | GUI-SKETCH-04 | C, D | #180 |

**Later, not in M7:** `EqualParam` proposals for parts of equal size (§3.2); remembering the mode
(§10 decision 3, if Marc wants it; one `UserSettings` field). Neither blocks anything above.

**Versioning:** each slice bumps the minor. **Ratchet:** A raises Core.Project's floor, B
Core.Geometry's, D the furniture module's — none touches another area's; C, E and F raise the
workflow count. `Napkin.App` is excluded from line coverage, so C's unit tests raise nothing and
are still required.

---

## 9. Risks

1. **Two modes confuse people.** The classic mode error: drawing precisely in Rough, or rough in
   Precise, without noticing. Mitigations, all in the design: the mode is named in the status bar
   at all times; Rough starts off every launch (§1.2); rough parts *look* different (§6.3) and are
   *tagged* on the cut list (§5), so a rough part cannot hide; and Precise mode cannot make a
   rough part, so a person who never presses `Q` is never in this story. If Marc finds the toggle
   itself is the confusion, the fallback is a **tool** rather than a mode — a "Plank" tool beside
   Rectangle that draws rough with coarse snaps and no statements, and no mode at all. Everything
   in §3–§5 survives that change unchanged; only §1 and the snap-step seam in §2.1 move.
2. **The rough step is wrong at some zoom.** It is a pure function with a table (§7.1); changing
   the rung or the floor is one line and one table row.
3. **Proposal lists get long** on a real assembly (a bookcase with six shelves against two sides
   is 12 against + up to 24 alongside). The popover scrolls; "all ticked" is the default for the
   common case; and the against/alongside split means a person who wants only "they touch" can
   untick a section with one header tick. If it is still too much, alongside becomes unticked by
   default — a one-line change and a §10 decision then.
4. **A proposal is rejected** because the design already states something that conflicts (a
   pinned part, an `AxisDistance`). The popover reports it in the updater's words and lands the
   rest; nothing moves, because every proposal holds by construction. Tested in §7.2's last item.
5. **Format bump 8 → 9 collides** with another slice bumping the format at the same time (the
   Views and Woodworking milestones have open design notes). Whoever lands second re-bumps and
   re-scripts the samples; the samples edit is mechanical and the reader's tests say at once if
   it was missed.
6. **`SnapStepInches` is read in more places than listed.** C greps for it and for
   `GridStepInches` before touching anything; the nudge and Duplicate read the *grid* step on
   purpose (a nudge is a precise move) and stay as they are — stated here so C does not "fix" them.
7. **Hover-to-reveal labels and the headless GUI suite.** `AppDriver.MoveTo` exists and the
   indicator tests already rely on pointer position; GUI-SKETCH-03 uses it.

---

## 10. Decisions for Marc

In plain words. Each has the default I'd pick; "use the recommended defaults" is a complete answer.

1. **What to call the two modes.** The look is already "Sketch" (View ▸ Sketch: napkin, pencil),
   so calling the entry mode "Sketch" too would put two different things under one word.
   *Recommended:* **Rough** and **Precise**, the status bar showing whichever is on. Alternatives:
   "Napkin / Drafting", or "Loose / Exact".
2. **The keys.** `Q` to switch Rough on and off, `F` to open Firm up. Both are unused today.
   *Recommended:* as written; say if you'd rather they had no key and lived only in the menus and
   toolbar.
3. **Does napkin remember the mode between launches?** *Recommended:* **no** — it always starts
   Precise, because being in Rough without knowing it does the more expensive damage (a whole part
   drawn to the wrong numbers with nothing stated), whereas being in Precise when you wanted Rough
   is noticed at the first drag. Alternative: remember it, like Snap to grid.
4. **Is "rough" saved in the file, or only a way of drawing?** *Recommended:* **saved**, one
   yes/no per part. Without it napkin cannot tell a rough plank from a carefully dragged precise
   part — both are just rectangles nobody typed a number on — and every existing sample would be
   called rough. Saving it means old files stop opening (they already do on every format change;
   that is the beta rule), and the fifteen samples get the new field in the same commit.
5. **How rough parts look on the sheet.** *Recommended:* in a **lighter pencil** than firm ones,
   the way a construction line is lighter than a finished one, whatever paper and line are chosen.
   Alternative: no visual difference — the cut-list tag alone says which are rough.
6. **How coarse "rough" is.** *Recommended:* **one step coarser than the grid you can see, and
   never finer than a whole inch** — so whole inches close up, then 3″, 6″, a foot. Alternative:
   two steps coarser (3″ close up), which makes small parts hard to draw without zooming in.
7. **Should Firm up also suggest joints?** *Recommended:* **no** — it ends by pointing you at Join
   all touching, which already does that well, and a rough sketch has not decided how parts meet.
8. **Should Firm up notice that four legs are the same size and offer to keep them equal?**
   *Recommended:* **not in this milestone**; it is a real and useful thing, and it needs its own
   rule for which parts to offer it on. Listed as later.
9. **Should Firm up suggest stock at all, given napkin never guesses?** *Recommended:* **yes, as
   a suggestion you accept**, never applied on its own — the nearest sizes from the materials
   library, best first, "none" always available. A suggestion you have to tick is not a guess.
