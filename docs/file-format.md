# The napkin project file — container version 1, scene format version 4

This is the public description of what napkin reads and writes. The format is documented
regardless of the app's own license, because an open, documented format is what keeps a project
file readable independent of whether the project is maintained in ten years (DESIGN.md §6.4).

**Status: M2, reader and writer.** There are two ways into a napkin drawing, and both are
supported:

| | What it is | Read by | Written by |
|---|---|---|---|
| `*.napkin` | A zip container holding `manifest.json` and `scene.json` | `ProjectFile.Load` | `ProjectFile.Save` |
| `*.scene.json` | One plain scene document, on its own | `SceneReader.Read` | `SceneWriter.Write` |

The scene document is the same either way — the container wraps it, it does not change it. A file
written by hand in a text editor, such as the three in [`samples/`](../samples), is a plain scene
document and stays one.

## The rules that matter most

1. **Lengths and angles are integers.** A length is a whole number of 1/1024 inch; an angle is a
   whole number of arcseconds. `30720` is 30 inches. A decimal is never a length: `30720.0`,
   `30720.5` and `3.072e4` are all refused. The reasoning is in
   [`docs/design/geometry-model.md`](./design/geometry-model.md) §1.
2. **Exact version match, and no migration — on both stamps.** A project carries two version
   numbers, for two different things: `containerVersion` in `manifest.json` says what shape the
   container is, and `formatVersion` in `scene.json` says what a drawing means. The reader accepts
   `"containerVersion": 1` and `"formatVersion": 4` and nothing else. A file from an older *or* a
   newer version of either is refused before the scene is parsed, with a message naming both
   versions. napkin is a pre-1.0 beta indefinitely: breaking changes are always allowed, each
   stamp is bumped whenever its own layer changes meaning, and no migration code or compatibility
   shim is ever written (DESIGN.md §12).

   **Version 2** (issue #8) added a `name` to every entity and a `part` to every box, which is what
   a cut list needs and a plan view cannot hold. Every version-1 file is refused, including the two
   this repository had committed until they were rewritten in the same change — that is the beta
   policy working as designed rather than an accident.

   **Version 3** added a `cuts` array to every box, which is what a part that is not a plain
   rectangle needs ([`docs/design/shaped-parts-model.md`](./design/shaped-parts-model.md) §5).
   Every version-2 file is refused, including the two this repository had committed until they
   were rewritten in the same change, and every project saved since version 2 landed. The *model*
   is a strict superset — a box with no cuts is the box version 2 described, in every respect —
   but the format has no optional fields, so a plain rectangle writes `"cuts": []` and a file
   without the field is not a version-3 file.

   **Version 4** put every box in space
   ([`docs/design/assembly-model.md`](./design/assembly-model.md) §10): a box's `anchor` gained a
   `z`, and the box gained a `depth` and a `faceUp`; a part lost `outOfPlane`, whose value is the
   box's `depth`; and the `corner` and `boxEdge` references gave way to one `feature` reference
   naming the faces of a box that meet at a place. Every version-3 file is refused, including the
   three this repository had committed until they were rewritten in the same change. The *model*
   is again a strict superset — a box lying as drawn (`"faceUp": "top"`) on the floor (`"z": 0`)
   is the box version 3 described, in every respect — and again the format has no optional fields.
3. **Reading is strict and never repairs.** An unknown field, a field written twice, an id that is
   not a GUID, an id that names nothing, an id that names the wrong kind of entity, a non-positive
   size, an un-normalised rotation, cuts out of site order, a cut that does not fit the blank it is
   on, a feature whose faces are repeated, opposite or out of order, two places a relationship
   cannot compare, a dimension that measures out of the plan, a duplicated id, two relationships
   saying the same thing, and a relationship kind this build cannot hold are each a refusal naming
   what was wrong. Nothing is opened approximately.
4. **A file must satisfy its own relationships.** After the scene is bound, `Sketch.Validate()` and
   `RelationshipChecker.Check` both run. A file whose geometry does not hold its own stated
   relationships — written by a buggy build, or edited by hand — is refused with the violations
   listed.
5. **What is written is a function of the drawing and nothing else.** The same drawing saved twice
   by the same build gives byte-identical files: no timestamps, no counters, no machine name, a
   fixed field order, and entities and relationships sorted by id. A project file therefore diffs
   cleanly in git, and "has this changed?" is a checksum rather than an opinion.

## The container

```
table.napkin            a zip, readable with any zip tool
  manifest.json         containerVersion, appVersion, adoptedCode
  scene.json            the scene document below, unchanged
```

Two entries, and no others. An entry this document does not define — including the `thumbnail.png`
and `assets/` that DESIGN.md §6.4 reserves for later and this build does not write — is a refusal,
for the same reason an unknown field is: with the container version pinned there is no legitimate
reason for one. Adding them is a `containerVersion` bump.

### `manifest.json`

```json
{
  "containerVersion": 1,
  "appVersion": "0.5.0-beta+1a2b3c4",
  "adoptedCode": null
}
```

| Field | Type | Meaning |
|---|---|---|
| `containerVersion` | integer | Exactly `1`. Judged before anything else in the project is read. |
| `appVersion` | string | The build that wrote the file, so a bug report can name it. Recorded, never judged: a project written by any version of napkin opens, as long as its two stamps match. |
| `adoptedCode` | object or `null` | The adopted building code the project is locked to, or `null` when none is chosen. |

**`adoptedCode` is reserved and is not interpreted.** When it is not `null` it is
`{ "pack": "us-ct-2026", "revision": 1 }` — a non-empty pack id and a revision counting up from
zero. Its *shape* is checked and nothing else: this build does not know what a pack id means, and
must not refuse a project for naming a pack it has never heard of. It is read, carried, and
written back unchanged. What it will mean is
[`docs/design/rules-engine-model.md`](./design/rules-engine-model.md) §7.1, which is a **draft**;
the field exists now so that a project drawn against a code has somewhere to say so.

### Why the units are not in the manifest

The geometry design's §6 sketch put `formatVersion` and `units` in the manifest. They are in the
scene document instead, and the manifest versions the container. That keeps each stamp next to
what it describes and lets the two move independently: adding a thumbnail entry bumps
`containerVersion` and leaves every scene readable, and renaming a relationship field bumps
`formatVersion` and leaves the container alone. It is also what lets a plain `scene.json` still be
a complete, self-describing file with no manifest anywhere near it.

### What a container is refused for

Each of these is a refusal naming what was wrong, never a repair:

| Refused | Why |
|---|---|
| The bytes are not a zip, or the zip is truncated or corrupt | A plain scene document handed to `ProjectFile` is refused here, with a message pointing at `SceneReader`. Nothing is sniffed. |
| No `manifest.json`, or no `scene.json` | Both are required. |
| An entry the format does not define | Strict, as above. |
| The same entry name twice | Which one is the project is not something to guess at. |
| An entry name that could leave the container | Rooted, containing `..`, containing a backslash or a drive letter, a directory entry, or a control character. Nothing is extracted to disk today; the check is there so that it is already there the day something is. |
| More than 16 entries | Refused before any entry is read. |

### Limits

A zip entry's stated size is written by whoever made the file, so it is a claim, not a
measurement. Each limit below is checked against what the entry claims *and* enforced again as the
bytes are decompressed, so a small file that expands to gigabytes stops at the limit instead of
filling memory.

| Limit | Value | Where |
|---|---|---|
| The whole `.napkin` file | 32 MB | Checked before the file is read at all. |
| `scene.json`, decompressed | 16 MB | The largest sample is about 12 kB, so this is room for a drawing far larger than a house. |
| `manifest.json`, decompressed | 64 kB | |
| Entries in the container | 16 | The format defines two. |

They are constants on `Napkin.Core.Project.ContainerLimits`. Raising one is a decision to make
deliberately.

### Saving is atomic

The whole container is built in memory, written to a temporary file *in the same directory* as the
target, flushed all the way to disk, and only then moved over the target. A failure at any point —
no room, no permission, a pulled cable — leaves the previous file exactly as it was, and leaves no
temporary behind. There is no moment at which the file on disk is half a drawing. The temporary
shares a directory with the target because a move across a filesystem boundary is a copy, and a
copy is not atomic.

A save also refuses a drawing that does not pass `Sketch.Validate()`, rather than writing a file
this build could not open again.

### Reproducible bytes, and where that stops

Saving the same drawing twice **with the same build** produces identical bytes. What makes that
true: the two entries are written in a fixed order; every entry carries the zip epoch
(1980-01-01 00:00) instead of a real modification time, which is a DOS time and so carries no time
zone; no extra fields are written; and both JSON documents are themselves deterministic — a fixed
field order, entities sorted by id, relationships sorted by id, integers only, `\n` line endings
on every platform, and a trailing newline.

Two places where the bytes legitimately differ:

- **Across builds.** `appVersion` is the build that wrote the file, and CI adds the commit SHA, so
  a file saved by a different commit differs in the manifest. That is the field doing its job.
- **Possibly across machines and runtimes.** The entries are DEFLATE-compressed, and what a
  compressor emits is the runtime's business: .NET's deflate is native zlib-ng, which may take a
  different code path on a different CPU, and a future runtime may compress differently again.
  What is tested, and what "save twice and compare" and a git diff actually need, is that the same
  drawing saved twice by the same build on the same machine gives the same bytes. Identity across
  machines or across runtime versions is **not** claimed and has **not** been measured.

## The scene document

```json
{
  "formatVersion": 4,
  "units": { "length": "inch/1024", "angle": "arcsecond" },
  "layers": [ … ],
  "entities": [ … ],
  "relationships": [ … ]
}
```

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | integer | Exactly `4`. Judged before anything else is read. |
| `units` | object | `length` is exactly `"inch/1024"`, `angle` is exactly `"arcsecond"`. The unit is named in the file so that a reader never has to assume one. |
| `layers` | array | Every layer, in the order the UI shows them. |
| `entities` | array | Every entity, in any order; ids may be referred to before they appear. |
| `relationships` | array | Every relationship, in any order. |

Every field listed in this document is required. There are no optional fields: a reference
dimension writes `"drives": null` and a box that is a plain rectangle writes `"cuts": []`, rather
than leaving the field out, so that the writer and the reader always agree on the shape.

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

Every entity has `id`, `type`, `layer` and `name`. `type` is one of `box`, `dimension`, `node`,
`segment`.

```json
{ "id": "…", "type": "box", "layer": "…", "name": "Leg, south-west",
  "anchor": { "x": 1536, "y": 1536, "z": 0 },
  "width": 2560, "height": 2560, "depth": 16640,
  "faceUp": "top", "rotation": 0,
  "part": { "stock": null, "species": null, "quantity": 1,
            "planAxes": { "x": "width", "y": "thickness" } },
  "cuts": [] }
```

| `type` | Fields |
|---|---|
| `node` | `position`: `{ "x": <integer>, "y": <integer> }` — a node is plan construction geometry, at the plan datum, and has no `z` |
| `segment` | `start`, `end`: ids of two `node` entities |
| `box` | `anchor`: `{ "x", "y", "z" }`, three integers — the box's south-west-bottom corner in its own frame; `width`, `height` and `depth`: integers greater than zero, along the box's local X, Y and Z; `faceUp`: which of its six faces points up, one of `top`, `bottom`, `north`, `south`, `east`, `west`; `rotation`: arcseconds, `0 ≤ rotation < 1296000`; `part`: below, or `null`; `cuts`: below, `[]` for a plain rectangle |
| `dimension` | `measures`, `drives`, `placement` — below |

A box is parametric: it stores the three sizes that were typed and derives its corners, so a
30-inch part stays 30 inches however it is turned. In plan view a wall is a box whose `width` is
its length, whose `height` is its thickness and whose `depth` is its height; an opening is a box
related to its wall, its `depth` the opening's height and its `anchor.z` its sill.

**Where a box is, and which way it is turned.** The three sizes are along the box's *own* axes;
`faceUp` and `rotation` say how those axes sit in the world, and `anchor` is where the box's own
origin is. `faceUp` is the face tipped to point up — `top` is the box as drawn, `bottom` turned
over, `north` tipped back, `south` tipped forward, `east` and `west` tipped over sideways — and
`rotation` is then the spin about the vertical, through the anchor, that the plan view has always
shown. Six faces and four quarter turns are the 24 orientations a block can take, each with one
spelling. Which world axis each of the box's own axes lands on for each `faceUp` is fixed by the
table in [`docs/design/assembly-model.md`](./design/assembly-model.md) §1.3, and every corner is
an exact sum of the anchor and the sizes. X runs east, Y north, Z up; a box lying as drawn on the
floor has `"faceUp": "top"` and `"z": 0`.

**`height` is not how tall the box stands.** It is the size along the box's own Y — the plan's
depth of its footprint — and has been since the first version; the size along its own Z, which
for a box lying as drawn is what a person would call its height or thickness, is `depth`. The
names are the 3D-graphics convention (width × height × depth).

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

- `measures` is either a **size reference** (`boxWidth`, `boxHeight`, `boxDepth`,
  `segmentLength` — the shapes below) or a **span**: `{ "kind": "axis", "from": <place>, "to":
  <place>, "axis": "x" }`.
- **A dimension lies in the plan** (`docs/design/assembly-model.md` §7.3, invariant 13): what it
  measures must lie along world X or Y once the box it belongs to is turned. A span's `axis` is
  `x` or `y` and both of its places must fix that axis; a size is refused when the box's `faceUp`
  stands that size vertical — `boxDepth` on a box lying as drawn, `boxWidth` on a box standing on
  its `east` or `west` face, `boxHeight` on one standing on `north` or `south`. Such a number is not
  lost: it is held by a `paramValue` or an `axisDistance`, which have no drawing to fit.
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

The samples are placed against that reading. In `wall-with-window` the opening's width carries
`"offset": 512` on `north`, so its line lands at y = 5632 + 512 = 6144 — the same height as the two
reference dimensions either side of it, which measure from corners at y = 0 with `"offset": 6144`.
The three then read as one dimension string: 4'-6" | 3'-0" | 4'-6".

### Names

`name` is a string on **every** entity, not only on a box — a named dimension reads better in a
conflict message too. An empty string is legal and means unnamed; only a non-string is refused.

A name is **not an id and is not unique**: four legs may all be called "Leg". Nothing looks an
entity up by name, and the cut list groups parts by their dimensions rather than by what they are
called (`docs/design/parts-and-cut-list.md` §3).

### Parts

`part` is a required field on a box. It is `null` on a box that is not a piece anybody cuts — a
wall, an opening — and otherwise an object with exactly these four fields:

```json
"part": {
  "stock": "2x4",
  "species": "Douglas fir",
  "quantity": 1,
  "planAxes": { "x": "width", "y": "thickness" }
}
```

| Field | Type | Meaning | Refused when |
|---|---|---|---|
| `stock` | string or `null` | A nominal name the materials library resolves, spelled however a yard spells it: `"2 x 4"` and `"2x4"` normalise to one stock | it is neither a string nor `null` |
| `species` | string or `null` | Free text, set after placing. Never interpreted by this build | it is neither a string nor `null` |
| `quantity` | integer | How many identical copies this one box stands for, for the four legs a person draws once. At least 1 | it is not an integer, or is less than 1 |
| `planAxes` | object | `x` and `y`, each exactly one of `length`, `width`, `thickness` | a key is missing, a value is not one of the three, or `x` and `y` name the same one |

A part written with the `outOfPlane` field version 3 had is refused as an unknown field.

**A part has three finished dimensions and the box stores all three.** `planAxes` says which of
`length`, `width` and `thickness` the box's `width` is and which its `height` is; the remaining
name is its `depth`. Nothing is stored twice, so a part's listed size cannot drift from the box
the person is drawing, and a part lists the same however it is turned or wherever it is placed:
the cut list reads the three sizes and never `anchor`, `faceUp` or `rotation`. A typed depth is a
`paramValue` on a `boxDepth` size, as a typed width is on `boxWidth`.

**`stock` is a name, not an id, and is not validated at load.** The reader checks that it is a
string; it does *not* check that this build's materials library carries it — the same stance the
manifest already takes on `adoptedCode`. A project drawn against a stock table a later build
renames still opens, and the cut list says the name did not resolve rather than guessing. A file is
refused for being malformed, never for naming something this build has not heard of.

### Cuts

`cuts` is a required field on a box. It is `[]` on a plain rectangle — which is every box in the
coffee table and the wall, and all but the top of the rounded-corner table — and otherwise lists
what has been cut off the blank, one object per cut. A cut is drawn on the box's own X–Y face and
runs square through its whole `depth`, so it turns with the box:

```json
"cuts": [
  { "kind": "roundedCorner", "corner": "southWest", "radius": 1024 },
  { "kind": "cornerCut",     "corner": "southEast", "alongX": 3072, "alongY": 5120 },
  { "kind": "curvedEdge",    "edge": "north", "bow": "inward", "depth": 2048 }
]
```

| `kind` | Fields | Meaning | Refused when |
|---|---|---|---|
| `cornerCut` | `corner`; `alongX`, `alongY`: integers | A straight cut across a corner, from the point `alongX` from it on the edge running along local X to the point `alongY` from it on the edge running along local Y. A clipped corner, a mitred end, a taper, a diagonal | a value is not a positive integer, or the cut does not fit the blank |
| `roundedCorner` | `corner`; `radius`: integer | A quarter-circle tangent to both edges `radius` from the corner | as above |
| `curvedEdge` | `edge`; `bow`: `outward` or `inward`; `depth`: integer | The whole edge replaced by a circular arc through three points on the grid. `outward` keeps the middle of the edge and brings the two corners in by `depth`; `inward` keeps the corners and takes the middle in by `depth` | as above, or `bow` is neither of the two |

**A box holds the blank, not the shape.** `width` and `height` stay the dimensions of the
rectangular board the part is cut from, and the outline is derived from them and the cuts — so a
taper never shrinks the size the cut list reads, and a file says what a person decided rather than
what that produced. The reasoning is
[`docs/design/shaped-parts-model.md`](./design/shaped-parts-model.md) §1.1.

**Corners and edges are named in the box's own local frame**, before it is turned:
`southWest`, `southEast`, `northEast`, `northWest` for a corner, and `south`, `east`, `north`,
`west` for an edge. Every stored value is a whole number of units, like every other length.

**Cuts are stored in site order**, which is `southWest`, `southEast`, `northEast`, `northWest`,
`south`, `east`, `north`, `west`. A file out of that order is refused rather than quietly
re-ordered, for the same reason an un-normalised rotation is: the model sorts, so such a file
would load as a sketch that no longer equals it, and a shape has one spelling.

**One cut per site, and a curved edge owns its corners.** No two cuts may name the same corner or
edge; nothing may be cut at either end of a `curvedEdge`, and two edges that meet cannot both be
curved. A clipped-then-rounded corner is not expressible, on purpose.

**A cut must fit the blank it is on**, which is three things together and each of them a refusal:
every value is positive and no longer than the edge it is set back along; the two cuts at the ends
of an edge claim no more than its length between them, and a curve and whatever faces it across
the blank claim no more than the size between them; and what is left has positive area. Two
full-diagonal corner cuts at opposite corners pass the first two and leave nothing, which the
third catches.

### References

Every reference is an object with a `kind`, so that no shape has to be guessed from which fields
are present.

| Refers to | `kind` | Fields | Fixes |
|---|---|---|---|
| a place | `node` | `node`: a node's id | X and Y |
| | `segment` | `segment`: a segment's id | Y when it is horizontal, X when it is vertical |
| | `center` | `box`: a box's id | X, Y and Z |
| | `feature` | `box`: a box's id; `faces`: one, two or three of `south`, `east`, `north`, `west`, `bottom`, `top` | one axis per face |
| a size | `boxWidth` | `box`: a box's id | |
| | `boxHeight` | `box`: a box's id | |
| | `boxDepth` | `box`: a box's id | |
| | `segmentLength` | `segment`: a segment's id | |

An axis is `x`, `y` or `z`.

**A feature is the faces that meet at it** (`docs/design/assembly-model.md` §1.5): one face, the
edge where two meet, or the corner where three meet, named in the box's own frame before it is
turned — `bottom` is its own z = 0 and `top` its own z = `depth`. So `["north"]` is a face,
`["south", "west"]` the edge along the box's own Z at the blank's south-west corner (what
version 3 called a `corner`), and `["south", "west", "top"]` the top of that edge. The faces are
written in the order `south`, `east`, `north`, `west`, `bottom`, `top`, so that a feature has one
spelling; a file with them in another order, with a face named twice, with two opposite faces
(`south` and `north`, `east` and `west`, `bottom` and `top` never meet), or with none or more than
three, is refused rather than repaired. The `corner` and `boxEdge` kinds of version 3 are refused,
and the message says what replaced them.

**What a place fixes, in the world.** A face is perpendicular to one world axis and fixes its
coordinate on it; which axis depends on how the box is turned — a box's `top` fixes Z when it
lies as drawn and X when it stands on its `east` face. An edge fixes the two axes its faces do, a
corner all three. A `center` fixes all three, rounding by half a unit on an odd size.

**Whether two places can be related is judged at load, by what each fixes** (§2.3): a
`coincident` needs two or three axes both places fix — two corners, two parallel edges, a node and
an edge standing upright, never a face; a `flush` needs two planes, both places fixing exactly
one axis and the same one; an `axisDistance` needs both places to fix its axis, and a `centered`
all three. A pairing that fails — a `flush` between one box's `east` face and another's `north`,
or a `flush` of a face with an edge — is refused as an invalid value naming both places and what
each fixes.

### Relationships

Every relationship has `id` and `kind`. Relationships are stored, never inferred from position:
two corners at the same coordinates are not coincident unless a `coincident` says so, and a leg
standing exactly under a top is not held there unless a `flush` says so.

**Held by this build** (the rectilinear set the direct updater handles):

| `kind` | Fields | Meaning |
|---|---|---|
| `anchored` | `entity` | The entity does not move in response to other entities. |
| `coincident` | `a`, `b`: places | The same place: equal on every axis both fix. |
| `horizontal`, `vertical` | `edge`: a `segment` or a `feature` | A line is axis-aligned. |
| `flush` | `a`, `b`: places | The same plane: two faces coplanar, or a face and an axis-aligned segment. |
| `axisDistance` | `from`, `to`: places; `axis`; `distance`: integer | Signed distance `to − from` along one axis, `x`, `y` or `z`. Along `x` or `y` it is what a driving linear dimension between two places is. |
| `paramValue` | `param`: a size; `value`: integer | A size is held at a value. This is what a driving dimension on a part's size is, and it is the one owner of that number. |
| `equalParam` | `a`, `b`: sizes | Two sizes are equal — four identical legs. |
| `centered` | `middle`, `a`, `b`: places; `axis` | `middle` is midway between `a` and `b` along the axis, `x`, `y` or `z`. |

Two of those load but are not editable: the direct updater treats `horizontal` and `vertical` on a
box edge, and `paramValue`/`equalParam` over a `segmentLength`, as things it can check but not
propagate, and it refuses a *geometry request* on a sketch holding one (geometry design §10.3).
M1 is read-only, so such a file opens and draws; M2's editing has to say so.

**Reserved for the constraint solver** (#28). These kinds are part of the format so that files and
the UI have names for them, but a build without the solver refuses a file containing one, naming
the kind rather than crashing: `parallel` and `perpendicular` (`a`, `b`: lines), `angleBetween`
(`a`, `b`: lines; `angle`: arcseconds), `distance` (`a`, `b`: places; `value`), `pointOnEdge`
(`point`: a place, `edge`: a line), `symmetric` (`a`, `b`: places; `mirror`: a line), `tangent`
(`a`, `b`: lines) and `radius` (`arc`, `value`). A line is a `segment` or a `feature`.

## An annotated example

A 12-foot wall, 5½ inches thick, with a 3-foot opening centred on it — the
[`samples/wall-with-window.scene.json`](../samples/wall-with-window.scene.json) fixture, shortened.

```jsonc
{
  "formatVersion": 4,                                  // exactly 4, judged first
  "units": { "length": "inch/1024", "angle": "arcsecond" },
  "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
  "entities": [
    // The wall: 144" x 1024 = 147456 units long, 5.5" x 1024 = 5632 units thick, lying as drawn
    // on the floor. Its depth is its height, which this sample's design does not state: 768 is a
    // placeholder (samples/wall-with-window.design.md). A wall is not a piece anybody cuts, so its
    // part is null — which is a statement, not an omission — and nothing has been cut off it,
    // which "cuts": [] says the same way.
    { "id": "30000000-0000-4000-8000-000000000001", "type": "box",
      "layer": "00000000-0000-0000-0000-000000000001", "name": "Wall",
      "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 147456, "height": 5632, "depth": 768,
      "faceUp": "top", "rotation": 0,
      "part": null, "cuts": [] },

    // The opening: 36" = 36864 units wide, the full thickness of the wall, starting 54" along.
    // Its anchor z would be its sill and its depth its height — placeholders here, like the wall's.
    { "id": "30000000-0000-4000-8000-000000000002", "type": "box",
      "layer": "00000000-0000-0000-0000-000000000001", "name": "Opening",
      "anchor": { "x": 55296, "y": 0, "z": 0 }, "width": 36864, "height": 5632, "depth": 768,
      "faceUp": "top", "rotation": 0,
      "part": null, "cuts": [] },

    // A driving dimension: the relationship named in "drives" owns the number 147456; this
    // annotation draws it, as 12'-0".
    { "id": "30000000-0000-4000-8000-000000000003", "type": "dimension",
      "layer": "00000000-0000-0000-0000-000000000001", "name": "Wall length",
      "measures": { "kind": "boxWidth", "box": "30000000-0000-4000-8000-000000000001" },
      "drives": "40000000-0000-4000-8000-000000000002",
      "placement": { "offset": 12288, "side": "south" } },

    // A reference dimension: it measures the span from the wall's west end to the opening and
    // owns nothing, so "drives" is null. It reads 4'-6". Each end is the edge where a box's south
    // and west faces meet: on a box lying as drawn, the plan's south-west corner.
    { "id": "30000000-0000-4000-8000-000000000006", "type": "dimension",
      "layer": "00000000-0000-0000-0000-000000000001", "name": "Wall west end to opening",
      "measures": { "kind": "axis",
        "from": { "kind": "feature", "box": "30000000-0000-4000-8000-000000000001", "faces": ["south", "west"] },
        "to":   { "kind": "feature", "box": "30000000-0000-4000-8000-000000000002", "faces": ["south", "west"] },
        "axis": "x" },
      "drives": null,
      "placement": { "offset": 6144, "side": "north" } }
  ],
  "relationships": [
    { "id": "40000000-0000-4000-8000-000000000002", "kind": "paramValue",
      "param": { "kind": "boxWidth", "box": "30000000-0000-4000-8000-000000000001" },
      "value": 147456 },

    // The opening runs the full thickness of the wall: their south faces are one plane …
    { "id": "40000000-0000-4000-8000-000000000006", "kind": "flush",
      "a": { "kind": "feature", "box": "30000000-0000-4000-8000-000000000001", "faces": ["south"] },
      "b": { "kind": "feature", "box": "30000000-0000-4000-8000-000000000002", "faces": ["south"] } },

    // … and sits in the middle of it: the opening's centre is midway between the wall's two ends.
    { "id": "40000000-0000-4000-8000-000000000008", "kind": "centered",
      "middle": { "kind": "center", "box": "30000000-0000-4000-8000-000000000002" },
      "a": { "kind": "feature", "box": "30000000-0000-4000-8000-000000000001", "faces": ["south", "west"] },
      "b": { "kind": "feature", "box": "30000000-0000-4000-8000-000000000001", "faces": ["south", "east"] },
      "axis": "x" }
  ]
}
```

(The comments are for this document. The reader rejects JSON comments and trailing commas.)

## Opening and saving

```csharp
// A whole project: table.napkin
switch (ProjectFile.Load(path))
{
    case LoadedProject project:
        Draw(project.Sketch);
        Remember(project.Manifest.AdoptedCode);
        break;
    case Refused refused:
        ShowTheUser(refused.Summary);   // every problem, each naming what was wrong
        break;
}

switch (ProjectFile.Save(path, sketch, adoptedCode))
{
    case Saved saved:
        MarkClean(saved.Path);
        break;
    case NotSaved notSaved:
        ShowTheUser(notSaved.Summary);
        break;
}
```

```csharp
// One plain scene document: wall-with-window.scene.json
switch (SceneReader.ReadFile(path))
{
    case Loaded loaded: Draw(loaded.Sketch); break;
    case Refused refused: ShowTheUser(refused.Summary); break;
}

SceneWriter.Write(stream, sketch);
```

`ProjectFile.Load(Stream)` and `SceneReader.Read(Stream)` are the same things over bytes, and
`ProjectFile.SaveToBytes` and `SceneWriter.WriteToBytes` produce what would have been written
without writing it anywhere. Every one of them takes an optional `IGeometryUpdater` whose
`SupportedRelationships` decide which relationship kinds this build can hold; the default is the
direct updater.

Every refusal is a `Refused` carrying a list of `LoadProblem`s — a kind, a location such as
`scene.json/entities/3/width`, and a message — and every save that could not be done is a
`NotSaved` carrying `SaveProblem`s. **Nothing is thrown for a bad file or a full disk**; exceptions
are for a caller that passed nonsense arguments.

`Load(Save(sketch)) == sketch` by value is property P9 of the geometry design, and it is tested
both ways round: a sketch survives being written and read back, and bytes survive being read and
written back.

## Divergences from the geometry design's §6 sketch

The geometry design set out the scene format in outline. Three things are settled differently
here, each because the outline is ambiguous under strict reading:

1. **Every reference carries an explicit `kind`.** §6's example writes a box-edge reference as
   `{ "box": …, "edge": … }` and a size as `{ "kind": "boxWidth", "box": … }`. Telling a
   `center` reference (`{ "box": … }`) from a `feature` reference by which fields are present is
   exactly the sort of guess that strict reading is meant to remove, so every place and size
   reference names its kind.
2. **The scene's stamp stays in the scene, and the manifest gets its own.** §6 puts
   `formatVersion` and `units` in `manifest.json`. They head the scene document instead, and the
   manifest carries a `containerVersion` of its own — the reasoning is under
   [Why the units are not in the manifest](#why-the-units-are-not-in-the-manifest) above. The
   consequence worth stating plainly: a plain `scene.json` with no manifest anywhere near it is
   still a complete, self-describing, refusable file, which is what keeps the hand-written samples
   working.
3. **A dimension's `measures` is the size reference itself for a size**, as §6's example shows,
   and `{ "kind": "axis", … }` for a span. There is no wrapper object, and `axis` is therefore not
   available as the name of a size reference.
4. **The container holds two entries, not four.** §6.4 of DESIGN.md sketches a `thumbnail.png` and
   an `assets/` directory alongside. A thumbnail means rendering, which lives in the app, and
   neither is written by this build; a container holding one is refused like any other unknown
   entry. Adding them is a `containerVersion` bump, and catalogue feature `PRJ-006` stays
   unclaimed until then.

One thing the reader deliberately does **not** check: that a driving dimension's `drives`
relationship is about the same measurand the dimension measures. Nothing in the design requires
it, and a mismatch is a UI bug rather than a corrupt file. If that changes, `formatVersion` does
not: it is a stricter reading of the same file, and it would be recorded here.
