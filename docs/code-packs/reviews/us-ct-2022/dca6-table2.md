# Review: us-ct-2022 / DCA 6-2015 Table 2 (deck joist spans and overhangs)

**Status: REVIEWED, CLEAN.** Independent review of the #41 slice B1 transcription against the PDF
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3). Every value below was read by the reviewer
from the rendered page **before** any pack file was opened, written down, and only then compared.

**Summary: 63 lines checked — 63 ✓, 0 ✗, 0 ?.** Rows 36/36 ✓ (species group, size, spacing, L_J, L_O, note
attachments, golden cases, citation text); table-level items 15/15 ✓; manifest items 12/12 ✓. No discrepancy;
nothing for the transcriber to fix. The three rows the transcriber flagged (L_O printed above L_J/4) are as
printed. The scope-limit texts and cites are verbatim; their conditions are the design's declared readings of
the page, stated as such below. Remarks that are not discrepancies are listed at the end.

Reviewed by: **Claude Fable 5.1**  Date: **2026-09-27**  Source: `awc-dca6-2015` sha256
`205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` (verified with `shasum -a 256` before reading;
equals the line below and the manifest's `sources[0].sha256`)

Pack data hash at the reviewed commit (`e52f345`, transcriber's last commit; fast-forwarded into `8bd189b` on `main`
with only `Directory.Build.props` changed, data files byte-identical):
`shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-2.json`
- `65cfd5208d5accb7d50be474c148e0c60510be21f5d46840991b87028e901de1  packs/layers/dca6-2015/layer.json`
- `7cc3f327802e1179ede2e30dfa02b39afface77431c98bf2a17dd485d93547dc  packs/layers/dca6-2015/deck/table-2.json`
- also reviewed: `d9c21929982ed6bd341a65c00dcdd906d613f5c5e73ed7febefcbf61028b24f2  packs/golden/us-ct-2022/dca6-table-2.golden.json`
- also reviewed: `841534b4cf8a2c725928252539287a58ff87c6f9b72d8b755034c5e97a03958d  packs/packs/us-ct-2022/pack.json`

## Reviewer's method

- Pages 1, 2, 3, 4 and 8 rendered at 300 dpi (`pdftoppm -r 300 -png`) into the reviewer's own scratch directory
  (not the transcriber's renders) and read as images; p. 24 rendered at 200 dpi for the printing mark and
  copyright line. Table 2's body and its notes re-read from 400 dpi crops (`pdftoppm -r 400 -x 480 -y 260 -W 2500
  -H 1300` and `-y 1180 -H 400`), where every digit and superscript is unambiguous.
- The reviewer's reading of all 72 cells, the seven notes, the superscript positions and the pp. 1–3, 8 passages was
  written to a file first; `pdftotext -layout` of p. 4 was then used only as a cross-check (it agrees on every cell,
  though its layout drops the last "2x12" label a line down and splits superscripts — a reason to trust the image,
  not the text).
- Comparison: a script holding the reviewer's reading (typed from that file, not from the pack) was diffed against
  `table-2.json` on species, member, spacing, span, overhang and footnotes for all 36 rows: 0 differences. Every
  row's `location` string (what `DeckCheck.Cited` shows a user) was checked to name its own group, size and
  spacing column, and only the two 18'-0" rows to carry the note-7 parenthetical: 0 problems.
- Golden: every hand span case's `allowed` equals the page's L_J and its `over` equals asked − L_J; every hand
  cantilever case's `allowed` equals min(page L_O, asked span ÷ 4) exactly, including the three L/4-governs cases;
  the L_J/L_O value each case's `location` claims equals the page's. 76 hand row cases, 9 hand scope/refusal cases,
  148 generated boundary cases.

## The document

| | |
|---|---|
| Title | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code* (DCA 6), American Wood Council |
| Printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" (cover); "Copyright © 2007, 2009, 2010, 2014, 2015, 2018 / American Wood Council" and printing mark "04-18" (p. 24) — both confirmed on the rendered pages |
| URL | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` |
| Retrieved / checked | 2026-09-27 (hash verified by the transcriber before reading, and again by the reviewer) |
| Pages read by the reviewer | p. 1 (cover), p. 2 (items 1–10), p. 3 (Table 1; JOIST SIZE), p. 4 (Table 2 and its notes; Figure 1A), p. 8 (DECK FRAMING PLAN; Figure 5), p. 24 (copyright, printing mark) |
| How read (transcriber) | Table 2 from the page rendered at 300 dpi (`pdftoppm -r 300`) and checked against `pdftotext -layout`; the two agree on every cell. Text passages from `pdftotext`, checked against the rendered pages 1, 2 and 8. |

Transcribed by: Claude Opus 5.5 (#41 slice B1), 2026-09-27. Files: `packs/layers/dca6-2015/layer.json`,
`packs/layers/dca6-2015/deck/table-2.json`, `packs/packs/us-ct-2022/pack.json` (the `guides` entry,
revision 2), golden file `packs/golden/us-ct-2022/dca6-table-2.golden.json`.

## Rows (Table 2, p. 4): 36 rows, each a span cell and an overhang cell

Each row is one species group × size × spacing; its span is the "Allowable Span (L_J)" cell and its
overhang the "Allowable Overhang (L_O)" cell under the same spacing heading. The "Pack" columns are
what `table-2.json` says; the reviewer held them — and the file itself — against the page. "Golden cases present"
means the golden file has a hand-authored span case for the row and a hand-authored cantilever case
whose expected `allowed` is the row's L_O (the runner enforces both; the reviewer checked that the expected values in
those cases are the page's, too).

Reviewer's verdict per row: ✓ = the pack value is what the page prints. All 36 rows: every column ✓.

| RowId | Printed row (p. 4) | Spacing column | Pack: span L_J | Pack: overhang L_O | Pack: notes | Inputs match | L_J matches | L_O matches | Notes match | Golden cases present | OK |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `r.sp.2x6.12` | Southern Pine 2x6 | 12" | 9'-11" | 1'-0" | 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x6.16` | Southern Pine 2x6 | 16" | 9'-0" | 1'-1" | 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x6.24` | Southern Pine 2x6 | 24" | 7'-7" | 1'-3" | 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x8.12` | Southern Pine 2x8 | 12" | 13'-1" | 1'-10" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x8.16` | Southern Pine 2x8 | 16" | 11'-10" | 2'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x8.24` | Southern Pine 2x8 | 24" | 9'-8" | 2'-4" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x10.12` | Southern Pine 2x10 | 12" | 16'-2" | 3'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x10.16` | Southern Pine 2x10 | 16" | 14'-0" | 3'-5" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x10.24` | Southern Pine 2x10 | 24" | 11'-5" | 2'-10" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x12.12` | Southern Pine 2x12 | 12" | 18'-0" | 4'-6" | 7 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2x12.16` | Southern Pine 2x12 | 16" | 16'-6" | 4'-2" | — | ✓ | ✓ | ✓ (flagged; as printed) | ✓ | ✓ | ✓ |
| `r.sp.2x12.24` | Southern Pine 2x12 | 24" | 13'-6" | 3'-4" | — | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x6.12` | DF-L, H-F, SPF 2x6 | 12" | 9'-6" | 0'-11" | 4, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x6.16` | DF-L, H-F, SPF 2x6 | 16" | 8'-4" | 1'-0" | 4, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x6.24` | DF-L, H-F, SPF 2x6 | 24" | 6'-10" | 1'-2" | 4, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x8.12` | DF-L, H-F, SPF 2x8 | 12" | 12'-6" | 1'-8" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x8.16` | DF-L, H-F, SPF 2x8 | 16" | 11'-1" | 1'-10" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x8.24` | DF-L, H-F, SPF 2x8 | 24" | 9'-1" | 2'-2" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x10.12` | DF-L, H-F, SPF 2x10 | 12" | 15'-8" | 2'-10" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x10.16` | DF-L, H-F, SPF 2x10 | 16" | 13'-7" | 3'-2" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x10.24` | DF-L, H-F, SPF 2x10 | 24" | 11'-1" | 2'-9" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x12.12` | DF-L, H-F, SPF 2x12 | 12" | 18'-0" | 4'-4" | 4, 7 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x12.16` | DF-L, H-F, SPF 2x12 | 16" | 15'-9" | 3'-11" | 4 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl.2x12.24` | DF-L, H-F, SPF 2x12 | 24" | 12'-10" | 3'-3" | 4 | ✓ | ✓ | ✓ (flagged; as printed) | ✓ | ✓ | ✓ |
| `r.rw.2x6.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 12" | 8'-10" | 0'-9" | 5, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x6.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 16" | 8'-0" | 0'-10" | 5, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x6.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 24" | 6'-10" | 0'-11" | 5, 6 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x8.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 12" | 11'-8" | 1'-5" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x8.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 16" | 10'-7" | 1'-7" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x8.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 24" | 8'-8" | 1'-9" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x10.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 12" | 14'-11" | 2'-5" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x10.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 16" | 13'-0" | 2'-7" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x10.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 24" | 10'-7" | 2'-8" | 5 | ✓ | ✓ | ✓ (flagged; as printed) | ✓ | ✓ | ✓ |
| `r.rw.2x12.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 12" | 17'-5" | 3'-7" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x12.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 16" | 15'-1" | 3'-9" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.rw.2x12.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 24" | 12'-4" | 3'-1" | 5 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Transcriber's note for the reviewer: in three rows the printed L_O is more than a quarter of the
row's own L_J — `r.sp.2x12.16` (4'-2" vs 16'-6"/4 = 4'-1 1/2"), `r.dfl.2x12.24` (3'-3" vs 3'-2 1/2") and
`r.rw.2x10.24` (2'-8" vs 2'-7 3/4"). That is as printed; note 3 and p. 3 cap the overhang at L/4, which
napkin applies. The golden file proves those three L_O values on a span of 4 × L_O and shows L/4
governing at L_J. Worth a second look on the page.

**Reviewer's verdict on the three flagged rows: as printed, confirmed.** At 400 dpi the cells read unambiguously
"4' - 2"" (Southern Pine 2x12, 16" L_O), "3' - 3"" (Douglas Fir-Larch group 2x12, 24" L_O) and "2' - 8"" (Redwood
group 2x10, 24" L_O); `pdftotext` agrees. Each exceeds its row's L_J/4 by 1/2", 1/2" and 1/4" respectively. This is
not a transcription error and not a page error to "correct": L_O is the cantilever's own limit (note 3: cantilever
length/180 with a 220 lb point load) and L/4 is a separate cap on the actual span L (p. 3; note 3), so a row whose
L_O sits just above L_J/4 is simply one where L/4 governs whenever the joist is at its full allowable span. The pack
transcribes L_O as printed and declares the cap once as `overhangLimit`; the golden cases show L_O governing at
4 × L_O and L/4 governing at L_J. That is the right encoding.

## Table-level encoding

| Item | Where read | What the pack encodes | Matches the page | Classification right | OK |
|---|---|---|---|---|---|
| Designation and title | p. 4, "Table 2. Maximum Joist Spans and Overhangs.¹" | `table: "2"`, `title: "Maximum Joist Spans and Overhangs."`, `location: "p. 4"` | ✓ | — | ✓ |
| Species column values = the three printed row headings | p. 4, Species column | "Southern Pine"; "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir"; "Redwood, Western Cedars, Ponderosa Pine, Red Pine" | ✓ (the page breaks the second heading "Douglas Fir-/Larch, Hem-Fir,/Spruce-Pine-Fir⁴" and the third "Redwood,/Western Cedars,/Ponderosa Pine⁵,/Red Pine⁵" across lines; the comma-joined forms are the natural single-line reading) | — | ✓ |
| Species group: Southern Pine | p. 4, first row heading | Southern Pine | ✓ | — | ✓ |
| Species group: DF-L, H-F, SPF | p. 4, second row heading (note 4) | Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir | ✓ (superscript 4 sits after "Spruce-Pine-Fir", the heading's last word) | — | ✓ |
| Species group: Redwood … Red Pine | p. 4, third row heading (note 5 on Ponderosa Pine and Red Pine) | Redwood, Western Cedars, Ponderosa Pine, Red Pine | ✓ (superscript 5 sits on "Ponderosa Pine" and on "Red Pine"; none on Redwood or Western Cedars — as the pack's `speciesGroups[2].location` says) | — | ✓ |
| Size column values | p. 4, Size column | 2x6, 2x8, 2x10, 2x12 (the "6" after 2x6 is note 6, not part of the size) | ✓ (each of the three "2x6" cells carries superscript 6; no other size cell has one) | — | ✓ |
| Spacing column values | p. 4, "Joist Spacing (o.c.)" | 12", 16", 24", exact | ✓ (printed twice, once over L_J and once over L_O; rows carry `12in`/`16in`/`24in`) | — | ✓ |
| `overhangLimit` | p. 3, JOIST SIZE: "Overhang length is the lesser of allowable overhang, LO, or one fourth the joist span, L/4."; p. 4 note 3 | `fraction: "1/4"`, `of: "span"` (the actual joist span L, face of support to face of support, p. 3 and Figure 1A) | ✓ (p. 3 sentence verbatim, subscript O flattened to "LO"; p. 3 defines L as face of support to face of support, excluding L_O; Figure 1A labels "L_O or L/4 maximum overhang" and "joist span (L≤L_J)") | declared factor ✓ | ✓ |
| Note 1 | p. 4 | "Assumes 40 psf live load, 10 psf dead load, No. 2 grade, and wet service conditions." | ✓ verbatim; superscript 1 is on the table title | not-encoded, table ✓ (also the first half of scope limit `s.loads`) | ✓ |
| Note 2 | p. 4 (on "Allowable Span") | "Assumes L/360 deflection." | ✓ verbatim; superscript 2 on "Allowable Span" | not-encoded, table ✓ | ✓ |
| Note 3 | p. 4 (on "Allowable Overhang") | "Maximum allowable overhang cannot exceed L/4 or ¼ of actual main span. Assumes cantilever length/180 deflection with 220 lb point load (See Figure 1A and Figure 2)." | ✓ verbatim; superscript 3 on "Allowable Overhang" | not-encoded, table; its L/4 cap also as `overhangLimit` ✓ | ✓ |
| Note 4 | p. 4 (on the DF-L, H-F, SPF heading) | "Incising assumed for Douglas Fir-Larch, Hem-Fir, and Spruce-Pine-Fir." | ✓ verbatim | not-encoded, on the 12 `r.dfl.*` rows ✓ (the superscript is on the group heading, so every row of the group) | ✓ |
| Note 5 | p. 4 (on Ponderosa Pine and Red Pine) | "Design values based on northern species with no incising assumed." | ✓ verbatim | not-encoded, on the 12 `r.rw.*` rows ✓ (the row is the group; the superscript sits on two of its four species, which the pack records in the group's `location`; the note is shown for the whole group, which is the finest the row structure allows and errs toward showing it) | ✓ |
| Note 6 | p. 4 (on each "2x6") | "Ledger shall be a minimum of 2x8 nominal. Joists and rim joists to which guard posts are attached shall be a minimum of 2x8 nominal." | ✓ verbatim | not-encoded, on the 9 `*.2x6.*` rows ✓ (its ledger half becomes a ledger-table limit in B4, its guard half a guard note in B5) | ✓ |
| Note 7 | p. 4 (on the two 18'-0" cells) | "Joist length prescriptively limited to 18'-0" for footing design." | ✓ verbatim; superscript 7 is on exactly the two "18' - 0"" span cells (Southern Pine 2x12 @12", DF-L group 2x12 @12") | not-encoded, on `r.sp.2x12.12` and `r.dfl.2x12.12` ✓ | ✓ |

## The guide manifest (`packs/layers/dca6-2015/layer.json`)

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| Identity | cover, p. 1; p. 24 | shortName "DCA 6-2015"; title "Prescriptive Residential Wood Deck Construction Guide, Based on the 2015 International Residential Code"; publisher American Wood Council; basis ICC IRC 2015 | ✓ (cover: "Prescriptive Residential Wood Deck Construction Guide" / "Based on the 2015 International Residential Code" on two lines, joined with a comma; the banner reads "Design for Code Acceptance 6"; p. 24 References lists "2015 International Residential Code (IRC), International Code Council (ICC)") | ✓ |
| Caveat `basis` | cover, p. 1 | "Based on the 2015 International Residential Code" | ✓ verbatim (cover subtitle) | ✓ |
| Caveat `irc-governs` | p. 1 | "Where differences exist between provisions of this document and the IRC, provisions of the IRC shall apply." | ✓ verbatim (cover box, fourth sentence) | ✓ |
| Limit `s.loads` (supports not in ["deck"]) | Table 2 note 1, p. 4; MINIMUM REQUIREMENTS & LIMITATIONS item 8, p. 2 | two verbatim passages joined by an ellipsis: note 1 (above) … "Decks supporting large concentrated loads such as hot tubs are beyond the scope of this document." | ✓ both passages verbatim, both cites right. **Condition:** the page has no "supports" input; `notIn ["deck"]` is the design's declared reading (deck-guide-pack §2) that note 1's 40 psf live / 10 psf dead assumption plus item 8 put anything a deck carries beyond itself out of the guide. Stricter than the page's literal words, cited to them, and the design says why — right. | ✓ |
| Limit `s.snow` (groundSnowLoad above 40) | item 9, p. 2 | "This document does not apply to decks which will experience snow loads, snow drift loads, or sliding snow loads that exceed 40 psf." | ✓ verbatim, cite right. **Condition:** "exceed 40 psf" → `above: 40` (exactly 40 stays in scope) ✓. The page speaks of snow loads on the deck; the pack compares the site's *ground* snow load, a conservative surrogate the design chose (§2) and lists as a Decision for Marc (§8.3) — an interpretation, not a transcription, and documented. | ✓ |
| Limit `s.shape` (deckLength above deckWidth) | item 2, p. 2; DECK FRAMING PLAN and Figure 5, p. 8 (length runs out from the house with the joists, width along it) | "Overall deck length shall be equal to or less than overall deck width. See DECK FRAMING PLAN for definition of deck length and width." | ✓ verbatim (both sentences of item 2), cites right. **Condition faithful:** Figure 5 dimensions "overall deck length" vertically along the joists from the ledger to the rim joist (joist span L plus "L_O or L/4 max. overhang") and "overall deck width" horizontally along the ledger (beam span plus two L_B/4 overhangs); p. 8 text: "The overall deck length shall be equal to or less than the overall deck width." Item 2 is violated exactly when length > width, which is `aboveInput` on `deckLength` vs `deckWidth`. napkin feeds `deckLength` from `DeckFraming.Depth` ("its depth out from the house") and `deckWidth` from `DeckFraming.Width` ("along the ledger") (`DeckCheck.cs` l. 104; `DeckEvaluator.cs` l. 297), so "deck length above deck width" is what item 2 / Figure 5 mean. | ✓ |
| Note `n.single` | item 1, p. 2 | "This document applies to single level residential wood decks that are attached to the house to resist lateral forces. [R507.2.4]" | ✓ verbatim including the bracketed IRC reference | ✓ |
| Note `n.stairs` | DECK FRAMING PLAN, p. 8 | "Stairs and stair landings shall not be included in determining the overall deck length or width." | ✓ verbatim (p. 8, second paragraph) | ✓ |
| Note `n.materials` | item 4, p. 2; Table 1, p. 3 | "All lumber and glued laminated timber shall be a naturally durable species (such as Redwood or Western Cedars where 90 percent or more of the width of each side is heartwood); or be preservatively treated with an approved process in accordance with American Wood Protection Association standards (Table 1) [R317 and R318]." | ✓ verbatim (item 4's second sentence; the page italicises *approved*, lost in plain text; the sentences before and after it, on grade marks and ground contact, are deliberately not included) | ✓ |
| Species (8) | Table 1, p. 3; Table 2, p. 4 | Southern Pine, Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine — Table 2's spelling; Table 1 prints "SPF" for Spruce-Pine-Fir | ✓ Table 1's eight Preservative-Treated species are exactly these (Redwood and Western Cedars appear again under Naturally Durable); each is placed in exactly one Table 2 group; the three groups are the three row headings | ✓ |
| Source | cover; p. 24; the file | URL, printing, retrievedOn 2026-09-27, sha256 as above | ✓ sha256 equals the verified file; printing "Copyright © 2018 American Wood Council (cover); printing mark 04-18 (p. 24)" confirmed on both pages | ✓ |
| CT pack `guides` entry, revision 2 | — (napkin's own) | `guides: [ { id: "dca6-2015", notes: … } ]`, `revision: 2`, `review.status: "unreviewed"` | ✓ present; the entry's note (a guide on the 2015 IRC, not CT's adopted 2021 IRC; the IRC governs where they differ) matches the cover. User-facing citation, verified at the code level (`DeckCheck.Cited` and `DeckGuide.Clause` read, not run), is of the form "DCA 6-2015 Table 2 row r.sp.2x8.16, p. 4, row Southern Pine 2x8, column 16" of L_J and of L_O; species group "…", … — a guide on the 2015 IRC, not the pack's adopted code; the IRC governs where they differ (p. 1)": the row `location` strings, the guide's short name and the caveat's location are the page's. | ✓ |

Read and deliberately not encoded in this slice: Tables 3A (both), 3B, 4, 5, 6, 7, the Appendix B tables and
the guard and stair figures (slices B2–B5); items 3, 5–7, 10–13 of pp. 2–3.

## Golden file spot-checks (`packs/golden/us-ct-2022/dca6-table-2.golden.json`)

All 76 hand row cases were checked by script against the reviewer's page reading (see method); these were also
read by eye against the page:

| Case (location) | Inputs | Expect | Page | OK |
|---|---|---|---|---|
| `r.sp.2x6.12` span | 12" 2x6 SP, span 9'-5" | passes, allowed 9'-11" | L_J 9'-11" | ✓ |
| `r.sp.2x6.16` cantilever | span 9'-0", cantilever 1'-2" | short, allowed 1'-1", over 1" | L_O 1'-1"; L/4 = 2'-3" so L_O governs | ✓ |
| `r.sp.2x8.16` span (worked example) | span 9'-9" | passes, allowed 11'-10" | L_J 11'-10" | ✓ |
| `r.sp.2x12.12` cantilever | span 18'-0", cantilever 4'-6" | passes, allowed 4'-6" | L_O 4'-6" = L/4 exactly | ✓ |
| `r.sp.2x12.16` cantilever, L_O governs | span 16'-8", cantilever 4'-3" | short, allowed 4'-2", over 1" | L_O 4'-2"; L/4 = 4'-2" | ✓ |
| `r.sp.2x12.16` cantilever, L/4 governs | span 16'-6", cantilever 4'-2" | short, allowed 4'-1 1/2", over 1/2" | L/4 of 16'-6" = 4'-1 1/2" < L_O 4'-2" | ✓ |
| `r.dfl.2x6.12` span (Douglas Fir-Larch typed) | span 9'-0" | passes, allowed 9'-6" | L_J 9'-6" | ✓ |
| `r.dfl.2x8.12` span (Hem-Fir typed) | span 12'-0" | passes, allowed 12'-6" | L_J 12'-6" | ✓ |
| `r.dfl.2x10.12` span (Spruce-Pine-Fir typed) | span 15'-2" | passes, allowed 15'-8" | L_J 15'-8" | ✓ |
| `r.dfl.2x12.24` cantilever, L_O governs | span 13'-0" (4 × L_O), cantilever 3'-2" (Hem-Fir typed) | passes, allowed 3'-3" | L_O 3'-3"; L/4 = 3'-3" | ✓ |
| `r.dfl.2x12.24` cantilever, L/4 governs | span 12'-10", cantilever 3'-3" | short, allowed 3'-2 1/2", over 1/2" | L/4 of 12'-10" = 3'-2 1/2" | ✓ |
| `r.rw.2x6.12` cantilever | span 8'-10", cantilever 9" | passes, allowed 9" | L_O 0'-9" | ✓ |
| `r.rw.2x10.24` cantilever, L_O governs | span 10'-8" (4 × L_O), cantilever 2'-7" (Ponderosa Pine typed) | passes, allowed 2'-8" | L_O 2'-8"; L/4 = 2'-8" | ✓ |
| `r.rw.2x10.24` cantilever, L/4 governs | span 10'-7", cantilever 2'-8" | short, allowed 2'-7 3/4", over 1/4" | L/4 of 10'-7" = 2'-7 3/4" | ✓ |
| `r.rw.2x12.24` span | 24" 2x12 RW, span 12'-3" | passes, allowed 12'-4" | L_J 12'-4" | ✓ |
| `s.loads` limit | supports "porch-roof" | outOfScope s.loads | note 1 / item 8 | ✓ |
| `s.snow` limit | groundSnowLoad 45 | outOfScope s.snow | item 9: exceed 40 psf | ✓ |
| `s.shape` limit | deckLength 16'-0 1/16", deckWidth 16'-0" | outOfScope s.shape | item 2: length must be ≤ width | ✓ |
| missing supports / groundSnowLoad / species | — | inputMissing | the scope and Table 2 need them | ✓ |
| Western Larch; 2x14; 20" spacing | — | outOfScope column species / member / spacing | none on p. 4 (Table 1 has no Western Larch) | ✓ |

## Remarks (not discrepancies, no action required)

1. `guide.title` and `sources[0].title` join the cover's two title lines with a comma and the latter appends "(DCA 6)";
   the cover has a line break and no parenthetical. Punctuation of a two-line title, not a data difference.
2. `speciesGroups[1].location` says "printed 'Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir' with note 4": the page
   breaks it "Douglas Fir-" / "Larch, Hem-Fir," / "Spruce-Pine-Fir⁴". Same words.
3. `overhangLimit.location` quotes "LO" for the page's L with subscript O. Plain-text flattening.
4. Note 5 rides all 12 Redwood-group rows although its superscript is on two of the group's four species (see the
   table-level line): the finest attachment the row structure allows, documented in the group's `location`.
5. The scope-limit *conditions* for `s.loads` and `s.snow` are interpretations the design declares (§2, §8.3), not
   transcriptions; their *texts* and *cites* are verbatim. `s.shape` is a faithful encoding of item 2 and Figure 5.
6. `DeckFraming.Width`'s doc comment calls the along-ledger dimension "the deck's length along the ledger" while DCA 6
   calls that the deck's *width*; the mapping into `deckWidth` is right, only napkin's own comment uses the other
   word. A code comment, outside this review's files.

## Sign-off

Rows checked: 36 of 36. Table-level items: 15 of 15. Manifest items: 12 of 12. Golden spot-checks: 20 lines by eye,
76 hand row cases by script, all ✓.
Discrepancies found (each a fix and re-review, not a comment): **none.**

Review status: per deck-guide-pack §5.3 the CT pack's `review.status` is pack-wide and cannot become `reviewed` on
this checklist alone — `frost.json` and `site-values.json` (#210) have no independent checklist yet. It stays
`unreviewed` (the B1 as-built choice) or becomes §5's `in-review` if the orchestrator prefers that label; either way
this table's own review is complete and clean, and `review.checklist` may point at this file.

Sign-off: Claude Fable 5.1 (independent reviewer), 2026-09-27
