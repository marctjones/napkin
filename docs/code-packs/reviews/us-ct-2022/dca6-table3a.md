# Review: us-ct-2022 / DCA 6-2015 Table 3A (dimension-lumber deck beam spans, p. 6)

**Status: NOT REVIEWED.** This checklist was prepared by the transcriber for the independent reviewer
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3), in the shape of the completed Table 2 review
(`dca6-table2.md`). Every verdict box below is empty on purpose: the reviewer reads the page first, writes
the reading down, then fills the boxes (✓ the pack is what the page prints, ✗ it differs — a fix and
re-review, not a comment, ? unreadable) and signs at the bottom. Until then every deck line says UNREVIEWED.

Reviewed by: ______  Date: ______  Source: `awc-dca6-2015` sha256 ______ (must equal the line below)

Pack data hash at the reviewed commit:
`shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-3a.json packs/golden/us-ct-2022/dca6-table-3a.golden.json packs/packs/us-ct-2022/pack.json`
- ______ `packs/layers/dca6-2015/layer.json` (changed since the Table 2 review: two scope notes added and its `notes` text, see "The guide manifest" below)
- ______ `packs/layers/dca6-2015/deck/table-3a.json`
- ______ `packs/golden/us-ct-2022/dca6-table-3a.golden.json`
- ______ `packs/packs/us-ct-2022/pack.json`

## Reviewer's method

(The reviewer's own: pages rendered, resolution, how the reading was recorded before the pack was opened,
how it was compared.) ______

## The document

| | |
|---|---|
| Title | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code* (DCA 6), American Wood Council |
| Printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" (cover); printing mark "04-18" (p. 24) |
| URL | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` |
| Retrieved / checked | 2026-09-27 (hash verified by the transcriber before reading) |
| Pages read by the transcriber | p. 3 (JOIST SIZE: the joist span L), p. 5 (BEAM SIZE & ASSEMBLY REQUIREMENTS), p. 6 (Table 3A and its notes), p. 7 (Table 3B, read and not transcribed; Figure 3), p. B2 (Beam Span Length B_L; Figure B3) |
| How read (transcriber) | Table 3A from the page rendered at 300 dpi (`pdftoppm -r 300`), in two crops, typed into the pack; the golden file's per-row expectations were written separately from `pdftotext -layout` of p. 6; the golden run agrees on all 168 rows. Figure 3 read again at 600 dpi for where its dimension arrows end. |

Transcribed by: Claude Opus 5.5 (#41 slice B2), 2026-09-27. Files: `packs/layers/dca6-2015/deck/table-3a.json`,
`packs/layers/dca6-2015/layer.json` (scope notes `n.beam-span`, `n.beam-sides`), `packs/packs/us-ct-2022/pack.json`
(revision 3), golden file `packs/golden/us-ct-2022/dca6-table-3a.golden.json`.

Beware: DCA 6 prints **two** tables designated "Table 3A": this one, beam spans, on **p. 6**, and joist hanger
capacity on p. 9. Only p. 6 is transcribed.

## Rows (Table 3A, p. 6): 168 rows

Each row is one species group × printed size × joist-span column ("Joist Spans (L) Less Than or Equal to:
6' … 18'"); its span is the L_B cell. The first group is **Southern Pine** (8 printed sizes: 2-2x6 … 2-2x12,
3-2x6 … 3-2x12); the second is the heading **Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western
Cedars, Ponderosa Pine, Red Pine** ("DF-L … Red Pine" below; 12 printed sizes). A cell printed with two
sizes ("3x8 or 2-2x8") is **two rows** citing the same cell, one per size. "Member" is napkin's name for the
printed size: "2-2x8" is `(2) 2x8`, "3-2x8" `(3) 2x8`, "3x8" `(1) 3x8`, "4x8" `(1) 4x8` — check the mapping
as well as the value. "Golden case present" means the golden file has a hand-authored case for the row
whose expected `allowed` is the row's L_B (the runner enforces the case; check the value in it is the
page's).

| RowId | Group | Printed size (p. 6) | Member | Column | Pack: L_B | Pack: notes | Inputs match | L_B matches | Notes match | Golden case present | OK |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `r.sp.2-2x6.6` | SP | 2-2x6 | `(2) 2x6` | ≤ 6' | 6'-8" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.8` | SP | 2-2x6 | `(2) 2x6` | ≤ 8' | 5'-8" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.10` | SP | 2-2x6 | `(2) 2x6` | ≤ 10' | 5'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.12` | SP | 2-2x6 | `(2) 2x6` | ≤ 12' | 4'-7" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.14` | SP | 2-2x6 | `(2) 2x6` | ≤ 14' | 4'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.16` | SP | 2-2x6 | `(2) 2x6` | ≤ 16' | 4'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x6.18` | SP | 2-2x6 | `(2) 2x6` | ≤ 18' | 3'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.6` | SP | 2-2x8 | `(2) 2x8` | ≤ 6' | 8'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.8` | SP | 2-2x8 | `(2) 2x8` | ≤ 8' | 7'-4" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.10` | SP | 2-2x8 | `(2) 2x8` | ≤ 10' | 6'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.12` | SP | 2-2x8 | `(2) 2x8` | ≤ 12' | 5'-11" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.14` | SP | 2-2x8 | `(2) 2x8` | ≤ 14' | 5'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.16` | SP | 2-2x8 | `(2) 2x8` | ≤ 16' | 5'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x8.18` | SP | 2-2x8 | `(2) 2x8` | ≤ 18' | 4'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.6` | SP | 2-2x10 | `(2) 2x10` | ≤ 6' | 10'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.8` | SP | 2-2x10 | `(2) 2x10` | ≤ 8' | 8'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.10` | SP | 2-2x10 | `(2) 2x10` | ≤ 10' | 7'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.12` | SP | 2-2x10 | `(2) 2x10` | ≤ 12' | 7'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.14` | SP | 2-2x10 | `(2) 2x10` | ≤ 14' | 6'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.16` | SP | 2-2x10 | `(2) 2x10` | ≤ 16' | 6'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x10.18` | SP | 2-2x10 | `(2) 2x10` | ≤ 18' | 5'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.6` | SP | 2-2x12 | `(2) 2x12` | ≤ 6' | 11'-11" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.8` | SP | 2-2x12 | `(2) 2x12` | ≤ 8' | 10'-4" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.10` | SP | 2-2x12 | `(2) 2x12` | ≤ 10' | 9'-2" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.12` | SP | 2-2x12 | `(2) 2x12` | ≤ 12' | 8'-4" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.14` | SP | 2-2x12 | `(2) 2x12` | ≤ 14' | 7'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.16` | SP | 2-2x12 | `(2) 2x12` | ≤ 16' | 7'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.2-2x12.18` | SP | 2-2x12 | `(2) 2x12` | ≤ 18' | 6'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.6` | SP | 3-2x6 | `(3) 2x6` | ≤ 6' | 7'-11" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.8` | SP | 3-2x6 | `(3) 2x6` | ≤ 8' | 7'-2" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.10` | SP | 3-2x6 | `(3) 2x6` | ≤ 10' | 6'-5" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.12` | SP | 3-2x6 | `(3) 2x6` | ≤ 12' | 5'-10" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.14` | SP | 3-2x6 | `(3) 2x6` | ≤ 14' | 5'-5" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.16` | SP | 3-2x6 | `(3) 2x6` | ≤ 16' | 5'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x6.18` | SP | 3-2x6 | `(3) 2x6` | ≤ 18' | 4'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.6` | SP | 3-2x8 | `(3) 2x8` | ≤ 6' | 10'-7" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.8` | SP | 3-2x8 | `(3) 2x8` | ≤ 8' | 9'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.10` | SP | 3-2x8 | `(3) 2x8` | ≤ 10' | 8'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.12` | SP | 3-2x8 | `(3) 2x8` | ≤ 12' | 7'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.14` | SP | 3-2x8 | `(3) 2x8` | ≤ 14' | 6'-11" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.16` | SP | 3-2x8 | `(3) 2x8` | ≤ 16' | 6'-5" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x8.18` | SP | 3-2x8 | `(3) 2x8` | ≤ 18' | 6'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.6` | SP | 3-2x10 | `(3) 2x10` | ≤ 6' | 12'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.8` | SP | 3-2x10 | `(3) 2x10` | ≤ 8' | 11'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.10` | SP | 3-2x10 | `(3) 2x10` | ≤ 10' | 9'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.12` | SP | 3-2x10 | `(3) 2x10` | ≤ 12' | 8'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.14` | SP | 3-2x10 | `(3) 2x10` | ≤ 14' | 8'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.16` | SP | 3-2x10 | `(3) 2x10` | ≤ 16' | 7'-8" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x10.18` | SP | 3-2x10 | `(3) 2x10` | ≤ 18' | 7'-3" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.6` | SP | 3-2x12 | `(3) 2x12` | ≤ 6' | 15'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.8` | SP | 3-2x12 | `(3) 2x12` | ≤ 8' | 13'-0" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.10` | SP | 3-2x12 | `(3) 2x12` | ≤ 10' | 11'-7" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.12` | SP | 3-2x12 | `(3) 2x12` | ≤ 12' | 10'-6" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.14` | SP | 3-2x12 | `(3) 2x12` | ≤ 14' | 9'-9" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.16` | SP | 3-2x12 | `(3) 2x12` | ≤ 16' | 9'-1" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.3-2x12.18` | SP | 3-2x12 | `(3) 2x12` | ≤ 18' | 8'-7" | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.6` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 6' | 5'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.8` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 8' | 4'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.10` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 10' | 3'-11" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.12` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 12' | 3'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.14` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 14' | 3'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.16` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 16' | 2'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x6.18` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 18' | 2'-6" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.6` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 6' | 5'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.8` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 8' | 4'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.10` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 10' | 3'-11" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.12` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 12' | 3'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.14` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 14' | 3'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.16` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 16' | 2'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x6.18` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 18' | 2'-6" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.6` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 6' | 6'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.8` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 8' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.10` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 10' | 5'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.12` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 12' | 4'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.14` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 14' | 4'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.16` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 16' | 3'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x8.18` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 18' | 3'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.6` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 6' | 6'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.8` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 8' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.10` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 10' | 5'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.12` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 12' | 4'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.14` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 14' | 4'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.16` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 16' | 3'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x8.18` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 18' | 3'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.6` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 6' | 8'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.8` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 8' | 7'-0" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.10` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 10' | 6'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.12` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 12' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.14` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 14' | 5'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.16` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 16' | 4'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x10.18` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 18' | 4'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.6` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 6' | 8'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.8` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 8' | 7'-0" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.10` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 10' | 6'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.12` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 12' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.14` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 14' | 5'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.16` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 16' | 4'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x10.18` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 18' | 4'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.6` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 6' | 9'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.8` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 8' | 8'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.10` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 10' | 7'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.12` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 12' | 6'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.14` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 14' | 6'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.16` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 16' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3x12.18` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 18' | 5'-4" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.6` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 6' | 9'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.8` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 8' | 8'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.10` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 10' | 7'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.12` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 12' | 6'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.14` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 14' | 6'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.16` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 16' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.2-2x12.18` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 18' | 5'-4" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.6` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 6' | 6'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.8` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 8' | 5'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.10` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 10' | 4'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.12` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 12' | 4'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.14` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 14' | 3'-11" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.16` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 16' | 3'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x6.18` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 18' | 3'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.6` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 6' | 8'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.8` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 8' | 7'-0" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.10` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 10' | 6'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.12` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 12' | 5'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.14` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 14' | 5'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.16` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 16' | 4'-11" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x8.18` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 18' | 4'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.6` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 6' | 9'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.8` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 8' | 8'-4" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.10` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 10' | 7'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.12` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 12' | 6'-9" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.14` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 14' | 6'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.16` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 16' | 5'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x10.18` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 18' | 5'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.6` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 6' | 11'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.8` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 8' | 9'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.10` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 10' | 8'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.12` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 12' | 7'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.14` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 14' | 7'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.16` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 16' | 6'-9" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.4x12.18` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 18' | 6'-4" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.6` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 6' | 7'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.8` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 8' | 6'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.10` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 10' | 5'-9" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.12` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 12' | 5'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.14` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 14' | 4'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.16` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 16' | 4'-6" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x6.18` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 18' | 4'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.6` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 6' | 9'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.8` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 8' | 8'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.10` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 10' | 7'-4" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.12` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 12' | 6'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.14` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 14' | 6'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.16` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 16' | 5'-9" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x8.18` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 18' | 5'-5" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.6` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 6' | 11'-9" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.8` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 8' | 10'-2" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.10` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 10' | 9'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.12` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 12' | 8'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.14` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 14' | 7'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.16` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 16' | 7'-1" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x10.18` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 18' | 6'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.6` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 6' | 13'-8" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.8` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 8' | 11'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.10` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 10' | 10'-6" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.12` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 12' | 9'-7" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.14` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 14' | 8'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.16` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 16' | 8'-3" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl-rw.3-2x12.18` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 18' | 7'-10" | 2, 3 | ☐ | ☐ | ☐ | ☐ | ☐ |

## Table-level encoding

| Item | Where read | What the pack encodes | Matches the page | Classification right | OK |
|---|---|---|---|---|---|
| Designation and title | p. 6, "Table 3A. Dimension Lumber Deck Beam Spans (L_B)¹ Supporting a Single Span of Joists with or without Overhangs." | `table: "3A"`, `title: "Dimension Lumber Deck Beam Spans (LB) Supporting a Single Span of Joists with or without Overhangs."` (subscript B flattened, superscript 1 dropped), `location: "p. 6"` | ☐ | — | ☐ |
| Not the p. 9 Table 3A | p. 9 | nothing from p. 9 is in the file | ☐ | — | ☐ |
| Species column values = the two printed row headings | p. 6, Species column | "Southern Pine"; "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine" (the page breaks the second across nine lines) | ☐ | — | ☐ |
| Species group: Southern Pine | p. 6, first row heading | Southern Pine | ☐ | — | ☐ |
| Species group: the second heading | p. 6, second row heading (note 2 on Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir; note 3 on Ponderosa Pine, Red Pine) | the seven species, each in the guide's list; all eight of the guide's species placed once | ☐ | — | ☐ |
| Size column values and members | p. 6, Size⁴ column | 16 members: `(2) 2x6`, `(2) 2x8`, `(2) 2x10`, `(2) 2x12`, `(3) 2x6` … `(3) 2x12`, `(1) 3x6` … `(1) 3x12`, `(1) 4x6` … `(1) 4x12`; Southern Pine prints no 3x or 4x row, so a Southern Pine `(1) 4x8` is out of scope (column `member`) | ☐ | — | ☐ |
| Two-name cells | p. 6, "3x6 or 2-2x6", "3x8 or 2-2x8", "3x10 or 2-2x10", "3x12 or 2-2x12" | each is two rows, `r.dfl-rw.3xN.*` and `r.dfl-rw.2-2xN.*`, with the same L_B and a `location` naming the same cell ("(its 3x8)", "(its 2-2x8)") | ☐ | design §3.2 ☐ | ☐ |
| Joist-span column | p. 6, "Joist Spans (L) Less Than or Equal to:" 6', 8', 10', 12', 14', 16', 18' | `joistSpan`, length, `upper-bound`, domain max `18ft 0in`; min `1/1024in` is napkin's (the page prints no lower bound) | ☐ | ☐ | ☐ |
| What the joist-span input is | p. 3, JOIST SIZE (L measured face of support to face of support); Figure 1A, p. 4 | napkin asks with the joists' span L, the same quantity as Table 2's check (`DeckFraming.JoistSpan`) | ☐ | ☐ | ☐ |
| Note 1 | p. 6 (on the title's L_B) | "Assumes 40 psf live load, 10 psf dead load, L/360 simple span beam deflection limit, cantilever length/180 deflection limit, No. 2 grade, and wet service conditions." | ☐ | not-encoded, table ☐ | ☐ |
| Note 2 | p. 6 (on Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir in the second heading) | "Incising assumed for Douglas Fir-Larch, Hem-Fir, and Spruce-Pine-Fir." | ☐ | not-encoded, on the second group's 112 rows (the row is the group; the superscript sits on three of its seven species) ☐ | ☐ |
| Note 3 | p. 6 (on Ponderosa Pine and Red Pine in the second heading) | "Design values based on northern species with no incising assumed." | ☐ | not-encoded, on the second group's 112 rows (the superscript sits on two of its seven species) ☐ | ☐ |
| Note 4 | p. 6 (on the Size heading) | "Beam depth must be equal to or greater than joist depth if joist hangers are used (see Figure 6, Option 3)." | ☐ | not-encoded, table ☐ | ☐ |
| Table 3B not transcribed | p. 7 | no glulam rows (napkin's beam is plies of dimension lumber; design §3.2) | ☐ | — | ☐ |

## How napkin measures the beam span it asks about (derivation, Decision 8)

| Item | Where read | What napkin does | Reviewer: what the page shows | OK |
|---|---|---|---|---|
| Beam span L_B | Figure 3, p. 7 ("beam span (L_B): See Table 3" between posts, "L_B/4 max. overhang" beyond the end posts); BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5 ("can extend past the post face up to LB/4"); Appendix B, p. B2 and Figure B3 ("The beam span is measured from either centerline of post to centerline of post, if there are overhangs, or to the outside edges of the deck, if there are no overhangs") | `DeckFraming.BeamSpan` = (deck width − post width) ÷ (posts − 1): post centreline to post centreline (design note Decision 8, signed off 2026-09-27); the line says "post centre to post centre" | ______ | ☐ |
| **Transcriber's finding, for the reviewer to confirm or refute** | Figure 3, p. 7, at 600 dpi | The arrowheads of Figure 3's "beam span (L_B)" and "L_B/4 max. overhang" dimensions end at the **faces** of the posts (each post's two arrowheads point outward from its middle to its two faces), which with p. 5's "past the post face" reads as a face-to-face L_B in the main body; only Appendix B, written for posts' and footings' tributary areas, measures centre to centre. napkin implements Decision 8 as signed off (the longer, conservative measure) and flags this for Marc; no verdict is asked on the decision, only on what the figure shows. | ______ | ☐ |
| No beam overhang check | Figure 3; p. 5 | napkin's beam ends at its end posts (no overhang), so L_B/4 is not checked; `n.beam-span` shows the sentence | ☐ | ☐ |

## The guide manifest additions (`packs/layers/dca6-2015/layer.json`)

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| Note `n.beam-span` | BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5; Figure 3, p. 7 | "Deck beam spans shall be in accordance with Table 3 and can extend past the post face up to LB/4 as shown in Figure 3." (subscript B flattened) | ☐ | ☐ |
| Note `n.beam-sides` | BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5 | "Joists shall not frame in from opposite sides of the same beam. See JOIST-TO-BEAM CONNECTION details, Figure 6." | ☐ | ☐ |
| Scope limits apply to the beam | p. 2 items 2, 8, 9; Table 3A note 1, p. 6 | the guide's `s.loads`, `s.snow`, `s.shape` are tried before every Table 3A lookup (the golden file has a case for each); `s.loads`'s text quotes Table 2 note 1, whose loads Table 3A note 1 repeats | ☐ | ☐ |
| CT pack revision 3 | — (napkin's own) | `revision: 3`, its `notes` say what revision 3 adds, `review.status: "unreviewed"` | ☐ | ☐ |

Read and deliberately not encoded in this slice: Table 3B (p. 7), Table 3A joist hanger capacity (p. 9),
Figure 4's beam assembly fastening (p. 5), Figure 6's joist-to-beam options (p. 9), Table 4 and the
Appendix B tables (slice B3), Table 5 (B4), the guard and stair figures (B5).

## Golden file spot-checks (`packs/golden/us-ct-2022/dca6-table-3a.golden.json`)

168 hand row cases (one per row); 13 more hand cases: the worked example, a two-name cell's two rows, the
three scope limits, the three inputs asked for and the column refusals, and the generator's 676 boundary
pairs. The reviewer's spot-checks by eye: ______

| Case (location) | Inputs | Expect | Page | OK |
|---|---|---|---|---|
| worked example, `r.sp.2-2x10.10` | Southern Pine (2) 2x10, joists 9'-9", span 5'-10 1/4" | passes, allowed 7'-9" | | ☐ |
| two-name cell, `r.dfl-rw.3x8.10` and `r.dfl-rw.2-2x8.10` | Hem-Fir, joists 10'-0", span 5'-1" | passes, allowed 5'-1" (both) | | ☐ |
| `s.loads`, `s.snow`, `s.shape` | porch-roof; 45 psf; length 16'-0 1/16" on width 16'-0" | outOfScope, each limit | | ☐ |
| Southern Pine `(1) 4x8`; `(2) 2x14`; joists 18'-0 1/16" | — | outOfScope column member / member / joistSpan | | ☐ |

## Remarks

______

## Sign-off

Rows checked: __ of 168. Table-level items: __ of 14. Derivation items: __ of 3. Manifest items: __ of 4.
Golden spot-checks: __.
Discrepancies found (each a fix and re-review, not a comment): ______

Sign-off: ______ (name/model), ______ (date)
