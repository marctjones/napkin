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

Early scaffold, pre-1.0 beta. The solution structure builds; almost none of the actual
functionality (geometry, the rules engine, the UI) exists yet.

The work is organised into five milestones, each of them something you can hold rather than a
layer of the architecture — **M1 Look** (open a sample design and look at it), **M2 Draw** (draw,
resize by typing a dimension, undo, save), **M3 Cut** (build the coffee table and get its cut list
and shopping list), **M4 Check** (put a window in a wall and get a header size with the code row it
came from), **M5 Brace and compare** (the bracing check, and switching a project's adopted code).
What each one gets you, and which issues build it, is the milestone table in
[`PLAN.md`](./PLAN.md); the same milestones are on the issue tracker.

napkin is a **beta indefinitely**: every release is a pre-release, breaking changes are always
allowed, and there is no migration path between betas (an older project file gets a clear
"unsupported version" error, not a conversion). Each milestone earns a tagged pre-release when it
is done, and its version number is assigned then rather than planned. See
[`DESIGN.md` §12](./DESIGN.md#12-versioning-and-releases-beta-policy).

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
  Napkin.Core.RulesEngine     # prescriptive tables keyed by adopted code + evaluation
  Napkin.Core.Project         # project file format: scene graph, serialization
  Napkin.Modules.Furniture    # parts, joinery, cut lists, materials lists
  Napkin.Modules.Building     # sites, walls, openings, decks — consumes the rules engine
  Napkin.Interop.Dxf          # DXF import/export (interop with FreeCAD/QCAD/LibreCAD)
  Napkin.Interop.SketchUp     # read-only .skp import
  Napkin.Interop.Pdf          # true-scale vector PDF export
  Napkin.App                  # Avalonia UI (Windows + macOS)
tests/
  Napkin.Core.RulesEngine.Tests   # golden tests against each state's published adopted text
  Napkin.Core.Geometry.Tests
```

`Core.*` and `Modules.*` have no UI dependency — the rules engine in particular is meant to be
testable and auditable independent of the app around it.

## License

[GNU Affero General Public License v3.0](./LICENSE) for this project's own code. Dependencies are
kept to permissive (MIT/Apache-2.0/BSD) or weak-copyleft (LGPL/MPL) licenses only — see
[`DESIGN.md` §2.1](./DESIGN.md#21-license-and-dependency-policy-decided) for the reasoning.

## Scope note

Four adopted codes are wanted (DESIGN.md §11), and they arrive one at a time. The **2026
Connecticut State Building Code** (IRC 2024 base, effective September 18, 2026) is encoded first,
in M4; the 2022 Connecticut code follows in M5, which is what makes code locking demonstrable —
a project stays locked to the code in force when its permit was applied for, and switching a
project's code recomputes every result. Massachusetts 780 CMR 10th edition and the Pennsylvania
Uniform Construction Code come after that. The rules engine is data-driven and keyed by adopted
code, not by IRC year, specifically so further jurisdictions are a data change rather than a code
change — see `DESIGN.md` for details. Imperial units only; 2D only.

This is not legal or engineering advice, and it is not a substitute for your local building
department's review or a licensed professional's judgment about site-specific conditions a
prescriptive table doesn't cover.
