# The deck guide pack: DCA 6 under the Connecticut pack

Status: **Signed off by Marc 2026-09-27 with the recommended decisions (§8)**, including decision 6:
shipping transcribed DCA 6 tables, cited to AWC's document, is within the #157 stance (recorded in
[`DESIGN.md`](../../DESIGN.md) §2.1).

**Deviations found while building slice A (#238):** the per-line clause cites the `irc-governs`
caveat's *location*, not its text ("…; the IRC governs where they differ (p. 1)"), and `irc-governs`
is a reserved caveat id — a guide without it gets a clause that says only what it is based on; the
paragraph at the top prints every caveat verbatim. `member` in a guide-level limit is the lookup's own
member, so a footing (which has none) is Input missing under a guide-level member limit: member limits
belong on the table (§3.3's ledger minimum is). The scope gates the table lookups — joists, beam,
rafters, ledger, footing — only; guard and stair lines from a guide carry the clause and UNREVIEWED but
are not scope-gated, which is B5's call. UNREVIEWED is on every line that answers from pack data, the
base layer's as well as a guide's (passes, short, sized, out of scope, and guard/stair lines citing a
provision), never on Input missing or No data. Limit ids share one namespace across a guide's limits
and notes and a table's own limits (a golden case names one unambiguously). A table under a guide with a
species column *must* declare `speciesGroups`; a base-layer table *may*. The deck golden runner covers
`member-span` (all three uses), `deck-ledger` and `deck-footing`; `deck-post` (B3) and
`deck-guard-stair` (B5) extend it, and B3 adds factor coverage with `centerPostFactor`. Each table's
golden file must cover its guide's limits as well as its own, so each table proves it applies the scope.
The boundary generator is `GoldenRunner.DeckBoundaries` (no CLI): the runner fails a file whose committed
pairs are not its output and prints them. `Recompute.DiffDeck` compares results by what they say (code,
table, row, values), and a span that moved within its row (`SpanMoved`) is not announced. The deck
panel's Supports box stays free text — offering the scope's `supports` values (risk 9) waits for B1,
the first real guide. The synthetic guide's snow limit is 77 psf, not DCA 6's number; no DCA 6 value
and no change to the CT pack are in this slice.

**As built in slice B1 (#41):** Table 2's designation is `"2"` and a guide's table is cited with the
guide's name ("DCA 6-2015 Table 2 row r.sp.2x8.16, p. 4 …"), so the evaluator's own sentences read
"Table 2 has no row …". The Cantilever line appears only when the deck has a cantilever; its result is
a Passes/Short whose `Allowed` is min(L_O, ⌊L·¼⌋ in 1/1024″), exact for the verdict, and the line
says both bounds. A deck golden case asks the cantilever by carrying `cantilever`; each row's overhang
needs a hand case where L_O governs — three printed rows have L_O above L_J/4 (Southern Pine 2x12 at
16", the Douglas Fir-Larch group's 2x12 at 24", the Redwood group's 2x10 at 24"), so their cases ask
on a span of 4 × L_O. All
seven notes are `not-encoded` with `appliesTo` where the superscript sits. The CT pack's `review`
stays `unreviewed` (not §5's `in-review`), per the task. Bumping it to revision 2 relocked the two
samples locked to revision 1 (they would otherwise resolve to no pack). The Supports offer (risk 9)
is a line under the deck block listing the pack's supports words and the guide's species, never
filled in; a `notIn` supports limit adds that anything else is beyond the guide's scope. The shipped
packs' golden files live in `packs/golden/` and run with every test run.

**As built in slice B2 (#41):** Table 3A's designation is `"3A"`, its location `"p. 6"`; 168 rows as
§3.6 counted, ids `r.sp.2-2x10.10` and `r.dfl-rw.3x8.10` / `r.dfl-rw.2-2x8.10` (group, printed size,
joist-span column in feet), members in napkin's form, the `joistSpan` column upper-bound with max 18'
and, since the page prints no lower bound, napkin's smallest length as its min. Notes 1 and 4 sit on the
title's L_B and the Size heading, so apply to the table; notes 2 and 3 sit on species in the second
group's heading and ride on its 112 rows (B1's precedent for a note on some of a group's species). p. 5's
two beam sentences — "can extend past the post face up to LB/4 as shown in Figure 3" and "Joists shall
not frame in from opposite sides of the same beam" — are guide scope notes `n.beam-span` and
`n.beam-sides`, so the paragraph at the top of the block shows them. The golden's per-row expectations
were written from the PDF's text layer while the pack's rows were typed from the 300-dpi render, so the
run cross-checks the two readings. `DeckFraming.BeamSpan` is (W − post width) ÷ (posts − 1), and the
tributary area's beam factor follows it (its joist factor is B3's); the beam line says "post centre to
post centre", the frame line "between post centres". The Table 3A lookup's joist-span input is
unchanged: its column is p. 3's L, face of support to face of support. The CT pack goes to revision 3,
`review` still `unreviewed`; the two locked samples follow.
**A finding for Marc and the reviewer, not resolved here:** read at 600 dpi, Figure 3 (p. 7) draws the
arrowheads of its "beam span (L_B)" and "L_B/4 max. overhang" dimensions to the **faces** of the posts,
not to a centreline, and p. 5 says the beam "can extend past the post face up to LB/4". The main body
therefore reads as face-to-face; only Appendix B (p. B2, Figure B3), written for the tributary area of
posts and footings, measures "from either centerline of post to centerline of post". §3.2 above read
Figure 3 as between post lines. Decision 8 is implemented as signed off (centre to centre, the longer and
so the conservative measure: a beam that passes face to face by less than one post width is now short);
the checklist asks the reviewer to confirm the figure, and whether Table 3A's L_B is face to face is
Marc's call to revisit.

**Decision 8 corrected (#41, Marc, 2026-09-27; slice B2-fix).** The independent review of Table 3A (C2,
[`dca6-table3a.md`](../code-packs/reviews/us-ct-2022/dca6-table3a.md), "Figure 3") confirmed the finding
above: Figure 3 (p. 7) draws each post as two face lines, with L_B's arrow tips on adjacent posts' inner
faces and the L_B/4 overhang's on the deck edge and the end post's outer face; p. 5 says the beam "can
extend past the post face up to LB/4". Appendix B's centreline measure is a different quantity, B_L,
defined "for posts or footings being considered" (p. B2). Marc decided: follow the source. Decision 8's
principle — measure as the source defines it, and say so — stands; its reading of Figure 3 and §3.2's
"centre-to-centre" were wrong. As built: `DeckFraming.BeamSpan` is L_B between post faces, (W − posts ×
post width) ÷ (posts − 1), and the beam line says "between post faces" (the worked example's 5'-6 3/4",
not 5'-10 1/4"); napkin's beam ends flush with its end posts' outer faces, so it has no overhang to check.
The tributary area (`DeckFraming.Tributary`) now follows Appendix B on both sides, since p. B1 defines the
joist side as plainly as p. B2 the beam side and neither needs B3's schema: B_L "from either centerline of
post to centerline of post, if there are overhangs, or to the outside edges of the deck, if there are no
overhangs", the greater of two unequal adjacent spans (p. B2) — with napkin's overhang-free beam, a middle
post's centreline to the deck's edge, or the whole width with two posts; J_L "from the ledger face to
either the center point of the beam, if there is an overhang, or to the outside face of the rimboard if
there is not an overhang", and J_O from the deck's outside edge to the beam's centreline, zero without an
overhang (p. B1). The equations are Eq. B-1, A = (½J_L + J_O)(B_L), for a centre post and Eq. B-2,
A = (½J_L + J_O)(½B_L + B_O), for a corner post (p. B1) — the ½ is on J_L alone, not on (J_L + J_O) as
§3.4 below wrote it. napkin's middle post (three or more posts) takes B-1 and its end post (two) B-2 with
B_O = 0; the footing line names the equation and both measures. The worked example's middle post is
72" × (118 1/2" ÷ 2) = 4266 sq in = 29.6 sq ft (was 28.5). Left for B3: Table B3's note 2 factor (×1.25
at centre posts under a continuous beam), the post-height tables' corner/centre position (which
`DeckTributary.Post` already says), and a beam overhang, should napkin ever model one (B_O and B_L to post
centrelines, and the L_B/4 check).

**As built in slice B3 (#42):** Tables B1 and B2 are **two** `deck-post` tables, keyed by a table-level
`position` (`corner`, `center`) as `member-span` is keyed by `use` — not one table with a position input column
as §3.4 drew it: one table cannot carry two designations, two pages and B2's own note 4, and every result cites
its table, row and page. Designations `"B1"`, `"B2"`, `"B3"`, locations `"p. B3"`, `"p. B4"`, `"p. B5"`; row ids
`r.sp.4x4.20`, `r.pp-rp-spf.6x6.170` (group, post, area band) and `r.40.1500` (area band, psf column). 250, 250 and
100 rows, typed from the pages rendered at 500 dpi and cross-checked cell by cell against the text layer (all 800
agree); the golden files' per-row cases were written from the text layer, so each run cross-checks the two
readings. B1 prints NP in 13 cells and B2 in 11 (B2's 4x4 Redwood at 250 sq ft prints 1): `notPermitted: true`,
answered Out of scope citing the row and quoting "NP" as printed — DCA 6 never expands it (pp. B3–B4, Table 4,
Table C4A), so napkin does not. The five species headings are the groups as printed; "SPF" places the guide's
Spruce-Pine-Fir (note 2, whose superscript sits on SPF, spells it "spruce-pine-fir"). Notes 1 and 3 sit on the
title, so apply to every row; note 2 sits on Douglas Fir-Larch, Hem-Fir and SPF, so rides on those three groups'
rows (B1's precedent); B3's note 1 applies to every row. **The ×1.25 is an operation:** B2 note 4 and B3 note 2 are
each table's `centerPostFactor` — `{ "note", "text" (verbatim), "multiply": "5/4", "location" }`, not also a
footnote (bracing's factors are declared the same way), refused on a corner table or any other kind — and the
evaluator, not `DeckFraming`, multiplies a centre post's area under a continuous beam before the lookup; the
result carries the area asked, the factor and the area looked up. napkin's beam is one piece the deck's width
long, so `DeckCheck` always asks with a continuous beam: the larger area, the conservative reading. **Post
position:** `DeckFraming` now carries `EndPost` (a corner post, Eq. B-2) and, with three or more posts,
`MiddlePost` (a centre post, Eq. B-1), and `PostLength`, grade to the beam's underside as p. 10 measures post
height. Without a beam overhang the end post's B_L is the same end span, the next post's centreline to the deck's
outside edge (the whole width with two posts), so an end post carries half the middle post's area; B1's 4x4
heights are so much lower than B2's that either may govern, so both are checked, on two lines (**End posts**,
**Middle posts** — two `DeckCheckKind`s, since a recompute keys a line by kind). The footing line still sizes
every footing for the most loaded post, the middle one with the factor. The footing sentence reads "14" round or
13" square, 6" thick, for a middle post's 29.6 sq ft (… Eq. B-1 …) × 1.25, a centre post under a continuous beam
(DCA 6-2015 Table B3 note 2, p. B5 …) = 37.0 sq ft, on 2000 psf (…)", the note verbatim after the table's notes.
The soil bearing value is the site's (`SiteValues.SoilBearingPsf`, typed in Project → Adopted code and site),
never defaulted; its Input missing now says where to type it and that it comes from the building department or
a soils report. B3's `soilBearing` column is `lower-bound` from 1500 psf (below it, Out of scope) with max the
last printed column, 3000 psf (a stronger soil reads that column); both area columns' domain min, 1 sq ft, is
napkin's (the pages print none). The worked example under CT: end posts 14.8 sq ft → B1 `r.sp.4x4.20`, 6'-0";
middle post 37.0 sq ft → B2 `r.sp.4x4.40`, 13'-0"; posts 1'-6 1/2" pass; footing on 2000 psf → B3 `r.40.2000`.
Risk 4 stands as designed: a 4x4 answers from B1/B2 with their note 3, and p. 10's "All deck post sizes shall be
6x6 (nominal) or larger" is not encoded in this slice — whether the guide's scope should say it is Marc's call.
*(Corrected the same day: p. 10 is now enforced — see "Corrected after slice B3" below.)*
The golden runner gains post cases (`post`, `height`, `tributaryArea`, `continuousBeam`) and footing cases
(`position`, `continuousBeam`), the expectations `sized { round, square, thickness }` and `outOfScope {
notPermitted: true }`, a coverage rule that a table's factor is exercised by a hand case, and boundary pairs
that ask an area under the factor at the band ÷ the factor. The synthetic pack gains a corner and a centre post
table (its factor 3/2, not DCA 6's) and its footing table's three outputs. The CT pack goes to revision 4,
`review` still `unreviewed`; the two locked samples follow. Checklists `dca6-tableB1.md`, `dca6-tableB2.md` and
`dca6-tableB3.md` are unfilled for the independent review (C3).

**Corrected after slice B3 (#42, 2026-09-27): p. 10's 6x6 minimum is enforced.** Found right after B3 landed at
0.216.0-beta, and a correctness fix, not a scope choice: read again at 300 dpi, p. 10 (POST REQUIREMENTS) opens
"All deck post sizes shall be 6x6 (nominal) or larger, and the maximum height shall be in accordance with Table 4
and measured from grade or top of foundation, whichever is highest, to the underside of the beam" — every deck post,
not one position — and item 3, p. 2 says "Minimum post size is 6x6 nominal and maximum post height shall be in
accordance with Table 4." Appendix B does not lift it: it is "an alternative to the assumptions of Table 4 Post
Height for 6x6 and Footings Sizes for all Posts" (p. B1), and "As an alternative to Table 4 of DCA 6, the post height
and footing size may be in accordance with Table B1 through B3" (p. B2) — heights and footings, not the minimum
size. B1/B2's 4x4 columns serve the commentary's alternative (C2 item 3, "In some instances, this commentary provides
a 4x4 nominal post alternative"; C7, other post sizes under alternative methods a building official approves), which
napkin does not model. So §3.4's "tension to transcribe as printed" and risk 4 gave a wrong answer: a 4x4 post
answered Passes from its column. **As built:** Tables B1 and B2 each carry a table limit `t.post-size` — `"when": {
"input": "member", "notIn": ["6x6", "6x8", "8x8"] }`, its text p. 10's opening clause verbatim, "All deck post sizes
shall be 6x6 (nominal) or larger", its location "POST REQUIREMENTS, p. 10" — tried after the guide's limits and before
the lookup, so a post under 6x6 nominal in either dimension is **Out of scope** citing p. 10 and never reaches its
column; the 4x4 columns stay transcribed as printed. The list is every materials-library size 6x6 nominal or larger
in both dimensions (a deck post's name can come from nowhere else: the frame refuses any other), so 4x4, 4x6, 2x4 and
5/4x6 are refused, and a 6x8 or 8x8 passes the limit to find no column in Appendix B (p. 10's "8x8 nominal posts can
be substituted anywhere in Table 4 but are limited to a maximum height of 14'-0"" is Table 4's, not encoded). It is
a **table** limit, not a guide scope limit as the fix was first framed: `member` in a guide-level limit is every
lookup's own member, so every footing (no member) would be Input missing, and each of the guide's other tables'
goldens would have to cover a limit that cannot apply to it (slice A's deviations, above). The guide gains item 3 as
the scope note `n.post-size`, so the paragraph at the top of the block shows it beside items 1, 2, 4, 8 and 9. No
engine change. The footing check is independent of it: Table B3 bands on area and soil, and every footing is still
sized for the most loaded post. **The worked example under CT (4x4 posts, Southern Pine):** "End posts 4x4, 1'-6 1/2"
from grade to the beam's underside, Southern Pine, each carrying 14.8 sq ft (DCA 6 Appendix B Eq. B-2, …): Beyond
table B1: "All deck post sizes shall be 6x6 (nominal) or larger" (DCA 6-2015 POST REQUIREMENTS, p. 10). Get it
engineered. UNREVIEWED: …", and the **Middle post** line the same citing B2, with its 29.6 sq ft (Eq. B-1) but no
× 1.25, since no table was read; the footing line is word for word what it was, 14" round or 13" square, 6" thick for
37.0 sq ft (B3 `r.40.2000`). As 6x6s the posts read B1 `r.sp.6x6.20` and B2 `r.sp.6x6.40` (at 37.0 sq ft), 14'-0"
each, and pass. The goldens' 125 4x4 row cases per table now expect `t.post-size` — they still name their rows and
record the printed cell, but the run no longer cross-checks the 4x4 cells (the checklists do) and generates no
boundary pair for them; the worked example is asked both as a 4x4 and as a 6x6, the inputs-asked and column cases ask
a 6x6, and a 4x6 case is added. The CT pack goes to revision 5 and the two samples relock; checklists B1 and B2 gain
the limit and the reading above for the reviewer to confirm, and all three the new revision and scope note. napkin's
deck tool still starts a deck on 4x4 posts, so a new deck under CT shows both post lines out of scope until they are
typed as 6x6: whether the starting value should change is Marc's call.

Design note written by Fable per [`PLAN.md`](../../PLAN.md) for Marc's decision of 2026-09-26 on
#209, **"Decks only via DCA 6"**: the deck checks' real data comes from the American Wood
Council's *Prescriptive Residential Wood Deck Construction Guide* (DCA 6), a free primary source,
with two caveats beside every answer — it is based on the **2015** IRC while Connecticut 2022
adopts the **2021** IRC, and it is a **guide, not the adopted code**, which itself says the IRC
governs where they differ. Headers (#14) and bracing (#158) stay blocked. This note decides where
a guide sits in a pack model keyed by adopted code, maps each DCA 6 table onto the deck kinds
#198–#200 built, names the schema gaps the real text exposes, sets the review process, and cuts
the work into landable slices for #40–#43.

Settled and not re-argued: packs are keyed by adopted code ([`rules-engine-model.md`](./rules-engine-model.md)
§1.1); every result cites edition, table, row and page (§2); the four results are sized/passes,
out of scope, input missing and no data — never a guess (§3); lengths are exact (§1.5); no
value is written from memory (CLAUDE.md); the deck kinds are as [`deck-and-porch.md`](./deck-and-porch.md)
§3 and `docs/rules-engine.md` describe them.

**What was read for this note.** The whole main body of DCA 6 (pp. 1–24), Appendix A (p. A1) and
Appendix B (pp. B1–B5) in text, and the figure pages 4, 7, 8, 19, 20 and 21 as rendered images,
because the guard and stair numbers live in figures. Appendix C (commentary, pp. C1–C14) was
skimmed for headings only; nothing is taken from it. This note quotes DCA 6 sparingly and shows at
most **one sample row per table**, cited, to fix the shape; the pack is the transcription, not
this note.

---

## How to use it (what a homeowner sees)

You draw a deck against your house on the Connecticut 2022 pack, pick your town, type your joists
(2x8 at 16"), your species (Southern Pine), your posts and your footing depth. The deck's **Code
check** block opens with one line that says where the deck answers come from:

> Deck checks under CT 2022 use **AWC DCA 6-2015**, a guide based on the 2015 IRC. It is not
> Connecticut's adopted code (CT 2022 adopts the 2021 IRC), and where the guide and the IRC differ
> the IRC governs. DCA 6 covers single-level decks attached to the house, deck length no more than
> deck width, snow loads up to 40 psf, and no hot tubs (p. 2).

Then every check answers with a real number and its page, and the same caveat in short:

> Joists 2x8 at 16" o.c., Southern Pine, span 9'-9": allowed up to 11'-10" (DCA 6-2015 Table 2,
> p. 4 — a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they
> differ) — UNREVIEWED until the row-by-row review is signed off.

Type a species or a fastener the guide has no row for, put a roof-bearing porch wall on the deck,
or enter a ground snow load over 40 psf, and the line says **Out of scope**, cites the page that
stops it, and says "get it engineered". Nothing is interpolated, defaulted or guessed. Switch the
project to a pack without the guide and every deck line goes back to **No data**, and the
recompute says which lines changed.

---

## 0. The document, as read

| | |
|---|---|
| Title (cover) | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code*; designation **DCA 6** (AWC's Design for Code Acceptance series; the appendices head themselves "DCA 6", the commentary calls the edition "DCA 6-15") |
| Publisher | American Wood Council |
| Edition and printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" on the cover; "Copyright © 2007, 2009, 2010, 2014, 2015, 2018" and the printing mark "04-18" on p. 24; PDF metadata title "DCA6 Prescriptive Residential Wood Deck Construction Guide Based on 2015 IRC", created 2018-04-17, modified 2019-01-21; 44 pages (24 numbered + A1 + B1–B5 + C1–C14) |
| URL as retrieved | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf — AWC's resource page https://awc.org/resources/dca-6-english-2015-prescriptive-residential-wood-deck-construction-guide/ redirects (301) to it, and AWC's collection page lists only this 2015 edition (English and Spanish), so it is the current edition on AWC's site |
| Retrieved | 2026-09-27 |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` (both URLs serve the identical file) |
| PDF flags | encrypted with `copy: no` set in its permissions (a viewer flag, not a licence term); text extracts with pdftotext; the figures need the rendered page |

The cover paragraph states both caveats in the document's own words: "Where differences exist
between provisions of this document and the IRC, provisions of the IRC shall apply" and "no
assurance can be given that designs and construction made in accordance with this document meet
the requirements of any particular jurisdiction" (p. 1). Bracketed IRC references throughout are
to the **2015** IRC (References, p. 24).

**Scope the document states** (Minimum Requirements & Limitations, pp. 2–3), the ones napkin's
deck meets or must check: item 1, single-level residential wood decks attached to the house to
resist lateral forces; item 2, overall deck length ≤ overall deck width (length runs with the
joists, width along the house — Figure 5, p. 8); item 3, posts 6x6 minimum, height per Table 4;
item 8, decks supporting large concentrated loads such as hot tubs are out; item 9, snow loads,
drift or sliding snow over 40 psf are out. Every span table adds "Assumes 40 psf live load, 10 psf
dead load, No. 2 grade, and wet service conditions" (Table 2 note 1, p. 4; Table 3A note 1, p. 6).

**What it contains that the deck kinds consume:** Table 2 joist spans and overhangs (p. 4);
Table 3A dimension-lumber beam spans (p. 6); Table 3B glulam beam spans (p. 7); Table 4 post
heights and footing sizes at 1,500 psf (p. 12); Table 5 ledger fastener spacing (p. 15); guard
requirements (p. 19, Figure 24); stair, handrail and stair-footing requirements (pp. 20–22,
Figures 27–34, Table 6); and Appendix B's Tables B1–B3, post heights and footing sizes by
tributary area and soil capacity (pp. B3–B5), offered "As an alternative to Table 4" (p. B2).
A quirk the citations must survive: **two tables are designated "3A"** — beam spans on p. 6 and
joist-hanger capacity on p. 9 — so every DCA 6 citation carries its page.

---

## 1. Where DCA 6 sits: a guide layer the pack declares

### 1.1 The constraint

Three facts decide the shape. Pack identity is the adoption (`us-ct-2022`), never a document
(§1.1). The loader refuses a base layer whose `baseCode` differs from the pack's, so a layer on
the 2015 IRC cannot be `layers[0]` of a 2021-IRC pack, and every later entry in `layers` must be
an `amendments` overlay. And a DCA 6 value must never print as "IRC 2021 … as adopted by CT
2022", which is what a row in `layers/irc-2021/deck/` would say.

### 1.2 Recommended: a guide layer, referenced by the pack, cited as itself

A **guide layer** is a third kind of layer, beside base layers and overlays: a directory
`packs/layers/dca6-2015/` whose manifest says what document it is and what it is not, and whose
`deck/` directory holds the same file kinds a base layer's `deck/` may (`member-span`,
`deck-ledger`, `deck-footing`, `deck-guard-stair`, and the new `deck-post` of §3.4), read by the
same `DeckReader`. A pack lists it in a new field, not in `layers`:

```json
// packs/layers/dca6-2015/layer.json (identity fields as read; the rest is this note's schema)
{ "schemaVersion": 1, "kind": "guide", "id": "dca6-2015",
  "guide": { "shortName": "DCA 6-2015",
             "title": "Prescriptive Residential Wood Deck Construction Guide, Based on the 2015 International Residential Code",
             "publisher": "American Wood Council",
             "basis": { "publisher": "ICC", "code": "IRC", "year": 2015 } },
  "caveats": [
    { "id": "basis", "text": "Based on the 2015 International Residential Code", "location": "cover, p. 1" },
    { "id": "irc-governs", "text": "Where differences exist between provisions of this document and the IRC, provisions of the IRC shall apply.", "location": "p. 1" } ],
  "scope": { "limits": [ … §2 … ], "notes": [ … §2 … ] },
  "species": [ "Southern Pine", "Douglas Fir-Larch", "Hem-Fir", "Spruce-Pine-Fir", "Redwood", "Western Cedars", "Ponderosa Pine", "Red Pine" ],
  "sources": [ { "id": "awc-dca6-2015", "title": "…", "publisher": "American Wood Council", "url": "…", "printing": "2015 IRC edition, © 2018, printing 04-18", "retrievedOn": "2026-09-27", "sha256": "205d57b5…3009e" } ] }
```

```json
// packs/packs/us-ct-2022/pack.json — the addition
"guides": [ { "id": "dca6-2015",
              "notes": "Deck tables only. A guide, not Connecticut's adopted code: CT 2022 adopts the 2021 IRC; DCA 6 is based on the 2015 IRC and says the IRC governs where they differ. Remove this entry when the IRC 2021 R507 tables are transcribed into layers/irc-2021/deck." } ]
```

- **Identity stays honest.** The picker still lists one thing, the adopted code; its status text
  gains a clause: "CT 2022 — IRC 2021, in force Oct 1, 2022 (pack us-ct-2022 rev 2): base tables
  not loaded; deck tables from AWC DCA 6-2015, a guide (UNREVIEWED)". `LoadedPack.StatusLabel`
  grows the clause from the manifest; nothing else in the picker changes.
- **Every deck citation names the guide, not the code.** `CitationLayer` gains `Guide`; a deck
  table's `SourceRef` is the guide's source; `DeckCheck.Cited` prints
  "DCA 6-2015 Table 2 row r.sp.2x8.16, p. 4" and appends the short caveat clause, composed from
  manifest fields (the guide's `shortName` and `basis`, the pack's `shortName` and `baseCode`, the
  `irc-governs` caveat's text) — napkin's own sentence about its data, not a code's provision, so
  the engine still builds in no code's text. The full two-caveat paragraph is shown once at the top
  of the deck's Code check block, with the scope notes (§2). The `UNREVIEWED` marker, which today
  reaches header results through `AdoptedCodeRef` but not deck lines, is appended to every deck
  line until the pack is signed off (rules-engine-model §13 Decision 5, unchanged).
- **Precedence: none.** If the base layer's `deck/` and a guide's `deck/` both declare the same
  kind (and, for `member-span`, the same `use`), the pack is **invalid** at load, naming both
  files — the pack author removes the guide entry when the real table arrives. A guide never
  silently wins or loses; that is the point of `guides` being a field the author writes.
- **Recompute on switch: unchanged.** The deck check reads `pack.Deck`; a pack without a guide
  gives No data as it does today, and `Recompute`'s deck diffs (built in slice A, §6) say which
  lines went from a number to No data and back. The project stores the pack id and revision as it
  does now; a guide has no identity of its own in the project file.
- **Load checks:** `guides[i].id` resolves to `layers/<id>/layer.json` with `kind: "guide"`;
  `guide.basis` is a base code; `caveats` is non-empty, each with `text` and `location`;
  `species` is non-empty and unique; every deck file's `source` is one of the guide's sources; a
  guide id listed twice, a guide with no `deck/` files, or a `kind: "guide"` layer named in
  `layers` is a problem. A base layer's `layer.json` may not carry `guide`, `caveats` or
  `scope`.

### 1.3 Rejected

- **A separate opt-in pack `us-awc-dca6`.** Its identity would be a document, not an adoption;
  the picker would offer "a code" that no building department applies. Rejected on §1.1.
- **DCA 6 files inside `layers/irc-2021/deck/`.** The layer's manifest says IRC 2021 and every
  citation would print "as adopted by CT 2022" for 2015-basis guide values. Dishonest; rejected.
- **A per-project toggle "use the deck guide".** With no other data, off means No data for every
  deck line; nobody wants that switch, and it puts a data-provenance decision on the person
  instead of on the pack author. The caveat on every line is the disclosure. Rejected; a
  Decision for Marc (§8.1) because it is a preference.

---

## 2. Scope limits, first-class and checked before any lookup

**The blind spot.** `DeckEvaluator.Lookup` iterates only the columns a table declares. Table 2 as
printed has species, size and spacing and no "supports" column; transcribed as printed, a deck
carrying a porch's roof-bearing wall would pass the joist check as if the porch were not there —
the silent wrong answer deck-and-porch §13.4 forbids. So a guide's scope is data checked
**before** every deck lookup under that guide, shaped like the bracing file's `limits` (each
cited; the first that holds makes the check **Out of scope**, citing it; an input a limit needs
and the project has not entered is **Input missing**, naming it):

```json
"scope": {
  "limits": [
    { "id": "s.loads", "when": { "input": "supports", "notIn": ["deck"] },
      "text": "Assumes 40 psf live load, 10 psf dead load … Decks supporting large concentrated loads such as hot tubs are beyond the scope of this document.",
      "location": "Table 2 note 1, p. 4; Table 3A note 1, p. 6; item 8, p. 2" },
    { "id": "s.snow", "when": { "input": "groundSnowLoad", "above": 40 },
      "text": "This document does not apply to decks which will experience snow loads, snow drift loads, or sliding snow loads that exceed 40 psf.", "location": "item 9, p. 2" },
    { "id": "s.shape", "when": { "input": "deckLength", "aboveInput": "deckWidth" },
      "text": "Overall deck length shall be equal to or less than overall deck width.", "location": "item 2, p. 2; Figure 5, p. 8" } ],
  "notes": [
    { "id": "n.single", "text": "This document applies to single level residential wood decks that are attached to the house to resist lateral forces.", "location": "item 1, p. 2" },
    { "id": "n.materials", "text": "All lumber … shall be a naturally durable species … or be preservatively treated …", "location": "item 4, p. 2; Table 1, p. 3" } ] }
```

- **Inputs a limit may name** (napkin supplies them): `supports`, `species` and `member` (enum),
  `groundSnowLoad` (psf, the site value), `deckLength` and `deckWidth` (lengths: the deck's depth
  out from the house and its width along the ledger, Figure 5's definitions). **Comparison forms:**
  `above` (number or length), `equals`, `in`/`notIn` (enum), `aboveInput` (a length against
  another length). A table may also carry its own `limits` (the ledger's 2x8 minimum, §3.3) in the
  same shape with `member` as the input.
- **The `supports` vocabulary.** The guide covers one value, `deck` (the deck's own weight and
  people). `s.loads` both declares it and stops everything else; the deck panel's Supports offers
  the values the scope's `in`/`notIn` lists name. A guide whose scope has no `supports` limit is
  **invalid at load** — the porch case must always have an answer.
- **The snow limit compares the site's ground snow load** (Appendix AY's per-town value under CT
  2022) to the guide's 40 psf as-is: napkin derives no deck snow load from it, so a town whose
  ground snow load is over 40 psf gets Out of scope, which is conservative and honest. A Decision
  for Marc (§8.3).
- **Single-level and attached** are napkin's deck by construction (deck-and-porch §13.2: a deck is
  drawn against an existing wall; one box, one level), so item 1 is a note shown once, not a
  limit. Hot tubs are not modelled; item 8 rides in `s.loads`'s text.
- **Load checks:** an unknown input or form, `above` on an enum, `in` on a length, an `aboveInput`
  naming a non-length, a limit without `text` and `location`, duplicate ids.

**The porch's roof-bearing case (deck-and-porch decision 4), confirmed from the text.** DCA 6 has
no row, table or footnote for a deck carrying a roof or a bearing wall: every span table assumes
40 psf live and 10 psf dead (Table 2 note 1, Table 3A note 1), the scope excludes large
concentrated loads (item 8), and nothing on pp. 2–24 mentions a roof on the deck. A deck whose
Supports is anything but `deck` is **Out of scope**, citing `s.loads`: "get it engineered". That
is the right answer, and it is cited.

---

## 3. Mapping: each DCA 6 table onto the deck kinds

Row ids follow the table's own axes (`r.sp.2x8.16`, `r.ls.lumber.12`); every row's `location` is
its page and printed row and column; every table note is transcribed verbatim and `not-encoded`
unless this section says it is a limit or a declared factor. The one sample row under each
heading is there to fix the shape and is cited; it is not the transcription.

### 3.1 Table 2, joist spans and overhangs (p. 4) → `member-span`, `use: deck-joist` (#41)

Inputs as printed: species group (three: Southern Pine; Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir;
Redwood, Western Cedars, Ponderosa Pine, Red Pine), size (2x6, 2x8, 2x10, 2x12), spacing (12", 16",
24") → **allowable span L_J** and **allowable overhang L_O**. Sample, as printed: Southern Pine,
2x8, 16" o.c.: span 11'-10", overhang 2'-0" (Table 2, p. 4). The joist span L "is measured from
the face of support at one end of the joist to the face of support at the other end" and excludes
the overhang (p. 3, Figure 1A, p. 4) — exactly `DeckFraming.JoistSpan` (ledger face to beam
face, cantilever excluded).

**Schema gaps.**
- **A second output, `overhang`,** on `deck-joist` rows, and the rule that goes with it: the
  overhang is "the lesser of allowable overhang, L_O, or one fourth the joist span, L/4" (p. 3;
  Table 2 note 3). The fraction is the table's, declared once — `"overhangLimit": { "fraction":
  "1/4", "of": "span", "location": "p. 3; Table 2 note 3" }` — and napkin computes
  min(L_O, L × 1/4) exactly and checks the deck's typed **Cantilever** against it, on a new
  **Cantilever** line in the deck's checks: "Cantilever 1'-6": allowed up to the lesser of 2'-0"
  (row) and L/4 = 2'-5" (…)". A table with `overhang` outputs must declare `overhangLimit`;
  one without has no cantilever check and says so (a typed cantilever under such a table is Out
  of scope: "the table does not cover an overhang").
- **Species groups** (§3.6): the typed species is one of the guide's eight; the table maps each
  to its printed group.
- Note 6, "Ledger shall be a minimum of 2x8 nominal. Joists and rim joists to which guard posts
  are attached shall be a minimum of 2x8 nominal", is encoded where it bites: its ledger half as
  the ledger table's limit (§3.3), its guard-post half as a note on the guard lines. Note 7's
  18'-0" cap is the last band of the 2x12 rows and needs nothing. Notes 1, 2 (L/360), 4 and 5
  (incising, northern species) ride `not-encoded`.

### 3.2 Table 3A, dimension-lumber beam spans (p. 6) → `member-span`, `use: deck-beam` (#41)

Inputs as printed: species group (two: Southern Pine; everything else in one group), size
(Southern Pine: 2-2x6 … 3-2x12; the other group: "3x6 or 2-2x6" … "3x12 or 2-2x12", 4x6 … 4x12,
3-2x6 … 3-2x12), joist span L ≤ 6', 8', 10', 12', 14', 16', 18' (`upper-bound`) → beam span L_B.
Sample, as printed: Southern Pine, 2-2x8, joist span ≤ 10': 6'-6" (Table 3A, p. 6).

- **Member names.** The pack writes napkin's form, which `DeckCheck` already builds —
  `"(2) 2x8"`, `"(1) 4x8"`, `"(3) 2x10"` — with the printed name in the row's `location`
  ("row 2-2x8"). A two-name cell ("3x8 or 2-2x8") becomes **two rows** (`(1) 3x8` and `(2) 2x8`)
  citing the same cell; the reviewer sees the cell twice and that is fine.
- **How L_B is measured.** Figure 3 (p. 7) dimensions "beam span (L_B)" between the post lines,
  with an "L_B/4 max. overhang" beyond the end posts, and Appendix B states "The beam span is
  measured from either centerline of post to centerline of post" (p. B2). `DeckFraming.BeamSpan`
  today is the **clear** length between posts, one post width shorter than the figure's span. The
  request must ask the table the number the table is about: slice B2 changes the derived beam span
  to **centre-to-centre**, (W − post width) ÷ (posts − 1), says so in the sentence, and the
  synthetic goldens follow. napkin's beam has no overhang past its end posts, so L_B/4 needs no
  check; a note says the table allows one. *(Corrected, #41, 2026-09-27: Figure 3 dimensions L_B face to
  face of posts, and napkin now measures it so; see "Decision 8 corrected" at the top.)*
- Note 4 (beam depth ≥ joist depth with hangers) rides `not-encoded`; "Joists shall not frame in
  from opposite sides of the same beam" (p. 5) is a scope note. Note 1 (40/10 psf, L/360 simple
  span, cantilever length/180, No. 2, wet service) rides with every row.

**Table 3B, glued laminated beams (p. 7): not transcribed.** napkin's beam is plies of dimension
lumber; a glulam stress class is not an input the deck has. Said here so nobody looks for it.

### 3.3 Table 5, ledger fastener spacing (p. 15) → `deck-ledger` (#40)

Inputs as printed: connection detail (½" lag screw with 15/32" maximum sheathing; ½" bolt with
15/32" maximum sheathing; ½" bolt with 15/32" maximum sheathing and ½" stacked washers), the
house's rim or band joist (1" LVL, 1-1/8" LVL, 1-½" lumber — the stacked-washer row has lumber
only), joist span (6'-0" and less, 6'-1" to 8'-0", … 16'-1" to 18'-0": `upper-bound` bands at
6, 8, 10, 12, 14, 16, 18 ft) → on-centre spacing. Sample, as printed: ½" lag screw, 1-½" lumber
band joist, joist span 10'-1" to 12'-0": 15" o.c. (Table 5, p. 15). 49 printed cells.

**Schema gaps — the largest of the note.**
- The kind was shaped on `member` × `joistSpan` (deck-and-porch §3.2); DCA 6 bands on **fastener
  type** and **band-joist material** instead, neither of which the deck has. The person must know
  both — DCA 6 itself says that if the band joist "cannot be verified … then either a non-ledger
  deck or full plan submission is required" (p. 13). So the deck gains **two typed inputs**,
  `LedgerFastener` and `HouseBandJoist`, strings as the pack names them, **null until entered**
  (Input missing names them and where to type them), in the deck panel beside Species; scene
  format bumps to the next free number (17 as `main` stands) with no migration, per policy. The
  `deck-ledger` kind's allowed inputs gain `fastener` and `bandJoist` (enum, exact); `member`
  becomes optional for the kind.
- The **2x8 ledger minimum** is a table limit on `member` (Table 5 note 5, Table 2 note 6): a deck
  framed in 2x6 gets "Ledger 2x6: below the 2x8 minimum (DCA 6-2015 Table 5 note 5, p. 15)".
- napkin's fastener count, ⌈length ÷ spacing⌉ + 1, stays napkin's and says so; Figure 19 (p. 16)
  shows the staggered pattern the spacing assumes, and note 4 ("staggered per Figure 19") rides
  with the row.
- **Notes carried `not-encoded`** with every ledger result, each cited: the prohibited attachments
  — exterior veneers, hollow masonry, cantilevered overhangs, bay windows (p. 15); siding removed
  and corrosion-resistant flashing at the ledger (p. 13); "LEAD ANCHORS ARE PROHIBITED" (p. 15);
  the lateral-load hold-downs, "not less than two locations … not less than 1,500 lb" [R507.2.4]
  (p. 17); Table 5 notes 1–3, 6–8 (tip past the band, ½" maximum gap, flashing, engineered
  products, sheathing kinds, other band species). Lateral connection devices (Figures 22–23) are
  not a check; the note is the disclosure.

### 3.4 Footings and post heights: Appendix B, not Table 4 (#42)

**Why Appendix B.** Table 4 (p. 12) is keyed on beam span × joist span, assumes full L_B/4 and
L_J/4 overhangs, and gives footing sizes for **1,500 psf soil only** (note 2), with "value may be
multiplied by 0.9 for corner posts". That is not the shape #42 asked for and not the shape
`deck-footing` has (tributary area × a typed soil bearing value with a `lower-bound` band).
Appendix B, "As an alternative to Table 4" (p. B2), is exactly that shape: **Table B3** (p. B5)
gives round diameter, square side and thickness by tributary area (10–250 sq ft in tens) and soil
capacity (1,500, 2,000, 2,500, 3,000 psf), and **Tables B1/B2** (pp. B3–B4) give maximum post
height by tributary area for corner and centre posts. The `lower-bound` band already encodes p. 11:
a site value below 1,500 psf is Out of scope ("the allowable bearing capacity shall be determined
by a soils investigation"). A Decision for Marc (§8.2), because it chooses one part of the
document over another.

**Table B3 → `deck-footing`.** Sample, as printed: 40 sq ft at 1,500 psf: 16" round, 15" square,
6" thick (Table B3, p. B5). Schema gaps:
- Three outputs — `round`, `square`, `thickness` — instead of one `footing` text; the sentence
  reads "16" round or 15" square, 6" thick".
- **Note 2 is an operation, not a footnote:** "Tributary area shall be multiplied by 1.25 at
  center posts with beams not spliced (continuous)." napkin's beam is one continuous piece over
  its posts (`DeckFraming` buys it full width), so the middle post's area is multiplied — declared
  by the table as `"centerPostFactor": { "multiply": "5/4", "location": "Table B3 note 2, p. B5" }`,
  applied by napkin's tributary-area derivation and **said in the sentence** ("30.0 sq ft × 1.25
  for a middle post under a continuous beam (…) = 37.5 sq ft"). Deck footnotes stay `not-encoded`;
  this is a declared factor with its own load checks, like bracing's.
- **The tributary area must be the source's.** Appendix B defines it to the **beam centreline**
  (joist length J_L "from the ledger face to … the center point of the beam") and **post
  centreline** (beam span B_L "centerline of post to centerline of post"), Eq. B-1 for a centre
  post, ½(J_L + J_O)·B_L, and Eq. B-2 for a corner post (pp. B1–B2). `DeckFraming.TributaryArea`
  today uses the clear beam span and the joist span to the beam face, so it under-counts by half a
  beam thickness one way and a post width the other. Slice B3 changes the derivation to the
  centreline definitions, keeps it exact, and the check's sentence names the equation. *(Done in
  slice B2-fix, #41, 2026-09-27, both sides; Eq. B-1 as printed is (½J_L + J_O)(B_L), and without
  overhangs B_L and J_L run to the deck's outside edges — see "Decision 8 corrected" at the top.)*

**Tables B1/B2 → a new kind, `deck-post`.** Inputs: species group (five: Southern Pine; Douglas
Fir-Larch; Hem-Fir, Western Cedars; Redwood; Ponderosa Pine, Red Pine, SPF), post (`6x6`, `4x4`),
position (`corner` from B1, `center` from B2 — napkin's end post is a corner post, its middle
post a centre post), tributary area (`upper-bound`, 10–250) → maximum post height, or **NP**
(a row with `notPermitted: true`, answered as Out of scope citing the row). Sample, as printed:
centre post, 40 sq ft, Southern Pine 6x6: 14 ft (Table B2, p. B4). The check compares
`DeckFraming`'s post length (grade to beam underside, which is how DCA 6 measures it: "from grade
or top of foundation, whichever is highest, to the underside of the beam", p. 10) on a new
**Posts** line. B2 note 4 is the same 1.25 factor as B3's. A tension to transcribe as printed, not
resolve: the main body's minimum post is 6x6 (item 3, p. 2; p. 10) while B1/B2 tabulate 4x4
heights with their own note 3; both are the document. *(Resolved after B3, #42, 2026-09-27: Appendix B
replaces Table 4's heights and footings, not the minimum, so B1/B2 refuse a post under 6x6 before the
lookup, citing p. 10, and keep their 4x4 columns as printed — "Corrected after slice B3" at the top.)*

**Frost and depth.** The frost line stays napkin's comparison of two typed values with CT 2022's
42" offered (deck-and-porch §3.4); DCA 6 adds two notes for the frost line, `not-encoded`: "at
least 12 inches below the undisturbed ground surface or below the frost line, whichever is
deeper" (p. 11), and footings "closer than 5'-0" to an exterior house foundation wall must bear at
the same elevation as the footing of the house foundation" (p. 11).

### 3.5 Guards and stairs (pp. 19–22) → `deck-guard-stair` (#43)

The provisions file's fields and where each value is printed. The numbers live in figures, so a
`location` must say "Figure 27, p. 20", and the transcriber reads the rendered page. The one
sample: guard `triggerHeight` comes from the sentence "All decks greater than 30" above grade are
required to have a guard [R312.1]" (p. 19), and napkin's `Height > trigger` matches "greater
than". The rest, by location only:

| Field | Where printed |
|---|---|
| guard `minimumHeight`, `maximumOpening` | Figure 24, p. 19: the height callout and the sphere rule |
| stair `maximumRiser`, `minimumTread`, `maximumRiserDifference` | Figure 27, p. 20 |
| stair `minimumWidth` | p. 20, text [R311.7] |
| stair `handrailWhenRisersAtLeast` | p. 22, text [R311.7.8] |

Each value goes in with the IRC 2015 section DCA 6 brackets beside it.

**Not covered by the file today, listed so the person knows** (each `null` prints "not covered
by this pack"; the small ones are optional fields for Marc, §8.7): the guard post's minimum size
and maximum spacing and the rim minimum (Figure 24 and text, p. 19); the stringer's minimum size
and the maximum stringer spans, cut and solid (p. 20; Figure 28, p. 21); the minimum count of
cut stringers (p. 20); the intermediate-landing rule by total rise (p. 20); the stair guard's own
rules (Figure 30, p. 21); handrail height and grip shapes (p. 22); Table 6's minimum tread boards
by species (p. 21); the stair footing depth (p. 22). napkin's stair does not know cut from solid
stringers, so the span limits would need a typed kind first.

### 3.6 Species: one typed value, three ways of grouping

Table 2 groups the eight species (Table 1, p. 3) three ways, Table 3A two ways, Tables B1/B2 five
ways. The deck's one typed Species must resolve in every table, so the guide manifest lists the
eight names as printed and each table declares its printed groups:

```json
"speciesGroups": [ { "group": "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir", "species": ["Douglas Fir-Larch", "Hem-Fir", "Spruce-Pine-Fir"], "location": "Table 2, p. 4, row heading" }, … ]
```

Rows carry the group; the lookup maps the typed species to the group first and the trace says so
("species Hem-Fir → group Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir"). Load checks: every
species in a group is in the guide's list, every group's species appear in exactly one group of
that table, every row's `species` is a declared group, and the guide's eight species are all
placed (Table 3A places all eight; Tables B1/B2 too). This keeps the row count at the printed
count — 36 for Table 2, 168 for Table 3A after two-name cells — instead of one row per species
per cell, which is what the reviewer must check against the page.

### 3.7 Read and deliberately left out

Table 3A joist-hanger capacity (p. 9) and Table 7 trimmer-hanger capacity (p. 23): napkin lists
hangers as typed hardware, not a check. Decking fastening and the three-joist bearing rule (p. 3):
a note for the Deck section later, not a check. Joist-to-beam connection options (Figure 6,
p. 9), post-to-beam attachment and diagonal bracing at corner posts over 2'-0" (p. 10), rim-joist
details (p. 11), framing at a chimney or bay window (p. 23): details the permit set (#212) may
draw on; no check here. Appendix A, safety glazing at stairs (p. A1): a note for the permit set.
Non-ledger decks (p. 17): out of napkin's deck by deck-and-porch §13.2.

---

## 4. Schema and loader changes, consolidated

Each with its load checks and golden cases, landing in the slice that needs it (§6):

1. **Guide layer** (`kind: "guide"` manifest; pack `guides`; `CitationLayer.Guide`; `deck/` read by
   `DeckReader`; the base-vs-guide conflict rule; the picker clause; the caveat clause and
   `UNREVIEWED` on every deck line) — slice A.
2. **Scope `limits` and `notes`** on the guide, table-level `limits` on any deck table; the inputs
   and comparison forms of §2 — slice A.
3. **`speciesGroups`** per deck table — slice A.
4. **Deck golden runner** (`"deck"` golden files for the five kinds and the scope: cases expect
   `passes`, `short`, `sized`, `outOfScope`, `inputMissing`, `noData`; the coverage test that every
   row, limit and factor has a hand-authored case; boundary pairs generated and committed) and
   `Recompute`'s deck diffs — slice A. rules-engine-model §8 needs a golden per row before any row
   is transcribed, and `GoldenRunner` today handles only `"table"` and `"section"`.
5. **`overhang` output and `overhangLimit`** on `deck-joist`; the Cantilever line — slice B1.
6. **Beam span centre-to-centre** in `DeckFraming` — slice B2.
7. **`deck-footing` outputs `round`/`square`/`thickness`, `centerPostFactor`, tributary area to
   centrelines (Eq. B-1/B-2)**; the new **`deck-post`** kind with `notPermitted` rows and the
   Posts line — slice B3.
8. **`fastener` and `bandJoist` columns** on `deck-ledger`, `member` optional, the two deck inputs
   and the format bump — slice B4.
9. Optional guard/stair fields Marc picks in §8.7 — slice B5.

Nothing here touches header tables, bracing, overlays or site values.

---

## 5. Review process

PLAN.md's rule stands: **Opus transcribes, Fable reviews against the PDF**, because a transcription
error is caught by an independent reader, not by tests that agree with the pack. Per data slice:

1. The transcriber downloads DCA 6 from AWC in the same task, checks the SHA-256 against §0 (a
   different hash is a new printing: stop and record it), records the source in the guide's
   `sources`, encodes the table with every row's page and printed row/column, transcribes every
   note verbatim and classifies it (note, limit, or declared factor per §3), writes the golden
   file **from the page, never by exporting the pack**, one hand-authored case per row plus the
   generated boundary pairs, bumps the pack `revision`, sets `review.status` to `in-review`, runs
   `gate.sh`, pushes.
2. The reviewer, with the same PDF open, walks every row against the page and commits the
   checklist to `docs/code-packs/reviews/us-ct-2022/dca6-<table>.md` with the PDF hash, the
   reviewer's name and date, and a yes per row for inputs, outputs, notes and golden case; a no is
   a fix-and-re-review, not a comment.
3. `review.status` is pack-wide (rules-engine-model §1.1), so the CT pack stays `in-review` — and
   every line says UNREVIEWED — until every table it ships has a checklist: the deck tables,
   `frost.json`, and `site-values.json`, whose #210 transcription has no independent checklist
   yet (the pack is `unreviewed`). That is the honest state; a per-table status is a later
   refinement if the label bothers people.
4. Figures are read from the rendered page by both roles; a value whose only home is a figure
   cites the figure and page.

---

## 6. Slices

Each lands alone on `main` through `tools/scripts/gate.sh`, in order; the data slices may be
worked in parallel once A is in, since each owns its own table files, golden file and review
checklist, and the pack revision is bumped by whoever lands. Disjoint files except the usual
collision points.

| Slice | What | Issue | Model | Tests |
|---|---|---|---|---|
| **A** | §4 items 1–4 on the **synthetic** pack: `us-zz-deck` gains a guide layer `zz-guide-2099` (NOT CODE VALUES) carrying a scope limit on `supports`, a snow limit, a `speciesGroups` table and the joist table moved under it (the picker clause and the caveat line are proven there; the CT pack is untouched until B1 adds its `guides` entry with the first table); deck golden runner and `Recompute` deck diffs; `docs/rules-engine.md` | #238 | Opus (loader strictness, as #198) | goldens for every synthetic row and limit; the conflict rule; a guide without a `supports` limit refused; the caveat clause on every deck line; the porch's Supports → Out of scope citing the limit; recompute to No data on switch |
| **B1** | Table 2 → `deck-joist` with `overhang`/`overhangLimit`; the Cantilever line | #41 | Opus | 36 goldens + boundaries; the worked example's 9'-9" joists |
| **B2** | Table 3A (p. 6) → `deck-beam`; beam span centre-to-centre | #41 | Opus | 168 goldens + boundaries; a two-name cell's two rows |
| **B3** | Table B3 → `deck-footing` (three outputs, `centerPostFactor`; the Appendix B tributary area landed in B2-fix); Tables B1/B2 → `deck-post`; the Posts line | #42 | Opus | 100 + 500 goldens incl. NP rows (25 areas × 5 groups × 2 posts × 2 positions); the ×1.25 sentence; below 1,500 psf → Out of scope |
| **B4** | Table 5 → `deck-ledger` with fastener and band-joist columns; the two deck inputs, panel boxes, format bump, samples | #40 | Opus (format and engine) | 49 goldens; Input missing names the new inputs; 2x6 ledger → the 2x8 limit; the notes shown |
| **B5** | pp. 19–22 → `deck-guard-stair`, plus the optional fields Marc picks | #43 | Opus | a golden per field; 30" trigger boundary; 4 risers → handrail |
| **C1–C5** | The independent review of each B slice, checklist committed, `review.status` per §5 | #40–#43 | **Fable** | the checklist test of rules-engine-model §8.3 |

Slice A must land before any B; B slices are independent of each other. The header and bracing
paths are untouched throughout.

---

## 7. Risks

1. **The guide's values may not be the adopted code's.** DCA 6 is based on the 2015 IRC (cover;
   References, p. 24); CT 2022 adopts the 2021 IRC (`pack.json`). Whether and how the deck
   provisions differ between those editions was not read for this note and is not claimed. The
   caveat on every line is the mitigation, and the guide entry is removed the day the IRC 2021
   deck tables are transcribed (#14's path).
2. **Ground snow load stands in for deck snow load** (§2). Conservative; a Decision.
3. **Two derivations were subtly off** against this source: the beam span (clear vs centreline)
   and the tributary area (faces vs centrelines). Both are fixed in B2/B3; the synthetic goldens
   change with them, which is a visible diff for the reviewer. *(Corrected, #41: the beam span was
   right as the clear span — Figure 3 measures L_B face to face; only the tributary area needed the
   centreline measures, done in B2-fix.)*
4. **4x4 posts in Appendix B vs the 6x6 minimum of the main body.** Transcribed as printed; the
   Posts line under a 4x4 cites B1/B2 and their note 3. *(Corrected, #42, 2026-09-27: that answered
   a 4x4 deck post Passes, which p. 10 forbids. The 4x4 columns stay as printed, and B1/B2's limit
   `t.post-size` refuses a post under 6x6 nominal before the lookup, citing p. 10.)*
5. **Figures carry the guard and stair values and the beam-span definition**; pdftotext does not
   extract them. Both roles read the rendered page; a citation names the figure.
6. **Two tables designated 3A.** Citations carry the page; a golden file names p. 6.
7. **Row count and review burden.** About 850 rows plus provisions across B1–B5 (36 + 168 + 100 +
   500 + 49), each with a golden case and a checklist line; Tables B1/B2 are more than half of
   it. This is the cost of #40–#43 being real; it is spread over five landable slices, and no
   slice ships a value without its review. If the post-height tables prove too much, B3 can land
   B3-the-footing-table first and the `deck-post` kind after.
8. **AWC's terms — now read, and open again.** Checked 2026-09-27 while researching the
   safe-default-pack question (`docs/research/safe-default-header-sources.md` §1):
   [awc.org/about/end-user-license-agreement](https://awc.org/about/end-user-license-agreement/)
   states (fragments; the page is Cloudflare-blocked to automated fetch, read only in part,
   **Marc should read it directly**): one printed copy only, no reproduction/resale/modification,
   "proprietary to the American Wood Council... protected under U.S. copyright law", and "INPUTTING
   THE PRODUCT OR ANY PORTION THEREOF INTO ANY ARTIFICIAL INTELLIGENCE OR SIMILAR PROGRAM, SUCH AS
   CHAT GPT, IS PROHIBITED." Whether "the Product" reaches a freely-downloadable guide like DCA 6,
   and what the AI clause means for a citation/computation use rather than reproducing the
   document, is Marc's reading, not a transcriber's or reviewer's. **Landed slices B1–B3 (Tables
   2, 3A, B1–B3) are not being revisited or retracted on this alone** — that is also Marc's call.
   **Ledger (#40, Table 5) and guards/stairs (#43) are held and must not start until Marc has read
   the page and said how this applies**, since they would repeat the same act now in question.
9. **The porch's Supports vocabulary** moves from "what the joist table declares" to "what the
   guide's scope declares"; the panel's `SupportsNote` and the free-text box must follow, or the
   person has nothing to choose from.

---

## 8. Decisions for Marc

Each in plain words with the default I recommend, so a "yes" is enough.

1. **DCA 6 is a guide layer the Connecticut pack declares, every deck answer names it and carries
   both caveats, and there is no project switch to turn it off.** Recommended: yes. The
   alternatives are a fake "code" in the picker or a switch whose off position is No data.
2. **Footings and post heights come from Appendix B (Tables B1–B3, by tributary area and your
   typed soil bearing value), not from Table 4 (1,500 psf soil only, full overhangs assumed).**
   Recommended: yes; the document offers B as an alternative to 4, and B is what #42 asked for.
3. **napkin compares your town's ground snow load with DCA 6's 40 psf limit as-is.** Recommended:
   yes; napkin does no snow arithmetic, so a town over 40 psf gets "out of scope, get it
   engineered" rather than a number.
4. **The deck gains two typed inputs for the ledger — the fastener (lag screw, bolt, bolt with
   stacked washers) and what the house's band joist is (1" LVL, 1-1/8" LVL, 1-½" lumber) — and the
   ledger check says Input missing until you type them.** Recommended: yes; the guide bands on
   exactly those, and it says you must verify the band joist before attaching a ledger.
5. **The short caveat clause is on every deck line, and the full paragraph once at the top of the
   block.** Recommended: yes — that is "beside every answer"; once per deck would be the
   alternative.
6. **Confirm that shipping transcribed DCA 6 tables, cited to AWC's document, is within the
   stance you took on 2026-09-25 for code tables, and record it in DESIGN.md §2.1** (as
   rules-engine-model §13.7 asked). Recommended: confirm before slice B1 starts; slice A ships no
   DCA 6 value.
7. **Which of the small guard and stair extras go in:** the guard post's maximum spacing, the
   stringer's minimum size, and the landing rule by total rise (§3.5). Recommended: the first
   two, as two fields; the stringer span limits and the stair guard wait for a typed cut/solid
   kind.
8. **Beam span and tributary area are measured the way the source defines them (post and beam
   centrelines), and the sentences say so.** Recommended: yes; the current face-to-face numbers
   under-count. *(Corrected by Marc, 2026-09-27, #41: the principle stands, the parenthesis was a
   misreading — the beam tables' L_B is face to face of posts (Figure 3, p. 7), and only the tributary
   area is measured to centrelines and the deck's edges (Appendix B, pp. B1–B2). See "Decision 8
   corrected" at the top.)*
9. **Opus transcribes, Fable reviews.** PLAN.md's table has Opus implementing #40–#43 with a
   Fable review against the source, while the four issues carry `model/sonnet`; the two disagree.
   Recommended: PLAN.md's rule, for the independence it argues.
