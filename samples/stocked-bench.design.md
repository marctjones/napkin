# Stocked bench — the design, as you would write it on paper

A low bench built from what a yard stocks: a 3/4 plywood top on a frame of 2x4 legs and
stretchers and 1x4 aprons and end rails. It is here for **the shopping list** (#9): every part
names its stock, so the cut list's five rows become three lines of stock to buy, and several
parts come out of one board.

```
  y
  ^                      48"
  |  +--------------------------------------------+  --+
  |  | =================apron===================  |    |
  |  | L  ==========stretcher===============   L |    |
  |  |e|                                       |e|   16"
  |  | L  ==========stretcher===============   L |    |
  |  | =================apron===================  |    |
  |  +--------------------------------------------+  --+
  +------------------------------------------------------> x
     L = leg (2x4), e = end rail (1x4), top (3/4 plywood) over all
```

## Stated dimensions

| What | Stock | Dimension |
|---|---|---|
| Top | 3/4 plywood | 48" × 16", 3/4" thick, underside at 16 1/2" |
| Legs (4) | 2x4 | 1 1/2" × 3 1/2", 16 1/2" tall, 1" in from each edge of the top, the 1 1/2" across X |
| Aprons (2) | 1x4 | 46" long, on edge against the legs' south and north faces, upper edge at 16 1/2" |
| End rails (2) | 1x4 | 14" long, on edge against the legs' west and east faces, upper edge at 16 1/2" |
| Stretchers (2) | 2x4 | 43" long between the legs, lying flat, underside 4" off the floor |

The cross-sections are the stocks' dry sizes as the shipped materials library carries them — a
2x4 is 1 1/2" × 3 1/2" and a 1x4 is 3/4" × 3 1/2" (PS 20-25 Table 3), and 3/4 plywood is the 3/4
Performance Category on a 48" × 96" sheet (PS 1-19 Table 10, §5.4). The stock lengths a board is
bought in, 6' to 16' in 2' steps, are the library's too (WCLIB Standard No. 17 ¶260-a, ¶2-i).
None of those numbers is written here from memory; each is cited in
`src/Napkin.Core.Materials/data`.

Overall: 4'-0" × 1'-4" × 1'-5 1/4". Eleven pieces from eleven boxes.

## The shopping list, worked by hand

First-fit decreasing over the stocked lengths (`docs/design/parts-and-cut-list.md` §4): each
piece, longest first, goes in the first board already bought with room for it, or else in a new
board of the shortest stocked length that holds it.

- **1x4** — 46, 46, 14, 14: two 6' boards, each carrying an apron and an end rail. The cut list's
  two rows of two are **2 × 6'-0"** to buy.
- **2x4** — 43, 43, 16 1/2 × 4: **3 × 6'-0"**.
- **3/4 plywood** — the top, 768 in² of a 4608 in² sheet: **1 sheet**, by area.

Board feet, from the nominal sizes (PS 20-20 §2.2, "Board measure", as `docs/design/parts-and-cut-list.md` §4.1 cites it), summed exactly and rounded once: 1x4 bought
4.0, used 3.3, waste 0.7; 2x4 bought 12.0, used 8.4, waste 3.6. Every step is written out in
`stocked-bench.expected.json`'s `shoppingList` derivations. None of it includes a saw kerf,
defect or a joinery allowance — the list says so on screen and in the file.
