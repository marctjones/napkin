# Sample designs (issue #37)

Three hand-crafted designs, in the scene format documented in
[`docs/file-format.md`](../docs/file-format.md), and the expectations a test asserts against them.

| Fixture | Files |
|---|---|
| A coffee table | `coffee-table.design.md`, `coffee-table.scene.json`, `coffee-table.expected.json` |
| A wall with a window | `wall-with-window.design.md`, `wall-with-window.scene.json`, `wall-with-window.expected.json` |
| A table with rounded corners | `rounded-corner-table.design.md`, `rounded-corner-table.scene.json`, `rounded-corner-table.expected.json` |
| A bookcase (#101) | `bookcase.design.md`, `bookcase.scene.json`, `bookcase.expected.json` |
| A bench with one box standing for two rails (#101) | `bench.design.md`, `bench.scene.json`, `bench.expected.json` |
| One beam placed four ways (#101) | `lying-beam.design.md`, `lying-beam.scene.json`, `lying-beam.expected.json` |
| Five slats, each flush to the last (#101) | `chain-of-five.design.md`, `chain-of-five.scene.json`, `chain-of-five.expected.json` |
| Awkward fractions, incl. an off-grid size (#101) | `fraction-stress.design.md`, `fraction-stress.scene.json`, `fraction-stress.expected.json` |
| A 40-foot plate and wall with a 1/32" shim (#101) | `scale-extremes.design.md`, `scale-extremes.scene.json`, `scale-extremes.expected.json` |
| 25 studs at 16" on centre and two plates (#101) | `framing-16-oc.design.md`, `framing-16-oc.scene.json`, `framing-16-oc.expected.json` |
| An L-bracket, a distinct feature on every side (#101) | `l-bracket.design.md`, `l-bracket.scene.json`, `l-bracket.expected.json` |
| Two boards in the same place and a peg through both (#101) | `overlap.design.md`, `overlap.scene.json`, `overlap.expected.json` |
| A new 3 ft window in an existing 12 ft exterior wall — renovation example 2 (#161) | `window-in-existing-wall.design.md`, `window-in-existing-wall.scene.json`, `window-in-existing-wall.expected.json` |
| Finishing a 12 × 14 ft basement room — renovation example 1 (#162) | `basement-room.design.md`, `basement-room.scene.json`, `basement-room.expected.json` |
| A picture frame, four 45° mitres cut to the long point (#101, #97) | `picture-frame.design.md`, `picture-frame.scene.json`, `picture-frame.expected.json` |
| A bench whose every part names its stock, for the shopping list (#9) | `stocked-bench.design.md`, `stocked-bench.scene.json`, `stocked-bench.expected.json` |
| A bench on four legs leaning one way — angled parts, plain mitres (#191) | `splayed-bench.design.md`, `splayed-bench.scene.json`, `splayed-bench.expected.json` |
| A footstool on legs leaning two ways (#191) | `splayed-footstool.design.md`, `splayed-footstool.scene.json`, `splayed-footstool.expected.json` |
| A chair frame with raked rear legs and rails flush to them (#194) | `raked-chair-frame.design.md`, `raked-chair-frame.scene.json`, `raked-chair-frame.expected.json` |
| A shelf tilted between two sides (#194) | `angled-shelf.design.md`, `angled-shelf.scene.json`, `angled-shelf.expected.json` |

`tests/Napkin.Core.Project.Tests` loads each of the first two scenes with the #6 reader and asserts
it matches its `*.expected.json` exactly, in integer units, and checks every box of all three —
its plan position and size and, since format version 4, its height, depth and face-up;
`tests/Napkin.Modules.Furniture.Tests` does the same for all three fixtures' cut lists, and for the
rounded-corner table's outline.

**The rounded-corner table is the shaped-part fixture** (`docs/design/shaped-parts-model.md` §8).
The coffee table is left exactly as drawn — five plain rectangles, square corners — because it is
Marc's design and a feature does not get to edit it; a shaped part earns its own sample instead.
The new one repeats the coffee table's frame so that the one thing that differs, a 1" radius at
each of the top's four corners, is legible against a familiar shape. Its expectations carry the
top's outline as well as its cut-list row, walked by hand from §1.5's rule.

**The stocked bench is the shopping list's fixture** (#9). Every part names its stock — 2x4 legs
and stretchers, 1x4 aprons and end rails, a 3/4 plywood top — so its cut list's five rows become
three lines to buy, and each 6' 1x4 carries an apron and an end rail: several parts from one
board. Its `shoppingList` and `shoppingListCsv` are worked by hand (first-fit decreasing over the
library's stocked lengths, board feet summed exactly and rounded once), each row with its
`derivation`; `SampleShoppingListTests` holds them and `GUI-CUT-04` reads them on screen. Its
`sheetLayout` (#26), like the one in `diy-coffee-table-drawers.expected.json`, is the plywood laid
out on its sheets by hand, strip by strip and piece by piece, by the rules in
`docs/design/parts-and-cut-list.md` §4.2; `CutLayoutSampleTests` holds both.

**The splayed bench and the splayed footstool are the angled-part fixtures** (#191,
`docs/design/angled-parts.md` §9.1–§9.2). Their legs are struts, stored by their two ends, so their
expectations carry no box bounds for them — only the cut list and its CSV, each leg's length worked
out from a Pythagorean triple chosen so that it is exact (7-24-25; 3-4-12-13), with the arithmetic in
each row's `derivation`. The footstool keeps its legs' wide faces vertical, so its ends are plain
mitres; the compound board the same four points make with the wide face turned is §9.2's
operation-order test and lives in the unit tests, not in a sample's headline number.

The ten before it are the **purpose-built situations** of issue #101 (the conflict case cannot be a file, see below; the picture frame is also #97's shaped-cut fixture, and its rows carry the mitre sentences worked out by hand from `docs/design/shaped-parts-model.md` §4.4). Their
expectations add each box's world-space `minUnits`/`maxUnits`, the design's `overall` size and the
`totalVolumeCubicUnits` of the cut list, all worked out by hand; `SampleSetTests` in
`tests/Napkin.Modules.Furniture.Tests` holds them. Being files in this folder they also appear in
the app's Samples menu, where they can be opened in the 3D view.

## The rule

**Expectations are hand-derived and are never regenerated from napkin's output.**

Every number in an `*.expected.json` is worked out from the dimensions written on the design plus
arithmetic, and each one carries a `derivation` string showing that arithmetic so a reviewer can
re-check it without running anything. An expectation copied from napkin's own output proves
nothing: it would agree with the code whatever the code did.

If a test fails, the first question is which of the two is wrong. Change the expectation only
after re-doing the arithmetic by hand and writing the new derivation down.

## How to re-derive a number

1. **Lengths are integers in 1/1024 of an inch** (`docs/design/geometry-model.md` §1.1). Inches ×
   1024 gives units; units ÷ 1024 gives inches. Every dimension in these fixtures is a multiple of
   1/4", so every unit value is a whole number and nothing rounds.
2. **Positions are the `anchor` of a box** — its south-west corner in its own frame — with X to
   the right and Y up the page (§2.1). A box occupies `anchor.x .. anchor.x + width` and
   `anchor.y .. anchor.y + height`; both fixtures are at rotation 0, so corners are plain sums.
3. **Label text** is the feet-inch-fraction rendering of the measured length at 1/16" precision
   (§1.5): feet, then `'-`, then whole inches, then a reduced fraction, then `"`. Whole inches are
   always shown when feet are shown (`6'-0 5/16"`, not `6'-5/16"`; §10.1); under a foot the feet
   are not shown at all (`2 1/2"`), which the design document leaves open and
   `src/Napkin.Core.Geometry/LengthFormat.cs` settles. Every value here is exact at 1/16", so no
   label is marked approximate.
4. **A dimension never stores a number.** Its value is computed from the geometry it measures
   (§3.3): a `boxWidth`/`boxHeight` measurand reads the box's stored size, and an `axis` measurand
   is `to − from` along that axis. The `valueUnits` in the expectations file is that computed
   value, and `text` is its rendering.

## What is deliberately **not** here

- **No materials list, except the stocked bench's.** No part of the coffee table or the other
  fixtures names a stock, deliberately (see the last bullet), so their shopping lists would report
  every part under "no stock chosen". The shopping list's worked example is `stocked-bench` (#9,
  below) rather than an edit to the coffee table.

  The **cut list** is here, as of #8: `coffee-table.expected.json`'s `cutList` is four rows
  re-derived by hand from `coffee-table.design.md`, each with its own `derivation`;
  `rounded-corner-table.expected.json`'s is the same four rows re-derived again, with the
  sentence its top's rounded corners read as; and `wall-with-window.expected.json`'s is empty
  because neither of its boxes is a part. The
  thicknesses and lengths a single 2D view cannot hold are still written in each `design.md` and
  repeated under `statedNotInScene`; the scene itself holds all three dimensions, as each box's
  `depth` alongside its length and width.
- **No header size, stud count or bracing length** in the wall fixture. Those expectations are
  added in M4 and M5 by a person reading the relevant row of Connecticut's published adopted text
  and citing the page it came from — never from memory, and never from napkin's own output.
- **No nominal-to-actual lumber sizes written here.** Every dimension in these fixtures is a
  finished dimension the design itself states. The stocked bench states its parts at its stocks'
  dry sizes, and those sizes and the stock lengths its shopping list buys in are the shipped
  materials library's rows, cited there, not numbers written from memory.

## Opening these in the app

**These files are the viewer's samples.** The viewer (#36) built its own copies in code while the
reader and these files were being written in parallel; it does not any more. `src/Napkin.App`
copies `samples/*.scene.json` into its build output and into a `dotnet publish` layout with a
`Content` item, and its Samples menu opens them through `FileDesignSource`, which is one
implementation of `IDesignSource` over `SceneReader`:

```csharp
LoadResult result = SceneReader.ReadFile(path);          // Napkin.Core.Project
return result switch
{
    Loaded loaded => Design.Unlabelled(name, loaded.Sketch),
    Refused refused => throw new DesignLoadException(
        refused.Summary,
        refused.Problems.Select(problem => problem.ToString())),
    _ => throw new InvalidOperationException(),
};
```

`Refused.Summary` is written to be shown to a person as it is, and `Refused.Problems` is the same
thing line by line: the viewer lists every one of them in a panel over the drawing, which is
`GUI-VIEW-05`. A file dialog on a machine that has never seen this repository therefore opens the
same bytes this directory holds.

**The expectations are the viewer's too.** `tests/Napkin.App.GuiTests` reads each fixture's
`*.expected.json` for the dimension strings its workflows assert and for the positions its parts
must be drawn at, so one set of hand-derived numbers is what both the reader and the viewer are
held to. It reads them from here, in the repository, rather than from the build output: they are
the answers, not something the application ships.

**Entities carry a name, and boxes carry a part.** Scene format version 2 (#8) added both, so the
names that used to live only in `*.expected.json` are now in the scene files too and
`Design.Labels` — the viewer's part names — is filled straight from the file. The expectations
file still states them, and the reader test asserts the two agree: the fixture is the answer and
the scene is what is being checked, which is the wrong way round only if they are allowed to
disagree silently.

The coffee table's nine boxes are all parts; the wall and its opening are not, so they carry
`"part": null`. A part's third dimension — the one a plan view cannot hold — is its box's `depth`
(scene format version 4; it was the part's `outOfPlane` until then), and every value of it here is
one of the three numbers already listed under `statedNotInScene`: 3/4″ = 768, 16 1/4″ = 16640 and
3 1/2″ = 3584 units. Nothing was invented to make a cut list possible.

**Every box is placed in space** (format version 4): an `anchor.z`, a `depth` and a `faceUp`.
In the two tables every value is arithmetic on the stated dimensions — legs on the floor, the
top's underside on the legs' tops, the aprons' upper edges flush under it — derived by hand in each
`design.md` and in each box's `derivation`; no relationship holds any of it in Z. **The wall's
are placeholders**: its design states no wall height, opening height or sill, so the wall and the
opening carry `z` 0 and `depth` 768, what the version-3 reader gave a box that was not a part,
until the design states real ones (`wall-with-window.design.md`).

**Format version 5 (joinery, #144) changed no sample's meaning.** Every scene here gained the version
number and the three empty lists the format now requires (`"hardware": []` on every part,
`"fastenerChoices": []` and `"supplies": []` at the root) and nothing else; no sample has a joint, so
every expectation in every `*.expected.json` is the hand-derived number it was, with only its
`formatVersion` changed. The first sample with joints is the DIY coffee table (#146), whose
expectations are derived by hand from `docs/design/joinery-and-fasteners.md` §2 and §6.5.

**Format version 6 (the code check's inputs, #18/#19) changed no sample's meaning.** Every scene
gained the version number, `"wall": null` on every box, and `"code": null` with an all-`null`
`"site"` at the root (nothing entered), and nothing else; every `*.expected.json` changed only its
`formatVersion`.

**Format version 8 (wall bracing, #39) changed no sample's meaning.** A wall's inputs gained
`"bracing"`; no sample has a wall with inputs (every box says `"wall": null`), so every scene and
every `*.expected.json` changed only its `formatVersion`. Format version 7 (`site.roofLiveLoad`)
likewise added only the null field and the version.

**Format version 9 (the rough mark, `docs/design/sketch-mode.md` §4.1) changed no sample's
meaning.** Every part gained `"rough": false` — no sample is rough — by `samples restamp`, which
also rewrote each scene through the real `SceneWriter`; every `*.expected.json` changed only its
`formatVersion`.

**Format version 10 (renovation, `docs/design/renovation-sketches.md` §7) changed no sample's
meaning.** Every entity gained `"phase": "new"`, every box `"room": null`, and every wall that has
inputs `"side": null, "bearing": null, "header": null`, by `samples restamp`; every
`*.expected.json` changed only its `formatVersion`.

**Format version 11 (angled parts, `docs/design/angled-parts.md` §7) changed no sample's
meaning.** It added the `strut` entity and the `strutEnd`, `strutFace` and `strutEndFace`
references, which no sample holds; every scene and expectation changed only its `formatVersion`.

**A purely additive bump — a null field or an empty list, no sample's meaning changed, as every
bump from version 5 on has been — no longer needs 15 scenes and 15 expectations rewritten by hand
(#181).** Run

```
dotnet run --project tools/Napkin.Tools -- samples restamp
```

after bumping `FormatStamp.CurrentVersion` and teaching the writer the new field. It brings every
`samples/*.scene.json` and `*.expected.json` still behind up to the current version — adding
exactly the fields that version introduced, as null or empty, the same way this file has described
each bump by hand above — and verifies the result with the real strict reader before writing it, so
a sample it touches is provably still readable. It changes nothing in a sample already at the
current version, so running it after there is nothing left to bump is a no-op with no diff. It
refuses (rather than guessing) a sample older than format version 4, since versions 2-4 changed what
existing fields *mean*, not just added new ones; a bump like that is still a by-hand rewrite, the
way the samples were rewritten for those versions. See `SamplesCommand` in
`tools/Napkin.Tools/Commands/` for the exact per-version field list.

**Per-view camera state is a user setting, not scene data, decided ahead of the milestone that will
tempt it (#181).** M6's per-view camera memory and Parts view (#127, #128) are exactly the kind of
feature that invites "while we're adding a view, just save what it was looking at" into
`scene.json`. It belongs in `UserSettings.cs` instead: it is what a person set for themselves while
looking at a design, not a fact about the design, and it should not force every collaborator's
saved file to carry it or every format bump to touch 15 samples for a field nothing else needs. See
`docs/file-format.md` for where this is recorded for the format itself.

**Layers are named "Default".** The viewer styles a part by the name of the layer it is on — a
part on "Parts" is drawn as furniture, one on "Wall" as a wall, one on "Opening" as a dashed hole —
so everything in these two fixtures draws in the neutral style. Giving the coffee table's layer the
name "Parts", and splitting the wall's into "Wall" and "Opening", would be a change to these
fixtures rather than to the viewer, and is #37's to make.
