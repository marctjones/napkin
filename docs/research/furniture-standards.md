# Research: furniture strength, load, stability and fastener standards (#154)

All retrievals 2026-09-26. Only what was read from the source is stated; failed fetches are recorded.

**Short answer:** No single standard covers "how strong furniture must be" or "which fasteners each piece needs."

- **Mandatory (US):** the only mandatory furniture-strength rule is tip-over stability for clothing storage units: 16 CFR 1261, which requires ASTM F2057-23. Child beds also have mandatory, mostly geometric, rules.
- **Voluntary:** BIFMA covers commercial office furniture, KCMA cabinets, and BHMA hardware. All are industry test standards.
- **Fasteners:** no source I read prescribes fastener type or spacing for furniture.
- **Engineering method:** the USDA Wood Handbook gives public-domain methods for deflection, screw withdrawal and nail edge distance. These are methods, not requirements.

## 1. 16 CFR part 1261, Safety Standard for Clothing Storage Units (CPSC, mandatory, US)
- **Source:** eCFR API, Title 16 as of 2026-09-24, `https://www.ecfr.gov/api/versioner/v1/full/2026-09-24/title-16.xml?part=1261`. The ecfr.gov HTML page redirected to a bot block.
  - Authority includes Pub. L. 117-328 Div. BB tit. II sec. 201 (STURDY).
  - Source: 88 FR 28408, May 4, 2023.
- **Scope (§1261.1):** the rule protects "children up to 72 months of age from tip-over-related death or injury". It covers "any free-standing furniture item … intended for the storage of clothing, typical of bedroom furniture" made after September 1, 2023.
- **Requirement (§1261.2):**
  - Each unit "that is subject to ASTM F2057-23 … approved on February 1, 2023, shall comply with ASTM F2057-23", which is incorporated by reference.
  - So the ASTM scope thresholds (§2 below) decide which units are covered.
  - The CFR itself has no loads. It says a free read-only copy is at astm.org/READINGLIBRARY/.
- **Test content, from the public-domain FR preamble** (`https://www.govinfo.gov/content/pkg/FR-2023-05-04/html/2023-08997.htm`):
  - **ASTM §9.2.1, Simulated Clothing Load:** hard, level surface, all extendible elements and doors open.
    - "If 50 percent or more of the storage volume is extended", the unit is filled with a simulated clothing load at 8.5 lb/ft³.
    - It must remain open 30 s without tipping.
  - **§9.2.2:** a 10 lb horizontal force is applied over ≥5 s at a "hand-hold" no higher than 56 in, then held ≥10 s.
  - **§9.2.3:** 60 lb is placed on the edge of an open drawer or pull-out shelf. The unit is tilted forward on a 0.43 in block (§8.2.3) with all extendibles open.
  - 60 lb is described as about a 95th-percentile 72-month-old. Warnings are in ASTM §10.
  - The preamble says the 8.5 lb/ft³ density and the carpet block match CPSC's November 2022 rule. Part 1261 now points only to F2057-23.
- **Usable as data?** Yes for the CFR and FR text: 17 U.S.C. §105 (law.cornell.edu/uscode/text/17/105) says copyright "is not available for any work of the United States Government". The detailed procedure (load placement, tolerances) exists only in the copyrighted ASTM text.

## 2. ASTM F2057-23, Standard Safety Specification for Clothing Storage Units
- **Source:** `https://www.astm.org/f2057-23.html`. WebFetch got 403; curl with a browser user-agent succeeded. The page marks -23 as Active, with editions -19, -17 and -14 listed.
- **Scope (§1.1):** "free-standing clothing storage units, including but not limited to chests, … armoires, … bureaus, door chests, and dressers" that are **≥27 in tall, ≥30 lb, ≥3.2 ft³ enclosed storage volume**.
- **Exclusions (§1.2):** shelving units such as bookcases and entertainment furniture, office and dining furniture, jewelry armoires, underbed drawer units, accent furniture, laundry units, and built-ins "intended to be permanently attached to the building".
- **Access:** sold. A free read-only copy is in ASTM's READINGLIBRARY (per 16 CFR 1261.2).
- **Usable as data?** Cite by name and scope. Use test parameters only as restated in the FR preamble.

## 3. BIFMA X5 series (voluntary, commercial/institutional)
- **bifma.org:** every page returned HTTP 403, both via WebFetch and via curl with browser headers. No official BIFMA page was read.
- **Read instead:** publisher sales listings at store.accuristech.com ("BIFMA: In Partnership with Accuris"). All show member/non-member pricing, meaning they are paywalled. No test loads were read.
  - **X5.9-2019 Storage Units** (`…/ansi-bifma-x5-9-2019?product_id=2033911`): "freestanding, mobile, and wall-mounted storage units". It "applies to products designed for use in commercial and institutional environments".
  - **X5.5-2021 Desk and Table Products** (`…/ansi-bifma-x5-5-2021?product_id=2208585`): covers "commercial office, institutional and educational environments; including retail spaces, restaurants, and cafeterias".
  - **X5.1-2017 (R2022) General-Purpose Office Chairs** (`…/ansi-bifma-x5-1-2017-r2022?product_id=1944483`): covers task, executive, guest and folding chairs and stools, and excludes lounge seating. Tests use NHANES "95th percentile male is 125 kg (275 pounds)" and an "estimated product life of ten years based on single-shift usage".
- **Usable as data?** Cite by name and scope only.

## 4. ANSI/KCMA A161.1 (kitchen cabinets; voluntary certification)
- **Source:** `https://kcma.org/insights/cabinets-certified-last-0`, KCMA's public test summary. The page shows "Published on January 17, 2023" and a Woodworking Network byline.
- **Structural tests:**
  - Shelves and bottoms at 15 lb/ft² for 7 days. The pass condition is "no excessive deflection and no visible sign of joint separation or failure", with **no numeric deflection limit**.
  - Wall-hung cabinets loaded gradually to 600 lb.
  - Base front joints take 250 lb (with drawer rails) or 200 lb (without).
- **Door and drawer tests:**
  - Doors: 25,000 cycles at 90° and a 65 lb load test.
  - Drawers: 15 lb/ft² load for 25,000 cycles.
- The FAQ page (`kcma.org/resources/Quality-Cabinet-Certification-FAQs`) says "14 rigorous, third-party tests".
- The A161.1 standard text itself was not located or read in this task.
- **Usable as data?** 15 lb/ft² could serve as a reference shelf load case, attributed to KCMA's page. It provides no pass/fail threshold.

## 5. ANSI/BHMA A156.9 Cabinet Hardware
- **Source:** `https://buildershardware.com/News/News-from-BHMA/ansibhma-a1569-cabinet-hardware`, dated April 1, 2019, quoting A156.9-2015 §1. The ANSI webstore lists a 2020 edition (title only; the page returned 403).
- **§1.1 scope:** hinges, pulls, catches, shelf rests, standards and brackets, drawer slides, and more, with "operational, cyclical, strength, and finish criteria".
- **§1.2:** "applicable to hardware products only and are not intended to evaluate systems incorporating cabinet components".
- **§1.3:** "Three grades are offered for most products; for drawer slides, additional grades 1HD are available."
- Per-grade loads were not read.
- **Usable as data?** Cite grade names. Slide capacities come from manufacturer datasheets.

## 6. Children's beds (CPSC, brief)
- **16 CFR 1213 / 1513 (bunk beds), eCFR API as of 2026-09-24:** these rules address entrapment, not strength.
  - A bunk bed has a foundation underside "over 30 inches (760 mm) from the floor".
  - It needs ≥2 guardrails, with the top ≥5 in above the maximum-thickness mattress (§1213.3(a)(6)).
  - Continuous-guardrail gaps must be ≤0.22 in (§1213.3(a)(2)).
  - Part 1513, for bunk beds intended for children, is "substantively identical".
- **16 CFR 1217 (toddler beds):** incorporates ASTM F1821-26 (91 FR 27203, May 14, 2026). No numbers are in the CFR.
- **Usable as data?** The CFR geometric limits are public domain and checkable.

## 7. USDA FPL Wood Handbook, FPL-GTR-282 (2021)
- **Sources:** `https://www.fpl.fs.usda.gov/documnts/fplgtr/fplgtr282/chapter_0{5,8,9}_fpl_gtr282.pdf`. The citation is from research.fs.usda.gov/treesearch/62200: "Forest Products Laboratory. 2021 … FPL-GTR-282".
- **Ch. 9, Eq. (9-2):** δ = k_b·W·L³/(E·I) + k_s·W·L/(G·A′).
  - Table 9-1: uniform load on a simply supported span gives k_b = 5/384 and k_s = 1/8. Uniform load with both ends clamped gives k_b = 1/384.
  - The chapter names "furniture" among its applications. It warns that beams "usually sag in time" (creep).
- **Ch. 5:**
  - Tables 5-3a/b give bending E by species, green and at 12% MC. For example, northern red oak at 12% is 1.82 ×10⁶ lbf/in² (Table 5-3b).
  - Tabulated E_L "includes an effect of shear deflection" and can be raised about 10% to remove it.
  - Creep "may approximately equal the initial, instantaneous elastic deformation" after several years. An increase of about 28 °C can cause "a two- to threefold increase in creep".
- **Ch. 8:**
  - Eq. (8-10b): wood-screw side-grain withdrawal p = 15,700·G²·D·L (lb), an *ultimate* load. It applies with lead holes about 70% of root diameter (softwood) or 90% (hardwood), and only within the Table 8-8 screw sizes.
  - End grain averages 75% of side grain.
  - Nails go "no closer to the edge … than one-half its thickness and no closer to the end than the thickness of the piece".
- **Public domain:** yes under 17 U.S.C. §105. **Caveat:** Ch. 9 has a Colorado State University co-author, so check authorship per chapter before copying prose. Cited numeric data is low-risk.

## Conclusions for napkin

**Can check honestly from its own data**
- **Shelf and top sag:** Wood Handbook Eq. 9-2 with Table 9-1 and Ch. 5 species E, plus a creep allowance (Ch. 5 suggests about 2× long-term). Label it an engineering estimate.
  - A load case can use KCMA's public 15 lb/ft², attributed to KCMA.
  - No source read gives a furniture **deflection limit**, so any threshold is napkin's or the user's choice and must say so.
- **Fastener capacity (not "required fasteners"):** estimate screw withdrawal with Eq. 8-10 and flag the nail edge/end-distance rule. Nothing read *requires* specific furniture fasteners.
- **Bunk-bed guardrails:** geometric check against 16 CFR 1213/1513.
- **Dresser tip-over:** static geometric estimate using the FR-described conditions.
  - The conditions are 60 lb on an open drawer edge, a 0.43 in forward tilt, and all extendibles open. Add the 8.5 lb/ft³ clothing fill only when ≥50% of storage volume is extended (§9.2.1).
  - Wording: "approximates F2057-23 §9.2.3 as described at 88 FR 28408", never "complies".
  - Flag when the design meets the ASTM scope thresholds (≥27 in, ≥30 lb, ≥3.2 ft³).

**Cite by name only:** ASTM F2057-23 beyond the FR summary, BIFMA X5.1/X5.5/X5.9, KCMA A161.1 beyond KCMA's summary, BHMA A156.9 grades, and ASTM F1821-26.

**Out of reach:** BIFMA test loads (paywalled; bifma.org blocked every fetch), BHMA per-grade loads and cycles, ASTM detailed procedures, and the A161.1 standard text (not located).
