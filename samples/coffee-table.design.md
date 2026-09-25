# Coffee table — the design, as you would write it on paper

Drawn from above. X runs to the right, Y runs up the page, the origin is the top's
south-west corner. Every number below is a **finished** dimension of this design: it is stated
here, not looked up. napkin has no materials library yet (#7), so nothing in this fixture depends
on a nominal-to-actual size.

```
  y
  ^                        48"  (4'-0")
  |   +--------------------------------------------------+   --+
  |   |  +----+                                  +----+   |     |
  |   |  |leg |==================================|leg |   |     |
  |   |  +----+   long apron (north), 40"        +----+   |     |
  |   |  ||                                          ||   |     |
  |   |  ||  short apron (west), 16"                 ||   |    24"  (2'-0")
  |   |  ||                        short apron (east)||   |     |
  |   |  +----+                                  +----+   |     |
  |   |  |leg |==================================|leg |   |     |
  |   |  +----+   long apron (south), 40"        +----+   |     |
  |   +--------------------------------------------------+   --+
  |
  +----------------------------------------------------------> x
      (0,0)
```

## Stated dimensions

| What | Dimension | Notes |
|---|---|---|
| Top, in plan | 48" × 24" | 4'-0" by 2'-0" |
| Top, thickness | 3/4" | The scene holds all three dimensions; this is the top's `depth`. |
| Leg, in plan | 2 1/2" × 2 1/2" | four of them, identical |
| Leg, length | 16 1/4" | The scene holds all three dimensions; this is each leg's `depth`. |
| Leg inset from each edge of the top | 1 1/2" | the top overhangs the leg frame all round |
| Apron thickness, in plan | 3/4" | |
| Apron width (the vertical face) | 3 1/2" | The scene holds all three dimensions; this is each apron's `depth`. |
| Apron faces | flush with the outer faces of the legs | |
| Long aprons (south, north) | run between the legs along X | length derived below |
| Short aprons (west, east) | run between the legs along Y | length derived below |

## What the scene file holds

`coffee-table.scene.json` holds nine boxes (the top, four legs, four aprons), six dimensions and
thirty-three relationships. Every relationship and dimension is about the plan view. Thickness,
leg length and apron face width are in the table above and in `coffee-table.expected.json` under
`statedNotInScene` — the plan view cannot show them — and each is also the `depth` of the boxes
it belongs to (scene format version 4).

## Where each part is in space

Scene format version 4 gives every box a height above the floor (`anchor.z`), a `depth` along its
own local Z, and the face that points up (`faceUp`). None of these is a new dimension of this
design: every value below is arithmetic on the stated dimensions above, worked by hand, in units of
1/1024" (`docs/design/assembly-model.md` §9 case 22 states the same numbers). Every part lies as
drawn, so `faceUp` is `top` throughout, and every number is a multiple of 256, so nothing rounds.

| Part | `depth` | `anchor.z` | Derivation |
|---|---|---|---|
| Legs (all four) | 16640 | 0 | They stand on the floor, so z = 0. Their depth is their stated length, 16 1/4" × 1024 = 16640, so their tops are at z = 16640. |
| Top | 768 | 16640 | It sits on the legs, so its underside is at their tops, 16640. Its depth is its stated thickness, 3/4" × 1024 = 768, so its upper surface is at 16640 + 768 = 17408 = 17". |
| Aprons (all four) | 3584 | 13056 | Their depth is the stated vertical face, 3 1/2" × 1024 = 3584. Their upper edges are flush with the top's underside at 16640, so their bottoms are at 16640 − 3584 = 13056 = 12 3/4". |

**No relationship holds any of this.** The file relates the parts in the plan, as it always has;
nothing says the top rests on the legs or the aprons meet the top, so moving a leg up would not
carry the top with it. That is deliberate — the fixture is not redesigned by a format change
(`docs/design/assembly-model.md` §11 decision 9) — and the relationships the plan states (a
corner of the blank, a side face) fix only X and Y, so the different heights break none of them.
The cut list reads each part's three stored sizes and never its position or orientation (§5), so
it is exactly what it was.

The dimensions are placed so that every one of them is drawn outside the part it measures: the
apron lengths sit just beyond the top's south and west edges, and the overall width and depth
further out again. What `side` and `offset` mean to the viewer is in
[`docs/file-format.md`](../docs/file-format.md).

There is no cut list and no materials list here. Those expectations belong with #8/#9 and the
materials library (#7); this fixture is the geometry they will be computed from.
