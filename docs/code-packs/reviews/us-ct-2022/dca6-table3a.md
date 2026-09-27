# Review: us-ct-2022 / DCA 6-2015 Table 3A (dimension-lumber deck beam spans, p. 6)

**Status: REVIEWED, CLEAN — with one finding for Marc on Decision 8 (how L_B is measured), which is not a
transcription discrepancy.** Independent review of the #41 slice B2 transcription against the PDF
(docs/design/deck-guide-pack.md §5; rules-engine-model §8.3). Every value below was read by the reviewer from the
rendered page **before** any pack file was opened, written down, and only then compared.

**Summary: 168 rows — 168 ✓, 0 ✗, 0 ?** (species group, member mapping, joist-span column, L_B, note attachments,
citation string, golden case). Table-level items 14/14 ✓; derivation items 3/3 ✓ (the beam span is implemented as
Decision 8 was signed off; the transcriber's finding that Figure 3 draws L_B face to face is **confirmed** and
sharpened — see "Figure 3" below); manifest items 4/4 ✓, and nothing the Table 2 review verified in `layer.json` or
`pack.json` changed (addendum in `dca6-table2.md`). Golden: all 171 hand row cases and the 10 other hand cases are the
page's; 676 generated boundary cases counted and tallied. No discrepancy; nothing for the transcriber to fix.

Reviewed by: **Claude Fable 5.1**  Date: **2026-09-27**  Source: `awc-dca6-2015` sha256
`205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` (verified with `shasum -a 256` before reading;
equals the line below and the manifest's `sources[0].sha256`)

Pack data hash at the reviewed commit (`8d98a93`, the transcriber's data commit; on `main` at `e61cbfb`, 0.205.0-beta,
the four files are byte-identical — `git diff 8d98a93 HEAD` on them is empty):
`shasum -a 256 packs/layers/dca6-2015/layer.json packs/layers/dca6-2015/deck/table-3a.json packs/golden/us-ct-2022/dca6-table-3a.golden.json packs/packs/us-ct-2022/pack.json`
- `155f818c9797e05115457f02e97d11d77c9b664b055962de634c04c440b9a8f5  packs/layers/dca6-2015/layer.json` (changed since the Table 2 review's `65cfd520…`: two scope notes added and its `notes` text, see "The guide manifest additions" below)
- `e9867886abb9ca4e29e2fc948f53cde5edab668efe897480358e3d1cbf15f94e  packs/layers/dca6-2015/deck/table-3a.json`
- `af7f35df39cd42e49fa084027af922a91300352c76c937d2d01b01e0ee1da60d  packs/golden/us-ct-2022/dca6-table-3a.golden.json`
- `5cfad56ef033ef983af9d39cb90f82ab082411258e0e9d0ca869d353dbd1e92d  packs/packs/us-ct-2022/pack.json` (changed since the Table 2 review's `841534b4…`: `revision` 2 → 3 and one sentence added to `notes`, nothing else)

## Reviewer's method

- Pages 5, 6, 7 and B2 (PDF page 27) rendered at 300 dpi (`pdftoppm -r 300 -png`) into the reviewer's own scratch
  directory (not the transcriber's renders) and read as images. Table 3A's header, both species blocks and its notes
  re-read from five 400 dpi crops (`pdftoppm -r 400 -x 200 -W 3200 …`), where every digit, superscript and rule is
  unambiguous; Figure 3's posts and dimension line read at 600 dpi and again at 1200 dpi (one post, the deck edge);
  Figure B3's post at 600 and 1200 dpi. p. 3 (Table 1, JOIST SIZE) and p. 9 (the other "Table 3A") via `pdftotext`.
- The reviewer's reading of all 140 printed cells (8 + 12 size rows × 7 columns), the title, the column headings,
  the two group headings with every superscript's position, and the four notes was written to a file first;
  `pdftotext -layout` of p. 6 was then used only as a cross-check (it agrees on every cell and every note).
- Comparison: a script expanded that file into 168 expected rows in napkin's form (two rows per two-name cell; the
  printed size mapped "2-2x8" → `(2) 2x8`, "3-2x8" → `(3) 2x8`, "3x8" → `(1) 3x8`, "4x8" → `(1) 4x8`) and diffed them
  against `table-3a.json` on id order, species, member, joist-span bound, span and footnotes: **0 differences**. Every
  row's `location` string (what `DeckCheck.Cited` shows a user) was checked to name its own group, its printed size
  cell (with "(its 3x8)" / "(its 2-2x8)" only on the four two-name cells) and its column: 0 problems. Member counts
  per group (8 for Southern Pine, 16 for the second group from its 12 printed sizes) and 7 rows per member were counted.
- Golden: a second script checked every hand row case against the reviewer's page reading, not the pack — the row
  its inputs name (species → group, member, joist span → column), its expected `allowed` equal to the page's L_B, a
  `short` case's `over` equal to asked − L_B, and the L_B quoted in the case's `location`: 171 cases, 0 problems.
  The 10 other hand cases and 24 of the row cases were read by eye against pp. 2, 3 and 6. The generated cases were
  counted and their expectations tallied against the generator's rule (`DeckGolden.Boundaries`, read).
- `dotnet test tests/Napkin.Core.RulesEngine.Tests --filter DeckGolden` was run once (38 passed): that confirms the
  committed golden file matches the pack, which is the transcriber's check, not independent verification of either.
- The user-facing citation was verified at the code level (`DeckCheck.Cited`, `DeckGuide.Clause`, `DeckCheck.cs`
  l. 116–124, `DeckFraming.cs` l. 22–26 and l. 155, and the string asserted in `DeckCheckTests.cs` l. 186–195), read,
  not run.

## The document

| | |
|---|---|
| Title | *Prescriptive Residential Wood Deck Construction Guide — Based on the 2015 International Residential Code* (DCA 6), American Wood Council |
| Printing | 2015 IRC edition; "Copyright © 2018 American Wood Council" (cover); printing mark "04-18" (p. 24) |
| URL | https://web-media.awc.org/wp-content/uploads/2022/02/17210514/AWC-DCA62015-DeckGuide-1804.pdf |
| SHA-256 | `205d57b515e22aeed6b2c5c5eecd02967dbb98aa40b6f841d25fcec119b3009e` |
| Retrieved / checked | 2026-09-27 (hash verified by the transcriber before reading, and again by the reviewer) |
| Pages read by the transcriber | p. 3 (JOIST SIZE: the joist span L), p. 5 (BEAM SIZE & ASSEMBLY REQUIREMENTS), p. 6 (Table 3A and its notes), p. 7 (Table 3B, read and not transcribed; Figure 3), p. B2 (Beam Span Length B_L; Figure B3) |
| Pages read by the reviewer | p. 3 (Table 1's species; JOIST SIZE), p. 5 (Figures 1B and 2; BEAM SIZE & ASSEMBLY REQUIREMENTS), p. 6 (Table 3A and its notes), p. 7 (Table 3B's title and notes, not transcribed; Figure 3), p. 9 (the second "Table 3A", joist hanger capacity), p. B2 (Beam Span Length B_L, Beam Overhang Length B_O; Figure B3) |
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

Reviewer's verdict per row: ✓ = the pack value is what the page prints. **All 168 rows: every column ✓.** Southern
Pine's eight printed sizes and the second group's twelve are as the page prints them; each two-name cell's two rows
carry that cell's one L_B; the 56 Southern Pine rows carry no note and the 112 second-group rows carry notes 2 and 3.

| RowId | Group | Printed size (p. 6) | Member | Column | Pack: L_B | Pack: notes | Inputs match | L_B matches | Notes match | Golden case present | OK |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `r.sp.2-2x6.6` | SP | 2-2x6 | `(2) 2x6` | ≤ 6' | 6'-8" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.8` | SP | 2-2x6 | `(2) 2x6` | ≤ 8' | 5'-8" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.10` | SP | 2-2x6 | `(2) 2x6` | ≤ 10' | 5'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.12` | SP | 2-2x6 | `(2) 2x6` | ≤ 12' | 4'-7" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.14` | SP | 2-2x6 | `(2) 2x6` | ≤ 14' | 4'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.16` | SP | 2-2x6 | `(2) 2x6` | ≤ 16' | 4'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x6.18` | SP | 2-2x6 | `(2) 2x6` | ≤ 18' | 3'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.6` | SP | 2-2x8 | `(2) 2x8` | ≤ 6' | 8'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.8` | SP | 2-2x8 | `(2) 2x8` | ≤ 8' | 7'-4" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.10` | SP | 2-2x8 | `(2) 2x8` | ≤ 10' | 6'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.12` | SP | 2-2x8 | `(2) 2x8` | ≤ 12' | 5'-11" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.14` | SP | 2-2x8 | `(2) 2x8` | ≤ 14' | 5'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.16` | SP | 2-2x8 | `(2) 2x8` | ≤ 16' | 5'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x8.18` | SP | 2-2x8 | `(2) 2x8` | ≤ 18' | 4'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.6` | SP | 2-2x10 | `(2) 2x10` | ≤ 6' | 10'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.8` | SP | 2-2x10 | `(2) 2x10` | ≤ 8' | 8'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.10` | SP | 2-2x10 | `(2) 2x10` | ≤ 10' | 7'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.12` | SP | 2-2x10 | `(2) 2x10` | ≤ 12' | 7'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.14` | SP | 2-2x10 | `(2) 2x10` | ≤ 14' | 6'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.16` | SP | 2-2x10 | `(2) 2x10` | ≤ 16' | 6'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x10.18` | SP | 2-2x10 | `(2) 2x10` | ≤ 18' | 5'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.6` | SP | 2-2x12 | `(2) 2x12` | ≤ 6' | 11'-11" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.8` | SP | 2-2x12 | `(2) 2x12` | ≤ 8' | 10'-4" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.10` | SP | 2-2x12 | `(2) 2x12` | ≤ 10' | 9'-2" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.12` | SP | 2-2x12 | `(2) 2x12` | ≤ 12' | 8'-4" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.14` | SP | 2-2x12 | `(2) 2x12` | ≤ 14' | 7'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.16` | SP | 2-2x12 | `(2) 2x12` | ≤ 16' | 7'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.2-2x12.18` | SP | 2-2x12 | `(2) 2x12` | ≤ 18' | 6'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.6` | SP | 3-2x6 | `(3) 2x6` | ≤ 6' | 7'-11" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.8` | SP | 3-2x6 | `(3) 2x6` | ≤ 8' | 7'-2" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.10` | SP | 3-2x6 | `(3) 2x6` | ≤ 10' | 6'-5" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.12` | SP | 3-2x6 | `(3) 2x6` | ≤ 12' | 5'-10" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.14` | SP | 3-2x6 | `(3) 2x6` | ≤ 14' | 5'-5" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.16` | SP | 3-2x6 | `(3) 2x6` | ≤ 16' | 5'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x6.18` | SP | 3-2x6 | `(3) 2x6` | ≤ 18' | 4'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.6` | SP | 3-2x8 | `(3) 2x8` | ≤ 6' | 10'-7" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.8` | SP | 3-2x8 | `(3) 2x8` | ≤ 8' | 9'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.10` | SP | 3-2x8 | `(3) 2x8` | ≤ 10' | 8'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.12` | SP | 3-2x8 | `(3) 2x8` | ≤ 12' | 7'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.14` | SP | 3-2x8 | `(3) 2x8` | ≤ 14' | 6'-11" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.16` | SP | 3-2x8 | `(3) 2x8` | ≤ 16' | 6'-5" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x8.18` | SP | 3-2x8 | `(3) 2x8` | ≤ 18' | 6'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.6` | SP | 3-2x10 | `(3) 2x10` | ≤ 6' | 12'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.8` | SP | 3-2x10 | `(3) 2x10` | ≤ 8' | 11'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.10` | SP | 3-2x10 | `(3) 2x10` | ≤ 10' | 9'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.12` | SP | 3-2x10 | `(3) 2x10` | ≤ 12' | 8'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.14` | SP | 3-2x10 | `(3) 2x10` | ≤ 14' | 8'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.16` | SP | 3-2x10 | `(3) 2x10` | ≤ 16' | 7'-8" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x10.18` | SP | 3-2x10 | `(3) 2x10` | ≤ 18' | 7'-3" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.6` | SP | 3-2x12 | `(3) 2x12` | ≤ 6' | 15'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.8` | SP | 3-2x12 | `(3) 2x12` | ≤ 8' | 13'-0" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.10` | SP | 3-2x12 | `(3) 2x12` | ≤ 10' | 11'-7" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.12` | SP | 3-2x12 | `(3) 2x12` | ≤ 12' | 10'-6" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.14` | SP | 3-2x12 | `(3) 2x12` | ≤ 14' | 9'-9" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.16` | SP | 3-2x12 | `(3) 2x12` | ≤ 16' | 9'-1" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.sp.3-2x12.18` | SP | 3-2x12 | `(3) 2x12` | ≤ 18' | 8'-7" | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.6` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 6' | 5'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.8` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 8' | 4'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.10` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 10' | 3'-11" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.12` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 12' | 3'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.14` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 14' | 3'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.16` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 16' | 2'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x6.18` | DF-L … Red Pine | 3x6 or 2-2x6 (its 3x6) | `(1) 3x6` | ≤ 18' | 2'-6" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.6` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 6' | 5'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.8` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 8' | 4'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.10` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 10' | 3'-11" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.12` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 12' | 3'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.14` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 14' | 3'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.16` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 16' | 2'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x6.18` | DF-L … Red Pine | 3x6 or 2-2x6 (its 2-2x6) | `(2) 2x6` | ≤ 18' | 2'-6" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.6` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 6' | 6'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.8` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 8' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.10` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 10' | 5'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.12` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 12' | 4'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.14` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 14' | 4'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.16` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 16' | 3'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x8.18` | DF-L … Red Pine | 3x8 or 2-2x8 (its 3x8) | `(1) 3x8` | ≤ 18' | 3'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.6` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 6' | 6'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.8` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 8' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.10` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 10' | 5'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.12` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 12' | 4'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.14` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 14' | 4'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.16` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 16' | 3'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x8.18` | DF-L … Red Pine | 3x8 or 2-2x8 (its 2-2x8) | `(2) 2x8` | ≤ 18' | 3'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.6` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 6' | 8'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.8` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 8' | 7'-0" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.10` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 10' | 6'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.12` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 12' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.14` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 14' | 5'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.16` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 16' | 4'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x10.18` | DF-L … Red Pine | 3x10 or 2-2x10 (its 3x10) | `(1) 3x10` | ≤ 18' | 4'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.6` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 6' | 8'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.8` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 8' | 7'-0" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.10` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 10' | 6'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.12` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 12' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.14` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 14' | 5'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.16` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 16' | 4'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x10.18` | DF-L … Red Pine | 3x10 or 2-2x10 (its 2-2x10) | `(2) 2x10` | ≤ 18' | 4'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.6` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 6' | 9'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.8` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 8' | 8'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.10` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 10' | 7'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.12` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 12' | 6'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.14` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 14' | 6'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.16` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 16' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3x12.18` | DF-L … Red Pine | 3x12 or 2-2x12 (its 3x12) | `(1) 3x12` | ≤ 18' | 5'-4" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.6` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 6' | 9'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.8` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 8' | 8'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.10` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 10' | 7'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.12` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 12' | 6'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.14` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 14' | 6'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.16` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 16' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.2-2x12.18` | DF-L … Red Pine | 3x12 or 2-2x12 (its 2-2x12) | `(2) 2x12` | ≤ 18' | 5'-4" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.6` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 6' | 6'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.8` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 8' | 5'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.10` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 10' | 4'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.12` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 12' | 4'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.14` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 14' | 3'-11" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.16` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 16' | 3'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x6.18` | DF-L … Red Pine | 4x6 | `(1) 4x6` | ≤ 18' | 3'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.6` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 6' | 8'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.8` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 8' | 7'-0" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.10` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 10' | 6'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.12` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 12' | 5'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.14` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 14' | 5'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.16` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 16' | 4'-11" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x8.18` | DF-L … Red Pine | 4x8 | `(1) 4x8` | ≤ 18' | 4'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.6` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 6' | 9'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.8` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 8' | 8'-4" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.10` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 10' | 7'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.12` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 12' | 6'-9" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.14` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 14' | 6'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.16` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 16' | 5'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x10.18` | DF-L … Red Pine | 4x10 | `(1) 4x10` | ≤ 18' | 5'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.6` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 6' | 11'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.8` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 8' | 9'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.10` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 10' | 8'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.12` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 12' | 7'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.14` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 14' | 7'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.16` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 16' | 6'-9" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.4x12.18` | DF-L … Red Pine | 4x12 | `(1) 4x12` | ≤ 18' | 6'-4" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.6` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 6' | 7'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.8` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 8' | 6'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.10` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 10' | 5'-9" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.12` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 12' | 5'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.14` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 14' | 4'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.16` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 16' | 4'-6" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x6.18` | DF-L … Red Pine | 3-2x6 | `(3) 2x6` | ≤ 18' | 4'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.6` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 6' | 9'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.8` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 8' | 8'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.10` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 10' | 7'-4" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.12` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 12' | 6'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.14` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 14' | 6'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.16` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 16' | 5'-9" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x8.18` | DF-L … Red Pine | 3-2x8 | `(3) 2x8` | ≤ 18' | 5'-5" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.6` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 6' | 11'-9" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.8` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 8' | 10'-2" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.10` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 10' | 9'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.12` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 12' | 8'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.14` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 14' | 7'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.16` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 16' | 7'-1" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x10.18` | DF-L … Red Pine | 3-2x10 | `(3) 2x10` | ≤ 18' | 6'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.6` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 6' | 13'-8" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.8` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 8' | 11'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.10` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 10' | 10'-6" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.12` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 12' | 9'-7" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.14` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 14' | 8'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.16` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 16' | 8'-3" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |
| `r.dfl-rw.3-2x12.18` | DF-L … Red Pine | 3-2x12 | `(3) 2x12` | ≤ 18' | 7'-10" | 2, 3 | ✓ | ✓ | ✓ | ✓ | ✓ |

## Table-level encoding

| Item | Where read | What the pack encodes | Matches the page | Classification right | OK |
|---|---|---|---|---|---|
| Designation and title | p. 6, "Table 3A. Dimension Lumber Deck Beam Spans (L_B)¹ Supporting a Single Span of Joists with or without Overhangs." | `table: "3A"`, `title: "Dimension Lumber Deck Beam Spans (LB) Supporting a Single Span of Joists with or without Overhangs."` (subscript B flattened, superscript 1 dropped), `location: "p. 6"` | ✓ (superscript 1 sits on the closing paren of "(L_B)"; the title breaks after "with or") | — | ✓ |
| Not the p. 9 Table 3A | p. 9 | nothing from p. 9 is in the file | ✓ (p. 9 prints "Table 3A. Joist Hanger Vertical Capacity."; the file's inputs are species, member and joist span and its 168 rows are beam spans — nothing of p. 9) | — | ✓ |
| Species column values = the two printed row headings | p. 6, Species column | "Southern Pine"; "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine" (the page breaks the second across nine lines) | ✓ same words (as printed the second heading is ten lines, not nine — remark 2) | — | ✓ |
| Species group: Southern Pine | p. 6, first row heading | Southern Pine | ✓ (no superscript on the heading) | — | ✓ |
| Species group: the second heading | p. 6, second row heading (note 2 on Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir; note 3 on Ponderosa Pine, Red Pine) | the seven species, each in the guide's list; all eight of the guide's species placed once | ✓ (superscript 2 on "Larch", on the "Fir" of Hem-Fir and on "Pine-Fir" of Spruce-Pine-Fir; superscript 3 on the "Pine" of Ponderosa Pine and of Red Pine; none on Redwood or Western Cedars — as `speciesGroups[1].location` says; with Southern Pine they are Table 1's eight species, p. 3, each placed in exactly one group) | — | ✓ |
| Size column values and members | p. 6, Size⁴ column | 16 members: `(2) 2x6`, `(2) 2x8`, `(2) 2x10`, `(2) 2x12`, `(3) 2x6` … `(3) 2x12`, `(1) 3x6` … `(1) 3x12`, `(1) 4x6` … `(1) 4x12`; Southern Pine prints no 3x or 4x row, so a Southern Pine `(1) 4x8` is out of scope (column `member`) | ✓ (Southern Pine prints 2-2x6, 2-2x8, 2-2x10, 2-2x12, 3-2x6, 3-2x8, 3-2x10, 3-2x12 — eight, no 3x or 4x; the second group prints the four two-name cells, 4x6, 4x8, 4x10, 4x12 and 3-2x6, 3-2x8, 3-2x10, 3-2x12 — twelve; the golden's Southern Pine `(1) 4x8` refuses on column `member`) | — | ✓ |
| Two-name cells | p. 6, "3x6 or 2-2x6", "3x8 or 2-2x8", "3x10 or 2-2x10", "3x12 or 2-2x12" | each is two rows, `r.dfl-rw.3xN.*` and `r.dfl-rw.2-2xN.*`, with the same L_B and a `location` naming the same cell ("(its 3x8)", "(its 2-2x8)") | ✓ (each cell's seven values are in both of its rows, identical; both `location`s name the cell and say which name is theirs) | design §3.2 ✓ | ✓ |
| Joist-span column | p. 6, "Joist Spans (L) Less Than or Equal to:" 6', 8', 10', 12', 14', 16', 18' | `joistSpan`, length, `upper-bound`, domain max `18ft 0in`; min `1/1024in` is napkin's (the page prints no lower bound) | ✓ ("Joist Spans (L) Less Than or Equal to:" over 6', 8', 10', 12', 14', 16', 18'; no lower bound printed) | ✓ ("Less Than or Equal to" is an upper bound; 18' is the last column, so 18'-0 1/16" refuses on the column) | ✓ |
| What the joist-span input is | p. 3, JOIST SIZE (L measured face of support to face of support); Figure 1A, p. 4 | napkin asks with the joists' span L, the same quantity as Table 2's check (`DeckFraming.JoistSpan`) | ✓ (p. 3, JOIST SIZE: "The span of a joist, L, is measured from the face of support at one end of the joist to the face of support at the other end of the joist and does not include the length of the overhangs, LO."; Table 3A's heading names that L) | ✓ (`DeckCheck.cs` l. 123 passes `framing.JoistSpan`, "the joists' clear span, ledger face to beam", the value the joist request carries at l. 106) | ✓ |
| Note 1 | p. 6 (on the title's L_B) | "Assumes 40 psf live load, 10 psf dead load, L/360 simple span beam deflection limit, cantilever length/180 deflection limit, No. 2 grade, and wet service conditions." | ✓ verbatim; superscript 1 on the title's "(L_B)" | not-encoded, table ✓ | ✓ |
| Note 2 | p. 6 (on Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir in the second heading) | "Incising assumed for Douglas Fir-Larch, Hem-Fir, and Spruce-Pine-Fir." | ✓ verbatim | not-encoded, on the second group's 112 rows (the row is the group; the superscript sits on three of its seven species) ✓ — B1's precedent, remark 5 | ✓ |
| Note 3 | p. 6 (on Ponderosa Pine and Red Pine in the second heading) | "Design values based on northern species with no incising assumed." | ✓ verbatim | not-encoded, on the second group's 112 rows (the superscript sits on two of its seven species) ✓ | ✓ |
| Note 4 | p. 6 (on the Size heading) | "Beam depth must be equal to or greater than joist depth if joist hangers are used (see Figure 6, Option 3)." | ✓ verbatim; superscript 4 on "Size" | not-encoded, table ✓ | ✓ |
| Table 3B not transcribed | p. 7 | no glulam rows (napkin's beam is plies of dimension lumber; design §3.2) | ✓ (Table 3B, p. 7, bands on glulam stress class, width and depth; `deck/` holds only `table-2.json` and `table-3a.json`) | — | ✓ |

## How napkin measures the beam span it asks about (derivation, Decision 8)

| Item | Where read | What napkin does | Reviewer: what the page shows | OK |
|---|---|---|---|---|
| Beam span L_B | Figure 3, p. 7 ("beam span (L_B): See Table 3" between posts, "L_B/4 max. overhang" beyond the end posts); BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5 ("can extend past the post face up to LB/4"); Appendix B, p. B2 and Figure B3 ("The beam span is measured from either centerline of post to centerline of post, if there are overhangs, or to the outside edges of the deck, if there are no overhangs") | `DeckFraming.BeamSpan` = (deck width − post width) ÷ (posts − 1): post centreline to post centreline (design note Decision 8, signed off 2026-09-27); the line says "post centre to post centre" | Implemented as Decision 8 was signed off: `DeckFraming.cs` l. 155 computes (W − post width) ÷ (posts − 1), post centre to post centre; the beam line says "post centre to post centre" (`DeckCheck.cs` l. 124, asserted in `DeckCheckTests.cs` l. 190). What the page shows is the "Figure 3" section below: the main body (Figure 3; p. 5) dimensions L_B face to face; Appendix B measures its B_L centre to centre when there are overhangs and to the deck's outside edge when there are none. | ✓ as signed off; finding for Marc |
| **Transcriber's finding, for the reviewer to confirm or refute** | Figure 3, p. 7, at 600 dpi | The arrowheads of Figure 3's "beam span (L_B)" and "L_B/4 max. overhang" dimensions end at the **faces** of the posts (each post's two arrowheads point outward from its middle to its two faces), which with p. 5's "past the post face" reads as a face-to-face L_B in the main body; only Appendix B, written for posts' and footings' tributary areas, measures centre to centre. napkin implements Decision 8 as signed off (the longer, conservative measure) and flags this for Marc; no verdict is asked on the decision, only on what the figure shows. | **Confirmed.** At 600 and 1200 dpi each Figure 3 post is two face lines and nothing else — no centreline; the arrow tips sit on those faces (the overhang's on the end post's outer face and on the deck edge, each beam span's on the inner faces of adjacent posts). Figure B3 draws a third, longer line through each post's centre and its B_L / B_O arrow tips meet on it. p. 5: "past the post face". Detail below. | ✓ |
| No beam overhang check | Figure 3; p. 5 | napkin's beam ends at its end posts (no overhang), so L_B/4 is not checked; `n.beam-span` shows the sentence | ✓ Figure 3 labels the overhang "optional overhang (may occur at each end)"; napkin's end posts are flush with the deck's ends (`DeckFraming.cs` l. 24), so there is no overhang to check; `n.beam-span` is in the manifest (below) | ✓ |

## The guide manifest additions (`packs/layers/dca6-2015/layer.json`)

| Item | Where read | What the pack encodes | Verbatim / right | OK |
|---|---|---|---|---|
| Note `n.beam-span` | BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5; Figure 3, p. 7 | "Deck beam spans shall be in accordance with Table 3 and can extend past the post face up to LB/4 as shown in Figure 3." (subscript B flattened) | ✓ verbatim (p. 5, the first sentence under BEAM SIZE & ASSEMBLY REQUIREMENTS; the page's L with subscript B is "LB" in plain text, as `pdftotext` renders it); cites right — Figure 3 is on p. 7 | ✓ |
| Note `n.beam-sides` | BEAM SIZE & ASSEMBLY REQUIREMENTS, p. 5 | "Joists shall not frame in from opposite sides of the same beam. See JOIST-TO-BEAM CONNECTION details, Figure 6." | ✓ verbatim (p. 5, right column, its first two sentences); cite right | ✓ |
| Scope limits apply to the beam | p. 2 items 2, 8, 9; Table 3A note 1, p. 6 | the guide's `s.loads`, `s.snow`, `s.shape` are tried before every Table 3A lookup (the golden file has a case for each); `s.loads`'s text quotes Table 2 note 1, whose loads Table 3A note 1 repeats | ✓ (Table 3A note 1 repeats Table 2 note 1's 40 psf live, 10 psf dead, No. 2 grade and wet service and adds the two deflection limits; items 2, 8 and 9 are the guide's, p. 2; `DeckCheck.cs` l. 123 passes supports, ground snow load, deck length and width with the beam request as l. 106 does with the joist request; the golden has an outOfScope case per limit and the generator two boundary cases for each `above`/`aboveInput` limit) | ✓ |
| CT pack revision 3 | — (napkin's own) | `revision: 3`, its `notes` say what revision 3 adds, `review.status: "unreviewed"` | ✓ (`revision: 3`; `notes` ends "Revision 3 (2026-09-27, #41 slice B2): the guide adds its deck beam table, Table 3A (p. 6)."; `review.status: "unreviewed"`, `review.checklist: null`; the diff from the Table 2 review's version is exactly those two hunks — addendum in `dca6-table2.md`) | ✓ |

Read and deliberately not encoded in this slice: Table 3B (p. 7), Table 3A joist hanger capacity (p. 9),
Figure 4's beam assembly fastening (p. 5), Figure 6's joist-to-beam options (p. 9), Table 4 and the
Appendix B tables (slice B3), Table 5 (B4), the guard and stair figures (B5).

## Golden file spot-checks (`packs/golden/us-ct-2022/dca6-table-3a.golden.json`)

168 hand row cases (one per row); 13 more hand cases: the worked example, a two-name cell's two rows, the
three scope limits, the three inputs asked for and the column refusals, and the generator's 676 boundary
pairs. Reviewer: 168 + 3 = 171 hand cases with a row and 10 without, 181 hand cases, plus 676
generated (`"generated": "boundary"`) = 857; the 676 are 338 pairs (remark 3). All 171 hand row cases were checked
by script against the reviewer's page reading (method); these were also read by eye against the page:

| Case (location) | Inputs | Expect | Page | OK |
|---|---|---|---|---|
| worked example, `r.sp.2-2x10.10` | Southern Pine (2) 2x10, joists 9'-9", span 5'-10 1/4" | passes, allowed 7'-9" | Southern Pine 2-2x10, ≤ 10' column (9'-9" ≤ 10'): L_B 7'-9"; 5'-10 1/4" is the design note's (144 − 3 1/2) ÷ 2 | ✓ |
| two-name cell, `r.dfl-rw.3x8.10` and `r.dfl-rw.2-2x8.10` | Hem-Fir, joists 10'-0", span 5'-1" | passes, allowed 5'-1" (both) | "3x8 or 2-2x8", ≤ 10' column: 5'-1", exactly at L_B for both names; Hem-Fir is in the second heading | ✓ |
| `s.loads`, `s.snow`, `s.shape` | porch-roof; 45 psf; length 16'-0 1/16" on width 16'-0" | outOfScope, each limit | Table 3A note 1, p. 6 with item 8, p. 2; item 9, p. 2 ("exceed 40 psf"); item 2, p. 2 (length ≤ width) | ✓ |
| Southern Pine `(1) 4x8`; `(2) 2x14`; joists 18'-0 1/16" | — | outOfScope column member / member / joistSpan | Southern Pine prints no 4x row; no 2x14 anywhere on p. 6; the last column is 18' | ✓ |
| missing supports / groundSnowLoad / species | — | inputMissing, each | items 8 and 9, p. 2 ask them; Table 3A bands on species | ✓ |
| Western Larch | — | outOfScope column species | none of Table 1's eight species (p. 3) and in neither heading on p. 6 | ✓ |
| `r.sp.2-2x6.6` | SP (2) 2x6, joists 5'-0", span 6'-7" | passes, allowed 6'-8" | 2-2x6 ≤ 6': 6'-8" | ✓ |
| `r.sp.2-2x8.10` | SP (2) 2x8, joists 9'-9", span 6'-8" | short, allowed 6'-6", over 2" | 2-2x8 ≤ 10': 6'-6" | ✓ |
| `r.sp.2-2x10.10` | SP (2) 2x10, joists 9'-9", span 7'-11" | short, allowed 7'-9", over 2" | 2-2x10 ≤ 10': 7'-9" | ✓ |
| `r.sp.2-2x12.18` | SP (2) 2x12, joists 17'-0", span 6'-9" | passes, allowed 6'-9" | 2-2x12 ≤ 18': 6'-9" | ✓ |
| `r.sp.3-2x6.8` | SP (3) 2x6, joists 8'-0", span 7'-2" | passes, allowed 7'-2" | 3-2x6 ≤ 8': 7'-2" | ✓ |
| `r.sp.3-2x8.14` | SP (3) 2x8, joists 14'-0", span 7'-0" | short, allowed 6'-11", over 1" | 3-2x8 ≤ 14': 6'-11" | ✓ |
| `r.sp.3-2x10.16` | SP (3) 2x10, joists 14'-1", span 7'-5" | passes, allowed 7'-8" | 3-2x10 ≤ 16': 7'-8" | ✓ |
| `r.sp.3-2x12.6` | SP (3) 2x12, joists 5'-0", span 14'-11" | passes, allowed 15'-0" | 3-2x12 ≤ 6': 15'-0" | ✓ |
| `r.sp.3-2x12.12` | SP (3) 2x12, joists 11'-6", span 10'-5 1/2" | passes, allowed 10'-6" | 3-2x12 ≤ 12': 10'-6" | ✓ |
| `r.dfl-rw.3x6.6` | Douglas Fir-Larch (1) 3x6, joists 5'-0", span 5'-1" | passes, allowed 5'-2" | "3x6 or 2-2x6" ≤ 6': 5'-2" | ✓ |
| `r.dfl-rw.2-2x6.6` | Douglas Fir-Larch (2) 2x6, joists 5'-0", span 5'-1" | passes, allowed 5'-2" | the same cell | ✓ |
| `r.dfl-rw.3x8.16` | Hem-Fir (1) 3x8, joists 14'-1", span 3'-7" | passes, allowed 3'-10" | "3x8 or 2-2x8" ≤ 16': 3'-10" | ✓ |
| `r.dfl-rw.2-2x8.16` | Hem-Fir (2) 2x8, joists 14'-1", span 3'-7" | passes, allowed 3'-10" | the same cell | ✓ |
| `r.dfl-rw.3x10.14` | Spruce-Pine-Fir (1) 3x10, joists 14'-0", span 5'-4" | short, allowed 5'-3", over 1" | "3x10 or 2-2x10" ≤ 14': 5'-3" | ✓ |
| `r.dfl-rw.3x12.18` | Redwood (1) 3x12, joists 17'-0", span 5'-4" | passes, allowed 5'-4" | "3x12 or 2-2x12" ≤ 18': 5'-4" | ✓ |
| `r.dfl-rw.2-2x12.18` | Redwood (2) 2x12, joists 17'-0", span 5'-4" | passes, allowed 5'-4" | the same cell | ✓ |
| `r.dfl-rw.4x6.12` | Western Cedars (1) 4x6, joists 11'-6", span 4'-2 1/2" | passes, allowed 4'-3" | 4x6 ≤ 12': 4'-3" | ✓ |
| `r.dfl-rw.4x8.8` | Ponderosa Pine (1) 4x8, joists 8'-0", span 7'-0" | passes, allowed 7'-0" | 4x8 ≤ 8': 7'-0" | ✓ |
| `r.dfl-rw.4x10.10` | Red Pine (1) 4x10, joists 9'-9", span 7'-7" | short, allowed 7'-5", over 2" | 4x10 ≤ 10': 7'-5" | ✓ |
| `r.dfl-rw.4x12.6` | Douglas Fir-Larch (1) 4x12, joists 5'-0", span 11'-1" | passes, allowed 11'-2" | 4x12 ≤ 6': 11'-2" | ✓ |
| `r.dfl-rw.3-2x6.14` | Hem-Fir (3) 2x6, joists 14'-0", span 4'-11" | short, allowed 4'-10", over 1" | 3-2x6 (second group) ≤ 14': 4'-10" | ✓ |
| `r.dfl-rw.3-2x8.12` | Spruce-Pine-Fir (3) 2x8, joists 11'-6", span 6'-7 1/2" | passes, allowed 6'-8" | 3-2x8 (second group) ≤ 12': 6'-8" | ✓ |
| `r.dfl-rw.3-2x10.8` | Redwood (3) 2x10, joists 8'-0", span 10'-2" | passes, allowed 10'-2" | 3-2x10 (second group) ≤ 8': 10'-2" | ✓ |
| `r.dfl-rw.3-2x12.18` | Western Cedars (3) 2x12, joists 17'-0", span 7'-10" | passes, allowed 7'-10" | 3-2x12 (second group) ≤ 18': 7'-10" | ✓ |

## Figure 3: how the page measures L_B (Decision 8) — the reviewer's finding for Marc

**Finding: in the main body of DCA 6, Table 3A's L_B is drawn and described face to face of posts; only Appendix B
measures centre to centre, and it does so for a differently named quantity, B_L, defined for post and footing
tributary areas — and for a beam without overhangs, as napkin's is, Appendix B measures to the deck's outside edge,
not to a post centre at all.** napkin's centre-to-centre measure (Decision 8) is the signed-off choice and is left as
it is; this review changes no code.

Evidence, all read from the rendered page:

1. **Figure 3, p. 7 ("Figure 3. Beam Span.")**, at 600 dpi and 1200 dpi. Each of the five posts is drawn as exactly
   two vertical lines — its two faces — running from under the beam to the break line and continuing below it as the
   dimension's extension lines. No centreline is drawn at any post. On the dimension line the arrowheads are placed
   outside their dimensions, inside each post's width, with their tips on the face lines: at each interior post a
   "◄" tip on its left face and a "►" tip on its right face; at the deck's left edge a "►" tip on the (dashed) deck
   edge line, from outside. So "L_B/4 max. overhang" runs from the deck edge to the end post's **outer** face, and
   each "beam span (L_B): See Table 3" runs from one post's **inner** face to the next post's inner face — a clear,
   face-to-face span. The only vertical line at a post's centre is the "beam splice (if needed) at interior post
   locations", drawn in the beam above the middle post, not on the dimension line.
2. **p. 5, BEAM SIZE & ASSEMBLY REQUIREMENTS**: "Deck beam spans shall be in accordance with Table 3 and can extend
   past the post face up to L_B/4 as shown in Figure 3." The overhang is measured from the post face, as Figure 3
   draws it.
3. **Tables 3A and 3B (pp. 6–7) and Figure 3** name the quantity L_B.
4. **Appendix B, p. B2, "Beam Span Length, B_L"**: "The beam span is measured from either centerline of post to
   centerline of post, if there are overhangs, or to the outside edges of the deck, if there are no overhangs. For
   posts or footings being considered with two unequal, adjacent beam spans, the greater span shall be used. See
   Figure B3." And "Beam Overhang Length, B_O": "The length of the beam overhang is measured from the outside edge of
   the deck to the centerline of the nearest post. See Figure B3. If a center post or footing is being considered or
   no overhang exists, zero is entered into the equation B-1 or B-2 for B_O." **Figure B3** ("Beam Span and Overhang
   Length") draws each post with three vertical lines — two faces stopping at the break line and a longer centreline
   crossing the dimension line — and the B_O / B_L / B_L / B_O arrow tips meet on those centrelines. The symbols are
   B_L and B_O, not L_B, and the section is written for "posts or footings being considered", i.e. the tributary-area
   equations B-1 and B-2 (not read in this review; slice B3).

What that means for napkin's number, on the design note's worked example (W = 144", three 4x4 posts, end posts flush
with the deck's ends, no overhang):

| Measure | Value | Where |
|---|---|---|
| Face to face, as Figure 3 draws L_B | 66 3/4" (5'-6 3/4"): 70 1/4" less one 3 1/2" post | Figure 3, p. 7; p. 5 |
| Centre to centre — napkin's `DeckFraming.BeamSpan` | 70 1/4" (5'-10 1/4"): (144 − 3 1/2) ÷ 2 | Decision 8; Appendix B's rule *with* overhangs |
| Appendix B's B_L *without* overhangs, an end span | 72" (6'-0"): interior post centreline to the deck's outside edge | p. B2; Figure B3 |

napkin's measure is longer than the main body's (conservative for the beam check: a beam that passes face to face by
less than one post width is reported short) and, because napkin's beam has no overhang, half a post width shorter
than the appendix's own no-overhang end span; it equals Appendix B's rule for a beam *with* overhangs, which napkin's
is not. None of this is a transcription error: the 168 L_B values are the page's whatever span is asked of them.
Whether the beam check should ask with the face-to-face span (the main body's L_B) while the tributary area uses
Appendix B's measure (slice B3's business) is Marc's to decide. The adopted 2021 IRC's own definition of the deck beam
span — which this review did not read and does not quote — is what settles it for Connecticut ("the IRC governs where
they differ", p. 1).

## Remarks (not discrepancies, no action required)

1. `title` drops the superscript 1 and flattens L with subscript B to "LB", as Table 2's `overhangLimit.location` does
   "LO" (that review's remark 3). Plain-text flattening.
2. This checklist's own prose (table-level item "Species column values") says the second heading "breaks across nine
   lines"; at 400 dpi it is ten ("Douglas Fir-" / "Larch², Hem-" / "Fir², Spruce-" / "Pine-Fir²," / "Redwood," /
   "Western" / "Cedars," / "Ponderosa" / "Pine³, Red" / "Pine³"), and `pdftotext` agrees. A miscount in the
   checklist's description, not in any pack file; same words.
3. This checklist and the golden's `notes` say "676 boundary pairs"; the file holds 676 generated *cases*, which are
   338 at-bound / one-step-past pairs: per row a span pair (at L_B passes; L_B + 1/1024" short) and a joist-span
   column pair (at the column's bound passes; one step past → the next column's row passes, or the column refuses on
   the 18' rows), 168 + 168, plus one pair each for `s.snow` (40 psf passes, 41 refuses) and `s.shape` (length = width
   passes, + 1/1024" refuses); `s.loads`, a `notIn` form, generates none. Tally: 482 passes, 168 short, 24 outOfScope
   column, 2 outOfScope limit.
4. The second group's hand row cases type its seven species by cycling per printed size row (Douglas Fir-Larch 3
   sizes, Hem-Fir 3 plus the two two-name-cell cases, Spruce-Pine-Fir 3, Redwood 3, Western Cedars 2, Ponderosa Pine
   1, Red Pine 1), so every one of the seven is resolved to the group at least seven times. Southern Pine types its own
   name in all 57 of its cases.
5. Notes 2 and 3 ride all 112 second-group rows although their superscripts sit on three and two of the group's seven
   species: the finest attachment the row structure allows, documented in `speciesGroups[1].location` (Table 2
   review, remark 4, same reasoning).
6. `review.checklist` is still `null` in `pack.json`; it may now point at this file and `dca6-table2.md`.
7. `DeckFraming.cs` still calls the along-ledger dimension `Width` "the deck's length along the ledger" where DCA 6
   calls that the deck's *width* (Table 2 review, remark 6). A code comment, outside this review's files.

## Sign-off

Rows checked: 168 of 168. Table-level items: 14 of 14. Derivation items: 3 of 3. Manifest items: 4 of 4.
Golden spot-checks: 171 hand row cases by script against the page, 24 of them and the 10 other hand cases by eye; 676
generated cases counted and tallied.
Discrepancies found (each a fix and re-review, not a comment): **none.** One finding for Marc (Decision 8, the
"Figure 3" section), which changes no data.

Review status: per deck-guide-pack §5.3 the CT pack's `review.status` is pack-wide; this table's own review is
complete and clean, as Table 2's is. It stays `unreviewed` until `frost.json` and `site-values.json` (#210) have
their checklists, or becomes §5's `in-review` if the orchestrator prefers that label; `review.checklist` may point at
both files.

Sign-off: Claude Fable 5.1 (independent reviewer), 2026-09-27
