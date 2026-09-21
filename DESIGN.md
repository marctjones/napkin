# Homeowner CAD / Code-Aware Planning Tool — Design & Implementation Document

Status: draft for discussion. Author: project owner, with architecture recommendations from
Claude. This document is meant to seed the project repository (e.g. as `DESIGN.md`) and to be
revised as decisions get made — it is not a finished spec.

## 1. Vision

A free, open-source, drag-and-drop desktop app for Windows and macOS that lets a homeowner with
basic DIY skills — no CAD training — design two overlapping kinds of things in one tool:

1. **Furniture and built structures** (a coffee table, a deck) with automatic cut lists and
   material takeoffs.
2. **Small home construction changes** (a new/relocated window or door, a wall removed or added,
   a deck, a shed-scale addition) sized against the **prescriptive code shortcuts** the IRC
   provides specifically so a homeowner can avoid hiring a structural engineer — with the tool
   showing its work (code edition, table, row) rather than asserting a bare answer.

The tool is not a substitute for a permit office, an inspector, or an engineer. It is a substitute
for graph paper, a tape measure, and a photocopied code table — done with snapping, live
dimensions, and a calculation engine that won't let a mistake in arithmetic become a mistake in
the finished wall.

## 2. Goals and non-goals

**Goals**
- No CAD background required. Drag, drop, snap, resize by typing a dimension.
- One tool covers furniture design + cut lists *and* building-shell design + prescriptive code
  checks. These are different domains but share a geometry/dimensioning core (see §5).
- Every structural number the tool produces cites its source (code edition, table, row) and is
  auditable by hand.
- Firmly bounded scope: prescriptive-code-only. When an input falls outside what a table covers,
  the tool says "this needs an engineer," never extrapolates.
- Runs offline, no account, no cloud dependency. Homeowner's dimensions and site details are
  private by default.
- Open source, Windows + macOS native.

**Non-goals**
- Not a general-purpose parametric mechanical CAD tool (not competing with FreeCAD/SolidWorks).
- Not a licensed-engineer replacement. No full structural analysis, no load-path calculation
  beyond what a table already prescribes.
- Not a permit-submission platform. No jurisdiction integration, no e-filing.
- Not a multi-user / collaboration tool in v1. Single user, local files.
- **Not 3D in v1.** 2D plan/elevation with live dimensions ships first, deep and solid; 3D
  visualization is an explicit later phase (see §8), not a v1 requirement.

### 2.1 License and dependency policy (decided)

- **Project license: AGPL-3.0.** Deliberate choice — if someone stands this up as a hosted
  service, they owe their source back, same as GPL's copyleft but extended to network use. For a
  desktop app run locally this clause mostly sits dormant; it exists specifically to prevent a
  SaaS-wrapped fork from staying closed.
- **Dependency policy: permissive (MIT/Apache-2.0/BSD) or weak-copyleft (LGPL/MPL) only.**
  AGPL is compatible with GPL-family dependencies, but the policy here is stricter than what the
  license permits — the goal is to never take on a dependency whose terms could constrain reuse
  of this project's own code, not merely to stay legally combinable.
  - Avalonia: MIT. Clears the bar.
  - PDF export: **PdfSharp** (MIT), not QuestPDF — QuestPDF's "Community" license is
    revenue-gated, not a clean permissive license, and would violate this policy the moment the
    project (or a fork) crossed its threshold.
  - DXF import/export: **netDxf** or **ACadSharp** (both MIT).
  - Constraint solver: see §5.1 — SolveSpace's own solver is GPL-3.0 and is excluded by this
    policy despite being license-*compatible* with AGPL. PlaneGCS (FreeCAD's derivative) is LGPL
    and would clear the bar, but is a C++ library requiring native interop; v1 avoids the
    question entirely (§5.1).

## 3. Target use cases (drawn from the actual project list)

| Project | Domain | What the tool needs to do |
|---|---|---|
| Coffee table | Furniture | Parts, joinery reference, cut list, material takeoff |
| Bathroom remodel (no wall moves) | Building (light) | Fixture/room layout only; permit needs vary by jurisdiction for plumbing/electrical scope — tool doesn't gate this |
| Hallway rewire + recessed lighting | Building (light) | Lighting/outlet layout plan; no structural engine involved |
| Relocating a window or sliding door on an exterior wall | Building (structural) | Wall load attributes, header sizing (IRC R602.7), wall bracing check (R602.10) |
| Deck wrapping a ground-level, pad-mounted hot tub, ledger-attached at an existing sliding door | Building (structural) + site | Prescriptive deck framing (DCA6-style spans/footings), ledger attachment, guard rails, site/setback plan |
| Small addition or shed at the "optimistic, non-engineered" permitting tier | Building (structural) + site | Foundation/footing table, rafter/truss span table, tie-in detail, site plan |

The shared thread: everything above is either **pure geometry + material takeoff** (furniture,
deck boards) or **geometry + a prescriptive table lookup** (headers, bracing, footings, spans).
Nothing on this list requires custom structural engineering — which is the intentional scope
boundary from §2.

## 4. Core design principle: two domains, one geometry core

Furniture design and building design look different but are the same underlying problem: sized,
dimensioned, constrained 2D/extruded-3D objects, organized into a project, exportable as a cut
list or a dimensioned sheet. Rather than building two apps, the architecture should have:

- **One geometry/constraint kernel** — lines, arcs, rectangles, extrusions, snapping,
  parametric dimensions (see §7).
- **Two domain modules on top of it**, each contributing object types, property panels, and
  (for the building module only) a rules engine:
  - **Furniture/Structure module**: parts, materials, joints, assemblies, cut lists, sheet-good
    nesting.
  - **Building module**: sites, walls, openings, framing members, and the prescriptive code
    engine.
- A **deck** is the natural bridge object: it's built like furniture (framing members, decking
  boards, a cut list) but sized like a building element (span tables, footing tables, ledger
  attachment) and sited like a building (setbacks, distance to property line). It should be
  modeled as a Building-module object that leans on Furniture-module machinery for its cut list.

## 5. Feature list

### 5.1 Shared drawing canvas
- Real-world units internally (store as integer millimeters or fixed-point; display in
  feet/inches/fractions or metric per user preference — never float inches as the source of
  truth).
- Snapping: grid, endpoint, midpoint, intersection, perpendicular, parallel.
- Constraint handling for v1: **no general nonlinear constraint solver.** The actual v1 object set
  (walls, openings, rectangular furniture parts) is overwhelmingly rectilinear, so coincident,
  parallel, perpendicular, and dimension-driven resize are handled as **direct, explicit geometric
  relationships** written in-house (a wall stays a rectangle; a dimension change updates the
  bound edge directly) — no dependency, no interop, fully permissively-clean by construction. This
  also sidesteps the licensing dead end in §2.1 (SolveSpace is GPL, its LGPL derivative PlaneGCS is
  C++-only). Revisit a real constraint graph solver only if/when freeform, non-rectilinear shapes
  are needed — likely a post-3D-phase problem.
- Live dimension objects bound to geometry, not floating text.
- Layers, per-project unit setting, undo/redo.
- Print/export to true-scale PDF (vector, not a rasterized screenshot) with a title block.

### 5.2 Furniture / structure module
- Parts with material, thickness, and quantity.
- Joinery reference library (dado, mortise/tenon, pocket screw, lap) — visual reference, not a
  toolpath generator.
- Auto-generated **cut list**: the parts you actually cut — final dimensions, quantity, material,
  derived from the object model plus the materials reference library (§5.6) so a "2x4" part
  resolves to its true 1.5"×3.5" stock dimension, not the nominal name.
- **Parts / materials list**: a distinct output from the cut list — aggregated raw stock to buy
  (how many 2x4x8s, how many sheets of ¾" plywood, how many #8×2" screws), derived from the cut
  list plus §5.6. Cut list drives the shop; materials list drives the store run. Both are core
  v1 outputs, not one-or-the-other.
- **Material takeoff**: board-footage or sheet count, grouped by material/thickness.
- Sheet-goods nesting/layout (given panel stock size, lay out parts to minimize waste) — this is
  a solved, boundable problem (bin-packing heuristic), and a natural place to reuse an existing
  open-source nesting algorithm rather than write one.
- Exploded/assembly view (nice-to-have, later phase).

### 5.6 Materials & hardware reference library

A data-driven reference module, same pattern as the rules engine (§5.4) — authored from primary
standards (NDS span tables, ALSC lumber standards, manufacturer spec sheets), not hardcoded
constants, and not copied verbatim from any single publisher's compiled table:

- **Lumber**: nominal-to-actual dimension mapping (a "2x4" is 1.5"×3.5", a "4x4" is 3.5"×3.5",
  standard lengths), standard stud spacing (16"/24" OC).
- **Sheet goods**: standard plywood/OSB sheet sizes (4×8) and actual thickness by nominal
  thickness (¾" nominal plywood is often actually 23/32"), drywall sheet sizes.
- **Decking**: actual dimensions for standard decking board nominal sizes (5/4×6, etc.).
- **Fasteners**: screw gauge/length, nail penny-size-to-length (16d, 10d, ...), bolt
  diameter/length/thread standards.
- This library isn't only a shopping-list convenience — it directly feeds a **second rules-engine
  table category**: the IRC's prescriptive **fastener schedule (Table R602.3(1))**, which
  specifies fastener type/size/spacing for structural connections (top plate to stud, sheathing
  to framing, etc.). The hardware reference data and the fastener-schedule rules engine share the
  same underlying fastener catalog.

### 5.3 Building module
- **Site plan**: property lines, setbacks, existing structures, north arrow. Supports placing a
  scanned survey/plat image as a calibrated underlay (click two known points, enter the real
  distance, everything scales from that).
- **Walls** as first-class objects carrying attributes:
  - What it supports: roof/ceiling only, roof + 1 floor, roof + 2 floors, interior non-bearing.
  - Site hazard inputs: ground snow load, wind speed, seismic design category (user-entered from
    ASCE 7 hazard maps or the local building department — never assumed/defaulted silently).
  - Existing bracing (sheathing type and length) along the wall line.
- **Openings** (windows, doors, slider) placed and resized on a wall, triggering a live lookup:
  - Header size and jack/king stud count from **IRC Table R602.7(1)/(2)**.
  - Remaining braced-wall-panel compliance from **IRC §R602.10** after the opening is cut in —
    this is the check most DIY openings miss, not the header.
  - Hard **out-of-scope flag** the moment span, floor count, or hazard values exceed what the
    table covers — routes to "get an engineer," never an extrapolated number.
- **Decks**: ledger attachment, joist/beam span tables and footing sizes (DCA6-style prescriptive
  path), guard rail trigger by height, stair layout to grade. Reuses the Furniture module's cut
  list machinery for the framing and decking bill of materials.
- **Small additions/sheds**: foundation/footing table, rafter or truss span table, tie-in detail
  to existing structure — same rules-engine pattern as walls/openings, scoped to the prescriptive
  band only (§2 non-goals).
- Every rules-engine result: shows code edition + table + row used; never a bare number.

### 5.4 Rules engine (the differentiator — detailed in §7)
- Versioned by code edition (2015/2018/2021 IRC, etc. — user-selectable, not hardcoded to latest).
- Data-driven: tables live as structured data files, not embedded in code logic, so adding an
  edition or a local amendment is a data change, not a code change.
- Every calculation traceable to a specific table/row for audit.
- Explicit, first-class "outside prescriptive scope" result type — this is not an error state to
  suppress, it's a correct and expected answer the UI must surface clearly.
- **Code edition is a per-project setting, chosen from a dropdown, not an app-wide default.** The
  dropdown is populated dynamically from whatever edition data packs (§6.3) are installed — one
  entry at v1 launch, growing for free as editions are added later, no UI change required. This
  matters beyond future multi-jurisdiction support: many jurisdictions govern a project by the
  code edition in effect at permit *application* (sometimes issuance), not whichever edition is
  current when the app happens to be opened, so a project may legitimately need to stay locked to
  an older edition even after a newer one ships. Set at project creation, stored in the project
  manifest (§6.4), and changeable later — but changing it forces a full recompute of every
  rules-engine result in the project and re-flags anything that no longer holds under the new
  edition's tables. A silent carryover of stale results across an edition change is exactly the
  class of quiet error this design is meant to prevent.

### 5.5 Import / export
- **Native project file**: documented, versioned, zip container (JSON scene graph + thumbnail +
  assets) — see §8. Open format on principle, independent of whether the app's license is
  copyleft or permissive.
- **DXF export** (target DXF 2000/AC1015, offer R12 as a compatibility fallback) for exchange
  with FreeCAD/QCAD/LibreCAD. Use an existing, well-tested DXF library rather than a hand-rolled
  writer; bake dimensions as literal geometry rather than DXF's native associative `DIMENSION`
  entity for cross-tool fidelity.
- **SketchUp import, read-only**: parse `.skp` via Trimble's published SketchUp SDK (free-tier
  SketchUp users can only export `.skp`/PDF, so this is the only realistic path to their models —
  requiring Pro-tier Collada/OBJ export would exclude the free-tier users who are the actual
  target audience). Treat as geometry-and-dimension reference import, not two-way sync.
- **PDF export**: true-scale vector sheets for both the cut-list/materials side and the
  building/site-plan side, with title block.

## 6. Software architecture

### 6.1 Stack: .NET 10 + Avalonia 12

- **Target .NET 10 (LTS, released Nov 2025, supported through Nov 2028), not .NET 8.** .NET 8 and
  9 both reach end of support on November 10, 2026 — starting a new project on a runtime with two
  months of life left is the wrong call regardless of familiarity. .NET 10 is the current LTS and
  the right target for something meant to last years. Pinned via `global.json`.
- **Avalonia 12**, matching .NET 10 — a real stable release, not a preview, and the pairing the
  Avalonia project templates default to as of this decision.
- **Avalonia UI**, not .NET MAUI. MAUI's desktop story runs through Mac Catalyst on macOS, which
  is weaker than Avalonia's single Skia-rendered codebase for a canvas-heavy precision app; MAUI's
  graphics stack is also less mature for custom drawing surfaces. Avalonia gives pixel-consistent
  rendering across Windows and macOS from one codebase, is MIT-licensed, and has real precedent
  in CAD-adjacent open-source tools.
- Note for the record (from earlier discussion): a Tauri + web-canvas stack would likely be
  *faster for an AI coding agent to iterate on* for the drawing-canvas piece specifically, given
  the density of precedent for interactive vector canvases in the web ecosystem. Staying with
  Avalonia is a reasonable trade for a "real" native .NET app with mature installer/signing
  tooling; worth revisiting only if the canvas/constraint-solver work stalls.

### 6.2 Solution layout

```
/src
  /Core.Geometry        // platform-agnostic: points, lines, arcs, constraints, units, solver
  /Core.RulesEngine      // code-edition-versioned prescriptive tables + evaluation, no UI deps
  /Core.Project          // project file format: scene graph, serialization, versioning
  /Modules.Furniture     // parts, joinery, cut list, nesting
  /Modules.Building      // sites, walls, openings, decks, additions — consumes RulesEngine
  /Interop.Dxf           // DXF import/export
  /Interop.SketchUp      // .skp read-only import via SketchUp SDK
  /Interop.Pdf           // vector PDF export
  /App.Avalonia          // UI: canvas, property panels, dialogs, per-platform packaging
/tests
  /Core.RulesEngine.Tests // golden-value tests against published IRC table rows
  ...
```

Keep `Core.*` and `Modules.*` free of any Avalonia reference. The rules engine and geometry
kernel should be usable from a unit test or a future CLI/batch tool without the UI — this also
keeps the safety-critical code (rules engine) testable in isolation from UI concerns.

### 6.3 Rules engine data model (sketch)

```json
{
  "codeEdition": "IRC-2021",
  "table": "R602.7(1)",
  "description": "Header spans for exterior bearing walls",
  "rows": [
    {
      "supports": "roof-ceiling",
      "groundSnowLoadMax": 30,
      "buildingWidthFt": 28,
      "headerSpanMaxFt": 6.0,
      "header": "(2) 2x8",
      "jackStuds": 1,
      "kingStuds": 1
    }
  ]
}
```

- `RulesEngine.Evaluate(WallContext, OpeningSpec) -> RuleResult` where `RuleResult` is a
  discriminated union: `Sized(header, studs, citation)` or `OutOfScope(reason, citation-of-limit)`.
  Never a third silent-failure case.
- Bracing check is a separate evaluator over the wall line's total opening length vs. required
  braced panel length from §R602.10, run whenever an opening on that wall changes.
- Tables are data files under version control, one directory per code edition, so a future local
  amendment or newer IRC edition is an additive data change with its own golden tests.

### 6.4 Project file format (sketch)

Zip container (same pattern as `.docx`/`.sketch`/FreeCAD's `.FCStd`):

```
project.hcad/
  manifest.json       // format version, app version, code edition used
  scene.json           // full object graph: walls, openings, parts, dimensions
  thumbnail.png
  assets/
    survey-underlay.pdf
```

Document the schema publicly regardless of the app's own license — an open, documented format is
what keeps a project file readable independent of whether the project is maintained in ten years.

### 6.5 Testing strategy

- **Golden-value tests are non-negotiable for the rules engine.** Every table row implemented
  should have a unit test asserting the exact published value from the IRC table it encodes.
  This is the one area of the codebase where "looks reasonable" is not an acceptable bar.
- Geometry/constraint solver: property-based tests (a dimension change should never silently
  produce a geometrically inconsistent state) plus regression tests against saved project files.
- Interop (DXF/SketchUp): round-trip tests against QCAD/FreeCAD/SketchUp's own outputs where
  practical, since fidelity here is empirical, not purely spec-derived.

### 6.6 Packaging and distribution

- Avalonia's publish pipeline supports self-contained, single-file deployment on both platforms.
- macOS: expect Gatekeeper friction for an unsigned/unnotarized open-source binary; plan for
  either notarization (requires an Apple developer account) or clear first-run instructions.
- Windows: expect SmartScreen friction similarly for an unsigned binary; a code-signing
  certificate removes this but is an ongoing cost for an OSS project — worth deciding early
  whether to budget for it or document the workaround.

## 7. Non-functional requirements

- **Offline-first.** No network calls required for core functionality. Hazard/code data ships
  with the app or is user-imported; no telemetry by default.
- **Auditability.** Every prescriptive-code output must display its citation (edition, table,
  row) in the UI and in any exported sheet — not just in a tooltip.
- **Honest scope boundary in the UI itself**, not just in this document: a persistent, real
  disclaimer (not boilerplate) that this tool applies published prescriptive tables and is not a
  substitute for a licensed engineer, a permit office's review, or professional judgment about
  site-specific conditions the tables don't cover. Given the audience — an open-source project a
  lawyer is building, doing life-safety-adjacent calculations for third parties who aren't you —
  this is worth drafting deliberately rather than copy-pasting generic software disclaimer
  language; you're better positioned than I am to get that wording right.
- **License**: AGPL-3.0 for the project, permissive/weak-copyleft-only for dependencies — decided,
  see §2.1.

## 8. Phased roadmap

1. **Phase 1 — Furniture module + cut lists.** No rules engine needed; fastest path to a genuinely
   useful tool (coffee table, shop projects). Validates the geometry/constraint kernel and the
   project file format before the harder building-module work starts.
2. **Phase 2 — Building module core: walls, openings, header/bracing rules engine.** The actual
   differentiator. Ship with one IRC edition and golden tests before adding others.
3. **Phase 3 — Deck module** (span/footing tables, ledger, guard rails) reusing Phase 1's cut-list
   machinery, plus site plan (lot lines, setbacks, survey underlay).
4. **Phase 4 — Interop**: DXF export, SketchUp read-only import.
5. **Phase 5 — Polish**: true-scale PDF sheet output with title blocks, packaging/installers,
   additional code editions and jurisdictional data.

## 9. Decided

- **License**: AGPL-3.0 for the project; dependencies permissive or weak-copyleft only (§2.1).
- **v1 scope is 2D only**, deep and solid, with 3D as an explicit later phase (§2, §8).
- **v1 constraint handling is direct/explicit geometry**, no general nonlinear solver dependency
  (§5.1) — sidesteps the SolveSpace(GPL)/PlaneGCS(LGPL, C++) licensing and interop trade-off
  entirely for now.
- **Cut list and parts/materials list are both core v1 outputs** (§5.2), backed by a
  materials & hardware reference library (§5.6).

## 10. Decided (round 2)

- **No metric support in v1.** Imperial only, matching the source tables directly — no
  conversion layer to get wrong. Revisit only if a future edition/jurisdiction genuinely needs it,
  and if so, as a display-only layer over the imperial source of truth (never convert a table's
  own thresholds into metric and evaluate against the converted value).
- **v1 launch code edition: IRC 2024, as adopted in the 2026 Connecticut State Building Code.**
  The 2026 CSBC became effective **September 18, 2026** — current law, not a pending draft, and
  therefore what Bloomfield (and every CT town — see below) enforces today. Also worth noting:
  on June 9, 2026 Connecticut extended its code adoption cycle from 3 years to 6 and paused
  further model-code adoption between the 2024 and 2030 cycles, so this edition should stay
  Connecticut's governing code for years, not get superseded on the usual 3-year clock. Adding
  IRC 2021 or another edition later (for a different state, or an older jurisdiction) is a
  data-file addition plus its own golden tests per §6.3 — no reason to hedge the v1 target because
  of that.
- Connecticut runs a single uniform state building code with **no local amendments permitted**,
  so Bloomfield (and every CT town) simply enforces the current CSBC — no jurisdiction-amendment
  layer is needed for a CT-only v1. Other states do allow municipal amendments on top of a state
  base code, so that mechanism stays a real requirement if the tool ever expands beyond CT.
