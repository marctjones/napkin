# Rounded-corner table — the design, as you would write it on paper

The same small table the coffee-table fixture draws, with **the one thing this sample exists to
show**: the top's four corners are rounded to a 1" radius. Everything else is here so that the
sample is a table somebody could build, rather than a top floating on its own
(`docs/design/shaped-parts-model.md` §8).

Drawn from above. X runs to the right, Y runs up the page, the origin is the top's
south-west corner — which is now the centre of a 1" arc rather than a square corner. Every number
below is a **finished** dimension of this design: it is stated here, not looked up.

```
  y
  ^                        48"  (4'-0")
  |    /----------------------------------------------\    --+
  |   /   +----+                                +----+  \     |
  |   |   |leg |================================|leg |  |     |
  |   |   +----+   long apron (north), 40"      +----+  |     |
  |   |   ||                                        ||  |     |
  |   |   ||  short apron (west), 16"               ||  |    24"  (2'-0")
  |   |   ||                      short apron (east)||  |     |
  |   |   +----+                                +----+  |     |
  |   \   |leg |================================|leg |  /     |
  |    \  +----+   long apron (south), 40"      +----+ /      |
  |     \----------------------------------------------/   --+
  |      1" radius at each of the four corners
  +----------------------------------------------------------> x
      (0,0)
```

## Stated dimensions

| What | Dimension | Notes |
|---|---|---|
| Top, in plan | 48" × 24" | 4'-0" by 2'-0" — the blank, before the corners are rounded |
| Top, corner radius | 1" | at all four corners |
| Top, thickness | 3/4" | The scene holds all three dimensions; this is the top's `depth`. |
| Leg, in plan | 2 1/2" × 2 1/2" | four of them, identical |
| Leg, length | 16 1/4" | The scene holds all three dimensions; this is each leg's `depth`. |
| Leg inset from each edge of the top | 1 1/2" | the top overhangs the leg frame all round |
| Apron thickness, in plan | 3/4" | |
| Apron width (the vertical face) | 3 1/2" | The scene holds all three dimensions; this is each apron's `depth`. |
| Apron faces | flush with the outer faces of the legs | |
| Long aprons (south, north) | run between the legs along X | length derived in the expectations |
| Short aprons (west, east) | run between the legs along Y | length derived in the expectations |

## What the rounding does and does not change

- **The blank is still 48" × 24".** The cut list lists the three dimensions of the board you start
  from, not a bounding box of what is left, so the top's row is the row a square top of this size
  would have — plus one sentence saying what to do to it (§4.1, §4.2).
- **The shopping list is unchanged** (§4.6): the top still buys the same board.
- **The legs are clear of the arcs.** A leg is inset 1 1/2" from each edge and the radius is 1",
  so the nearest leg corner is 1/2" clear of where the arc leaves the edge. The rounding is a
  detail of the top, not a change to the frame under it.

## What the scene file holds

`rounded-corner-table.scene.json` holds the plan view: nine boxes (the top, four legs, four
aprons), the top's four `roundedCorner` cuts in site order, two dimensions on the top and three
relationships — the top pinned where it is and its two sizes stated. The legs and aprons are
placed at the positions the expectations file derives; this sample does not repeat the coffee
table's full web of relationships, because what it is here to demonstrate is the cuts.

Thickness, leg length and apron face width are in the table above and in
`rounded-corner-table.expected.json` under `statedNotInScene` — the plan view cannot show them —
and each is also the `depth` of the boxes it belongs to (scene format version 4).

## Where each part is in space

The coffee table's frame, so the coffee table's heights, worked the same way by hand from the
stated dimensions (units of 1/1024"; every part `faceUp` `top`, every value a multiple of 256):

| Part | `depth` | `anchor.z` | Derivation |
|---|---|---|---|
| Legs (all four) | 16640 | 0 | On the floor; depth is the stated length, 16 1/4" × 1024 = 16640, so their tops are at 16640. |
| Top | 768 | 16640 | Its underside on the legs' tops at 16640; depth is the stated thickness, 3/4" × 1024 = 768. |
| Aprons (all four) | 3584 | 13056 | Depth is the stated 3 1/2" face, × 1024 = 3584; upper edge flush with the top's underside at 16640, so z = 16640 − 3584 = 13056. |

No relationship holds any of these heights — this sample's three relationships are the top's
anchor and its two plan sizes — and the rounded corners are cut through the top's full depth, in
its own frame, so neither its outline nor its cut-list row changes.
