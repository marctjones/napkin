# A wall with a window — the design, as you would write it on paper

Plan view (looking down at a horizontal slice through the wall). X runs to the right along the
wall, Y runs up the page across its thickness, the origin is the wall's south-west corner.

```
  y
  ^            54"              36"              54"
  |   |<--------------->|<--------------->|<--------------->|
  |   +-----------------+-----------------+-----------------+  --+ 5 1/2"
  |   |      wall       |     opening     |      wall       |  --+
  |   +-----------------+-----------------+-----------------+
  |   0"               54"               90"              144"
  +---------------------------------------------------------------> x
```

## Stated dimensions

| What | Dimension | Notes |
|---|---|---|
| Wall length | 12'-0" (144") | |
| Wall thickness, in plan | 5 1/2" | **A dimension of this drawing**, not a value looked up in a standard or a code. |
| Opening width | 3'-0" (36") | one window |
| Opening depth | the full thickness of the wall | both long faces flush with the wall's |
| Opening position | centred on the wall's length | the two reference dimensions either side are derived from that |

## What the scene file holds

`wall-with-window.scene.json` holds two boxes (the wall and the opening), five dimensions —
three driving, two reference — and eight relationships, one of which is the `centered` that puts
the opening in the middle.

For **M1 this is a geometry and labelling fixture only**. Its expectations are entity positions
and the exact feet-inch-fraction text each dimension label should read.

> **No header size, stud count or bracing length is written into this fixture.** Those
> expectations are added in M4 and M5 by a person reading the relevant row of Connecticut's
> published adopted text and citing the page it came from — never from memory, and never from
> napkin's own output. Until then this fixture carries no structural expectation at all.
