# napkin

An open-source, drag-and-drop desktop CAD tool for homeowners doing their own furniture, deck,
and permit-adjacent building projects — no CAD training required.

It's meant to replace graph paper, a tape measure, and a photocopied code table for the kind of
project a homeowner with basic DIY skills reliably does themselves: a coffee table, a deck, moving
a window on an exterior wall, a shed-scale addition. Two things make it more than a drawing tool:

- **It sizes structural elements against the IRC's homeowner prescriptive-code shortcuts**
  (header spans, wall bracing, deck framing) so a straightforward project can be sized correctly
  without hiring a structural engineer — and it always cites the code edition, table, and row
  behind every number, rather than asserting a bare answer.
- **It generates cut lists and materials lists** from the same drawing, resolving nominal lumber
  and sheet-goods sizes (a "2x4" is really 1.5"×3.5") to their true dimensions.

napkin is not a substitute for a permit office, an inspector, or a licensed engineer. It's scoped
deliberately to the prescriptive-code band of projects — the moment a project's inputs fall
outside what a published table covers, the tool says so and stops, rather than guessing.

See [`DESIGN.md`](./DESIGN.md) for the full design and implementation document: vision, scope,
architecture, and the decisions made so far. Detailed designs live under
[`docs/design/`](./docs/design/), starting with the
[geometry model](./docs/design/geometry-model.md) (lengths, entities, relationships, the update
interface).

## Status

Early beta. M1 through M5 are done: open and draw a sample design with dimensions (M1); draw,
resize by typing a dimension, undo, save (M2); build the coffee table and get its cut list and
shopping list (M3, audited 0.93.0-beta); put a window in a wall and get a header size with the
code row it came from (M4, 0.97.0-beta); the bracing check and switching a project's adopted code
(M5, 0.101.0-beta). What's being built now is a Hardening pass — architecture drift, correctness
and doc fixes from a full-repo review — before M6 Views and drawings (six locked orthographic
views, a third-angle sheet, a Parts view).

The work is organised into milestones, each of them something you can hold rather than a layer of
the architecture — **M1 Look**, **M2 Draw**, **M3 Cut**, **M4 Check**, **M5 Brace and compare**,
**Hardening**, then **M6 Views and drawings**, **M7 Sketch mode**, **M8 Renovation**, **M9 Shape**,
and **M10 Real code**. What each one gets you, and which issues build it, is the milestone table in
[`PLAN.md`](./PLAN.md); the same milestones are on the issue tracker.

napkin is a **beta indefinitely**: every release is a pre-release, breaking changes are always
allowed, and there is no migration path between betas (an older project file gets a clear
"unsupported version" error, not a conversion). See
[`DESIGN.md` §12](./DESIGN.md#12-versioning-and-releases-beta-policy).

## Trying it

There are no downloadable builds; building and releasing installers is parked until the core
functionality exists. Run it from source (see [Building](#building)).

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (LTS, supported
through November 2028) — pinned in `global.json`.

```sh
dotnet build
dotnet test
dotnet run --project src/Napkin.App
```

## Project layout

```
src/
  Napkin.Core.Geometry        # platform-agnostic geometry: points, lines, dimensions, units
  Napkin.Core.Materials       # the materials library: stock lumber, panels, hardware, fasteners
  Napkin.Core.RulesEngine     # prescriptive tables keyed by adopted code + evaluation
  Napkin.Core.Project         # project file format: scene graph, serialization
  Napkin.Modules.Furniture    # parts, joinery, cut lists, materials lists
  Napkin.Modules.Building     # sites, walls, openings, decks — consumes the rules engine
  Napkin.Modules.Editing      # the editing model: design editor, undo, snapping, drawing tools
  Napkin.App                  # Avalonia UI (Windows + macOS)
tests/
  Napkin.Core.RulesEngine.Tests   # golden tests against each state's published adopted text
  Napkin.Core.Geometry.Tests
  ...                              # one test project per src/ assembly, plus Napkin.App.GuiTests
```

DXF, SketchUp and PDF interop are parked (backlog), not yet in the tree — the three stub projects
that once held their place were removed in Hardening (#182); they come back when that work starts.

`Core.*` and `Modules.*` have no UI dependency — the rules engine in particular is meant to be
testable and auditable independent of the app around it.

## Versioning

napkin is a pre-1.0 beta indefinitely. Every version is `0.N.0-beta`: the minor number goes up by
exactly one with each change landed on `main` (development is direct to `main`, no pull requests,
since 2026-09-22), and nothing is promised to stay compatible between betas. The version is
set in one place, [`Directory.Build.props`](./Directory.Build.props); see
[`DESIGN.md` §12](./DESIGN.md#12-versioning-and-releases-beta-policy).

## License

[GNU Affero General Public License v3.0](./LICENSE) for this project's own code. Dependencies are
kept to permissive (MIT/Apache-2.0/BSD) or weak-copyleft (LGPL/MPL) licenses only — see
[`DESIGN.md` §2.1](./DESIGN.md#21-license-and-dependency-policy-decided) for the reasoning.

## Scope note

Four adopted codes are wanted (DESIGN.md §11), and they arrive one at a time. The **2022
Connecticut State Building Code** (IRC 2021 as amended) shipped first, in M4 and M5 — the engine,
pack format, overlay, interpolation and picker are complete, and switching a project's adopted
code recomputes every result, which is what makes code locking demonstrable. The IRC 2021 base
tables themselves are bring-your-own until M10 transcribes them from a primary source (#158/#14).
The 2026 Connecticut code (IRC 2024 base) is not yet in force and is not yet encoded. Massachusetts
780 CMR 10th edition and the Pennsylvania Uniform Construction Code come after that. The rules
engine is data-driven and keyed by adopted code, not by IRC year, specifically so further
jurisdictions are a data change rather than a code change — see `DESIGN.md` for details. Imperial
units only; 2D only.

This is not legal or engineering advice, and it is not a substitute for your local building
department's review or a licensed professional's judgment about site-specific conditions a
prescriptive table doesn't cover.
