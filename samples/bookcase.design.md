# Bookcase — the design, as you would write it on paper

A plywood carcass: two sides, a top and a bottom cap, two shelves and a thin back. It is here for
**sheet goods and repeated parts** — a back panel 1/4" thick beside 3/4" boards, and pairs that
must merge into one cut-list row — and to prove that two boards which differ only by 3/4" of depth
are *not* the same row.

Plan view (looking down; X to the right, Y up the page, origin at the south-west corner). The
front is the bottom edge of the drawing.

```
  y
  ^     30"
  |  +----------------------------+ -- back panel, 1/4" (y 11 1/4 .. 11 1/2)
  |  |+--------------------------+|
  |  ||  shelves 28 1/2" x 10 1/2"||   11 1/4"
  |  |+--------------------------+|
  |  +----------------------------+
  +-------------------------------------> x
     sides 3/4" thick at each end
```

## Stated dimensions

| What | Dimension |
|---|---|
| Overall | 30" wide, 11 1/2" deep (with the back), 36" tall |
| Sides (2) | 3/4" thick, 11 1/4" deep, 36" tall |
| Caps, top and bottom (2) | 28 1/2" long (30 − 2 × 3/4), 11 1/4" deep, 3/4" thick |
| Shelves (2) | 28 1/2" long, 10 1/2" deep (set back 3/4" from the front), 3/4" thick, undersides at 12" and 24" |
| Back (1) | 30" wide, 36" tall, 1/4" thick, behind the carcass |

No part names a stock: every dimension here is a finished dimension the design states itself, so
nothing depends on a nominal-to-actual lookup.

## What the cut list must say

Largest first: **Back** ×1 (3'-0" × 2'-6" × 1/4"), **Side** ×2 (3'-0" × 11 1/4" × 3/4"),
**Cap** ×2 (2'-4 1/2" × 11 1/4" × 3/4"), **Shelf** ×2 (2'-4 1/2" × 10 1/2" × 3/4"). Seven pieces
from seven boxes. The derivations are in `bookcase.expected.json`.
