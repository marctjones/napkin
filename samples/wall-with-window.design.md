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
| Wall height | **not stated** | see "Height and sill" below |
| Opening height, sill height | **not stated** | see "Height and sill" below |

## Height and sill: not stated, and placeholders in the file

Scene format version 4 requires every box to have a `depth` (for a wall, its height; for an
opening, the opening's height) and an `anchor.z` (for an opening, its sill), and says which face
is up (`docs/design/assembly-model.md` §1.2). **This design has never stated a wall height, an
opening height or a sill**, and none is recoverable from anything earlier in the repository: the
plan view never needed one, and every version of this fixture, its expectations and its design
notes is silent on all three.

The file therefore carries, for both the wall and the opening, `faceUp` `top`, `anchor.z` 0 and
`depth` 768 — exactly what the version-3 reader gave a box that was not a part (the rectangle
tool's 3/4" default). **These are placeholders, not dimensions of this drawing.** They keep the
file loading and change nothing it loaded as; nothing in the expectations depends on them beyond
that they load unchanged, and nothing in napkin reads a wall's height yet (§6). Choosing a real
wall height and a real opening height and sill is a design decision for this fixture's author,
and when one is made it is written into the table above, into the scene, and into
`wall-with-window.expected.json` with its derivation, like every other number here.

## What the scene file holds

`wall-with-window.scene.json` holds two boxes (the wall and the opening), five dimensions —
three driving, two reference — and eight relationships, one of which is the `centered` that puts
the opening in the middle.

The three dimensions across the top are placed to land on one line: the opening's width is offset
512 units from its north edge at 5632, and the two reference dimensions are offset 6144 from the
wall's south corners at 0, so all three sit at y = 6144 and read as a dimension string,
4'-6" | 3'-0" | 4'-6". How `side` and `offset` are read is in
[`docs/file-format.md`](../docs/file-format.md).

Every relationship names a box's place in the plan: the flushes name the two boxes' south and
north side faces, and the reference dimensions and the `centered` name corners of the blank as
`feature` references — `["south", "west"]` is the edge where the south and west faces meet,
which for a box lying as drawn is the plan's south-west corner.

For **M1 this is a geometry and labelling fixture only**. Its expectations are entity positions
and the exact feet-inch-fraction text each dimension label should read.

> **No header size, stud count or bracing length is written into this fixture.** Those
> expectations are added in M4 and M5 by a person reading the relevant row of Connecticut's
> published adopted text and citing the page it came from — never from memory, and never from
> napkin's own output. Until then this fixture carries no structural expectation at all.
