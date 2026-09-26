# napkin — research notes

Background research from early in the project (HANDOFF.md, which summarized it, was removed in
Hardening — beta policy is to remove stale docs, not keep them). Covers the CAD/drawing tools
evaluated, the file-format landscape, and the prescriptive-code research that shaped the rules
engine design.

## CAD / drawing tools compared

| Tool | License | Strengths | Why not chosen as napkin's model / basis |
|---|---|---|---|
| **SketchUp** (Free/Web, Shop, Pro) | Proprietary (Trimble) | Easiest 3D tool to learn (push/pull modeling); huge pre-built furniture/hardware library via 3D Warehouse — the actual reason it dominates makerspace/woodworking use. | Not open source. The free tier's export formats drove napkin's early SketchUp interop idea (read-only `.skp` import); that import is closed (#24), and what the free tier exports was not confirmed from a vendor page when re-checked on 2026-09-26 (#215), so it is not stated here. |
| **Sweet Home 3D** | Open source (GPL) | Best-in-class for a non-CAD homeowner doing interior/floor-plan work — literal drag-and-drop walls/doors/furniture, simultaneous 2D+3D view, exports a to-scale PDF plan. | No concept of a property line, easement, or setback — can't do the site-plan/zoning side of a permit project at all. Good reference for "how easy should wall/opening editing feel," not usable as a base to build on for the building module. |
| **FreeCAD** | Open source (mostly LGPL, some GPL/MPL components) | Most capable open-source CAD overall since the 1.0 release (Sept 2024) — parametric 3D, Arch/BIM workbench, TechDraw for dimensioned output, real IFC import/export. Its Sketcher constraint solver (PlaneGCS, LGPL, a SolveSpace derivative) is the reference implementation for 2D geometric constraint solving at this scale. | Steepest learning curve of anything evaluated — real CAD, not a napkin-sketch tool. PlaneGCS is C++, so using it from .NET/Avalonia means native interop, which is why napkin defers a general constraint solver rather than adopting it wholesale in v1. |
| **QCAD** (Community GPL / Pro paid) | GPL (Community) | Best-maintained, best-documented pure 2D drafting tool of the group; AutoCAD-like workflow; solid native Windows/macOS builds; RibbonSoft (the vendor) has a commercial reason to keep it stable. Good fit for site/plot plans traced over a calibrated survey underlay. | Not chosen as a *base* to build on (napkin is a from-scratch app), but is the closest analog for what napkin's site-plan tooling should feel like, and a real interop target (DXF). |
| **LibreCAD** | Open source (GPL) | Same 2D-drafting category as QCAD; fully free even at the "Pro" feature level QCAD gates. | Development pace slower, documentation thinner, macOS build historically less polished than QCAD's. Secondary interop target after QCAD. |

### Why not just extend one of these instead of building napkin?

None of them combine (a) genuinely no-CAD-background usability, (b) both furniture/cut-list and
building/site-plan domains in one tool, and (c) prescriptive building-code awareness baked into
the drawing interaction itself. Sweet Home 3D nails (a) but not (b) or (c). FreeCAD covers (b) and
could theoretically support (c) via a plugin but fails badly on (a). QCAD/LibreCAD are precision
2D tools with no code-awareness at all. Free sizing tools do exist — AWC's span calculator, ForteWEB,
BC Calc, the DCA 6 guide (#215 lists them with their pages, checked 2026-09-26) — but each sizes one
member from numbers typed into it. What none does is tie a prescriptive table lookup to a live
drawing, with the citation, a bracing check and an out-of-scope stop; that is napkin's reason to
exist rather than being a wrapper or a fork.

### DIY design and sizing tools (checked 2026-09-26)

The comparison tools above are the CAD/drawing category; this table is the wider DIY landscape a
person doing this kind of project actually runs into (design tools, cut-list/optimizer tools, and
structural sizing calculators), each vendor page re-read the day this table was written. "Not
confirmed today" means the cited page didn't state that fact when fetched, not that the fact is
false.

| Tool | Category | Notes |
|---|---|---|
| [SketchUp](https://sketchup.trimble.com/en/plans-and-pricing) | Furniture/woodworking | A Free plan is documented on [help.sketchup.com](https://help.sketchup.com/en/admin/sketchup-free) (web-only, "not for commercial use") but is no longer listed on the pricing page itself; Go $10.75/mo (web + iPad, no LayOut or extensions); Pro $33.25/mo (desktop, extensions, LayOut); Studio $71.58/mo (Windows only). Free tier's export formats not confirmed today — see DESIGN.md §5.5. |
| [OpenCutList](https://github.com/lairdubois/lairdubois-opencutlist-sketchup-extension) | Furniture/woodworking | Free, GPLv3. Parts lists, cutting diagrams, nesting, labels, cost/weight reports. Desktop SketchUp 2017+ only — [not available for the web or iPad versions](https://docs.opencutlist.org/getting-started/installing). v7.1.0, released 2025-12-10. |
| [Autodesk Fusion, Personal Use](https://help.autodesk.com/view/fusion360/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FFusion-360-Free-License-Changes.html) | Furniture/woodworking | Free for home-based, non-commercial use; 10 active editable documents. No cut-list feature stated on this page. |
| [Shapr3D](https://www.shapr3d.com/pricing) | Furniture/woodworking | Free tier: 2 projects, basic-resolution STL/3MF export only. Pro $299/yr per editor seat. |
| [SketchList 3D](https://www.sketchlist.com/pricing) | Furniture/woodworking | $599.99/yr (or $79.99/mo), or a $999.99 one-time licence plus a maintenance plan. Has a cut list and an optimizer. |
| [MaxCut](https://www.maxcutsoftware.com/pricing) | Furniture/woodworking | Community Edition free (limited library, basic cut list). Business $20/mo or $200/yr per device. Platform not stated on this page. |
| [CutList Optimizer](https://cutlistoptimizer.com/) | Furniture/woodworking | Web-based panel optimizer; free plus paid Bronze/Silver/Gold tiers — exact paid prices not confirmed today. |
| [FreeCAD](https://blog.freecad.org/2026/03/25/freecad-version-1-1-released/) + [Woodworking workbench](https://github.com/dprojects/Woodworking) | Furniture/woodworking | Free. FreeCAD 1.1 released 2026-03-25; 1.1.3 is the current release (2026-07-25). The Woodworking workbench (MIT) adds an automatic cut list exportable to CSV/JSON/HTML/Markdown. |
| [Sweet Home 3D](https://www.sweethome3d.com/) | Home layout | Free, GPL. Version 7.5. |
| [Chief Architect Home Designer](https://www.homedesignersoftware.com/products/home-designer-suite-architectural-pro/) | Home layout | One product line since 2026 (page confirms the consolidation). Pricing and any framing-generation or code-check claim not stated on this page today. |
| [AWC span calculator](https://awc.org/codes-standards/calculators-software/spancalc/) | Structural sizing | Joists and rafters only, not beams or headers; NDS 2018 Supplement. Whether it's free wasn't restated on this fetch (it's publicly reachable with no login shown). |
| [Weyerhaeuser ForteWEB](https://www.weyerhaeuser.com/woodproducts/software-learning/forte-software/) | Structural sizing | "Free to use!"; requires a registered account. Sizes joists, beams, posts/studs — engineered wood, dimension lumber and steel. |
| [Boise Cascade BC Calc](https://www.bc.com/ewp/software/bc-calc/) | Structural sizing | Login required; the page reads "Try Free" / "Try for Free Today" rather than stating a permanent free plan, so free status is still unverified. Sizes joists, beams, columns, studs and tall walls. |
| [AWC DCA 6](https://awc.org/collection/design-for-code-acceptance/) | Structural sizing / decks | Latest edition listed is DCA 6-2015. The IRC edition it's keyed to and whether the PDF is free were not restated on this collection page today — unverified. |
| [Simpson Strong-Tie Deck Planner](https://www.strongtie.com/products/go/software/deckplanner) | Decks | Free, web-based. Produces "permit submittal pages" and a Bill of Materials. Sizing framing to code is not mentioned. |
| [Trex deck designer](https://www.trex.com/build-your-deck/planyourdeck/deck-designer/) | Decks | Free. The user picks the lumber size and species themselves; the tool tells them to take the resulting blueprint to the building office to check it meets code. |
| [CraftCut](https://craftcut.io/) | Newer / AI tools | Browser-based AI furniture design with cut optimization, in beta (Pro features free through 2026-09-30). Pro $19/mo (~$15.20/mo billed annually) after. |
| [Woodplans.ai](https://woodplans.ai/) | Newer / AI tools | Freemium — 3 free plan credits, then paid credits. AI-generated plans with a cut list; its "engineering checks" verify fastener lengths, spans and assembly clearances (confirmed on today's fetch). |
| [UpCodes](https://up.codes/) | Newer / AI tools | Searchable building-code, assembly and product text. An AI assistant feature is not confirmed from the homepage today. |
| [Symbium](https://symbium.com/) | Newer / AI tools | Instant permit-compliance automation. Sold to building departments, contractors and a handful of CA/CO/MD jurisdictions today — not to homeowners directly. |

## File format landscape

- **DXF** — the de facto (not ISO) standard for 2D CAD interchange, implemented (with varying
  fidelity) by most tools above. Not one format but a lineage of
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
