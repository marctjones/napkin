# Research: is there a citable "safe default" source for wall-opening headers?

**Outcome: negative. No design note was written; nothing here should land as a feature.** Research
by Fable, 2026-09-27, for Marc's ask of the same day: *"most people are still going to over engineer
things to be safe. And we should support the over engineering with perhaps an over engineering data
packet for safe default"* — accepted with the constraint that napkin cannot invent the numbers: the
pack needs "a real, citable, generously-conservative source", not a made-up multiplier. This note
records what was checked, with URLs, hashes and retrieval dates, so the next session does not repeat
it, and one licence finding that reaches beyond this task (§1).

Every claim below is from the named source read on 2026-09-27 unless it says otherwise. No span,
load or design value is written here from memory or copied from a source: the candidates are judged
on their own scope statements and footnotes, quoted, not on their numbers.

## The bar a source had to clear

1. **Not the IRC or NDS itself** — independently licensed, the way AWC's DCA 6 was for decks (#209).
2. **Explicitly and provably more conservative than the adopted code's own minimum** for a header
   over a wall opening — such that taking its worst-case cell within scope is defensibly "safely
   oversized for anything less extreme", not merely a different table with different numbers.
3. **Actually covers headers over wall openings** (not joists, rafters or decks).

Connecticut's site values, for scale: the CT 2022 pack's per-town ground snow loads are 30, 35 and
40 psf and its ultimate wind speeds 115–129 mph (`packs/packs/us-ct-2022/site-values.json`,
Appendix AY, transcribed 2026-09-26); seismic B statewide.

## 1. A licence finding that comes first: AWC's End User License Agreement

The deck guide note's risk 8 recorded that "AWC's terms of use were not read for this note". They
were read for this one, because every serious header candidate turned out to be an AWC document.

**Source:** https://awc.org/about/end-user-license-agreement/ — the page the 2018 WFCM publication
page links as "End User License Agreement". Retrieved 2026-09-27 through the WebFetch tool in three
passes — a summary, then two asking for verbatim clauses (the page is behind a Cloudflare challenge
that blocks `curl`); the tool returned short
verbatim fragments and summaries, so **Marc should read the page himself** before relying on any
wording below. Fragments as returned, capitals as on the page:

- A paragraph before the numbered sections: "USE OF ANY 'PAGE SCRAPING,' 'DEEP LINK,' 'ROBOT,'
  'SPIDER' OR OTHER AUTOMATIC DEVICE…" (the sentence continues; not returned in full).
- The same preamble: "INPUTTING THE PRODUCT OR ANY PORTION THEREOF INTO ANY ARTIFICIAL INTELLIGENCE
  OR SIMILAR PROGRAM, SUCH AS CHAT GPT, IS PROHIBITED."
- §1(b): "You may install and permit access to one copy of the Product on each of two computers…
  You may make one (1) copy the Product for backup purposes only." and "You shall not merge, adapt,
  translate, modify, rent, lease, sell, sublicense, assign, or otherwise transfer any of the Product,
  or remove any proprietary notice or label appearing on any Product."
- §1(c): "You may print one copy of the Product." and "You may not otherwise reproduce, resell,
  reconvey, or grant others permission to reprint the Product or any of its files."
- §1(e): "You acknowledge and agree that the Product is proprietary to the American Wood Council
  (the 'Owner'), and is protected under U.S. copyright law and international copyright treaties."

The tool's summary added that the agreement does not distinguish free from paid publications and has
no attribution clause. Whether "the Product" covers a free PDF such as DCA 6 or the free view-only
WFCM chapters, and what the artificial-intelligence sentence means for tables an agent transcribed,
are legal readings that are Marc's, not this note's. **It bears on the landed DCA 6 tables (#41,
#42) as much as on anything proposed here**, so it is reported first and separately. Each AWC PDF
read for this note also carries its own notice — the 140 mph guide's is "No part of this publication
may be reproduced, distributed, or transmitted in any form or by any means … without express written
permission of the American Wood Council" (the unnumbered title-page verso, before the page marked
"i") — the same posture DCA 6's cover took.

Because of the AI sentence, this session **stopped extracting text from AWC PDFs** once the EULA
was read (§3, WFCM 2018). Before it was read, this session had already extracted the text of the
three WFCM High Wind Guides and the 2018 WFCM Workbook (§2.3, §2.4); that is recorded here rather
than hidden.

## 2. Candidates checked, and why each fails the bar

### 2.1 AWC's Design for Code Acceptance (DCA) series

https://awc.org/collection/design-for-code-acceptance/ (2026-09-27). The collection lists DCA 1
(flame spread), DCA 3 (fire-resistance-rated assemblies), DCA 4 (2014 and 2026 editions, the
component additive method for fire resistance), DCA 5 (post-frame buildings), DCA 6 (the 2015 deck
guide, English and Spanish) and DCA 7 (2012 IECC energy). **Nothing on headers, beams or wall
framing. Fails 3.**

### 2.2 AWC's span calculator

https://awc.org/calculators/span-options-calculator-for-wood-joists-and-rafters/ (2026-09-27):
"Joists and rafter spans for common loading conditions." Re-checked as the task asked; **joists and
rafters only. Fails 3.**

### 2.3 AWC *Guide to Wood Frame Construction in High Wind Areas* (WFCM Guides), 2015 edition

Free PDFs on AWC's media host, one per wind zone; three were read:

| Zone | URL | SHA-256 | Pages |
|---|---|---|---|
| 115 mph Exp. B | https://web-media.awc.org/wp-content/uploads/2021/12/17210705/AWC-WFCM2015-HWG115B-1703.pdf | `4064cd3af7a5e49aca57ae586e513a7a654d99ddaad66d80b848c82842199af2` | 36 |
| 140 mph Exp. B | https://web-media.awc.org/wp-content/uploads/2021/12/17210700/AWC-WFCM2015-HWG140B-1703.pdf | `2f3c5c584fbdace6213a239e007aff3246b881b1f7d31c6dd05f2b7fe712d0bc` | 36 |
| 150 mph Exp. C | https://web-media.awc.org/wp-content/uploads/2021/12/17210658/AWC-WFCM2015-HWG150C-1703.pdf | `ce0fac789d4061d9b96fec4c673f652786b90e3ffc6df8202bd5a9deed361331` | 36 |

(All three PDFs carry a 2015-01-17 creation date; the `awc.org/wp-content/…` URLs the search
returned redirect to these `web-media.awc.org` files.)

The 140 mph guide is "Copyright © 2015 by American Wood Council … First Web Version: November 2015,
ISBN 978-1-940383-29-3", "Based on the 2015 WFCM" (title-page verso). It has a header table — **Table 11,
Headers in Loadbearing Exterior Walls** (p. 17), by header span in feet with a minimum header size,
full-height studs at each end and uplift and lateral connection loads — but the document itself says
it is not a gravity header table:

- Table 11 footnote 1 (p. 17): "Header is sized to resist a 20 psf live load and a 20 psf dead load
  for a 40 SF/LF tributary area (36 foot building width two-foot overhangs). Uplift and lateral loads
  are from wind pressures."
- §4.2 (p. 16): "Headers Resisting Gravity Loads. Framing above openings in exterior walls that
  resist gravity loads shall be in accordance with the governing building code."
- Foreword (p. 1): "This Guide does not specifically address other loading conditions such as those
  created by live, seismic (earthquake), snow, or flood loads. These additional loading conditions
  must be considered as required by the building code and are addressed in the WFCM."
- §1.1 (p. 2): "Structural elements not complying with the conditions contained in this Guide shall
  be designed in accordance with the WFCM or the governing building code."

So the one AWC header table that is a plain free download is a **wind-connection** table sized for a
20 psf roof live load, one roof-only load case and one building width, and it hands gravity sizing
back to the code. Under Connecticut's 30–40 psf ground snow it is not conservative for gravity; it is
silent on it by design. **Fails 2 outright** (it is less than the code's question, not more than
it), and 1 is the AWC EULA question of §1.

### 2.4 AWC *2018 WFCM Workbook*

https://web-media.awc.org/wp-content/uploads/2021/12/17210707/AWC-WFCM2018-Workbook-181128.pdf
(free; SHA-256 `ea6d2e9bc4b2b61ba874ccb6e16ce64bdd118a2a228b40f037a6757d3fa2111a`, 85 pp.,
created 2018-09-27; "Copyright © 2018 by American Wood Council", p. ii). A worked example on one two-storey house
(ground snow load 30 psf in its design data), not a table. Its header page ("Exterior Loadbearing
Wall Headers (WFCM 3.4.1.4.1)", p. 37) selects headers from "Tables 3.22A-E and Table 3.22F" by
building width, span and ground snow load, with jack studs from Table 3.22F and a roof-span
adjustment, dropped (wind) headers from Table 3.23A, and interior loadbearing headers from Tables
3.24A-C. Useful only as evidence of what the WFCM's header tables are keyed on. **Fails 3 as a source
(it holds no table).**

### 2.5 ANSI/AWC WFCM-2018, Chapter 3 Prescriptive Design — the one real candidate, blocked

The 2018 WFCM publication page (https://awc.org/codes-standards/publications/wfcm-2018/ and
https://awc.org/resources/2018-wfcm/, 2026-09-27) offers free **view-only** PDFs of the table of
contents, Chapters 1–3, the Supplement and the References through AWC's viewer, and says "Searching,
printing, and zooming features are available exclusively in the purchased PDF version of this
publication." The files behind the viewer download directly:

| Part | URL | SHA-256 | pdfinfo |
|---|---|---|---|
| TOC | https://web-media.awc.org/wp-content/uploads/2021/12/17210819/AWC_WFCM2018_20190430_AWCWebsite_TOC.pdf | `287a67cd95b8189a5f833a23d575cb405af4c969e65acf732da210fe372d4015` | 8 pp., 2020-06-08, encrypted print:no copy:no |
| Chapter 1 | https://web-media.awc.org/wp-content/uploads/2021/12/17210819/AWC_WFCM2018_20190430_AWCWebsite_Chapter01.pdf | `4b857e7ccfb9abd9847324fc126c15b9a933bc696bfa1052fb38d05e69e21218` | 12 pp., 2020-06-08, encrypted print:no copy:no |
| **Chapter 3** | https://awc.org/wp-content/uploads/2026/08/AWC_WFCM2018_20190430_website_chapter-3-prescriptive-design.pdf | `2d97f48c825f39b5f1e763c4ea0ade96ccac62ce776f36a094b6cfcfcce48e4e` | 224 pp., title "2018 Wood Frame Construction Manual (WFCM) for One- and Two-Family Dwellings", created 2026-08-26, encrypted print:no copy:no (AES-256) |
| References | https://web-media.awc.org/wp-content/uploads/2021/12/17210820/AWC_WFCM2018_20190430_AWCWebsite_References.pdf | `c764582396213d6e7d88daf118fc04b7f129db8afe6484c1445503a5a58a97f5` | 4 pp., 2020-06-08 |

**Their content was not read** — only hashed and their metadata listed — because §1's EULA sentence
was read first. What is known about them comes from outside the PDFs:

- The 2021 IRC lists "WFCM—18, Wood Frame Construction Manual for One- and Two-Family Dwellings" in
  Chapter 44, referenced from R301.1, and R301.1.1 *Alternative provisions* names "AWC Wood Frame
  Construction Manual (WFCM)" first among the standards permitted as an alternative "subject to the
  limitations of this code and the limitations therein" (read 2026-09-27 in UpCodes' Connecticut
  2021 IRC viewer, https://up.codes/viewer/connecticut/irc-2021/chapter/44/referenced-standards and
  …/chapter/3/building-planning — a secondary viewer, used here for section structure only).
- The WFCM's header tables exist and are keyed on building width, span, ground snow load and
  species/grade, with a jack-stud table (§2.4, from the Workbook).
- A state code authority accepts them as an alternate to R602.7(1) (§2.6).

Against the bar: it clears 3, and it is the only candidate that does. It **does not clear 2 as
framed**: the WFCM is not "more conservative than the code" cell for cell — it is the engineered
basis the code accepts as an *alternative*, and its numbers are its own. Any conservatism would come
from how napkin picked the cell (the heaviest band within scope for whatever is unknown), which the
IRC's own table would supply just as well. And on 1 it is exactly the DCA 6 question, now with the
EULA of §1 read: AWC's document, AWC's terms. **Whether the WFCM may be read and transcribed under the
stance Marc took for DCA 6 (deck-guide-pack.md decision 6) is his call, and it should be made with §1
in front of him, not assumed from the DCA 6 precedent.**

### 2.6 North Carolina OSFM, *Supplements to Girder and Header Span Tables for #2 Southern Pine*

https://www.ncosfm.gov/residential/06027-supplements-girder-and-header-span-tables-no-2-southern-pine/open
(2026-09-27; SHA-256 `296ba4bc7ae4b670dca0f8060aa2aba1451bfc0cc0a84ba3ad1bc2faeda42baa`, 5 pp.,
"NC Department of Insurance, Office of the State Fire Marshal - Engineering Division", "Code: 2018
Residential Code, Date: July 2, 2020, Sections: Tables R602.7(1) and R602.7(2)"). The only
**non-AWC, non-ICC** header table found. It is a state code authority's supplemental table in exactly
R602.7(1)'s shape — supporting condition × header size × ground snow load (30, 50, 70 psf) × building
width (20, 28, 36 ft), spans and jack studs — and it says why it exists:

> "Although Tables R602.7(1) and R602.7(2) do not prohibit the use of No. 2 Southern Pine for headers
> and girders as long as they are appropriately sized, the spans shown are inadequate for the new
> Southern Pine design values, except for No. 1 grade (and higher grade) Southern Pine lumber. These
> tables can still be used for No. 2 Douglas Fir-Larch, Hem-Fir, and Spruce-Pine-Fir lumber headers
> and girders." (p. 1)

and its footnote b: "Spans are based on minimum design properties for No. 2 Grade lumber of southern
pine only. For other species, see Table R602.7(1) in the 2018 NCRC." It also records that NC accepts
the 2015 WFCM's header tables as "an acceptable alternate to the prescriptive framing members
addressed by the North Carolina Residential Code" (p. 1), linking an AWC view-only URL that now
returns 404.

Against the bar: clears 3, and plausibly 1 (a state document; its copyright status is not asserted
here). **Fails 2**: it is more conservative than the IRC *for Southern Pine No. 2*, by its own
account, and says nothing about the other three species the IRC table covers. napkin's header check
has no species input (the IRC table needs none — footnote b of R602.7(1) bases its spans on the
minimum properties of the four species), so a table valid for one species cannot be napkin's floor
for a wall whose lumber it does not know. Making it one would need either a species input on walls
or a comparison against the IRC table napkin cannot read. It is also written to the 2018 NCRC where
CT 2022 adopts the 2021 IRC.

### 2.7 Wisconsin Uniform Dwelling Code, SPS 321.25 Wood frame walls

https://docs.legis.wisconsin.gov/code/admin_code/sps/safety_and_buildings_and_environment/320_325/321.pdf
(2026-09-27; SHA-256 `7633cbd431fd0af6eb49eba942c988bca37682198a5dc14b968be7932609faf0`;
"Register November 2024 No. 827"; published by the Legislative Reference Bureau under s. 35.93,
Stats.). Wisconsin writes its own dwelling code, so this is public state law with no ICC text. Tables
321.25-B (headers supporting roof/ceiling), -C (one floor) and -D (one floor and roof/ceiling) are
keyed on house width 24–32 ft and Wisconsin's snow Zones 1/2, and their footnote says: "These tables
are based on wood with a fiber bending stress of 1,000 psi. For other species with different fiber
bending stresses, multiply the span by the square root of the ratio of the actual bending stress to
1,000 psi." **Fails 2**: a single-stress basis with a scaling formula is not a floor — the person must
know their lumber's bending stress to use it, which is the opposite of a safe default — and its widths
stop at 32 ft and its snow zones are Wisconsin's (values on a map, not in the text). It is another
jurisdiction's code, not a guide more conservative than Connecticut's.

### 2.8 HUD, *Residential Structural Design Guide*, Second Edition (October 2017)

https://www.huduser.gov/portal/publications/pdf/residential.pdf (2026-09-27; SHA-256
`9e8e5c62a965afd2c8f98fb4faacdb07ac7f9735435d43d10082cac82cf083ba`, 403 pp., "Prepared for U.S.
Department of Housing and Urban Development … Prepared by Coulbourne Consulting"). An engineering
design guide with worked examples (Example 5.5 designs one header by hand); it has no prescriptive
header table and itself says "using the header tables found in a typical residential building code
may be more cost effective", pointing to an NAHB Research Center handbook (1994) that is not free.
Several of its figures are "Reprinted with permission from … Copyright ASCE/ICC", so it is not
uniformly public domain either. **Fails 3.**

### 2.9 USDA Forest Service, *Wood-Frame House Construction*, Agriculture Handbook No. 73

Copy read: http://www.survivorlibrary.com/library/wood_frame_house_construction_1989.pdf
(2026-09-27; 241 pp.; title page "Agriculture Handbook No. 73, February 1955", L. O. Anderson and
O. C. Heyer, Forest Products Laboratory). A federal work. Its "Window and Door Headers" passage gives
header depth by opening width in stud spaces "in normal light frame construction" with no load, span
or species basis, and adds "For other than normal light frame construction, independent design may
be necessary." **Fails 2**: a rule of thumb with no stated basis cannot be shown conservative
relative to anything.

### 2.10 University extension publications

The University of Alaska Fairbanks Cooperative Extension's *Allowable Loads for Round Timber Poles*
(HCM-00752, reviewed March 2022; https://www.uaf.edu/ces/publications/database/housing/allowable-loads.php)
is beam span tables for round poles used as roof and floor beams, not wall headers. No other
extension header table surfaced in a search of `.edu` domains. **Fails 3.**

### 2.11 An engineered-lumber manufacturer's guide

Weyerhaeuser's *Specifier's Guide for Trus Joist Beams, Headers and Columns* (TJ-9000;
https://www.weyerhaeuser.com/woodproducts/document-library/document_library_detail/tj-9000/,
2026-09-27; file date July 2026; "© Weyerhaeuser Company. All Rights Reserved.") covers TimberStrand
LSL, Microllam LVL and Parallam PSL only — no sawn dimension lumber. As expected of the category: a
manufacturer's header table assumes that manufacturer's product. **Fails 2 and 3 for napkin's sawn
header, and its copyright is reserved.** (The PDF link the search returned, /application/files/…/TJ-9000.pdf,
was 404; the detail page was read.)

## 3. What the code's own structure says about the question

The task asked whether "more conservative than code" is even a coherent category for headers. From
the 2021 IRC's own footnotes to Table R602.7(1) (read in UpCodes' Connecticut viewer, 2026-09-27; the
same footnote e is already in the CT pack verbatim from Connecticut's document): footnote b bases the
spans on "minimum design properties for No. 2 grade lumber of Douglas fir-larch, hem-fir, Southern
pine, and spruce-pine-fir" — the weakest of the four; footnote e floors low snow at 30 psf; footnote f
reduces spans by 0.70 where the header's top is not braced. **The prescriptive table already is the
simplified, conservative path relative to engineering**, and the only source found that could be
called "more conservative" than it (§2.6) is so for one species because the code's own basis fell
behind that species' revised values — a correction, not a margin. There is no publisher whose business
is a header table safer than the IRC's; the way to be conservative with a prescriptive table is to
read its heaviest band within scope, and that needs the table, which is #14.

## 4. What would serve the want, each contingent on Marc

Marc's want — keep building right now with something visibly oversized, with zero code checking, and
check it against the real row later — is real. What honestly serves it:

1. **#14 and #158** remain the answer for headers and bracing. Nothing found here shortens them.
2. **An opt-in "heaviest band in scope" offer, once any header table is loaded.** Today a table that
   needs a site value the person has not entered returns *Input missing*. A small, general offer —
   "napkin can size this for the table's heaviest ground snow load and widest building width; the
   answer will be labelled as the table's worst case, not your site's" — is the over-engineering mode
   Marc described, built on real cited rows, generic across any table input, refusing (Out of scope)
   when even the heaviest band does not reach the span. It is not a new pack and needs no new source;
   it is a small design once #14 exists, for tables that band on a site value (R602.7(1)'s snow and
   width columns; the WFCM's 3.22 series). Not designed here: the task was a pack.
3. **If Marc, having read §1, accepts the WFCM under the DCA 6 stance**, the honest feature is a
   **WFCM header guide layer** through #238's machinery — `guides` entry, scope limits, a header
   table kind, the caveat clause on every line, answered on the wall's real inputs and labelled a
   guide's answer, exactly as decks are — not a "safe default pack". That is a separate design note,
   and it should not start before the licence question is settled in writing.
4. **Nothing built from a multiplier, a rule of thumb, or a table for one species** offered as a
   floor for all. Each was checked above and each would be a number napkin could not stand behind.

## 5. Not done, and why

- The WFCM 2018 Chapter 1 and Chapter 3 PDFs were downloaded, hashed and their metadata read; their
  text was not extracted (§1). The scope statement of the standard and the header tables' footnotes
  are therefore not quoted here.
- The 115 mph and 150 mph guides' Table 11 were not compared line by line with the 140 mph guide's;
  the 140 mph guide was read for the header question (front matter, scope, §4.2, Table 11 and
  searches of its text), not cover to cover.
- No issue was filed and no design note written: deliverable (b) of the task. The orchestrator
  should decide whether §1 becomes an issue against the DCA 6 stance (recommended) and whether this
  research note lands (it is `docs/research/`, like `diy-alternatives.md`, not a design).
