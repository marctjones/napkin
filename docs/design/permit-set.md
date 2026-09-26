# Permit set: the sheets for a deck or a window move, and the site plan they start from

Status: **DRAFT awaiting Marc's sign-off.** Milestone **M13 Permit set**: this note is #212's
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
- It does not print a set whose results are not all Sized without the banner (§3).

## 8. Slices

| Slice | What | Waits on |
|---|---|---|
| **A** | Site plan model (§5.2, §5.3, §5.5): boundary entity from courses, closure error, setbacks, distances, north; the Site layer; scene format bump; panel and plan drawing; GUI workflow | sign-off |
| **B** | Survey underlay (§5.4): container `assets/`, calibration, drawing it behind the plan | A |
| **C** | Sheet framework on #25: title block, banner, scale choice, Letter/Tabloid | #25 |
| **D** | Deck set: S1, A2, S2, S3, C1 | C, M11 (B–F) |
| **E** | Window set: S1, A1, A2, S3, C1 | C |

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
