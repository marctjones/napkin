# Walls, openings and their framing

Issue #18. What napkin means by a wall and an opening, how it works out the frame, how it checks
each opening's header against the adopted code, and what it does not do yet.

## A wall

A wall is a box, not a new kind of thing. It is a wall when it is on the layer called **Wall**, or
when it is itself called "Wall" (`samples/wall-with-window` predates the layer).

| Box size | Is the wall's |
|---|---|
| width | length |
| plan height | thickness: the depth of its studs |
| depth | height, bottom of the bottom plate to the top of the top plates |

Draw one with **Draw → Walls → Wall** or **W**, then drag its length. It is as thick as its studs are deep
(a 2x4 is 3 1/2", a 2x6 5 1/2", as the materials library carries PS 20-25 Table 3) and starts
8'-0" tall, a starting value to type over in the panel's Depth. **Draw → Walls → Wall, 2x6 studs** picks
the other member. A drag mostly north–south makes the wall turned a quarter, so its length is
always its width. The studs are the library lumber whose dressed width is the wall's thickness; a
wall of any other thickness is not framed, and the panel says so.

## An opening

A window or door is a box on the layer **Opening** (or called "Opening"), standing the way its
wall does, across the wall's whole thickness, flush with both faces. **Draw → Window** or
**Draw → Door**, then click on a wall: the opening is centred on the click and held there by a
`Flush` on each long face, an `EqualParam` on the thickness and an `AxisDistance` from the wall's
start corner (BLD-002), so it moves with the wall and slides when that distance changes. A door is
an opening whose sill is 0. Its width is typed on its dimension label or dragged; its height is the
panel's Depth and its sill the panel's Up. The starting sizes are not standards.

## The frame is derived, never stored

`FramingList.Of(sketch, library, options)` works the frame out from the walls and openings every
time. Nothing about framing is saved. With `t` the stud stock's thickness (1 1/2"), `L` and `H`
the wall's length and height, and `s` the spacing:

- One bottom plate and two top plates, each `L`, in one piece.
- Layout studs with their start face at `k·s` while `k·s + t ≤ L`, plus an end stud at `L − t`
  unless the last layout stud is already there. Each `H − 3t`. A 12'-0" wall at 16" has 0…128,
  nine, plus the end stud: 10.
- Each opening of width `w` at `a`: `j` jacks each side, `sill + height − t` long, and `k` kings
  outside them, `H − 3t`. A layout stud whose body touches `[a − (j+k)t, a + w + (j+k)t)` is left
  out. `j` and `k` are the code check's (per side); until it sizes the header they are **1, a
  placeholder**, said wherever the frame shows.
- A header `w + 2jt` long in the room from the opening's top to the top plates
  (`H − 2t − top`): the checked member's plies of the library lumber it names, bought on the
  shopping list. Not sized, it buys nothing and says so.
- A window gets a rough sill `w` long and cripples below, `sill − 2t` long, at the layout positions
  wholly inside the opening. A door gets neither; its bottom plate is bought whole and cut out.
- With a sized header of depth `d` (the lumber's dressed width), cripples above, `room − d` long,
  stand at the layout positions over the header.

Refused, with the reason in the panel: an opening wider than its wall, one whose kings and jacks
would stand past the wall's end, two whose studs overlap, one reaching into the top plates or
leaving no room for a header, a window sill under `2t`; a wall turned other than by right angles,
shorter than two studs, or no taller than its three plates.

**Stud spacing** is 16" on centre by default: a design default, not a code requirement. The panel
offers the spacings the library carries for walls (PS 2-18's Wall - 16 and Wall - 24). The choice
is the wall's, saved with the design (format 6) and undoable.

## The code check on an opening

`CodeCheck.Of(sketch, packs)` asks the rules engine for every opening's header, every time
anything changes; nothing is stored. The question is:

| Input | From |
|---|---|
| the code | the project's adopted code (**Project → Adopted code and site…**): a pack napkin found, locked to one revision or following the newest installed one |
| which table | the wall's **Side** in the part panel: the pack's exterior-bearing header table for an exterior wall, its interior-bearing table for an interior one (a pack without that table says No data, naming it) |
| what the wall supports | the wall's **Supports** in the part panel: one of the values the table itself declares (dashes read as spaces), never assumed |
| header span | the opening's rough width |
| site values | the project's ground snow load, wind speed, seismic design category, frost depth and building width, typed in the same window; an empty field is "not entered" |

The table's jack and king stud counts are read as **per side** of the opening. What the part panel
says for a selected opening, under **Code check**:

- **Sized**: "Header (2) 2x10, 1 jack stud and 2 king studs each side." and the citation line
  exactly as the engine gives it (edition, table, row, source); **How it was found** opens the band
  trace and footnotes. The frame uses the member and the counts; the header is bought.
- **Out of scope**: "This opening is beyond what Table … covers: *the limit, with its row*. napkin
  stops here: get this header engineered." No size is shown or bought.
- **Input missing**: which input is not entered and where to enter it (the wall's Supports, or
  Project → Adopted code and site).
- **No data**: the engine's message, "Where to add tables: docs/rules-engine.md". With the shipped
  Connecticut pack this is every opening: its IRC base tables are not loaded
  ([rules-engine.md](rules-engine.md)). With no code chosen: "No code selected: choose one under
  Project → Adopted code and site."

Changing anything the answer depends on — resizing the opening, the wall's Supports, a site
value, the code or its lock — recomputes every opening, and the message bar says each result that
changed, the most serious first: "Header for Window 1 changed: (1) 2x8 → (2) 2x10, 1 jack and 2
king each side (Table … row …)", "Header for Window 1 is now beyond Table …: get it engineered".
Undo takes it back and says so again. A result that can no longer be computed becomes input
missing, no data or out of scope; nothing is kept from before. The shopping list's **Framing**
section names the code and each opening's result.

## A header row you entered by hand

When napkin says **No data** for a header because the adopted code's pack has no header table, a
person with their own copy of the code can put the one row their opening needs into the project:
the header, the jack and king studs each side, and where they read it — the code and its edition,
the table, the page and the row — with their name and the date. (The window to type it comes in a
later beta; a project file can already carry one.)

napkin uses that row for that opening only, and labels it **ENTERED BY HAND** everywhere it
appears: the part panel's headline starts with it, the citation line says who entered it, when and
from what, and *"Typed from a copy of the code; not napkin's data, not reviewed by napkin."*, and
the message bar and the shopping list's Framing section say the same. **How it was found** lists
the inputs it was entered for. The frame uses the member and the counts. napkin does not check the
row; apply your state's amendments yourself.

The row keeps the inputs it was entered for: the adopted code, the wall's side, what the wall
supports, the header span and the site values, each as it was, "not entered" included. The moment
any one changes — a wider window, another code, a site value typed where there was none — the row
stops applying: the check says No data again and names what moved, from what to what, and never
shows the row's number. Enter the row again for the new inputs, or remove it; undo brings it back.

If the adopted code's pack gains a header table, napkin answers from its own table and says the
entered row is superseded: an entered row never overrides napkin's own answer, even one that says
get it engineered. A wall marked not bearing keeps the row and does not use it.

## Wall bracing

Issue #39. Cutting or widening an opening shortens the solid wall beside it, and that can leave the
wall short of the braced length the adopted code requires — the check most DIY openings miss, and
one the header check cannot reveal. `BracingCheck.Of(sketch, packs)` runs it for every wall, every
time anything changes; nothing is stored but the methods the person chose.

**A wall line is one wall's solid segments**, start to end, between its openings and its two ends.
Each segment's length comes from the drawing, exactly. A segment is named by what bounds it: the
opening it starts after (or the wall's start) and the opening it ends before (or the wall's end):
"wall start to Window 1", "Window 1 to Window 2", "Window 2 to wall end". An opening flush with a
wall end leaves no segment there; overlapping or touching openings leave none between them.

**The person says what bracing is already on each segment.** Select a wall (or an opening in it):
the part panel's **Bracing** block lists every segment with its length and a picker of the adopted
code's bracing methods, by the names the pack gives them. Nothing is defaulted or inferred: a new
segment is "not braced" until someone chooses, and "not braced" counts for nothing. With no code, or
a code whose pack has no bracing provisions (the shipped Connecticut pack), the pickers are disabled
and say why. A method the current code does not have (chosen under another pack) is shown as it is,
"zz-board (not in this code)", and counts for nothing — it is re-flagged, never carried over. Each
choice is one undo step and is saved with the wall (format 8, `wall.bracing`, [file-format.md](file-format.md)).

**What happens to a choice when the drawing changes:**

- Widening, narrowing or moving an opening keeps every choice: its neighbours are still bounded by
  the same opening. Their lengths change, and so does the check.
- Deleting an opening (or moving it to another wall) merges the segments either side. The merged
  segment keeps their method if they all had the same one. If they had different methods it is
  **not braced**, and the message bar says so: "Wall 1: the segment wall start to Window 2 merged
  braced segments with different methods, so it is not braced now; choose its method again."
- Moving an opening past another, or adding one that splits a segment, makes segments with new
  boundaries: they are **not braced**, and the message bar says each one that replaced a braced
  segment. A new wall-bracing panel is never assumed from an old one.
- Undo puts the opening back and its neighbours' choices with it: the choices for segments that no
  longer exist stay saved until a method is next chosen on that wall, when the list is rewritten for
  the current segments.
- Duplicating a wall with its openings carries the choices onto the copies; a mirror copy across the
  wall's length turns them end for end. A wall copied without its openings is one segment, which
  keeps a method only if every segment had the same one.

**Reading the result** (under the pickers, and in red when the line falls short or is out of scope):

- **Passes**: "Braced length 10'-0" of 6'-6" required: passes (ZZ-BRACE.1)." with the citation line
  (edition, section, the base row and the source). **How it was worked out** opens every step: the
  base row, each factor with its condition and source, the exact required length and its rounding,
  and every segment's contribution with why ("shorter than the 2'-0" minimum panel … so it counts for
  nothing", "not braced: no method assigned", "capped at 6'-0"").
- **Short**: "Braced length 5'-0" of 6'-6" required: SHORT by 1'-6" (ZZ-BRACE.1)." The shortfall is
  what to add; the section is the one applied.
- **Out of scope**: "This wall line is beyond what Section … covers: *the limit*. napkin stops here:
  get the bracing engineered." Never a length.
- **Input missing**: which site value is not entered and where to enter it (Project → Adopted code and
  site).
- **No data**: with no code chosen, or a pack without bracing provisions. With the shipped
  Connecticut pack: "The loaded pack CT 2022 has no wall-bracing provisions, so napkin cannot check
  this wall line's bracing. Nothing is guessed: add them to the pack directory from your copy of the
  code (docs/rules-engine.md). Where to add tables: docs/rules-engine.md". No real bracing values
  ship ([rules-engine.md](rules-engine.md#wall-bracing)).

The request is the wall's length, its height (bottom plate to top plates), its segments in order
with their methods, and the project's site values. The arithmetic is entirely the pack's
([rules-engine.md](rules-engine.md#wall-bracing)).

**Every edit recomputes**, and the message bar says what changed with the edit that caused it:
"Set Window 1's width to 6'-1". Wall 1's braced line is now SHORT by 1'-6", braced 5'-0" of 6'-6"
required (ZZ-BRACE.1)." On the plan, a braced segment is a thin tinted strip down the wall with the
method's id beside it, and short ticks mark every segment's ends; unbraced walls are not marked.

**Switching the code** (Project → Adopted code and site…, or a following project taking a newer
revision) recomputes every header and every wall line under the new pack, and the message bar
leads with a summary: "Now checking against ZZ BRACE B (IRC 2099, pack us-zz-brace-b rev 1): every
result recomputed; 1 changed, 1 newly flagged, none can no longer be computed." — then each result
that changed, newly flagged first. A locked project does not change pack by itself; undo switches
back and says so again. (The saved result snapshot and the open-time comparison with an installed
newer revision are not built.)

### Side, bearing and the header you choose

Two per-wall inputs, **entered, never defaulted** ([`design/renovation-sketches.md`](design/renovation-sketches.md) §4.3),
under **Side** and **Bearing** in the wall's panel:

| Side | Bearing | What napkin does |
|---|---|---|
| exterior | bearing | asks the exterior-bearing table, as above |
| interior | bearing | asks the interior-bearing table |
| any | not bearing | **Not checked**: "Wall 1 is marked not bearing, so napkin does not size this header from the code. Header: (2) 2x6, your choice." The wall's **Header** picker (one to three plies of a library 2x) is the header over every opening in the wall — your choice, not a code result — framed with one jack and one king each side, said to be napkin's placeholder counts. No header chosen: it buys nothing and says so |
| not said | any | **Input missing**: "Say whether Wall 1 is exterior or interior (Part panel)." |
| any | not said | **Input missing**: "Say whether Wall 1 is bearing (Part panel)." |

The bracing check runs on every wall whatever its side or bearing. A bearing wall marked demolish
says "Removing a bearing wall needs an engineer; napkin does nothing here."

## Renovating: how to use it

From [`design/renovation-sketches.md`](design/renovation-sketches.md), the finished feature in one
place; the sections below say how each part works.

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

## Existing, new and demolished walls

Every wall and opening is **existing**, **new** or **demolish** (**Edit → Phase**, or the Phase
picker by the panel's Apply). Every check runs on the building as it will be — existing and new —
so a demolished opening is not in its wall's line, and a demolished wall has no line.

**The framing diff** (`FramingDiff.Of`) frames each wall as it will be and as it is and compares
the pieces by role, length and stock: what the wall will have beyond what it has is **new
material**, bought through the Framing section; what it has beyond what it will have **comes out**,
listed under the shopping list's **Demolition**. A new wall is all new; a demolished wall all
out; an existing wall with no change is on neither list; a window closed up (its opening marked
demolish) lists the studs that fill it as new and its header, kings, jacks, sill and cripples as
coming out. The frame of an existing wall is napkin's regular layout, not a survey of the real
wall, so every count out of one says "assuming a regular 16" layout in the existing wall". Put a
new window in an existing wall and the message bar says the diff with the edit: "In Wall 1
(existing): new — 2 king studs, 2 jack studs, header, sill, 4 cripples; out — 2 studs, assuming a
regular 16" layout in the existing wall."

**Corners.** Two walls join when an end of one lies on a long face of the other, or two ends
meet, or they overlap at a corner — decided from the boxes' exact corners, with no tolerance. The
plan draws a joined pair with no seam between them. Each wall is still framed alone: no corner
studs, blocking or nailers are counted. An opening whose width reaches into a joined wall's
thickness is refused: "it reaches the corner with Wall 2".

## Rooms and the area takeoff

A **room** is a box on the layer Room ([`design/renovation-sketches.md`](design/renovation-sketches.md)
§4.2, §5): its plan width and height are the inside length and width, its depth the ceiling height.
**Draw → Room** (Shift+W) drags one out, its edges landing on the wall faces within a grid step; a
click inside four walls takes their inside faces exactly; a click anywhere else makes a 10'-0"
square room to type over (a starting size, not a standard). It starts 8'-0" tall. A room is drawn,
never found from the walls, and is never framed, cut or bought as a box.

A wall **bounds** a room when one of its long faces lies on the line of a room edge and overlaps
it — exactly, no tolerance; a wall a little off is named in the panel ("not bounded by Wall 2 —
snap it to the wall"). The bounding walls' openings that lie within the room's edge are the room's.

The room's panel block takes the finishes and the values **you type from the packages** — napkin
carries no sheet size, coverage or stick length: drywall (walls and ceiling, or walls) and the sheet
size; insulation (exterior walls — the bounding walls whose Side is exterior — or all walls) by area
with a bag's coverage, or by stud bays; paint with the coats and a gallon's coverage; flooring with
its waste allowance (napkin's own 10 % to start, labelled so, editable, 0 allowed) and a box's
coverage; baseboard and the stick length. The shopping list's **Area takeoff** section works each
New room exactly in square units and rounds each line once: sheets by area in one pool for walls and
ceiling ("a layout may need more"), bags, gallons, boxes up, sticks up ("not allowing for corners or
waste"); the flooring area is shown rounded up to a whole square foot. Nothing typed: the area alone
and what to type. Stud bays are counted, not sized: the gaps between full-height members, less one
per opening, and the line says to buy by area for square feet.

**Measured** in the room's panel takes the four wall lengths and two diagonals of the real room:
unequal opposite sides or diagonals, or one diagonal whose square is not L² + W² (compared exactly),
say "out of square"; the takeoff stays on the drawn size and says so. Nothing is redrawn.

## Decks and porches: how to use it

From [`design/deck-and-porch.md`](design/deck-and-porch.md), in one place; **File → Samples → Building →
Porch on a deck** is the finished example.

- **Draw the house wall and mark it Existing** (Edit → Phase → Existing): a deck's ledger is the edge
  that lies on an existing wall's face.
- **Draw → Deck (Shift+D)** and drag out from that face. The Part panel's **Deck** block takes the
  joists, beam, posts, decking, what it supports, the species and the footing depth; tick **Guard**
  and **Stair**, and type the stair's **risers** when the adopted code gives no maximum riser.
- **Enclose it with W**: a wall drawn inside the deck's outline stands on the decking. Say the front
  wall is **Bearing**; put in windows with **Draw → Window** and screens with **Draw → Screen**, and
  set any opening's **Fill**.
- **Roof it: Draw → Porch roof (Shift+R)** and click the deck. Type the pitch as "5 in 12" in the
  **Roof** block; read the rafters, the cuts, the coverings, the rafter check and the **Sunroom test**.
- **Buy it**: Ctrl/Cmd+Shift+L — the **Deck** and **Roof** sections, with the sunroom line under Roof.
- Every check is a cited line, or says **No data** / out of scope and why. Under the shipped
  Connecticut pack the joists (and their cantilever) answer from AWC's DCA 6-2015 Table 2, the
  beam from its Table 3A and the posts and footings from its Appendix B Tables B1–B3, a guide (below);
  the ledger and rafter tables are **No data** until they land (#40, #209).

## A deck

A **deck is a box on the layer Deck** (or called "Deck", or carrying deck inputs): its plan outline
is the deck, its depth the walking surface's height above grade — **Z = 0 is grade** in a deck
drawing ([`deck-and-porch.md`](./design/deck-and-porch.md) §2). Its **ledger edge** is derived, never
stored: the one edge lying on a long face of an **Existing** wall, its whole length on that face,
judged from exact corners. No such edge says "not against a wall"; two say "against two walls: not
modelled". The other edges are **open** until a wall stands on them at the deck's surface.

The frame is derived every time (`DeckFrame.Of`): a ledger and a rim, each the deck's length along
the house; joists out from the house at the typed spacing (faces at k·s while k·s + t fits, then an
end joist), each D − 2t; one row of blocking at mid-span, a piece per bay; the beam's plies; the
posts, each the deck's height less the decking, a joist and the beam (pier top at grade, a stated
assumption); and the decking, the least number of boards whose widths and gaps cover the depth,
with how much of the last board shows. The **beam span** L_B is measured between post faces —
(W − posts × post width) ÷ (posts − 1), the clear span DCA 6's Figure 3 (p. 7) dimensions for its beam
tables; the beam ends flush with its end posts, so there is no overhang past an end post's outer face.
Each post's **tributary area** is measured as DCA 6 Appendix B does (pp. B1–B2; #41, #42): a middle post
(three or more posts, the one beside an end post, which carries the most) takes Eq. B-1, (½J_L + J_O) × B_L,
and an end post Eq. B-2, (½J_L + J_O) × ½B_L, half the middle post's. With no beam overhang B_L runs from
the next post's centreline to the deck's outside edge (the whole width with two posts); J_L runs from the ledger face to the rim's outside face, or with a
cantilever to the beam's centre, and J_O from there to the deck's edge. Both are kept as exact fractions
for the code checks and shown rounded, with ≈ when they are not on the grid. Every piece becomes a
cut-list row, so the shopping list buys decks as it buys walls.

**The deck's code check** (`DeckCheck`, deck-and-porch §3) looks each piece up in the adopted
code's deck tables ([rules-engine.md](rules-engine.md#deck-tables-198)): the joists' span, the
beam's span between post faces for the joists it carries, the ledger's fastening (with napkin's own
count, ⌈length ÷ spacing⌉ + 1), the posts' height — an **End posts** line (corner posts) and, with three
or more posts, a **Middle posts** line (centre posts), each the frame's post length from grade to the
beam's underside against its own table with its own area — and the footing under the most loaded post (a
middle one with three or more posts, an end one with two) on the site's **soil bearing** value, typed in
**Project → Adopted code and site** from the building department or a soils report and never defaulted
(until then the line asks for it). The post and footing lines name the area's equation and measures and,
when the table declares it, the centre-post factor: napkin's beam is one piece the deck's width long, so
continuous, and DCA 6 multiplies a centre post's area by 1.25 then (Table B2 note 4, Table B3 note 2) —
the larger area, the conservative reading: "Footings: 14" round or 13" square, 6" thick, for a middle
post's 29.6 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0" of beam, post centreline to the deck's
outside edge, × half the joists' 9'-10 1/2", ledger face to the rim's outside face) × 1.25, a centre post
under a continuous beam (DCA 6-2015 Table B3 note 2, p. B5 …) = 37.0 sq ft, on 2000 psf (…)". Each line is exactly one
of passes, short (by how much), sized, out of scope, input missing or no data, with its table, row
and page. **Frost** is napkin's comparison of two typed values — the deck's footing depth and the
site's frost depth — and the adopted code, when it prints a frost depth (Connecticut's Table
R301.2, p. 131: 42"), is **offered** with its citation ("CT 2022 says 3'-6" … — use it?"), never
applied until you press **Use it**. A bearing wall standing on a deck whose Supports is empty asks
you to choose what the deck supports. A deck with a **Cantilever** gets its own line: the overhang
past the beam may be the lesser of the joist row's own overhang and the table's fraction of the joist
span (DCA 6: L_O or L/4), both said, compared exactly; a table that prints no overhang says it does
not cover one. Under the deck block, napkin lists the words the adopted pack's tables and guide use
for **Supports** and **Species** — type one; nothing is filled in.

**Under the shipped Connecticut pack** (revision 5) the joist table is **DCA 6-2015 Table 2** (p. 4),
the beam table its **Table 3A** (p. 6), and the post and footing tables its Appendix B **Tables B1, B2
and B3** (pp. B3–B5), from the American Wood Council's *Prescriptive Residential
Wood Deck Construction Guide*, a guide on the 2015 IRC — not Connecticut's adopted code (the 2021
IRC), and the guide says the IRC governs where they differ. The block opens with that paragraph, every
joist and beam line carries the short clause and **UNREVIEWED** until the transcription is
independently checked, and the guide's scope is checked first: Supports must be `deck` (a porch roof
on the deck is out of scope: get it engineered), the site's ground snow load at most 40 psf, and the
deck no longer out from the house than it is wide. Species is one of the eight DCA 6 names (Southern
Pine, Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine),
read as each table's printed group (Table 2 prints three, Table 3A two). The beam is typed as plies
and lumber — "(2) 2x10" is Table 3A's "2-2x10", "(1) 4x8" its "4x8" — and Table 3A prints no solid
3x or 4x beam for Southern Pine. Its line reads the beam span between post faces, as Figure 3 (p. 7)
dimensions Table 3A's L_B, against the column for the joists it carries: "Beam (2) 2x10 on 3 posts, span
5'-6 3/4" between post faces, carrying 9'-7 1/2" of joists: allowed up to 7'-9" (DCA 6-2015 Table 3A row
r.sp.2-2x10.10, p. 6 …)". The posts: DCA 6 says "All deck post sizes shall be 6x6 (nominal) or larger"
(p. 10; item 3, p. 2, shown in the block's opening paragraph), and Tables B1 and B2 check it before they are
read, so a post under 6x6 nominal — the worked example's 4x4s, a 4x6, a 2x4 — is **out of scope** citing p. 10:
"End posts 4x4, 1'-6 1/2" from grade to the beam's underside, Southern Pine, each carrying 14.8 sq ft (…):
Beyond table B1: "All deck post sizes shall be 6x6 (nominal) or larger" (DCA 6-2015 POST REQUIREMENTS, p. 10).
Get it engineered." The tables' own 4x4 columns are transcribed as printed but never answer (#42; Appendix B is
an alternative to Table 4's heights and footings, not to the minimum post). On 6x6 posts the same deck reads
Table B1's ≤ 20 sq ft row for its 14.8 sq ft end posts, 14 ft (p. B3), and Table B2's ≤ 40 row for its middle
post's 29.6 × 1.25 = 37.0 sq ft, 14 ft (p. B4); both stand 1'-6 1/2" and pass. An 8x8 meets p. 10 but Appendix
B prints no 8x8 column, so it is out of scope naming the post column. A cell printed **NP** is out of scope,
quoting NP as the page prints it. The footing does not depend on the post's size: on 2000 psf it is Table B3's
≤ 40 sq ft row, 14" round or 13" square, 6" thick (p. B5), under 4x4 and 6x6 posts alike; below 1,500 psf is out
of scope. The ledger line is still **No data**: its table is M10's next slice (#40).

**Guard and stair** (§4, napkin's layout, not a code detail). Tick **Guard** and every open edge gets
posts at both ends of each run and evenly between (at most the typed spacing apart, a corner post
shared), two rails and a cap per bay, and the fewest balusters giving a gap no wider than the typed
one — the actual gaps, exact, are what the check reads. Tick **Stair** and it is laid out on the first
open edge: the risers typed, or the fewest the adopted code's maximum riser allows; each rise exact
(shown with ≈ when it is between sixteenths); the diagonal by integer square root, rounded up; and
the stringer board the diagonal plus one tread, napkin's allowance, in one sentence to lay it out by.
The pack's guard and stair provisions — when one is required, its height, its openings, the riser,
tread, handrail and width — are each a cited line, or "not covered by this pack", or No data.

**Screens and the 40 % line** (§5.2, §5.5). Every opening has a **fill** — glass, screen or solid —
stored on it: **Draw → Window** starts glass, **Door** solid, and **Draw → Screen** places a window
filled with screen; the panel's **Fill** changes any of them, and the frame does not change with it.
Under a porch roof napkin works out the glazing ratio IRC 2021 §R202's "sunroom" definition turns on
(read via UpCodes, docs/research/porch-rules.md): the glass openings in the walls standing on the
deck, over those walls' gross area plus the triangle above each side wall and the roof's sloped area,
to a tenth of a percent, and says which side of the 40 % line the drawing is on. It never
classifies; a category is yours.

**A shed porch roof** (§5.4, §9.5). **Draw → Porch roof** (**Shift+R**) and a click on a deck
make the roof over the deck's outline, at napkin's starting 4 in 12. Its high end is at the ledger.
Its low end is the wall standing on the deck's far edge; with no wall there, it is a (2) 2x10 beam
on two 4x4 posts, 8'-0" above the decking. A wall drawn with **W** inside a deck's outline stands on
the decking. The panel's **Roof** block takes the pitch typed as "5 in 12" (which sets the rise, on
the grid) and the rafter, spacing, ledger, overhang, sheathing and roofing. It shows the rafters,
the cuts, the coverings, the rafter check and the **Sunroom test**. The shopping list's **Roof**
section buys the pieces, with the sunroom line under it. A roof is a box on the roof layer or with
roof inputs. The box's depth out from the house is the run and its height is the rise, so its pitch is
"5 in 12", or "≈ 4.96 in 12" when it is not a whole number. The high end is a ledger on the house.
The low end is either a wall on the deck (napkin asks you to mark it bearing) or a beam on posts
standing on the deck. From the typed rafter, ledger, spacing and overhang, napkin works out, all
exactly:
- the rafter length, by an integer square root;
- the height above the plate;
- the ledger's top above the low support;
- the cuts, as one sentence to lay a rafter out by: the plumb cut, the birdsmouth distance and its
  notch depth, and the tail;
- the rafters, laid out like joists, and the blocking at the plate;
- the sheathing sheets and roofing units by sloped area, with "a layout may need more".

The rafters' horizontal span is checked against the adopted pack's rafter table at the site's
ground snow load: passes, short, or No data. As with the deck, napkin does not know the house, so
the panel asks you to check that the ledger clears the eave and any openings.

## The site plan

**Project → Site plan…** takes the lot's property lines as the survey prints them
([`permit-set.md`](./design/permit-set.md) §5), one course per line: a bearing and a distance,
`N 12°34'56" E 125.50'`, with an optional setback after it, `rear 30'`. It also takes the point of
beginning (where the survey starts, on the drawing) and **north**, in degrees clockwise from the
drawing's up; the bearings are measured from north. The corners are worked out from the courses and
rounded once each. When the courses don't close, napkin says by how much and never adjusts them, as
a survey reports its own closure.

For each line the window says how far the structure (any new or existing wall, deck or roof) is
from it, and compares that with the setback you typed: "Wall 1 to rear line 24'-8 1/2" (setback
20'-0": clear)". Distances are shown with ≈ unless the line runs along the drawing's axes. Setbacks
are zoning, not the building code: napkin compares only against what you type, and says nothing
about whether the lot conforms. On the plan the lot is drawn on the **Site** layer, with each
setback dashed inside its line and a north arrow at the point of beginning. This site plan is not
a survey.

**A survey image under the plan.** In the Site plan window, **Choose image…** puts a PNG or JPEG
(up to 8 MB) behind the plan, half transparent. Nothing snaps to it and nothing is traced from it.
To calibrate it:
1. Type the real distance between two points you can find on the survey, like `100'`.
2. Press **Calibrate**.
3. Click those two points on the plan.

The image is scaled and turned so they are that far apart. The window then says "Survey underlay:
survey.png, calibrated to 100'-0" between two points. This site plan is not a survey."

A design is saved as a **`.napkin` project** by default: the zip container, which carries the
survey image beside the drawing. A `.scene.json` still saves the drawing, but not the image, and
says so.

## Where it shows

- The part panel, with a wall or an opening selected: what it is, its sizes, an opening's code
  check, the wall's frame in one line ("8 studs, 2 king studs, …"), its openings, what the wall
  supports, its bracing (segments, methods and the check), and anything refused.
- The shopping list's **Framing** section: the pieces as boards to buy, through the same
  first-fit aggregation as the parts (`ShoppingList.Of` over `FramingList.CutRows`). A wall is
  never a row of the cut list.
- The 3D view: a wall is a solid; an opening is painted over it where it reads as a hole. It is a
  stand-in: it is painted over anything in front of the wall too.

## Limits

Plates longer than the longest stocked length are refused by the shopping list, not spliced.
Corners and intersecting walls are not framed as corners: each wall is framed alone, and no corner
studs are counted. No blocking, no sheathing, no fastening schedule. Girders are not here; removing
a bearing wall gets no beam, post or check. Each wall is its own braced wall line: lines made of
several walls, spacing between lines, and storeys are not modelled. A header member the
materials library does not carry is said and not bought. Whether the plies fit the wall's
thickness is not checked. A pack's newer revision on disk is used by a following project at once;
the saved revision is not rewritten until the person locks again (the open-time diff against a
stored result snapshot is M5's).
