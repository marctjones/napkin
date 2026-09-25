# Renovation sketches: existing, new and demolished, rooms, and the area takeoff

Status: **DRAFT — awaiting Marc's sign-off.** Implementation of the slices in §11 (#160–#164) is not
authorized until signed off. Milestone **M8 Renovation**.

Design note written by Fable per [`PLAN.md`](../../PLAN.md), for Marc's ask (2026-09-25): napkin
sketches of additions and renovations, "like adding in a window or finishing an unfinished room",
kept as simple as a DIY person's paper sketch. It decides how a drawing says what is already there,
what is going in and what is coming out; how walls get corners, rooms and a bearing/not-bearing
answer; how the lists respect that; and how a room's finishes are taken off by area. The two worked
examples throughout are **finishing a 12 × 14 ft unfinished basement room** (§2) and **adding a
3 ft window to an existing 12 ft exterior wall** (§3); the test plan (§10) is hand-derived from
them.

Settled decisions taken as given and not re-argued: a wall is a box on the layer **Wall** and an
opening a box on **Opening**, bound to its wall by relationships ([`../building.md`](../building.md));
the frame is derived from the drawing every time and never stored (`FramingList.Of`); the code
check and the bracing check are derived the same way (`CodeCheck.Of`, `BracingCheck.Of`); exact
lengths on the 1/1024-inch grid and no floating point anywhere ([`geometry-model.md`](./geometry-model.md)
§1); the cut list and the shopping list consume rows, and the shopping list rounds once at the end
([`parts-and-cut-list.md`](./parts-and-cut-list.md) §4.1); the file format has no optional fields
and no migration ([`../file-format.md`](../file-format.md)); everything is on the napkin sheet with
the carpenter's pencil ([`napkin-look.md`](./napkin-look.md)).

**The data rule (CLAUDE.md) governs every number in this note.** No sheet size, coverage rate,
waste allowance, door size, ceiling height or stick length is stated here as a fact. Where a worked
example needs one it is the **builder's typed choice, labelled as such**, and napkin carries no
default for it, with two exceptions that are **napkin's own design defaults**, labelled and
editable: the flooring waste allowance (§5.4) and the phase a new entity starts in (§1.2). Gypsum
board is not in the materials library; adding cited rows is issue #155's, and nothing here waits
for it (§5.1).

---

## How to use it (the finished feature, in one place)

- **Say what is already there.** Draw the walls of the house as they are, select them and choose
  **Edit → Phase → Existing** (or the Part panel's Phase). They turn ghosted: faint pencil. Nothing
  existing is ever bought or cut.
- **Draw what goes in.** Every new wall, opening, room, part or note starts **New**, drawn solid,
  and that is what the cut list and the shopping list buy.
- **Cross out what comes out.** Select it and choose **Edit → Phase → Demolish**: it is drawn
  dashed with a cross through it, it takes no part in any check, and the shopping list's
  **Demolition** section counts it. There is no "modified": draw the old one demolished and the new
  one new.
- **Add a window to a wall you have.** The wall is Existing; **Draw → Window** on it makes a New
  opening. The Part panel shows the header, king and jack studs, sill and cripples as **new
  material** and the studs that come out under **Demolition**, and the code check and the bracing
  check run exactly as today on the wall as it will be.
- **Say what a wall is.** In the Part panel: **Side** exterior or interior, **Bearing** yes or no.
  Bearing walls get the code check as today (and Supports, as today). A wall marked not bearing gets
  no code check: you pick its openings' header yourself, and napkin says it is your choice.
- **Finish a room.** **Draw → Room**, drag or type its inside length and width, type the ceiling
  height in the panel's Depth, tick the finishes you want — drywall, insulation, paint, flooring,
  baseboard — and type the sheet size, coverage, waste and stick length you will actually buy. The
  shopping list's **Area takeoff** section does the arithmetic and rounds once per line.
- **Measured a real room?** Type the four wall lengths and, if you have them, the two diagonals in
  the room's panel; napkin notes "out of square by …" and does not try to model it.
- **Mark the rough-in.** **Draw → Note**, click, type "outlet", "switch", "light", "supply", "drain"
  or anything else; a symbol for the words it knows. Notes are counted, never modelled.

---

## 1. Simplicity rules — what napkin will not model

1. **No corner framing.** Two walls meeting at a corner are drawn clean (§4.1) and framed alone,
   as today; the extra corner studs, blocking and nailers are not counted. Said in the panel.
2. **No wall topology beyond a flush face.** A wall bounds a room when one of its long faces lies
   on the room's edge (§4.2). Nothing joins walls into a graph; there are no polygonal rooms.
3. **No "modified" phase.** Old thing demolished, new thing new: two entities.
4. **No structural meaning to Demolish.** Removing a wall marked bearing gets no beam, no post, no
   check, and the panel says so: "Removing a bearing wall needs an engineer; napkin does nothing
   here."
5. **No MEP.** Notes count words; no circuits, boxes, wire, pipe, fixtures or code.
6. **No exterior finishes**, no ceilings other than flat at the room's height, no sloped walls, no
   layout of sheets on walls. Sheets are counted **by area** and say so, exactly as panels are.
7. **No out-of-square geometry.** Typed measurements are compared and reported, never drawn.
8. **No demolition material list.** Demolition is a count of what comes out, not a dumpster size.

---

## 2. Worked example 1: finishing a 12 × 14 ft basement room

Every dimension here is the builder's typed choice. Inside dimensions **12'-0" × 14'-0"**, ceiling
**8'-0"**. Four **New** 2x4 stud walls stand inside the foundation (the foundation itself is not
drawn: it is not a wall napkin frames). Every wall is **Side: exterior** (it stands against
foundation, so the builder wants it insulated) and **Bearing: no**; the header the builder types
for its openings is **(2) 2x6**. Stud spacing 16" (the design default). One door and one window,
both New.

| Entity | Layer | Phase | Box (width × height × depth) | Anchor | Notes |
|---|---|---|---|---|---|
| Wall, south | Wall | New | 14'-7" × 3 1/2" × 8'-0" | (0, 0, 0) | runs full, 175 in |
| Wall, north | Wall | New | 14'-7" × 3 1/2" × 8'-0" | (0, 12'-3 1/2", 0) | runs full |
| Wall, west | Wall | New | 12'-0" × 3 1/2" × 8'-0" | turned a quarter, x 0..3 1/2", y 3 1/2"..12'-3 1/2" | butts between |
| Wall, east | Wall | New | 12'-0" × 3 1/2" × 8'-0" | turned, x 14'-3 1/2"..14'-7" | butts between |
| Room | Room | New | 14'-0" × 12'-0" × 8'-0" | (3 1/2", 3 1/2", 0) | inside faces on its edges |
| Door | Opening | New | 3'-0" × 3 1/2" × 6'-8", sill 0 | 5'-0" along the south wall | |
| Window | Opening | New | 3'-0" × 3 1/2" × 2'-0", sill 4'-0" | 4'-6" along the west wall (centred) | |
| Notes | Notes | New | — | | "outlet" × 4, "switch" × 1, "light" × 1 |

Finishes ticked on the room, with the builder's typed values: drywall on walls and ceiling,
sheet **4' × 8'**; insulation **by area** on exterior walls, bag covers **40 sq ft**; paint, **2**
coats, can covers **350 sq ft per gallon**; flooring, waste **10 %** (napkin's design default),
box covers **20 sq ft**; baseboard, stick **8'-0"**.

The hand-derived frame, takeoff and shopping list are in §10.2.

## 3. Worked example 2: a 3 ft window in an existing 12 ft exterior wall

One 2x4 wall, **12'-0" long, 8'-0" tall, Existing, Side: exterior, Bearing: yes**, Supports typed
from the adopted code's table. A **New** window, **3'-0" wide × 4'-0" tall, sill 3'-0"**, centred
(4'-6" along). No other openings. What is new material, what comes out, and what the checks say are
in §10.3.

---

## 4. Walls as first-class

### 4.1 Corners and joins

Two walls **join** when a box end of one lies on a long face of the other, or two ends meet, with
their thicknesses overlapping — decided from the boxes' exact corners, no tolerance. The plan draws
a joined pair with **no seam line** between them (the shared edge is not stroked), which is the
whole of what a corner is here. Each wall is still framed alone (§1.1) and each is its own braced
line, as today. Openings cannot be placed across a join: an opening whose extent reaches into the
joined wall's thickness is refused with "reaches the corner with Wall 2".

### 4.2 Rooms

A **room is a box on the layer Room** (or called "Room"), as a wall is a box on Wall: its plan
width and height are the inside length and width, its depth the ceiling height. **Draw → Room**
drags one out or, after one click, takes typed length and width the way any dimension is typed
(M2), and starts at the same 8'-0" a wall starts at — a starting value, not a standard. A room is
never framed, never cut, never bought as a box; it carries the finishes (§5).

**Draw → Room snaps its edges to wall faces** through the flush snap M2 already has, so a room
dragged or typed inside four walls lands exactly on their inside faces; the rule below has no
tolerance, and a room that misses a wall by 1/1024" reads "not bounded by Wall 2 — snap it to the
wall" in the panel (risk §12.4's stance). A room is a rectangle the person draws, never something
napkin finds from the walls around it (§1.2, §13.11).

A wall **bounds** a room when one of the wall's long faces lies on the line of a room edge and the
wall's extent along that edge overlaps the edge's extent. The bounding walls' openings whose extent
along the edge lies within the room's edge are the room's openings. A room with no bounding wall
still has a takeoff — its own perimeter × ceiling height — with the note "no walls bound this room;
openings are not subtracted".

### 4.3 Wall types: side, bearing, and the header you choose

A wall's stud size is its thickness, as today (2x4 or 2x6 by the library's dressed width). Two new
per-wall inputs, both **entered, never defaulted** (`null` = not said):

| Input | Values | What it changes |
|---|---|---|
| **Side** | exterior, interior | which header table the code check asks for; insulation "on exterior walls" (§5.2) |
| **Bearing** | yes, no | whether the code check runs at all |

And one per-wall choice, **Header**, offered only when Bearing is no: plies (1–3) and a library
lumber (the picker lists dimension lumber), labelled everywhere "your choice, not a code result".
It is per wall, not per opening, because a not-bearing wall's openings all get the same header in a
DIY sketch and the opening box carries no inputs block; if two different headers are wanted, draw
two walls.

**The code check, routed** (`CodeCheck.For`), for an opening in a wall:

| Side | Bearing | What napkin does |
|---|---|---|
| exterior | yes | asks the pack's exterior-bearing header table, exactly as today (Supports required) |
| interior | yes | asks the pack's interior-bearing header table (`wallKind: interior-bearing`, already in the pack format); a pack without one is **No data**, saying which table |
| any | no | **Not checked**: "Wall 1 is marked not bearing, so napkin does not size this header from the code. Header: (2) 2x6, your choice." The frame uses the typed header with **1 jack and 1 king each side**, said to be napkin's placeholder counts, not a code result; no header chosen → the header buys nothing and says so, as today |
| not said | any | **Input missing**: "Say whether Wall 1 is exterior or interior (Part panel)" |
| any | not said | **Input missing**: "Say whether Wall 1 is bearing (Part panel)" |

`HeaderResult` gains no member: "Not checked" is a building-module state (`OpeningCheck.Result`
becomes nullable with a `NotChecked` reason string), so the rules engine stays a pure lookup. The
bracing check keeps running for **every** wall as today, whatever its side or bearing; whether an
interior wall is a braced wall line is a question for the adopted text, not this note (§12, §13.5).

### 4.4 Nothing in the wall's frame is a fact about a real wall

The frame of an **Existing** wall is napkin's regular layout, not a survey. When a new opening
"removes" three studs, that is three of napkin's layout studs; the real wall may have four, or a
post. The panel says "assuming a regular 16" layout in the existing wall" whenever a demolition
count comes from an existing wall (§6.3).

---

## 5. The area takeoff, stated exactly

`AreaTakeoff.Of(sketch, room) -> TakeoffLine[]`, in `Napkin.Modules.Building`, for each **New**
room (an Existing room is an outline and a name; a Demolished room is ignored). All arithmetic is in
exact square units (`Area`, 1024² per square inch, `Int128`), every product and quotient an exact
rational, and **each line rounds once, at its end**: up to a whole count, or to one decimal place of
square feet for an area shown alone.

With `L`, `W` the room's inside sizes, `H` its ceiling height, `P = 2(L + W)`, and `openings` the
room's openings (§4.2) in the **after** sketch (§6.1):

| Quantity | Exactly |
|---|---|
| `wallGross` | `P × H` |
| `openingArea` | Σ over openings of `width × height` |
| `wallNet` | `wallGross − openingArea` |
| `ceiling` | `L × W` |
| `doorWidths` | Σ over openings with sill 0 of `width` |

### 5.1 Drywall

`on`: walls and ceiling, walls only, or none. Area = `wallNet` (+ `ceiling`). Sheet size is
**typed** (width × length, exact lengths). Sheets = ⌈area ÷ (sheetWidth × sheetLength)⌉, **one
pool** for walls and ceiling (offcuts cross between them), labelled "sheets by area — a layout may
need more", the panel's own words. No sheet size typed → the area is shown and nothing is counted:
"type the sheet size you will buy". When #155 lands cited gypsum rows, a **Cited sizes…** list
beside the boxes fills them, exactly as brads do; the boxes stay the source of truth.

### 5.2 Insulation

`on`: exterior walls (bounding walls with Side exterior), all walls, or none. **By area**: the net
area of those walls' share of `wallNet` (each bounding wall's edge length × H, minus its openings).
**By stud bays**: for each of those walls that napkin frames, the count of full-height bays — the
gaps between adjacent full-height members (studs and king studs) along the wall, minus one per
opening (the gap between an opening's kings is the opening) — and the bay height `H − 3t`; the odd
bays at ends and beside openings are not sized, and the line says "N bays, H tall, at 16" o.c.;
buy by area for square feet". A wall napkin does not frame (an Existing wall of an unframed
thickness) contributes nothing by bays and the line says so. Bags = ⌈area ÷ typed coverage⌉ when a
coverage is typed; otherwise area only.

### 5.3 Paint

Area = `wallNet + ceiling` (walls only is a choice), × typed coats. Gallons = ⌈that ÷ typed
coverage per gallon⌉ when typed; otherwise the area to cover, in square feet, and "type the
coverage from your can".

### 5.4 Flooring

Area = `ceiling × (100 + waste) / 100`, waste a whole percent, **napkin's design default 10 %**,
editable and labelled "napkin's allowance, not a fact about your floor". Shown rounded **up** to a
whole square foot; boxes = ⌈unrounded area ÷ typed box coverage⌉ when typed.

### 5.5 Baseboard

Linear = `P − doorWidths`. Shown in feet and inches exactly. Sticks = ⌈linear ÷ typed stick
length⌉ when typed, "not allowing for corners or waste"; otherwise the length and "napkin has read
no stock-length list for trim", the shopping list's own words for a length it will not guess.

### 5.6 Measured rooms and out-of-square

The room's panel takes, optionally, the **four measured wall lengths** and the **two diagonals**.
If the four lengths disagree with the box (opposite sides unequal) the panel says "measured
south 14'-0", north 14'-1": out of square; drawn as 14'-0"". If two diagonals are typed and unequal
the panel says "diagonals differ by 1 1/2": out of square". If one diagonal is typed, its square is
compared to `L² + W²` in `Int128` (no square roots, no floating point): "diagonal 18'-5" measures
long for 12'-0" × 14'-0"". The takeoff uses the **drawn** box and says "from the drawn size". Nothing
is redrawn. Saved with the room (§7).

---

## 6. Phase: existing, new, demolish

### 6.1 One field on every entity

Every entity — box, dimension, node, segment, note — has a **phase**: `existing`, `new` or
`demolish`. A new entity starts **New** (napkin's default). **Edit → Phase → Existing / New /
Demolish** sets the selection's phase in one undo step, and the Part panel has the same picker.
Setting a wall's phase does **not** change its openings' (a new window in an existing wall is the
point); duplicating copies the phase.

Two derived views of a sketch, never stored:

- **after** = entities whose phase is Existing or New: the building as it will be.
- **before** = entities whose phase is Existing or Demolish: the building as it is.

A view drops the relationships that name a dropped entity (the file keeps them). **Every check
runs on `after`**: `CodeCheck.Of`, `BracingCheck.Of`, `WallLine.Of`, opening recognition. A
demolished opening is not in its wall's line; a demolished wall has no line.

### 6.2 What the lists list

| List | Rule |
|---|---|
| Cut list | rows for **New** parts only |
| Shopping list, boards and sheets | from those rows |
| Framing section | the **framing diff** (§6.3), new pieces only |
| Fasteners, glue | joints where **at least one** part is New |
| Hardware, supplies | hardware on New parts; supplies as typed |
| Area takeoff | New rooms (§5) |
| **Demolition** (new section) | Demolish parts by name and count; framing pieces that come out (§6.3); Demolish walls, openings and rooms by name |
| Notes (new section) | New notes, counted by symbol: "outlet × 4, switch × 1, light × 1, 2 other notes" |

The shopping list's header line says "Only what is New is listed; 3 items to remove are under
Demolition." A design with nothing Existing or Demolished reads exactly as today.

### 6.3 The framing diff

For each wall in `after`: `FramingList.Frame` on `after` gives the pieces as the wall will be;
`FramingList.Frame` on `before` (if the wall is there) gives the pieces as it is. Pieces are
compared by **(role, length, stock)**: quantities in `after` beyond `before` are **new material**
(bought through the framing section as today); quantities in `before` beyond `after` **come out**
(the Demolition section: "Wall 1: 2 studs 91 1/2" come out"). A New wall has no `before`: all new.
A Demolish wall has no `after`: all of it comes out. An Existing wall with no change: nothing on
either list. A window closed up (opening Demolish) lists the studs and plate piece that fill it as
new and the header, kings, jacks and cripples as coming out — nothing special-cased.

Every demolition count from an Existing wall carries §4.4's note.

### 6.4 Drawing styles (plan, 3D, standard views, parts view)

| Phase | Plan | 3D |
|---|---|---|
| New | as today: solid pencil; openings keep their dash (it reads as a hole) | as today |
| Existing | **ghosted**: the same strokes and fills at 40 % opacity; its dimensions likewise | 40 % opacity |
| Demolish | **long dash** (12/6 in the pencil colour, so it cannot be mistaken for an opening's short dash) and a diagonal **cross** corner to corner; a demolished opening is drawn the same, over its wall | long-dash edges, cross on the top face |

The status bar's entity line ends with the phase: "Wall 1, existing". No legend.

---

## 7. The file format: one bump, 8 → 9

Every version-8 file is refused, with no converter; every committed sample is rewritten in the same
change (slice A). The format keeps having no optional fields: every field below is written every
time.

**On every entity:** `"phase": "existing" | "new" | "demolish"`. Refused: any other value, or the
field missing.

**On a box that is a wall**, `wall` becomes `{ "supports", "studSpacing", "bracing", "side",
"bearing", "header" }`:

| Field | Type | Refused when |
|---|---|---|
| `side` | `"exterior"`, `"interior"` or `null` | other text |
| `bearing` | `true`, `false` or `null` | not a boolean or `null` |
| `header` | `null` or `{ "plies": 1–3, "lumber": text }` — the typed header for a not-bearing wall | plies outside 1–3, empty lumber (not checked against the library, as a part's stock is not) |

**On a box that is a room:** `"room": null` or

```json
"room": { "drywall": "walls-and-ceiling" | "walls" | "none",
          "sheet": { "width": <length>, "length": <length> } | null,
          "insulation": "exterior" | "all" | "none", "insulationBy": "area" | "bays",
          "insulationCoverage": <sq ft, integer> | null,
          "paint": "walls-and-ceiling" | "walls" | "none", "paintCoats": <integer ≥ 1> | null,
          "paintCoverage": <sq ft per gallon, integer> | null,
          "flooring": true | false, "flooringWaste": <whole percent ≥ 0>, "flooringBox": <sq ft> | null,
          "baseboard": true | false, "baseboardStick": <length> | null,
          "measured": { "south": <length> | null, "north": …, "east": …, "west": …,
                        "diagonal1": <length> | null, "diagonal2": <length> | null } }
```

(Values shown are the shape, not data.) Refused: an unknown enum text, a negative or zero coverage,
a zero-length sheet side, `room` on a box that also has `part` or `wall` other than `null`.

**A new entity type `note`:** `{ "id", "type": "note", "layer", "name", "phase", "position":
{ "x", "y" }, "text": <text>, "symbol": "none" | "outlet" | "switch" | "light" | "supply" |
"drain" }`. `text` may be empty when `symbol` is not `none`. A note has no size and no
relationships; it is styled by the layer **Notes** and, whatever its layer, drawn as a note.

**Layers by name**, as Wall and Opening are: **Room**, **Notes**.

**Expected files** (`samples/*.expected.json`) gain `phase` on each box row, `demolition`,
`areaTakeoff` and `notes` sections where the sample has them.

---

## 8. The UI, minimal and in napkin's conventions

- **Draw menu / toolbar:** **Room** and **Note** after Door, in that order. Proposed keys
  **Shift+W** (room) and **Shift+N** (note) — R, W, D, J, Shift+J, P, X and the command keys are
  taken; the implementer verifies no collision, and the menu shows whatever is chosen.
- **Edit → Phase ▸ Existing / New / Demolish**, radio-checked for a single selection, applying to
  the whole selection; one undo step ("Marked Wall 1 existing").
- **Part panel:** a **Phase** row on every entity. On a wall: **Side**, **Bearing**, and **Header**
  (only when Bearing is no), beside Supports and the spacing. On a room: the finishes block (§5)
  — ticks and typed boxes, mono 12.5 like every entry — and the measured lengths. On a note: text
  and a symbol row (six small pencil glyphs, the chosen one moss-washed). On an opening in a
  not-bearing wall, the code-check block reads "Not checked" with §4.3's sentence.
- **Shopping list window:** two new sections, **Area takeoff** (one line per finish, with the
  typed values it used and the per-line rounding said) and **Demolition**, and a **Notes** line;
  each in the CSV under its own header. Existing sections unchanged.
- **Canvas:** the styles of §6.4; a note draws as its glyph (or a small pencil circle) with its
  text beside it, at the plan's label size, and is hit-tested as a point with a small radius.
- **Message bar:** the framing diff is said with the edit that caused it: "Placed Window 1 in
  Wall 1 (existing): new — 2 king studs, 2 jack studs, header, sill, 4 cripples; out — 2 studs."

Nothing else moves. No new window, no wizard.

---

## 9. The user's flow (the storyboard for `GUI-RENO-04`)

1. New sheet. **W**, drag the south wall 14'-7"; Duplicate for the north; **W** dragging
   north–south for west and east, 12'-0" each. Type each length on its label.
2. Select all four; Part panel: Side exterior, Bearing no, Header (2) 2x6.
3. **Draw → Door** on the south wall; type 3'-0" on its label; Depth 6'-8". **Draw → Window** on
   the west wall; 3'-0", Depth 2'-0", Up 4'-0".
4. **Shift+W**, click inside, type 14'-0" and 12'-0"; the room snaps to the four inside faces
   (the panel names all four bounding walls); Depth 8'-0". Tick drywall, insulation,
   paint, flooring, baseboard; type 4' × 8', 40, 2, 350, 20, 8'-0".
5. **Shift+N** four times: "outlet"; once "switch"; once "light".
6. **Ctrl+Shift+L**: the Framing section (§10.2), Area takeoff (§10.2), Notes line; no Demolition.
7. Select the west wall, **Edit → Phase → Existing**: the framing section drops its studs and
   plates, keeps the window's kings, jacks, header, sill and cripples, and Demolition says "Wall,
   west: 2 studs 91 1/2" come out (assuming a regular 16" layout in the existing wall)". Undo.

---

## 10. Test plan

Feature ids: unit `BLD-006` onward; GUI `GUI-RENO-NN`. Expectations are hand-derived from §2 and §3
with a `derivation` string, never regenerated (`samples/README.md`). Values below in inches; the
file carries 1024ths (91 1/2" = 93696, 175" = 179200, 144" = 147456, 78 1/2" = 80384, 70 1/2" =
72192, 45" = 46080, 39" = 39936, 36" = 36864, 15 1/2" = 15872, 7 1/2" = 7680, 82 1/2" = 84480,
33" = 33792, 3 1/2" = 3584).

### 10.1 The samples

`samples/basement-room.{design.md,scene.json,expected.json}` — §2 exactly, formatVersion 9.
`samples/window-in-existing-wall.{…}` — §3; its code check is exercised under the synthetic header
pack the building tests already use (`tests/Napkin.Modules.Building.Tests/CodePacks`, NOT CODE
VALUES), whose exterior-bearing row for this request is arranged to answer **(2) 2x6, 1 jack and 1
king each side**; under the shipped Connecticut pack the panel says **No data** as today, and the
sample's `expected.json` records both.

### 10.2 Example 1, by hand

**Frame**, per [`../building.md`](../building.md) with `t` = 1 1/2", `s` = 16", stud length
`H − 3t` = 91 1/2":

- South wall, 175": layout studs at 0, 16, … 160 (eleven; 160 + 1 1/2 ≤ 175, 176 is not), end
  stud at 173 1/2: **12**. Door at `a` = 60, `w` = 36, `j = k = 1`: studs whose body touches
  [57, 99) — at 64, 80, 96 — are left out: **9 studs**, **2 king** (91 1/2"), **2 jack**
  (0 + 80 − 1 1/2 = **78 1/2"**), header **39"** long (36 + 2·1·1 1/2), room 96 − 3 − 80 = 13";
  the typed (2) 2x6 header (dressed width 5 1/2", the library's) leaves cripples above of
  13 − 5 1/2 = **7 1/2"** at 64, 80 and 96: **3**. No sill, no cripples below. Plates: one
  bottom, two top, 175" each.
- North wall, 175": **12 studs**, 3 plates.
- East wall, 144": studs at 0 … 128 (nine) plus the end stud at 142 1/2: **10 studs**, 3 plates.
- West wall, 144": window at `a` = 54, `w` = 36, sill 48, height 24: studs touching [51, 93) —
  64, 80 — out: **8 studs**, **2 king**, **2 jack** (48 + 24 − 1 1/2 = **70 1/2"**), header
  **39"**, room 96 − 3 − 72 = 21", cripples above 21 − 5 1/2 = **15 1/2"** at 64, 80: **2**;
  rough sill **36"**; cripples below 48 − 3 = **45"** at the positions wholly inside [54, 90):
  64, 80: **2**.

Pieces (2x4 unless said): 39 studs + 4 kings = **43 × 91 1/2"**; 2 × 78 1/2"; 2 × 70 1/2";
2 × 45"; 1 × 36"; 2 × 15 1/2"; 3 × 7 1/2"; plates **6 × 175"** and **6 × 144"**; headers **4 ×
39" 2x6** (two plies per opening, the builder's choice). 71 pieces of 2x4, 4 of 2x6.

**Boards** (first-fit decreasing over the library's 6'…16', 1/8" kerf, `parts-and-cut-list.md`
§4 — **to be re-derived by the implementer**; this is one hand pass): six 16' boards take the 175"
plates; six 12' take the 144" plates before the shorter pieces arrive, but three of them later
also take a 45", a 45" and the 36" (144 + 45 + 1/8 = 189 1/8 ≤ 192, so those three grow to 16');
21 boards take two studs each (183 1/8 ≤ 192) and a 22nd takes the 43rd stud with a 78 1/2" jack;
the second jack and a 70 1/2" share a board (149 1/8 → 14'); the last 70 1/2" is alone (→ 6');
the 15 1/2"s and 7 1/2"s ride on the plate boards' offcuts. **2x4: 31 × 16', 3 × 12', 1 × 14',
1 × 6' = 36 boards**, 552 lineal feet, **368.0 board feet** (2 × 4 ÷ 12 × 552, once, at the
end). **2x6: 1 × 14'** (4 × 39 + 3 × 1/8 = 156 3/8 ≤ 168), 14.0 board feet.

**Area takeoff:**

| Line | Exactly | Shown |
|---|---|---|
| walls gross | 2(12 + 14) × 8 = 416 sq ft | |
| openings | 3 × 6 2/3 + 3 × 2 = 20 + 6 = 26 sq ft | |
| walls net | 390 sq ft | |
| ceiling | 168 sq ft | |
| **Drywall** | 558 ÷ 32 = 17.4375 | **18 sheets** 4' × 8' (by area — a layout may need more) |
| **Insulation** | exterior walls, all four bound: 390 ÷ 40 = 9.75 | **390 sq ft; 10 bags** at 40 sq ft (typed) |
| — by bays, for reference | N 12 − 1 = 11; E 10 − 1 = 9; S (9 + 2) − 1 − 1 = 9; W (8 + 2) − 1 − 1 = 8 | 37 bays, 91 1/2" tall |
| **Paint** | 558 × 2 = 1116 ÷ 350 = 3.19 | **1116 sq ft to cover; 4 gallons** at 350 (typed) |
| **Flooring** | 168 × 110/100 = 184.8; ÷ 20 = 9.24 | **185 sq ft** (10 % allowance); **10 boxes** at 20 (typed) |
| **Baseboard** | 52 − 3 = 49 ft; ÷ 8 = 6.125 | **49'-0"; 7 sticks** of 8' (typed; no corners or waste) |
| Notes | | outlet × 4, switch × 1, light × 1 |
| Demolition | | nothing |

Two pools for drywall would give 13 + 6 = 19; §13.3.

### 10.3 Example 2, by hand

`before` (Existing wall, no openings): 10 studs 91 1/2", 3 plates 144". `after`: studs touching
[51, 93) — 64, 80 — out: 8 studs; 2 kings 91 1/2"; 2 jacks 36 + 48 − 1 1/2 = **82 1/2"**; header
39" (2 plies 2x6 under the synthetic pack), room 96 − 3 − 84 = 9", cripples above 9 − 5 1/2 =
**3 1/2"** at 64, 80: 2; sill 36"; cripples below 36 − 3 = **33"** at 64, 80: 2.

**Diff:** new — 2 × 91 1/2" (kings), 2 × 82 1/2", 1 × 36", 2 × 33", 2 × 3 1/2", header 2 × 39"
2x6; out — **2 studs 91 1/2"** ("assuming a regular 16" layout in the existing wall"). Plates:
unchanged, nothing. Boards (to be re-derived): 2x4 1 × 16' (two kings and both 3 1/2"s), 1 × 14'
(two jacks), 1 × 10' (36 + 33 + 33 + kerfs = 102 1/4); 2x6 1 × 8' (78 1/8).

**Checks on `after`:** header as the synthetic pack says, cited; bracing: segments "wall start to
Window 1" 4'-6" and "Window 1 to wall end" 4'-6" (54" each), methods as the person picks, result
per the synthetic bracing pack. Under the shipped CT pack: header **No data**, bracing **No data**,
verbatim as today. With Bearing switched to **no**: "Not checked … your choice"; with Bearing not
said: "Say whether Wall 1 is bearing".

### 10.4 Golden cases (unit)

Project (`Napkin.Core.Project.Tests`):
1. Format 9 round-trips `phase`, `wall.side/bearing/header`, `room`, `note`; each refusal of §7
   is hit by one malformed file naming the field; a version-8 file is refused naming both versions.

Geometry (`Napkin.Core.Geometry.Tests`):
2. `Sketch.After()` / `Before()` drop the right entities and the relationships that name them; the
   sketch itself is unchanged; a `note` survives every exhaustive switch (binder, writer, updater,
   checker, validate) — a reflection test lists the switches.

Building (`Napkin.Modules.Building.Tests`):
3. `BLD-006` join: two walls at a corner join; offset by 1/1024" they do not; an opening reaching
   a join is refused.
4. `BLD-007` a wall bounds a room by a flush face; its openings inside the edge are the room's;
   one straddling the edge's end is not.
5. `BLD-008` code-check routing: the five rows of §4.3, each with its sentence.
6. `BLD-009` the framing diff: example 2's new and out lists; a New wall all new; a Demolish wall
   all out; a closed-up window; an Existing untouched wall nothing.
7. `BLD-010` the takeoff: every line of §10.2, exactly, including the one-pool sheet count, the
   waste as an exact rational and the ceiling rounded up once; no typed coverage → area only.
8. `BLD-011` out-of-square: unequal opposite sides, unequal diagonals, one diagonal compared in
   `Int128`; a true 3-4-5 room (12 × 16, diagonal 20) is not flagged.
9. `BLD-012` bays: example 1's 37.
10. The two samples, row for row, against their `expected.json`.

Furniture (`Napkin.Modules.Furniture.Tests`):
11. Cut list lists New parts only; a joint with one New part counts its fasteners; a Demolish part
    is in the demolition rows with its count.

### 10.5 GUI workflows (`[GuiWorkflow]`, ≥ 5 actions, keyboard and pointer)

- `GUI-RENO-01` draw a wall, mark it Existing by menu and by panel, place a window; the panel's
  new/out lines; undo restores the phase and the lines.
- `GUI-RENO-02` a room by Shift+W with typed sizes; tick finishes, type values; the Area takeoff
  section and its CSV.
- `GUI-RENO-03` notes by Shift+N with keyboard text and pointer symbol; the Notes line; Delete.
- `GUI-RENO-04` §9 end to end, asserting §10.2's board count, sheet count and the Demolition
  sentence after step 7.

---

## 11. Implementation slices

Each lands alone on `main` in this order, with local build, test and `ratchet check`. Files are
disjoint between slices except the recurring collision points (`napkin.sln`,
`Directory.Build.props`, `ratchet/baseline.json`, `PlannedFeatures.g.cs`). Slice A rewrites every
`samples/*.json` version field — land it first and alone.

| Slice | What | Model | Files | Depends on |
|---|---|---|---|---|
| **A** (#160) | `Phase` on `Entity`; `Note` entity; `WallInputs` side/bearing/header; `RoomInputs`; `formatVersion` 9 with every refusal of §7; `Sketch.After()`/`Before()`; the `note` arms in every switch; **Edit → Phase**, the panel's Phase row, the three drawing styles in plan and 3D; cut list New-only; the Demolition section for parts; bump every sample; `docs/file-format.md`; tests 1, 2, 11 | Sonnet | `Napkin.Core.Geometry/Entity.cs`, `Sketch.cs`, new `Note.cs`, `Propagator.cs`, `DirectUpdater.cs`, `RelationshipChecker.cs`; `Napkin.Core.Project/*`; `Napkin.Modules.Furniture/CutList.cs`; `Napkin.App` (Edit menu, panel row, `CanvasPalette`, `CanvasView`, `ModelView`, shopping-list window's Demolition section); `samples/*.json`; `docs/file-format.md` | — |
| **B** (#161) | Corners (§4.1) and the seamless join in plan; code-check routing (§4.3) and the typed header in `FramingOptions`; the framing diff (§6.3) and its Demolition rows; §4.4's note; the message-bar sentence; `samples/window-in-existing-wall`; the panel's Side/Bearing/Header; tests 3, 5, 6, 10 (example 2) | **Opus** (three modules' switches meet here and the diff is easy to get subtly wrong) | `Napkin.Modules.Building/Wall.cs`, `FramingList.cs`, `CodeCheck.cs`, new `WallJoin.cs`, new `FramingDiff.cs`; `Napkin.App/MainWindow.Building.cs`, `CutListWindow`; `docs/building.md` | A |
| **C** (#162) | `Room` reading, bounding walls (§4.2), `AreaTakeoff.Of` (§5), out-of-square (§5.6), bays; **Draw → Room** and the room panel block; the Area takeoff section and CSV; `samples/basement-room`; tests 4, 7, 8, 9, 10 (example 1) | Sonnet | new `Napkin.Modules.Building/Room.cs`, `AreaTakeoff.cs`, `TakeoffLine.cs`; `Napkin.App/Editing/RoomTool.cs`, `MainWindow.Building.cs`, `CutListWindow`; `docs/building.md` | A |
| **D** (#163) | **Draw → Note**, the six glyphs, hit-testing, the note panel, the Notes line and CSV | Sonnet | `Napkin.App/Editing/NoteTool.cs`, `Viewing/*`, `CutListWindow` | A |
| **E** (#164) | `GUI-RENO-01…04`; `features/catalog.json` entries `BLD-006…012` and the four GUI ids; scorecard stubs; the `docs/building.md` "How to use it" | Sonnet | `tests/Napkin.App.GuiTests/*`, `features/*.json`, `PlannedFeatures.g.cs` | B, C, D |
| with #155 | Cited gypsum rows and the **Cited sizes…** list beside the sheet boxes; only if the primary source can be fetched and read | Sonnet, data rule strictly | `Napkin.Core.Materials/data/sheet-goods-gypsum.json`, `CutListWindow` | C |

---

## 12. Risks and unknowns

1. **A fifth entity type.** `note` hits every exhaustive switch over `Entity` — binder, writer,
   validate, updater, checker, canvas, hit-test, 3D, standard views, parts view. Miss one and a
   single note freezes editing or crashes a view. Test 2's reflection list is slice A's gate, and A
   must not land without it (the joinery note's risk #1, again).
2. **Sample churn.** The bump touches every `samples/*.json`; slice A pays it once, alone.
3. **The framing diff on an Existing wall is a guess about the real wall** (§4.4). The note is
   always shown; if it is not enough, the alternative is to show no demolition count for Existing
   walls at all (§13.4).
4. **Corner detection with no tolerance.** Two walls that "look" joined but sit 1/1024" apart are
   two walls with a seam. Snapping already lands boxes flush, so this is the geometry model's
   existing stance, not a new one; the panel says "not joined" for a near miss.
5. **Bays are ill-defined beside openings and at ends.** §5.2 counts and says what it does not
   size; if Marc wants a square-foot figure by bays, it would need a rule this note declines to
   invent.
6. **Interior bearing tables.** Routing to `interior-bearing` is only as good as the pack; the
   shipped CT pack has neither table, so every check is No data until #157's transcription lands.
7. **Panel crowding.** A wall's panel now has Supports, spacing, Side, Bearing, Header, Bracing
   and Phase. If it grows past a screen, the bracing block folds; not a model risk.
8. **"Not checked" as a nullable result.** A UI arm that treats null as "no data" would hide the
   typed-header sentence; test 5 pins each of the five sentences.
9. **Waste and coverage typed as whole numbers.** A coverage of 37.5 sq ft per bag cannot be
   typed; whole numbers keep the file exact. If it matters, a 1/1024 sq-ft grid is the same
   mechanism as lengths.

---

## 13. Decisions for Marc

Each in plain words, with the default I recommend, so a "yes" is enough.

1. **How do you mark what's already there?** Recommended: every drawn thing has one of three
   tags — *existing* (drawn faint), *new* (drawn solid), *demolish* (dashed with a cross) — set
   from the Edit menu or the side panel, and there is no "changed" tag: you draw the old one as
   demolish and the new one as new, like crossing out on paper. Only *new* things are bought;
   *demolish* things are counted under a Demolition heading. The alternative is a separate
   "existing" drawing layer, which is harder to explain and cannot say what comes out.
2. **Should napkin tell you which studs come out of an old wall when you cut a window in it?**
   Recommended: yes, with the words "assuming a regular 16-inch layout in the existing wall",
   because napkin has never seen inside your wall. The alternative is to show only the new lumber
   and say nothing about what comes out.
3. **Drywall sheets: one count for walls and ceiling together, or two?** Recommended: one count
   (18 sheets in the example, not 19), because offcuts from the ceiling get used on the walls. Two
   separate counts is more cautious and buys one more sheet.
4. **A wall you say is not load-bearing: no code check, and you pick the header yourself?**
   Recommended: yes — napkin says "your choice, not a code result" and buys what you picked. The
   alternative is to check every wall against the code as it does today, which would ask you for
   snow and wind loads for a closet wall.
5. **Should the bracing check keep running on every wall, including interior ones?** Recommended:
   yes, as today, until someone reads what the adopted code says about interior walls; a wall you
   do not care about just says "not braced". The alternative — skip interior walls — would be
   napkin claiming something about the code it has not read.
6. **A default 10 % extra for flooring waste?** Recommended: yes, shown as "napkin's allowance",
   and you can type any other number including 0. The alternative is no default, so flooring
   shows no number until you type an allowance.
7. **Sheet size, paint coverage, insulation coverage, flooring box coverage and baseboard stick
   length: typed by you from the package, not built in?** Recommended: yes, because napkin does
   not have a checked source for any of them (gypsum sheet sizes may come later under #155, and
   then a "Cited sizes…" list fills the boxes for you). The alternative is napkin guessing typical
   values, which the data rule forbids.
8. **Notes for outlets, switches, lights and pipes are just words with a small symbol, counted
   but never wired or plumbed?** Recommended: yes. Anything more is an electrical or plumbing
   feature of its own.
9. **The header you type for a not-bearing wall applies to every opening in that wall, not each
   opening separately?** Recommended: per wall — one choice, fewer boxes; draw two walls if you
   want two headers.
10. **Corners: draw them clean, but do not add corner studs to the count?** Recommended: yes for
    now; the extra corner and blocking pieces are a small follow-up once a real framing text is
    read for them.
11. **A room is a rectangle you draw, not something napkin works out from the walls around it?**
    Recommended: draw it — it snaps to the walls' inside faces, and napkin then knows which walls
    and openings belong to it. Working rooms out from the walls would need napkin to understand how
    walls connect up, which this note deliberately keeps out (§1.2), and it would give the same
    answer for any room a DIY sketch has.
