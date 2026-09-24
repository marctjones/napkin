# Bench — the design, as you would write it on paper

A slab top on four legs with a pair of rails. It is here for **quantity**: the legs are drawn once,
duplicated and mirrored (four boxes that must merge into one row of 4), and the rails are drawn
**once as a single box that stands for two pieces**, so a cut list that counts boxes instead of
summing quantities would say 1 where the bench needs 2.

```
  y
  ^          42"
  |  +------------------------+  --+
  |  |  +--+============+--+  |    |
  |  |  |leg   rail x2  |leg|  |   12"
  |  |  +--+            +--+  |    |
  |  +------------------------+  --+
  +--------------------------------> x
```

## Stated dimensions

| What | Dimension |
|---|---|
| Top | 42" × 12", 1 1/2" thick, underside at 17" |
| Legs (4) | 3 1/2" square, 17" tall, inset 2" from each edge of the top |
| Rail (2 pieces, one box) | 31" long between the legs, 3/4" thick, 3" tall, upper edge flush with the top's underside |

Overall: 3'-6" × 1'-0" × 1'-6 1/2". Seven pieces from six boxes. Derivations are in
`bench.expected.json`.
