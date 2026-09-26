# Furniture checks: shelf sag, tip-over, bunk-bed guards and screw hold, honestly scoped

Status: **DRAFT awaiting Marc's sign-off.** Milestone **M9 Shape**, issue #154. §8 lists the
slices, filed as #216–#222; none starts before sign-off. §9 lists the decisions
that are Marc's, each with the default I recommend, so a "yes" is enough.

Marc asked (#154): *is there a standard for how strong furniture must be, what weight it must
carry, and what fasteners are required for different pieces?* The research,
[`../research/furniture-standards.md`](../research/furniture-standards.md), read the primary sources
on 2026-09-26. Its answer is short:

- **No single standard says how strong furniture must be.**
- **Nothing read requires particular fasteners for any piece.**
- One rule is **mandatory** in the US: tip-over stability for clothing storage units (16 CFR 1261,
  which requires ASTM F2057-23). Bunk beds have mandatory **geometric** rules (16 CFR 1213/1513).
- The industry standards are **voluntary**, and their test loads are **paywalled**: BIFMA X5
  (commercial), KCMA A161.1 (kitchen cabinets) and BHMA A156.9 (cabinet hardware).
- The **method** for sag and screw hold is public domain: the USDA Wood Handbook
  (FPL-GTR-282, 2021), which gives formulas and properties but no limits.

Every number this note mentions is cited to that research file (source, section, retrieval date).
Per CLAUDE.md, no number is stated here from memory, and none of them is shipped by this note. The
slice that ships a table reads it again from the source in its own task.

---

## 1. What napkin will and will not say

The rules engine's honesty applies here unchanged
([`rules-engine-model.md`](./rules-engine-model.md) §3.1: no silent third state). Every furniture
check result is one of:

| Result | Meaning | Example wording |
|---|---|---|
| **Estimate** | napkin worked a number from a public-domain method and the design's own sizes, and says it is an estimate | "Shelf sags ≈ *a* under 15 lb/sq ft (KCMA's shelf load); ≈ *2a* over years (Wood Handbook creep allowance). No standard sets a limit; yours is *b*." (illustrative; letters, not numbers) |
| **Geometric check** | a mandatory rule's geometric limit, compared exactly | "Guardrail top is *h* above the mattress; 16 CFR 1213.3(a)(6) asks for at least 5"." (5" as read in the research, §6) |
| **Not checked** | napkin names the standard and why it cannot check it | "Commercial desks and tables: BIFMA X5.5-2021 applies; its test loads are not public, so napkin does not check them." |
| **Input missing** | a check that could run, but lacks one input | "Species not chosen: no sag estimate." |

Three rules follow from the research:

- **Never "complies" and never "passes".** At most napkin says it "approximates F2057-23 §9.2.3 as
  described at 88 FR 28408". A furniture check is an estimate or a geometric comparison.
- **A limit is always someone's choice, and the check names whose.** A sag limit is the
  person's, or napkin's default, labelled as napkin's. No source read gives one: KCMA's summary says
  "no excessive deflection" with no number.
- **Not checked is said out loud.** Listing BIFMA, BHMA and KCMA by name and scope, with "not
  checked", is part of the result. It is not a footnote.

## 2. Which checks apply to which piece

napkin has no furniture types: a design is parts, joints and fasteners. So a check does not ask
"is this a dresser?". It reads what the design says, plus one mark the person sets (§9.1).

| Check | Runs on | Reads | Source of the method | Source of any number |
|---|---|---|---|---|
| **Shelf sag** (§3) | a part lying flat (thickness up) held at its two ends by joints: a dado, a shelf pin line, a cleat | span between the supports, width, thickness, species, load | Wood Handbook ch. 9 Eq. 9-2, Table 9-1 | E by species: Wood Handbook ch. 5 Table 5-3b. Load: the person's, or KCMA's 15 lb/sq ft, attributed. Limit: the person's |
| **Tip-over estimate** (§4) | a design marked **clothing storage** (§9.1) | its outline, its parts' weights (volume × a species density, §9.4), which parts are drawers and how far they open | static moments on the tilted unit | F2057-23 §9.2.1–9.2.3 as restated in 88 FR 28408 (public domain) |
| **Clothing-storage scope flag** (§4.3) | the same design | height, estimated weight, enclosed volume | — | ASTM F2057-23 §1.1 thresholds, read from astm.org's scope |
| **Bunk-bed guardrail** (§5) | a design marked **bunk bed** (§9.1) | guardrail top above the mattress top; gaps | exact geometry | 16 CFR 1213.3(a)(2), (a)(6) |
| **Screw hold** (§6) | every screwed joint | screw diameter and embedment (from the typed size), species specific gravity | Wood Handbook ch. 8 Eq. 8-10b | G by species: Wood Handbook ch. 5 |
| **Nail edge and end distance** (§6) | every nailed or bradded joint | the nail's distance to the receiving part's edge and end | Wood Handbook ch. 8's rule | — |
| **Not checked** list (§7) | every design | nothing | — | BIFMA X5.1/X5.5/X5.9, KCMA A161.1, BHMA A156.9, ASTM F1821-26, by name and scope |

## 3. Shelf sag

A part is a **shelf** when both of these hold:

- it lies with its thickness up;
- it is the inserted part of joints at two opposite ends: a dado or a butt into two sides, or shelf
  pins, once they exist.

The **span** is the clear distance between the receiving faces, exactly as the joint geometry
already knows it (`JointGeometry.Contact`).

The estimate is Eq. 9-2, bending plus shear, with Table 9-1's coefficients for a uniform load on a
simply supported span (dados and pins are treated as simple supports, the conservative case), and E
for the part's species at 12 % moisture from Table 5-3b. The instant sag is doubled for the
long-term figure, per chapter 5's creep sentence, and the panel says that factor is napkin's
reading of the Handbook.

- **Load:** the person types lb/sq ft. The default is KCMA's public 15 lb/sq ft, labelled "KCMA's
  shelf test load (kcma.org summary)".
- **Limit:** the person types it. No default ratio is proposed here, because no source gives one.
  With no limit typed, napkin shows the estimate with no verdict (§9.2).
- **Arithmetic:** in `decimal`, not on the 1/1024" grid. It is an estimate, shown with ≈, never
  stored.
- **Species:** a species napkin has no row for is "Input missing: species", never a guess.
  `Part.Species` is free text today (never interpreted), so slice A adds a species picker from the
  cited table and keeps free text for anything else.

## 4. Tip-over estimate

### 4.1 What is modelled

A unit marked **clothing storage** gets three static estimates, one per F2057-23 test as restated
in the Federal Register (88 FR 28408):

1. **§9.2.1:** every drawer and door open, on level ground. If half or more of the storage volume
   is extended, the drawers are filled at the FR's simulated clothing density.
2. **§9.2.2:** a horizontal pull, of the FR's force, at the highest hand-hold no higher than the
   FR's limit.
3. **§9.2.3:** the FR's weight on the front edge of the open drawer that gives the worst moment,
   with the unit tilted forward on the FR's block.

Each estimate gives the margin: the restoring moment about the front tipping edge minus the
tipping moment, in lb·in. The words are "stands, with ≈ N lb·in to spare" or "tips, short by
≈ N lb·in". It is always an estimate that "approximates F2057-23 §9.2.x as described at
88 FR 28408", never "complies".

### 4.2 What napkin needs that it does not have

- **Drawers.** A drawer is today just parts joined together. Slice C adds one mark on a joint
  group, **drawer**, with a typed extension (how far it opens). Slides are the person's typed hardware
  line, as now. BHMA grades are cited by name only.
- **Weight.** Part volume × species density. The density comes from the same Wood Handbook
  chapter 5 table as E, cited. Sheet goods need their own cited density. Without one, that part
  is "Input missing: density", and the estimate says so rather than leaving the part out.
- **Wall anchoring.** A unit the person marks as anchored is reported as "anchored: tip-over
  estimate not needed", since anchoring is how people meet the rule in practice. Whether napkin
  says that is a decision (§9.3).

### 4.3 Scope flag

When the design is at or over all three of F2057-23 §1.1's thresholds (height, weight, enclosed
volume), napkin says "this is a clothing storage unit as ASTM F2057-23 §1.1 defines it; if sold,
16 CFR 1261 applies". This is a fact about the design, not a check, and it is shown whether or not
the person marked the unit.

## 5. Bunk-bed guardrails

A design marked **bunk bed** has the upper mattress top as a typed height (napkin has no mattress
part). The check compares two things exactly on the grid:

- every guardrail part's top to that height, against §1213.3(a)(6);
- every gap between guardrail parts, against §1213.3(a)(2).

The CFR's text is public domain (17 U.S.C. §105), so its numbers may ship in a data file, read
and cited in slice E's task. Entrapment rules beyond these two are listed as "not checked".

## 6. Screw hold and nail distances

Slice F. For every joint fastened with wood screws whose typed size napkin can read (a gauge and
a length), the withdrawal estimate is Eq. 8-10b. It is an **ultimate** load, and napkin says so:
"≈ *p* lb each, ultimate (Wood Handbook Eq. 8-10b); a design load is a fraction of this". The
estimate applies only within Table 8-8's sizes and with a pilot hole. End grain is reduced per
chapter 8, and outside the table's sizes the result is "not checked".

A nail or brad closer to the receiving part's edge or end than chapter 8's rule is a warning line,
by joint. That is the only fastener "rule" napkin has a source for. There is **no** "required
fasteners" check, because nothing read requires any.

## 7. The "not checked" list

Every design's check panel ends with the standards napkin knows of and does not check, each with
its scope in one line from the research file:

- BIFMA X5.1, X5.5 and X5.9 (commercial and institutional);
- KCMA A161.1 (kitchen cabinets; napkin uses only the public shelf load);
- BHMA A156.9 (cabinet hardware grades; slide ratings come from the maker's datasheet);
- ASTM F1821-26 (toddler beds).

This list is static text in the Furniture module, cited, and is not a code pack.

## 7a. Where it lives: not the rules engine

The rules engine (M4, #12/#13) is for **adopted building codes**: jurisdiction, adoption date,
overlays and locking. None of that applies to furniture. There is no jurisdiction; the CFR is
federal and uniform, and the rest are voluntary. So furniture checks go in
**`Napkin.Modules.Furniture`**, in a `FurnitureChecks` type, with small cited data files under
`src/Napkin.Modules.Furniture/Data/`. They reuse only the result vocabulary (§1). This is a
decision (§9.5).

## 8. Slices

Each slice is one issue. They land in this order, and none starts before sign-off.

| Slice | What | Files |
|---|---|---|
| **A** | Species table: E and specific gravity at 12 % MC (and density) for the species the materials library sells, transcribed from Wood Handbook ch. 5 Tables 5-3b/5-3a in the slice's own task, cited per row. A species picker on a part (free text still allowed, and never interpreted) | `Modules.Furniture/Data/wood-properties.json`, `Part` species picker in the panel |
| **B** | Shelf sag (§3): shelf detection from joints, Eq. 9-2 with Table 9-1, the load and limit inputs, the check panel's first block, the ≈ wording | `FurnitureChecks.cs`, `ShelfSag.cs`, the panel |
| **C** | Drawer mark and extension on a joint group; the design marks (clothing storage / bunk bed / anchored), stored in the scene (a format bump) | Core.Geometry, Core.Project, the panel |
| **D** | Tip-over estimate (§4) and the F2057-23 scope flag, with the three FR conditions read into a cited data file in the slice's task | `TipOver.cs`, `Data/f2057-fr.json` |
| **E** | Bunk-bed guardrail check (§5), with the CFR limits read into a cited data file | `BunkBed.cs`, `Data/cfr-1213.json` |
| **F** | Screw withdrawal and nail distances (§6) | `FastenerHold.cs` |
| **G** | The "not checked" list (§7) and a GUI workflow over a bookcase and a dresser | panel, `tests/Napkin.App.GuiTests` |

A and B are what #154's question most needs: "what weight will this shelf carry". D is the only
mandatory rule. C is plumbing for D.

## 9. Decisions for Marc

1. **How napkin knows what a piece is:** the person marks a design as *clothing storage*,
   *bunk bed*, or neither. There is no automatic detection. Recommended: yes. The scope flag (§4.3)
   still speaks up when the sizes say "dresser" but the mark is missing.
2. **Shelf sag limit:** no default; napkin shows the estimate with no verdict until the person
   types a limit. Recommended: yes, because no source read gives one. The alternative, napkin's own
   labelled default ratio, would still be napkin's invention.
3. **Anchored units:** a clothing storage unit marked "anchored to the wall" skips the tip-over
   estimate and says why. Recommended: no. Show the estimate anyway, with "anchored" beside it,
   since the federal rule tests the unit un-anchored.
4. **Weight from species density:** napkin estimates weight from part volumes and a cited density,
   and says so. Recommended: yes. Otherwise the person types the unit's weight.
5. **Where it lives:** furniture checks in the Furniture module, not the rules engine (§7a).
   Recommended: yes.
6. **Scope:** A and B first, D next; E and F only if wanted. Recommended: A, B, C, D in this
   milestone; E, F, G to the backlog.
