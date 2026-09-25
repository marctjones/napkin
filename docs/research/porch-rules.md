# Research: code rules for an attached, unconditioned three-season porch (#188)

Feeds the porch design note for M11. Base code is the 2021 IRC as adopted, with amendments,
by the 2022 Connecticut State Building Code (CSBC). Retrieved 2026-09-25.

Primary sources used:

- **CT amendments PDF**: 2022 Connecticut State Building Code (w/ Errata #1), "Amendments to the
  2021 International Residential Code," https://portal.ct.gov/-/media/DAS/Office-of-State-Building-Inspector/2022-State-Codes/2022-CSBC-Final.pdf
  — downloaded and text-extracted (`pdftotext -layout`) 2026-09-25. Page numbers below are the
  document's own printed footers ("Page - N").
- **UpCodes rendering of IRC 2021 (as adopted by Connecticut)**, https://up.codes/viewer/connecticut/irc-2021/
  — used to read 2021 IRC base sections that Connecticut's amendments document does not reprint
  (Connecticut's PDF is amendments-only: it shows only sections it adds, deletes, or amends).
  Cited per Marc's 2026-09-25 decision that transcribed/rendered code content may be used when read
  from a source and cited. Retrieved 2026-09-25.
- **UpCodes rendering of IECC 2021 (as adopted by Connecticut)**, https://up.codes/viewer/connecticut/irc-2021/chapter/11/re-energy-efficiency
  (Connecticut's energy chapter [RE] is the 2021 IECC residential provisions folded into the IRC
  numbering as Chapter 11). Retrieved 2026-09-25.
- `packs/packs/us-ct-2022/ct-overlay-data.json` — already in the repo, CT's Table R301.2 frost
  depth (42") and Appendix AY location, transcribed from the same CT PDF.

## Summary

| # | Question | Answer | Citation |
|---|---|---|---|
| 1 | How does the code classify an attached, unconditioned three-season porch? | Depends on the glazing ratio. IRC §R202/§N1101.6 defines a **sunroom** as "a one-story structure attached to a dwelling with a glazing area in excess of 40 percent of the gross area of the structure's exterior walls and roof." If the porch clears that ratio, §R301.2.1.1.1 requires it to comply with AAMA/NPEA/NSA 2100 and be assigned one of 5 categories; an unconditioned three-season porch is **Category I, II, or III** ("nonhabitable and unconditioned"). If it does **not** clear the 40% ratio (e.g., a screened porch with a solid shingled roof and mostly-screen walls — screening is not glazing), it is not a "sunroom" under this definition and is instead an ordinary unconditioned roofed addition, governed by the general structural chapters and the IECC's "conditioned space" scoping rather than §R301.2.1.1.1 or §N1102.2.13. | IRC 2021 §R202/§N1101.6 definition and §R301.2.1.1.1 categories, via UpCodes CT rendering, retrieved 2026-09-25 |
| 2 | Can it sit on piers instead of a continuous foundation? | Yes, structurally — CT's amended §R403.1 allows "continuous solid or fully grouted masonry or concrete footings, crushed stone footings, wood foundations **or other approved structural systems**," and §R403.1.4.1 (CT-amended) names "foundation walls, **piers** and other permanent supports" as the class of thing frost protection applies to — piers are a recognized support type, not just continuous footings. But an attached porch does **not** get the frost-depth exemption CT gives to decks (see Q2 detail) — its piers must still reach the frost line (or use one of the alternate methods) because it is attached to and supported partly by the dwelling. | CT amendments PDF, §R403.1 and §R403.1.4.1, p.144, retrieved 2026-09-25 |
| 3 | Frost depth | **42 inches (3 ft 6 in)**, from CT's amended Table R301.2, and piers/footings must extend below that line unless built to §R403.3 (frost-protected shallow foundation), ASCE 32, or on solid rock. | CT amendments PDF, Table R301.2, p.131, retrieved 2026-09-25 (already in `ct-overlay-data.json`) |
| 4 | What does CT change relevant to a porch? | (a) Rewrites §R403.1 General and §R403.1.4.1 Frost protection (above); (b) deletes footing/pier sections R403.1.2, R403.1.3, R403.1.3.1–.6 tied to Seismic Design Categories D0–D2 (moot — CT is SDC B statewide) — R403.1.3.6 (isolated footings) applied only to *detached* one/two-family dwellings ≤3 stories anyway, so its deletion does not remove pier permission for an *attached* porch; (c) sets ground snow load and wind speed to "as set forth in Appendix AY" rather than a single statewide number; (d) amends Table R602.7 footnotes for wall header spans below 30 psf ground snow load; (e) exempts "the addition of a porch or deck" from triggering whole-house smoke-alarm (§R314.2.2 Exception 1) and carbon-monoxide-alarm (§R315.2.2 Exception 1) retrofits that other alterations/additions would trigger. CT's PDF does **not** amend the sunroom definition/section (§R202, §R301.2.1.1.1) or the IECC sunroom insulation section (§N1102.2.13) — those stand as base 2021 IRC/IECC text. | CT amendments PDF, Ch.4 pp.144–145, Ch.6 p.145, §§R314.2.2/R315.2.2 p.139, retrieved 2026-09-25 |
| 5 | Roof snow and wind source | Connecticut Table R301.2 points to **Appendix AY** (a per-municipality table of ultimate wind speed Vult, nominal wind speed Vasd, ground snow load pg, and hurricane-prone-region flag; 169 municipality rows) instead of one statewide value; Appendix AY's header and table run pp.157–160. Roof design pressures for the sunroom/porch also draw on IRC §R301.2.1.1.1's own note pointing to Table R301.2.1(1) component-and-cladding pressures and §R301.2.1 main-windforce-resisting-system pressures. | CT amendments PDF, Appendix AY, pp.157–160, retrieved 2026-09-25; IRC 2021 §R301.2.1.1.1 via UpCodes, retrieved 2026-09-25 |

## Detail

### 1. Classification (sunroom definition + IRC §R301.2.1.1.1)

Connecticut's amendments PDF does not touch this section (no hit for "sunroom" anywhere in the
document's extracted text), so the base 2021 IRC/IECC text applies verbatim in Connecticut. The
threshold definition, read via the UpCodes CT-specific Chapter 11 [RE] rendering (§R202/§N1101.6):

> "Sunroom: A one-story structure attached to a dwelling with a glazing area in excess of 40
> percent of the gross area of the structure's exterior walls and roof."

**This 40% glazing-vs.-gross-wall-and-roof-area ratio is the actual discriminator.** A three-season
porch that is mostly screen (insect screening, not glazing) with a conventional shingled roof can
fail this ratio — in which case it is not a "sunroom" for code purposes at all, §R301.2.1.1.1 and
the IECC sunroom-insulation section (§N1102.2.13, below) do not apply, and the space is just an
ordinary unconditioned roofed addition subject to the general structural chapters. If the ratio is
met, §R301.2.1.1.1 (via UpCodes CT rendering) applies:

> "Sunrooms shall comply with AAMA/NPEA/NSA 2100. For the purpose of applying the criteria of
> AAMA/NPEA/NSA 2100 based on the intended use, sunrooms shall be identified as one of the
> following categories..." — IRC 2021 §R301.2.1.1.1

The five categories (condensed from the same section, via UpCodes CT rendering):

- **Category I** — thermally isolated; walls open, screened, or thin plastic film; "nonhabitable
  and unconditioned."
- **Category II** — thermally isolated; enclosed walls with translucent/transparent plastic or
  glass; "nonhabitable and unconditioned."
- **Category III** — thermally isolated; enclosed glazed walls meeting extra air-infiltration and
  water-penetration resistance requirements; "nonhabitable and unconditioned."
- **Category IV** — thermally isolated but separately heated/cooled; "nonhabitable and
  conditioned."
- **Category V** — open to the main structure, heated or cooled with it; "habitable and
  conditioned."

A three-season porch — usable spring through fall without heat, screened or glazed, not tied into
the house HVAC — fits **Category I** (screened) or **Category II/III** (glazed but unconditioned).
Category IV/V would make it a year-round conditioned addition, a different design case.

Chapters brought in by this classification:
- **Structural/foundation**: normal IRC Chapters 3–4 (loads, footings, frost protection) — a
  sunroom is not given a lighter structural path merely by being a "sunroom"; §R301.2.1.1.1 itself
  cross-references Table R301.2.1(1) (component and cladding pressures) and §R301.2.1 (main
  windforce-resisting system) for its own loads.
- **Floor/wall/roof framing**: standard Chapters 5–8, sized per the same wind/snow/seismic
  criteria as the rest of the house (CT's Table R301.2 / Appendix AY).
- **Energy (IECC/Chapter 11)**: because Category I–III are "unconditioned," they fall outside the
  conditioned-space envelope requirements that Chapter 11 (2021 IECC) imposes — see below.

### 2 & 3. Foundation type and frost protection

CT's amended §R403.1 General (p.144):

> "All exterior walls shall be supported on continuous solid or fully grouted masonry or concrete
> footings, crushed stone footings, wood foundations or other approved structural systems..."

The only exception is for **freestanding accessory structures** ≤600 sf with eave height ≤10 ft —
not applicable to an attached porch (a porch with a roof tied to the house wall is not
freestanding).

CT's amended §R403.1.4.1 Frost protection (p.144):

> "Except where otherwise protected from frost, foundation walls, piers and other permanent
> supports of buildings and structures shall be protected from frost by one or more of the
> following methods: 1. Extended below the frost line specified in Table R301.2.(1). 2.
> Constructed in accordance with Section R403.3. 3. Constructed in accordance with ASCE 32. 4.
> Erected on solid rock. Footings shall not bear on frozen soil unless the frozen condition is
> permanent."

Its exceptions carve out **freestanding accessory structures** (again, not applicable, p.144) and,
for decks/ramps specifically (p.145):

> "3. Decks and ramps not supported by a dwelling need not be provided with footings that extend
> below the frost line. 4. The footing for the grade-level termination of stairs or ramps attached
> to decks or landings... shall only be required to be placed at least 12 inches... below the
> undisturbed ground surface."

An attached three-season porch is supported by, and structurally tied to, the dwelling — it is
neither a freestanding accessory structure nor a deck/ramp "not supported by a dwelling" — so
neither exception applies. **Piers are allowed as a support type** (named explicitly in the text
above), but each pier must independently satisfy one of the four frost-protection methods, most
commonly extending to the 42" frost line (Table R301.2, p.131) or being built as a
frost-protected shallow foundation per §R403.3.

Connecticut also deletes §§403.1.2, 403.1.3, 403.1.3.1–.6 (prescriptive continuous-footing/
isolated-footing rules tied to Seismic Design Categories D0–D2, p.144) because Connecticut's own
Table R301.2 sets Seismic Design Category **B** statewide. Base 2021 IRC §R403.1.3.6 (read via a
non-CT UpCodes rendering, since CT's own copy is deleted) scoped isolated concrete footings to
"detached one- and two-family dwellings that are three stories or less in height and constructed
with stud bearing walls" — it never covered an *attached* porch in the first place, so deleting it
removes nothing an attached porch could have relied on; the general "other approved structural
systems" clause in §R403.1 (above) remains the operative permission for attached piers.

### 4. Connecticut amendments touching this scope

From the CT amendments PDF (Chapter 4, pp.144–145; Chapter 3, p.139; Chapter 6, p.145):

- **§R403.1 General** (Amd, p.144) — quoted above; adds "or other approved structural systems"
  language and the freestanding-accessory-structure exception.
- **§403.1.2, §403.1.3, §403.1.3.1–.3.6** (Del, p.144) — seismic category D0–D2 footing/pier
  detailing removed (moot; CT is SDC B); see the R403.1.3.6 scope note above.
- **§R403.1.4.1 Frost protection** (Amd, pp.144–145) — quoted above; explicitly names piers, adds
  deck/ramp/stair exceptions.
- **§R404.6, §R404.6.1** (Add, p.144) — deep foundations must comply with IBC §1810 and get
  special inspections per IBC §§1705.7–1705.10 (unlikely to apply to a residential porch, but
  relevant if a designer proposes a helical-pile/deep-pier system).
- **§R405.3** (Add, p.144) — above-grade drainage (gutters, downspouts, roof/yard drains) may not
  connect to the foundation drainage system — relevant to how a porch roof sheds water near its
  footings.
- **Table R301.2** (Amd, p.131) — ground snow load and wind speed replaced with "as set forth in
  Appendix AY" (per-municipality) instead of one statewide figure; frost line depth 42"; Seismic
  Design Category B.
- **Table R602.7(1) footnote e / R602.7(3) footnote b** (Amd, p.145) — for wall header spans, use
  30 psf ground snow load where the town's actual load is below 30 psf and roof live load ≤20 psf;
  interpolate between 30–50 psf (already encoded in
  `packs/packs/us-ct-2022/amendments/r602.7-1.json` / `r602.7-3.json`).
- **§R314.2.2 Exception 1 / §R315.2.2 Exception 1** (Amd, p.139) — "the addition of a porch or
  deck" is listed alongside re-roofing, re-siding, and window/door replacement as work that does
  **not** trigger a whole-house smoke-alarm or carbon-monoxide-alarm retrofit that other
  alterations/additions would otherwise require.

Not amended by Connecticut (base 2021 IRC/IECC governs as-is): §R202/§N1101.6 (sunroom
definition), §R301.2.1.1.1 (sunroom categories), and §N1102.2.13 (sunroom insulation/envelope
requirement, below).

### Energy code note (Chapter 11 [RE] / 2021 IECC, not directly asked but bears on classification)

Via UpCodes CT rendering of Chapter 11 [RE] and the general (non-CT) UpCodes rendering of the same
section number, since the CT Chapter 11 fetch truncated before reaching it:

> "Sunroom: A one-story structure attached to a dwelling with a glazing area in excess of 40
> percent..." and "Thermal isolation: Physical and space conditioning separation from conditioned
> spaces..." — §N1101.6 (cross-referencing R202), UpCodes CT rendering

> "Sunrooms enclosing conditioned space shall meet the insulation requirements of this code" ...
> "Walls separating a sunroom with a thermal isolation from conditioned space shall comply with the
> building thermal envelope requirements of this code." — §N1102.2.13 (R402.2.13) Sunroom
> insulation, via UpCodes, retrieved 2026-09-25 (fetched from the general UpCodes rendering, not
> confirmed against the CT-specific page directly — CT's amendments PDF has no hits for
> "sunroom," so there is no reason to expect CT-specific text here, but see Gaps)

Practical reading: a Category I–III (unconditioned) three-season porch itself is not subject to
IECC insulation/fenestration U-factor requirements, but the **wall between the porch and the
conditioned house** must meet the house's normal envelope requirements (insulation, air sealing,
window U-factor) as if it were an exterior wall.

### 5. Roof snow and wind (Appendix AY)

CT's Table R301.2 (Amd), p.131, replaces single statewide wind/snow numbers with pointers:

> "GROUND SNOW LOAD ... As set forth in Appendix AY. ... WIND DESIGN Speed (mph) ... As set forth
> in Appendix AY."

Appendix AY (Add), "WIND SPEEDS, SEISMIC DESIGN CATEGORIES and GROUND SNOW LOADS," pp.157–160
(the appendix header and the full 169-row per-municipality table both fall between the "Page - 156"
and "Page - 160" footers, i.e. on pages 157 through 160): a per-municipality table with columns
Ultimate Design Wind Speed *V*ult (mph), Nominal Design Wind Speed *V*asd (mph), Ground Snow Load
*pg* (psf), and Hurricane-Prone Region (Yes/–). First and last rows read directly from the
extracted text: "Andover 120 93 30 Yes" (p.157) ... "Woodstock 120 93 40 Yes" (p.160). Despite the
appendix's title, **it has no seismic design category column** — Table R301.2 sets SDC B statewide
instead.

For the sunroom/porch roof specifically, IRC §R301.2.1.1.1 itself notes that component-and-
cladding pressures come from Table R301.2.1(1) and main-windforce-resisting-system pressures from
§R301.2.1 — i.e., the porch roof uses the same town-specific Vult/pg from Appendix AY as the rest
of the house, run through the normal Chapter 3 pressure tables, not a separate sunroom-specific
load path.

## What napkin could check vs. only cite

**Could check (structured data, testable against a design):**
- The 40% glazing-area-vs.-gross-wall-and-roof-area ratio (§R202/§N1101.6) — the most concrete
  checkable item here: napkin already models wall/roof/opening geometry, so it can compute this
  ratio directly and tell the user whether their porch is a code "sunroom" (routing to
  §R301.2.1.1.1/§N1102.2.13) or an ordinary roofed addition.
- Frost depth compliance: pier/footing bottom elevation ≥ 42" below grade (or flagged as needing
  §R403.3/ASCE 32/solid-rock justification) — `frostLineDepth` already in `ct-overlay-data.json`.
- Whether the porch is "supported by a dwelling" (attached) vs. a genuinely freestanding structure
  — determines whether the deck/freestanding exceptions in §R403.1 / §R403.1.4.1 can even be
  offered as an option.
- Category selection consistency: if the user marks the porch "unconditioned," napkin could flag
  Category IV/V inputs (a heating/cooling system on the porch) as inconsistent, or vice versa.
- Ground snow load / wind speed lookup by CT municipality once Appendix AY's per-town table is
  transcribed into the pack (currently `valuesTranscribed: false` — a gap, see below).
- Wall-header snow-load footnote interpolation (30–50 psf) — already encoded in the amendments
  JSON files for R602.7.

**Only cite (judgment calls or non-structured requirements napkin should surface, not enforce):**
- Which sunroom Category (I–V) actually fits the user's described porch — that's a design/intent
  decision by the user, not something napkin can infer from geometry alone.
- Whether an "other approved structural system" (§R403.1) is acceptable for a given pier design —
  engineering judgment/local building official approval.
- AAMA/NPEA/NSA 2100 fenestration performance requirements (air infiltration, water penetration)
  for Category II/III glazing — a referenced standard napkin doesn't have licensed text for.
- Deep-foundation special inspection requirements (§R404.6.1) if a design goes that route.

## Gaps

- **Appendix AY per-town wind/snow values are not transcribed** into `ct-overlay-data.json`
  (`valuesTranscribed: false`, confirmed still true here) — napkin cannot look up a given town's
  Vult/Vasd/pg/hurricane-flag today; a project must enter these manually until someone transcribes
  the full 169-municipality table (row count from the extracted PDF text, 2026-09-25).
- **AAMA/NPEA/NSA 2100 full standard text** was not read (it is a purchased ANSI-approved standard,
  https://store.fgiaonline.org/AAMA/NSA-2100-19.pdf and similar — not freely available); only the
  IRC's own summary of the 5 categories, as rendered by UpCodes, was read and cited. Napkin should
  not claim to enforce AAMA/NPEA/NSA 2100's internal engineering requirements beyond the category
  definitions quoted here.
- **§R507 (deck) footing/post provisions** were referenced only secondhand via search-engine
  summaries, not read from a primary IRC or UpCodes page directly, because the porch case (attached,
  roofed, not a deck) makes §R507 likely inapplicable — flagged rather than cited as fact. If a
  future porch design includes an open deck portion, §R507 should be read and cited fresh.
- **UpCodes' own chapter-2 definitions page** for R202 timed out with a truncated fetch and did not
  yield the "Sunroom" definition text directly (it was instead confirmed via the Chapter 11 [RE]
  cross-reference and via §R301.2.1.1.1 directly) — not a contradiction, but the Chapter 2 page
  itself was not successfully read start-to-finish.
- **ICC's own codes.iccsafe.org page for §R301.2.1.1.1 returned HTTP 403** (blocked); the UpCodes
  CT-specific rendering of the same section was used instead and is the citation of record above.
- **§N1102.2.13 (sunroom insulation) was read from a general (non-CT) UpCodes page**, not the
  CT-specific Chapter 11 [RE] page directly — a direct CT-page fetch for that section truncated
  before reaching it. Because CT's amendments PDF has zero hits for "sunroom," there is no
  positive evidence CT changes this section, but it was not confirmed word-for-word against the
  CT-hosted page; a future task should re-fetch
  `up.codes/viewer/connecticut/irc-2021/chapter/11/re-energy-efficiency` targeted at N1102.2.13
  specifically to close this out.
- **IRC §R308 (hazardous glazing locations)** has no hits in Connecticut's amendments PDF, so it is
  presumed unamended, but was not itself read from any source in this task (out of scope for the
  four questions asked) — listed here only so it isn't mistaken for "checked."
- **§R403.1.1 (minimum footing size, 12"×6") and §R403.1.4 (minimum 12" footing depth)** were read
  from UpCodes (Maryland and CT renderings) but not quoted at length above since they are generic
  footing-size rules, not porch- or pier-specific; CT's amendments PDF does not show either section
  in its "Amd/Add/Del" list, so base IRC text governs. §R407 (Columns) text could not be retrieved
  from either UpCodes fetch (table-of-contents only) — a gap if napkin later needs prescriptive
  column sizing for porch piers.
