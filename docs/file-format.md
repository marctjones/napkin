# The napkin project file — M1 (format version 1)

This is the public description of what napkin reads. The format is documented regardless of the
app's own license, because an open, documented format is what keeps a project file readable
independent of whether the project is maintained in ten years (DESIGN.md §6.4).

**Status: M1, the reader only.** M1 reads one plain JSON file — no zip, no manifest, no writer.
Saving, the zip container and the manifest arrive in M2 (issue #6, stage 2). What is written here
is what `Napkin.Core.Project.SceneReader` accepts today.

## The rules that matter most

1. **Lengths and angles are integers.** A length is a whole number of 1/1024 inch; an angle is a
   whole number of arcseconds. `30720` is 30 inches. A decimal is never a length: `30720.0`,
   `30720.5` and `3.072e4` are all refused. The reasoning is in
   [`docs/design/geometry-model.md`](./design/geometry-model.md) §1.
2. **Exact format-version match, and no migration.** The reader accepts `"formatVersion": 1` and
   nothing else. A file from an older *or* a newer version is refused before the scene is parsed,
   with a message naming both versions. napkin is a pre-1.0 beta indefinitely: breaking changes
   are always allowed, `formatVersion` is bumped whenever the meaning of the file changes, and no
   migration code or compatibility shim is ever written (DESIGN.md §12).
3. **Reading is strict and never repairs.** An unknown field, a field written twice, an id that is
   not a GUID, an id that names nothing, an id that names the wrong kind of entity, a non-positive
   size, an un-normalised rotation, a duplicated id, two relationships saying the same thing, and a
   relationship kind this build cannot hold are each a refusal naming what was wrong. Nothing is
   opened approximately.
4. **A file must satisfy its own relationships.** After the scene is bound, `Sketch.Validate()` and
   `RelationshipChecker.Check` both run. A file whose geometry does not hold its own stated
   relationships — written by a buggy build, or edited by hand — is refused with the violations
   listed.

## The document

```json
{
  "formatVersion": 1,
  "units": { "length": "inch/1024", "angle": "arcsecond" },
  "layers": [ … ],
  "entities": [ … ],
  "relationships": [ … ]
}
```

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | integer | Exactly `1`. Judged before anything else is read. |
| `units` | object | `length` is exactly `"inch/1024"`, `angle` is exactly `"arcsecond"`. The unit is named in the file so that a reader never has to assume one. |
| `layers` | array | Every layer, in the order the UI shows them. |
| `entities` | array | Every entity, in any order; ids may be referred to before they appear. |
| `relationships` | array | Every relationship, in any order. |

Every field listed in this document is required. There are no optional fields: a reference
dimension writes `"drives": null` rather than leaving the field out, so that the writer and the
reader always agree on the shape.

**Ids are GUIDs** written in the canonical 8-4-4-4-12 form, for example
`10000000-0000-4000-8000-000000000001`. Braces, `N` form and other spellings are refused. Ids are
stable for the life of an entity: they survive save/load and undo/redo, and they are what
relationships and dimensions refer to.

### Layers

```json
{ "id": "00000000-0000-0000-0000-000000000001", "name": "Default" }
```

The layer `00000000-0000-0000-0000-000000000001` is napkin's default layer. Every entity names a
layer the file defines.

### Entities

Every entity has `id`, `type` and `layer`. `type` is one of `box`, `dimension`, `node`,
`segment`.

```json
{ "id": "…", "type": "box", "layer": "…",
  "anchor": { "x": 0, "y": 0 }, "width": 30720, "height": 3584, "rotation": 0 }
```

| `type` | Fields |
|---|---|
| `node` | `position`: `{ "x": <integer>, "y": <integer> }` |
| `segment` | `start`, `end`: ids of two `node` entities |
| `box` | `anchor`: a point, the box's south-west corner in its own frame; `width` and `height`: integers greater than zero, along the box's local X and Y; `rotation`: arcseconds, `0 ≤ rotation < 1296000` |
| `dimension` | `measures`, `drives`, `placement` — below |

A box is parametric: it stores the width and height that were typed and derives its corners, so a
30-inch part stays 30 inches whatever its rotation. In plan view a wall is a box whose `width` is
its length and whose `height` is its thickness; an opening is a box related to its wall.

**Rotation is stored normalised.** `Angle` normalises into [0°, 360°), so a file saying `1296000`
would load as `0` and the sketch would no longer equal the file; such a file is refused rather
than quietly normalised.

### Dimensions

```json
{ "id": "…", "type": "dimension", "layer": "…",
  "measures": { "kind": "boxWidth", "box": "…" },
  "drives": "…",
  "placement": { "offset": 8192, "side": "north" } }
```

- `measures` is either a **size reference** (`boxWidth`, `boxHeight`, `segmentLength` — the shapes
  below) or a **span**: `{ "kind": "axis", "from": <point>, "to": <point>, "axis": "x" }`.
- `drives` is the id of the `paramValue` or `axisDistance` relationship that owns the number, or
  `null` for a reference dimension. **A dimension never stores a length of its own**: its value is
  always computed from the geometry it measures (geometry design §3.3).
- `placement` is canvas-only: `offset` (integer units) and `side` (`north`, `south`, `east`,
  `west`). It never affects geometry — getting it wrong moves a line, never a part.

**What `side` means to the viewer.** `src/Napkin.App/Viewing/DimensionLayout.cs` reads it twice,
and a file's placements are worth writing with that in mind:

1. **Which corners a size is measured between.** A `boxWidth` placed `north` measures the box's two
   *north* corners and otherwise its two south corners; a `boxHeight` placed `east` measures the
   east corners and otherwise the west ones. The extension lines then run from the near edge
   outwards instead of across the part. The number is the same either way — a box's width is what
   it stores — so this is about the drawing, not the value.
2. **Where the dimension line sits.** For a measurement running along X, `south` puts the line
   `offset` below the lower end and any other side puts it `offset` above the upper end; along Y,
   `west` puts it `offset` left of the leftmost end and any other side `offset` right of the
   rightmost. A side parallel to the measurement falls back to that axis's default rather than
   drawing the line through the geometry.

Both samples are placed against that reading. In `wall-with-window` the opening's width carries
`"offset": 512` on `north`, so its line lands at y = 5632 + 512 = 6144 — the same height as the two
reference dimensions either side of it, which measure from corners at y = 0 with `"offset": 6144`.
The three then read as one dimension string: 4'-6" | 3'-0" | 4'-6".

**There is no display name on an entity.** The format stores ids, geometry and relationships; what
a part is *called* ("Leg, south-west") lives in the fixture's expectations file and, in the app, in
the viewer's own side table. If a name ever belongs in the file it is a new field on the entity and
a `formatVersion` bump, because it changes what the file means.

### References

Every reference is an object with a `kind`, so that no shape has to be guessed from which fields
are present.

| Refers to | `kind` | Fields |
|---|---|---|
| a point | `node` | `node`: a node's id |
| | `corner` | `box`: a box's id; `corner`: `southWest`, `southEast`, `northEast`, `northWest` |
| | `center` | `box`: a box's id |
| an edge | `segment` | `segment`: a segment's id |
| | `boxEdge` | `box`: a box's id; `edge`: `south`, `east`, `north`, `west` |
| a size | `boxWidth` | `box`: a box's id |
| | `boxHeight` | `box`: a box's id |
| | `segmentLength` | `segment`: a segment's id |

Corners and edges are named in the box's own local frame, before rotation. An axis is `x` or `y`.

### Relationships

Every relationship has `id` and `kind`. Relationships are stored, never inferred from position:
two corners at the same coordinates are not coincident unless a `coincident` says so.

**Held by this build** (the rectilinear set the direct updater handles):

| `kind` | Fields | Meaning |
|---|---|---|
| `anchored` | `entity` | The entity does not move in response to other entities. |
| `coincident` | `a`, `b`: points | Two points are the same point. |
| `horizontal`, `vertical` | `edge` | A segment is axis-aligned. |
| `flush` | `a`, `b`: edges | Two parallel axis-aligned edges lie on the same line. |
| `axisDistance` | `from`, `to`: points; `axis`; `distance`: integer | Signed distance `to − from` along one axis. This is what a driving linear dimension between two points is. |
| `paramValue` | `param`: a size; `value`: integer | A size is held at a value. This is what a driving dimension on a part's size is, and it is the one owner of that number. |
| `equalParam` | `a`, `b`: sizes | Two sizes are equal — four identical legs. |
| `centered` | `middle`, `a`, `b`: points; `axis` | `middle` is midway between `a` and `b` along the axis. |

Two of those load but are not editable: the direct updater treats `horizontal` and `vertical` on a
box edge, and `paramValue`/`equalParam` over a `segmentLength`, as things it can check but not
propagate, and it refuses a *geometry request* on a sketch holding one (geometry design §10.3).
M1 is read-only, so such a file opens and draws; M2's editing has to say so.

**Reserved for the constraint solver** (#28). These kinds are part of the format so that files and
the UI have names for them, but a build without the solver refuses a file containing one, naming
the kind rather than crashing: `parallel` and `perpendicular` (`a`, `b`: edges), `angleBetween`
(`a`, `b`: edges; `angle`: arcseconds), `distance` (`a`, `b`: points; `value`), `pointOnEdge`
(`point`, `edge`), `symmetric` (`a`, `b`: points; `mirror`: an edge), `tangent` (`a`, `b`: edges)
and `radius` (`arc`, `value`).

## An annotated example

A 12-foot wall, 5½ inches thick, with a 3-foot opening centred on it — the
[`samples/wall-with-window.scene.json`](../samples/wall-with-window.scene.json) fixture, shortened.

```jsonc
{
  "formatVersion": 1,                                  // exactly 1, judged first
  "units": { "length": "inch/1024", "angle": "arcsecond" },
  "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
  "entities": [
    // The wall: 144" x 1024 = 147456 units long, 5.5" x 1024 = 5632 units thick.
    { "id": "30000000-0000-4000-8000-000000000001", "type": "box",
      "layer": "00000000-0000-0000-0000-000000000001",
      "anchor": { "x": 0, "y": 0 }, "width": 147456, "height": 5632, "rotation": 0 },

    // The opening: 36" = 36864 units wide, the full thickness of the wall, starting 54" along.
    { "id": "30000000-0000-4000-8000-000000000002", "type": "box",
      "layer": "00000000-0000-0000-0000-000000000001",
      "anchor": { "x": 55296, "y": 0 }, "width": 36864, "height": 5632, "rotation": 0 },

    // A driving dimension: the relationship named in "drives" owns the number 147456; this
    // annotation draws it, as 12'-0".
    { "id": "30000000-0000-4000-8000-000000000003", "type": "dimension",
      "layer": "00000000-0000-0000-0000-000000000001",
      "measures": { "kind": "boxWidth", "box": "30000000-0000-4000-8000-000000000001" },
      "drives": "40000000-0000-4000-8000-000000000002",
      "placement": { "offset": 12288, "side": "south" } },

    // A reference dimension: it measures the span from the wall's west end to the opening and
    // owns nothing, so "drives" is null. It reads 4'-6".
    { "id": "30000000-0000-4000-8000-000000000006", "type": "dimension",
      "layer": "00000000-0000-0000-0000-000000000001",
      "measures": { "kind": "axis",
        "from": { "kind": "corner", "box": "30000000-0000-4000-8000-000000000001", "corner": "southWest" },
        "to":   { "kind": "corner", "box": "30000000-0000-4000-8000-000000000002", "corner": "southWest" },
        "axis": "x" },
      "drives": null,
      "placement": { "offset": 6144, "side": "north" } }
  ],
  "relationships": [
    { "id": "40000000-0000-4000-8000-000000000002", "kind": "paramValue",
      "param": { "kind": "boxWidth", "box": "30000000-0000-4000-8000-000000000001" },
      "value": 147456 },

    // The opening runs the full thickness of the wall …
    { "id": "40000000-0000-4000-8000-000000000006", "kind": "flush",
      "a": { "kind": "boxEdge", "box": "30000000-0000-4000-8000-000000000001", "edge": "south" },
      "b": { "kind": "boxEdge", "box": "30000000-0000-4000-8000-000000000002", "edge": "south" } },

    // … and sits in the middle of it: the opening's centre is midway between the wall's two ends.
    { "id": "40000000-0000-4000-8000-000000000008", "kind": "centered",
      "middle": { "kind": "center", "box": "30000000-0000-4000-8000-000000000002" },
      "a": { "kind": "corner", "box": "30000000-0000-4000-8000-000000000001", "corner": "southWest" },
      "b": { "kind": "corner", "box": "30000000-0000-4000-8000-000000000001", "corner": "southEast" },
      "axis": "x" }
  ]
}
```

(The comments are for this document. The reader rejects JSON comments and trailing commas.)

## Reading a file

```csharp
LoadResult result = SceneReader.ReadFile("samples/wall-with-window.scene.json");

switch (result)
{
    case Loaded loaded:
        Draw(loaded.Sketch);
        break;
    case Refused refused:
        ShowTheUser(refused.Summary);   // every problem, each naming what was wrong
        break;
}
```

`SceneReader.Read(Stream)` is the same thing over bytes. Both take an optional `IGeometryUpdater`
whose `SupportedRelationships` decide which relationship kinds this build can hold; the default is
the direct updater. Every refusal is a `Refused` carrying a list of `LoadProblem`s — a kind, a
location such as `/entities/3/width`, and a message. Nothing is thrown for a bad file; exceptions
are for a caller that passed nonsense arguments.

## What changes in M2

Saving arrives with the writer, and with it the container from DESIGN.md §6.4:

```
project.napkin/         (a zip, readable with any zip tool)
  manifest.json         formatVersion, app version, units, the project's adopted code
  scene.json            exactly the scene body described above
  thumbnail.png
  assets/
```

The seam is already in place: `FormatStamp` — `formatVersion` and `units` — is read as its own
record. In M1 it is the head of the scene file; in M2 it moves into `manifest.json` and gains the
app version and the adopted code, and the scene body below it does not change. The writer is the
mirror image of the reader and shares its one table of field names, so a name cannot change on one
side only.

`Load(Save(sketch)) == sketch` by value is M2's acceptance test (property P9 of the geometry
design). It is not tested here because there is no writer yet.

## Divergences from the geometry design's §6 sketch

The geometry design set out the scene format in outline. Three things are settled differently
here, each because the outline is ambiguous under strict reading:

1. **Every reference carries an explicit `kind`.** §6's example writes a box-edge reference as
   `{ "box": …, "edge": … }` and a size as `{ "kind": "boxWidth", "box": … }`. Telling a
   `center` reference (`{ "box": … }`) from a `corner` reference by which fields are present is
   exactly the sort of guess that strict reading is meant to remove, so every point, edge and size
   reference names its kind.
2. **The stamp is in the scene file in M1.** §6 puts `formatVersion` and `units` in
   `manifest.json`, which does not exist until M2. Without them in the file there would be nothing
   to refuse an old file with, so they head the scene document and move to the manifest in M2.
3. **A dimension's `measures` is the size reference itself for a size**, as §6's example shows,
   and `{ "kind": "axis", … }` for a span. There is no wrapper object, and `axis` is therefore not
   available as the name of a size reference.

One thing the reader deliberately does **not** check: that a driving dimension's `drives`
relationship is about the same measurand the dimension measures. Nothing in the design requires
it, and a mismatch is a UI bug rather than a corrupt file. If that changes, `formatVersion` does
not: it is a stricter reading of the same file, and it would be recorded here.
