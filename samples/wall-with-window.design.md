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
| Wall height | 8'-0" (96") | Marc, 2026-09-23 |
| Opening height | 3'-6" (42") | Marc, 2026-09-23 |
| Sill height | 3'-0" (36") | Marc, 2026-09-23 |

## Height and sill

Scene format version 4 requires every box to have a `depth` (for a wall, its height; for an
opening, the opening's height) and an `anchor.z` (for an opening, its sill), and says which face
is up (`docs/design/assembly-model.md` §1.2). **This design stated no wall height, no opening
height and no sill from M1 through format version 4 landing** — the plan view never needed one —
so the file briefly carried placeholder values (`anchor.z` 0, `depth` 768) exactly matching what
the version-3 reader gave a box that was not a part.

Marc supplied the three real values on 2026-09-23, a design decision for this fixture with no
primary source to cite (it is not a building-code value): a standard 8'-0" residential wall
height, a 3'-0" sill, and a 3'-6" opening height, which puts the opening's top at 3'-0" + 3'-6" =
6'-6" (78"), leaving 1'-6" (18") of wall above it for a header under the 8'-0" (96") wall — a
plausible, if unchecked, header allowance; this fixture is a geometry and labelling test (M1), not
a structural one, so nothing here asserts a header actually fits. The wall's `anchor.z` is 0 (it
stands on the floor); its `depth` is its height, 96" = 98304 units. The opening's `anchor.z` is
its sill, 36" = 36864 units; its `depth` is its height, 42" = 43008 units. Both boxes stay
`faceUp: top` — the wall and the opening are drawn as drawn, not turned. These values and their
derivations are in `wall-with-window.expected.json`.

No relationship ties the wall's height to the opening's height or sill — unlike the opening's
in-plan depth, which an `equalParam` ties to the wall's thickness — so raising the wall or moving
the sill does not (yet) move the other; each is simply stated. That is consistent with
shaped-parts §11.7's and the coffee-table fixture's precedent: a fixture's stated dimensions are
not redesigned by a feature landing, only made expressible by one.

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
