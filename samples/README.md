# Sample designs (issue #37)

Two hand-crafted designs, in the M1 scene format documented in
[`docs/file-format.md`](../docs/file-format.md), and the expectations a test asserts against them.

| Fixture | Files |
|---|---|
| A coffee table | `coffee-table.design.md`, `coffee-table.scene.json`, `coffee-table.expected.json` |
| A wall with a window | `wall-with-window.design.md`, `wall-with-window.scene.json`, `wall-with-window.expected.json` |

`tests/Napkin.Core.Project.Tests` loads each scene with the #6 reader and asserts it matches its
`*.expected.json` exactly, in integer units.

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

- **No cut list and no materials list.** Those need finished sizes in three dimensions and the
  materials library (#7), neither of which exists; catalogue features `CUT-004` and `CUT-005` stay
  unclaimed until #8 and #9 can produce something to compare. The thicknesses and lengths the plan
  view cannot hold are written in each `design.md` and repeated in the expectations file under
  `statedNotInScene`, marked so that nobody mistakes them for something a test checks.
- **No header size, stud count or bracing length** in the wall fixture. Those expectations are
  added in M4 and M5 by a person reading the relevant row of Connecticut's published adopted text
  and citing the page it came from — never from memory, and never from napkin's own output.
- **No nominal-to-actual lumber sizes.** Every dimension in these fixtures is a finished dimension
  the design itself states.

## Opening these in the app

The viewer (#36) builds its M1 samples in code, because the reader and these files were written in
parallel. Wiring it to open these instead is one implementation of its own `IDesignSource`:

```csharp
LoadResult result = SceneReader.ReadFile(path);          // Napkin.Core.Project
return result switch
{
    Loaded loaded => Design.Unlabelled(name, loaded.Sketch),
    Refused refused => throw new DesignLoadException(refused.Summary),
    _ => throw new InvalidOperationException(),
};
```

`Refused.Summary` is written to be shown to a person as it is: one line per problem, each naming
the field, id, kind or value that was wrong.

Issue #37 also asks that these fixtures ship inside the app build, so the file dialog can open them
on a machine that has never seen this repository. That is a `Content` item over
`samples/*.scene.json` with `CopyToOutputDirectory` in `src/Napkin.App/Napkin.App.csproj`, which
belongs to the viewer; it is not done here.

**Entities carry no name.** `Design.Labels` — the viewer's part names — has nothing to read from
the file: the format stores ids, geometry and relationships only. The names in these fixtures live
in their `*.expected.json`. Putting a name in the scene file is a new field and a `formatVersion`
bump, which the cut list (#8) may well want; see `docs/file-format.md`.
