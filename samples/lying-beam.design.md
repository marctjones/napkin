# Lying beam — the design, as you would write it on paper

One 36" length of 1 1/2" × 3 1/2" material placed four ways: lying flat along X, lying flat along
Y (the box spun 90°), standing on end, and tipped onto its edge (face up = north). It is here to
prove that **how a piece is turned does not change what is cut**: four boxes in four orientations
are one row of 4, and each occupies exactly the space worked out below.

```
  y
  ^   36"
  |   +--+                       upright (50..53 1/2, y 0..1 1/2, z 0..36)
  |   |  |  along Y (40..43 1/2, y 0..36)
  |   |  |
  |  == on its side (0..36, y 8 1/2..10, z 0..3 1/2)
  |  == along X (0..36, y 0..3 1/2, z 0..1 1/2)
  +----------------------------------> x
```

## Stated dimensions

| What | Dimension |
|---|---|
| The piece | 36" × 3 1/2" × 1 1/2", no stock named |
| Along X | anchor (0, 0, 0), as drawn |
| Along Y | anchor (43 1/2, 0, 0), rotated 90° |
| Upright | anchor (50, 0, 0), 36" as its depth |
| On its side | anchor (0, 10, 0), face up = north |

Overall: x 0..53 1/2, y 0..36, z 0..36 (4'-5 1/2" × 3'-0" × 3'-0"). Derivations are in `lying-beam.expected.json`.
