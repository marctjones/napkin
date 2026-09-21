# napkin — handoff from cloud session

Written by the cloud Claude session that designed and scaffolded this project, for the local
session picking it up. Everything below reflects decisions actually made and reasoning actually
worked through in that conversation, not a fresh take — treat it as a design record, not a
suggestion to re-litigate from scratch.

## What napkin is

An open-source, drag-and-drop desktop CAD tool for a homeowner with basic DIY skills — no CAD
training — covering two overlapping domains in one tool:

1. **Furniture and built structures** (a coffee table, a deck) with automatic cut lists and
   materials lists, resolving nominal lumber/sheet-good sizes to true dimensions.
2. **Small home construction changes that need a permit but not an engineer** (relocating a
   window/door on an exterior wall, a deck, a shed-scale addition) — sized against the IRC's
   homeowner prescriptive-code shortcuts, with every structural number citing its source (code
   edition, table, row) rather than asserting a bare answer.

Concrete projects this is being built against: a coffee table build; a bathroom remodel; a hallway
rewire with recessed lighting; relocating a window/sliding door on an exterior wall; a deck
wrapping a ground-level, pad-mounted hot tub, ledger-attached at an existing sliding door; a
shed-scale addition at the "no full architectural review needed" permitting tier.

Explicit non-goals: not a general parametric mechanical CAD tool, not a licensed-engineer
replacement (no custom structural analysis — prescriptive-table lookups only), not a permit
e-filing platform, not multi-user in v1. The scope boundary is deliberate: when a project's inputs
fall outside what a published table covers, the tool says "get an engineer" and stops, rather than
extrapolating.

Full design rationale lives in [`DESIGN.md`](./DESIGN.md) — read that before making architectural
changes; this file is a summary and a status snapshot, not a replacement for it.

## Decisions already made

| Area | Decision | Why |
|---|---|---|
| Language/runtime | **.NET 10 (LTS)** | .NET 8/9 both reach end of support Nov 10, 2026 — two months out. .NET 10 (Nov 2025) is supported through Nov 2028. Pinned via `global.json`. |
| GUI toolkit | **Avalonia 12** | Single Skia-rendered codebase across Windows/macOS (not per-platform native wrapping). MIT-licensed. Chosen over .NET MAUI specifically — MAUI's desktop story runs through Mac Catalyst, weaker for a canvas-heavy precision app, and its graphics stack is less mature for custom drawing surfaces. |
| Geometry/constraint kernel | **No general nonlinear constraint solver in v1.** Direct, explicit geometric relationships written in-house (a wall stays a rectangle; a dimension change updates the bound edge directly). | v1's actual object set (walls, openings, rectangular furniture parts) is overwhelmingly rectilinear — a full solver is unneeded complexity. Also sidesteps a real licensing dead end (see below). |
| Project file format | Documented, versioned **zip container** (JSON scene graph + thumbnail + assets), same pattern as `.docx`/`.sketch`/FreeCAD's `.FCStd`. | Keeps the project readable independent of the app's own survival; open format regardless of the app's license. |
| Interop export | **DXF, target version 2000/AC1015**, R12 offered as a compatibility fallback. Dimensions baked as literal geometry, not DXF's associative `DIMENSION` entity. | AC1015 is the version most interop libraries (and QCAD/LibreCAD's own `libdxfrw`) treat as their stable baseline; newest DXF versions (2013+) are where cross-tool fidelity actually breaks. Associative dimension entities are inconsistently implemented across tools. |
| Interop import | **SketchUp `.skp`, read-only**, via Trimble's published SketchUp SDK. | Free-tier SketchUp Web can only export `.skp`/PDF — Collada/OBJ/DXF export requires the paid Shop/Pro tier. Since free-tier users are the actual target audience, `.skp` parsing is the only realistic path to their models, not a two-way sync. |
| PDF export | **PdfSharp** (MIT) | Not QuestPDF — its "Community" license is revenue-gated, which conflicts with the permissive-only dependency policy below. |
| Platforms | Windows + macOS, native, no cloud dependency, offline-first | Homeowner's dimensions/site details are private by default; no account required. |
| Project license | **AGPL-3.0** for napkin's own code | If someone stands this up as a hosted service, they owe source back — deliberate, not a default pick. |
| Dependency license policy | **Permissive (MIT/Apache-2.0/BSD) or weak-copyleft (LGPL/MPL) only** — stricter than what AGPL itself requires. | The goal is never taking a dependency whose terms could constrain reuse of napkin's own code, not merely staying legally combinable. |
| v1 launch code edition | **IRC 2024, as adopted in the 2026 Connecticut State Building Code** (effective Sept 18, 2026) | Verified current, not a pending draft, at time of this decision. CT also extended its adoption cycle to 6 years in June 2026 (pausing further model-code adoption until the 2030 cycle) — this edition should stay CT's governing code for years. Rules engine is data-driven and edition-versioned specifically so more editions are a data change later, not a rewrite. |
| Metric support | **None in v1.** | Source IRC tables are imperial; a conversion layer adds a place for rounding error to enter a number that's supposed to be an exact table citation. If added later, must be a display-only layer over the imperial source of truth, never a converted evaluation threshold. |
| 3D visualization | **Deferred past v1.** 2D plan/elevation with live dimensions ships first, deep and solid. | Matches the "furniture module first, no rules engine needed" fast-path in the roadmap; 3D is a real but separable investment. |

### What was considered and ruled out

- **.NET MAUI** — weaker macOS (Mac Catalyst) desktop story and less mature custom-canvas graphics
  than Avalonia for this specific app's needs.
- **Electron/Tauri + web-canvas stack** — genuinely discussed as having an edge for *AI-agent build
  speed* on the interactive-canvas piece specifically (far more precedent in the web ecosystem for
  Figma-style interactive vector canvases). Avalonia was still chosen for native tooling/installer
  maturity; worth revisiting only if the canvas/constraint work stalls badly.
  See discussion in the source conversation if this decision needs to be reopened — it was a real,
  close call, not a formality.
- **SolveSpace's constraint solver** — GPL-3.0, fails the dependency policy above even though it's
  license-*compatible* with AGPL (the policy is intentionally stricter than what the license
  permits). Its LGPL derivative, **PlaneGCS** (what FreeCAD's Sketcher uses), would clear the
  license bar but is a C++ library requiring native interop from .NET — real added engineering
  cost. v1 avoids the whole question via the direct-geometry approach above; revisit PlaneGCS via
  interop only once freeform, non-rectilinear shapes are actually needed.
- **QuestPDF** — ruled out on the license technicality above (revenue-gated Community license).
- **.NET 8** — the cloud session's *first* pick, but only because it was what installed cleanly in
  a sandbox, not an evaluated choice. Corrected to .NET 10 once actually checked against the
  support lifecycle (see table above). Worth noting as a pattern: verify lifecycle/version
  decisions against current facts, don't default to what's locally convenient.
- **Metric units, 3D, and general jurisdiction-amendment support** — all explicitly deferred, not
  forgotten; see DESIGN.md §2/§8 for the phased roadmap they belong to.

## Research findings: CAD/drawing tools compared

Full comparison in [`RESEARCH.md`](./RESEARCH.md). Summary of the actual conclusions:

- **SketchUp** (free web + Pro) — easiest tool to learn, huge furniture library via 3D Warehouse,
  but not open source, and the free tier's only export paths are `.skp` and PDF/image — no
  dimensioned CAD export without paying for Pro/Shop. This constraint is *why* napkin's SketchUp
  interop is scoped to read-only `.skp` import via Trimble's SDK rather than asking users to
  export to Collada/OBJ.
- **Sweet Home 3D** (open source, GPL) — best homeowner-friendly floor-plan/interior tool, drag
  and drop, no CAD background needed, but no concept of property lines or setbacks — not usable
  for the site-plan/zoning side of a permit.
- **FreeCAD** — most capable open-source option overall post-1.0 (Arch/BIM workbench, TechDraw
  dimensioned output, IFC support), but the steepest learning curve of the group. Used here mainly
  as the architectural reference point (its IFC handling and its PlaneGCS solver), not as a tool
  napkin's users would touch directly.
- **QCAD** (community edition GPL, Pro paid) and **LibreCAD** (GPL) — the pure 2D drafting tools;
  best fit for site/plot plans traced over a scanned survey underlay. QCAD is better maintained
  and documented than LibreCAD.
- **File formats**: DXF is the de facto (not ISO) standard for 2D interchange — see the AC1015
  decision above. IFC (ISO 16739) is the real open BIM standard but scoped-down to a minimal
  Model View Definition (IfcSite/Building/Storey/Wall/Window/Door/Space) if ever implemented — full
  IFC is building-lifecycle-scale overkill for this app. STEP (ISO 10303) is the mechanical-CAD
  analog to IFC; noted but out of scope. PDF is the universal submission/underlay format.
- **Prescriptive code sources**: IRC Table R602.7(1)/(2) for header sizing, §R602.10 for wall
  bracing (the compliance gap DIY openings actually miss, more than header sizing), Table
  R602.3(1) for the fastener schedule, and DCA6 for prescriptive deck framing. Connecticut
  specifics: a single uniform state code with no local amendments, currently the 2026 CSBC
  (IRC 2024 base, effective Sept 18, 2026), on a newly-extended 6-year adoption cycle (next model
  code pause until 2030 per a June 2026 law).

## What's done

- `DESIGN.md` — full design and implementation document; all major decisions closed (see table
  above and DESIGN.md §9/§10).
- Solution scaffold: layered .NET 10 / Avalonia 12 solution —
  `Napkin.Core.Geometry`, `Napkin.Core.RulesEngine`, `Napkin.Core.Project`,
  `Napkin.Modules.Furniture`, `Napkin.Modules.Building`,
  `Napkin.Interop.Dxf` / `Napkin.Interop.SketchUp` / `Napkin.Interop.Pdf`, `Napkin.App`
  (Avalonia UI), plus two test projects. Project references wired per the intended dependency
  layering (`Core.*`/`Modules.*` have zero Avalonia references by design). Builds clean, tests
  (placeholder) pass, targeting `net10.0` throughout.
- `LICENSE` — AGPL-3.0, fetched verbatim from gnu.org (not reconstructed from memory).
- `README.md`, `global.json` (SDK pinned to 10.0.x).
- A GitHub issue backlog was drafted (Phase 1–5 features + bug-tracking labels) but **not filed**
  — the cloud session hit a `403` trying to create the repo itself, and by the time repo access
  got sorted out, this handoff happened instead. That backlog content should be recreated/filed
  from the roadmap in DESIGN.md §8 once someone has GitHub write access.

## What's next (in order)

1. **`Core.Geometry`**: points, lines, rectangles, real-world units (integer mm or fixed-point
   internally), dimension objects bound to geometry, the direct/explicit constraint relationships
   described above (no solver dependency).
2. **`Core.Project`**: the actual project file schema + serialization (zip + JSON, per DESIGN.md
   §6.4) and versioning.
3. **`Modules.Furniture`**: parts, cut list, materials list, and the materials/hardware reference
   library (DESIGN.md §5.6 — nominal-vs-actual lumber/sheet-good dimensions, fastener sizes; this
   also feeds the rules engine's fastener-schedule table later, so build it as shared data, not
   furniture-only).
4. File the GitHub issue backlog from DESIGN.md §8 (blocked on repo write access until now).
5. **`Core.RulesEngine`**: encode IRC 2024 (2026 CSBC) Tables R602.7(1)/(2), §R602.10, and
   R602.3(1) as versioned data with golden-value tests against the published table rows — this is
   the one area of the codebase where "looks reasonable" is explicitly not an acceptable bar
   (DESIGN.md §6.5).
6. **`Modules.Building`**: walls/openings/decks consuming the rules engine, plus the per-project
   code-edition dropdown (populated dynamically from installed edition data packs, not hardcoded —
   DESIGN.md §5.4).
7. Avalonia canvas UI: drag/drop/snap wired to `Core.Geometry`.
8. Interop phase: DXF export, SketchUp read-only import.
9. PDF export with title blocks, packaging/installers (macOS notarization and Windows code-signing
   are both real ongoing-cost decisions still open — see below).

## Open questions for Marc

- Whether to ship IRC 2021 (the outgoing CT edition) or another state's edition alongside 2024 in
  v1, or defer all multi-edition work — architecture supports it cheaply either way.
- macOS notarization (needs an Apple developer account) and Windows code-signing (ongoing
  certificate cost) — budget/plan this before Phase 5 packaging, not during it.
- When to invest in a real constraint solver (PlaneGCS via native interop) versus staying on
  direct/explicit geometry — only once freeform, non-rectilinear furniture shapes are actually
  needed.
- Confirm whether GitHub issue/project-tracking work now happens entirely from the local session
  going forward, since the cloud session couldn't get write access to `napkin` this round.
