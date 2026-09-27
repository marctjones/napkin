# Deck and three-season porch: a deck on piers, light walls on it, and a shed roof to the house

Status: **Signed off by Marc 2026-09-26 with the recommended §13 decisions** (in chat: "Continue implementing napkin using reasonable recommendations"; recorded on #187). Scene format is **13**, not 11: #189 took 11 and #140 took 12. Milestone **M11 Deck and three-season porch**
(umbrella #20, porch #187, research #188; the four deck pieces #40–#43 stay open as the real,
cited data behind slices D–F). Slices A–J are #195–#204 (§10).

> **Format-version coordination (2026-09-25):** `main` is at scene format **10** (M8's, #160);
> #162 does not bump it. This note takes **11**. If another note lands a bump first (M9 Shape
> may), slice A takes the next free number and this banner is updated — the number is confirmed
> when slice A lands, not here.

Design note written by Fable per [`PLAN.md`](../../PLAN.md), for Marc's ask (2026-09-25): **no full
additions; a deck is enough, plus a three-season porch without a full foundation.** It decides what
a deck is in napkin, how its frame, decking, guards and stairs are derived and bought, how its four
code checks run through the rules engine, how a porch is enclosed with the wall tool napkin already
has, and the one genuinely new structure, a **shed roof** from a ledger on the house to the porch's
front wall. The worked example throughout is a **12 × 10 ft deck, 3 ft above grade, on an existing
house wall, then enclosed as a three-season porch under a 5-in-12 shed roof** (§9); the test plan
(§11) is hand-derived from it.

Settled decisions taken as given and not re-argued: a wall is a box on the layer **Wall** and an
opening a box on **Opening**, bound to its wall by relationships, and the frame, the code check and
the bracing check are derived every time and never stored ([`../building.md`](../building.md));
every entity has a phase and every check runs on the building as it will be
([`renovation-sketches.md`](./renovation-sketches.md) §6); exact lengths on the 1/1024-inch grid
and no floating point anywhere ([`geometry-model.md`](./geometry-model.md) §1); the cut list and the
shopping list consume rows and round once at the end ([`parts-and-cut-list.md`](./parts-and-cut-list.md)
§4.1); a code result is exactly one of sized / out of scope / input missing / no data, cited, and
nothing is defaulted ([`rules-engine-model.md`](./rules-engine-model.md) §3); the rules engine ships
**no real deck or roof tables** — the mechanism is proven on synthetic packs
(`tests/Napkin.Modules.Building.Tests/CodePacks`, `SYNTHETIC TEST DATA - NOT CODE VALUES`) and real,
cited tables are **M10**'s (#158); the file format has no optional fields and no migration
([`../file-format.md`](../file-format.md)).

**The data rule (CLAUDE.md) governs every number in this note.** No guard height, opening limit,
riser height, tread depth, ledger bolt spacing, span, footing size, notch limit, cantilever ratio or
snow load is stated here as a fact. Each is one of three things, always labelled: the **person's
typed choice**; a **pack row** (synthetic in every test, No data under the shipped Connecticut
pack); or **napkin's own design default** (a spacing, a gap, an allowance), editable and said to be
napkin's. The only code facts cited are the ones [`../research/porch-rules.md`](../research/porch-rules.md)
read from the primary source on 2026-09-25: the 42" frost line (CT Table R301.2, p. 131), the 40 %
sunroom definition and its categories (IRC 2021 §R202/§N1101.6, §R301.2.1.1.1, via UpCodes), the
frost exception for decks *not* supported by a dwelling and the 12" stair-termination footing
(CT §R403.1.4.1 exceptions 3 and 4, p. 145), and the porch/deck exemption from whole-house alarm
retrofits (CT §R314.2.2 / §R315.2.2 exception 1, p. 139). That research **did not read IRC §R507**
(the deck chapter) — every deck provision below is a table kind the pack may carry, never a value.

---

## How to use it (the finished feature, in one place)

- **Draw the house wall you are building against** and mark it Existing (M8). Nothing existing is
  bought.
- **Draw → Deck** (**Shift+D**): drag the deck's outline out from that wall; its edge snaps flush to
  the wall's face and that edge is the **ledger** side. Type its height above grade in the panel's
  Depth. Choose the joist direction, spacing, and the lumber for joists, beam, posts and decking;
  type how many posts. The panel shows the frame in one line; the shopping list's **Deck** section
  lists ledger, joists, rim, blocking, beam, posts, decking boards and the hardware you typed.
- **Code checks on the deck** run in the panel as they do on an opening: ledger fastening, joist
  span, beam span, footing size, frost depth, guard, stair — each *sized/passes*, *out of scope*,
  *input missing* or *no data*, with the citation. With the shipped Connecticut pack every one but
  frost is **No data**, and says so. Type the soil bearing value and the species in **Project →
  Adopted code and site**; napkin never guesses either.
- **Guards and stairs:** tick Guard and type its height, post spacing and the gap you want between
  balusters; tick Stair, say which edge and where, type its width and tread run. The pieces land in
  the Deck section; the checks compare your numbers to the pack's.
- **Enclose it:** draw walls on the deck with **W** as today (they stand on the decking), put
  windows in with **Draw → Window**, and set an opening's **Fill** to *glass*, *screen* or *solid*.
  The front wall carries the roof: mark it Bearing, and say what the deck supports.
- **Draw → Porch roof** (**Shift+R**): click the deck; the roof takes its outline. Type the pitch
  as "5 in 12", the overhang, the rafter spacing and lumber, the sheathing and roofing you will buy.
  The panel says each rafter's length, where to cut the plumb and seat cuts in plain words, the
  rafter count, the ledger and its height on the house wall, the sheathing sheets and the roofing
  area; the **Roof** section buys them. The rafter check is a code lookup like the others.
- **Sunroom or not:** the roof panel shows the glazing percentage of the porch's walls and roof
  and whether it is over the 40 % line, with the citation. It is arithmetic on your drawing, not a
  ruling.

---

## 1. Simplicity rules — what napkin will not model

1. **No full additions.** No foundation walls, no slab, no conditioned space. A deck on piers, and
   a porch on the deck. (Marc, 2026-09-25.)
2. **Rectangles only.** A deck is one rectangle against one wall. No L-shaped decks, no wrap-around,
   no angled corners, no hot-tub cut-out (the DESIGN.md §3 hot-tub deck is a rectangle with the tub
   drawn as a part on it).
3. **One roof shape: a shed roof.** No hip, gable, valley, dormer, ridge or overhang at the rakes.
   The rafters run from the house down to the front wall; the end rafters sit flush with the side
   walls.
4. **The triangle above each side wall, under the sloping rafters, is reported, not framed.**
   napkin's walls are rectangles ([`../building.md`](../building.md)); the rake fill above a side
   wall is a triangle whose size and area the roof panel states (§5.6), and which the sunroom
   arithmetic counts as gross wall. Framing it (studs of stepping lengths) is a later slice, listed
   in §10 as optional.
5. **No attached-structure lateral design.** No lateral-load connection between deck and house, no
   diagonal bracing of posts, no hold-downs; the porch walls get the bracing check they get today,
   each wall its own line. When a pack carries a provision that says a lateral connection is
   required, it is shown as a `not-encoded` footnote with the ledger result — text, never a design.
6. **No insulation, no HVAC, no electrical beyond M8's notes.** A three-season porch is
   unconditioned (research §1: categories I–III); a Room may be drawn on the deck for paint and
   ceiling drywall if wanted, and nothing here changes the takeoff.
7. **No flashing, drip edge, fascia, gutters, soffit, ridge or wall flashing as parts.** Where a
   pack's ledger provision names flashing, the text rides with the result as a footnote.
8. **No joist hangers, post bases or caps derived.** They are hardware the person types on the
   deck, counted as hardware is today (joinery §7.5); napkin says how many joists there are so the
   count is easy.
9. **No freestanding decks.** A deck edge must lie on an Existing wall's face. A deck touching no
   wall is refused in the panel ("draw the deck against an existing wall") — the frost-depth
   exception for decks not supported by a dwelling (CT §R403.1.4.1 exc. 3) is therefore never
   offered, because a ledger-attached deck *is* supported by the dwelling.
10. **Nothing structural is sized by napkin.** Every member is the person's typed lumber, and every
    check compares that choice to the pack's row. napkin never changes lumber under the person.
11. **Notches, birdsmouth depth limits, bearing lengths, splice rules, cantilever limits: not
    checked** unless the pack declares them as limits, exactly as bracing's `limits` are declared.
    None is built in.

---

## 2. The deck object

### 2.1 What it is

A **deck is a box on the layer Deck** (or called "Deck"), as a wall is a box on Wall: its plan
`width` and `height` are the outline; its `depth` is the height of the walking surface above grade;
its `anchor.z` is 0. **Z = 0 is grade for a deck project** — a decision for Marc (§13.1); a house
wall drawn as Existing is then anchored at its own floor height above grade. A deck carries a `deck`
inputs record (§7) and is never a part: it is never a row of the cut list, and its 3D drawing is a
stand-in (the decking slab at its surface, posts to the ground), as an opening's is.

**The ledger edge is derived, never stored:** it is the deck edge that lies on a long face of an
**Existing** wall, judged from the boxes' exact corners with no tolerance, as walls join
([`renovation-sketches.md`](./renovation-sketches.md) §4.1); **Draw → Deck** snaps an edge to the
wall's face through the flush snap M2 has. Exactly one such edge is required: none, and the panel
says "not against a wall"; two (a deck in an inside corner) is refused with "against two walls: not
modelled". The three other edges are **open edges** until a wall stands on them (§4.4).

### 2.2 Inputs, and what each is

| Input | Values | Kind |
|---|---|---|
| Height above grade | the box's Depth | the person's |
| Joist direction | *out* from the ledger (perpendicular to the house) or *along* it | napkin's design default: *out* |
| Joist spacing | the library's Subfloor spacings (PS 2-18 `Subfloor - 16`, `- 24`, …), the same list the wall picker reads | napkin's design default 16", said with `FramingOptions.DefaultSpacingNote` |
| Joist, ledger, rim | one library dimension lumber (2x6 … 2x12) for all three | typed; "your choice, checked against the code below" |
| Beam | plies (1–3) and a library lumber | typed |
| Posts | a library lumber (4x4, 4x6, 6x6) and a **count ≥ 2** | typed; napkin never picks a post count — the beam span the count gives is what the beam check reads |
| Cantilever | how far the joists run past the beam, ≥ 0 | typed; napkin's design default 0 |
| Decking | a library decking board (5/4x4, 5/4x6) or dimension lumber, and the **gap** between boards | typed; gap is napkin's design default 1/8", editable |
| Blocking | one row at mid-span, yes or no | napkin's design default: yes |
| Supports | one of the values the pack's joist table declares (a deck alone; a deck carrying a roof-bearing wall; …) | **entered, never defaulted**, exactly as a wall's Supports |
| Species | one of the values the pack's span tables declare | **entered, never defaulted** |
| Footing depth below grade | a length | typed; the frost check reads it |
| Hardware | lines like `Joist hanger, 2x8 x 10` | typed, counted as today |
| Guard, Stair | §4 | typed |

Two site inputs are added under **Project → Adopted code and site**: **soil bearing value** (whole
psf, from the building department or a soils report, #42) and nothing else — frost depth, snow
load, wind speed and roof live load are already there. Empty is "not entered", and the checks say
so.

### 2.3 The frame, derived every time

`DeckFraming.Of(sketch, library) -> DeckFraming` in `Napkin.Modules.Building`, a sibling of
`FramingList`, giving `FramingPiece`s with new `FramingRole`s (`Ledger`, `Joist`, `RimJoist`,
`Blocking`, `Beam`, `Post`, `DeckingBoard`, and §4's `GuardPost`, `GuardRail`, `GuardCap`,
`Baluster`, `Stringer`, `Tread`) so the shopping list's first-fit aggregation and the CSV take them
unchanged. With `t` the joist stock's thickness (1 1/2"), `W` the deck's length along the ledger,
`D` its depth out from the house, `s` the spacing, `c` the cantilever, joists running *out*:

- **Ledger**: one piece, `W`, the joist stock, against the house.
- **Rim joist**: one piece, `W`, at the outer edge.
- **Joists**: layout positions along the ledger exactly as studs along a wall — start faces at
  `k·s` while `k·s + t ≤ W`, plus an end joist at `W − t` unless the last is already there — each
  `D − 2t` long (ledger face to rim's inner face). The first and last are the end joists.
- **Beam**: `plies` pieces, `W`, under the joists, its outer face `c` in from the rim's outer face.
- **Posts**: `n` pieces, one at each end of the beam and the rest evenly between; each
  `Depth − decking thickness − joist width − beam width` long (pier top at grade: a design
  assumption the panel states; a typed "pier top above grade" is a one-line follow-up if wanted).
  **Beam span** = the clear length between adjacent posts, `(W − n·postWidth) / (n − 1)`, exact.
- **Blocking**: one row at mid-span, one piece per bay between adjacent joists, each the bay's
  clear width (`s − t`, and the odd last bay its own width).
- **Decking**: boards across the joists, each `W` long; count `n` = the least with
  `n·boardWidth + (n − 1)·gap ≥ D`; the panel says the last board's width. Boards are listed as
  count × length; **the library carries no stock lengths for decking** (`decking-softwood.json`
  has none; #155 is the cited-lengths issue), so the shopping list says "22 boards 12'-0", no
  stock-length list read for decking" rather than picking a length, the wording it already uses for
  trim.

Joists *along* the ledger are a different deck: they need a beam at each end running out from the
house, and a ledger that carries nothing. M11 keeps to joists *out* — the direction that gives a
ledger deck its reason for the ledger — and the *along* option is shown greyed in the panel with
"joists along the house need two beams; not in M11". A decision for Marc (§13.3): keep the greyed
option, or drop the input.

### 2.4 Where the deck's lists go

| List | Rule |
|---|---|
| Cut list | nothing: a deck is framing, as a wall is |
| Shopping list, **Deck** section (new) | every piece above and §4's, as boards to buy, through `ShoppingList.Of` over the deck's cut rows, one section per deck named "Deck 1", with the code check results beneath (as the Framing section names each opening's) |
| Hardware | the deck's typed lines, summed with the parts' |
| Demolition | a deck marked Demolish, by name |
| CSV | the Deck section under its own header |

---

## 3. The deck's code checks

`DeckCheck.Of(sketch, packs) -> ImmutableArray<DeckChecks>` runs every check on every deck, every
time anything changes, on the `after` sketch; nothing is stored. Each check is a **lookup in the
adopted pack**, each result exactly one of the honest states, worded as the header check's are, and
the message bar says every result that changed with the edit that changed it. The **mechanism** is
what M11 lands; every table below is **synthetic** in tests and **absent** from the shipped
Connecticut pack, which answers "The loaded pack CT 2022 has no deck joist span table, so napkin
cannot check these joists. Nothing is guessed: add it from your copy of the code
(docs/rules-engine.md)." Real, cited rows are M10's and the four data issues' (#40–#43).

### 3.1 One new table kind covers joists, beams and rafters: `member-span`

The engine has `header-sizing` (banded inputs → a member) and `wall-bracing` (a provision file).
Joist, beam and rafter tables all have the same shape — *given a species, a member and a spacing
(and for rafters a snow load), the longest span allowed* — so one kind serves all three, declared
per table with `use`:

```json
{ "schemaVersion": 1, "kind": "member-span", "use": "deck-joist" | "deck-beam" | "rafter",
  "table": "ZZ-DECK-JOIST", "title": "…", "source": "…", "location": "p. …",
  "inputs": [ { "name": "supports", "type": "enum", "band": "exact", "values": ["zz-deck", "zz-deck-and-roof"] },
              { "name": "species",  "type": "enum", "band": "exact", "values": ["zz-fir"] },
              { "name": "member",   "type": "lumber", "band": "exact" },
              { "name": "spacing",  "type": "length", "band": "exact" } ],
  "outputs": [ { "name": "span", "type": "length" } ],
  "footnotes": [ … ], "rows": [ { "id": "r.fir.2x8.16", "supports": "zz-deck", "species": "zz-fir", "member": "2x8", "spacing": "16in", "span": "11ft 1in", "location": "…" }, … ] }
```

(The numbers are the synthetic fixture's, made up and chosen **not** to coincide with any
published span; the fixture's README says so, as the bracing fixture's does.) A `deck-beam`
table's inputs are `species`,
`member` (plies and nominal, as a header row's) and `joistSpan` (`upper-bound`: the joists the beam
carries); a `rafter` table's add `groundSnowLoad` (`upper-bound`) and may add `roofLiveLoad` and
`deadLoad` if the adopted text bands on them — declared by the pack, asked of the person only when
declared, exactly as headers ask for a roof live load only when a footnote needs it.

**The request** is the deck's (or roof's) typed member, spacing, species and Supports, and the
**actual span** from the drawing — the joists' `D − 2t`, the beam's clear span between posts, the
rafters' horizontal run from the ledger face to the outer face of the plate (§5.3). **The result**
(`SpanResult`, shaped like `BracingResult`):

- **Passes**: "Joists 2x8 at 16" o.c., zz-fir, span 9'-9": allowed up to 11'-1" (ZZ-DECK-JOIST
  row r.fir.2x8.16, p. …)." with the citation line and the row's footnotes.
- **Short**: "Joists 2x8 … span 12'-3": allowed up to 11'-1", **over by 1'-2"** (row …). Use a
  deeper joist, closer spacing or another beam." Never a member suggestion beyond that sentence.
- **Out of scope**: a Supports or species value the table has no row for ("this table has no row
  for a deck carrying a roof-bearing wall: get it engineered"), a spacing not in the table, a limit
  declared by the table.
- **Input missing**: the species, the Supports, the snow load — naming where to type it.
- **No data**: no code chosen, or a pack without the table for this `use`.

Why a *check of the typed member* and not a *sizing*: #41 asks for "the matching row" for a given
size; a check never changes lumber under the person (rule 10); and the deck's box does not depend
on the joist depth, so nothing in the drawing moves when the answer changes. Headers are sized
because a header has no other home; a joist is chosen.

### 3.2 Ledger fastening: `deck-ledger`

Inputs the table declares — typically `joistSpan` (`upper-bound`) and the ledger `member`
(`exact`); outputs `fastener` (text as printed) and `spacing` (length), with the rows' footnotes
(flashing, prohibited attachments, the band-joist material the row assumes) shown `not-encoded`
with the result — the existing footnote mechanism, nothing new. **Sized**: "Ledger to the house:
zz-bolts, staggered, 17" on centre (ZZ-DECK-LEDGER row …); 10 fasteners for a 12'-0" ledger
(⌈144 ÷ 17⌉ + 1, napkin's count)." (Synthetic fastener and spacing.) The count rule is napkin's, said. **Out of scope** on a
span past the last band, citing it; **No data** and **Input missing** as above. What the ledger
attaches to (the house's band joist, its material) is **not modelled** and rides as the row's
footnote text: napkin does not know what is behind the siding.

### 3.3 Footing size: `deck-footing`

Inputs: `tributaryArea` (`upper-bound`, square feet, whole) and `soilBearing` — a **new band kind,
`lower-bound`**: the largest column bound *at most* the site's value (a stronger soil is never
rounded up to a column it does not reach; a site value below the smallest column is
**Out of scope**, "below the table's lowest bearing value"). Output: `footing` (text as printed:
"zz 15 in square" in the synthetic pack — always the pack's words). **Tributary area** is derived: for an interior
post, half the beam span each side × (half the joist span + the cantilever); for an end post, its
one side; the check runs for the **largest**, and says which post. The band kind, its load checks
(a `lower-bound` column's bands must ascend and be gap-free like any other) and its golden cases
are the engine slice's (§10 D).

### 3.4 Frost depth

No table: the deck's typed **footing depth below grade** against the project's **frost depth**
site value. **Passes**: "Footings 3'-6" below grade; frost line 3'-6" (site value, Town building
department, 2026-09-24)." **Short** by the difference. **Input missing** naming the site value or
the deck's depth. The line also says why no exception is offered: "attached to Wall 1 by its ledger,
so the exception for decks not supported by a dwelling does not apply (CT §R403.1.4.1 exc. 3,
p. 145)". That sentence is **not** built into the engine (it builds in no code's text): it is a
`not-encoded` footnote carried by a new one-line provision file a pack may have, `frost.json`
(`kind: "frost"`: the site-criteria value `frostLineDepth`, its source and location, and
footnotes). The shipped CT pack **gains that file**, its value the one `ct-overlay-data.json`
already holds, read and cited from Table R301.2 (p. 131), and the panel offers it as a **cited
suggestion** — "CT 2022 says 42" (Table R301.2, p. 131) — use it?" — that the person accepts
explicitly into the site value (rules-engine-model §5's stated pattern, never applied silently).
A decision for Marc (§13.6).

### 3.5 Guards and stairs: `deck-guard-stair`

A provision file, shaped like bracing's `limits`, each item cited:

```json
{ "schemaVersion": 1, "kind": "deck-guard-stair", "section": "ZZ-GUARD.1", "source": "…", "location": "…",
  "guard": { "triggerHeight": "28in", "minimumHeight": "34in", "maximumOpening": "5in", "location": "…" },
  "stair": { "maximumRiser": "8-1/4in", "minimumTread": "9in", "maximumRiserDifference": "1/2in",
             "handrailWhenRisersAtLeast": 3, "minimumWidth": "32in", "location": "…" },
  "footnotes": [ … ] }
```

(Synthetic numbers, made up and chosen not to coincide with any adopted text's; nothing here was
read from a code.) Results, one line each in the deck panel under **Guard** and **Stair**:
*guard required* when the deck's height is above `triggerHeight` (cited) and the deck has an open
edge (§4.4); *guard height passes/short* against the typed height; *baluster gap passes/over*
against the typed gap and the derived bottom clearance; *riser height passes/over*, *tread run
passes/short*, *handrail required* — each cited, or **No data** under a pack without the file. A
pack's file may leave any field `null`, and that item is then "not covered by this pack".

---

## 4. Guards and stairs, at DIY level

### 4.1 Guard, stated exactly

Ticked on the deck, with typed **height** `g` (top of the cap above the decking), **post spacing**
`p` (maximum, on centre), **baluster gap** `b` (the most the person will accept), a **bottom
clearance** `e` (typed; napkin's design default equals `b`), and lumber for posts (4x4), rails
(2x4), cap (2x6) and balusters (2x2) — all typed, the defaults being napkin's. On each **open edge
run** (an open edge, less any stair opening on it, §4.4):

- **Posts**: at both ends of the run and evenly between, the fewest with spacing ≤ `p`: bays
  `= ⌈run ÷ p⌉`, posts `= bays + 1`; a post at a corner is shared with the adjoining run (counted
  once, in id order of the runs). Each post `g + joist width + decking thickness` long, bolted to
  the rim (the bolts are hardware).
- **Bay clear** `= (run − posts·postWidth) ÷ bays`, exact.
- **Rails**: two per bay, on edge, each the bay clear.
- **Cap**: one per run, the run's length (corners butt; no mitre).
- **Balusters** per bay: `n = ⌈(clear − b) ÷ (balusterWidth + b)⌉`, the fewest giving a gap
  ≤ `b`; the **actual gap** `= (clear − n·balusterWidth) ÷ (n + 1)`, exact, shown, and what the
  gap check reads. Each `g − cap thickness − 2·rail width − e` long.

### 4.2 Stair, stated exactly

Ticked on the deck with **edge** (a compass name; the ledger edge is refused at check time, "the
stair cannot be on the house side"), **at** (its offset
along that edge), **width** `w`, **tread run** `u` (typed), **riser count** (typed, or `null` to
take the fewest the pack allows), **stringers** (count, typed; napkin's design default 3) and their
lumber (2x12), and **tread boards** per tread (count, typed; the decking board). Total rise
`R` = the deck's height above grade (to grade at Z = 0; a landing is not modelled and the pack's
grade-termination text rides as a footnote — CT's exception 4 says 12" below undisturbed ground,
p. 145, per the research).

- **Risers** `N` = the typed count, or `⌈R ÷ maximumRiser⌉` when the pack has one (cited); with
  neither, "type the riser count". **Rise each** `= R ÷ N`, an exact rational, shown to 1/16" with
  ≈ when it does not land on the grid. **Treads** `N − 1`, **total run** `(N − 1)·u`.
- **Stringer layout, in plain words**: "Lay out 5 risers of 7 3/16" (≈, exactly 36 ÷ 5) and 4
  treads of 10" on a 2x12 with the square at 7 3/16 and 10; the diagonal of the whole rise and run
  is 53 7/8", so a 6'-0" board is enough." The **diagonal** `⌈√(R² + U²)⌉` is the integer square
  root in `Int128` of the units squared, **rounded up** to the grid and shown rounded **up** to
  1/16"; the board bought is the diagonal **plus one tread run** (napkin's allowance for the end
  cuts, labelled) rounded to the next stock length by the shopping list as any piece is.
- **Treads**: `(N − 1) × treadBoards` boards, each `w` long. Stringers `count` pieces.
- The **stair opening** in the guard is `w` wide at `at` on its edge; the guard runs either side of
  it get their own posts. Handrails are hardware (typed) and the pack's handrail text a cited line.

### 4.3 What the guard and stair are not

Not a lateral design (no post-to-rim connection is sized: the bolts are typed hardware), not a
closed-riser stair, not a landing, not a graspable-rail profile. The pack's provisions say what is
required; napkin's pieces are what a DIY guard is made of, and the panel says "napkin's layout, not
a code detail" beside them.

### 4.4 Open edges

An edge of the deck is **open** unless a wall (any phase but Demolish) stands on it — a wall whose
bottom is at the deck's surface and one of whose long faces lies on the edge's line, with its extent
covering the edge (the room-bounding rule of [`renovation-sketches.md`](./renovation-sketches.md)
§4.2, applied at the deck's surface height). The ledger edge is never open. An enclosed porch has no
open edge, so no guard and no guard check; the stair keeps its own.

---

## 5. The porch: walls on the deck, screens, and the shed roof

### 5.1 Walls on the deck

Nothing new. A wall drawn on the deck is a wall as today, anchored at `z` = the deck's surface
(`Draw → Walls → Wall` over a deck starts the wall there; the panel's Up shows it). Its frame, its
openings' headers, its bracing line and its phase are exactly [`../building.md`](../building.md)'s.
The **front wall** — the one on the edge opposite the ledger — carries the roof: the person marks it
**Side exterior, Bearing yes** and chooses its **Supports** from the pack's header table (a roof
alone, in most tables' words), and its headers are code-sized as today. The two **side walls**
carry no rafters (they run parallel to the rafters): **Bearing no**, and the person types their
header. napkin does not infer any of this; the roof panel says which wall its low end sits on and
"this wall carries the roof: mark it bearing" when it is not marked.

**The deck carries the wall.** A bearing wall on a deck makes the deck's joists, beam and footings
carry a roof — and deck span tables are written for deck loads. napkin routes this through the deck's
**Supports** (§2.2): the joist and beam tables declare what values they cover; a deck whose Supports
says it carries a roof-bearing wall asks for that row, and a table without one answers **Out of
scope** citing the table. When a bearing wall stands on the deck and its Supports still says a deck
alone, the panel says "Wall 3 (bearing) stands on this deck: choose what the deck supports". The
person chooses; napkin never switches it. Risk §12.1, decision §13.4.

**Checks that need the wall's `z`.** Wall reading, opening placement, `FramingList`, `WallLine` and
`WallJoins` work in each wall's own frame or in plan (`WallJoin.cs` projects with `Z = 0`
deliberately, plan-only); nothing read for this note assumes a wall's bottom is at world 0, but
slice B's first test stands a wall at `z` = 36" and runs every check on it before anything else is
built on that.

### 5.2 Screens: an opening's fill

Today an opening is a window or a door by its sill (`OpeningKind`, derived). A porch adds
**screens**, and a screen door has sill 0, and a sliding glass door is glazing — so what is needed is
not a third *kind* but a stored **fill** on every opening: **glass**, **screen** or **solid**
(`"opening": { "fill": … }` on the box, §7; `null` on a box that is not an opening). The frame does
not change with the fill; the sunroom arithmetic (§5.5) reads it; the plan draws a screen with a
finer dash and the 3D stand-in paints it lighter. **Draw → Window** starts *glass*, **Draw → Door**
starts *solid*, and a new **Draw → Screen** starts *screen* with the window's placement; the panel's
Fill picker changes any of them. A decision for Marc against #187's literal "screens as a kind of
opening" (§13.5).

### 5.3 The shed roof: the one new structure

A **roof is a box on the layer Roof**: its plan `width` is the roof's width along the house; its
plan `height` is the **run** — the horizontal distance from the house wall's face to the outer face
of the low support (the front wall's top plate, or the beam); its `depth` is the **rise** over that
run; its `anchor.z` is the top of the low support (the front wall's top plates); in plan the roof box
covers the deck's outline and shares its `anchor.x` and `.y` (a box's anchor is its south-west
corner). Its **high edge** is the deck's ledger edge, derived the same way. **Draw → Porch roof**
clicks a deck and takes all of that from the deck and the front wall; the person then types the
pitch. The 3D view draws a sloped slab within the box — a
stand-in, like an opening.

**Pitch is derived from the box, not stored**: pitch = `rise ÷ run`, shown as "5 in 12" when
`rise × 12 = run × n` for a whole `n`, else "≈ 4.96 in 12 (rise 50" over 121")". Typing a pitch in
the panel sets the box's depth to `run × n ÷ 12`, **rounded to the grid** if it does not land on it,
and the panel shows what it got. Resizing the deck changes the run, the rise stays, and the pitch
drifts and says so — exactly as a dimension follows geometry. A decision for Marc (§13.7): store the
rise (recommended) or a pitch.

Inputs (`roof`, §7): **rafter spacing** (the library's Roof spacings, `Roof - 16` / `- 24`;
napkin's design default 16", said), **rafter** and **ledger** lumber (typed; the ledger is the
rafter's stock), **overhang** at the eave (horizontal, typed; napkin's design default 12", said),
**blocking** at the plate (yes/no, design default yes), **sheathing** (a library panel, typed) and
**roofing** (a typed name, a typed coverage in whole square feet per unit, a typed waste percent,
default 0 — the roofing panel's words, as paint's), and the **low end**: the front wall (by id,
derived from the deck edge it stands on) or a **beam on posts** (plies and lumber, post lumber and
count — an open porch roof; the beam's top is the roof's `anchor.z`, the posts stand on the deck at
the outer edge, each `anchor.z − deck surface − beam width` long; the beam span is the clear length
between posts and the beam check is §3.1's `deck-beam` use).

### 5.4 The rafters, stated exactly

With `run` the box's plan height, `rise` its depth, `t_l` the ledger's thickness, `o` the overhang,
`s` the spacing, `W` the width, `d` the rafter's dressed width, `p` the plate's width (the front
wall's thickness), and all products exact rationals:

- **Rafter run** `r = run − t_l + o`: from the ledger's face to the tail, horizontally. The rafter's
  plumb cut bears on the ledger's face, not the house wall's.
- **Rafter length along the slope** `L = r × √(run² + rise²) ÷ run`. When `run : rise : hypotenuse`
  is a whole-number triple (5 in 12 is `12 : 5 : 13`) this is an exact rational; otherwise the
  hypotenuse is the **integer square root in `Int128`** of the units squared, rounded **up** to the
  grid, and `L` is shown to 1/16" with ≈. No floating point.
- **Count**: layout positions along `W` as joists (§2.3): `k·s` while `k·s + t ≤ W`, plus an end
  rafter at `W − t`. The end rafters sit flush with the side walls.
- **Ledger**: one piece, `W`, the ledger stock. **Its top edge above the low support's top** =
  `HAP + (run − t_l) × rise ÷ run`, where the **height above plate** `HAP = d × hyp ÷ run − p × rise
  ÷ run` (the rafter's plumb depth less the birdsmouth's plumb depth); shown to 1/16" with ≈, and
  "on Wall 1 (existing) at 15'-7 3/4" above grade — check it clears the house's eave and openings;
  napkin does not know the house". The rafters hang on the ledger in hangers (typed hardware).
- **The cuts, in plain words**, on the rafter's row and in the panel: "Plumb cut at the top: set the
  square at 5 and 12 and mark plumb. **Birdsmouth**: from the top plumb cut measure `(run − t_l) ×
  hyp ÷ run` along the top edge (the plate's outer face), mark a plumb line, then a level **seat**
  `p` long (the plate's width) back toward the top; the plumb part of the notch is `p × rise ÷ run`
  deep (≈). **Tail**: `o × hyp ÷ run` further along the top edge, cut plumb." A square tail is not
  offered; the fascia is not modelled (rule 7).
- **Blocking** at the plate: one piece per bay between rafters, each the bay's clear width, the
  rafter stock, as deck blocking.
- **Sheathing**: area = `W × L` (the sheathed slope, overhang included); sheets = ⌈area ÷ (sheet
  width × sheet length)⌉ of the typed panel, "sheets by area — a layout may need more", the
  takeoff's own words. **Roofing**: the same area × (100 + waste) ÷ 100, in square feet to one
  decimal, and units = ⌈that ÷ typed coverage⌉ when typed, else "type the coverage from the bundle".

### 5.5 Sunroom or not: the 40 % line

From the research (§1): a **sunroom** is "a one-story structure attached to a dwelling with a
glazing area in excess of 40 percent of the gross area of the structure's exterior walls and roof"
(IRC 2021 §R202/§N1101.6, via UpCodes, retrieved 2026-09-25). napkin computes the ratio and says
which side of the line the drawing is on; it does not classify (the category I–V is the person's,
research "only cite"), and it does not enforce AAMA/NPEA/NSA 2100 (not read).

`Glazing.Of(sketch, roof) -> GlazingRatio`, exact rationals in `Area`:

| Quantity | Exactly |
|---|---|
| `wallsGross` | Σ over the porch's walls (every wall standing on the deck) of `length × height`, **gross** — openings are not subtracted, the definition says gross |
| `rakeFill` | for each side wall (a wall on the deck running out from the house) the triangle above it: `½ × wallLength × (wallLength × rise ÷ run)` |
| `roof` | `W × L`, the sheathed slope |
| `glazing` | Σ over openings in those walls with fill **glass** of `width × height`; screens and solids count nothing |
| **ratio** | `glazing ÷ (wallsGross + rakeFill + roof)`, shown to one decimal place of a percent |

Shown in the roof panel: "Glazing 10.4 % of walls and roof (45.0 sq ft of 432.0): **under the 40 %
line**, so not a 'sunroom' by IRC §R202's definition (research 2026-09-25); an ordinary
unconditioned roofed addition." or "**over the 40 % line**: a 'sunroom' by IRC §R202; §R301.2.1.1.1
asks you to assign a category — a three-season porch is I, II or III (nonhabitable, unconditioned)."
Whether the porch walls or the house wall between porch and house is "exterior" for the denominator:
the porch's three walls and its roof, not the house wall — the structure's own envelope. A decision
for Marc (§13.8).

### 5.6 The triangle above each side wall

Reported in the roof panel and the Roof section, never framed in M11 (rule 4): "Above Wall 2: a
triangle 9'-8 1/2" long rising to 4'-0 9/16" (≈) at the house, 19.6 sq ft — not framed; its studs
step by `s × rise ÷ run` each." The optional slice in §10 frames it: studs at the layout positions,
each `(H_front − 3t) + x × rise ÷ run` rounded **down** to the grid, said.

---

## 6. Phase

Everything a deck project draws is **New** against an **Existing** house wall, with M8's phases
exactly. A deck, a roof and a wall on the deck marked Demolish come off every list but Demolition.
The framing diff does not apply to decks or roofs (nothing existing is ever framed as a deck), and
an Existing deck is an outline whose walls may be new — the porch-on-an-old-deck case, which this
note supports without a word more: the deck's pieces are not bought, its checks still run and say
"existing deck: checked as drawn, not surveyed".

---

## 7. The file format: one bump, 10 → 11

Every version-10 file is refused, no converter; every committed sample is rewritten in slice A. No
optional fields: every field below is written every time.

**On every box:** `"deck": null | {…}`, `"roof": null | {…}`, `"opening": null | { "fill": "glass" |
"screen" | "solid" }`. Refused: `deck` or `roof` on a box with a non-null `part`, `wall`, `room` or
`opening`; `opening` non-null on a box that also has `wall`; an unknown fill.

```json
"deck": { "joistDirection": "out",
          "joistSpacing": <length>, "joist": "2x8", "beam": { "plies": 2, "lumber": "2x10" },
          "post": "4x4", "postCount": 3, "cantilever": <length ≥ 0>,
          "decking": "5/4x6", "deckingGap": <length ≥ 0>, "blocking": true,
          "supports": <text> | null, "species": <text> | null,
          "footingDepth": <length> | null,
          "hardware": [ { "name": <text>, "quantity": <int ≥ 1> } ],
          "guard": null | { "height": <length>, "postSpacing": <length>, "balusterGap": <length>,
                            "bottomClearance": <length>, "post": "4x4", "rail": "2x4", "cap": "2x6", "baluster": "2x2" },
          "stair": null | { "edge": "north" | "south" | "east" | "west", "at": <length>, "width": <length>,
                            "run": <length>, "risers": <int ≥ 2> | null, "stringers": <int ≥ 2>,
                            "stringer": "2x12", "treadBoards": <int ≥ 1> } }
```

| Field | Refused when |
|---|---|
| `joistDirection` | other than `out` (`along` is reserved and refused in M11, §2.3) |
| `joistSpacing`, `postSpacing`, `width`, `run`, `height` | 0 or less |
| `joist`, `beam.lumber`, `post`, `decking`, `stringer`, `guard.*` lumber | empty (not checked against the library, as a part's stock is not) |
| `beam.plies` | outside 1–3 |
| `postCount` | below 2 |
| `edge` | not one of the four compass names. Which edge is the ledger's is geometry, not judged at load: a stair on the ledger edge is refused at check time, in the panel |
| `hardware` | as a part's |

```json
"roof": { "rafterSpacing": <length>, "rafter": "2x8", "ledger": "2x8", "overhang": <length ≥ 0>,
          "blocking": true, "sheathing": <text> | null,
          "roofing": { "name": <text>, "coverage": <sq ft, int > 0> | null, "waste": <whole percent ≥ 0> },
          "lowEnd": { "kind": "wall", "wall": <id> } | { "kind": "beam", "beam": { "plies": 2, "lumber": "2x10" }, "post": "4x4", "postCount": 2 } }
```

`lowEnd.wall` is a **reference** (refused when it names no box, as a relationship's is), and the box
it names must read as a wall at check time, else the panel says "the roof's low end names something
that is not a wall". `pitch` is **not** stored (§5.3).

**Site:** `"soilBearing": <whole psf> | null` — negative or non-integer refused.

**Layers by name**, as Wall and Opening are: **Deck**, **Roof**.

**Expected files** (`samples/*.expected.json`) gain `deck`, `roof` and `glazing` sections and the
`deckChecks` results under both the synthetic pack and the shipped Connecticut pack, as
`window-in-existing-wall` records both.

---

## 8. The UI, minimal and in napkin's conventions

- **Draw menu / toolbar:** **Deck** (**Shift+D**) after Room; **Screen** after Door; **Porch roof**
  (**Shift+R**) after Deck. `KeyMaps.cs` binds Shift with M, J, X, Y, Z, S, L and the arrows; D and
  R are free; the implementer confirms against the shell's and view's maps.
- **Draw → Deck:** press on an Existing wall's face (the edge snaps flush), drag the outline out;
  release. Depth starts at **3'-0"** — a starting value to type over, not a standard, said as the
  wall's 8'-0" is. The deck tool is `DeckTool` in `Napkin.Modules.Editing`, shaped like `WallTool`.
- **Draw → Porch roof:** click a deck; the roof box is made from the deck and the wall on its far
  edge (or, with no wall there, a beam on posts is the low end, at the wall's starting height), at a
  starting pitch of **4 in 12** — napkin's starting value, to type over.
- **Part panel**, deck selected: a **Deck** block — the inputs of §2.2 as pickers and typed boxes
  (mono 12.5 like every entry), the frame in one line ("ledger, 10 joists 2x8 at 16", rim, (2) 2x10
  beam on 3 posts, 22 boards"), then **Code check** with the seven lines of §3, each with its
  citation and **How it was found**, then **Guard** and **Stair** blocks. Roof selected: a **Roof**
  block — pitch, the inputs of §5.3, the rafter line ("10 rafters 2x8 × 141 3/8" at 16"; ledger
  12'-0" at 12'-7 3/4" (≈) above the deck"), the cuts in plain words, sheathing and roofing lines,
  the rafter check, the **triangle** line, and **Sunroom test** (§5.5). Opening selected: a **Fill**
  row.
- **Shopping list window:** **Deck** and **Roof** sections between Framing and Area takeoff, each
  with its pieces as boards to buy and its check results beneath; **Sunroom test** one line under
  Roof; each in the CSV under its own header.
- **Canvas:** a deck draws as its outline with the joist direction ticked at the ledger edge and
  the posts as small squares; a roof as its outline with the eave line and a slope arrow; a screen
  opening with a finer dash. 3D: the decking slab and posts; the sloped slab. Status bar: "Deck 1,
  new".
- **Message bar:** every changed check with the edit that changed it, as today: "Set Deck 1's post
  count to 2. Beam (2) 2x10 span 11'-8 1/2" is now OVER the 8'-0" allowed (ZZ-DECK-BEAM row …)."

Nothing else moves. No wizard, no new window.

---

## 9. The worked example, by hand

Every dimension is the builder's typed choice unless marked as napkin's default. Values in inches;
`t` = 1 1/2". The synthetic pack `us-zz-deck` (slice D) is arranged to answer as stated, and its
numbers are **made up**; under the shipped Connecticut pack every check but frost says **No data**.

### 9.1 The drawing

Z = 0 is grade. The house wall: Existing, 2x6, 20'-0" long, 8'-0" tall, anchored at (0, 0, 36) —
its south face on the line y = 0, its floor 36" above grade.

| Entity | Layer | Phase | Box (width × height × depth) | Anchor | Notes |
|---|---|---|---|---|---|
| House wall | Wall | Existing | 240 × 5 1/2 × 96 | (0, 0, 36) | Side exterior, Bearing yes |
| Deck 1 | Deck | New | 144 × 120 × 36 | (48, −120, 0) | north edge on y = 0: the ledger edge |

Deck inputs: joists *out*, 16" (default), joist/ledger/rim **2x8**, beam **(2) 2x10**, posts **4x4
× 3**, cantilever 0, decking **5/4x6**, gap 1/8" (default), blocking yes, Supports **zz-deck**,
species **zz-fir**, footing depth **42"** (accepted from the CT suggestion, §3.4), hardware "Joist
hanger 2x8 × 10", "Post base × 3", "Post cap × 3". Site: soil bearing **2000** psf (typed), frost
depth 42", snow 30 psf, wind 115 mph, SDC B, roof live load 20 psf.

### 9.2 The deck frame

- **Joists**: positions 0, 16, … 128 (nine; 128 + 1 1/2 ≤ 144, 144 is not), end joist at 142 1/2:
  **10 joists 2x8 × 117"** (120 − 3). Bays: 8 × 14 1/2" and the last 142 1/2 − 129 1/2 = 13".
- **Ledger** 1 × 144" 2x8; **rim** 1 × 144" 2x8; **blocking** 8 × 14 1/2" + 1 × 13" 2x8.
- **Beam** 2 × 144" 2x10; **beam span** (144 − 3 × 3 1/2) ÷ 2 = **66 3/4"** clear.
- **Posts** 3 × 4x4, each 36 − 1 − 7 1/4 − 9 1/4 = **18 1/2"**.
- **Decking**: n with 5 1/2 n + 1/8 (n − 1) ≥ 120 → 5 5/8 n ≥ 120 1/8 → n ≥ 21.36 → **22 boards
  × 144"**, the 22nd covering 120 − 118 − 1/8 = **1 7/8"** ("adjust the gaps or the overhang").
  Listed as 22 boards 12'-0", no stock length read (#155).
- **Ledger fasteners** (synthetic row: zz-bolts staggered at 17"): ⌈144 ÷ 17⌉ + 1 = **10**.
- **Tributary area**, middle post: 66 3/4" × (117 ÷ 2 = 58 1/2") = 3904.875 sq in = **27.1 sq ft**
  (shown to one decimal; the table's band reads the exact value).

Pieces: 2x8 — 2 × 144, 10 × 117, 8 × 14 1/2, 1 × 13 (21 pieces); 2x10 — 2 × 144; 4x4 — 3 × 18 1/2.

**Boards** (first-fit decreasing over the library's 6'…16', 1/8" kerf — **to be re-derived by the
implementer**; one hand pass): 2x8: each 144" alone on a 12' (nothing else fits the 0" left); each
117" alone on a 10' (3" left); the blocking, 8 × 14 1/2 + 13 + 8 kerfs = 130", on one 12' →
**3 × 12', 10 × 10' = 136 lineal ft, 181.3 board feet**. 2x10: **2 × 12'**, 40.0 board feet. 4x4:
3 × 18 1/2 + 2 kerfs = 55 3/4" → **1 × 6'**, 8.0 board feet.

**Checks** (synthetic): joists 2x8 at 16", zz-fir, span 9'-9": passes (row); beam (2) 2x10 span
5'-6 3/4" for a joist span of 9'-9": passes; ledger as above; footing at 27.1 sq ft and 2000 psf:
"zz 15 in square" (row); frost 42" of 42": passes; guard: 36" is above the synthetic 28" trigger
and three edges are open → required.

### 9.3 Guard and stair on the open deck

Guard: height 36", post spacing 72", baluster gap 3 1/2", bottom clearance 3 1/2" (default = gap),
4x4 / 2x4 / 2x6 / 2x2. Stair on the **east** edge at 42" from the house, width 36", run 10", risers
from the pack, 3 stringers 2x12, 2 tread boards.

- Runs: west 120", south 144", east 42" (house to the stair) and 42" (stair to the corner).
- Posts: west ⌈120/72⌉ = 2 bays, 3 posts; south 2 bays, 3; east 1 bay each, 2 + 2; corners shared
  (south-west, south-east) → **8 posts × 44 1/4"** (36 + 7 1/4 + 1).
- Bay clear: west (120 − 10 1/2) ÷ 2 = 54 3/4"; south (144 − 10 1/2) ÷ 2 = 66 3/4"; east (42 − 7)
  = 35". **Rails** 2x4: 4 × 54 3/4, 4 × 66 3/4, 4 × 35. **Caps** 2x6: 120, 144, 42, 42.
- Balusters per bay: west ⌈(54 3/4 − 3 1/2) ÷ 5⌉ = ⌈10.25⌉ = 11, gap (54 3/4 − 16 1/2) ÷ 12 =
  **3 3/16"**; south ⌈63 1/4 ÷ 5⌉ = 13, gap (66 3/4 − 19 1/2) ÷ 14 = **3 3/8"**; east ⌈31 1/2 ÷ 5⌉
  = 7, gap (35 − 10 1/2) ÷ 8 = **3 1/16"**. Total 2·11 + 2·13 + 2·7 = **62 balusters 2x2 ×
  24"** (36 − 1 1/2 − 3 1/2 − 3 1/2 − 3 1/2). Every gap ≤ 3 1/2": the gap check passes (synthetic
  5" limit); height 36" passes the synthetic 34" minimum.
- Stair: synthetic maximum riser 8 1/4" → N = ⌈36 ÷ 8 1/4⌉ = **5 risers of 7 1/5"** (36 ÷ 5; shown
  7 3/16" ≈), **4 treads**, total run 40". Diagonal √(36² + 40²) = √2896 = 53.814… → **53 7/8"**
  rounded up; board = 53 7/8 + 10 = 63 7/8" → **3 × 6' 2x12**. Treads **8 × 36"** 5/4x6.

### 9.4 The porch

Three New 2x4 walls on the deck (z = 36), 8'-0" tall (the wall's starting height, kept):

| Wall | Box | Anchor | Side / Bearing | Openings (a along the wall, w × h, sill, fill) |
|---|---|---|---|---|
| Front (south) | 144 × 3 1/2 × 96 | (48, −120, 36) | exterior / **yes**, Supports zz-roof | windows 36 × 60, sill 24, **glass** at a = 9, 54, 99 |
| West | 116 1/2 × 3 1/2 × 96, turned | x 48..51 1/2, y −116 1/2..0 | exterior / no, header (2) 2x6 | screens 36 × 60, sill 24, **screen** at a = 14, 66 1/2 |
| East | 116 1/2 × 3 1/2 × 96, turned | x 188 1/2..192 | exterior / no, header (2) 2x6 | screen 36 × 60, sill 24 at a = 14; **screen door** 36 × 80, sill 0 at a = 66 1/2 |

(The side walls butt between the house wall's face and the front wall's inner face: 120 − 3 1/2.)
Deck Supports changes to **zz-deck-and-roof** (the panel asks, §5.1). No open edge: the guard is
gone; the stair stays, under the screen door.

**Front wall frame** (`FramingList`, studs 91 1/2"): layout 0 … 128 (nine) + end 142 1/2 = 10;
excluded bodies [6, 48), [51, 93), [96, 138) take out 16, 32, 64, 80, 96, 112, 128: **3 studs**
(0, 48, 142 1/2); **6 kings** 91 1/2"; **6 jacks** 82 1/2" (24 + 60 − 1 1/2); **3 headers 39"**,
sized **(2) 2x6** by `us-zz-deck` — a project has one adopted code, so the synthetic deck pack
also carries an exterior-bearing header row, copied from ZZ-RENO-HEADER, answering (2) 2x6 with 1
jack and 1 king for zz-roof at 30 psf and a 3'-0" span; room 96 − 3 − 84 = 9", cripples above 3 1/2" at 16, 32; 64, 80; 112, 128: **6**; rough sills
**3 × 36"**; cripples below 21" (24 − 3) at the same six positions: **6**. Plates **3 × 144"**.
**West wall**: layout 0 … 112 (eight) + end 115 = 9; excluded [11, 53) and [63 1/2, 105 1/2)
take out 16, 32, 48, 64, 80, 96: **3 studs**, 4 kings, 4 jacks 82 1/2", 2 headers 39" (typed (2)
2x6), cripples above at 16, 32, 48 and 80, 96: **5 × 3 1/2"**, sills 2 × 36", cripples below at 16,
32, 48 (48 + 1 1/2 ≤ 50) and 80, 96: **5 × 21"**; plates 3 × 116 1/2". **East wall**: the same
first opening; the door: 2 kings, 2 jacks **78 1/2"**, header 39", room 13", cripples above
**2 × 7 1/2"** at 80, 96; no sill: **3 studs**, 4 kings, 2 × 82 1/2 + 2 × 78 1/2 jacks, 2 headers,
3 × 3 1/2 + 2 × 7 1/2 cripples above, 1 sill, 3 × 21 below.

### 9.5 The roof

Roof box **144 × 120 × 50** at (48, −120, 132): run 120", pitch typed **5 in 12** → rise 120 × 5 ÷
12 = **50"** exactly. Rafters 2x8 at 16" (default), ledger 2x8, overhang 12" (default), blocking
yes, sheathing 7/16 OSB (library, 4' × 8'), roofing "asphalt shingles", coverage 33 sq ft, waste 0.
Low end: the front wall.

- `hyp ÷ run` = 13 ÷ 12 (12 : 5 : 13). **Rafter run** r = 120 − 1 1/2 + 12 = 130 1/2".
- **Rafter length** L = 130 1/2 × 13 ÷ 12 = **141 3/8"** exactly. **Count**: 0 … 128 + 142 1/2 =
  **10 rafters 2x8 × 141 3/8"**.
- **Plumb depth** of a 2x8 = 7 1/4 × 13 ÷ 12 = 7 41/48 (≈ 7 7/8"); **birdsmouth** seat 3 1/2"
  (the 2x4 plate), plumb 3 1/2 × 5 ÷ 12 = 1 11/24 (≈ 1 7/16"); **HAP** = 7 41/48 − 1 11/24 =
  6 19/48 (≈ 6 3/8"). From the top plumb cut to the birdsmouth's plumb line: 118 1/2 × 13 ÷ 12 =
  **128 3/8"** along the top edge; from there to the tail: 12 × 13 ÷ 12 = **13"**.
- **Ledger** 1 × 144" 2x8, its top edge 6 19/48 + 118 1/2 × 5 ÷ 12 = 6 19/48 + 49 3/8 =
  55 37/48 (≈ **55 3/4"**) above the front wall's plates: 132 + 55 3/4 = 187 3/4" (≈) above grade,
  151 3/4" above the deck.
- **Blocking** at the plate: 8 × 14 1/2" + 1 × 13" 2x8.
- **Sheathing**: 144 × 141 3/8 = 20358 sq in = **141.4 sq ft** → ⌈141.375 ÷ 32⌉ = **5 sheets**.
  **Roofing**: 141.375 ÷ 33 = 4.28 → **5 bundles**.
- **Rafter check** (synthetic `rafter` use): 2x8 at 16", zz-fir, snow 30 psf, horizontal span
  118 1/2" = 9'-10 1/2": passes (row).
- **Triangles** above each side wall: 116 1/2 × 5 ÷ 12 = 48 13/24 (≈ 48 9/16") high at the house;
  area ½ × 116 1/2 × 48 13/24 = 2827 53/96 sq in ≈ **19.6 sq ft** each.

### 9.6 Sunroom test

| | sq in |
|---|---|
| walls gross: 144 × 96 + 2 × 116 1/2 × 96 | 13824 + 22368 = 36192 |
| rake fill: 2 × 2827 53/96 | 5655 5/48 |
| roof: 144 × 141 3/8 | 20358 |
| **denominator** | 62205 5/48 (= 432.0 sq ft) |
| glazing: 3 × 36 × 60 (the front windows; screens count nothing) | 6480 (= 45.0 sq ft) |
| **ratio** | 6480 ÷ 62205.104 = **10.4 %** — under 40 %: not a sunroom |

Change every screen to glass (+ 3 × 2160 + 2880 = 9360): 15840 ÷ 62205.104 = **25.5 %** — still
under. A solid roof makes the line hard to reach, which is the thing worth seeing.

---

## 10. Implementation slices

Each lands alone on `main` in this order, with `tools/scripts/gate.sh`. Files are disjoint between
slices except the recurring collision points (`napkin.sln`, `Directory.Build.props`,
`ratchet/baseline.json`, `PlannedFeatures.g.cs`). Slice A rewrites every `samples/*.json` version
field — land it first and alone. Feature ids: the catalog's **DECK-001…005** already exist (#40–#43,
#20) and are reused; **ROOF-001…** and **GUI-DECK-NN / GUI-PORCH-NN** are new.

| Slice | What | Model | Files | Depends on |
|---|---|---|---|---|
| **A** (#195) | Format 11 (§7): `deck`, `roof`, `opening.fill`, `site.soilBearing`, layers Deck/Roof, every refusal; `DeckInputs`, `RoofInputs`, `OpeningFill` records; bump every sample; `docs/file-format.md`; tests 1–2 | Sonnet | `Napkin.Core.Geometry/Box.cs` (the three fields), `Napkin.Core.Project/*`, `samples/*.json`, `docs/file-format.md` | — |
| **B** (#196) | `Deck` reading (ledger edge, open edges §4.4), `DeckFraming.Of` (§2.3, joists *out*, decking count, beam span, tributary area), the new `FramingRole`s and the Deck section rows through `ShoppingList.Of`; a wall at `z` = 36" through every existing check (§5.1's test); `samples/deck-12x10`; tests 3–5 | **Opus** — the layout arithmetic crosses Building and Furniture, and a beam span or tributary area that is subtly wrong looks plausible | `Napkin.Modules.Building/Deck.cs`, `DeckFraming.cs`, `FramingList.cs` (roles only), `Napkin.Modules.Furniture/ShoppingList.cs` (a section hook), `docs/building.md` | A |
| **C** (#197) | **Draw → Deck** (`DeckTool`), the deck panel block (inputs and the one-line frame), the plan and 3D stand-ins, the Deck section in the window and CSV, `Project → Adopted code and site`'s soil bearing box; `GUI-DECK-01` | Sonnet | `Napkin.Modules.Editing/DeckTool.cs`, `Napkin.App/MainWindow.Deck.cs`, `CanvasView`, `ModelView`, `CutListWindow`, `tests/Napkin.App.GuiTests` | B |
| **D** (#198) | Rules engine: `member-span` (§3.1), `deck-ledger` (§3.2), `deck-footing` with the **`lower-bound` band** (§3.3), `frost` (§3.4), `deck-guard-stair` (§3.5); `SpanResult`, `LedgerResult`, `FootingResult`, `GuardStairResult`; load checks for each (gaps, overlaps, unknown fields, a `use` the engine does not know, a null provision); the golden-file format for each; `Recompute` diffs; **synthetic** `us-zz-deck` under `tests/…/CodePacks/deck` with goldens | **Opus** — five kinds and a new band through the loader's validation, which must be exhaustive as bracing's was; a lenient loader silently accepts a bad pack | `Napkin.Core.RulesEngine/*` (new files per kind), `tests/Napkin.Core.RulesEngine.Tests`, `tests/Napkin.Modules.Building.Tests/CodePacks/deck`, `docs/rules-engine.md` | — (parallel with B, C) |
| **E** (#199) | `DeckCheck.Of` (§3): the seven checks, the Supports routing (§5.1), the frost suggestion from the pack's `frost.json` and the CT pack's own `frost.json` (its value from `ct-overlay-data.json`, cited); the panel's Code check block, the message-bar sentences, the Deck section's check lines; tests 6–8 | Sonnet | `Napkin.Modules.Building/DeckCheck.cs`, `Napkin.App/MainWindow.Deck.cs` (the block), `packs/packs/us-ct-2022/frost.json`, `docs/building.md` | B, D |
| **F** (#200) | Guards and stairs (§4): `GuardFraming.Of`, `StairLayout.Of` (integer square root in `Int128`, rounding rules), open edges, the Guard and Stair panel blocks and their checks; tests 9–10 | Sonnet | `Napkin.Modules.Building/Guard.cs`, `Stair.cs`, the panel blocks | B, D |
| **G** (#201) | Screens (§5.2): **Draw → Screen**, the Fill row, the plan dash and 3D tint; `Glazing.Of` (§5.5) and the Sunroom test line; `samples/porch-12x10` (walls on the deck, §9.4); tests 11–12 | Sonnet | `Napkin.Modules.Building/Glazing.cs`, `Wall.cs` (fill), `Napkin.Modules.Editing/PlacementTool.cs`, panel | A, B |
| **H** (#202) | The roof (§5.3–§5.4, §5.6): `Roof` reading, `RoofFraming.Of` (rafter length by exact ratio or integer square root, count, the cuts' words, HAP, the ledger height, blocking, sheathing, roofing, the triangle), the rafter check through `member-span` `use: rafter`, the beam-on-posts low end; `samples/porch-12x10` gains the roof; tests 13–15 | **Opus** — the slope arithmetic has four places to be off by a plate or a ledger thickness, each giving a plausible number | `Napkin.Modules.Building/Roof.cs`, `RoofFraming.cs`, `docs/building.md` | B, D, G |
| **I** (#203) | **Draw → Porch roof** (`RoofTool`), the roof panel block, the sloped 3D slab, the Roof section and CSV; `GUI-PORCH-01` | Sonnet | `Napkin.Modules.Editing/RoofTool.cs`, `Napkin.App/MainWindow.Roof.cs`, views, `CutListWindow` | H |
| **J** (#204) | `GUI-DECK-02`, `GUI-PORCH-02` (§11.3); `features/catalog.json` (DECK-001…005 re-pointed, ROOF-001…004, the GUI ids); scorecard stubs; the "How to use it" of `docs/building.md`; `PLAN.md`'s M11 row | Sonnet | `tests/Napkin.App.GuiTests/*`, `features/*.json`, `PlannedFeatures.g.cs`, docs | C, E, F, I |
| optional, later | Rake-fill studs (§5.6) as pieces; `joistDirection: along`; a typed pier-top height; a square rafter tail | Sonnet | — | H |
| M10 (#40–#43) | The **real** Connecticut rows for each table kind, read from the adopted text in the same task and cited; independent review (rules-engine-model §8.3) | Opus, then Fable review, data rule strictly | `packs/packs/us-ct-2022/…`, `golden/` | D, #158 |

---

## 11. Test plan

Feature ids: unit `DECK-001…005` (existing), `ROOF-001…004`; GUI `GUI-DECK-NN`, `GUI-PORCH-NN`.
Expectations are hand-derived from §9 with a `derivation` string, never regenerated
(`samples/README.md`). Values in inches; the file carries 1024ths (117" = 119808, 141 3/8" =
144768, 66 3/4" = 68352, 18 1/2" = 18944, 82 1/2" = 84480, 78 1/2" = 80384, 55 3/4" is ≈ and is
asserted as the exact rational 55 37/48 in units of 1/48", 50" = 51200).

### 11.1 The samples

`samples/deck-12x10.{design.md,scene.json,expected.json}` — §9.1–§9.3 exactly, formatVersion 11,
with the guard and the stair; its checks exercised under `us-zz-deck` (synthetic), and recorded
under the shipped Connecticut pack as **No data** for all but frost.
`samples/porch-12x10.{…}` — §9.4–§9.6: the three walls, the openings with fills, the roof, the deck
with Supports `zz-deck-and-roof`, no guard; expected records the frame of every wall, the roof's
pieces, the glazing ratio 6480 / 62205 5/48 as an exact rational and "10.4 %", and every check under
both packs.

### 11.2 Golden cases (unit)

Project (`Napkin.Core.Project.Tests`):
1. Format 11 round-trips `deck`, `roof`, `opening.fill`, `site.soilBearing`; each refusal of §7 is
   hit by one malformed file naming the field; a version-10 file is refused naming both versions.
2. `deck`, `roof` and `opening` survive every exhaustive switch (the reflection list of
   renovation's test 2).

Building (`Napkin.Modules.Building.Tests`):
3. `DECK-005` the deck frame: every piece and count of §9.2; the 22nd board's 1 7/8"; a deck 1/1024"
   off the wall face is "not against a wall"; a deck on two walls is refused.
4. Beam span and tributary area for 2, 3 and 4 posts, exact rationals; cantilever 12" shifts the
   beam and the area.
5. A wall at `z` = 36" on the deck: its frame, its opening's header check and its bracing line are
   identical to the same wall at `z` = 0.
6. `DECK-001` ledger: sized with the count 10; out of scope past the last band, at the band and
   1/1024" over; the flashing footnote rides with the result; No data under CT.
7. `DECK-002` joists and beam: passes, over by 1'-2" (a 12'-6" deck: joists 12'-3"), out of scope for Supports
   `zz-deck-and-roof` when the table lacks the row, input missing for species; the rafter use
   with snow 30 and with snow not entered.
8. `DECK-003` footing: 27.1 sq ft at 2000 psf → the row; 1999 psf takes the lower column; below the
   lowest column → out of scope; frost 42 of 42 passes, 41 short by 1", missing site value named;
   the CT suggestion's text is exactly Table R301.2's value and citation.
9. `DECK-004` guard: 8 posts, the three bay clears, 11/13/7 balusters and the three gaps of §9.3;
   an enclosed deck has no open edge; the stair opening splits the east run.
10. Stair: 5 risers of 36/5 exactly, 4 treads, the diagonal ⌈√2896⌉ rounded up to 53 7/8", the
    6' board; a 3-4-5 stair (rise 36, run 48) has the exact diagonal 60 and no ≈; riser count
    typed overrides the pack's.
11. Fill: a screen door at sill 0 frames as a door; glazing counts glass only.
12. `ROOF-004` glazing: §9.6's rational, both the 10.4 % and the 25.5 % cases; a porch with no
    roof drawn says "draw the roof first"; a ratio over 40 % says "over the line".
13. `ROOF-001` rafters: 10 × 141 3/8" exactly, no ≈; the same roof at 4 in 12 (rise 40) gives
    `⌈√(130 1/2² + 43 1/2²)⌉` = 137.56… → shown 137 9/16" ≈ (the implementer re-derives the integer
    square root and its rounding); the count at 24" spacing.
14. `ROOF-002` the cuts: 128 3/8" to the birdsmouth's plumb line, 13" to the tail, seat 3 1/2",
    plumb 35/24", HAP 307/48; the ledger's top 55 37/48 above the plates; the sentence text of §5.4
    asserted verbatim.
15. `ROOF-003` sheets 5, bundles 5, waste 10 % → 155.5 sq ft and 5 bundles; the triangle's area
    2827 53/96; the beam-on-posts low end: post length and beam span.

Rules engine (`Napkin.Core.RulesEngine.Tests`): each new kind's golden file format; the
`lower-bound` band's selection, gaps and overlaps; every load check of slice D hit by one bad pack.

### 11.3 GUI workflows (`[GuiWorkflow]`, ≥ 5 actions, keyboard and pointer)

- `GUI-DECK-01` draw the house wall, mark it Existing, **Shift+D** and drag the deck against it,
  type Depth 3'-0", pick lumber and post count by keyboard, read the panel's frame line; undo.
- `GUI-DECK-02` tick Guard and Stair, type their values, open **Ctrl+Shift+L**, assert the Deck
  section's post and baluster counts and the stair sentence; change the post count to 2 and assert
  the message bar's beam sentence under the synthetic pack.
- `GUI-PORCH-01` on the deck sample: **W** three walls on the deck, windows and **Draw → Screen**
  by pointer, Fill by keyboard, mark the front wall bearing, **Shift+R** on the deck, type "5 in
  12"; assert the rafter line and the Sunroom line; undo the roof.
- `GUI-PORCH-02` §9 end to end from a new sheet, asserting §9.5's rafter length and §9.6's
  percentage in the shopping list.

---

## 12. Risks and unknowns

1. **A deck under a roof-bearing wall is not a deck-table case.** Routing it through Supports is
   honest but only as good as the pack; the shipped CT pack has nothing, so every porch is No data
   until M10, and a real IRC deck table may declare no roof-carrying row at all — then the answer
   is Out of scope, get it engineered, and that is the right answer.
2. **The research did not read §R507.** Every deck table kind here is shaped from what #40–#43 ask
   for, not from the adopted text. When M10 reads the real text, a kind may need an input this
   schema lacks (a wet-service flag, a joist-to-beam bearing length); as bracing's note says, it is
   added then with its own load checks, not guessed now.
3. **Ledger fastening depends on what is behind the siding**, which napkin cannot see. The row's
   footnotes say what it assumes; a person with a brick-veneer house will read "prohibited" in a
   footnote and nothing more. The alternative — modelling the house's band joist — is an addition
   napkin is not doing.
4. **The rake triangle is a hole a builder hits on day one** (rule 4). It is reported with its
   size; if that is not enough, the optional slice frames it.
5. **Square roots on the grid.** Every diagonal is rounded up once and marked ≈; a person reading
   141 3/8" and 137 9/16" ≈ side by side must understand the second is a bound. The panel says
   "rounded up to 1/16"" beside every ≈ length.
6. **The ledger's height on the house wall is a claim about the house.** napkin does not know the
   eave or the second-floor windows; the panel says so every time.
7. **The `z` of walls on decks.** No code read for this note assumes world `z` = 0 for a wall, but
   the app's placement and the 3D view were not read line by line; slice B's test 5 is the gate.
8. **Panel crowding**: a deck's panel has fourteen inputs plus seven checks plus guard and stair.
   The checks fold as bracing's do; not a model risk.
9. **Decking has no stock lengths** (#155): the Deck section's decking line reads as trim's does
   until cited lengths land. Marc may find "22 boards 12'-0"" enough.
10. **Synthetic values look like code values.** Every synthetic result in the app under
    `us-zz-deck` carries the pack's own name "ZZ DECK (synthetic)" in its citation; the pack never
    ships and lives under `tests/`.

---

## 13. Decisions for Marc

Each in plain words, with the default I recommend, so a "yes" is enough.

1. **Where is the ground?** Recommended: in a deck drawing, height zero is the ground, and you type
   how high the deck's surface is above it (3 ft in the example). The house wall you draw as
   "existing" then sits at its own floor height. The alternative — zero is the house's floor and
   the ground is a negative number — reads worse in the panel.
2. **A deck must be drawn against an existing wall; freestanding decks wait.** Recommended: yes.
   The whole point of the first deck is the ledger to the house, and a freestanding deck gets
   different footing rules (Connecticut's own exception) that this note deliberately leaves
   unmodelled rather than half-modelled.
3. **Joists always run out from the house.** Recommended: yes for now; joists running along the
   house need two beams and no ledger, a different deck. The picker still shows the option, greyed,
   so nobody wonders.
4. **When you put a roof-carrying wall on the deck, napkin asks you to say so (in "what the deck
   supports") and the deck's span checks look for that case in the code tables.** Recommended: yes.
   Deck tables are written for a deck's own weight and people; a roof is more. If the table has no
   such row napkin says "get it engineered" — never a number. The alternative is to check the deck
   as if the porch weren't there, which is wrong quietly.
5. **A screen is a setting on a window or door — glass, screen, or solid — not a third kind of
   opening.** Recommended: yes. A screen *door* has no sill and frames like a door; a sliding
   glass door is glass and should count toward the sunroom percentage; one setting covers all of
   them. #187 said "screens as a kind of opening"; this is the same idea with a cleaner file.
6. **napkin offers the Connecticut frost depth (42", Table R301.2) with its citation, and you click
   to accept it.** Recommended: yes — the value is already in the pack's data, read from the state's
   document; napkin never fills it in for you, it offers. The alternative is typing 42 yourself
   from the same document.
7. **The roof stores its rise (a length), and the pitch is worked out and shown as "5 in 12".**
   Recommended: yes. You type "5 in 12" and napkin sets the rise; if you later make the deck 1" deeper
   the panel says the pitch is now "≈ 4.96 in 12" instead of silently moving the roof. Storing the
   pitch instead would make the rise a computed number, which the file format forbids.
8. **The sunroom percentage counts the porch's three walls (gross, openings included), the
   triangles above the side walls, and the roof's sloped area — not the house wall.** Recommended:
   yes; that is the definition's "the structure's exterior walls and roof". Leaving out the
   triangles would make the percentage look higher than it is.
9. **Every deck and roof member is your choice, and napkin checks it against the code's table
   (passes, or over by so much) — it never picks a joist for you.** Recommended: yes. Headers are
   sized for you because there is no other way to get one; a joist is something you choose, and a
   tool that silently swapped your 2x8 for a 2x10 would be surprising.
10. **Post count is typed by you, minimum two; napkin shows the beam span that gives and checks
    it.** Recommended: yes. Any default would be a guess about your beam; with two posts on a 12 ft
    beam the check will simply say the span is over, and you add one.
11. **Guard and stair pieces use napkin's layout (posts at most so far apart, balusters spaced to
    your gap), labelled as napkin's, and the code's numbers only ever come from a pack.**
    Recommended: yes; with no pack, the pieces still list and the checks say No data.
12. **The triangle above each side wall under the roof is measured and reported but not framed
    in this milestone.** Recommended: yes, with the follow-up slice ready when you want it.
13. **The stringer board is bought as the diagonal plus one tread, as napkin's allowance.**
    Recommended: yes; the alternative is you typing a board length.
14. **Shift+D for a deck and Shift+R for a porch roof.** Recommended: yes; both keys are free.
