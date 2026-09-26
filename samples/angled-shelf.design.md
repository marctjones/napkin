# Angled shelf — the design, as you would write it on paper

A display shelf tilted between two sides, its front edge lower than its back: the angled shelf of
[`docs/design/angled-parts.md`](../docs/design/angled-parts.md) §1.5. The shelf is an angled part
stored *across* its tilt — from its back edge to its front edge — so the length napkin works out is
the shelf's **width**, and the 30" it runs between the sides is its thickness-free third dimension.

## Stated dimensions

| What | Dimension | Where it comes from |
|---|---|---|
| Sides | 3/4" plywood, 12" deep, 24" tall, 30" apart inside | design choice; 3/4 plywood is 3/4" (PS 1-19 Table 10, the shipped library) |
| Shelf | 3/4" plywood, 30" long between the sides | design choice |
| Shelf's back edge | 2" in from the back, 18" up | design choice |
| Shelf's front edge | 10" in from the back, 12" up | design choice: 8" forward and 6" down, a 6-8-10 triangle |

Both edges are cut to the vertical (`Y` cuts): the back edge to sit against a back panel's face, the
front edge plumb. The shelf keeps its end profile as its drawn face (`reference: y`), so each edge is
a single mitre, and its part says the run is its **width** (`planAxes: { x: width, y: thickness }`).

## What the cut list should say

- **Shelf**, 1, 2'-6" × 10 9/16" × 3/4", 3/4 plywood: 10" across plus half of each 9/16" setback,
  exact. Each edge is a mitre ≈37° off square, for the full 2'-6" length.
- **Side**, 2, 2'-0" × 1'-0" × 3/4", 3/4 plywood.
