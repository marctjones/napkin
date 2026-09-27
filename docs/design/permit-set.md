# Permit set: the sheets for a deck or a window move, and the site plan they start from

Status: **Signed off by Marc 2026-09-26** with the §9 defaults (in chat, recorded on #212). Milestone **M13 Permit set**: this note is #212's
step 2, and it also designs #21, the site plan. §8 lists the slices (#223–#227); none starts before
sign-off. Every sheet is a PDF page, so every slice after A also waits on #25. That work is
parked, and Marc picks it up.

What the sheets must carry comes from
[`../research/permit-sets.md`](../research/permit-sets.md). It read Connecticut towns'
published checklists and deck guides on 2026-09-26: Bloomfield, West Hartford, Simsbury and
Danbury, plus Wethersfield's plot-plan specification. Glastonbury's page was blocked, and
Manchester, Greenwich and Fairfield published no drawing checklist. This note names **sheets and
their contents**. It states no code number. Where a town's guide quotes a number (a footing
depth, a guard height), that number belongs to the adopted code's pack, cited, and never to this
note or to the sheet's template.

## 1. What the research says a set is

*Narrowed 2026-09-27 (Marc): the research now rests on **Bloomfield and Simsbury only**
([`../research/permit-sets.md`](../research/permit-sets.md)). The three deck drawings below stand on
those two towns' documents. Statements here that came from towns since dropped no longer bear on
napkin: West Hartford's copy count, Danbury's and West Hartford's item lists, and Wethersfield's
plot-plan scale and sheet size (§4, §9.4 mention it only to set it aside).*

**Deck.** Every town whose deck guide was read asks for three drawings:

1. **A plot plan** with the deck drawn to scale on the lot. West Hartford and Simsbury ask for
   it in their deck guides; Bloomfield asks for a certified plot plan for any exterior work.
2. **A framing plan**, dimensioned: deck size, joist size and spacing, beam size and count, post
   size, spacing and connections, and decking. All four towns ask for it.
3. **An elevation with details**: height above grade, footing size and depth, the ledger
   attachment and its flashing, guard and handrail heights and openings, and stair rise and run.
   West Hartford, Simsbury and Danbury name these items one by one.

Only West Hartford states a copy count. No deck guide states a scale or a paper size.

**Window or door move.** No town read publishes a checklist for this. Bloomfield's general
checklist asks for "a complete set of legible building plans" and engineering details for
structural elements, naming headers. The set napkin makes for it is therefore napkin's
proposal, built from the same sheet kinds (§9.2).

**Plot plan, precisely.** Wethersfield's specification is the only one read that states a scale
and a sheet size. It also asks for a licensed surveyor's A-2 survey. napkin cannot make that
survey, and §4 says so on the sheet.

## 2. The sheets

| Sheet | For | Draws from the model | Carries |
|---|---|---|---|
| **S1 Site plan** | both | the site plan (§5): property lines, setbacks, the structure's footprint, north | distances from the structure to each property line, and the survey the underlay came from |
| **A1 Plan** | window | the plan view of the wall and the opening, dimensioned | the opening's size and position, and the wall's length |
| **A2 Elevation** | both | the standard view (M2) facing the wall, or the deck's side | heights above grade or floor, with guard and stair for a deck |
| **S2 Framing plan** | deck | the deck frame (M11 `DeckFraming`): ledger, joists, rim, beam, posts, blocking | every member's size and spacing, and the post count and beam span |
| **S3 Details** | both | fixed detail blocks filled from the model: ledger attachment, footing, guard, stair for a deck; header and jacks/kings for a window | each block's sizes, and its code result |
| **C1 Code page** | both | every rules-engine result in the design | one line per result, with the code, table and row, and its status |

A deck set is S1, A2, S2, S3 and C1. A window set is S1, A1, A2, S3 and C1.

## 3. Results on paper

The rules engine's four results print as they read on screen, and a sheet never prints a bare
number:

- **Sized**: the size, followed by the citation in full (code, table, row), for example
  "(2) 2x10, CT 2022 Table R602.7(1) row …".
- **Out of scope**: the member is drawn with no size, and the note says "beyond the table:
  get this engineered", citing the limit.
- **Input missing**: "not sized: enter the ground snow load (Project → Adopted code and site)".
- **No data**: "not sized: the adopted code's pack has no table for this". The code page lists
  every one.

**A sheet with any result other than Sized carries a banner** across its title block: "NOT A
COMPLETE PERMIT SET — n items are not sized; see C1". The C1 code page is always printed, even
when every result is Sized.

### 3.1 Blanks and a worksheet page (decided 2026-09-27)

Marc, 2026-09-27: *"if we don't have code book values for these prints, can we leave the spaces
to fill in the missing data and add a printed page with instructions what is missing and a
worksheet for collecting it?"* Decided: yes, in addition to the banner, not instead of it.

- **On the sheet itself**, wherever a member's size would print, a result other than Sized draws
  a **blank line** (an underscore-style rule the right width for a nominal dimension, e.g.
  `______`) beside its label, instead of leaving the label bare or omitting the row. The row's
  usual note still prints beneath it exactly as §3 already specifies ("beyond the table: get this
  engineered", "not sized: enter the ground snow load…", "not sized: the adopted code's pack has
  no table for this") — the blank is for a value someone will write in by hand, on paper, after
  the sheet is printed; it is never something napkin fills in for them, on screen or off.
- **A new worksheet page, W1**, printed whenever any result is not Sized (alongside C1, never
  instead of it): one row per unsized item, each naming exactly what to look up — the adopted
  code, the table, and the inputs already known (span, snow load band, what the wall or deck
  member supports) — a blank line to write the answer, and a blank line for its citation (table
  and row, or "entered by hand" if that is what napkin already recorded via #245). W1 is the
  paper form of the same lookup #245's *Enter this row…* button does on screen; filling it out on
  paper and later typing it into napkin via #245 is the expected loop, not a competing one.
- The banner's wording gains "see C1 and W1" once W1 is added.
- This does not relax §7's rule that a set with any non-Sized result is marked incomplete. A
  filled-in worksheet is a person's own reading, not napkin's, and every value that reaches the
  screen from it still goes through #245's citation and "ENTERED BY HAND" tag — W1 does not let a
  blank line's answer join the sheet's own printed numbers unlabeled.

## 4. The title block

Every sheet has one title block, the same as #25's:

- the project name, the sheet number and title, the scale, and the date;
- the adopted code and pack revision, locked or following;
- **the scope disclaimer**: napkin sizes from prescriptive tables only, is not a design
  professional, and does not replace one. It also says that the site plan is not a survey
  (§5.4).

The paper is Letter or Tabloid, which the person chooses; napkin has no source for anything
else. The scale is a standard architectural or engineering scale, chosen to fit, and printed.
A town that states a scale, as Wethersfield does for its surveyed plot plans, is the surveyor's
concern, not napkin's (§9.4).

## 5. The site plan (#21)

### 5.1 What it is

A **site plan is a sheet-level drawing in the same project, on a layer named Site**. It holds four
things:

- the property lines;
- the setbacks;
- a north direction;
- optionally, a survey image underneath.

The structure's footprint is the design's own boxes, drawn in plan. It is never copied.

### 5.2 Property lines, from the survey

Property lines are a new entity, a **closed boundary**: an ordered list of courses, each stored
as the survey prints it. A course is a **bearing** (degrees, minutes and seconds with its
quadrant, "N 12°34'56" E") and a **distance** (a length). The `Angle` type already stores
arcseconds exactly (geometry-model §1.6).

The corners are **derived**. Starting at the point of beginning, which the person places, each
course's end is its start plus distance × (sin, cos) of the bearing. That is computed in
`double` and rounded to the 1/1024" grid once per corner. The **closure error** (the last
corner's distance from the first) is shown, as the survey's own closure is. napkin never
adjusts a survey to close; it reports the error.

### 5.3 Setbacks and distances

A setback is a distance the person types per boundary course, labelled front, side or rear. It
is drawn as the course offset inward.

For each boundary course, the sheet and the panel give the **distance from the structure** to
that course: the least distance from any corner of any New or Existing box on a building layer.
It is computed in `double`, rounded once, shown with ≈ unless the course runs along a world
axis, and compared with the setback: "Deck to rear line ≈ 32'-4" (setback 30'-0": clear)".

A setback is **zoning, not the building code**. napkin compares only against what the person
types, and the sheet says so.

### 5.4 The survey underlay

This slice bumps the **container version** and adds `assets/`, which the container already
reserves (`ContainerNames`, PRJ-006). An asset is a PNG or JPEG, named by its SHA-256, with a
limit stated beside `ContainerLimits`.

An underlay stores its asset, **two pixel points** on the image, and **two world points** with
the **distance typed between them**. The second world point lies along the direction the person
clicked, at exactly that distance, rounded to the grid.

The image's placement (scale, rotation and offset) is derived in `double` for drawing only. The
underlay is a picture behind the drawing: nothing snaps to it and nothing is traced from it.
Measuring between the two calibration points reads the typed distance. That is #21's acceptance.

The sheet prints "Survey underlay: <file name>, calibrated to <distance> between two points.
This site plan is not a survey."

### 5.5 North

North is stored as an exact `Angle` from the drawing's +Y axis, clockwise, and defaults to 0.
The bearings of §5.2 are measured from it. A north arrow is drawn on S1 and in the plan view
when the Site layer is visible.

## 6. File format

**Scene format +1:**
- a `boundary` entity (the point of beginning plus its courses, each a quadrant, an `Angle` and
  a length);
- `setbacks` on it, one per course, a length or null, with a label;
- `site.north` (an arcsecond count);
- `site.underlay` (null, or the asset hash, two pixel points, two world points and the typed
  distance);
- the layer **Site**.

**Container version +1:** `assets/<sha256>.png|jpg`, refused when unreferenced, over the limit,
or not a PNG or JPEG. No field is optional, as always.

## 7. What napkin does not do

- It does not make a survey, check a survey, or say whether a lot conforms to zoning.
- It does not trace an image or read a PDF survey.
- It does not decide which sheets a town wants. The set is §2's, and each town may ask for more.
- It does not print a set whose results are not all Sized without the banner and the W1 worksheet
  (§3, §3.1).
- It does not accept a worksheet's filled-in answer back into the project except through #245,
  cited and tagged like any other entered value.

## 8. Slices

| Slice | What | Waits on |
|---|---|---|
| **A** | Site plan model (§5.2, §5.3, §5.5): boundary entity from courses, closure error, setbacks, distances, north; the Site layer; scene format bump; panel and plan drawing; GUI workflow | sign-off |
| **B** | Survey underlay (§5.4): container `assets/`, calibration, drawing it behind the plan | A |
| **C** | Sheet framework on #25: title block, banner, scale choice, Letter/Tabloid, blanks on unsized results and the W1 worksheet page (§3.1) | #25 |
| **D** | Deck set: S1, A2, S2, S3, C1 | C, M11 (B–F) |
| **E** | Window set: S1, A1, A2, S3, C1 | C |

**Slice C as built (#225).** `Napkin.Interop.Pdf`: `PermitSetPdf` draws a set's sheets in #211's
frame (`SheetFrame`, `SheetSet`), each titled by its number ("A2 Elevation"), then **C1** and, when
anything is not sized, **W1**. `SheetPaper.Letter` is Excise.Core's; `SheetPaper.Tabloid` is 17 × 11 in,
"11 inches by 17 inches", "ANSI B under the ANSI/ASME Y14.1 standard" (Ricoh USA's glossary, *11x17
paper*, read 2026-09-27; the standard itself is not free to read). `SheetScale.Architect` and
`SheetScale.Engineer` are the scales USFA/FEMA's *Using Engineer and Architect Scales* lists (p. 2,
read 2026-09-27): 1 1/2" to 3/32" to the foot, and 1" = 10' to 60'; each prints as the rule reads it.
`PermitItems` classes every result sized, not sized or not checked (a wall said not to bear), in the
panels' words, and gives each unsized one its lookup: the adopted code and table, and the inputs
already known. The banner is `TitleBlock.Banner`, a band across every title block; blanks are
`SheetInk.Blank`, thin rules to write on. A deck line napkin writes itself when it cannot answer
carries `DeckCheckLine.Unanswered`. No menu item yet: the sets that use it are D and E.

**Slice D as built (#226).** `DeckSetPdf`: **S1** draws the lot's courses as `SitePlan` lays them, each
setback offset inward in the centre line's chain, the footprints of the New and Existing walls, decks
and roofs, and a north arrow, with each numbered line's distance and setback, the closure and the
zoning note at the side — or says how to enter a lot when there is none — at the largest architect
or engineer scale that fits. **A2** is the standard view facing the deck's open side (the side away
from its ledger), built by the app from the locked view's own calls, with each deck's height above
grade and its guard and stair lines. **S2** draws `DeckFrame`'s frame with the house at the top:
ledger, rim, every joist, the blocking row, and the beam and posts dashed beneath; width and depth
dimensioned. **S3** holds the ledger, footing and frost, guard and stair blocks. On S2, S3 and A2
each member prints its size, or a blank rule when its check has no answer, and its check's lines.
**File → Print permit set on Letter… / on Tabloid…** prints it for a design with a deck. IBM Plex has no
⌈ ⌉, so the ledger count's brackets print as "ceil(" and ")".

**Decided (Marc, 2026-09-27): the permit set targets typical Connecticut requirements only** — the
towns of §1. Other jurisdictions' specific asks (text-size minimums, a mandatory graphic scale bar and
the like) are out of scope; the sheets keep their text sizes and print the scale as a ratio and in words.

## 9. Decisions for Marc

Each has the default I recommend, so a "yes" is enough.

1. **The deck set is plot plan, elevation, framing plan, details, and a code page; the window
   set swaps the framing plan for a plan view.** Recommended: yes. It is what the deck guides
   read ask for, plus the code page that is napkin's reason to exist.
2. **For a window or door move, napkin proposes its own set,** since no town read publishes
   one. Recommended: yes. The sheets are named and headed so a building official can see what
   each is.
3. **A set with any unsized result prints a "not a complete permit set" banner** on every sheet
   and still prints. Recommended: yes. The alternative, refusing to print, blocks the person
   from taking a partial set in to ask questions.
4. **Letter or Tabloid only, with a standard scale chosen to fit.** Recommended: yes. No deck
   guide read states a scale. Wethersfield's 24"×36" at 1"=20' is for surveyed plot plans,
   which napkin does not make.
5. **Property lines are entered as survey courses** (bearing and distance), not drawn.
   Recommended: yes. A survey prints courses, and drawing lines over an image would copy its
   errors.
6. **The survey image is an underlay only**: nothing snaps to it and nothing is traced.
   Recommended: yes, per #21's scope.
7. **Setbacks are typed and labelled zoning**, never taken from a code pack. Recommended: yes.
   Zoning is local and not in the building code.
