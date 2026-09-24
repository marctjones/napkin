# Overlap — the design, as you would write it on paper

Two identical boards at the same place, and a peg through both. Nothing in napkin detects or forbids overlap (`docs/design/assembly-model.md` section 6: no interference detection, by design), and this sample states what it does: the file is valid, every relationship holds, and the cut list counts pieces to cut, so the two boards are one row of 2 and the peg one row of 1. The total volume is counted twice where the boards coincide.

## Stated dimensions

| What | Dimension |
|---|---|
| Boards (2) | 24" x 4" x 1", both at the origin |
| Peg | 1" x 1" x 3", x 10..11, y 1 1/2..2 1/2, through both |

Overall 24" x 4" x 3". Three pieces from three boxes, two rows. Derivations are in `overlap.expected.json`.
