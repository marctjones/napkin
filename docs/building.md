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

Draw one with **Draw → Wall** or **W**, then drag its length. It is as thick as its studs are deep
(a 2x4 is 3 1/2", a 2x6 5 1/2", as the materials library carries PS 20-25 Table 3) and starts
8'-0" tall, a starting value to type over in the panel's Depth. **Draw → Wall, 2x6 studs** picks
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
| the code | the project's adopted code (**Edit → Adopted code and site…**): a pack napkin found, locked to one revision or following the newest installed one |
| which table | the pack's header table for exterior bearing walls, the only walls napkin draws yet |
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
  Edit → Adopted code and site).
- **No data**: the engine's message, "Where to add tables: docs/rules-engine.md". With the shipped
  Connecticut pack this is every opening: its IRC base tables are not loaded
  ([rules-engine.md](rules-engine.md)). With no code chosen: "No code selected: choose one under
  Edit → Adopted code and site."

Changing anything the answer depends on — resizing the opening, the wall's Supports, a site
value, the code or its lock — recomputes every opening, and the message bar says each result that
changed, the most serious first: "Header for Window 1 changed: (1) 2x8 → (2) 2x10, 1 jack and 2
king each side (Table … row …)", "Header for Window 1 is now beyond Table …: get it engineered".
Undo takes it back and says so again. A result that can no longer be computed becomes input
missing, no data or out of scope; nothing is kept from before. The shopping list's **Framing**
section names the code and each opening's result.

## Where it shows

- The part panel, with a wall or an opening selected: what it is, its sizes, an opening's code
  check, the wall's frame in one line ("8 studs, 2 king studs, …"), its openings, what the wall
  supports, and anything refused.
- The shopping list's **Framing** section: the pieces as boards to buy, through the same
  first-fit aggregation as the parts (`ShoppingList.Of` over `FramingList.CutRows`). A wall is
  never a row of the cut list.
- The 3D view: a wall is a solid; an opening is painted over it where it reads as a hole. It is a
  stand-in: it is painted over anything in front of the wall too.

## Limits

Plates longer than the longest stocked length are refused by the shopping list, not spliced.
Corners and intersecting walls are not framed as corners: each wall is framed alone. No blocking,
no sheathing, no fastening schedule. Every wall is checked as an exterior bearing wall; interior
bearing walls, girder and bracing checks are not here (bracing is #39). A header member the
materials library does not carry is said and not bought. Whether the plies fit the wall's
thickness is not checked. A pack's newer revision on disk is used by a following project at once;
the saved revision is not rewritten until the person locks again (the open-time diff against a
stored result snapshot is M5's).
