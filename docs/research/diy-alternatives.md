# Research: what DIYers use instead of napkin

Kept short on purpose: what a DIYer actually needs from a tool, what they use for it today, and
what that means for napkin. Retrieved 2026-09-26. Every claim is from the vendor's own page unless
it says otherwise. A cell that says *not read* means the primary page couldn't be fetched, so
nothing is claimed.

## What a DIYer needs, in practice

1. **Real dimensions**, in feet, inches and fractions, without having to learn CAD.
2. **A cut list and a shopping list**: boards and sheets to buy, and how to cut them.
3. **Will it pass?** A span, a ledger, a footing, a guard or a stair checked against the code the
   inspector uses, with the section cited so the inspector can check it too.
4. **Something to hand the building office**: a plan with dimensions.
5. **Free, or close to it.** One project a year doesn't justify a subscription.

## The alternatives

| Tool | 1 Dimensions | 2 Cut / shopping list | 3 Code check | 4 Plan out | 5 Cost |
|---|---|---|---|---|---|
| **SketchUp Free** | General 3D modeller, in a browser | None built in | None | Exports SKP, PNG and STL | Free |
| **SketchUp Go** | Same modeller, on iPad and web | Extensions "not supported at this tier" | None | — | $10.75/mo, billed annually |
| **SketchUp Pro + OpenCutList** | Desktop modeller | OpenCutList: parts list, cutting diagrams, labels, cost and weight reports; handles lumber in fractional inches | None | Pro exports | Pro $33.25/mo billed annually; OpenCutList is free (GPLv3) |
| **Fusion for personal use** | Parametric CAD with a learning curve | None built in | None | DXF only "from sketch"; drawings "single sheet, print only" | Free for "home-based, non-commercial" use; 10 active documents; no extensions |
| **FreeCAD 1.1** | Parametric CAD, with a BIM add-on | No woodworking features mentioned | None | Yes | Free, open source |
| **SketchList 3D** | Cabinets, furniture and built-ins, parametric | Cut lists and cutting layouts | None | — | Subscription; 14-day trial |
| **CutList Optimizer** | None (you type in the parts) | Sheet and linear cutting layouts, kerf, grain, edge banding | None | — | Limited free tier; subscription or $4.90 for 3 days |
| **Trex Deck Designer** | Deck shapes, stairs, railing, the house | Shopping list, including substructure lumber | None stated: the blueprint is "to take to your local building office to be sure it meets safety codes" | Blueprint | Free; laptop or desktop only |
| **Home Depot and Lowe's deck designers** | *not read* (both vendor pages returned 403) | *not read* | *not read* | *not read* | Free |
| **AWC DCA 6** (a guide, not software) | — | — | Prescriptive deck tables with IRC section references | Details to copy | Free PDF |

**DCA 6, as read** (the 2015 edition, "Based on the 2015 International Residential Code",
© 2018 AWC):
- Scope, p. 2: it covers single-level decks "attached to the house to resist lateral forces"
  (item 1). It excludes snow loads over 40 psf (item 9) and hot tubs (item 8).
- Table 2, p. 4: joist spans assume "40 psf live load, 10 psf dead load, No. 2 grade, and wet
  service conditions."
- Contents, p. 2: joists, beams, posts, footings, ledgers and fasteners, lateral loads, guards,
  stairs, handrails and stair lighting.
- It says that where it and the IRC differ, "provisions of the IRC shall apply."
- A Connecticut building department, Watertown's, appears to host the same PDF: the URL was seen in a search result but not opened.

## What this means for napkin

- **Nothing on this list checks a design against the code and cites it.** The deck designers stop
  at a shopping list plus "take it to your building office". DIYers who want to know use DCA 6 by
  hand. That hand arithmetic is napkin's job: M4/M5/M11 with cited tables.
- **That job is empty until real tables load.** Under the shipped CT pack the deck checks say No
  data (#209, waiting on the IRC). **DCA 6 is a free, primary AWC source for exactly those
  tables.** Whether a pack may be built on it is Marc's decision: it's the 2015 IRC basis, while CT
  2022 adopts the 2021 IRC, and it's a guide rather than the adopted code. If yes, the pack must say
  both. This is the one finding worth acting on.
- **The furniture cut list already has a strong free rival**, SketchUp Pro + OpenCutList. It isn't
  free overall, though: Pro costs $33.25/mo billed annually, and Go doesn't run extensions. napkin's
  edge is being free, exact fractions, and one tool for furniture and building. Its cutting
  diagrams don't need to beat OpenCutList's or CutList Optimizer's.
- **A plan for the building office** (the permit set, #223–#227) is the other thing DIYers
  concretely need. Fusion's free drawings are single-sheet and print-only; SketchUp Free doesn't
  export DXF or PDF per its page.
- **Don't chase** modelling freedom, rendering, model libraries or plugin ecosystems. Every
  competitor above already has them, and none of them helps with the permit.

## Sources (retrieved 2026-09-26)

- SketchUp plans and pricing: https://sketchup.trimble.com/en/plans-and-pricing
- SketchUp Free: https://sketchup.trimble.com/en/plans-and-pricing/sketchup-free
- OpenCutList: https://github.com/lairdubois/lairdubois-opencutlist-sketchup-extension
- Fusion for personal use limits (Autodesk help, "Changes to Fusion for personal use"):
  https://help.autodesk.com/view/fusion360/ENU/?caas=caas%2Fsfdcarticles%2Fsfdcarticles%2FFusion-360-Free-License-Changes.html
- FreeCAD: https://www.freecad.org
- SketchList 3D: https://sketchlist.com
- CutList Optimizer: https://www.cutlistoptimizer.com
- Trex Deck Designer: https://www.trex.com/build-your-deck/planyourdeck/deck-designer/
- Home Depot decking calculator (403, not read): https://www.homedepot.com/project-seller/decking-calculator
- Lowe's deck designer (403, not read): https://www.lowes.com/l/about/deck-designer-planner
- AWC DCA 6-2015: https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf
  (read pp. 1–4)
- Watertown, CT building inspectors' copy of DCA 6:
  https://cms7files.revize.com/watertownct/Departments/Building%20Inspectors/AWC-DCA62015-DeckGuide-1804.pdf
  (URL seen in a search result; not opened)
