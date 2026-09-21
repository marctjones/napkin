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
- Not a multi-user / collaboration tool in the first betas. Single user, local files.
- **Not 3D in the first betas.** 2D plan/elevation with live dimensions ships first, deep and
  solid; 3D visualization is an explicit later phase (see §8), not a requirement of the first
  working beta.

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
  - Constraint solver: see §5.1 and §11 — SolveSpace's own solver is GPL-3.0 and is excluded by
    this policy despite being license-*compatible* with AGPL. PlaneGCS (FreeCAD's derivative) is
    LGPL and would clear the license bar, but is a C++ library requiring native interop, which
    §11's .NET-native rule excludes. The first betas use direct geometry (§5.1); the solver is
    its own workstream (#28).

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
- Real-world units internally: an exact fixed-point length (integer 1/1024ths of an inch — see
  [`docs/design/geometry-model.md`](./docs/design/geometry-model.md) §1; integer millimetres were
  ruled out in #4 because they cannot represent imperial values exactly), displayed as
  feet/inches/fractions — never float inches as the source of truth, and imperial only (§10).
- Snapping: grid, endpoint, midpoint, intersection, perpendicular, parallel.
- Constraint handling in the first betas: **no general nonlinear constraint solver.** The
  first-beta object set (walls, openings, rectangular furniture parts) is overwhelmingly
  rectilinear, so coincident, flush, dimension-driven resize and the rest of the rectilinear set
  are handled as **direct, explicit geometric relationships** written in-house (a wall stays a
  rectangle; a dimension change updates the bound edge directly) — no dependency, no interop,
  fully permissively-clean by construction. This also sidesteps the licensing dead end in §2.1
  (SolveSpace is GPL, its LGPL derivative PlaneGCS is C++-only). A .NET-native solver is its own
  workstream (§11, #28), slotted in behind the same update interface; it is not on the critical
  path to the first working beta.
- Live dimension objects bound to geometry, not floating text.
- Layers, per-project display precision (which fraction of an inch to show), undo/redo.
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
  outputs of the first beta, not one-or-the-other.
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

### 5.4 Rules engine (the differentiator — detailed in §6.3 and
[`docs/design/rules-engine-model.md`](./docs/design/rules-engine-model.md))
- Keyed by **adopted code** — a jurisdiction's adoption of a model-code edition with its
  amendments ("CT 2026 — IRC 2024", "CT 2022 — IRC 2021"), user-selectable per project, never
  hardcoded to the latest. The model-code year is an attribute of a pack, not its identity (§11).
- Data-driven: tables live as structured data files, not embedded in code logic, so adding an
  adopted code, a state amendment or a municipal amendment is a data change with its own golden
  tests, not a code change.
- Every calculation traceable to a specific adopted code, table and row for audit, with the
  source document and where in it the row was read.
- Explicit, first-class "outside prescriptive scope" result type — this is not an error state to
  suppress, it's a correct and expected answer the UI must surface clearly, and it cites the
  limit that excluded the input.
- **The adopted code is a per-project setting, chosen from a dropdown, not an app-wide default.**
  The dropdown is populated dynamically from whatever adopted-code data packs (§6.3) are
  installed — four entries across the first betas (§11), growing for free as packs are added
  later, no UI change required. This matters beyond future multi-jurisdiction support: many
  jurisdictions govern a project by the code in effect at permit *application* (sometimes
  issuance; Connecticut's 2022 code states the application-date rule in its own introduction),
  not whichever code is current when the app happens to be opened, so a project may legitimately
  need to stay locked to a superseded adoption even after a newer one is in force. Set at
  project creation, stored in the project manifest (§6.4), and changeable later — but changing
  it forces a full recompute of every rules-engine result in the project and shows the user a
  before/after report of everything that changed, so nothing that no longer holds under the new
  pack's tables survives unflagged. A silent carryover of stale results across a code change is
  exactly the class of quiet error this design is meant to prevent.

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
  /Core.Geometry        // platform-agnostic: lengths, points, lines, relationships, the update
                         // interface and its direct updater (docs/design/geometry-model.md)
  /Core.Solver           // the .NET-native constraint solver (#28): a second implementation of
                         // Core.Geometry's update interface; Core.Geometry never references it
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

Designed in full in [`docs/design/rules-engine-model.md`](./docs/design/rules-engine-model.md)
(#12, draft awaiting sign-off). The shape, with **synthetic values** — nothing here is a code
value:

```json
// packs/us-ct-2026/pack.json — identity is the adoption, not the IRC year
{ "id": "us-ct-2026", "revision": 1,
  "adoption": { "name": "2026 Connecticut State Building Code", "shortName": "CT 2026",
                "inForce": { "from": "2026-09-18", "to": null },
                "appliesTo": "permit-application-date" },
  "baseCode": { "publisher": "ICC", "code": "IRC", "year": 2024 },
  "layers": [ "irc-2024", "amendments" ],
  "sources": [ { "id": "csbc-2026", "title": "…", "url": "…", "retrievedOn": "…", "sha256": "…" } ],
  "review": { "status": "unreviewed", "checklist": null } }

// layers/irc-2024/tables/r602.7-1.json — a table as data; lengths are exact strings, never doubles
{ "kind": "header-sizing", "table": "R602.7(1)",
  "inputs": [ { "name": "groundSnowLoad", "type": "psf", "band": "upper-bound" },
              { "name": "headerSpan", "type": "length", "band": "capacity" } ],
  "rows": [ { "id": "…", "groundSnowLoad": 99, "headerSpan": "99ft 9in",
              "header": { "plies": 9, "nominal": "2x99" }, "jackStuds": 9, "kingStuds": 9,
              "location": "page …" } ] }

// packs/us-ct-2026/amendments/r602.7-1.json — the state's Add / Amd / Del, as an overlay
{ "table": "R602.7(1)", "source": "csbc-2026", "location": "…", "operations": [] }
```

- `IRulesEngine.SizeHeader(HeaderRequest) -> HeaderResult` where `HeaderResult` is a closed
  union: `Sized(header, studs, citation)` or `OutOfScope(reason, citation-of-limit)`. Never a
  third silent-failure case; missing site inputs are a project state that prevents the request
  from being built, not a result.
- Bracing check is a separate evaluator (`CheckBracing`) over the wall line's provided bracing
  vs. the required braced length from §R602.10, run whenever an opening on that wall changes;
  its results are `Passes`, `Fails` (with the shortfall) or `OutOfScope`, each cited.
- Tables are data files under version control: model-code base layers are ingredients, and one
  directory per **adopted code** (a state's adoption of a model-code edition, with its
  amendments) holds the overlay that makes it a selectable pack. A new adoption, a state
  amendment or a municipal amendment is an additive data change with its own golden tests and a
  signed-off row-by-row review against the primary source. The IRC year is an attribute of the
  pack, never its identity — see §11.
- Thresholds are `Length` (1/1024″, exact) and integers; no double is ever read from a pack.

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

**Format versioning during the beta: a stamp, no migration.** `manifest.json` carries an integer
`formatVersion`, bumped on every change to what the file means. The loader accepts exactly the
version the app writes; any other version — older or newer — fails before the scene is parsed,
with a message naming the file's version and the app's. There is no migration code and no
compatibility shim, per the beta policy (see "Versioning and releases" below); things the format
no longer wants are simply removed. What the scene stores is set out in
[`docs/design/geometry-model.md`](./docs/design/geometry-model.md) §6.

### 6.5 Testing strategy

Five layers, each answering a different question. Two of them are pull-request gates; one of them
is deliberately *not* a gate; the last does not exist yet.

**1. Unit tests — does this code do what it says?**

Ordinary tests over `Core.*` and `Modules.*`, which have no UI dependency and are therefore
testable without an app around them. Geometry in particular gets **property-based** tests: a
dimension change should never silently produce a geometrically inconsistent state, whatever the
sketch. The concrete properties and golden cases are in
[`docs/design/geometry-model.md`](./docs/design/geometry-model.md) §7.

**2. Golden tests — does this match the source it claims to come from?**

For anything whose correct answer comes from *outside* napkin. **Non-negotiable for the rules
engine**: every encoded table row has a test asserting it matches the state's published adopted
text, citing the page or section it was read from. This is the one area of the codebase where
"looks reasonable" is not an acceptable bar, and where the expected value must never be derived
from napkin's own output. The same discipline covers the materials library (authored from primary
standards, §5.6) and the sample fixtures, whose expected cut lists and shopping lists are computed
by hand before the code that produces them exists.

Interop (DXF, PDF, SketchUp) is golden in a looser, empirical sense: round-trip against
QCAD, FreeCAD or SketchUp's own output, because fidelity there is established by experiment rather
than derived from a spec.

**3. The feature scorecard — how much of the design actually works?**

[`features/*.json`](./features/README.md) catalogues the features napkin intends to have, each
with one concrete acceptance sentence, the milestone it belongs to and the issue that delivers it.
Tests claim a feature with a trait; the scorecard reads the test results and reports what is
passing, planned, partial or not started, by area and by milestone.

**The scorecard measures progress and never gates anything** — not a build, not a pull request,
not a tag. Its job is to make "how far along is napkin?" answerable from the test suite instead of
from a status report, and to show what to build next. A metric that can block a merge stops being
an honest measurement. Tracked in #34; see
[`docs/testing/scorecard.md`](./docs/testing/scorecard.md).

**4. The coverage ratchet — a gate, and it only goes up.**

A committed baseline records each assembly's line and branch coverage. CI fails a pull request
whose coverage falls below its floor, and the baseline is raised as coverage improves — never
silently lowered. It gates pull requests and nothing else: it does not gate a tag or a release,
because the beta policy says nothing is scheduled and a release is a snapshot of whatever is
there. Tracked in #32; see [`docs/testing/ratchet.md`](./docs/testing/ratchet.md).

**5. The GUI workflow suite — a gate, and it only goes up.**

A separate suite drives the real Avalonia UI headlessly with simulated pointer, key, text and
wheel events through the visual tree, so hit-testing, focus, shortcuts and routed events are
actually exercised — no shortcut that pokes a view model directly. A scenario is a **workflow**,
not a click: it uses several input actions and both keyboard and pointer, and asserts an
observable outcome after a state-changing sequence. Scenarios grow with each milestone, and their
count and their ids may not fall. Tracked in #33; see
[`docs/testing/gui-automation.md`](./docs/testing/gui-automation.md).

**Later: a small real-OS smoke layer.** Headless cannot cover what only a real desktop has —
window-manager focus, input methods, native menus, drag-and-drop from Finder or Explorer, and
whether an unsigned build gets past Gatekeeper or SmartScreen at all. That is a handful of tests
on a real machine, added once there is a downloadable build worth smoke-testing. It is deliberately
kept small: it is the slowest and most fragile layer, and everything that can be proven one layer
down should be.

### 6.6 Packaging and distribution

- Avalonia's publish pipeline supports self-contained, single-file deployment on both platforms.
- macOS: expect Gatekeeper friction for an unsigned/unnotarized open-source binary; plan for
  either notarization (requires an Apple developer account) or clear first-run instructions.
- Windows: expect SmartScreen friction similarly for an unsigned binary; a code-signing
  certificate removes this but is an ongoing cost for an OSS project — worth deciding early
  whether to budget for it or document the workaround.
- **Decided: no code signing and no notarization** (§11). Installers ship unsigned; the README
  must document the first-run steps on both platforms. On macOS 15 and later the old
  Control-click → Open bypass is gone, so the instructions must point to System Settings →
  Privacy & Security → Open Anyway. Note that Apple-silicon binaries still carry an automatic
  *ad-hoc* signature applied by the build toolchain — that is required for arm64 code to run at
  all, costs nothing, and is not the Developer ID signing this decision declines.

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

## 8. Roadmap: five milestones

Set by Marc on 2026-09-21, replacing the earlier phase list. **The milestones are an order of
work**, each one defined by what a person can see and play with rather than by which layer of the
architecture it fills. A milestone earns a tagged pre-release when it is done, and its version
number is assigned at that moment rather than planned in advance — which is why they have names.
Nothing is scheduled toward a date (see "Versioning and releases" below).

1. **M1 Look.** A read-only viewer: download an unsigned build, open a hand-crafted sample design
   from a file, pan, zoom, zoom to fit, and read dimension labels in feet, inches and fractions.
   Nothing editable, nothing saved. It proves the foundation — exact lengths (§5.1), the geometry
   model, a strict scene reader (§6.4), hand-computed fixtures, and a pipeline that turns a tag
   into something downloadable.
2. **M2 Draw.** The viewer becomes a drawing tool: draw by dragging, move and resize, resize by
   *typing* a dimension, snap parts together and see the relationship the snap created, undo and
   redo, save a design and reopen it. Invalid input is explained; two dimensions that cannot both
   hold produce a named conflict rather than a wrong number.
3. **M3 Cut.** Furniture and its two distinct outputs (§5.2): build the coffee table, assign
   materials from the reference library (§5.6), and get both a cut list and a shopping list, on
   screen and as CSV, checked against expectations computed by hand.
4. **M4 Check.** The differentiator, one answer at a time: a wall, an opening, a header size and
   stud count with the code edition, table and row behind it (§5.3, §5.4), and a hard out-of-scope
   result the moment the inputs leave what the table covers. One adopted code pack — **Connecticut
   2026** — plus the per-project picker (§5.4).
5. **M5 Brace and compare.** The wall-bracing check (§5.3 — the check most DIY openings miss),
   and a **second** pack, Connecticut 2022. The second pack is the point: locking a project to the
   code in force at permit application, and recomputing every result when that changes, is only
   demonstrable once there are two codes to move between.

**Backlog**, wanted but not scheduled: the deck module in its pieces (ledger, joists and beams,
footings, guards and stairs) reusing M3's cut-list machinery; the site plan; DXF and PDF export;
the Massachusetts and Pennsylvania packs and municipal amendment overlays; sheet-goods nesting;
SketchUp import; installers.

Alongside all five, off the critical path: the **constraint solver workstream** (§11, #28), which
starts once Core.Geometry (#5) has landed and **gates no milestone**. If it proves hard, it waits;
§11's seams mean deferring it costs nothing. The test infrastructure of §6.5 — the coverage
ratchet, the GUI workflow suite and the feature scorecard — likewise starts in M1 and grows with
every milestone rather than belonging to one.

The issue-by-issue breakdown, and which model leads each step, is in
[`PLAN.md`](./PLAN.md).

## 9. Decided

- **License**: AGPL-3.0 for the project; dependencies permissive or weak-copyleft only (§2.1).
- **Beta scope is 2D only**, deep and solid, with 3D as an explicit later phase (§2, §8).
- **First-beta constraint handling is direct/explicit geometry**, no general nonlinear solver
  dependency (§5.1) — sidesteps the SolveSpace(GPL)/PlaneGCS(LGPL, C++) licensing and interop
  trade-off entirely for now. The solver is a separate workstream (§11).
- **Cut list and parts/materials list are both core outputs of the first beta** (§5.2), backed
  by a materials & hardware reference library (§5.6).

## 10. Decided (round 2)

- **No metric support in the betas.** Imperial only, matching the source tables directly — no
  conversion layer to get wrong. Revisit only if a future edition/jurisdiction genuinely needs it,
  and if so, as a display-only layer over the imperial source of truth (never convert a table's
  own thresholds into metric and evaluate against the converted value).
- **First code edition: IRC 2024, as adopted in the 2026 Connecticut State Building Code.**
  (Superseded in part by §11, which adds three more adopted codes to the first beta; the
  reasoning below still stands for why CT 2026 is encoded first.)
  The 2026 CSBC became effective **September 18, 2026** — current law, not a pending draft, and
  therefore what Bloomfield (and every CT town — see below) enforces today. Also worth noting:
  on June 9, 2026 Connecticut extended its code adoption cycle from 3 years to 6 and paused
  further model-code adoption between the 2024 and 2030 cycles, so this edition should stay
  Connecticut's governing code for years, not get superseded on the usual 3-year clock. Adding
  IRC 2021 or another edition later (for a different state, or an older jurisdiction) is a
  data-file addition plus its own golden tests per §6.3 — no reason to hedge the first target
  because of that.
- Connecticut runs a single uniform state building code with **no local amendments permitted**,
  so Bloomfield (and every CT town) simply enforces the current CSBC — no jurisdiction-amendment
  layer is needed for CT alone. Other states do allow municipal amendments on top of a state
  base code, so that mechanism stays a real requirement if the tool ever expands beyond CT.

## 11. Decided (round 3)

Decided by Marc on 2026-09-21, after the project moved from the cloud session to a local one.

### Four adopted codes, not one

| Adopted code | Base model code | In force | Primary source |
|---|---|---|---|
| **CT 2026** State Building Code | IRC 2024 + CT amendments | Sept 18, 2026 | Conn. DAS, Office of State Building Inspector |
| **CT 2022** State Building Code | IRC 2021 + CT amendments | Oct 1, 2022 (permit applications on or after); superseded by the 2026 code | [2022 CSBC](https://portal.ct.gov/-/media/DAS/Office-of-State-Building-Inspector/2022-State-Codes/2022-CSBC-Final.pdf) |
| **MA** 780 CMR, 10th ed., Residential Volume (Ch. 51) | IRC 2021 + MA amendments | Oct 11, 2024; concurrent with the 9th edition until June 30, 2025, sole code since | [Mass.gov — 780 CMR](https://www.mass.gov/massachusetts-state-building-code-780-cmr) |
| **PA** Uniform Construction Code | 2021 ICC codes + PA amendments | Jan 1, 2026; the 2018 code remains usable where a contract was signed before Jan 1, 2026 and the permit applied for by June 30, 2026 | [Pa. DLI — UCC](https://www.pa.gov/agencies/dli/programs-services/labor-management-relations/bureau-of-occupational-and-industrial-safety/uniform-construction-code-home) |

CT 2022 matters even though it is superseded: §5.4 locks a project to the code in force at permit
application, so a CT project applied for before Sept 18, 2026 is governed by the 2022 code. The PA
transition rule comes from a secondary source (a law-firm summary); verify it against the
regulation text in 34 Pa. Code before encoding it.

All four are wanted, and they arrive one at a time (§8): **CT 2026 in M4**, **CT 2022 in M5**, and
Massachusetts and Pennsylvania in the backlog after that. Four packs remains the commitment; a
single pack in M4 is the order of work, not a narrowing of scope. The second pack is what makes
code locking demonstrable rather than merely designed, which is why it arrives as early as M5.

### Consequences for the design

- **The rules engine is keyed by adopted code, not IRC edition.** Three of the four packs share
  the 2021 IRC base but not its numbers, because each state amends it differently. Amendments can
  change site inputs to a table (Massachusetts sets its own snow and frost criteria) and can change
  the tables themselves; which rows survive is only knowable from each state's adopted text. So
  golden tests are written against each state's *published adopted code*, never against the base
  IRC. §6.3 is updated accordingly.
- **The amendment layer is now beta scope.** §10 noted that CT permits no municipal
  amendments and that the layer would become real "if the tool ever expands beyond CT." It has:
  Pennsylvania municipalities may adopt stricter amendments with Department of Labor and Industry
  approval under Act 45 §503 (see DLI's [register of municipal code-change
  ordinances](https://www.pa.gov/agencies/dli/programs-services/labor-management-relations/bureau-of-occupational-and-industrial-safety/uniform-construction-code-home/ucc-municipal-code-change-ordinances)).
  Its shape follows §6.3: an additive data overlay on the state pack. How far the first betas go
  in encoding specific municipalities is not yet decided (#22).
- **The per-project picker lists adopted codes by jurisdiction** ("Pennsylvania UCC — 2021
  ICC, in force Jan 1, 2026"), not bare IRC years.

### Packaging

- **No code signing and no notarization**, on either platform. See §6.6 for the first-run
  documentation this requires.

### Constraint solver: wanted, .NET-native, a separate workstream off the critical path

- **napkin will get a geometric constraint solver**, because furniture with angled or curved
  parts needs relationships that direct geometry can't maintain. The first working beta ships on
  direct, explicit geometry (§5.1) regardless; the solver is its own line of work and must not
  delay that beta. If it proves hard, it waits — nothing else depends on it.
- **It must be .NET-native** — managed C#, no native interop. That rules out both mature open
  solvers: SolveSpace (C, GPL, which also fails §2.1) and PlaneGCS (C++, LGPL). The workstream
  (#28) starts by evaluating existing pure-.NET solvers against §2.1's license policy, and writes
  one in-house if none qualifies. It is run as a **time-boxed spike** in its own assembly
  (`Core.Solver`, §6.2), started after Core.Geometry (#5) lands, with explicit continue/defer
  criteria recorded in #28.
- **`Core.Geometry` is built so the solver slots in later without a rewrite** — designed in
  [`docs/design/geometry-model.md`](./docs/design/geometry-model.md) and delivered by #5:
  - Relationships are stored explicitly as data (a dimension bound to the edge it measures, a
    part anchored to another) — never implied by where the UI happens to place things.
  - Every geometry update goes through one interface, `IGeometryUpdater`, with the direct
    updater as the first implementation. A solver is a second implementation of the same
    contract; no caller changes.
  - The states a solver produces are explicit result types from day one: `Solved`,
    `UnderConstrained` (a success — a part that is free to move is the normal state of a
    drag-and-drop drawing, not an error), and `OverConstrained` carrying a conflict report that
    names the relationships that cannot all hold. The design adds a fourth, `Rejected`, for
    requests that are not geometric states at all (an unknown id, a zero-width part, a
    relationship kind the current updater does not implement), so that the conflict report never
    has to cover for a malformed request. The direct updater does no degree-of-freedom analysis
    and therefore only ever returns `Solved`, `OverConstrained` or `Rejected`. The UI then already
    has somewhere to show "this shape has no solution" when the solver arrives — the same
    principle as §5.4's first-class out-of-scope result.
  - Lengths stay exact fixed-point; the solver works in `double` on a copy and its result is
    rounded once, repaired through the direct updater's exact propagation, and verified against
    every relationship before it is accepted — never silently (design doc §5).

### Where the work happens

- All development now happens in the local repository and local Claude sessions. The cloud
  session that designed the project is retired; its final state is preserved unchanged on the
  `claude/cloud-handoff` branch.

## 12. Versioning and releases (beta policy)

Set by Marc on 2026-09-21.

- **napkin is a pre-1.0 beta indefinitely.** Nothing is working toward a 1.0. Every release is a
  pre-release beta. The first "working" version could be 0.2, 0.5, 0.20 or 0.75 — a milestone
  (§8) is tagged when it is done and its number is assigned at that moment, not guessed in
  advance. That is why the milestones have names rather than numbers.
- **Breaking changes are always allowed.** There is no backwards-compatibility concern, no
  deprecation period, no migration shims. Things the project no longer wants are removed. This
  includes the project file format (§6.4): a format-version stamp gives an old file a clear
  "unsupported version" error, and no migration code is ever written.
- **Nothing is scheduled toward a release date.** Do not rush.
- **This does not shrink product scope.** Four adopted codes (§11) and locking a project to its
  permit-date code edition (§5.4) are features, not legacy. "Beta scope" in this document means
  what the betas are meant to do, not what is provisional.
- **Version numbers:** the minor number increments as work is committed; a release is tagged when
  features improve significantly.

**Mechanism — PROPOSED, awaiting Marc's confirmation** (tracked as its own issue; the exact
per-commit rule is unconfirmed):

- A single `<VersionPrefix>0.N.0</VersionPrefix>` in a `Directory.Build.props` at the repository
  root, with `<VersionSuffix>beta</VersionSuffix>`, so every assembly and the app report the same
  `0.N.0-beta`. The file does not exist yet; creating it is implementation work, not a decision
  made here.
- The minor number `N` is bumped in the pull request that lands the work, by its author, as part
  of that PR. Patch stays 0.
- CI passes the commit SHA into the informational version, so a running beta can say exactly
  which commit it is.
- When a milestone is worth naming, a `v0.N.0-beta` tag and a GitHub pre-release with the
  unsigned installers (#27). No "latest" release is ever marked stable.
