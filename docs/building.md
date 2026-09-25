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
