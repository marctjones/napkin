# napkin — research notes

Background research from early in the project (HANDOFF.md, which summarized it, was removed in
Hardening — beta policy is to remove stale docs, not keep them). Covers the CAD/drawing tools
evaluated, the file-format landscape, and the prescriptive-code research that shaped the rules
engine design.

## CAD / drawing tools compared

| Tool | License | Strengths | Why not chosen as napkin's model / basis |
|---|---|---|---|
| **SketchUp** (Free/Web, Shop, Pro) | Proprietary (Trimble) | Easiest 3D tool to learn (push/pull modeling); huge pre-built furniture/hardware library via 3D Warehouse — the actual reason it dominates makerspace/woodworking use. | Not open source. Free tier's export is limited to `.skp` and PDF/image — no DXF/Collada/OBJ without paying for Shop/Pro. This single fact drove napkin's SketchUp interop design: read-only `.skp` import via Trimble's SDK, not "ask users to export to a friendlier format." |
| **Sweet Home 3D** | Open source (GPL) | Best-in-class for a non-CAD homeowner doing interior/floor-plan work — literal drag-and-drop walls/doors/furniture, simultaneous 2D+3D view, exports a to-scale PDF plan. | No concept of a property line, easement, or setback — can't do the site-plan/zoning side of a permit project at all. Good reference for "how easy should wall/opening editing feel," not usable as a base to build on for the building module. |
| **FreeCAD** | Open source (mostly LGPL, some GPL/MPL components) | Most capable open-source CAD overall since the 1.0 release (Sept 2024) — parametric 3D, Arch/BIM workbench, TechDraw for dimensioned output, real IFC import/export. Its Sketcher constraint solver (PlaneGCS, LGPL, a SolveSpace derivative) is the reference implementation for 2D geometric constraint solving at this scale. | Steepest learning curve of anything evaluated — real CAD, not a napkin-sketch tool. PlaneGCS is C++, so using it from .NET/Avalonia means native interop, which is why napkin defers a general constraint solver rather than adopting it wholesale in v1. |
| **QCAD** (Community GPL / Pro paid) | GPL (Community) | Best-maintained, best-documented pure 2D drafting tool of the group; AutoCAD-like workflow; solid native Windows/macOS builds; RibbonSoft (the vendor) has a commercial reason to keep it stable. Good fit for site/plot plans traced over a calibrated survey underlay. | Not chosen as a *base* to build on (napkin is a from-scratch app), but is the closest analog for what napkin's site-plan tooling should feel like, and a real interop target (DXF). |
| **LibreCAD** | Open source (GPL) | Same 2D-drafting category as QCAD; fully free even at the "Pro" feature level QCAD gates. | Development pace slower, documentation thinner, macOS build historically less polished than QCAD's. Secondary interop target after QCAD. |

### Why not just extend one of these instead of building napkin?

None of them combine (a) genuinely no-CAD-background usability, (b) both furniture/cut-list and
building/site-plan domains in one tool, and (c) prescriptive building-code awareness baked into
the drawing interaction itself. Sweet Home 3D nails (a) but not (b) or (c). FreeCAD covers (b) and
could theoretically support (c) via a plugin but fails badly on (a). QCAD/LibreCAD are precision
2D tools with no code-awareness at all. The prescriptive-code engine tied to live dimensioning is
the actual differentiator and doesn't exist anywhere in this landscape — that's napkin's reason to
exist rather than being a wrapper or a fork.

## File format landscape

- **DXF** — the de facto (not ISO) standard for 2D CAD interchange, implemented (with varying
  fidelity) by every tool above except SketchUp's free tier. Not one format but a lineage of
  versions (R12 through 2018+); newer versions add associative dimensions and other entity types
  that are exactly where cross-tool fidelity breaks down in practice. **Decision: target DXF 2000
  (AC1015)** — the version most interop libraries (`ezdxf`, `libdxfrw`/QCAD's and LibreCAD's own
  DXF library) treat as their stable baseline, with R12 offered as a lowest-common-denominator
  fallback. Bake dimensions as literal geometry (extension lines, arrowhead blocks, text) rather
  than DXF's native associative `DIMENSION` entity — sacrifices "live" dimension editing on the
  receiving end for guaranteed visual fidelity, which is the right trade for a one-way export.
- **IFC (Industry Foundation Classes, ISO 16739)** — the real ISO-standardized BIM interchange
  format, carrying semantic building objects (a wall, a door, a room) rather than bare lines. Full
  IFC (IFC4/IFC4x3) is enormous — thousands of entity types covering MEP, structural analysis, and
  full building lifecycle — which is why even mature BIM vendors run buildingSMART conformance
  certification. **If napkin ever implements IFC, it should be a purpose-built Model View
  Definition** (the mechanism buildingSMART itself uses for narrow-purpose exchanges), scoped to
  roughly `IfcSite`, `IfcBuilding`, `IfcBuildingStorey`, `IfcWall`, `IfcWindow`, `IfcDoor`,
  `IfcSpace`, and basic `IfcExtrudedAreaSolid` geometry — not full-spec IFC. This is a v2+/stretch
  scope, not v1.
- **STEP (ISO 10303)** — the ISO standard for general mechanical/solid CAD geometry (what
  FreeCAD/SolidWorks use for precise 3D interchange). Noted as the correct analog to IFC for
  furniture/joinery if napkin ever needs real solid-geometry interchange, but out of scope — DXF
  or a dimensioned cut list is sufficient for hobbyist furniture.
- **PDF** — the universal format for two purposes: what gets handed to a permit office (every
  evaluated tool can export/print to PDF), and what gets imported back in as a calibrated
  underlay/tracing reference (click two known points, enter the real-world distance, everything
  scales from that). Not an editable CAD format.
- **SketchUp `.skp`** — proprietary, versioned per SketchUp release. Third-party read access goes
  through Trimble's published SketchUp SDK (the same mechanism render engines like Enscape/V-Ray
  use) — treat as one-way geometry/dimension reference import, not two-way sync, and expect to
  track SDK updates against SketchUp's own release cadence.
- **STL/OBJ** — explicitly the *wrong* target for this app's needs. They're the standard for 3D
  printing/mesh interchange but typically carry no real-world units and no 2D dimension
  annotations — exactly the information a permit-facing drawing needs to keep.

## Prescriptive building code research

- **IRC Table R602.7(1)** (exterior bearing wall headers) and **R602.7(2)** (interior bearing
  walls) — header size and jack/king stud count, keyed on what the wall supports (roof/ceiling
  only, +1 floor, +2 floors), ground snow load band, building width, and header span.
- **IRC §R602.10** (wall bracing) — the compliance gap that actually trips up DIY window/door
  openings more often than header sizing does: cutting a new opening can eat into a wall line's
  required braced-wall-panel length, requiring additional sheathing or a portal frame elsewhere on
  the same wall line even when the header itself is correctly sized.
- **IRC Table R602.3(1)** (fastener schedule) — prescriptive fastener type/size/spacing for
  structural connections (top plate to stud, sheathing to framing, etc.). This is why napkin's
  materials/hardware reference library isn't purely a shopping-list convenience — it feeds this
  rules-engine table directly.
- **DCA6** (American Wood Council's Prescriptive Residential Wood Deck Construction Guide) — free,
  public, widely accepted prescriptive path for standard deck framing (spans, footings, ledger
  attachment, guard rails) without an engineer. Explicitly does **not** cover concentrated point
  loads (e.g., a hot tub actually resting on the deck structure itself, as opposed to its own
  separate at-grade pad) — worth keeping as a hard scope boundary if napkin's deck module is ever
  extended toward heavier fixtures.
- **Connecticut specifics** (the initial target jurisdiction): Connecticut runs a single, uniform
  state building code with **no local amendments permitted** — a town's building department
  enforces whatever the state code currently is, with no municipal-amendment layer to model. As of
  this research, the currently effective code is the **2026 Connecticut State Building Code**,
  based on **IRC 2024**, effective **September 18, 2026** (it had been delayed past an original
  July 1, 2026 target pending Legislative Regulation Review Committee approval). Separately, on
  **June 9, 2026** Connecticut extended its code-adoption cycle from 3 years to **6 years**,
  explicitly pausing further model-code adoption between the 2024 and 2030 cycles — meaning this
  edition should remain Connecticut's governing code for the rest of the decade rather than being
  superseded on the usual 3-year clock. This combination (current, stable, and long-lived) is why
  IRC 2024 was chosen as napkin's v1 launch edition rather than hedging toward an older, more
  "settled" edition.

Sources consulted during this research (web search, current as of the research date — re-verify
before relying on anything time-sensitive here, especially the Connecticut code-adoption status):
Connecticut Office of the State Building Inspector (portal.ct.gov/das), UpCodes' Connecticut code
pages, the Building Code Forum, AIA Connecticut, and Builder and Developer Magazine's coverage of
the 2026 adoption-cycle law change.
