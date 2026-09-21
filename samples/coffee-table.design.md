# Coffee table — the design, as you would write it on paper

Plan view (looking down). X runs to the right, Y runs up the page, the origin is the top's
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
| Top, thickness | 3/4" | **Stated, not in the scene** — the plan view has no third dimension. |
| Leg, in plan | 2 1/2" × 2 1/2" | four of them, identical |
| Leg, length | 16 1/4" | **Stated, not in the scene**, for the same reason. |
| Leg inset from each edge of the top | 1 1/2" | the top overhangs the leg frame all round |
| Apron thickness, in plan | 3/4" | |
| Apron width (the vertical face) | 3 1/2" | **Stated, not in the scene**, for the same reason. |
| Apron faces | flush with the outer faces of the legs | |
| Long aprons (south, north) | run between the legs along X | length derived below |
| Short aprons (west, east) | run between the legs along Y | length derived below |

## What the scene file holds

`coffee-table.scene.json` holds the plan view only: nine boxes (the top, four legs, four aprons),
six dimensions and thirty-three relationships. Thickness and leg length are in the table above and
in `coffee-table.expected.json` marked `statedNotInScene`, so that nothing tests napkin for a
number the scene does not carry.

The dimensions are placed so that every one of them is drawn outside the part it measures: the
apron lengths sit just beyond the top's south and west edges, and the overall width and depth
further out again. What `side` and `offset` mean to the viewer is in
[`docs/file-format.md`](../docs/file-format.md).

There is no cut list and no materials list here. Those expectations belong with #8/#9 and the
materials library (#7); this fixture is the geometry they will be computed from.
