# Parts, the cut list and the shopping list

Status: **DRAFT, not yet signed off by Marc.** §4 (the shopping list, #9) was implemented on 2026-09-24 at Marc's direction, with sign-off still pending; its worked example is a new `samples/stocked-bench` (§8.3), not the coffee table. Written as part of issue #7 (the materials
library), which owes it to #8 (the parts model and cut list) and #9 (the materials list and
takeoff). §1–§4 are the model, the file-format change and the two algorithms, stated exactly
enough to implement from; §6 checks them against the committed coffee-table fixture; §7 is the
step-by-step plan; §8 lists the decisions that are Marc's, not an agent's (CLAUDE.md, "Decisions
that are Marc's").

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024-inch grid
([`geometry-model.md`](./geometry-model.md) §1), a box stores the width and height that were typed
and derives its corners (§2.3), strict reading with no migration and no shims
([`../file-format.md`](../file-format.md), DESIGN.md §12), and the stock picker decided in issue
#7 — one icon per category, a text list inside the chosen category, a hover showing the actual
size before the item is placed, and species and grade set afterwards in a properties panel.

---

## 1. What a part is

A **part** is one piece you cut. It is not a new entity type: it is a box entity plus the
information a plan view cannot hold.

A part has three finished dimensions — **length**, **width** and **thickness**. A plan view can
hold two of them, and it holds them in the box's stored `width` and `height` parameters, which is
what the cut list reads: never the distance between derived corners (`CUT-002`, geometry model
§2.3), so a part typed 10″ wide still lists as 10″ after it is rotated 37°.

So a part needs exactly four things the format does not carry today:

| What | Why the plan view cannot hold it |
|---|---|
| A **name** — "Leg", "Apron, long" | The format stores ids, geometry and relationships and nothing else (file-format.md, "There is no display name on an entity") |
| The **one dimension that is out of the plan** | A plan view has two axes |
| **Which of the three** the box's two axes are | A box 40″ × 3/4″ could be a 40″-long piece on edge or a 3/4″-wide rip; only the person drawing knows |
| Which **stock** it is cut from, and its **species** and **quantity** | None of these is geometry |

### 1.1 The three dimensions, and the two that are in the plan

A part records which of length, width and thickness lies along the box's local X (its stored
`width`) and which along its local Y (its stored `height`). The third name — the one neither axis
claims — is the out-of-plane dimension, and its value is the one number the part stores itself.

```
planAxes = { x: <name>, y: <name> },  x ≠ y,  each one of length | width | thickness
outOfPlane = the remaining name;  its value is part.outOfPlane
```

Nothing is stored twice: two of the three come from the box's parameters and one from the part.
That is what keeps `CUT-002` true by construction — there is no second copy of an in-plan
dimension that could drift from the box's.

Worked, on the four kinds of part in the coffee-table fixture:

| Part | Box | `planAxes` | out of plane |
|---|---|---|---|
| Table top, lying flat | 48″ × 24″ | `x: length, y: width` | thickness, 3/4″ |
| Leg, standing up | 2 1/2″ × 2 1/2″ | `x: width, y: thickness` | length, 16 1/4″ |
| Apron running along X, on edge | 40″ × 3/4″ | `x: length, y: thickness` | width, 3 1/2″ |
| Apron running along Y, on edge | 3/4″ × 16″ | `x: thickness, y: length` | width, 3 1/2″ |

### 1.2 How a footprint maps to a stock's cross-section

This is the question issue #7 poses: *"the part's footprint then follows the stock's cross-section
(a 4x4 leg is 3-1/2″ square, only its length is free)."* With §1.1's naming, the answer falls out:
**a stock item fixes some of the three names, and `planAxes` says where each of those lands.**

| Stock kind | Names the stock fixes | Free | The case |
|---|---|---|---|
| `LumberStock` — 2x4, 1x6, 6x6, 5/4x6 | **width and thickness** (the cross-section) | length | A 4x4 leg is 3 1/2″ × 3 1/2″; only how long is yours. |
| `PanelStock` — 23/32 plywood, 7/16 OSB | **thickness** | length, width | A plywood shelf is any size you like, at the panel's thickness. |
| `HardwoodStock` — 4/4, 8/4 | **thickness** (the surfaced thickness; the rough one is what you pay for) | length, width | Hardwood is sold in random widths, so the width is yours too. |
| no stock | none | all three | The coffee-table fixture today: every dimension is stated by the design. |

**On assignment**, for each name the stock fixes:

1. If `planAxes` puts that name on X or Y, the box's corresponding parameter is **set** through the
   normal update interface (`IGeometryUpdater`), so relationships propagate and a conflict is
   reported the way any other conflict is (geometry model §4). If it is the out-of-plane name,
   `part.outOfPlane` is set directly — nothing else depends on it.
2. If any of those updates conflicts — the part is pinned to something that cannot move — the whole
   assignment is **refused** with the same conflict summary the editing loop already shows, and the
   part keeps both its old size and its old stock. Nothing is half-assigned.
3. Free names are never touched.

A `LumberStock` assignment therefore fixes a leg's 2 1/2″ square footprint and leaves its 16 1/4″
alone; the same assignment on an apron fixes its 3/4″ plan dimension and its 3 1/2″ face and leaves
its 40″ alone. One rule, both cases, no special-casing by orientation.

**A part whose stock is `null` is legal and is not an error.** Its finished dimensions are its own,
it lists with an empty material column, and the shopping list reports it under "no stock chosen"
rather than dropping it.

### 1.3 What a part is *not*

- **Not a joinery model.** A tenon, a dado and a mitre change finished length, and napkin does not
  know about them. The cut list lists the stored size; #8 adds no joinery allowance and does not
  pretend to one.
- **Not a solid, as designed here.** `part.outOfPlane` was a number on a plan-view box, not a Z
  extent with a position; [`assembly-model.md`](./assembly-model.md) (signed off 2026-09-22) gives
  every box a real position and orientation in space and moves this value to `Box.Depth`. The cut
  list's own rule stands unchanged: it reads stored parameters, never a derived position.
- **Not a grain direction.** Hardwood width and sheet nesting both want one eventually. Not here.

---

## 2. The file-format change

One change, one bump: **`formatVersion` 1 → 2**. Beta policy — a version-1 file is refused with the
message the reader already gives, never converted (DESIGN.md §12, file-format.md rule 2). No
migration code, no shim, and `SceneWriter` writes version 2 only.

Every field the format defines is required, with no optional fields (file-format.md, "Every field
listed in this document is required"), so the new fields are required and a part with no stock
writes `"stock": null` rather than leaving the field out.

### 2.1 `name`, on every entity

```json
{ "id": "…", "type": "box", "layer": "…", "name": "Leg, south-west",
  "anchor": { "x": 1536, "y": 1536 }, "width": 2560, "height": 2560, "rotation": 0,
  "part": { … } }
```

A string on **every** entity type, not only on boxes — a named dimension reads better in a conflict
message too. An empty string is legal and means unnamed; only a non-string is refused. Names are
**not** unique and are **not** ids: grouping uses dimensions, not names (§3 step 4).

### 2.2 `part`, on a box

```json
"part": {
  "stock": "2x4",
  "species": "Douglas fir",
  "quantity": 1,
  "outOfPlane": 16640,
  "planAxes": { "x": "width", "y": "thickness" }
}
```

| Field | Type | Meaning | Refused when |
|---|---|---|---|
| `stock` | string or `null` | A nominal name the materials library resolves, normalised the way `NominalName` normalises it, so `"2 x 4"` and `"2x4"` are one stock | not a string and not null |
| `species` | string or `null` | Free text, set in the properties panel after placing (issue #7). Never interpreted by this build | not a string and not null |
| `quantity` | integer ≥ 1 | How many identical copies this one box stands for, for the four legs a person draws once | not an integer, or < 1 |
| `outOfPlane` | integer units > 0 | The value of the one dimension the plan cannot show | not an integer, or ≤ 0 |
| `planAxes` | object | `x` and `y`, each exactly one of `length`, `width`, `thickness` | a missing key, an unknown value, or `x` equal to `y` |

`part` is `null` on a box that is not a part — a wall, an opening — so its shape is
`{ … } | null`, and M2's existing boxes become `"part": null`.

**`stock` is a name, not an id, and is not validated at load.** The reader checks that it is a
string; it does **not** check that this build's materials library carries it. Deliberate, and the
same stance the manifest already takes on `adoptedCode` (file-format.md, "`adoptedCode` is
reserved and is not interpreted"): a project drawn against a stock table a later build renames must
still open, with the cut list saying so in the material column. A file is refused for being
malformed, never for naming something this build has not heard of.

### 2.3 What does **not** change

Relationships, dimensions, layers, the container, ids, the integer-units rule, the determinism
rules. A part is fields on an entity, not a new entity type and not a new relationship kind, so
`RelationshipChecker`, the updater and every propagation test are untouched.

---

## 3. The cut list, stated exactly

```
CutList(sketch, library) -> Row[]
```

A pure function — no state, no I/O — so a test calls it directly, and the on-screen table and the
CSV export are two renderings of one list of rows, which is what makes #8's "same data, no
reordering" true rather than asserted.

**Step 1 — collect.** For every box entity whose `part` is not null, in ascending id order, take
the box's **stored** `width` and `height` parameters and the part's `outOfPlane`. `rotation` is not
read at all, so a rotated part cannot list differently from the same part unrotated (`CUT-002`).

**Step 2 — name the three dimensions.** From `planAxes`:

```
dimension["length"], dimension["width"], dimension["thickness"]
  where planAxes.x names box.width, planAxes.y names box.height,
  and the remaining name takes part.outOfPlane
```

A row prints **length × width × thickness**, in that order, because that is how a cut list is read
at a bench.

**Step 3 — resolve the stock.** `part.stock` null → the row's material is empty. Otherwise
`library.TryFind(part.stock, out item)`:

- found → material is `item.Name`, and the row carries `item` for the shopping list to aggregate;
- not found → material is `part.stock` with `Unresolved = true`, shown as "*name* — not in this
  build's materials library" in the table and exported with the same text. **Never silently
  dropped, never guessed at.**

**Step 4 — group.** Two parts are one row when all of these are equal:

```
(length, width, thickness, normalised stock name or null, species or null)
```

Exact `Length` equality, which is exact integer equality. **No tolerance** — two parts typed to the
same number are equal on the grid, two typed differently are different parts, and a tolerance would
quietly merge a 10″ part with a 10 1/64″ one.

The **name is not in the key**, so four boxes named "Leg, south-west" … "Leg, north-east" are one
row of four. The row's label is the **longest common prefix of its members' names, cut back to the
last word boundary and stripped of trailing punctuation and whitespace**: "Leg, south-west" and
"Leg, north-east" give `Leg`; "Apron, long, south" and "Apron, long, north" give `Apron, long`.
With no common prefix, the label is the first member's name in id order. That is what makes a
grouped row read like the hand-written expectations without anyone naming groups by hand.

Quantity is the sum of the members' `part.quantity`.

**Step 5 — order.** Descending by length, then descending by width, then descending by thickness,
then by label, ordinal. Deterministic, and largest-part-first, which is the order a person cuts in.

### 3.1 A row, and the CSV

| Field | Type |
|---|---|
| `Label` | string |
| `Quantity` | int ≥ 1 |
| `Length`, `Width`, `Thickness` | `Length` |
| `Material` | string, possibly empty |
| `Unresolved` | bool |
| `Members` | the entity ids, so clicking a row selects the parts |

CSV: a header line saying the list is before saw kerf and joinery allowance (§1.3), then
`Label,Quantity,Length,Width,Thickness,Material`. Lengths render with `LengthFormat.Default`
(feet-inches at 1/16″) and are quoted, because `4'-0"` carries a quote. A length that is not exact
at 1/16″ carries the same `≈` marker the canvas uses (geometry model §1.4), so the CSV never claims
more than the screen does.

---

## 4. The shopping list, stated exactly

```
ShoppingList(cutRows, library) -> StockRow[] + TakeoffRow[]
```

The cut list drives the shop; this drives the store run (#9). It consumes the **rows**, not the
sketch, so the two lists can never disagree about what is being built.

**Step 1 — bucket by stock.** Every row with a resolved stock goes into that stock item's bucket.
Rows with no stock and rows whose stock did not resolve go into a **"no stock chosen"** section,
reported on its own and never aggregated into anything.

**Step 2 — per bucket, by stock kind.**

**`LumberStock` — a linear problem**, because the cross-section is fixed and only length varies.
Each row needs `quantity` pieces of `length`. Then **first-fit decreasing** over the stock lengths
the library carries for that item (`StockItem.StandardLengths`):

1. Sort every needed piece descending by length.
2. Put each piece in the first already-bought board with enough remaining length.
3. If none has room, buy the **shortest stocked length that fits this piece**, and put it there.
4. If a piece is longer than the longest stocked length, report **"no stocked length holds this
   piece"** and buy nothing for it. An honest refusal, not a failure: a 14-foot bench top is a real
   design and the answer is that this stock does not come that long.

**No saw kerf.** A kerf is a real 1/8″ per cut and napkin does not know the blade, so the result is
labelled "boards needed, before saw kerf and defect" on the table and in the CSV header. Adding a
kerf setting later changes this step and nothing else.

Where the library carries **no** stock-length list for an item — `StandardLengths` is empty, which
happens whenever no grading rulebook has been read for that size — the bucket reports total linear
feet and says "napkin has read no stock-length list for this size", and buys nothing. It does not
fall back on a guess. That behaviour is why `StandardLengths` is empty rather than defaulted.

**`PanelStock` — a two-dimensional problem, and #9 does not solve it.** Sheet nesting is its own
issue (DESIGN.md §5.2, a bin-packing heuristic). #9 reports, per panel item, the total **area** of
the parts cut from it, the sheet's area, and `ceil(parts area / sheet area)` sheets, labelled
**"sheets by area — a nesting layout may need more"**. That number is a floor and says so. A part
larger than a sheet in either dimension is reported the way an over-long board is.

**`HardwoodStock`** — sold by the board foot in random widths, so there is nothing to bin-pack. The
bucket reports board feet (§4.1) and no piece count.

**Fasteners** — counted, not packed. Nothing in the model references a fastener yet: the fastener
schedule that produces counts is rules-engine work (DESIGN.md §5.6, IRC Table R602.3(1)), not #9.
The fastener section exists and is empty until a feature fills it.

### 4.1 Takeoff, rounded once at the end

Board feet, from PS 20-20 §2.2 "Board measure": the number of board feet is obtained *by
multiplying the nominal thickness in inches or fraction of an inch by the nominal width in feet by
the length in feet*. Note **nominal**, not dressed — which is exactly why `LumberStock` carries
`NominalThickness` and `NominalWidth` as exact lengths rather than as labels.

```
totalUnits³ = Σ over boards bought of  nominalThickness.Units × nominalWidth.Units × length.Units
boardFeet   = totalUnits³ / (1024³ × 144)            # one board foot is 144 in³
```

computed in `Int128` (geometry model §1.3 provides `Area` for exactly this reason and says the
three-factor case "may be added by #9"), summed exactly, and **converted to a decimal once, at the
end** — never per part, which is #9's acceptance criterion. Shown to one decimal place.

Board feet are computed **from the boards bought, not from the parts cut**, because that is what
you pay for. The parts' own footage is reported beside it as "used", and the difference is waste —
which is the number that makes a person pick a different stock length.

For panels: sheet count, an integer, summed. For hardwood: board feet from the **rough** thickness,
because NHLA ¶16 tallies by the standard thickness and hardwood is priced rough; the surfaced
thickness is what the cut list shows.

Grouping: one row per (stock item, species), ordinal by stock name then species.

---

## 5. Where every number comes from

| Number on a cut-list row | Source |
|---|---|
| the two in-plan dimensions | the box's stored `width`/`height` (geometry model §2.3) |
| the third | `part.outOfPlane` |
| which is which | `part.planAxes` |
| material | `part.stock`, resolved through `MaterialsLibrary` |

| Number on a shopping-list row | Source |
|---|---|
| the stock's actual cross-section | `Napkin.Core.Materials`, cited per row to PS 20-20 Table 3 |
| stock lengths available | `StockItem.StandardLengths`, cited to a grading agency's rulebook |
| boards needed | first-fit decreasing over those lengths (§4 step 2) |
| board feet | PS 20-20 §2.2, on nominal sizes from the library |
| sheet size | `PanelStock.SheetWidth`/`SheetLength`, cited to PS 1-19 §5.4 |

**No dimension in either list is written in code.** Every one is either a number the person typed or
a number the materials library read out of a primary standard and cites.

---

## 6. Checked against the coffee-table fixture

`samples/coffee-table.design.md`, `coffee-table.scene.json`, `coffee-table.expected.json`.

| Part | Plan, from the scene | The third dimension |
|---|---|---|
| Top | 49152 × 24576 = 48″ × 24″ | 3/4″ — **`statedNotInScene`** |
| Leg ×4 | 2560 × 2560 = 2 1/2″ × 2 1/2″ | 16 1/4″ — **`statedNotInScene`** |
| Apron, long ×2 | 40960 × 768 = 40″ × 3/4″ | 3 1/2″ — **`statedNotInScene`** |
| Apron, short ×2 | 768 × 16384 = 3/4″ × 16″ | 3 1/2″ — **`statedNotInScene`** |

### 6.1 The cut list this design produces

With the `planAxes` of §1.1 and the out-of-plane values above, §3 gives:

| Label | Qty | Length | Width | Thickness | Material |
|---|---|---|---|---|---|
| Top | 1 | 4'-0″ | 2'-0″ | 3/4″ | — |
| Apron, long | 2 | 3'-4″ | 3 1/2″ | 3/4″ | — |
| Leg | 4 | 1'-4 1/4″ | 2 1/2″ | 2 1/2″ | — |
| Apron, short | 2 | 1'-4″ | 3 1/2″ | 3/4″ | — |

Every number and every word of that table is derivable:

- **the plan numbers** from the scene's stored `width`/`height` — 49152, 24576, 40960, 768, 2560,
  16384 units, exactly the values `coffee-table.expected.json` already asserts;
- **the third** from `statedNotInScene`, which is where the fixture puts 3/4″, 16 1/4″ and 3 1/2″
  precisely so that nothing tests napkin for a number the scene does not carry;
- **the labels** from §3 step 4's longest-common-prefix rule over the names in `expected.json`
  ("Leg, south-west" … "Leg, north-east" → `Leg`; "Apron, long, south" and "Apron, long, north" →
  `Apron, long`) — and note that the four legs and the two long aprons are already grouped that way
  in the fixture's own `partsList`, by hand, which is independent confirmation that the rule
  produces what a person would write;
- **the quantities** 1, 2, 4, 2, which are the fixture's `partsList` quantities;
- **the order** from §3 step 5, descending by length: 48 > 40 > 16 1/4 > 16.

**The text** is `LengthFormat.Default`. Six of the eight distinct strings are already in the
fixture, derived by hand: `4'-0"`, `2'-0"`, `3'-4"`, `1'-4"`, `2 1/2"` and `3/4"` all appear in its
`partsList` or `dimensionLabels`. The other two — `1'-4 1/4"` for the leg's length and `3 1/2"` for
the apron's face — are this design's renderings of `statedNotInScene`'s `16 1/4"` and `3 1/2"`, and
are new strings for #8's expectations file to state, derived by hand like the rest.

### 6.2 What the fixture cannot produce yet

**The cut list is fully derivable once two things are added to the fixture**: a `name` on each box
and a `part` on each. Both are the §2 format change, and **neither invents a number** — every
number is already written in `coffee-table.design.md` and repeated in `coffee-table.expected.json`
under `statedNotInScene`. `samples/README.md`'s rule is kept: the expectations are re-derived by
hand, not generated.

**The shopping list is not derivable from the fixture at all**, and cannot be made so without a
decision:

1. **No part references any stock.** The fixture states finished dimensions only, deliberately
   (`samples/README.md`: "No nominal-to-actual lumber sizes. Every dimension in these fixtures is a
   finished dimension the design itself states."). With no stock, §4 puts every part in the "no
   stock chosen" section and buys nothing. There is no shopping list to compare against.
2. **There is no expected shopping list to compare to.** `coffee-table.expected.json` carries no
   `cutList` and no `materialsList`; its own notes say those "arrive with #8, #9 and the materials
   library (#7)". A hand-computed expectations file has to be written by a person from the design —
   it cannot be generated (`samples/README.md`, "The rule").

**The fixture's parts do map onto stock this branch ships**, which is what makes closing that gap
worth doing:

- the **aprons** are 3/4″ × 3 1/2″, which is the minimum dressed dry size of a **1x4** (PS 20-20
  Table 3, Boards: nominal 1″ → 3/4″, nominal 4″ → 3 1/2″);
- the **legs** are 2 1/2″ square, which is a dry **3x3** (Table 3, Dimension: nominal 3″ → 2 1/2″
  on both axes);
- the **top** at 48″ × 24″ × 3/4″ is half of a **3/4 Performance Category** 4×8 sheet (PS 1-19
  Table 10 and §5.4) — or a solid glue-up, which napkin has no model for;
- and the aprons are exactly the "several parts from one board" case #9 asks for: 2 × 40″ +
  2 × 16″ = 112″ = 9'-4″, which fits **one 10-foot 1x4** (Standard No. 17 ¶260-a(f), boards 6′ to
  16′; ¶2-i, multiples of 2′) with 8″ to spare. The cut list shows four apron pieces; the shopping
  list shows one board. That is the visible difference between the two lists that #9's acceptance
  criterion asks the fixture to demonstrate.

Note also what the fixture does **not** offer: a leg at 2 1/2″ square is a 3x3, a size a yard
carries but not one most people buy, and the top's 3/4″ is a panel thickness rather than a solid
one. Whoever closes this gap should decide whether the fixture stays as-drawn with those stock
references, or whether a second, more ordinary design (a 2x4-and-plywood workbench) is the better
worked example for #9. That is decision §8.3.

---

## 7. The plan an implementer can follow

### #8 — the parts model and cut list

1. **Format** (`Napkin.Core.Project`, `docs/file-format.md`). Bump `FormatStamp.CurrentVersion` to
   2. Add `name` to every entity and `part` to a box, in `SceneBinder` (read, with refusals),
   `SceneWriter` (write, fixed field order) and `SceneNames`. `LoadProblemKind` already has every
   kind §2.2 needs — `MissingField`, `UnknownField`, `UnknownValue`, `NotAnInteger`,
   `InvalidValue` — so add none. Update `docs/file-format.md` in the same commit. **No migration
   code**: a version-1 file is refused.
2. **Model** (`Napkin.Core.Geometry`'s entity, or `Napkin.Modules.Furniture` if the part is kept
   out of the geometry core). A `Part` record on the box entity, not a side table, so that undo,
   redo, save and load carry it for free. A box with `part: null` behaves exactly as it does today.
3. **Fixture** (`samples/`). Add names and parts to `coffee-table.scene.json`; add the expected cut
   list to `coffee-table.expected.json`, **re-derived by hand** from `coffee-table.design.md` with
   a `derivation` string on every row. §6.1 has the four rows; re-derive them rather than copying
   them, which is the point of `samples/README.md`'s rule.
4. **Cut list** (`Napkin.Modules.Furniture`). `CutList(sketch, library)` exactly as §3. Pure, no
   UI. Tests: the fixture row for row (`CUT-004`); a rotated part listing its typed size
   (`CUT-002`); four identical legs as one row of four (`CUT-003`); a part whose stock name does
   not resolve surviving as an unresolved row; a part with no stock at all.
5. **CSV** (`Napkin.Modules.Furniture`). `ToCsv(rows)` over the same rows, with a test that parses
   the CSV back and asserts it equals the rows in the same order.
6. **UI** (`Napkin.App`). A sortable cut-list window; the picker of issue #7 — one icon per
   `MaterialsLibrary.Categories` entry, a text list from `InCategory(category)`, and
   `StockItem.HoverText` on hover, which already renders "2x4 — actual 1 1/2″ x 3 1/2″, PS 20-20";
   a properties panel for species. `GUI-CUT-01`, `GUI-CUT-02`, `GUI-CUT-03`.

Land 1–5 before 6. Steps 1–5 are a runnable, testable slice with no GUI in it, which is what
CLAUDE.md's "core functionality first" asks for.

### #9 — the shopping list and takeoff

7. **Aggregation** (`Napkin.Modules.Furniture`). `ShoppingList(cutRows, library)` exactly as §4,
   including all three honest refusals: no stocked length holds this piece; no stock-length list
   has been read for this size; sheets are a floor because nesting is a later issue.
8. **Takeoff.** Board feet in `Int128` per §4.1, rounded once at the end. Add the three-factor
   helper to `Napkin.Core.Geometry` beside `Area` if it wants a home there.
9. **Fixture.** The hand-computed materials list, once §8.3 is decided. The 1x4 aprons are the
   "several parts from one board" case.
10. **CSV and UI.** The same shape as #8's. `GUI-CUT-04`.

---

## 8. Decisions for Marc

1. **The `formatVersion` 1 → 2 bump.** It refuses every version-1 file, including both committed
   samples until step 3 updates them, and any project anyone has saved. That is beta policy working
   as designed, but it breaks committed artefacts and is worth saying out loud before it lands.
2. **Two in-plan dimensions plus one out-of-plane, named by `planAxes`** (§1.1), rather than three
   stored dimensions. It stores nothing twice, which is what keeps `CUT-002` true by construction,
   but it means the file cannot be read as a cut list without understanding `planAxes`. The
   alternative — store all three and have the reader refuse a file where they disagree with the box
   — is more redundant and more self-checking.
3. **Which fixture gets stock references** (§6.2): grow `coffee-table` to carry them, or write a
   second, more ordinary fixture for #9. Blocks `CUT-005`.
4. **Dry sizes only.** The library carries PS 20-20 Table 3's Dry columns, not its Green ones. A
   green 2x4 is 1 9/16″ × 3 9/16″, and someone building an outdoor structure from green lumber
   would want that. Adding it is a second pair of lengths on every lumber row plus a choice in the
   UI; it is not in this branch.
5. **No saw kerf and no joinery allowance** (§1.3, §4). Both are real and both are absent. The cut
   list and the shopping list say so on the table and in the CSV header rather than being quietly
   optimistic.
