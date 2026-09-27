# Review: us-ct-2022 / DCA 6-2015 Table B3 (footing sizes, p. B5)

**Status: NOT REVIEWED.** This checklist was prepared by the transcriber for the independent reviewer
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3), in the shape of the completed Table 2 and Table 3A
reviews (`dca6-table2.md`, `dca6-table3a.md`). Every verdict box below is empty on purpose: the reviewer reads the
page first, writes the reading down, then fills the boxes (✓ the pack is what the page prints, ✗ it differs — a fix
and re-review, not a comment, ? unreadable) and signs at the bottom. Until then every deck line says UNREVIEWED.

Reviewed by: ______  Date: ______  Source: `awc-dca6-2015` sha256 ______ (must equal the line below)

Pack data hash at the reviewed commit:
`shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-b3.json packs/golden/us-ct-2022/dca6-table-b3.golden.json packs/packs/us-ct-2022/pack.json`
- ______ `packs/layers/dca6-2015/layer.json` (its `notes` text names Tables B1–B3 and their checklists; nothing else changed since the Table 3A review)
- ______ `packs/layers/dca6-2015/deck/table-b3.json`
- ______ `packs/golden/us-ct-2022/dca6-table-b3.golden.json`
- ______ `packs/packs/us-ct-2022/pack.json` (`revision` 3 → 4 and one sentence added to `notes`, nothing else)

## Reviewer's method

(The reviewer's own: pages rendered, resolution, how the reading was recorded before the pack was opened,
how it was compared.) ______

## The document

| | |
|---|---|
| Title | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code* (DCA 6), American Wood Council; Appendix B |
| Printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" (cover); printing mark "04-18" (p. 24) |
| URL | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` |
| Retrieved / checked | 2026-09-27 (hash verified by the transcriber before reading) |
| Pages read by the transcriber | pp. B1–B2 (Tributary Area, Eq. B-1 and B-2, J_L, J_O, B_L, B_O, Figures B1–B3, "Post and Footings Size"), pp. B3–B5 (Tables B1, B2, B3 and their notes), p. 10 (POST REQUIREMENTS: post height measured grade to the beam's underside; the 6x6 minimum), p. 12 (Table 4, read and not used, decision 2) |
| How read (transcriber) | Each table from its page rendered at 500 dpi (`pdftoppm -r 500`), in four crops (the 6x6 and 4x4 halves, rows 10–130 and 140–250; for B3 the 1500/2000 and 2500/3000 psf halves), typed into a reading file and from it into the pack; the golden file's per-row expectations were generated separately from `pdftotext -layout` of pp. B1–B5; the two readings were compared cell by cell before either was committed (800 cells, 0 differences) and the golden run agrees on every row. |

Transcribed by: Claude Opus 5.5 (#42 slice B3), 2026-09-27. Files: `packs/layers/dca6-2015/deck/table-b3.json`, `packs/layers/dca6-2015/layer.json` (its `notes`), `packs/packs/us-ct-2022/pack.json` (revision 4), golden file `packs/golden/us-ct-2022/dca6-table-b3.golden.json`.

## Rows (Table B3, p. B5): 100 rows

Each row is one Tributary Area (sq. ft.) row (10, 20 … 250, an upper-bound band) × one Soil Bearing Capacity column
(1500, 2000, 2500, 3000 psf, a lower-bound band: the row answers every soil value from its column up to the next);
its three outputs are the column's printed Round Footing Diameter (in.), Square Footing (in.) and Footing Thickness
(in.). "Golden case present" means the golden file has a hand-authored case for the row expecting its three values.

| RowId | Area ≤ (sq ft) | Soil ≥ (psf) | Pack: round | Pack: square | Pack: thickness | Inputs match | Outputs match | Golden case present | OK |
|---|---|---|---|---|---|---|---|---|---|
| `r.10.1500` | 10 | 1500 | 8" | 7" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.10.2000` | 10 | 2000 | 7" | 7" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.10.2500` | 10 | 2500 | 7" | 6" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.10.3000` | 10 | 3000 | 6" | 5" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.20.1500` | 20 | 1500 | 12" | 10" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.20.2000` | 20 | 2000 | 10" | 9" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.20.2500` | 20 | 2500 | 9" | 8" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.20.3000` | 20 | 3000 | 8" | 7" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.30.1500` | 30 | 1500 | 14" | 13" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.30.2000` | 30 | 2000 | 12" | 11" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.30.2500` | 30 | 2500 | 11" | 10" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.30.3000` | 30 | 3000 | 10" | 9" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.40.1500` | 40 | 1500 | 16" | 15" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.40.2000` | 40 | 2000 | 14" | 13" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.40.2500` | 40 | 2500 | 13" | 11" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.40.3000` | 40 | 3000 | 12" | 10" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.50.1500` | 50 | 1500 | 18" | 16" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.50.2000` | 50 | 2000 | 16" | 14" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.50.2500` | 50 | 2500 | 14" | 13" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.50.3000` | 50 | 3000 | 13" | 12" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.60.1500` | 60 | 1500 | 20" | 18" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.60.2000` | 60 | 2000 | 17" | 15" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.60.2500` | 60 | 2500 | 16" | 14" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.60.3000` | 60 | 3000 | 14" | 13" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.70.1500` | 70 | 1500 | 22" | 19" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.70.2000` | 70 | 2000 | 19" | 17" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.70.2500` | 70 | 2500 | 17" | 15" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.70.3000` | 70 | 3000 | 15" | 14" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.80.1500` | 80 | 1500 | 23" | 21" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.80.2000` | 80 | 2000 | 20" | 18" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.80.2500` | 80 | 2500 | 18" | 16" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.80.3000` | 80 | 3000 | 16" | 15" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.90.1500` | 90 | 1500 | 25" | 22" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.90.2000` | 90 | 2000 | 21" | 19" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.90.2500` | 90 | 2500 | 19" | 17" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.90.3000` | 90 | 3000 | 17" | 15" | 6" | ☐ | ☐ | ☐ | ☐ |
| `r.100.1500` | 100 | 1500 | 26" | 23" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.100.2000` | 100 | 2000 | 23" | 20" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.100.2500` | 100 | 2500 | 20" | 18" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.100.3000` | 100 | 3000 | 18" | 16" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.110.1500` | 110 | 1500 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.110.2000` | 110 | 2000 | 24" | 21" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.110.2500` | 110 | 2500 | 21" | 19" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.110.3000` | 110 | 3000 | 19" | 17" | 7" | ☐ | ☐ | ☐ | ☐ |
| `r.120.1500` | 120 | 1500 | 29" | 26" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.120.2000` | 120 | 2000 | 25" | 22" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.120.2500` | 120 | 2500 | 22" | 19" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.120.3000` | 120 | 3000 | 20" | 18" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.130.1500` | 130 | 1500 | 30" | 27" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.130.2000` | 130 | 2000 | 26" | 23" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.130.2500` | 130 | 2500 | 23" | 20" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.130.3000` | 130 | 3000 | 21" | 18" | 8" | ☐ | ☐ | ☐ | ☐ |
| `r.140.1500` | 140 | 1500 | 31" | 28" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.140.2000` | 140 | 2000 | 27" | 24" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.140.2500` | 140 | 2500 | 24" | 21" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.140.3000` | 140 | 3000 | 22" | 19" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.150.1500` | 150 | 1500 | 33" | 29" | 14" | ☐ | ☐ | ☐ | ☐ |
| `r.150.2000` | 150 | 2000 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.150.2500` | 150 | 2500 | 25" | 22" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.150.3000` | 150 | 3000 | 22" | 20" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.160.1500` | 160 | 1500 | 34" | 30" | 15" | ☐ | ☐ | ☐ | ☐ |
| `r.160.2000` | 160 | 2000 | 29" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.160.2500` | 160 | 2500 | 25" | 23" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.160.3000` | 160 | 3000 | 23" | 20" | 9" | ☐ | ☐ | ☐ | ☐ |
| `r.170.1500` | 170 | 1500 | 35" | 31" | 15" | ☐ | ☐ | ☐ | ☐ |
| `r.170.2000` | 170 | 2000 | 30" | 26" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.170.2500` | 170 | 2500 | 26" | 23" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.170.3000` | 170 | 3000 | 24" | 21" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.180.1500` | 180 | 1500 | 36" | 32" | 16" | ☐ | ☐ | ☐ | ☐ |
| `r.180.2000` | 180 | 2000 | 30" | 27" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.180.2500` | 180 | 2500 | 27" | 24" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.180.3000` | 180 | 3000 | 24" | 22" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.190.1500` | 190 | 1500 | 37" | 33" | 16" | ☐ | ☐ | ☐ | ☐ |
| `r.190.2000` | 190 | 2000 | 31" | 28" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.190.2500` | 190 | 2500 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.190.3000` | 190 | 3000 | 25" | 22" | 10" | ☐ | ☐ | ☐ | ☐ |
| `r.200.1500` | 200 | 1500 | 38" | 34" | 17" | ☐ | ☐ | ☐ | ☐ |
| `r.200.2000` | 200 | 2000 | 32" | 29" | 14" | ☐ | ☐ | ☐ | ☐ |
| `r.200.2500` | 200 | 2500 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.200.3000` | 200 | 3000 | 26" | 23" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.210.1500` | 210 | 1500 | 39" | 35" | 17" | ☐ | ☐ | ☐ | ☐ |
| `r.210.2000` | 210 | 2000 | 33" | 29" | 14" | ☐ | ☐ | ☐ | ☐ |
| `r.210.2500` | 210 | 2500 | 29" | 26" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.210.3000` | 210 | 3000 | 26" | 23" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.220.1500` | 220 | 1500 | 40" | 35" | 18" | ☐ | ☐ | ☐ | ☐ |
| `r.220.2000` | 220 | 2000 | 34" | 30" | 15" | ☐ | ☐ | ☐ | ☐ |
| `r.220.2500` | 220 | 2500 | 30" | 26" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.220.3000` | 220 | 3000 | 27" | 24" | 11" | ☐ | ☐ | ☐ | ☐ |
| `r.230.1500` | 230 | 1500 | 41" | 36" | 18" | ☐ | ☐ | ☐ | ☐ |
| `r.230.2000` | 230 | 2000 | 35" | 31" | 15" | ☐ | ☐ | ☐ | ☐ |
| `r.230.2500` | 230 | 2500 | 31" | 27" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.230.3000` | 230 | 3000 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.240.1500` | 240 | 1500 | 42" | 37" | 19" | ☐ | ☐ | ☐ | ☐ |
| `r.240.2000` | 240 | 2000 | 35" | 31" | 15" | ☐ | ☐ | ☐ | ☐ |
| `r.240.2500` | 240 | 2500 | 31" | 28" | 13" | ☐ | ☐ | ☐ | ☐ |
| `r.240.3000` | 240 | 3000 | 28" | 25" | 12" | ☐ | ☐ | ☐ | ☐ |
| `r.250.1500` | 250 | 1500 | 43" | 38" | 19" | ☐ | ☐ | ☐ | ☐ |
| `r.250.2000` | 250 | 2000 | 36" | 32" | 16" | ☐ | ☐ | ☐ | ☐ |
| `r.250.2500` | 250 | 2500 | 32" | 28" | 14" | ☐ | ☐ | ☐ | ☐ |
| `r.250.3000` | 250 | 3000 | 29" | 26" | 12" | ☐ | ☐ | ☐ | ☐ |

## Table-level encoding

| Item | Where read | What the pack encodes | Verbatim / right | Classification | OK |
|---|---|---|---|---|---|
| Designation, title, page | p. B5 | `"table": "B3"`, `"title": "Footing Sizes Based on Tributary Area for Various Soil Capacities."` (superscript dropped), `"location": "p. B5"` | ☐ | — | ☐ |
| Kind | p. B5 | `"kind": "deck-footing"`, three outputs `round`, `square`, `thickness` (inches as printed) | ☐ | design §3.4 ☐ | ☐ |
| Area column | p. B5, "Tributary Area² (sq. ft.)", rows 10 … 250 | `tributaryArea`, `sqft`, `upper-bound`, domain max 250; min 1 sq ft is napkin's | ☐ | ☐ | ☐ |
| Soil column | p. B5, "Soil Bearing Capacity" 1500, 2000, 2500, 3000 psf | `soilBearing`, psf, `lower-bound`, domain 1500 … 3000: a value between two columns reads the lower; below 1500 psf is Out of scope; above 3000 reads 3000 | ☐ | ☐ | ☐ |
| Note 1 | p. B5 (on the title's "Footing Sizes") | "Assumes 40 psf live load, 10 psf dead load, 150 pcf concrete and 2,500 psi compressive strength of concrete. Coordinate footing thickness with post base and anchor requirements." | ☐ | not-encoded, table ☐ | ☐ |
| Note 2 | p. B5 (on the Tributary Area heading) | "Tributary area shall be multiplied by 1.25 at center posts with beams not spliced (continuous)." | ☐ | **the table's `centerPostFactor`**: `multiply` `"5/4"` (1.25 exact), `location` "Table B3 note 2, p. B5 (its superscript on the Tributary Area heading)"; not also a footnote; applied to a centre post under a continuous beam before the lookup, and said ☐ | ☐ |
| Offered as an alternative to Table 4 | p. B2, "Post and Footings Size" | used instead of Table 4 (1,500 psf only; design note decision 2); Table 4 not transcribed | ☐ | — | ☐ |
| Soil value typed, never defaulted | #42; p. 11 | the site's soil bearing value, typed in Project → Adopted code and site; Input missing until typed | ☐ | — | ☐ |

## How napkin asks the post and footing tables (derivation)

| Item | Where read | What napkin does | Reviewer: what the page shows | OK |
|---|---|---|---|---|
| Tributary area equations | p. B1, Eq. B-1 (centre post) and Eq. B-2 (corner post) | A = (½J_L + J_O)(B_L) for a middle post, (½J_L + J_O)(½B_L + B_O) for an end post, B_O = 0 (napkin's beam has no overhang; p. B2: zero "if … no overhang exists") | ______ | ☐ |
| B_L | p. B2, "Beam Span Length, B_L"; Figure B3 | to post centrelines, or "to the outside edges of the deck, if there are no overhangs"; the greater of two unequal adjacent spans. napkin: the end span, the next post's centreline to the deck's edge — the middle post's greater span and the end post's only one; the whole width with two posts | ______ | ☐ |
| J_L and J_O | p. B1, "Joist Length, J_L", "Joist Overhang Length, J_O"; Figure B2 | J_L from the ledger face to the rim's outside face (no cantilever) or to the beam's centre (with one); J_O from the beam's centre to the deck's edge, zero without a cantilever | ______ | ☐ |
| Which post is which | p. B1, Figure B1 ("Corner Tributary Area", "Center Tributary Area") | napkin's end posts are corner posts (Table B1), its middle posts centre posts (Table B2); both lines are checked, since B1's 4x4 heights are far lower than B2's | ______ | ☐ |
| Post height | p. 10, POST REQUIREMENTS: "measured from grade or top of foundation, whichever is highest, to the underside of the beam" | the frame's post length, the deck's height less decking, joist and beam, from grade (the larger of the two, so conservative) | ______ | ☐ |
| Continuous beam | Table B2 note 4, Table B3 note 2 ("beams not spliced (continuous)") | napkin's beam is one piece the deck's width long, so continuous over every middle post: the factor applies (the larger area) | ______ | ☐ |
| 4x4 posts | p. 10 ("All deck post sizes shall be 6x6 (nominal) or larger"); Tables B1/B2 print 4x4 columns with note 3 | transcribed as printed (design note risk 4): a 4x4 answers from its column; p. 10's sentence is not encoded (Marc's call) | ______ | ☐ |

## The guide manifest and the pack

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| `layer.json` `notes` | — (napkin's own) | names Tables B1–B3 (#42 slice B3) and their three checklists; the caveats, scope limits, notes, species and sources are unchanged | ☐ | ☐ |
| Scope limits apply to the post and footing tables | p. 2 items 2, 8, 9; Table B3 note 1 | the guide's `s.loads`, `s.snow`, `s.shape` are tried before every lookup (the golden file has a case for each) | ☐ | ☐ |
| CT pack revision 4 | — (napkin's own) | `revision: 4`, its `notes` say what revision 4 adds, `review.status: "unreviewed"` | ☐ | ☐ |

Read and deliberately not encoded in this slice: Table 4 (p. 12; decision 2), p. 10's diagonal bracing and
post-to-beam details, p. 11's footing and frost text (napkin's frost line is its own comparison), Appendix C.

## Golden file spot-checks (`packs/golden/us-ct-2022/dca6-table-b3.golden.json`)

100 hand row cases (one per row) plus the worked example, the three scope limits, the inputs asked for and the
column refusals, and the generator's boundary pairs. The reviewer's spot-checks by eye: ______

| Case (location) | Inputs | Expect | Page | OK |
|---|---|---|---|---|
| worked example's middle post, `r.40.2000` | 29.6 sq ft, centre post, continuous beam (× 1.25 = 37.0), 2000 psf | sized 14" round, 13" square, 6" thick | | ☐ |
| the same post on a spliced beam, and as a corner post, `r.30.2000` | 29.6 sq ft, not multiplied | sized 12", 11", 6" | | ☐ |
| the design note's sample, `r.40.1500` | 40 sq ft, 1500 psf | sized 16", 15", 6" | | ☐ |
| 1499 psf; no soil value; 251 sq ft; 201 sq ft × 1.25 | — | outOfScope column soilBearing / inputMissing soilBearing / outOfScope column tributaryArea (both) | | ☐ |
| `s.loads`, `s.snow`, `s.shape`; no supports; no deck length | — | outOfScope each limit; inputMissing supports / deckLength | | ☐ |

## Remarks

______

## Sign-off

Rows checked: __ of 100. Table-level items: __. Derivation items: __ of 7. Manifest items: __ of 3.
Golden spot-checks: __.
Discrepancies found (each a fix and re-review, not a comment): ______

Sign-off: ______ (name/model), ______ (date)
