# Review: us-ct-2022 / DCA 6-2015 Table 2 (deck joist spans and overhangs)

**Status: NOT REVIEWED.** This checklist was prepared by the transcriber for the independent reviewer
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3). Every verdict box below is empty on
purpose: the reviewer fills them, with the PDF open, and signs at the bottom. Until then the pack's
`review.status` stays `unreviewed` and every deck line says UNREVIEWED.

Reviewer: ______  Date: ______  Source: `awc-dca6-2015` sha256 ______ (must equal the line below)
Pack data hash: `shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-2.json` at the reviewed commit: ______

## The document

| | |
|---|---|
| Title | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code* (DCA 6), American Wood Council |
| Printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" (cover); printing mark "04-18" (p. 24) |
| URL | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` |
| Retrieved / checked | 2026-09-27 (hash verified by the transcriber before reading) |
| Pages read | p. 1 (cover), p. 2 (items 1–10), p. 3 (Table 1; JOIST SIZE), p. 4 (Table 2 and its notes; Figure 1A), p. 8 (DECK FRAMING PLAN; Figure 5) |
| How read | Table 2 from the page rendered at 300 dpi (`pdftoppm -r 300`) and checked against `pdftotext -layout`; the two agree on every cell. Text passages from `pdftotext`, checked against the rendered pages 1, 2 and 8. |

Transcribed by: Claude Opus 5.5 (#41 slice B1), 2026-09-27. Files: `packs/layers/dca6-2015/layer.json`,
`packs/layers/dca6-2015/deck/table-2.json`, `packs/packs/us-ct-2022/pack.json` (the `guides` entry,
revision 2), golden file `packs/golden/us-ct-2022/dca6-table-2.golden.json`.

## Rows (Table 2, p. 4): 36 rows, each a span cell and an overhang cell

Each row is one species group × size × spacing; its span is the "Allowable Span (L_J)" cell and its
overhang the "Allowable Overhang (L_O)" cell under the same spacing heading. The "Pack" columns are
what `table-2.json` says; hold them — and the file itself — against the page. "Golden cases present"
means the golden file has a hand-authored span case for the row and a hand-authored cantilever case
whose expected `allowed` is the row's L_O (the runner enforces both; check that the expected values in
those cases are the page's, too).

| RowId | Printed row (p. 4) | Spacing column | Pack: span L_J | Pack: overhang L_O | Pack: notes | Inputs match | L_J matches | L_O matches | Notes match | Golden cases present | OK |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `r.sp.2x6.12` | Southern Pine 2x6 | 12" | 9'-11" | 1'-0" | 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x6.16` | Southern Pine 2x6 | 16" | 9'-0" | 1'-1" | 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x6.24` | Southern Pine 2x6 | 24" | 7'-7" | 1'-3" | 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x8.12` | Southern Pine 2x8 | 12" | 13'-1" | 1'-10" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x8.16` | Southern Pine 2x8 | 16" | 11'-10" | 2'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x8.24` | Southern Pine 2x8 | 24" | 9'-8" | 2'-4" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x10.12` | Southern Pine 2x10 | 12" | 16'-2" | 3'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x10.16` | Southern Pine 2x10 | 16" | 14'-0" | 3'-5" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x10.24` | Southern Pine 2x10 | 24" | 11'-5" | 2'-10" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x12.12` | Southern Pine 2x12 | 12" | 18'-0" | 4'-6" | 7 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x12.16` | Southern Pine 2x12 | 16" | 16'-6" | 4'-2" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2x12.24` | Southern Pine 2x12 | 24" | 13'-6" | 3'-4" | — | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x6.12` | DF-L, H-F, SPF 2x6 | 12" | 9'-6" | 0'-11" | 4, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x6.16` | DF-L, H-F, SPF 2x6 | 16" | 8'-4" | 1'-0" | 4, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x6.24` | DF-L, H-F, SPF 2x6 | 24" | 6'-10" | 1'-2" | 4, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x8.12` | DF-L, H-F, SPF 2x8 | 12" | 12'-6" | 1'-8" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x8.16` | DF-L, H-F, SPF 2x8 | 16" | 11'-1" | 1'-10" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x8.24` | DF-L, H-F, SPF 2x8 | 24" | 9'-1" | 2'-2" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x10.12` | DF-L, H-F, SPF 2x10 | 12" | 15'-8" | 2'-10" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x10.16` | DF-L, H-F, SPF 2x10 | 16" | 13'-7" | 3'-2" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x10.24` | DF-L, H-F, SPF 2x10 | 24" | 11'-1" | 2'-9" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x12.12` | DF-L, H-F, SPF 2x12 | 12" | 18'-0" | 4'-4" | 4, 7 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x12.16` | DF-L, H-F, SPF 2x12 | 16" | 15'-9" | 3'-11" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.2x12.24` | DF-L, H-F, SPF 2x12 | 24" | 12'-10" | 3'-3" | 4 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x6.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 12" | 8'-10" | 0'-9" | 5, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x6.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 16" | 8'-0" | 0'-10" | 5, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x6.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x6 | 24" | 6'-10" | 0'-11" | 5, 6 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x8.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 12" | 11'-8" | 1'-5" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x8.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 16" | 10'-7" | 1'-7" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x8.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x8 | 24" | 8'-8" | 1'-9" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x10.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 12" | 14'-11" | 2'-5" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x10.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 16" | 13'-0" | 2'-7" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x10.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x10 | 24" | 10'-7" | 2'-8" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x12.12` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 12" | 17'-5" | 3'-7" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x12.16` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 16" | 15'-1" | 3'-9" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.2x12.24` | Redwood, W. Cedars, P. Pine, R. Pine 2x12 | 24" | 12'-4" | 3'-1" | 5 | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

Transcriber's note for the reviewer: in three rows the printed L_O is more than a quarter of the
row's own L_J — `r.sp.2x12.16` (4'-2" vs 16'-6"/4 = 4'-1 1/2"), `r.dfl.2x12.24` (3'-3" vs 3'-2 1/2") and
`r.rw.2x10.24` (2'-8" vs 2'-7 3/4"). That is as printed; note 3 and p. 3 cap the overhang at L/4, which
napkin applies. The golden file proves those three L_O values on a span of 4 × L_O and shows L/4
governing at L_J. Worth a second look on the page.

## Table-level encoding

| Item | Where read | What the pack encodes | Matches the page | Classification right | OK |
|---|---|---|---|---|---|
| Designation and title | p. 4, "Table 2. Maximum Joist Spans and Overhangs.¹" | `table: "2"`, `title: "Maximum Joist Spans and Overhangs."`, `location: "p. 4"` | ☐ | — | ☐ |
| Species column values = the three printed row headings | p. 4, Species column | "Southern Pine"; "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir"; "Redwood, Western Cedars, Ponderosa Pine, Red Pine" | ☐ | — | ☐ |
| Species group: Southern Pine | p. 4, first row heading | Southern Pine | ☐ | — | ☐ |
| Species group: DF-L, H-F, SPF | p. 4, second row heading (note 4) | Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir | ☐ | — | ☐ |
| Species group: Redwood … Red Pine | p. 4, third row heading (note 5 on Ponderosa Pine and Red Pine) | Redwood, Western Cedars, Ponderosa Pine, Red Pine | ☐ | — | ☐ |
| Size column values | p. 4, Size column | 2x6, 2x8, 2x10, 2x12 (the "6" after 2x6 is note 6, not part of the size) | ☐ | — | ☐ |
| Spacing column values | p. 4, "Joist Spacing (o.c.)" | 12", 16", 24", exact | ☐ | — | ☐ |
| `overhangLimit` | p. 3, JOIST SIZE: "Overhang length is the lesser of allowable overhang, LO, or one fourth the joist span, L/4."; p. 4 note 3 | `fraction: "1/4"`, `of: "span"` (the actual joist span L, face of support to face of support, p. 3 and Figure 1A) | ☐ | declared factor | ☐ |
| Note 1 | p. 4 | "Assumes 40 psf live load, 10 psf dead load, No. 2 grade, and wet service conditions." | ☐ | not-encoded, table (also the first half of scope limit `s.loads`) | ☐ |
| Note 2 | p. 4 (on "Allowable Span") | "Assumes L/360 deflection." | ☐ | not-encoded, table | ☐ |
| Note 3 | p. 4 (on "Allowable Overhang") | "Maximum allowable overhang cannot exceed L/4 or ¼ of actual main span. Assumes cantilever length/180 deflection with 220 lb point load (See Figure 1A and Figure 2)." | ☐ | not-encoded, table; its L/4 cap also as `overhangLimit` | ☐ |
| Note 4 | p. 4 (on the DF-L, H-F, SPF heading) | "Incising assumed for Douglas Fir-Larch, Hem-Fir, and Spruce-Pine-Fir." | ☐ | not-encoded, on the 12 `r.dfl.*` rows | ☐ |
| Note 5 | p. 4 (on Ponderosa Pine and Red Pine) | "Design values based on northern species with no incising assumed." | ☐ | not-encoded, on the 12 `r.rw.*` rows (the group; the superscript sits on two of its four species) | ☐ |
| Note 6 | p. 4 (on each "2x6") | "Ledger shall be a minimum of 2x8 nominal. Joists and rim joists to which guard posts are attached shall be a minimum of 2x8 nominal." | ☐ | not-encoded, on the 9 `*.2x6.*` rows (its ledger half becomes a ledger-table limit in B4, its guard half a guard note in B5) | ☐ |
| Note 7 | p. 4 (on the two 18'-0" cells) | "Joist length prescriptively limited to 18'-0" for footing design." | ☐ | not-encoded, on `r.sp.2x12.12` and `r.dfl.2x12.12` | ☐ |

## The guide manifest (`packs/layers/dca6-2015/layer.json`)

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| Identity | cover, p. 1; p. 24 | shortName "DCA 6-2015"; title "Prescriptive Residential Wood Deck Construction Guide, Based on the 2015 International Residential Code"; publisher American Wood Council; basis ICC IRC 2015 | ☐ | ☐ |
| Caveat `basis` | cover, p. 1 | "Based on the 2015 International Residential Code" | ☐ | ☐ |
| Caveat `irc-governs` | p. 1 | "Where differences exist between provisions of this document and the IRC, provisions of the IRC shall apply." | ☐ | ☐ |
| Limit `s.loads` (supports not in ["deck"]) | Table 2 note 1, p. 4; MINIMUM REQUIREMENTS & LIMITATIONS item 8, p. 2 | two verbatim passages joined by an ellipsis: note 1 (above) … "Decks supporting large concentrated loads such as hot tubs are beyond the scope of this document." | ☐ | ☐ |
| Limit `s.snow` (groundSnowLoad above 40) | item 9, p. 2 | "This document does not apply to decks which will experience snow loads, snow drift loads, or sliding snow loads that exceed 40 psf." | ☐ | ☐ |
| Limit `s.shape` (deckLength above deckWidth) | item 2, p. 2; DECK FRAMING PLAN and Figure 5, p. 8 (length runs out from the house with the joists, width along it) | "Overall deck length shall be equal to or less than overall deck width. See DECK FRAMING PLAN for definition of deck length and width." | ☐ | ☐ |
| Note `n.single` | item 1, p. 2 | "This document applies to single level residential wood decks that are attached to the house to resist lateral forces. [R507.2.4]" | ☐ | ☐ |
| Note `n.stairs` | DECK FRAMING PLAN, p. 8 | "Stairs and stair landings shall not be included in determining the overall deck length or width." | ☐ | ☐ |
| Note `n.materials` | item 4, p. 2; Table 1, p. 3 | "All lumber and glued laminated timber shall be a naturally durable species (such as Redwood or Western Cedars where 90 percent or more of the width of each side is heartwood); or be preservatively treated with an approved process in accordance with American Wood Protection Association standards (Table 1) [R317 and R318]." | ☐ | ☐ |
| Species (8) | Table 1, p. 3; Table 2, p. 4 | Southern Pine, Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine — Table 2's spelling; Table 1 prints "SPF" for Spruce-Pine-Fir | ☐ | ☐ |
| Source | cover; p. 24; the file | URL, printing, retrievedOn 2026-09-27, sha256 as above | ☐ | ☐ |
| CT pack `guides` entry, revision 2 | — (napkin's own) | `guides: [ { id: "dca6-2015", notes: … } ]`, `revision: 2`, `review.status: "unreviewed"` | ☐ | ☐ |

Read and deliberately not encoded in this slice: Tables 3A (both), 3B, 4, 5, 6, 7, the Appendix B tables and
the guard and stair figures (slices B2–B5); items 3, 5–7, 10–13 of pp. 2–3.

## Sign-off

Rows checked: __ of 36. Table-level items: __ of 15. Manifest items: __ of 12.
Discrepancies found (each a fix and re-review, not a comment): ______

Sign-off: ______ (name/model), ______ (date)
