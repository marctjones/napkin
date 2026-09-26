# Splayed bench — the design, as you would write it on paper

A low bench whose four legs lean out sideways: the worked example of
[`docs/design/angled-parts.md`](../docs/design/angled-parts.md) §9.1. Each leg is an angled part —
a **strut**, stored by its two ends — and leans one way only, so its ends are plain mitres, and the
arithmetic is chosen to land every length exactly on the grid.

## Stated dimensions

| What | Dimension | Where it comes from |
|---|---|---|
| Seat | 36" long × 12" wide × 3/4" plywood | design choice; 3/4 plywood is 3/4" (PS 1-19 Table 10, the shipped library) |
| Seat underside | 24" above the floor | design choice |
| Legs | 2x2, 1 1/2" × 1 1/2" | the shipped library (PS 20-25 Table 3, dry) |
| Each leg's top | 4" in from a seat end, 3" in from a seat edge | design choice |
| Each leg's foot | straight below its top in X, 7" further out in Y | design choice: a 7-24-25 triangle |

Every leg's ends are cut to the floor and to the seat's underside (`Z` cuts), and its wide face is
kept vertical (`reference: z`), so each end is one mitre-saw setting.

## Relationships

The seat is anchored. Each leg's top is held to the seat — on its underside, 4" in from its west
end (or 32" for the east legs), 3" in from its south edge (or 9") — and each foot is held to its
own top: the same X, 7" further out in Y, 24" lower. Widen the seat and the tops follow it and the
feet follow the tops; the legs stay the same board.

## What the cut list should say

- **Seat**, 1, 3'-0" × 1'-0" × 3/4", 3/4 plywood.
- **Leg**, 4, 2'-1 7/16" × 1 1/2" × 1 1/2", 2x2: 25" along the centreline (7² + 24² = 25²) plus half
  of each 7/16" setback. No length is approximate. Each end is mitred ≈16.5° off square.
