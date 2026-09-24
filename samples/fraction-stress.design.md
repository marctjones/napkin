# Fraction stress — the design, as you would write it on paper

Four small parts whose sizes are awkward fractions: 1/16, 3/32, 5/64, and 1/3".

The scene format stores whole units of 1/1024", so a dimension that is not a multiple of 1/1024" cannot be written. One third of an inch is stored as its closest legal value, 341 units (0.33301"), the same rounding `Length.Divide` documents. The cut list prints sizes at 1/16", so 3/32" and 5/64" and 341 units display rounded (half away from zero) and marked with a leading approximation sign, while the 1/16" sizes read exactly.

## Stated dimensions

| Part | Length | Width | Thickness |
|---|---|---|---|
| Rail | 23 15/16" | 1 1/16" | 3/16" |
| Strip | 10 3/32" | 5/64" | 1/16" |
| Spacer | 6 1/16" | 3/32" | 5/64" |
| Third | 341/1024" | 1/16" | 1/16" |

Four pieces from four boxes, all on the floor in a row. Derivations are in `fraction-stress.expected.json`.
