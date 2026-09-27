# Review: us-ct-2022 / DCA 6-2015 Table B1 (post heights, corner posts, p. B3)

**Status: NOT REVIEWED.** This checklist was prepared by the transcriber for the independent reviewer
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3), in the shape of the completed Table 2 and Table 3A
reviews (`dca6-table2.md`, `dca6-table3a.md`). Every verdict box below is empty on purpose: the reviewer reads the
page first, writes the reading down, then fills the boxes (✓ the pack is what the page prints, ✗ it differs — a fix
and re-review, not a comment, ? unreadable) and signs at the bottom. Until then every deck line says UNREVIEWED.

Reviewed by: ______  Date: ______  Source: `awc-dca6-2015` sha256 ______ (must equal the line below)

Pack data hash at the reviewed commit:
`shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-b1.json packs/golden/us-ct-2022/dca6-table-b1.golden.json packs/packs/us-ct-2022/pack.json`
- ______ `packs/layers/dca6-2015/layer.json` (its `notes` text names Tables B1–B3 and their checklists and, since #42's 6x6 fix, the scope note `n.post-size` is added; nothing else changed since the Table 3A review)
- ______ `packs/layers/dca6-2015/deck/table-b1.json`
- ______ `packs/golden/us-ct-2022/dca6-table-b1.golden.json`
- ______ `packs/packs/us-ct-2022/pack.json` (`revision` 3 → 4 → 5, a sentence added to `notes` for each, nothing else)

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
| Pages read by the transcriber | pp. B1–B2 (Tributary Area, Eq. B-1 and B-2, J_L, J_O, B_L, B_O, Figures B1–B3, "Post and Footings Size"), pp. B3–B5 (Tables B1, B2, B3 and their notes), p. 10 (POST REQUIREMENTS: post height measured grade to the beam's underside; the 6x6 minimum), p. 2 (item 3, the 6x6 minimum again), p. B1's opening paragraph (what Appendix B is an alternative to), pp. C2 and C7 (commentary on post size, read for context only), p. 12 (Table 4, read and not used, decision 2) |
| How read (transcriber) | Each table from its page rendered at 500 dpi (`pdftoppm -r 500`), in four crops (the 6x6 and 4x4 halves, rows 10–130 and 140–250; for B3 the 1500/2000 and 2500/3000 psf halves), typed into a reading file and from it into the pack; the golden file's per-row expectations were generated separately from `pdftotext -layout` of pp. B1–B5; the two readings were compared cell by cell before either was committed (800 cells, 0 differences) and the golden run agrees on every row. |

Transcribed by: Claude Opus 5.5 (#42 slice B3), 2026-09-27. Files: `packs/layers/dca6-2015/deck/table-b1.json`, `packs/layers/dca6-2015/layer.json` (its `notes`), `packs/packs/us-ct-2022/pack.json` (revision 4; revision 5 for the 6x6 fix), golden file `packs/golden/us-ct-2022/dca6-table-b1.golden.json`.

## Rows (Table B1, p. B3): 250 rows

Each row is one printed cell: a Tributary Area (sq. ft.) row (10, 20 … 250, an upper-bound band: the row answers
every area above the previous row's and at most its own) × the **6x6** or **4x4** Post Height (ft.) column × one of
the five species headings (SP = Southern Pine; DF-L = Douglas Fir-Larch; HF, WC = Hem-Fir, Western Cedars; RW =
Redwood; PP, RP, SPF = Ponderosa Pine, Red Pine, SPF). "Pack: height" is the post height in whole feet as printed,
or **NP** where the cell prints NP (`"notPermitted": true`; the page prints 13 such cells). "Pack: notes" lists the
footnotes the row carries beside the table-wide ones. "Golden case present" means the golden file has a hand-authored
case for the row whose expectation is the row's height (or NP); the runner enforces the case — check the value in it
is the page's. Since #42's 6x6 fix a **4x4** row's case expects the table's limit `t.post-size` instead (no lookup
reaches a 4x4 row); its `location` still records the printed cell, so check the height there against the page, since
the run no longer does.

| RowId | Area ≤ (sq ft) | Post | Species heading | Pack: height | Pack: notes | Inputs match | Height matches | Notes match | Golden case present | OK |
|---|---|---|---|---|---|---|---|---|---|---|
| `r.sp.6x6.10` | 10 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.10` | 10 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.10` | 10 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.10` | 10 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.10` | 10 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.10` | 10 | 4x4 | SP | 9' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.10` | 10 | 4x4 | DF-L | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.10` | 10 | 4x4 | HF, WC | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.10` | 10 | 4x4 | RW | 11' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.10` | 10 | 4x4 | PP, RP, SPF | 8' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.20` | 20 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.20` | 20 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.20` | 20 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.20` | 20 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.20` | 20 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.20` | 20 | 4x4 | SP | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.20` | 20 | 4x4 | DF-L | 4' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.20` | 20 | 4x4 | HF, WC | 5' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.20` | 20 | 4x4 | RW | 7' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.20` | 20 | 4x4 | PP, RP, SPF | 5' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.30` | 30 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.30` | 30 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.30` | 30 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.30` | 30 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.30` | 30 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.30` | 30 | 4x4 | SP | 5' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.30` | 30 | 4x4 | DF-L | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.30` | 30 | 4x4 | HF, WC | 4' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.30` | 30 | 4x4 | RW | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.30` | 30 | 4x4 | PP, RP, SPF | 4' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.40` | 40 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.40` | 40 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.40` | 40 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.40` | 40 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.40` | 40 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.40` | 40 | 4x4 | SP | 4' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.40` | 40 | 4x4 | DF-L | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.40` | 40 | 4x4 | HF, WC | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.40` | 40 | 4x4 | RW | 5' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.40` | 40 | 4x4 | PP, RP, SPF | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.50` | 50 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.50` | 50 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.50` | 50 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.50` | 50 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.50` | 50 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.50` | 50 | 4x4 | SP | 4' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.50` | 50 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.50` | 50 | 4x4 | HF, WC | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.50` | 50 | 4x4 | RW | 4' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.50` | 50 | 4x4 | PP, RP, SPF | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.60` | 60 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.60` | 60 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.60` | 60 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.60` | 60 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.60` | 60 | 6x6 | PP, RP, SPF | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.60` | 60 | 4x4 | SP | 3' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.60` | 60 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.60` | 60 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.60` | 60 | 4x4 | RW | 4' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.60` | 60 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.70` | 70 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.70` | 70 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.70` | 70 | 6x6 | HF, WC | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.70` | 70 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.70` | 70 | 6x6 | PP, RP, SPF | 13' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.70` | 70 | 4x4 | SP | 3' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.70` | 70 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.70` | 70 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.70` | 70 | 4x4 | RW | 3' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.70` | 70 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.80` | 80 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.80` | 80 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.80` | 80 | 6x6 | HF, WC | 13' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.80` | 80 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.80` | 80 | 6x6 | PP, RP, SPF | 12' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.80` | 80 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.80` | 80 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.80` | 80 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.80` | 80 | 4x4 | RW | 3' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.80` | 80 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.90` | 90 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.90` | 90 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.90` | 90 | 6x6 | HF, WC | 12' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.90` | 90 | 6x6 | RW | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.90` | 90 | 6x6 | PP, RP, SPF | 10' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.90` | 90 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.90` | 90 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.90` | 90 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.90` | 90 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.90` | 90 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.100` | 100 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.100` | 100 | 6x6 | DF-L | 14' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.100` | 100 | 6x6 | HF, WC | 11' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.100` | 100 | 6x6 | RW | 13' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.100` | 100 | 6x6 | PP, RP, SPF | 9' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.100` | 100 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.100` | 100 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.100` | 100 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.100` | 100 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.100` | 100 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.110` | 110 | 6x6 | SP | 14' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.110` | 110 | 6x6 | DF-L | 13' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.110` | 110 | 6x6 | HF, WC | 10' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.110` | 110 | 6x6 | RW | 12' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.110` | 110 | 6x6 | PP, RP, SPF | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.110` | 110 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.110` | 110 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.110` | 110 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.110` | 110 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.110` | 110 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.120` | 120 | 6x6 | SP | 13' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.120` | 120 | 6x6 | DF-L | 12' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.120` | 120 | 6x6 | HF, WC | 10' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.120` | 120 | 6x6 | RW | 12' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.120` | 120 | 6x6 | PP, RP, SPF | 6' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.120` | 120 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.120` | 120 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.120` | 120 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.120` | 120 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.120` | 120 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.130` | 130 | 6x6 | SP | 13' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.130` | 130 | 6x6 | DF-L | 11' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.130` | 130 | 6x6 | HF, WC | 9' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.130` | 130 | 6x6 | RW | 11' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.130` | 130 | 6x6 | PP, RP, SPF | 4' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.130` | 130 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.130` | 130 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.130` | 130 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.130` | 130 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.130` | 130 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.140` | 140 | 6x6 | SP | 12' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.140` | 140 | 6x6 | DF-L | 11' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.140` | 140 | 6x6 | HF, WC | 8' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.140` | 140 | 6x6 | RW | 10' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.140` | 140 | 6x6 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.140` | 140 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.140` | 140 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.140` | 140 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.140` | 140 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.140` | 140 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.150` | 150 | 6x6 | SP | 11' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.150` | 150 | 6x6 | DF-L | 10' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.150` | 150 | 6x6 | HF, WC | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.150` | 150 | 6x6 | RW | 10' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.150` | 150 | 6x6 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.150` | 150 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.150` | 150 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.150` | 150 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.150` | 150 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.150` | 150 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.160` | 160 | 6x6 | SP | 11' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.160` | 160 | 6x6 | DF-L | 9' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.160` | 160 | 6x6 | HF, WC | 6' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.160` | 160 | 6x6 | RW | 9' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.160` | 160 | 6x6 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.160` | 160 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.160` | 160 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.160` | 160 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.160` | 160 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.160` | 160 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.170` | 170 | 6x6 | SP | 10' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.170` | 170 | 6x6 | DF-L | 9' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.170` | 170 | 6x6 | HF, WC | 5' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.170` | 170 | 6x6 | RW | 9' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.170` | 170 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.170` | 170 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.170` | 170 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.170` | 170 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.170` | 170 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.170` | 170 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.180` | 180 | 6x6 | SP | 10' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.180` | 180 | 6x6 | DF-L | 8' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.180` | 180 | 6x6 | HF, WC | 3' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.180` | 180 | 6x6 | RW | 9' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.180` | 180 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.180` | 180 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.180` | 180 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.180` | 180 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.180` | 180 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.180` | 180 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.190` | 190 | 6x6 | SP | 10' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.190` | 190 | 6x6 | DF-L | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.190` | 190 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.190` | 190 | 6x6 | RW | 8' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.190` | 190 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.190` | 190 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.190` | 190 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.190` | 190 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.190` | 190 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.190` | 190 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.200` | 200 | 6x6 | SP | 8' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.200` | 200 | 6x6 | DF-L | 7' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.200` | 200 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.200` | 200 | 6x6 | RW | 8' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.200` | 200 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.200` | 200 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.200` | 200 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.200` | 200 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.200` | 200 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.200` | 200 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.210` | 210 | 6x6 | SP | 8' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.210` | 210 | 6x6 | DF-L | 6' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.210` | 210 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.210` | 210 | 6x6 | RW | 7' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.210` | 210 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.210` | 210 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.210` | 210 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.210` | 210 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.210` | 210 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.210` | 210 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.220` | 220 | 6x6 | SP | 7' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.220` | 220 | 6x6 | DF-L | 5' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.220` | 220 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.220` | 220 | 6x6 | RW | 7' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.220` | 220 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.220` | 220 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.220` | 220 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.220` | 220 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.220` | 220 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.220` | 220 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.230` | 230 | 6x6 | SP | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.230` | 230 | 6x6 | DF-L | 4' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.230` | 230 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.230` | 230 | 6x6 | RW | 7' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.230` | 230 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.230` | 230 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.230` | 230 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.230` | 230 | 4x4 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.230` | 230 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.230` | 230 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.240` | 240 | 6x6 | SP | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.240` | 240 | 6x6 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.240` | 240 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.240` | 240 | 6x6 | RW | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.240` | 240 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.240` | 240 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.240` | 240 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.240` | 240 | 4x4 | HF, WC | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.240` | 240 | 4x4 | RW | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.240` | 240 | 4x4 | PP, RP, SPF | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.6x6.250` | 250 | 6x6 | SP | 5' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.6x6.250` | 250 | 6x6 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.6x6.250` | 250 | 6x6 | HF, WC | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.6x6.250` | 250 | 6x6 | RW | 6' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.6x6.250` | 250 | 6x6 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.sp.4x4.250` | 250 | 4x4 | SP | 2' | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.dfl.4x4.250` | 250 | 4x4 | DF-L | 2' | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.hf-wc.4x4.250` | 250 | 4x4 | HF, WC | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.rw.4x4.250` | 250 | 4x4 | RW | NP | — | ☐ | ☐ | ☐ | ☐ | ☐ |
| `r.pp-rp-spf.4x4.250` | 250 | 4x4 | PP, RP, SPF | NP | 2 | ☐ | ☐ | ☐ | ☐ | ☐ |

## Table-level encoding

| Item | Where read | What the pack encodes | Verbatim / right | Classification | OK |
|---|---|---|---|---|---|
| Designation, title, page | p. B3 | `"table": "B1"`, `"title": "Post Heights Based on Tributary Area for Corner Posts."` (superscripts dropped; the notes say where they sit), `"location": "p. B3"` | ☐ | — | ☐ |
| Kind and position | p. B3, the title ("Corner Posts"); p. B1, Figure B1 | `"kind": "deck-post"`, `"position": "corner"`: napkin's end posts ask this table | ☐ | design §3.4, as built ☐ | ☐ |
| Species headings and groups | p. B3, the five headings over each post column (with note 2 on Douglas Fir-Larch, Hem-Fir and SPF) | five groups as printed: "Southern Pine" → Southern Pine; "Douglas Fir-Larch" → Douglas Fir-Larch; "Hem-Fir, Western Cedars" → Hem-Fir, Western Cedars; "Redwood" → Redwood; "Ponderosa Pine, Red Pine, SPF" → Ponderosa Pine, Red Pine, Spruce-Pine-Fir; every one of the guide's eight species placed once | ☐ | "SPF" read as Spruce-Pine-Fir (note 2, on SPF, spells it spruce-pine-fir) ☐ | ☐ |
| Post columns | p. B3, "6x6 Post Height (ft.)", "4x4 Post Height (ft.)" | `post`: `6x6`, `4x4`; heights in whole feet | ☐ | — | ☐ |
| Limit `t.post-size` (#42 fix, 2026-09-27) | p. 10, POST REQUIREMENTS, its first sentence, "All deck post sizes shall be 6x6 (nominal) or larger, and the maximum height shall be in accordance with Table 4 …"; p. 2, item 3 | `"limits"`: `t.post-size`, `when` `member` `notIn` `["6x6", "6x8", "8x8"]` (every name in napkin's materials library that is 6x6 nominal or larger in both dimensions; a deck post's name can come from nowhere else), `"text": "All deck post sizes shall be 6x6 (nominal) or larger"` (the sentence's opening clause, verbatim), `"location": "POST REQUIREMENTS, p. 10"`; tried after the guide's limits and before the lookup, so a 4x4, 4x6, 2x4 … is Out of scope ("Beyond table B1: … (DCA 6-2015 POST REQUIREMENTS, p. 10). Get it engineered.") and never reaches the 4x4 column | ☐ | on the table, as a table's member limit (a guide-level `member` limit would leave every footing Input missing); the table's golden covers it through its 4x4 row cases and a 4x6 case ☐ | ☐ |
| Area column | p. B3, "Tributary Area (sq. ft.)", rows 10 … 250 | `tributaryArea`, `sqft`, `upper-bound`, domain max 250; min 1 sq ft is napkin's (the page prints no lower bound) | ☐ | ☐ | ☐ |
| NP cells | p. B3 | `notPermitted: true` on the 13 rows printing NP; answered Out of scope citing the row, the sentence quoting "NP" without expanding it (the document never defines it) | ☐ | ☐ | ☐ |
| Note 1 | p. B3 (on the title's "Based") | "Assumes No 2. Stress grade and wet service conditions." | ☐ | not-encoded, table ☐ | ☐ |
| Note 2 | p. B3 (on Douglas Fir-Larch, Hem-Fir and SPF in the headings) | "Incising assumed for Douglas fir-larch, hem-fir, and spruce-pine-fir." | ☐ | not-encoded, on the DF-L, HF, WC and PP, RP, SPF rows (150) ☐ | ☐ |
| Note 3 | p. B3 (on the title's "Based") | "Some post heights for 4x4 post sizes show a greater load carrying capacity than 6x6 post sizes since different ASTM Standards are used to develop design values for visually graded dimension lumber vs. visually graded timbers." | ☐ | not-encoded, table ☐ | ☐ |
| No factor | p. B3 | Table B1 prints no note on its Tributary Area heading; `centerPostFactor` absent (a corner table may not declare one) | ☐ | — | ☐ |
| Offered as an alternative to Table 4 | p. B2, "Post and Footings Size" | used instead of Table 4 (design note decision 2); Table 4 not transcribed | ☐ | — | ☐ |

## How napkin asks the post and footing tables (derivation)

| Item | Where read | What napkin does | Reviewer: what the page shows | OK |
|---|---|---|---|---|
| Tributary area equations | p. B1, Eq. B-1 (centre post) and Eq. B-2 (corner post) | A = (½J_L + J_O)(B_L) for a middle post, (½J_L + J_O)(½B_L + B_O) for an end post, B_O = 0 (napkin's beam has no overhang; p. B2: zero "if … no overhang exists") | ______ | ☐ |
| B_L | p. B2, "Beam Span Length, B_L"; Figure B3 | to post centrelines, or "to the outside edges of the deck, if there are no overhangs"; the greater of two unequal adjacent spans. napkin: the end span, the next post's centreline to the deck's edge — the middle post's greater span and the end post's only one; the whole width with two posts | ______ | ☐ |
| J_L and J_O | p. B1, "Joist Length, J_L", "Joist Overhang Length, J_O"; Figure B2 | J_L from the ledger face to the rim's outside face (no cantilever) or to the beam's centre (with one); J_O from the beam's centre to the deck's edge, zero without a cantilever | ______ | ☐ |
| Which post is which | p. B1, Figure B1 ("Corner Tributary Area", "Center Tributary Area") | napkin's end posts are corner posts (Table B1), its middle posts centre posts (Table B2); both lines are checked, since B1's 4x4 heights are far lower than B2's | ______ | ☐ |
| Post height | p. 10, POST REQUIREMENTS: "measured from grade or top of foundation, whichever is highest, to the underside of the beam" | the frame's post length, the deck's height less decking, joist and beam, from grade (the larger of the two, so conservative) | ______ | ☐ |
| Continuous beam | Table B2 note 4, Table B3 note 2 ("beams not spliced (continuous)") | napkin's beam is one piece the deck's width long, so continuous over every middle post: the factor applies (the larger area) | ______ | ☐ |
| 4x4 posts | p. 10 ("All deck post sizes shall be 6x6 (nominal) or larger"); p. 2, item 3 ("Minimum post size is 6x6 nominal"); p. B1 (Appendix B provides "an alternative to the assumptions of Table 4 Post Height for 6x6 and Footings Sizes for all Posts"); p. B2 ("As an alternative to Table 4 of DCA 6, the post height and footing size may be in accordance with Table B1 through B3"); Tables B1/B2 print 4x4 columns with note 3; commentary C2 item 3 and C7 (a 4x4 post as an alternative method a building official approves) | **Changed by #42's fix, 2026-09-27** (was: a 4x4 answered from its column, p. 10 not encoded). Appendix B stands in for Table 4's height and footing assumptions, not for the size minimum, so p. 10 holds under it: Tables B1 and B2 refuse any post under 6x6 nominal (limit `t.post-size`) before their lookup, citing p. 10, and the 4x4 column stays transcribed as printed. The footing is sized either way: Table B3 bands on area and soil, not on the post. Reviewer: confirm this reading of pp. B1–B2 against p. 10 and p. 2 item 3, and that nothing in Appendix B itself permits a 4x4 deck post | ______ | ☐ |

## The guide manifest and the pack

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| `layer.json` `notes` | — (napkin's own) | names Tables B1–B3 (#42 slice B3) and their three checklists, and the 6x6 fix (`n.post-size`, the tables' `t.post-size`); the caveats, scope limits, species and sources are unchanged | ☐ | ☐ |
| Scope note `n.post-size` (#42 fix) | p. 2, MINIMUM REQUIREMENTS & LIMITATIONS item 3 | "Minimum post size is 6x6 nominal and maximum post height shall be in accordance with Table 4.", `location` "MINIMUM REQUIREMENTS & LIMITATIONS item 3, p. 2": shown once in the paragraph at the top of the deck block beside the other scope items, not itself checked (Tables B1 and B2's `t.post-size` is) | ☐ | ☐ |
| Scope limits apply to the post and footing tables | p. 2 items 2, 8, 9; Table B3 note 1 | the guide's `s.loads`, `s.snow`, `s.shape` are tried before every lookup (the golden file has a case for each) | ☐ | ☐ |
| CT pack revision 5 | — (napkin's own) | `revision: 5`, its `notes` say what revisions 4 (the three tables) and 5 (the 6x6 minimum) add, `review.status: "unreviewed"` | ☐ | ☐ |

Read and deliberately not encoded in this slice: p. 10's "8x8 nominal posts can be substituted anywhere in Table 4
but are limited to a maximum height of 14'-0"" (Table 4 note 5 likewise) — it is Table 4's, and Appendix B prints no
8x8 column, so an 8x8 passes `t.post-size` and is Out of scope naming the post column; Table 4 (p. 12; decision 2), p. 10's diagonal bracing and
post-to-beam details, p. 11's footing and frost text (napkin's frost line is its own comparison), Appendix C.

## Golden file spot-checks (`packs/golden/us-ct-2022/dca6-table-b1.golden.json`)

250 hand row cases (one per row; the 125 4x4 ones expect `t.post-size`) plus the worked example, asked as a 4x4 and as a
6x6, the three scope limits, the inputs asked for and the column refusals (asked as a 6x6), a 4x6, and the generator's
boundary pairs (none for a 4x4 row). The reviewer's spot-checks by eye: ______

| Case (location) | Inputs | Expect | Page | OK |
|---|---|---|---|---|
| worked example's end post, `r.sp.4x4.20` | Southern Pine 4x4, 3'-0" × 4'-11 1/4" = 14.8 sq ft, 1'-6 1/2" high | outOfScope, limit `t.post-size` (p. 10); the cell's 6 is in the location | | ☐ |
| the same post as a 6x6, `r.sp.6x6.20` | Southern Pine 6x6, the same area and height | passes, allowed 14'-0" | | ☐ |
| a 4x6 | Southern Pine 4x6, 20 sq ft | outOfScope, limit `t.post-size` | | ☐ |
| an NP cell, `r.pp-rp-spf.6x6.170` | Ponderosa Pine 6x6 (or Red Pine, Spruce-Pine-Fir), 170 sq ft | outOfScope, notPermitted | | ☐ |
| `s.loads`, `s.snow`, `s.shape` | porch-roof; 45 psf; length 16'-0 1/16" on width 16'-0" | outOfScope, each limit | | ☐ |
| Western Larch; 8x8; 12'-0" × 20'-10 1/16" | 6x6 posts but for the 8x8 | outOfScope column species / post / tributaryArea | | ☐ |

## Remarks

______

## Sign-off

Rows checked: __ of 250. Table-level items: __. Derivation items: __ of 7. Manifest items: __ of 4.
Golden spot-checks: __.
Discrepancies found (each a fix and re-review, not a comment): ______

Sign-off: ______ (name/model), ______ (date)
