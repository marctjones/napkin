# Picture frame — the design, as you would write it on paper

A flat frame of 1 1/2"-wide moulding around an 8" × 10" opening, with a 45° mitre at each of its
four corners. It is here for **shaped cuts** (#97): a mitred piece is cut to its *long point* — the
length measured along its outside edge — so the cut list must say 13" and 11", not the 10" and 8"
of the opening they surround. The opposite pieces are **identical**: each pair is drawn once, and
the second of the pair is the same box turned half a turn, so the list says two of each.

```
  y
  ^            11"
  |  +--------------------+  --+
  |  |\    rail, top     /|    |
  |  | +----------------+ |    |
  |  | |                | |    |
  |  |s|    opening     |s|   13"
  |  |t|    8" x 10"    |t|    |
  |  |i|                |i|    |
  |  |l|                |l|    |
  |  |e+----------------+e|    |
  |  |/   rail, bottom   \|    |
  |  +--------------------+  --+
  +------------------------------> x
```

## Stated dimensions

| What | Dimension |
|---|---|
| Opening | 8" wide × 10" tall |
| Moulding | 1 1/2" wide, 3/4" thick, lying flat |
| Outer size | 8 + 2 × 1 1/2 = 11" wide, 10 + 2 × 1 1/2 = 13" tall |
| Stiles (2) | 13" long point, mitred 45° at both ends towards the inside edge; the right one is the left one turned 180° about (11, 13) |
| Rails (2) | 11" long point, mitred 45° at both ends towards the inside edge; the top one is the bottom one turned 180° about (11, 13) |

Every mitre is a corner cut with both setbacks 1 1/2": the whole end, and 1 1/2" along the inside
edge (`docs/design/shaped-parts-model.md` §1.3). The blanks overlap in the corner triangles the
mitres take away; the outlines meet along the mitre lines.

Overall: 11" × 1'-1" × 3/4". Four pieces from four boxes, two rows of 2. Derivations are in
`picture-frame.expected.json`.
