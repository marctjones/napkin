# Chain of five — the design, as you would write it on paper

Five slats in a row, each flush against the one before it: the regression case for chain resolution (#49). Slat 1 is anchored; every other slat is tied to its predecessor by a coincident corner (south-east of one to south-west of the next), so moving or resizing any slat carries the rest along.

```
|--1--|-2-|---3---|-4-|--5--|
```

## Stated dimensions

| Slat | Length | Width | Thickness |
|---|---|---|---|
| 1 | 10" | 3 1/2" | 3/4" |
| 2 | 8 1/2" | 3 1/2" | 3/4" |
| 3 | 12" | 3 1/2" | 3/4" |
| 4 | 6 3/4" | 3 1/2" | 3/4" |
| 5 | 9 1/4" | 3 1/2" | 3/4" |

Overall: 46 1/2" (3'-10 1/2") x 3 1/2" x 3/4". Five pieces from five boxes, five rows (every length differs), four relationships plus the anchor. Derivations are in `chain-of-five.expected.json`.
