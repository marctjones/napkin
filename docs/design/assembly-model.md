# Assembly: parts in three dimensions

Status: **Signed off by Marc, 2026-09-23 ("do your recommendations").** All 24 decisions in §11 are
accepted as recommended, with no exceptions. Authorized for implementation per §10. No issue filed
yet for §1–§10 themselves; this extends the geometry kernel (#5), the editor (#10), the parts model
(#8) and the file format (#6), and it supersedes DESIGN.md §2's "Not 3D in the first betas" and
§9's "Beta scope is 2D only" (§11 decision 14 — update both on the first implementation commit).
It belongs to no milestone until Marc places it. **Revised the same day it was first drafted**
after Marc's correction that a sawhorse must be designable: §3a adds angled members without a
solver, §3.3, §6, §9, §10 and §11 are updated to match, and the headline claim is qualified where
it needed to be (§3.1). §11 decisions 17–24 are the calls that revision added.

Design document written by Fable per [`PLAN.md`](../../PLAN.md): a foundation every later phase
builds on, where a wrong choice is a silent, expensive one. It decides how a part that today lives
in a plan view gains a position and an orientation in space, how parts are held against each other
in three dimensions, what the direct updater has to do about it, how a shaped part becomes a
solid, what the file stores, and what the plan canvas built this session keeps doing unchanged.

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024-inch grid and
angles in integer arcseconds ([`geometry-model.md`](./geometry-model.md) §1); a box is
parametric — it stores what was typed and derives what that produces (§2.3); rotation by a
right-angle multiple is an exact swap-and-negate, never trigonometry (§1.6, §2.1); relationships
are stored as data behind one update interface with explicit result types (§3, §4); the solver is
a separate workstream that slots in behind the same interface and works in `double` on a copy,
rounded once and repaired (§5); a part is fields on a box, and the cut list reads stored
parameters, never derived corners ([`parts-and-cut-list.md`](./parts-and-cut-list.md) §1.1,
`CUT-002`); a shaped part is a blank with cuts, its outline derived and every cut square through
the plan ([`shaped-parts-model.md`](./shaped-parts-model.md) §1); strict reading with no migration
and no shims ([`../file-format.md`](../file-format.md), DESIGN.md §12).

**The product decision, made by Marc (2026-09-22) and not re-litigated here:** napkin gets real 3D
*editing*, not a passive preview, with two constraints he stated himself. **No physics** — nothing
about collision, deformation, gravity or simulation. And the need is narrow: *"the ability to
rotate things in 3D and be able to put the shapes together."* That is furniture assembly — boards,
legs and panels turned in the hand and set against one another the way physical parts are — not a
general 3D CAD kernel. §6 states the non-goals so scope does not creep past that.

**The one decision this document argues hardest for** is that *rotation of a box* in three
dimensions is restricted to the **24 axis-aligned orientations** — a block turned to any face-up
position and spun by quarter turns — and not arbitrary tilt (§1.3, §11 decision 1). The whole
kernel's load-bearing property is "exact at right angles" (geometry-model §1.6, §2.3, §5);
extending that guarantee from one rotational axis to three keeps every coordinate, every
relationship and every cut-list number exact and citable, and it means everything axis-aligned
here stays on the direct updater's exact path with **no solver and no new floating-point machinery
at all** (§3). Arbitrary tilt of a box would import the full double-precision-plus-repair path for
a need that, in Marc's own framing, is about assembly rather than sculptural angles.

**Marc's correction (2026-09-22): "We do want things that aren't 90 degrees. We should be able to
design a saw horse."** An earlier draft excluded the sawhorse as "the solver's domain by
construction". That was true of a *box* given a tilt, and it is false of a member stored by *where
its two ends are*. §3a adds one narrow, separately-argued primitive for that — the **strut**, an
angled member defined by two exact endpoints, whose direction, length and end cuts are derived —
and shows that it, too, stays on the exact propagator with no solver, at the price of a small,
named set of once-rounded numbers on its cut-list row. The axis-aligned path keeps every guarantee
§3.1 proves; the strut adds a second path beside it without weakening the first. What remains
genuinely excluded is named in §3.3 and §3a.8, on purpose, not by omission.

---

## 1. What a box is in three dimensions

### 1.1 The same `Box`, with a third size, a height and a face-up

A part in space is **the `Box` that exists today, given the third size a plan view could not hold,
a Z coordinate for its anchor, and a statement of which of its faces points up.** Nothing is
wrapped, nothing is forked.

```csharp
public sealed record Box(
    EntityId Id, LayerId Layer,
    Point3 Anchor,          // the local origin corner: south-west-bottom in the local frame
    Length Width,           // along local X; > 0                       (unchanged)
    Length Height,          // along local Y; > 0                       (unchanged)
    Length Depth,           // along local Z; > 0                       NEW — was Part.OutOfPlane
    BoxFace FaceUp,         // which local face points to world +Z      NEW — Top is today's box
    Angle Rotation)         // spin about world Z, about Anchor; right-angle multiples in the
                            // direct updater                            (unchanged)
    : Entity(Id, Layer)
{
    public Part? Part { get; init; }                 // unchanged, minus OutOfPlane (§1.2)
    public ImmutableList<Cut> Cuts { get; init; }    // unchanged: in the local XY frame (§4)
    public Outline Outline();                        // now LOCAL-frame: the cap of the solid (§4, §7.2)
    public Solid Solid();                            // NEW: the outline extruded along local Z (§4)
    public Footprint Footprint();                    // NEW: what the plan view sees (§7.1)

    public Orientation Orientation => new(FaceUp, Rotation);   // derived; §1.3
    public Point3 Vertex(BoxCorner corner, BoxLevel level);    // exact under all 24 orientations
    public Point3 Center { get; }                              // rounds by half a unit per axis
}
```

Why extend in place rather than add an entity, argued the way shaped-parts §1.1 argued it:

- **A `Solid` entity beside `Box`** would be a second thing with a second reference vocabulary, a
  second propagator path and a second cut-list reader, for an object that is the same board. Every
  `Flush`, every `ParamValue`, every `Cut`, the workshop, the stock tool and the cut list would have
  to know that the two are one. Shaped-parts §1.1 rejected the same shape for the same reason.
- **A `Placement` wrapper referencing a box by id** — two ids for one piece; selection, undo,
  layers, names, the cut list and every relationship would all have to look through it.
  Parts-and-cut-list §7 step 2 rejected side tables for `Part` on exactly this ground.
- **3D data on `Part`** — a wall is a box too and now has a height; the kernel should not reach
  into a nullable module record to place an entity in space.

A box whose `Anchor.Z` is zero, whose `FaceUp` is `Top` and whose `Rotation` is a right-angle
multiple is **today's box in every respect**: same corners in the plan, same relationships, same
cut-list row, same drawing. That is what §7 rests on.

### 1.2 The third size lives on the box

`Part.OutOfPlane` is **removed**. Its value moves to `Box.Depth`, required and positive on every
box, part or not. `PlanAxes` is unchanged and still names which of length, width and thickness lie
along local X and Y; the remaining name is the one along local Z, and its value is now
`Box.Depth`. Nothing is stored twice, so `CUT-002` holds by construction exactly as it does today:
the three finished dimensions of a part are three stored parameters of one box, and rotation and
position are not read.

```csharp
public sealed record Part(string? Stock, string? Species, int Quantity, PlanAxes PlanAxes);
// SizeOn(box) reads Width, Height and Depth by PlanAxes; Orientation is not read at all.
```

Consequences, stated now rather than discovered:

- **Every box needs a depth, including walls and openings.** A wall's depth is its height; an
  opening's is the opening's height, and its `Anchor.Z` its sill. Nothing in the rules engine
  reads either yet (§6); they exist because a box in space has an extent in space. The
  `wall-with-window` sample gains hand-chosen values recorded with a `derivation` string per
  `samples/README.md` — a design choice for the fixture, not a code value, so no citation applies
  and the design file says so.
- **Stock assignment now sets all three fixed dimensions through the updater.** Parts §1.2 step 1
  set the out-of-plane value "directly — nothing else depends on it". Something now does: a
  `Flush` on the top and bottom faces. So a `LumberStock`'s width and thickness, and a
  `PanelStock`'s thickness, land as `ParamValue`s on whichever of `BoxWidthRef`, `BoxHeightRef`
  and the new `BoxDepthRef` `PlanAxes` says, all in one `Batch`, and a conflict is reported the way
  any other is. The special case in `StockAssignment.RequestsFor` goes away.
- **A typed depth is a `ParamValue(BoxDepthRef …)`**, exactly as a typed width is a
  `ParamValue(BoxWidthRef …)` (geometry-model §4.1). The properties panel's out-of-plane field
  becomes the depth field and creates or edits that relationship.
- **The plain-English trap, named once.** `Height` is the size along *local Y* — the plan-view
  depth of the footprint — and has been since #5; the new size along local Z is `Depth`, which for
  a box lying as drawn is what a person would call its height or its thickness. The names are the
  3D-graphics convention (width × height × depth) and they match what the cut list, the file and
  every existing test already call the first two. Renaming `Height` is §11 decision 3.

### 1.3 Orientation: which face is up, then a spin

The 24 orientations a block can take by quarter turns form a group (the rotations of the cube).
The stored form is **not** a matrix, a quaternion or a 24-valued opaque enum. It is two fields that
each say something a person decided, and whose product is exactly the group:

- **`FaceUp`** — one of the six local faces, the one pointing to world +Z. `Top` is the box as
  drawn. `Bottom` is turned over. `North` is tipped back, `South` tipped forward, `East` and
  `West` tipped over sideways. Six choices: "stand the leg up, lay the top flat, stand the apron on
  edge".
- **`Rotation`** — the existing field, unchanged in type and meaning: the spin about world Z, about
  the anchor, that the plan view already shows. Right-angle multiples in the direct updater.

6 × 4 = 24, and with `Rotation` a quarter turn the pair is a *bijection* onto the group: every
orientation has exactly one spelling, so two boxes turned the same way are equal by value and a
saved file has one spelling. The factoring is chosen over a single 24-valued field for two reasons.
Every existing box is the `FaceUp = Top` case with identical meaning, so nothing that reads
`Rotation` changes. And the solver seam is untouched: geometry-model §5.2's rotation scalar and
§10.5's note that the `Propagator` will one day gain a rotation `ScalarKind` are about *this*
field, and when #28 extends `Rotation` past quarter turns — a board spun 37° in the plan — the six
face-up choices stay exact underneath it. A design that dropped `Angle Rotation` for a 24-valued
struct would delete that seam (`Vector2.Rotate`'s non-right branch, `RotationNotSupported`, and
the 45° propagator test geometry-model §8 step 8 says #28 relies on), and this document does not.

**The transform.** For a local point `p` in `[0, Width] × [0, Height] × [0, Depth]`:

```
World(p) = Anchor + Rz(Rotation) · Tip(FaceUp) · p
```

`Tip(f)` is the fixed proper rotation that carries local face `f`'s outward normal to world +Z.
Its images of the three local axes are pinned here and nowhere else; an implementation that
disagrees with this table is wrong, not different:

| `FaceUp` | local +X → | local +Y → | local +Z → | In the hand |
|---|---|---|---|---|
| `Top` | +X | +Y | +Z | as drawn |
| `Bottom` | +X | −Y | −Z | turned over, end for end, east and west kept |
| `North` | +X | +Z | −Y | tipped back: the north face is up, the drawn top faces south |
| `South` | +X | −Z | +Y | tipped forward: the south face is up, the drawn top faces north |
| `East` | +Z | +Y | −X | tipped over westward: the east face is up, the drawn top faces west |
| `West` | −Z | +Y | +X | tipped over eastward: the west face is up, the drawn top faces east |

Every row is a signed permutation with determinant +1 — a rotation, never a mirror. That invariant
is load-bearing: a mirror would turn a left-hand part into its right-hand twin with no visible
error and a cut list that lists the wrong hand. `Rz(q)` for a quarter turn `q` is the swap-and-
negate `Vector2.Rotate` already does, applied to X and Y with Z untouched. So `World(p)` is exact:
three components, each a stored coordinate plus or minus a stored size. `Box.Vertex` never rounds,
for any of the 24 orientations, for the same reason `Box.Corner` never rounds today for any of the
four.

```csharp
/// The 24 axis-aligned orientations: which local face points up, then a spin about world Z.
public readonly record struct Orientation(BoxFace FaceUp, Angle Rotation)
{
    public static readonly Orientation AsDrawn = new(BoxFace.Top, Angle.Zero);
    public bool IsExact => Rotation.IsRightAngleMultiple;

    /// Where a local axis points in the world, and which way: exact.
    public (Axis Axis, bool Positive) Image(Axis local);
    /// The world axis a local face is perpendicular to, and whether it faces the positive way.
    public (Axis Axis, bool Positive) Normal(BoxFace face);

    public Vector3 Apply(Vector3 local);       // Rz · Tip · v — exact when IsExact
    public Vector3 Unapply(Vector3 world);     // the inverse: exact when IsExact

    /// This orientation after a quarter turn about a world axis. Closed: the result is one of
    /// the 24, in its one spelling.
    public Orientation TurnedAbout(Axis axis, int quarterTurns);
}
```

`TurnedAbout` composes signed permutations and *decomposes* the product back into the one
`(FaceUp, Rotation)` spelling: `FaceUp` is the local face whose normal lands on +Z; `Rotation` is
read off where local +X lands once `Tip(FaceUp)` is factored out. Integer arithmetic on three
signs and three axis names; the tests in §9 pin all 24 × 3 × 4 cases. Turning about world Z is the
only case that leaves `FaceUp` alone, which is the plan view's rotate-in-place today.

**Two spellings of one block, and why that is fine.** A 40″ × 3½″ × ¾″ apron can be drawn lying
flat (`Width 40, Height 3½, Depth ¾, FaceUp Top`) or drawn on edge (`Width 40, Height ¾, Depth
3½, FaceUp Top`), or drawn flat and then stood on edge (`… FaceUp North`). All three occupy the
same space. The model stores what the person drew, because cuts break the symmetry — a taper is
drawn on the face, lying flat; a chamfer along the length is drawn on the footprint — and the cut
list groups by the three finished dimensions through `PlanAxes`, so all three list identically
(§5). This is shaped-parts §1.4's "a cut is only expressible in the plane the part is drawn in",
now with the plane free to be turned afterwards.

### 1.4 Value types

```csharp
public enum Axis { X, Y, Z }                            // Z added; Y is still up in the plan view

public readonly record struct Point3(Length X, Length Y, Length Z);
public readonly record struct Vector3(Length Dx, Length Dy, Length Dz)
{
    public Length Component(Axis axis);
    public static Vector3 Along(Axis axis, Length distance);
    public Vector2 XY { get; }                           // the plan projection
    // +, -, unary -, * long: exact, as Vector2
}
```

`Point2` and `Vector2` stay, for nodes and for the plan canvas. `Node.Position` stays a `Point2`:
nodes and segments are plan-plane construction geometry — a wall centreline, a construction line —
and nothing on the list in DESIGN.md §3 needs a free point in space. A node fixes X and Y (§2.1)
and lies at the plan datum, Z = 0. Giving nodes a Z is deliberately not done (§6).

### 1.5 Faces, edges and vertices

A box in space has 6 faces, 12 edges and 8 vertices, and every one of them is named in the box's
**local frame, before orientation** — exactly as `BoxCorner` and `BoxEdge` already name things for
references and for cuts — using words the plan canvas, the workshop and the cut list already use:

```csharp
/// A face of the box, in its local frame. The four sides carry the names of the plan edges they
/// are seen as from above; Bottom is local z = 0 and Top is local z = Depth.
public enum BoxFace { South, East, North, West, Bottom, Top }

/// Bottom or top: which end of an upright edge, or which level a horizontal edge is at.
public enum BoxLevel { Bottom, Top }

/// One feature of a box: a face, an edge (two adjacent faces) or a vertex (three).
public readonly record struct BoxFeature
{
    public static BoxFeature Face(BoxFace face);
    public static BoxFeature Edge(BoxFace a, BoxFace b);                 // adjacent, not opposite
    public static BoxFeature Vertex(BoxFace a, BoxFace b, BoxFace c);    // mutually adjacent
    public static BoxFeature LocalUpright(BoxCorner corner);             // the LOCAL-Z edge at a corner of the blank as drawn
    public static BoxFeature Vertex(BoxCorner corner, BoxLevel level);

    public ImmutableArray<BoxFace> Faces { get; }   // in BoxFace order: one spelling
    public int Dimension { get; }                   // 2 for a face, 1 for an edge, 0 for a vertex
}
```

A feature is **the set of faces that meet at it**, held in `BoxFace` order so that it has one
spelling in memory and in the file. This is the representation because it is what the propagator
needs (§2.1): a face fixes one world coordinate; an edge fixes the two its faces fix; a vertex all
three. Opposite faces (`South`/`North`, `East`/`West`, `Bottom`/`Top`) never share a feature.

The four local-Z edges are the four corners of the blank as drawn: `LocalUpright(SouthWest)` is
`Edge(South, West)`. That is not a coincidence of naming; it is the observation §2 is built on.
Two words are kept apart on purpose, because confusing them is a silent bug: a **local upright**
is an edge along local Z, which points up only when `FaceUp` is `Top`; a **plan upright** is an
edge along *world* Z, which is what a plan corner projects from, and only `Footprint.UprightAt`
(§7.1) says which local edge that is for a given orientation.

### 1.6 Invariants

Added to geometry-model §2.5's and shaped-parts §1.6's lists. A sketch failing any is invalid; the
updater never returns one; the loader refuses a file containing one.

10. Every `Box` has `Depth > 0`, alongside `Width > 0` and `Height > 0`.
11. Every `Box.Rotation` in a sketch the direct updater handles is a right-angle multiple —
    the existing precondition, unchanged — so that `Orientation.IsExact` holds for every box the
    exact path touches.
12. Every `BoxFeature` in a reference names one, two or three mutually adjacent faces, in
    `BoxFace` order.
13. A `Dimension`'s measurand lies in the plan, judged in **world** terms through the owning box's
    orientation (§7.3): a `ParamMeasurand` names a size whose local axis `Orientation.Image`
    sends to world X or Y, and an `AxisMeasurand`'s axis is X or Y and both its places fix it.
    No dimension measures along world Z in this design — so `boxDepth` on a `Top` box is
    refused, and so is `boxWidth` on an `East` box, whose width now stands vertical.

---

## 2. References and relationships in three dimensions

### 2.1 The observation: the plan relationships already are the 3D ones

Today `Coincident(CornerRef(A, SouthEast), CornerRef(B, SouthWest))` pins X and Y and says nothing
about Z, because Z does not exist. `Flush(BoxEdgeRef(A, North), BoxEdgeRef(B, South))` pins one
axis — the normal — and nothing else. `Propagator.PairsOf` (geometry-model §10.3) already models
every relationship as "pairs of equal values, one per axis it speaks about".

Read in three dimensions, those are already the right relationships, restricted to what a plan
view can see. A plan corner is the projection of an **upright edge**; two plan corners coincident
means two upright edges are collinear — equal in X and Y, free in Z. A plan edge is the projection
of a **side face**; two plan edges flush means two side faces are coplanar — equal on the one axis
they are both perpendicular to. Nothing about the meaning changes; only the set of features and
axes grows.

So the one rule that generalises everything:

> **A feature fixes a set of world axes and a coordinate on each. `Coincident` and `Flush` mean
> "equal on every axis both features fix."**

For a box with orientation `O`, the face `f` is perpendicular to the world axis
`O.Normal(f).Axis`, and its coordinate on that axis is `Anchor` there plus, when `f` is the face at
the far end of its local axis (`East`, `North`, `Top`), the box's size along that local axis with
the sign `O.Image` gives — and plus nothing when `f` is at the near end (`West`, `South`,
`Bottom`). An edge's coordinates are its two faces'; a vertex's its three. Each is an anchor
component plus or minus a stored size selected by a signed permutation: **exact**.

### 2.2 The reference vocabulary

`CornerRef` and `BoxEdgeRef` are replaced by one reference to a feature. `PointRef` and `EdgeRef`,
whose split existed so that `Coincident` took points and `Flush` took edges, merge into one base;
what a relationship may take becomes a validation rule (§2.3) rather than a static type, because a
feature can be any of the three dimensions and a node is a two-axis thing that today legally
coincides with a corner.

```csharp
/// Something with a place: a point, a line or a plane, each fixing some world axes.
public abstract record PlaceRef { public abstract EntityId Owner { get; } }

public sealed record NodeRef(EntityId Node) : PlaceRef;                       // fixes X, Y
public sealed record SegmentRef(EntityId Segment) : PlaceRef;                 // fixes X or Y when axis-aligned, as today
public sealed record CenterRef(EntityId Box) : PlaceRef;                      // fixes X, Y, Z
public sealed record FeatureRef(EntityId Box, BoxFeature Feature) : PlaceRef; // fixes 1, 2 or 3 axes
public sealed record StrutEndRef(EntityId Strut, StrutEnd End) : PlaceRef;    // fixes X, Y, Z (§3a.5)

public abstract record ParamRef { … }                                         // unchanged
public sealed record BoxWidthRef(EntityId Box) : ParamRef;
public sealed record BoxHeightRef(EntityId Box) : ParamRef;
public sealed record BoxDepthRef(EntityId Box) : ParamRef;                    // NEW
public sealed record StrutHeightRef(EntityId Strut) : ParamRef;               // NEW (§3a.2); a strut has no length ref
public sealed record StrutDepthRef(EntityId Strut) : ParamRef;                // NEW
public sealed record SegmentLengthRef(EntityId Segment) : ParamRef;
```

`Sketch.PointOf`/`EdgeOf` become one `Sketch.PlaceOf(PlaceRef) → Place`, where a `Place` is the
set of `(Axis, Length)` pairs the reference fixes. The plan canvas's `CornerRef(b, SouthWest)` is
now `FeatureRef(b, footprint.UprightAt(SouthWest))` and its `BoxEdgeRef(b, North)` is
`FeatureRef(b, Face(footprint.FaceAt(North)))` (§7.1) — which for a `Top` box are
`LocalUpright(SouthWest)` and `Face(North)`, and both mean what they meant. The canvas always
goes through the footprint, never straight from a plan name to a local name, because for a
tipped box the plan's south-west corner is not the blank's.

One vocabulary, not three: `FaceRef`/`Edge3Ref`/`Corner3Ref` as separate records were considered
and rejected because the propagator's rule does not care which of the three it has, only which axes
it fixes, and three records would be three places to get the axis set wrong. The cost is that a
nonsensical pairing (`Flush` between two vertices) is refused at runtime rather than at compile
time — the same trade geometry-model §10.3 already accepted for `SupportedRelationships` being an
upper bound.

### 2.3 The relationship set, generalised

Every kind keeps its name, its record shape and its meaning. What changes is the reference type and
the set of axes.

| Relationship | 3D meaning | Legal when | Class |
|---|---|---|---|
| `Anchored(entity)` | Pins all six of a box's scalars: X, Y, Z, Width, Height, Depth. | — | exact |
| `Coincident(PlaceRef, PlaceRef)` | The same place: equal on every axis both fix. Two vertices; two parallel edges; a node and an upright edge; a centre and a vertex; a centre and a node. | the **intersection** of the two axis sets (after orientation) has two or three axes — so a face never qualifies, a node meets an upright edge or a centre on X and Y as it does today, and two edges must be parallel | exact |
| `Flush(PlaceRef, PlaceRef)` | The same plane: two faces coplanar, or a face and an axis-aligned segment. | both fix exactly **one** axis, the same one | exact |
| `AxisDistance(from, to, Axis, Length)` | Signed distance along X, Y **or Z** between two places. | both fix `Axis` | exact |
| `Centered(middle, a, b, Axis)` | Midway along X, Y or Z. | all three fix `Axis` | exact; half-unit on an odd span, per axis |
| `ParamValue(ParamRef, Length)` | A width, height **or depth** is a stated value. | — | exact |
| `EqualParam(ParamRef, ParamRef)` | Two sizes are equal — a leg's depth and another leg's depth; a leg's depth and an apron's width. | — | exact |
| `Horizontal`, `Vertical` | Segments only, unchanged. | — | exact |

A `StrutEndRef` (§3a.5) is a place fixing all three axes and takes part in `Coincident`,
`AxisDistance` and `Centered` under exactly the rules above — it is a vertex that belongs to a
strut instead of a box. `Anchored` also accepts a strut. No relationship kind is added for struts.

"Legal when" is decided by `AddRelationship` and by the loader, with one new rejection reason,
`PlacesNotComparable`, whose message names both places and what each fixes — "the top's bottom
face fixes Z; the apron's north face fixes Y". A `Flush` between a face pointing up and a face
pointing north cannot hold and is refused, not stored as a tolerance-class relationship and quietly
violated. The checker still evaluates whatever a file contains, by the same per-axis rule, so a
solver-written sketch with a non-axis-aligned `Flush` is judged the way geometry-model §10.2
already judges one.

The reserved solver kinds (`Parallel`, `Perpendicular`, `AngleBetween`, `Distance`,
`PointOnEdge`, `Symmetric`, `Tangent`, `Radius`) take `PlaceRef`s where they took `PointRef`s or
`EdgeRef`s and are otherwise untouched; the direct updater rejects them as today.

Two rules from geometry-model §3.2 that still hold, extended: rectilinearity is not a relationship
but a property of `Orientation`; and relationships are stored, never inferred — a leg standing
under a top at exactly the right Z is not held there until a `Flush(leg.Top, top.Bottom)` says so.
The 3D snap creates that relationship (§7.2), as the plan snap creates a `Flush` today.

### 2.4 Requests

```csharp
public sealed record SetPosition(EntityId Id, Point3 Anchor) : Request;             // was Point2
public sealed record SetOrientation(EntityId Box, BoxFace FaceUp, Angle Rotation) : Request;  // replaces SetRotation
public sealed record Drag(EntityId Id, Vector3 Delta) : Request;                    // was Vector2
public sealed record DragFace(EntityId Box, BoxFace Face, Length Delta) : Request;  // replaces DragEdge
public sealed record SetStrutEnd(EntityId Strut, StrutEnd End, Point3 Position) : Request;   // NEW, exact (§3a.5)
public sealed record DragStrutEnd(EntityId Strut, StrutEnd End, Vector3 Delta) : Request;    // NEW, best-effort (§3a.5)
// AddEntity, RemoveEntity, AddRelationship, RemoveRelationship, SetLayer, SetName, SetPart,
// SetParameter, SetCut, RemoveCut, Batch: unchanged.
```

- **`SetOrientation`** is one request, not a `SetFaceUp` beside `SetRotation`, because a quarter
  turn about world X changes both fields at once and the canvas must never be able to land a box
  between two spellings. It rotates about the anchor, which stays where it is, exactly as
  `SetRotation` does today — so it is in `ChangeSet.Modified`, neither a move nor a resize. It is
  `Rejected(RotationNotSupported)` when `Rotation` is not a quarter turn, and
  `Rejected(OrientationWithRelationships)` — the renamed `RotationWithRelationships`, same rule,
  same reason — when the box has any `Coincident`, `Flush`, `AxisDistance` or `Centered`, **or
  any `Dimension` whose measurand would leave the plan after the turn** (invariant 13 — a
  reference dimension has no relationship to catch it by, and a driving `ParamValue` on the
  width is allowed through, yet tipping the box `East` stands that width vertical): a
  relationship names faces in the local frame, and turning the box would silently turn a
  face-to-face relationship into a face-to-edge one, or a drawn dimension into one nothing can
  draw. The canvas offers to remove them first. This is also the physical workflow: you turn a
  board in your hands, *then* you set it against the other one. Auto-removing or re-mapping
  instead is §11 decision 6.
- **`DragFace`** is `DragEdge` with six faces instead of four edges: the size along the local axis
  normal to the face changes; the anchor moves when the face is at the local origin (`South`,
  `West`, `Bottom`) — by `Orientation.Apply(Vector3.Along(localAxis, −δ))`, the 3D form of
  today's `Vector2.Along(localAxis, −delta).Rotate(box.Rotation)`, so that the sign is right for
  a box whose local origin face points up or east — and stays otherwise; `Rejected(DrivenSize)`
  when a `ParamValue` drives that
  size; clamped by the cuts' claims for the four side faces (shaped-parts §2.3) and by nothing for
  `Bottom` and `Top`, which a cut never reaches.
- **`Drag`** carries a `Vector3`; `ChangeSet.AppliedDelta` becomes a `Vector3?`. A plan-canvas
  drag is a `Vector3` with `Dz = 0`.
- **`SetCut`/`RemoveCut`** are unchanged: a cut is in the local XY frame and turns with the box.

### 2.5 What a cut's virtual corner and edge line become

Shaped-parts §2.1 binds every reference to the *blank*: a `Flush` on an edge that a curve or a cut
has removed holds on the blank's edge line, and a `Coincident` on a rounded corner holds at the
virtual corner. In three dimensions the same reading holds one dimension up: a `Flush` on a side
face holds on the blank's face plane, whatever the cut took off it; an upright edge at a clipped
corner is the blank's virtual edge; a vertex on it is the virtual vertex. The 3D view marks a
referenced virtual feature the way the plan canvas marks a virtual corner (§7.2). Nothing in
`Propagator` sees a cut, as before.

---

## 3. The propagator and the solver seam

### 3.1 The finding: the whole scope stays on the exact path

Geometry-model §5 fixed the boundary between the direct updater's exact integer propagation and a
solver that works in `double` and rounds once. With orientation restricted to the 24 axis-aligned
cases, **everything in this document is on the exact side of that boundary**, and the reasoning is
short enough to state as a claim with its premises:

1. Every coordinate a relationship reads — a face's position, an edge's, a vertex's, a centre's —
   is an anchor component plus or minus a stored size, selected by a signed permutation with unit
   entries (§1.3, §2.1). Integer addition and negation. Exact.
2. Every relationship is an equality of such coordinates, one per axis (§2.3), or an equality of
   sizes. Integer copying. Exact.
3. The direct updater's algorithm (geometry-model §4.4) is stated per scalar: a worklist of
   assignments, sizes settled before positions (§10.3's two phases), pins travelling along
   relationships that already hold, the anchor rule, the distance-from-request tie-break. None of
   it depends on there being two axes rather than three, and none of it depends on which local
   size sits on which world axis, because a `FeatureRef` answers "which scalars, which signs" once
   and the engine never asks again.

Therefore the `Propagator` gains two `ScalarKind`s — `Z` and `Depth` — and reads coordinates
through `BoxFeature` and `Orientation` instead of `Box.Corner`, and *no other change of kind*.
There is no Jacobian, no residual, no rounding except the one that already exists — the half-unit
of a `Centered` midpoint or a `CenterRef` on an odd span, now per axis — and no `double` anywhere
in the kernel's 3D path. `RelationshipChecker` judges every kind in §2.3 with zero tolerance.
`Tolerances.Position` is not reached.

This is the scope-reducing consequence of §11 decision 1, and it is the reason the recommendation
is argued so hard: 3D editing of axis-aligned parts needs no new floating-point machinery at all.

**Qualified once, for the strut.** The claim above is stated for boxes and is unchanged for them.
The strut of §3a is *also* on the exact side of the boundary, by a different argument: its
relationship-bearing scalars are its two endpoints and two cross-section sizes, every one of them
stored, so premises 1–3 hold for it verbatim. What is *not* exact about a strut — its length, its
tilt, the setback of each end cut — is never a scalar a relationship reads; it is derived from the
exact scalars for the cut list and the screen, rounded once each, and marked. That is one
derivation function holding the only `double` this design adds to the kernel, and §3a.4 says
precisely where it is and what it may not touch. The unqualified sentence "no `double` anywhere
in the kernel's 3D path" is therefore true of the propagator, the checker and the loader, and
false of `StrutBlank.Derive`, which none of them calls.

### 3.2 What the seam looks like from the solver's side

Unchanged in shape. When #28 arrives, its variables are the same scalars — now six per box plus
the `Rotation` it already planned to own — loaded exactly, solved in `double`, rounded once through
`Length.FromInches`/`Angle.FromDegrees`, repaired through the `Propagator` with the exact-class
relationships, verified by the checker (geometry-model §5.2). `FaceUp` is **not** a solver
variable: it is discrete, and a solver that could flip a board over mid-solve would be solving a
different problem. The `Propagator` is handed a sketch whose `FaceUp` values are facts, exactly as
it is handed cuts as facts (shaped-parts §3).

A box whose `Rotation` is not a quarter turn — reachable only from a solver-written file, as
today — makes `Orientation.IsExact` false; `Orientation.Apply` then goes through `double` for the X and
Y components exactly as `Vector2.Rotate` does now, with Z untouched; and `DirectUpdater.Apply`
rejects geometry requests on that sketch with `RotationNotSupported`, as now. The precondition,
where it is checked and where it is not (geometry-model §4.4 "two components"), does not move.

### 3.3 What is excluded, named

An earlier draft of this section excluded "compound-angle parts: a sawhorse, a splayed-leg stool
or bench, a Windsor-chair leg" as the solver's domain by construction. Marc's answer was that a
sawhorse must be designable, and §3a shows how without a solver. The list of what is excluded is
therefore shorter, and what remains on it is excluded for a reason that survives §3a:

- **A tilted or spun `Box`.** A box's orientation stays one of the 24; a box given a `Rotation`
  that is not a quarter turn, or any tilt, is #28's business as before. The strut is not a rotated
  box: it is a different entity with a different stored form (§3a.2), and the two coexist. What
  this keeps out is *rotating a rectangle that has cuts and relationships* — an octagonal top's
  rails rotated into place with their mitres, a shaped gusset turned 30° — because a box's
  exactness rests on its orientation being a signed permutation, and no amount of storing
  endpoints changes that for a part whose shape is a blank with cuts. The factoring in §1.3 is
  chosen so that when #28 extends `Rotation`, a tilted `FaceUp` underneath it keeps its exactness.
- **A member whose end cuts need a bevel.** A strut's two end cuts must lie in one drawn plane
  with the strut's own axis (§3a.3's coplanarity rule). A leg whose foot sits on the floor and
  whose *face* lies flat against a rail's vertical side while also splaying sideways needs a bevel
  — on the leg's end or on the rail's side — and bevels are out of the model for boxes
  (shaped-parts §6, §4.2 here) and struts alike. `Rejected(StrutNeedsBevel)` names it.
- **A strut cut to meet anything but an axis-aligned plane.** An octagonal frame's rails mitred
  to *each other* at 22½°, two diagonal braces mitred where they cross, a strut whose end is cut to
  another strut's face: the mating plane is not axis-aligned, its normal is irrational, and the
  cut cannot be stated on the grid in any frame. Excluded, named (§3a.8).
- **Relationships on a strut's derived quantities.** "These two legs are the same length", "this
  brace is at 45° to the post": a `Distance` or `AngleBetween` on values that are irrational on the
  grid. Tolerance class, #28's, exactly as shaped-parts §3 says of a cut's derived angle.

Everything else on DESIGN.md §3's list — a coffee table, a deck's framing, a wall with an opening, a
shed's plates and studs — is axis-aligned boards set against axis-aligned boards; the sawhorse, the
splayed stool, the A-frame and the diagonal brace are axis-aligned boards with struts between them.

---

## 3a. Angled members: the strut

Added after Marc's correction of 2026-09-22. This section is the whole of the design for members
that are not at right angles; §1–§3 and §4–§8 are unchanged in substance and are touched only
where they say how a strut fits. It is numbered 3a rather than renumbering §4–§11, so that every
cross-reference in the document and in Marc's notes stays valid.

### 3a.1 The finding: store the ends, derive the board

Start from the fact that rules out every clever alternative. **A member that is not axis-aligned
cannot be exact on the grid in all of its numbers, whatever is stored.** A segment with both
endpoints on the 1/1024″ grid has an irrational length unless the three deltas happen to form a
Pythagorean quadruple; a segment with an on-grid start and an exact length has an off-grid end;
a stored exact angle makes both the length and the far end irrational (shaped-parts §1.3 showed
this in two dimensions for a mitre: setbacks or angle, never both). So the design question is not
"how do we keep everything exact" — nothing can — but **which numbers are exact and which are
derived, rounded once and marked.** That is the same question shaped-parts §1.3 answered for a
mitre, and this section answers it the same way, with one inversion worth stating plainly.

For a mitre, shaped-parts stored the *marks on the board* (the setbacks) and derived the *angle*.
For a strut, this design stores the *placement* — the two points in space where the member's
centreline meets the surfaces it is cut to — and derives the *board*: its length and the setback
of each end cut. The reasons the inversion is right, argued rather than assumed:

- **It stores what was decided.** A person designing a sawhorse decides the stock (a 2x4), the
  height of the rail, and where the feet land — "9″ out to the side, 6″ out past the end". The
  leg's length is what they want napkin to *tell* them; nobody decides "the legs are 30 31/64″
  long" and lets the feet fall where they may. The strut's stored form is those decisions and
  nothing else, which is §1.1's standard for `Box` and shaped-parts §1.1's for a cut.
- **It keeps assembly exact, and that is what keeps the solver out.** With both ends stored, every
  relationship a strut can take part in — its top on a rail's underside, its foot level with the
  other feet, its end at a post's corner — is an equality of stored integers (§3a.5), so the
  direct updater propagates it exactly and the checker judges it with zero tolerance. Store the
  length instead and the far end is irrational; every relationship on it is tolerance class;
  the sawhorse is on the solver's path, which is exactly the verdict the old §3.3 reached for a
  tilted box. The exclusion was never about angles; it was about *where the irrational number
  lands*. Put it on the cut-list row, where a person cuts to 1/32″ anyway and the `≈` marker
  already exists, and the kernel stays exact.
- **It matches what shaped-parts already accepted.** A stored setback of 3″ on a 5″ end reads
  back as "≈ 31° off square", and the design accepted that a derived, once-rounded, marked number
  is honest. A strut's derived length is the same kind of number, one dimension up.

What the inversion costs, stated now: **`CUT-002`'s letter — "the cut list reads stored
parameters, never derived corners" — does not hold for a strut**, whose listed length is a
derived distance. Its *purpose* does hold: that rule exists so that placement cannot leak into a
listed size (a box typed 10″ wide lists as 10″ after it is rotated 37°). A strut has no typed
length to protect; its length *is* its placement. §3a.6 names the new rule that replaces
`CUT-002` for struts and the test that pins it.

### 3a.2 The entity: why a strut is not a box

A `Box` is anchor + orientation + three sizes, and its exactness under all 24 orientations rests on
one fact: every vertex is the anchor plus or minus stored sizes under a *signed permutation*
(§1.3). Give a box a direction that is not a signed permutation and every far vertex becomes
anchor + size × (irrational unit vector); the sizes stay exact and the *positions* go. A strut's
parametrisation is the other way round — two exact positions, a derived size — and no field added
to `Box` expresses "my far end is this exact point" without making `Width` either redundant or
wrong. Extending `Box` with an "endpoint mode" was considered and rejected: every reader of
`Box.Width`, `Box.Vertex`, `Footprint`, `Solid`, the cut list and the propagator would branch on
the mode, which is a second entity hidden inside the first. So the strut is its own entity, held
to the standard §1.1 held `Box` to — it stores what was decided and derives what that produces:

```csharp
/// A member between two points in space, not axis-aligned. Its cross-section is a rectangle from
/// stock; its length, direction and end cuts are derived from where its ends are.
public sealed record Strut(
    EntityId Id, LayerId Layer,
    Point3 From,            // exact: where the centreline meets the surface the From end is cut to
    Point3 To,              // exact: the same at the other end
    EndCut FromCut,         // the plane the From end is cut to: an axis-aligned plane through From, or square
    EndCut ToCut,
    Length Height,          // the cross-section size along local Y: in the drawn plane, > 0
    Length Depth)           // the cross-section size along local Z: out of the drawn plane, > 0
    : Entity(Id, Layer)
{
    public Part? Part { get; init; }         // stock, species, quantity, PlanAxes with X = length (§3a.6)

    public Vector3 Direction => To - From;   // exact integer vector; never normalised in the kernel
    public StrutFrame Frame();               // the local axes as exact integer vectors (§3a.3)
    public StrutBlank Blank();               // the board to cut: length and end cuts, each rounded once (§3a.4)
    public Solid Solid();                    // for drawing and picking, in double (§3a.7)
}

/// Which plane an end is cut to. The world is axis-aligned except for struts, so these are the
/// only planes an end can meet.
public enum EndCut { Square, X, Y, Z }
public enum StrutEnd { From, To }
```

Three choices inside the record, each argued:

- **The sizes are named `Height` and `Depth`, as on `Box`, and there is no `Width`.** A strut has
  three finished dimensions like any part; its length is derived, and the other two are the
  cross-section — the size in the drawn plane (local Y) and the size out of it (local Z), in
  `Box`'s own names for local Y and local Z. `Part.PlanAxes` maps the stock names onto them
  exactly as it does for a box, with one rule: `PlanAxes.X` must be `length`, because the strut's
  local X is its centreline by construction, and a `Part` saying otherwise is refused. `{ x:
  length, y: width }` is a 2x4 with its wide face in the drawn plane (`Height` 3½″, `Depth` 1½″);
  `{ x: length, y: thickness }` is the same 2x4 on edge. §1.2's plain-English trap applies once
  more and is not repeated. The sizes live on the strut, not on `Part`, for §1.1's reason: a
  strut with no `Part` is still a solid in space.
- **The ends are on the centreline** — the point where the strut's axis meets its cut plane — and
  not at a corner of the end face. Whichever point is chosen, the others are irrational (the
  corners of a tilted end face sit at the centreline point ± half a size along two irrational
  unit directions), so exactness does not decide it. Two things do. A centreline point does not
  move when the stock changes — assign a 2x6 in place of a 2x4 and the leg stays where it was
  drawn, wider — which keeps sizes and positions independent, and that is what the propagator's
  two-phase structure assumes (§3a.5). And a centreline is what a person lays out: the sawhorse's
  feet are set out from the rail's centreline, not from a corner of a foot. §11 decision 18.
- **The end cut is an axis, or square.** Every surface a strut can meet in this model is a face
  of an axis-aligned box or the floor, so the cut planes are the three axis-aligned planes
  through the endpoint, or none (a square end, cut perpendicular to the centreline, for a member
  that butts into nothing flat or is fixed by hardware). An exact `Vector3` normal was considered
  for generality and rejected: the one case it would add — a 45° plane, `(1, 1, 0)` — has no
  present use, and every other non-axis plane has an irrational normal and is not expressible
  anyway. §11 decision 21.

**Does this reopen the wrapper objection?** Partly, and honestly. §1.1 rejected a `Solid` entity
beside `Box` because it would be "a second thing with a second reference vocabulary, a second
propagator path and a second cut-list reader, *for an object that is the same board*." A strut
has a second reference vocabulary (`StrutEndRef`), a second scalar set in the propagator, a second
`Part.SizeOn`, a second solid builder and a second plan-view drawing path. What it does not have
is the part of the objection that made it decisive: no code path ever has to know that a strut
and a box are one thing, because they are not. It is a second *kind of part*, not a second
representation of the same part, and the cost is the ordinary cost of a second entity — the same
cost `Node` and `Segment` already pay beside `Box`. The alternative that avoids the cost (a box
with a tilt) puts the sawhorse on the solver, which is the outcome this section exists to avoid.

### 3a.3 The end cut is a plain mitre in the strut's own drawn plane

This is the scope-reducing finding, and it was looked for hard before being believed. A sawhorse
leg's end is cut so that it sits flat on the floor while the leg tilts in two directions at once,
and magazines call that a compound cut. Shaped-parts §6 excludes compound cuts and §4.2 restates
the exclusion as a property of extrusion: a cut is square through the cap. The question is whether
the strut's cut is *still* square through its cap in the strut's own frame, so that no new cut kind
exists — and the answer is yes, under a condition that can be stated exactly and checked in
integers.

**The rule.** Let `d` be the strut's direction and `n₁`, `n₂` the normals of the planes its two
ends are cut to (world axes, or absent for a square end). A cut is "square through the drawn
plane" when its cut plane contains the drawn plane's normal — local Z — which is to say `nᵢ ⊥
local Z`. Local Z is also perpendicular to `d` (it is a cross-section axis). So a local Z that
makes *both* cuts plain mitres in *one* drawn plane exists **iff `d`, `n₁` and `n₂` are coplanar**,
and that plane is the drawn plane. Case by case:

| Cuts | Condition | The drawn plane | Example |
|---|---|---|---|
| `Z`, `Z` — floor and a horizontal underside | none: `d` and `Z` always span a plane | the vertical plane through the strut | **a sawhorse leg splayed both ways** |
| `Y`, `Y` (or `X`, `X`) | none | the plane through `d` and that axis | a diagonal brace between two joists' faces, drawn flat in the plan |
| `Z`, `Y` — floor and a vertical side | `d ⊥ X`: the strut lies in a north–south vertical plane | the YZ plane | a leg leaning sideways only, its top against a beam's side |
| `Z`, `X`; `Y`, `X` | `d ⊥ Y`; `d ⊥ Z` | the XZ plane; the XY plane | a knee brace from a post's face to a beam's underside |
| one cut, one square | none | the plane through `d` and the cut's axis | a Windsor-style leg, square-topped into a seat's underside |
| square, square | none: `Z` is used as the reference | the vertical plane through the strut | a bare brace fixed by hardware |
| any pair failing its condition | — | none exists | **refused: `StrutNeedsBevel`** |

When the condition holds, each end cut in the strut's frame is a straight line across the drawn
face at an angle — which is a `CornerCut` whose setback along local Y is the full `Height`
(shaped-parts §1.3: "a `CornerCut` whose setback along one edge is the *whole* of that edge is a
mitred end"). No new `Cut` kind, no bevel, nothing shaped-parts' outline, invariants and cut-list
sentences do not already handle. When it fails, no drawn plane contains both cuts, so at least one
of them is a bevel with respect to any face — and a bevel is what the whole model declines to
represent.

**Why it looks compound.** A leg splayed both ways and cut to the floor has a vertical face — the
drawn face — and on that face the cut is one angle from square. Set the leg on a mitre saw with
that face down and swing the blade: one setting. The "compound" of a magazine plan arises when the
board is cut with a *different* face down — its wide face, say, when the wide face is not the
vertical one — and the same plane then needs a swing and a tilt. That is a saw-setup fact, not a
geometric one, and shaped-parts §6 already says the cut list says what to cut and not how to set
the fence. The cut-list sentence names the drawn face's angle, as it does for every mitre.

**The construction that genuinely needs a bevel, named so it is not assumed.** A sawhorse whose
legs' wide faces lie flat against the beam's vertical sides *and* splay sideways cannot exist with
flat contact unless the beam's sides are bevelled to the splay; the leg's end cut is then compound
by geometry, not by setup. The beam is a `Box`, and a bevel on it is out (shaped-parts §6). That
construction is `StrutNeedsBevel` for the leg and unrepresentable for the beam, and it is not what
§3a makes possible. What §3a makes possible is the sawhorse whose legs sit on the floor and meet a
horizontal surface — the beam's underside, or a top plate — and are fixed there, with any two-way
splay at all.

**The frame, exactly.** Everything below is integer arithmetic on stored values; the only
irrational quantities are unit lengths, which the kernel never forms.

1. `n_ref` is the highest-priority axis among the two cuts, in the order `Z`, `Y`, `X`; `Z` when
   both ends are square.
2. `d` is oriented so that `d · n_ref > 0` — for `Z` cuts, the lower end is first. If the stored
   `From`/`To` have `d · n_ref < 0`, the derivation swaps them for its own purposes and nothing
   stored changes. This is what makes the derived blank canonical: two legs drawn foot-first and
   rail-first, or two legs mirrored across the sawhorse's centre, derive the *same* board.
3. Local X is `d`. Local Z is `z = d × n_ref`, an integer vector, nonzero because `d` is not
   axis-aligned (invariant 14). Local Y is `y = z × d = n_ref |d|² − d (d · n_ref)`, also an
   integer vector, and `y · n_ref = |d|² − (d · n_ref)² > 0`: local Y points toward the reference
   axis. `(d, y, z)` is right-handed.
4. The other cut's axis `n₂` is admissible iff `n₂ · z = 0` — the coplanarity condition, one
   integer dot product. For `n_ref = Z` and `n₂ = Y` that is `dₓ = 0`, as the table says.
5. The setback at an end cut to axis `n` is `s = Height · tan α`, where `tan α` is the ratio of
   the cut normal's local-Y and local-X components: `tan α = (|y · n| / |y|) / (|d · n| / |d|)`.
   For a `Z` cut that is `Height · √(dₓ² + d_y²) / |d_z|` — the run over the rise, times the
   size in the drawn plane. Irrational in general; rounded once in §3a.4.
6. Which corner each cut removes follows from the sign of the cut's outward normal's local-Y
   component, and with steps 1–3 fixing the signs it is: at the first end, `SouthWest` when the
   normal's local-Y component is negative and `NorthWest` otherwise; at the second end,
   `NorthEast` when positive and `SouthEast` otherwise. Two parallel cuts (`Z`, `Z`) give
   `SouthWest` and `NorthEast` — a parallelogram, the shape a board takes when both ends are cut
   at one mitre setting from one reference edge. Two perpendicular cuts (`Z`, `Y`) give
   `SouthWest` and `SouthEast` — a trapezoid. The §9 cases pin all four combinations.

### 3a.4 What is exact and what rounds, precisely

Following shaped-parts §1.3's discipline: name every stored value, every exact derivation, and
every place a rounding happens, so that nobody discovers a second one later.

**Stored, exact.** `From`, `To`, `Height`, `Depth`, `FromCut`, `ToCut`, and `Part.PlanAxes`. These
are the strut's scalars; they are all the propagator, the checker and the loader ever read.

**Derived, exact, in integers.** `d`; `|d|²` and the run's square `dₓ² + d_y²` in `Int128`; the
three local axis directions of §3a.3 as integer vectors; the axis-aligned test, the coplanarity
test and the corner-site rule. Invariants 14–16 use only these.

**Derived with exactly one rounding each — the `≈` set.** For a strut with centreline length `c
= |d|` and setbacks `s₁`, `s₂` (zero at a square end):

| Number | Formula | Rounded |
|---|---|---|
| the blank's length `L` | `c + (s₁ + s₂) / 2` — the long-point length: the centreline meets each cut half a setback in from the blank's end | once, from the exact inputs |
| the setback `s₁`, `s₂` at each cut end | §3a.3 step 5 | once each |
| the tilt from the reference axis, the azimuth in the plan | `atan` of ratios of the exact components | display only; never stored, never a `Length` |

`L` is computed from `c`, `s₁` and `s₂` **as doubles, from the exact integers, and rounded once**.
It is not `round(c) + round(s)`, and the reason is a golden case, not a principle: for `d = (1″, 0,
12″)` and `Height = 3½″`, once-rounding gives 12 629 units and the sum of two roundings gives
12 630. The rule the solver already lives under — one rounding per scalar, from exact inputs
(geometry-model §5.4) — is the rule here. A strut therefore carries up to three once-rounded
numbers (two for a parallelogram, whose setbacks are equal; one for a square-ended brace), none
of them computed from another rounded one. That is the honest answer to "is it one rounding or a
compounding of two": it is *several independent* roundings and *no* compounding, and the test in
§9 constructs the case where the difference shows.

**Proven exact, or marked.** A rounded value is not automatically inexact: a 3-4-5 leg — `d = (3″,
0, 4″)`, `Height 3½″`, cut `Z` at both ends — has `c = 5″` exactly, `s = 3½″ × ¾ = 2⅝″` exactly,
and `L = 7⅝″` exactly, and its row should print no `≈`. Exactness is **proven in integer
arithmetic**, never inferred from the double: `c` is exact iff `|d|²` is a perfect square (an
integer square-root helper over `Int128`, to be written — there is none in the BCL); a `Z`-cut
setback is exact iff `Height² (dₓ² + d_y²)` is `d_z²` times a perfect square; `L` is exact iff
its parts are and the half is on the grid. Anything not proven is `≈`, including a value whose
proof would overflow `Int128` (products of four lengths: only a part beyond ~2⁴⁰ units, a
thousand miles, gets there) — the helper checks the magnitude first and declines rather than
throwing, so that `≈` is the conservative answer everywhere. The existing `≈` is not enough for this: `FormattedLength.IsExact` (geometry-model §1.4,
place 4) says whether the *displayed* text equals the *stored* value, and a derived length that
rounds to 27 5/16″ would print without a marker although it is not the leg's length. So the
derived blank carries the proof beside each value, and the cut list reads it (§3a.6). The
alternative — always mark a strut's numbers `≈` — is simpler and lies about the 3-4-5 leg;
§11 decision 20 recommends proving.

```csharp
/// The board a strut is cut from: derived, never stored. Each length is on the grid and says
/// whether it was proven to be the exact value or was rounded onto the grid once.
public sealed record StrutBlank(
    DerivedLength Length,            // the long-point length L
    ImmutableList<DerivedCut> Cuts); // the end cuts as CornerCut(site, setback, Height), in SITE order
                                     // like Box.Cuts — never "from"/"to", because §3a.3 step 2 may
                                     // have reoriented the pair; zero, one or two entries

public readonly record struct DerivedLength(Length Value, bool Exact);
public readonly record struct DerivedCut(Cut Cut, bool Exact);   // Exact: the setback was proven on the grid
```

**Where the `double` lives, and where it may not.** `StrutBlank.Derive` is one pure function in
`Napkin.Core.Geometry`, and it is the third caller of `Length.FromInches(…, HalfToEven)` after the
solver boundary and decimal entry (geometry-model §1.4, place 2). Its operation order is fixed —
the exact integer sum of squares, one `Math.Sqrt`, the multiplication and division in the order
§3a.3 step 5 writes them — so that two struts with equal `|dₓ|`, `|d_y|`, `|d_z|`, `Height` and
cuts produce bit-identical doubles and therefore identical rounded values on every machine
(IEEE 754 square root is correctly rounded), which is what lets mirror-image legs group into one
row. The `Propagator` and `RelationshipChecker` never call it. `Sketch.Validate` calls it exactly
once per strut, for invariant 17 and only after invariants 14–16 have passed on the integers —
and that is the route by which the loader and the direct updater's post-write check reach it
(§3a.5, P19). The solid (§3a.7) is built from the exact frame in `double` for the screen and is a separate
derivation that does not go through the blank — two derived views of one exact thing, as the arc
of a rounded corner is drawn in `double` from exact endpoints while the outline stores none of it
(shaped-parts §1.5).

**What this does to the guarantee, said plainly.** The kernel's promise was never "nothing
rounds"; it was "nothing rounds silently, and nothing the propagator or the checker reads is a
rounded value" (geometry-model §1.4, §5.4). Both halves hold. A strut's row shows `≈` on the
numbers that earned it and nothing else; a sawhorse's relationships hold exactly; and the numbers
a person takes to the saw are on the 1/1024″ grid, 32× finer than the finest mark on the tape,
with the marker telling them so. What is *lost* is the ability to type a leg's length and have
the design follow; that is the trade §3a.1 makes on purpose, and it is reversible only by the
solver.

### 3a.5 The strut in the relationship system

A strut is not a one-shot placement. The case for participation is not that it would be nice; it
is that a sawhorse that does not track its rail is a drawing, not a design — raise the rail and
four legs are wrong until retyped — and that participation costs *nothing new*, because the
endpoints are already exact scalars. "Typed once, retyped by hand" was considered as a first cut
and rejected: it would make the strut the only entity in the sketch that relationships cannot
reach, and the sawhorse would be the one sample whose parts are not held together.

**The reference.** `StrutEndRef(strut, end)` is a `PlaceRef` fixing X, Y and Z at the end's stored
point. Under §2.3's rules it takes part in `Coincident` (with a box vertex, a `CenterRef`, the
other strut's end, or a `NodeRef` on X and Y), `AxisDistance` and `Centered` along any axis, and
nothing else. No relationship kind is added, no legality rule changes, and `PlacesNotComparable`
covers the nonsense pairings. A strut's *body* is not a place: there is no `Flush` to a strut's
face and no `Coincident` with a corner of its end, because those are irrational, and §3.3 keeps
them out.

**The one new propagation rule.** Geometry-model §4.4 step 3 says a positional implication on a
box "moves the *whole* other entity by the same delta". Applied to a strut that rule would be
wrong: raise a rail and the leg whose top is under it would lift its foot off the floor. So, for a
strut, **a positional implication on an end assigns that end's three scalars and nothing else**;
the other end keeps its value unless something else moves it. A strut moves as a unit only under
`Drag`, whose rigid group includes it as an entity (both ends) when either end is reached through
a relationship, exactly as the group includes a segment through its nodes and holds both
(geometry-model §10.3). The precedent is the node: a segment's two nodes are independently
assignable scalars coupled only by drag, and a strut is, for the propagator, a segment in three
dimensions whose ends happen to carry a cross-section. The case that forces the rule rather than
merely motivates it: a knee brace with its foot `Coincident` to a post's vertex and its top
`AxisDistance` to a beam's underside. Make the post taller and the beam rises; under the
whole-entity rule the brace's foot would have to rise with its top *and* stay at the post's
vertex — a contradiction on every resize of a sketch that is perfectly consistent. Under the
per-end rule the top follows the beam, the foot stays on the post, and the brace lengthens,
which is what the derived length is for.

**The two phases hold trivially.** The propagator settles sizes before positions because a box's
corner depends on its size (geometry-model §10.3). No strut position depends on a strut size —
the ends are on the centreline (§3a.2) — so a strut's `Height` and `Depth` can change in phase one
without moving anything, and its ends move in phase two without reading a size.

**What each request does.**

- `SetStrutEnd(strut, end, p)` seeds that end's three scalars with `p`; propagation runs as for
  `SetPosition`. The other end is not seeded. `Rejected(StrutIsAxisAligned)` or
  `Rejected(StrutNeedsBevel)` when the result would fail invariant 14 or 15 — the invariants are
  checked on the written sketch, after propagation, the way shaped-parts §2.3 checks a cut's fit —
  and `Rejected(StrutTooShortForItsCuts)` for invariant 17.
- `DragStrutEnd(strut, end, δ)` is the best-effort form, the strut's analogue of `DragFace`: the
  end moves by as much of `δ` as the relationships on it allow, per axis, and the applied delta
  is reported. Never `OverConstrained`.
- `Drag(strut, δ)` moves the rigid group — both ends — with the anchored-entity zeroing of §4.4.
- `SetPosition`, `SetOrientation`, `DragFace`, `SetCut` and `RemoveCut` on a strut are
  `Rejected(UnsupportedRequest)`: a strut has no anchor, no `FaceUp`, no resizable face and no
  cut list of its own.
- `SetParameter` on a `ParamValue(StrutHeightRef …)` or `(StrutDepthRef …)`, `EqualParam`
  between those and any other size ref (a leg's `Depth` equal to a rail's `Height` — both are the
  1½″ of a 2x), and stock assignment landing `LumberStock`'s width and thickness on them by
  `PlanAxes` — all as for a box. There is **no `StrutLengthRef`**: a `ParamValue` on a strut's
  length is the `Distance` relationship, tolerance class, #28's, and a canvas that offers it gets
  `Rejected(UnsupportedRelationship)` as it does for a `SegmentLengthRef` today (geometry-model
  §10.3).
- `Anchored(strut)` pre-assigns all eight scalars — both ends, `Height`, `Depth` — with the same
  "stands aside for the number being edited" reading §10.3 gives a box.
- `AddEntity` validates invariants 14–17; `RemoveEntity` cascades as for any entity.

**The sawhorse, held together.** The fixture of §9 case 30 is: a 2x6 rail on edge, `Anchored`;
four 2x4 legs, `FromCut = ToCut = Z`, wide face in the drawn plane. Per leg, three relationships
on its `To` end — `AxisDistance(rail.Face(Bottom), leg.To, Z, 0)`, `AxisDistance(rail.Face(West)`
or `(East), leg.To, X, ±3″)`, `Centered(leg.To, rail.Face(South), rail.Face(North), Y)` — and,
tying the feet together, `AxisDistance(legA.From, legB.From, Z, 0)` around the four. Then:

- **Raise the rail** — `SetPosition` on it with its anchor removed, or `SetParameter` on an
  `AxisDistance` that sets its underside's height above a foot: every leg's `To` follows the
  underside; every `From` stays; the derived length of all four grows by the same once-rounded
  amount; the cut list's one row of four shows the new `≈` length. Nothing else moves. That is
  the per-end rule doing what a person expects: a taller sawhorse has longer legs.
- **Lengthen the rail** by typing its `Width`: the two legs at the east end follow the east face
  through their `AxisDistance` on X; the west legs stay; all four feet stay, because nothing
  holds a foot to its top — so the east legs tilt more and lengthen, honestly, until the person
  moves their feet. To make the feet follow, the fixture holds each foot to its own top with two
  more exact relationships, `AxisDistance(leg.To, leg.From, X, ±6″)` and `(…, Y, ±9″)` — legal,
  since both ends fix every axis — and then the whole sawhorse rescales with its rail. That is the
  useful sawhorse: five ordinary relationships per leg and a ring of four between the feet, no
  new kind among them.
- **Drag the rail** with the anchor removed: the rigid group is the rail, the four legs (through
  their tops) and every foot (through the legs); the sawhorse moves as one.
- **The floor is not an entity.** Nothing holds a foot at Z = 0 except that nothing moves it — the
  same stance §2.3 takes for a leg standing at the right Z under a top before a `Flush` says so.
  The feet are held *level with each other* by the ring of `AxisDistance`s, and to the datum by
  one leg's foot being related to something that is, or by nothing. A floor entity is not added
  (§6).

**Invariants**, continuing §1.6's numbering:

14. A `Strut`'s ends differ in at least two coordinates: `d` is not axis-aligned. An axis-aligned
    member is a box, and a strut whose ends differ in one coordinate is refused
    (`StrutIsAxisAligned`) rather than allowed to be a worse box.
15. `From ≠ To`, `Height > 0`, `Depth > 0`, and the end cuts satisfy §3a.3's coplanarity
    condition (`StrutNeedsBevel` otherwise).
16. If the strut has a `Part`, `Part.PlanAxes.X == length`.
17. The derived blank satisfies shaped-parts invariants 7–9 on its rounded values — the setbacks
    fit the length and what is left has positive area. This is the only invariant judged on
    derived values, and it is judged *exactly* on them, by the same code that judges a box's cuts.
    A very short strut between two faces at a shallow angle, whose two mitres would cross, is
    `StrutTooShortForItsCuts`.

### 3a.6 The cut-list row, the group key and the marker

`Part.SizeOn(strut)` reads `Blank().Length`, `Height` and `Depth` through `PlanAxes` — length is
always the derived one, the other two are stored — and never `From` or `To` directly. The rule
that replaces `CUT-002` for struts, to be given the next free `CUT-` id: **a strut's listed
length is derived from its two ends, rounded once, and marked `≈` unless proven exact; the cut list
reads nothing else about its placement.** `CutListRow` gains what it needs to say so:

- `Length` stays a `Length`; a `bool LengthExact` beside it is `true` for every box row and is the
  blank's proof for a strut row. `CutListCsv.Text` and the on-screen list prefix `≈` when
  `!IsExact || !LengthExact` — the existing marker, one more reason to show it. The CSV's shape
  does not change, and the parse-back test (parts §7 step 5) already reads a marked field as
  text.
- `Cuts` holds the derived `CornerCut`s, so `CutDescription.Describe` produces shaped-parts
  §4.4's mitre sentence unchanged — "Mitre the west end: from ≈ 1 13/32″ in along the south edge
  to the north-west corner (≈ 22° off square)" — with the setback marked when the blank says it
  was rounded, and the angle marked as it already is unless exact. A parallelogram's two like
  cuts collapse to one sentence by the existing rule.
- **The group key** compares the rounded values, the cuts in site order, stock and species, as
  today. Because the derivation is canonical and deterministic (§3a.3 steps 1–2, §3a.4's fixed
  order), the four legs of a symmetric sawhorse — two pairs of mirror images — are **one row of
  four**, as four hand-drawn box legs are. Two legs that differ in placement by one unit are two
  rows, honestly.
- **The shopping list is unchanged.** It consumes rows; a strut's row is a board of the listed
  length from the listed stock.
- **The three committed `expected.json` files are still byte-identical** (§9 case 20): none of
  them contains a strut. The sawhorse sample is a fourth, with its derived length hand-computed
  in its design file (§9 case 30).

### 3a.7 Drawing, picking and creating a strut

- **The solid** is the derived blank's outline — a parallelogram or trapezoid in the drawn plane —
  extruded by `Depth` along local Z and placed through the exact frame: six planar quads, every
  vertex computed in `double` from the exact ends, the exact integer axes and the exact sizes.
  Its end faces are the planes through `From` and `To` with the stated normals, exactly, so what
  is drawn sits flat on the floor even where the rounded setback would be a hair off; the blank
  and the solid are two derived views of the same exact input (§3a.4). The renderer's three-tone
  rule keys on the *dominant* world component of a face's normal, which for an axis-aligned face
  is the axis it already uses.
- **Picking** in the 3D view is the ray against the solid's six polygons in `double`, §8.2 step 3.
  A strut's end points are picked by projected proximity, as vertices are.
- **In the plan view**, a strut draws its silhouette from above — the solid's XY projection, in
  `double`, like an arc — with its two ends as grips at their exact XY. Hit-testing is a point-in-
  polygon in `double`, the stance shaped-parts §2.4 already takes for a curved edge. A strut's
  body is not a snap target (nothing can be flush to it); a strut's *end* snaps to a box's upright
  edges and vertices and to the grid, producing `Coincident` or `AxisDistance` on the end, and a
  box's feature snaps to a strut end as to any point. The orientation glyph of §7.2 is not drawn;
  a diagonal silhouette is its own glyph.
- **Creating one.** Two clicks. In the 3D view, each click is a point on what the ray hits (§8.2),
  snapped to the grid step: a face, or an edge — and the edge matters, because the default
  isometric camera looks from above and a rail's underside is culled (§8.4), so the click that
  puts a leg's top under the rail lands on the rail's **lower edge**, which is its visible lower
  silhouette, is picked by §8.2 step 4, and fixes Y and Z exactly with X taken from the pointer
  along it. A click on the floor grid is a point at Z = 0. Then stock. **The end cut defaults to
  `Z` for a click on the grid or the plan datum — the datum plane *is* the floor — and to the
  axis of the face or the face-pair of the edge that was hit (`Z` for a lower edge, the
  underside being the face a leg meets); `Square` is chosen, never defaulted.** In the plan view,
  both clicks are at Z = 0 with `Z` ends, and the properties panel edits each end's Z and cut,
  because the plan cannot see the underside of anything (§6, elevation editing is not in this
  slice). Both produce one `AddEntity` through `DesignEditor`. The tool lives in both views
  because the sawhorse *is* designable from the plan alone — feet in the plan, tops in the plan
  under the rail, two typed Z values — while the 3D view is where it is natural. §11 decision 22.
- **The properties panel** shows both ends editable, `Height` and `Depth` editable (or driven by
  stock), the end cuts as two pickers, and three read-only derived readouts — length, tilt from
  the reference axis, azimuth — each marked. Typing a tilt to place the far end is an *entry
  mode* that rounds the computed end once and flags it, the analogue of shaped-parts' angle entry
  for a mitre; it is a later nicety, not this slice (§11 decision 23).
- **Dimensions.** An `AxisMeasurand` whose two places are strut ends and whose axis is X or Y is
  legal under invariant 13 as written — a plan dimension between two feet is what a sawhorse
  drawing carries — and is refused along Z like every other. There is no `ParamMeasurand` on a
  strut's length, for the reason there is no `StrutLengthRef`.

### 3a.8 What this unblocks, and what it still does not

**In scope, by this section:** any member between two exact points in space whose ends are cut to
axis-aligned planes or square, with the two cuts coplanar with the member (§3a.3's table). Worked
against Marc's named examples:

- **A sawhorse** with any two-way splay, legs to the floor and to a horizontal surface: fully
  representable, held together by existing relationships, one cut-list row for four legs.
- **A splayed stool or bench:** the same with a seat for a rail.
- **A Windsor-chair leg:** as a rectangular stand-in for a turned leg — a square top into a point
  on the seat's underside, a `Z` cut at the floor. The round section and the tapered tenon are
  not modelled; the *length and the angles* are, which is what a person needs to drill the seat.
- **An A-frame:** two struts in one vertical plane, `Z` at the feet, `Y` (the plane between them)
  at the apex, `Coincident(A.To, B.To)`. Their mitred tops meet on an axis-aligned plane because
  the frame is symmetric; an unsymmetric A-frame's apex plane is not axis-aligned and is excluded
  below. The cross-tie is a box positioned by `AxisDistance` from a foot or the apex with a typed
  length, and *nothing holds its ends to the legs' faces* — that would be a `Flush` to a strut's
  face, which is excluded — so lengthen the frame and the tie is retyped. Said so it is not
  assumed.
- **A diagonal brace between two rectilinear parts** — a knee brace from a post's face to a beam's
  underside (`X`, `Z`, `d ⊥ Y`), a let-in brace across studs (`Z`, `Z`, in the wall's plane, which
  puts the drawn face parallel to the wall, as it should be), a deck's diagonal brace between two
  joists (`Y`, `Y`, drawn flat in the plan): each is one strut and two or three relationships.
  §9 case 29 lands the knee brace's foot on a post *vertex* because that pins three axes in one
  relationship, which is what the test wants; a real brace lands its foot on the post's *face*,
  which is two `AxisDistance`s and a `Centered` — the same scalars, one relationship at a time.

**The solver seam is untouched.** No `Orientation` changes, no `Rotation` outside a quarter turn,
no `RotationNotSupported` path reached, no tolerance consulted. When #28 arrives, a strut's eight
scalars are solver variables loaded exactly like a box's, and its derived length is not a
variable — the same position shaped-parts §3 takes for a cut's setback. `Distance` on two strut
ends is the relationship #28 would add to type a leg's length, and it is tolerance class there.

**Still excluded, each for a reason that survives this section** (§3.3's list, in one place):

- A strut whose cuts need a bevel (non-coplanar cuts; the legs-against-a-bevelled-beam sawhorse).
- A strut cut to meet a plane that is not axis-aligned: an octagon's rails mitred to each other,
  an unsymmetric A-frame's apex, a brace mitred against another brace.
- Cuts on a strut beyond its two end cuts: a tapered or chamfered splayed leg. A strut has no
  `Cuts` list; its blank is derived, and shaped-parts' cuts are stored against a stored blank.
- A strut whose drawn face is *not* the plane its cuts force — a leg deliberately rolled so that
  neither face is vertical. No axis-aligned surface it could meet needs it.
- Relationships on a strut's length or angle, and a typed length (`Distance`, #28).
- A `Flush` to a strut's face, or a `Coincident` with a corner of its end face (irrational).
- A tilted or spun `Box` (§3.3, first bullet): shaped, related rectangles at an angle stay #28's.
- A floor entity, physics, joinery, the round section of a turned leg: the non-goals of §6 stand.

---

## 4. Cuts extrude into a solid

### 4.1 The outline is the cap

A shaped part's `Outline()` is the closed boundary of what is left of the blank in the local XY
plane (shaped-parts §1.5). Shaped-parts §1.4 states that every cut "is made perpendicular to the
plan view and runs the full out-of-plane dimension of the part". In three dimensions that sentence
is not a limitation to describe; it is the definition of the solid:

> **`Box.Solid()` is the local outline extruded along local Z from 0 to `Depth`, then oriented.**

```csharp
/// The solid a blank and its cuts leave: a prism whose caps are the outline. Derived, never stored.
public sealed record Solid(ImmutableArray<SolidFace> Faces);

/// One planar or singly-curved face of the solid, in world coordinates.
public sealed record SolidFace(BoxFace? Of, ImmutableArray<SolidSegment> Boundary);

public abstract record SolidSegment(Point3 From, Point3 To);
public sealed record StraightSegment3(Point3 From, Point3 To) : SolidSegment(From, To);
public sealed record ArcByCenter3(Point3 From, Point3 To, Point3 Center) : SolidSegment(From, To);
public sealed record ArcThrough3(Point3 From, Point3 Through, Point3 To) : SolidSegment(From, To);
```

Mechanically, from the local outline `O` with segments `s₁ … sₙ`:

- **Bottom cap** (`Of = Bottom`): `O` lifted to local z = 0, walked in reverse so that it winds
  outward-facing (downward).
- **Top cap** (`Of = Top`): `O` lifted to local z = `Depth`, walked as is.
- **One side per outline segment**: for a `StraightSegment` from `a` to `b`, the quad `a₀ b₀ b_D
  a_D` (`Of` is the `BoxFace` the segment lies on when it is an uncut run of an edge, and `null`
  for a cut face — a mitre face, a chamfer); for an `ArcByCenter` or `ArcThrough`, the singly-
  curved patch between the arc at z = 0 and the same arc at z = `Depth`, represented by its two
  arcs and two straight rulings.

Every vertex of every face is `Anchor + Orientation.Apply((x, y, z))` for an outline point `(x,
y)` and `z ∈ {0, Depth}`: exact for all 24 orientations, for the reason §1.3 gives, plus the one
half-unit rounding of an edge's middle that the outline already has. The arcs stay circular arcs
lying in the two cap planes with exact endpoints and an exact centre or third point; the curved
side patches are ruled between them. Nothing here is a new geometric primitive: it is the
existing outline, a `Depth`, and a signed permutation.

A plain rectangle's solid is six quads — the box's six faces, each carrying its `BoxFace` — which
is what the renderer draws for the common case without ever computing an outline.

### 4.2 What this settles

- **Bevels and compound cuts stay out.** A cut is square through the cap: the side face it makes is
  perpendicular to the caps. That is shaped-parts §6's first non-goal restated as a property of
  the extrusion. It holds for a strut too: its end cuts are square through *its* cap, in its own
  tilted frame, and look compound only from the world's axes (§3a.3). A cut that is not square
  through some cap of the part it is on is a bevel, and there is none anywhere in the model.
- **Cuts turn with the box.** A chamfer drawn on a leg's footprint is a chamfer down the leg's
  length whichever way the leg is then oriented. The workshop (shaped-parts §7) shows the blank
  in its own frame, unrotated, and is untouched by this design.
- **The cut list's sentences are unchanged.** They name corners and edges in the part's own frame
  as drawn (shaped-parts §4.4), which is the local frame, which orientation does not touch.

---

## 5. The cut list and the shopping list are unchanged

Position and orientation are about assembly. What is bought and what is cut are not.

- The cut list reads `Width`, `Height` and `Depth` through `PlanAxes` — the three stored sizes —
  and never `Anchor`, `FaceUp` or `Rotation` (`CUT-002` extended to the third size, which was
  already the rule for `OutOfPlane`). A leg standing up, lying down or turned over lists as the
  same leg.
- The group key (parts §3 step 4, shaped-parts §4.3) is the three finished dimensions, stock,
  species and cuts. Orientation is not in it, so four legs turned four ways are one row of four,
  and the two spellings of an apron in §1.3 are one row too.
- The shopping list consumes rows. Nothing in it changes.
- **The three committed `expected.json` files are byte-identical before and after this design**
  (§9 case 20). That is the proof, and the strongest test in the plan: if any expected cut list,
  shopping list or CSV changes, something read a coordinate it should not have.
- **The strut is the one stated exception**, and it is stated in §3a.6, not discovered: its
  length is its placement, derived from its two ends, rounded once and marked. Every other
  sentence in this section is about boxes and is unchanged.

---

## 6. Non-goals

Stated so scope does not creep past "rotate things in 3D and put the shapes together". Each is a
real thing; none is this design.

- **Physics of any kind.** No collision detection, no interference or overlap detection, no
  gravity, no deformation, no simulation. Two boxes that occupy the same space are allowed and
  **not flagged**; a joint that would need a tenon is two boxes that abut or overlap. A later
  "parts overlap" report is §11 decision 15; it is not here.
- **Non-axis-aligned rotation of a box.** §1.3 and §3.3: a box has no tilt and no in-plane angle
  other than a quarter turn. An angled *member* is a strut (§3a), a different entity with a
  different stored form; the sawhorse is in scope through it, and what a strut still cannot be
  — bevelled, cut to a non-axis plane, shaped beyond its two end cuts — is named in §3a.8 so it
  is not silently assumed possible.
- **A floor entity.** A strut's foot is at Z = 0 because it was put there; nothing represents the
  floor, and nothing needs to (§3a.5).
- **Lighting, materials, textures, shadows, perspective.** The renderer draws flat faces in three
  tones by axis with edges as lines (§8). A perspective camera is §11 decision 10, not in the
  first slice.
- **Mesh or solid modelling.** No booleans, no lofts, no arbitrary polyhedra; the only solid is a
  blank with cuts, extruded. No `Arc` entity, no curve tools — shaped-parts §6 stands.
- **Joinery volumes.** A dado, a tenon or a rabbet is not modelled; parts-and-cut-list §1.3 stands.
- **Dimensions in three dimensions.** `Dimension` entities stay plan-view annotations and their
  measurands fix X or Y (§7.3). A typed depth is a `ParamValue` with no annotation, exactly as a
  typed size on an undriven box is today (geometry-model §4.1). 3D dimension lines are a later
  feature, if wanted.
- **Editing in elevation views with the plan tools.** The 3D camera can look from the front or the
  side (§7.1), but the rectangle tool, the stock tool, the snap resolver and the workshop operate
  in the plan view only in this slice. Generalising the plan editor to the six axis-aligned views
  is a natural later step and is named as such, not done.
- **Nodes and segments in space.** Plan-plane construction geometry, at Z = 0 (§1.4).
- **Groups, sub-assemblies, exploded views.** "Move the whole table" is what `Drag` already does
  through the rigid group of relationships (geometry-model §4.4); there is no separate grouping.
- **The building module reading a wall's height.** A wall now has a `Depth`; the rules engine
  does not read it, and no header or bracing table in this design's scope needs it.
- **Saving the camera.** The 3D view's camera is UI state, like `ViewTransform`, and is not in the
  file.

---

## 7. What stays 2D, and how the plan view fits

### 7.1 The plan canvas is one camera

The plan canvas is the orthographic camera looking straight down world −Z. `ViewTransform` — the
one place the model meets the screen (its own doc comment) — is that camera's special case: a
scale, a centre and the Y flip. The 3D view (§8) is the same idea with two more angles. Nothing
about the plan view is replaced; it is named for what it always was.

What the plan view draws for a box is its **footprint**: the rectangle the box's eight vertices
project to, which for every one of the 24 orientations is a rectangle whose edges are the four side
faces whose world normals are horizontal.

```csharp
/// What the plan view sees of a box: a rectangle in the plan, spun by Rotation about the anchor.
public sealed record Footprint(
    Point2 Anchor,             // Box.Anchor.XY, verbatim
    Vector2 LowCorner,         // where the rectangle's south-west corner is, relative to Anchor, before Rotation
    Length PlanWidth,          // the rectangle's size along plan X before Rotation
    Length PlanHeight,         // … along plan Y
    Angle Rotation)
{
    public BoxFace FaceAt(BoxEdge planSide);        // which local face the plan sees as that side
    public BoxFeature UprightAt(BoxCorner planCorner);
}
```

The table that defines it, from §1.3's images (extents are relative to the anchor, before the spin):

| `FaceUp` | plan X extent | plan Y extent | world Z extent | plan sees `Width × Height` of |
|---|---|---|---|---|
| `Top` | [0, W] | [0, H] | [0, D] | W × H — **today's box, verbatim** |
| `Bottom` | [0, W] | [−H, 0] | [−D, 0] | W × H, mirrored north–south |
| `North` | [0, W] | [−D, 0] | [0, H] | W × D |
| `South` | [0, W] | [0, D] | [−H, 0] | W × D |
| `East` | [−D, 0] | [0, H] | [0, W] | D × H |
| `West` | [0, D] | [0, H] | [−W, 0] | D × H |

For `FaceUp = Top`, `Footprint` is `(Anchor.XY, (0, 0), Width, Height, Rotation)` — every value the
plan canvas reads today, from the same fields. For any other orientation it is the same shape with
a different corner and different sizes, and the canvas draws, grips, snaps and hit-tests it
without knowing the difference.

### 7.2 What the plan canvas keeps doing, unchanged in behaviour

- **`BoxGeometry`** (grips, `AxisAlignedEdges`, `Contains`, `GripAt`, `OutwardDelta`) reads a
  `Footprint` instead of a `Box`. `AxisAlignedEdges` becomes "the faces whose world normal is
  horizontal", which for a `Top` box are the four sides in their current order.
- **`SnapResolver`** is unchanged in logic: per plan axis, an edge line of another part within
  the radius beats the grid, with the overlap test on the other axis. The relationship it produces
  is `Flush(FeatureRef(moving, Face(f)), FeatureRef(target, Face(g)))` where `f` and `g` come from
  `FaceAt`; a corner snap produces `Coincident` on two upright edges via `UprightAt`. For `Top`
  boxes those are, feature for feature, the relationships it produces today.
- **`RectangleTool` and `StockTool`** draw `Top` boxes at Z = 0. The stock tool knows the depth
  (the stock's thickness, or its width for a part placed on edge — parts §1.2 says which); the
  rectangle tool needs a third size it never needed before, and §11 decision 7 recommends a
  visible default of ¾″ (768 units) in the properties panel, editable, rather than a prompt.
- **`DesignEditor`** — one mutation path (CVS-005), `Apply`/`ApplyQuietly`, gestures as undo
  steps — is untouched. `Drag(id, Vector2)` becomes `Drag(id, (dx, dy, 0))`; `DragEdge` becomes
  `DragFace` on `FaceAt(side)`.
- **The shape workshop and `CutTool`** are untouched: they operate on the blank in its local frame.
- **`Outline()` becomes local-frame**, which changes one contract the plan canvas relies on
  today: `OutlineBuilder` already builds both (`world: false` is the workshop's view), and the
  world-frame outline of a tipped box is not a plan shape at all, so the world form goes. The
  plan then draws a box by one rule: for `Top`, the local outline placed through the footprint
  (`Anchor.XY`, spun by `Rotation`); for `Bottom`, the same mirrored north–south, which is what
  the table in §7.1 says; for the four side-up cases, the footprint rectangle — the solid's
  silhouette from above, on which a cut is not visible. `OutlineDrawing`, `OutlineHitTest` and
  `CutThumbnail` take the local outline plus the placement, and hit-testing follows the same
  three-way split. `DimensionLayout` and `SelectionDimensions` read the footprint.
- **Every existing `GUI-DRAW-*`, `GUI-CUT-*` and `GUI-VIEW-*` workflow passes without change.** That
  suite is the regression test for this section, and §10 step 2 is not done until it does.

The one visible addition to the plan view: a box that is not `Top` draws its footprint with a small
orientation glyph (which face is up), so a leg standing on end and a leg lying down are
distinguishable at a glance. Whether and how is UI. A strut has no footprint rectangle; the plan
draws its silhouette in `double` with its two exact ends as grips, and the strut tool is two clicks
at Z = 0 (§3a.7).

### 7.3 Dimensions stay in the plan

`DimensionPlacement.Side` is north, south, east or west; `DimensionLayout` draws in the plan. A
`Dimension` whose measurand stands vertical in the world — `ParamMeasurand(BoxDepthRef …)` on a
`Top` box, `ParamMeasurand(BoxWidthRef …)` on an `East` box, or an `AxisMeasurand` along Z — has
nowhere to be drawn and is refused by `AddEntity` (`Rejected(UnsupportedRequest)`), by the loader,
and by any `SetOrientation` that would make an existing dimension so (§2.4), in this slice
(invariant 13). The number itself is not lost: it is a `ParamValue`
or an `AxisDistance` in the relationship list, editable from the properties panel and the
relationship badge exactly as an undriven size is edited today. Drawing it as a dimension line in
the 3D view is a later feature (§6).

---

## 8. The 3D view, at a design level

Short, because the UI plan is a later step; what is decided here is what an implementer needs to
know to start: the camera, picking, the gestures, and the rendering approach.

### 8.1 The camera is `double`, the model is not

The 3D view is a **mode the window is in**, the way the workshop is (shaped-parts §7.1): same
document, same selection, same `DesignEditor`, same undo stack; View → 3D and View → Plan switch
between it and the plan canvas. It holds one `Camera` value:

```csharp
/// Where the drawing is looked at from. A value; every navigation verb returns a new one.
public readonly record struct Camera(
    double AzimuthDegrees,        // spin about world Z
    double ElevationDegrees,      // 0 = from the side, 90 = from above (the plan view)
    double CenterX, double CenterY, double CenterZ,   // the model point at the centre, in inches
    double PixelsPerInch,
    Size Viewport)
{
    public Point Project(Point3 p);                   // exact model point → screen, in double
    public (Vector3d Origin, Vector3d Direction) Ray(Point screen);   // screen → model ray, in a
                                                                       // double-precision vector
                                                                       // type that is UI-only
    public Camera Orbit(Vector screenDelta);
    public Camera Pan(Vector screenDelta);
    public Camera ZoomAt(Point anchor, double factor);
    public Camera FitTo(Bounds3 bounds, Size viewport);
    public static Camera Isometric(…);                 // the default: azimuth 45°, elevation 35.264°
}
```

**Orthographic, not perspective, in the first slice.** An orthographic axonometric is what a
woodworking drawing is; parallel edges stay parallel, a length along an axis is the same length
anywhere on screen, and a screen ray is simply the view direction through the pixel, which makes
picking and axis-constrained dragging (§8.3) one projection each. Perspective is §11 decision 10.

Everything in `Camera` is `double`, for the same reason `ViewTransform` is: it is the render edge,
and no view operation ever writes back to an entity. Orbit, pan and zoom are rendering-only. The
plan canvas is `Camera` at elevation 90°, azimuth 0°, and `ViewTransform` may stay as the plan
view's own simpler type or be reimplemented over `Camera`; that is an implementation choice.

### 8.2 Picking: a ray against exact geometry, resolved in double

A click is a screen point; the question is which part, and which face, edge or vertex of it. This
is a UI hit-test, not model data, so it is resolved in `double` — the same stance shaped-parts §2.4
takes for point-in-outline against a curved edge.

1. `Camera.Ray(screen)` gives an origin and a direction in model inches.
2. For each box, **transform the ray into the box's local frame**: subtract the anchor, then
   `Orientation.Unapply` — a signed permutation, exact even in `double` — and run the standard
   slab test against `[0, W] × [0, H] × [0, D]`. The entry distance orders candidates; the slab
   the ray entered through names the face hit.
3. For a box with cuts, test the ray against the `Solid`'s faces instead: planar polygons directly,
   curved patches through the same chord tessellation the renderer draws with (§8.4). A hit in a
   clipped-off corner then picks what is behind it, as the plan canvas already does.
4. Nearest hit wins. Edges and vertices are picked by proximity to their projected positions, in
   screen pixels, with corners winning over edges and edges over faces — `GripAt`'s rule, one
   dimension up. (**Since #89:** only on the face the ray hit, or anywhere when the ray hits
   nothing — the silhouette case §3a.7 relies on. Another part's edge a few pixels away no longer
   takes a click on the face under the pointer, nor does an edge on the far side of a thin board.)

Nothing stored depends on any of this.

### 8.3 The gestures

- **Turn.** With 24 states, a quarter turn is a command, not a drag: three keys or toolbar buttons —
  turn about X, about Y, about Z — each `SetOrientation(box, Orientation.TurnedAbout(axis, 1))`,
  with the modifier for the other direction. A rotation gizmo with three rings was considered and
  set aside: ring-dragging chooses among a continuum and then snaps to four stops, which is more
  gesture for the same four answers and reintroduces the depth ambiguity the command avoids. This
  is §11 decision 12. **The command turns a part in place (#75, 2026-09-22):** `SetOrientation`
  keeps the anchor, so on its own it swings the part about its local south-west-bottom corner and
  three of the six face-up results land below the anchor — through the floor for a leg standing on
  it. The command therefore sends `Batch(SetOrientation, SetPosition)` so that the low corner of
  the part's extent is where it was: the part stays on what it sat on. The request itself is
  unchanged; a pinned part, which cannot move, still turns about its anchor.
- **Move.** Per-axis handles: three arrows at the selected part's anchor along world X, Y and Z
  (**from the centre of the part's extent since #83, 2026-09-23**: the anchor is the local
  south-west-bottom corner — the far end of a long apron, or wherever that corner went after a
  turn).
  Dragging one projects the pointer's screen motion onto that axis's projected direction and
  produces `Drag(id, Vector3.Along(axis, d))`, snapped to the grid step and to faces (below). A
  drag on the part's body, not a handle, moves in the plane of the face that was hit — two axes —
  which is how a board is slid along a top. Never three axes from one pointer: that is the depth
  ambiguity per-axis handles exist to remove.
- **Grabbing a part (#85, 2026-09-23).** A press on a part that is not selected, dragged past the
  click threshold, selects it and moves it in the plane of the face pressed — in the plan too;
  empty space, the middle button and Shift-drag move the view, and a click still selects. A move
  or resize that leaves the part where it was states nothing — a snap the part never reached is not
  a relationship — and says why: the updater's refusal, or that the part is pinned, with an offer
  to unpin it. A move that returns a part to where it was *and* reached the snap still states it,
  which is how two parts already touching are made to hold together.
- **Snap.** `SnapResolver` generalised to three axes: for the axis or axes being dragged, a face of
  another part perpendicular to that axis within the snap radius beats the grid when the two
  parts overlap on the other two axes; the plan says `Flush(face, face)`. Three axes caught at
  once on one part — a vertex landing on a vertex — say `Coincident(vertex, vertex)`. The indicator
  draws the target face's outline, faint, as the plan canvas draws the edge line. Relationships are
  candidates until the drop, and the editor asks the updater whether each can hold, as today.
- **Resize.** A handle at the centre of each of the six faces: `DragFace(box, face, delta)`, the
  motion projected onto the face's world normal. **Since #80** the dragged face — only that one —
  snaps to a coplanar face of another part it overlaps, and the drop states `Flush(target face,
  dragged face)`: a leg's top stretched up meets the table's underside and is held there. **Since
  #86** a label by the pointer says what a move or resize has done so far — "up 2 1/2″", "Length
  1'-4 1/4″" — and what the snap caught, "flush with Top's bottom face". (**Since #84:** each handle stands a few pixels
  out from its face's centre along the face's projected normal, on a stalk, and no two handles —
  arrows' tips included — are drawn within two grab distances of each other; at the centre itself
  a 3/4″ board's handles were a few pixels apart, on the body a drag grabs to move it.)
- **A strut** has no turn and no face handles. Each end carries the three per-axis move arrows
  (`DragStrutEnd`), the body drags the whole strut (`Drag`), and the strut tool is two clicks on
  faces or the grid (§3a.7). Snapping an end onto a face or a vertex produces the `AxisDistance`
  or `Coincident` the plan snap would.
- **Everything else** — type a size, add a relationship, delete, duplicate, undo — is the same
  request through the same `DesignEditor` as in the plan view.

### 8.4 Rendering: hand-rolled over Avalonia's 2D drawing

**Recommendation: no 3D engine.** Project the solids to the screen with `Camera.Project` and draw
them with the `DrawingContext` and `StreamGeometry` the plan canvas already uses, back to front.
The reasons are the repository's own:

- **No new dependency.** Every package still goes through the hand license check that stands in
  for #2 (PLAN.md), and a 3D engine is a large surface to vet for a need that is a few hundred
  flat-shaded quads.
- **No cross-platform regression.** CI builds and tests on `windows-latest` and `macos-latest`.
  A DirectX-backed library would not run on the Mac; an OpenGL control would add a native
  surface with its own headless-testing story. Avalonia's 2D drawing already works headlessly on
  both, which is what the GUI workflow suite depends on.
- **The projection is pure and testable.** `Camera.Project`, the painter's sort and the picking
  math are functions of exact model values and doubles, unit-testable without a window, exactly
  as `ViewTransform` is today.

The algorithm, enough for an implementer:

- Collect every `SolidFace` of every visible box; **cull faces whose world normal points away
  from the camera**; **sort the rest back to front** by their furthest point along the view
  direction — per face, not per box, so that one part standing in front of another draws over it
  correctly. Draw each as a filled polygon: three tones by the axis of its normal (the classic
  axonometric look — light for up-facing, medium and dark for the two side directions), edges as
  lines, arcs tessellated to chords in `double` from their exact endpoints and centre.
- **The painter's algorithm fails on cyclic overlap** — three faces that each occlude the next —
  and on interpenetrating solids. Non-interpenetrating assemblies of convex prisms do not produce
  cycles in practice, and interpenetration is not detected by design (§6); when it happens the
  drawing is briefly wrong in the overlap, which is honest. A depth buffer or a BSP would fix it and
  is not worth its complexity here.
- Selection, handles, the snap indicator and the virtual-feature markers draw last, on top.

Scale: a coffee table is nine boxes and 54 faces; a deck's framing is a few hundred. Sorting and
drawing that per frame is nothing.

---

## 9. Test plan

Feature-catalog ids are the next free `GEO-`, `CUT-`, `PRJ-` and `CVS-` numbers, assigned when the
catalog is edited; the GUI workflows need a new family (`ASSEM` is a reasonable name) added to
`features/README.md`'s list in the same edit. The shapes are not invented here.

### 9.1 Golden cases

Orientation and value types (`Napkin.Core.Geometry.Tests`):

1. `Orientation.Image` for each of the six `FaceUp` values equals §1.3's table, all three axes,
   both the axis and the sign; the determinant of each is +1, computed from the signed
   permutation.
2. The 24 orientations are distinct: `(FaceUp, quarter turn)` over all pairs gives 24 distinct
   `Apply` images of the three basis vectors.
3. Closure: for every one of the 24, every world axis and every quarter-turn count 1–3,
   `TurnedAbout` returns one of the 24 in its one spelling, and applying it equals rotating the
   original's images by that quarter turn. `TurnedAbout(Z, q)` leaves `FaceUp` alone and adds `q`
   to `Rotation`. Four turns about any axis is the identity.
4. `Apply` then `Unapply` is the identity on `(W, H, D)` and on `(-1, 2, -3)` in units, for all 24;
   no `Length` is constructed through `FromInches` anywhere in the exact path (assert with a
   sentinel or by inspection of the call graph — the implementer chooses).
5. `Box.Vertex` for all eight `(BoxCorner, BoxLevel)` pairs on a 48″ × 24″ × ¾″ box, under all 24
   orientations, with `Anchor = (1536, 2048, 4096)`: each equals the hand-derived
   `Anchor + Rz(Tip(local))`. Every value is a multiple of 256; nothing rounds.
6. `Footprint` for all six `FaceUp` values equals §7.1's table; for `Top` it is
   `(Anchor.XY, (0,0), Width, Height, Rotation)` field for field; `FaceAt` and `UprightAt`
   return the faces the table implies (for `East`, the plan's west side is local `Top`).
7. `BoxFeature`: `Face`, `Edge`, `Vertex` and `LocalUpright` produce faces in `BoxFace` order;
   `Edge(South, North)` and any vertex with two opposite faces throw; `LocalUpright(SouthWest)
   == Edge(South, West)`. And the two uprights are told apart: for an `East` box,
   `Footprint.UprightAt(SouthWest)` is *not* `LocalUpright(SouthWest)` but the local edge the
   §7.1 table puts at the plan's south-west corner, and it fixes world X and Y.
8. `Sketch.PlaceOf(FeatureRef)` for each of the 26 features of one box under all 24 orientations:
   the axis set has the right size (1, 2, 3) and each coordinate equals the anchor component plus
   the size the table implies, with the sign the table implies.

Relationships and the updater:

9. `AddRelationship(Flush(top.Bottom, leg.Top))` with both `Top`: `Solved`, Z pinned, X and Y
   untouched. Then set the leg's `Depth` by `SetParameter` with the leg `Anchored`: the top
   translates up by the delta (`Moved`), the leg is `Resized`, nothing else moves. Same with the
   *top* anchored: the leg's anchor moves down, its top face stays — the anchor rule along Z.
10. Four legs `EqualParam` on depth, one leg `ParamValue`, all four `Flush` under the top: set the
    one value; all four resize, the top rises, and the change set lists five entities.
11. `Flush(A.North, B.Top)` where both are `Top` boxes: `Rejected(PlacesNotComparable)` naming
    both faces and the axes each fixes. `Coincident` between a face and a vertex: the same.
    `Coincident(NodeRef, FeatureRef(LocalUpright(SouthWest)))` on a `Top` box: accepted, X and Y
    pinned; the same on an `East` box: `Rejected(PlacesNotComparable)`, because that local edge
    now fixes Y and Z. `Coincident(NodeRef, CenterRef)`: accepted on X and Y, as today.
    `Coincident(vertex, vertex)`: accepted, all three pinned; `Drag` along any axis of a
    vertex-coincident pair with one end anchored applies zero on all three.
12. `AxisDistance(from, to, Z, d)` between a face and a vertex that both fix Z: accepted;
    `SetParameter` on it moves the free box along Z only. `Centered` along Z with an odd span:
    half-unit rule, per axis.
13. `SetOrientation` on a free box: `Solved`, in `Modified`, anchor unchanged, every vertex equal to
    case 5's prediction; on a box with a `Flush`: `Rejected(OrientationWithRelationships)` naming
    it; with only `Anchored`/`ParamValue`/`EqualParam`: `Solved`. `SetOrientation` with a 45°
    rotation: `Rejected(RotationNotSupported)`.
13a. **Invariant 13 survives a turn.** A `Top` box with a driving `boxWidth` dimension:
    `SetOrientation(East, 0)` is `Rejected(OrientationWithRelationships)` naming the dimension;
    `SetOrientation(Bottom, 0)` and `SetOrientation(Top, 90°)` are `Solved`, because the width
    still lies in the plan. A box with a *reference* dimension on an `AxisMeasurand` between two
    of its own local uprights, turned `North`: refused for the same reason — there is no
    relationship to catch it by, and the test is that the dimension alone is enough.
14. `SetOrientation` on a box with cuts: the local `Outline()` is unchanged; `Solid()`'s caps
    equal that outline lifted to z = 0 and z = `Depth` and oriented, vertex for vertex.
15. `DragFace(Bottom, δ)`: anchor moves down by δ, `Depth` grows by δ; `DragFace(Top, δ)`: anchor
    stays; `DragFace` on a driven depth: `Rejected(DrivenSize)`; `DragFace(East, −δ)` past a
    corner cut's claim: clamped, as `DragEdge` is today; `DragFace(Top, −δ)` is clamped by nothing
    but `NonPositiveSize`.
16. `Drag((0, 0, δ))` on a box flush under an anchored top: applied delta `(0, 0, 0)`, `Solved`;
    on a box flush *beside* an anchored one on X: applied `(0, 0, δ)`.
17. The propagator-only 45° test from geometry-model §8 step 8 still passes, with `Z` and `Depth`
    scalars present and `FaceUp = Top`.

Solid:

18. A plain box: six faces, each `Of` the right `BoxFace`, each a quad of the right four vertices,
    winding outward. A box with `RoundedCorner` at all four corners (the `rounded-corner-table`
    sample's top): two caps of four straight and four arc segments each (one outline segment per
    corner and per edge), eight sides — four quads and four curved patches — every endpoint
    exact. **Corrected against the landed implementation** (step 6, 2026-09-23): the original text
    here said "eight straight and four arc segments, twelve sides", which over-counted — one
    outline segment per site, not two; and it said every endpoint is "a multiple of 1024", which
    holds for X and Y but not Z, whose grain is the anchor/depth grid (a multiple of 256 — e.g.
    the sample top's Z values of 16640 and 17408), not necessarily 1024. A box with a full mitre:
    the cut side face has `Of = null`.

Cut list and shopping list (`Napkin.Modules.Furniture.Tests`):

19. `Part.SizeOn` reads `Depth` for the out-of-plane name; every existing cut-list test passes
    with `OutOfPlane` replaced by `Depth` and nothing else changed.
20. **The three committed `expected.json` files are byte-identical to their pre-design content**
    — assert against a copy checked in with this design's first commit, and delete the copy once
    the format lands. `SetOrientation` and `SetPosition` on every box of every sample, in turn,
    leave `CutList.Of` and the shopping list equal by value.

Format (`Napkin.Core.Project.Tests`):

21. `Load(Save(s)) == s` for a sketch holding every `FaceUp`, a `Depth`, a Z anchor, every
    reference shape in §2.2 and a Z `AxisDistance`. Refusals, each named: a `formatVersion: 3`
    file; a box without `depth`, `faceUp` or `anchor.z`; a `part` with `outOfPlane`; a feature
    with opposite faces, out of order, or four faces; a `dimension` whose measurand leaves the
    plan — `boxDepth` on a `Top` box, `boxWidth` on an `East` box, an `axis` span along `"z"`;
    the old `corner`/`boxEdge` reference kinds.

Fixtures:

22. All three samples load, validate, check, and give their unchanged expectations. The
    `coffee-table` sample's legs stand at Z = 0 with `Depth 16640`, the aprons at Z = 13056 with
    `Depth 3584`, the top at Z = 16640 with `Depth 768`, `FaceUp = Top` throughout — values derived
    by hand in its design file (the top's underside at 16¼″ is the legs' length; the apron's top
    flush with it; every number a multiple of 256). No Z relationship is added to it (§11
    decision 9). The `wall-with-window` sample's wall and opening carry the depth and sill its
    design file records.

Struts (`Napkin.Core.Geometry.Tests` unless said otherwise; §3a):

23. **The frame.** For `d = (6144, 9216, 27648)` — 6″, 9″, 27″ — with `Z` cuts at both ends,
    `n_ref = Z`, local Z is `(9216, −6144, 0)`, local Y is `Z·|d|² − d·d_z` computed in `Int128`
    and has a positive Z component, `(d, y, z)` is right-handed (the triple product is positive),
    and every component is an integer. Swapping `From` and `To` gives the same frame (step 2's
    reorientation). Mirroring `d_y` gives a mirrored frame and, in case 25, the same blank.
24. **Coplanarity.** `(Z, Y)` cuts with `d = (0, 9216, 27648)`: accepted, local Z is `X`. The same
    cuts with `d = (1024, 9216, 27648)`: `Rejected(StrutNeedsBevel)`, and the message says the
    strut runs 1″ east and would need to lie in a north–south plane. `(Z, X)` with `d ⊥ Y`:
    accepted. `(Square, Square)` with any non-axis-aligned `d`: accepted. `d = (0, 0, 27648)` and
    `d = (6144, 0, 0)`: `Rejected(StrutIsAxisAligned)`.
25. **The sawhorse leg's blank.** `d` as in case 23, `Height 3584`, `Depth 1536`, `Z` cuts. `|d|²
    = 887 095 296`, not a perfect square, so `c ≈ 29 784.145` units and `Length.Exact` is false;
    the setback `s = 3584·√122 683 392 / 27 648 ≈ 1435.81` rounds to **1436** and is not exact;
    `L = c + s ≈ 31 219.956` rounds to **31 220** (30.488″). The cuts are `CornerCut(SouthWest,
    1436, 3584)` and `CornerCut(NorthEast, 1436, 3584)` — a parallelogram. The same strut with
    `d_y` negated, and again with `From` and `To` swapped, gives a blank equal by value.
26. **One rounding, not two.** `d = (1024, 0, 12288)`, `Height 3584`, `Z` cuts: `L` is **12 629**;
    `round(c) + round(s)` would be 12 630. The test asserts 12 629.
27. **Proven exact.** `d = (3072, 0, 4096)` — a 3-4-5 leg — `Height 3584`, `Z` cuts: `c = 5120`
    exact, `s = 2688` exact, `L = 7808` exact, every `Exact` flag true, and the row prints no
    `≈`. `d = (4096, 6144, 12288)`, the same otherwise: `c = 14 336` exact by the perfect-square
    test, `s ≈ 2153.72` not exact, so `L` is 16 490 and marked — exactness of a part does not
    leak to the whole.
28. **Corner sites.** `(Z, Y)` with `d = (0, 9216, 27648)`: `SouthWest` and `SouthEast`, a
    trapezoid, and the same with `d_y` negated. `(Z, Square)`: one `CornerCut` at the first end,
    `null` at the second, `L = c + s/2`. Invariant 17: a parallelogram can never fail it, because
    `L = c + s` exceeds `s`; a trapezoid can. `(Z, Y)` with `d = (0, 2048, 2048)` and `Height
    3584` — a 2x4 at 45° spanning 2″ each way between a floor and a wall — has `s₁ = s₂ = 3584`
    exactly, `L = 6480`, and the two mitres claim 7168 from a 6480 edge: shaped-parts invariant 8
    fails on the derived blank and the request is `Rejected(StrutTooShortForItsCuts)`.
29. **Propagation, per end.** The knee brace: a post (anchored) and a beam `Flush` on its top;
    a strut with `From` `Coincident` to the post's top-outer vertex and `To` `AxisDistance` 0
    along Z from the beam's bottom face. Set the post's `Depth` larger: the beam rises, the
    strut's `To.Z` rises by the delta, `From` is unchanged, the change set lists the strut as
    `Moved`, and `Blank().Length` grew. The same sketch with a whole-entity rule would be a
    contradiction; the test asserts `Solved`. `Drag(strut, (0, 0, δ))` on the same sketch:
    applied delta `(0, 0, 0)`, because the group reaches the anchored post through the
    `Coincident`. `Drag` on a strut whose ends are related to nothing: both ends move by `δ`.
    `SetStrutEnd` on an end held by an `AxisDistance` to an anchored box: `OverConstrained`,
    naming it. `Anchored(strut)` then `SetStrutEnd` on it: `OverConstrained`, naming the anchor.
    `SetParameter` on a `ParamValue(StrutHeightRef)`: `Resized`, neither end moves.
30. **The sawhorse sample** (`Napkin.Core.Project.Tests`, `Napkin.Modules.Furniture.Tests`). A
    fourth sample, `sawhorse`, per §11 decision 9 as revised: a 2x6 rail on edge, 36″ long,
    `Anchored`, underside at 27″ (`Anchor (0, −768, 27648)`, `Width 36864`, `Height 1536`,
    `Depth 5632`); four 2x4 legs, `Height 3584`, `Depth 1536`, `{ x: length, y: width }`, `Z`
    cuts, tops at `(3072, 0, 27648)` and `(33792, 0, 27648)`, feet at `(−3072, ±9216, 0)` and
    `(39936, ±9216, 0)`; the relationships of §3a.5. Its `expected.json` carries one row of four
    legs with `Length 31220`, `LengthExact false`, the two cuts of case 25, and the rail's row,
    every number with a `derivation` string per `samples/README.md` — the leg's showing `|d|²`,
    the square root to enough digits to round, the setback and the sum. The file loads,
    validates, checks with zero violations, and `Load(Save(s)) == s`.
31. **Format** (`Napkin.Core.Project.Tests`). Round-trip of a strut with every `EndCut` value and
    a `strutEnd` reference in a `Coincident`, an `AxisDistance` and a `Centered`. Refusals: a
    strut missing `from`, `to`, `fromCut`, `toCut`, `height` or `depth`; an unknown cut name; a
    `part` with `planAxes.x` other than `length`; a strut failing invariants 14, 15 or 17; a
    `param` of kind `strutLength`; a `flush` naming a `strutEnd`.
32. **The cut list is blind to a strut's placement, in the only way it can be.** `Drag` the
    sawhorse (anchor removed) by `(1024, 2048, 0)`: every row is equal by value to before,
    including the `≈` length — a translation changes no `d`. Move one foot by one unit: that leg
    leaves the row of four and lists on its own, with its own derived length.
33. **The dimension rule.** An `AxisMeasurand` along X between two feet: accepted and drawn; along
    Z between a foot and a top: `Rejected(UnsupportedRequest)`, invariant 13.

### 9.2 Properties

Extending geometry-model §7.2's generator to three axes, six `FaceUp` values, depths, and the
reference shapes of §2.2 (derive relationships from the geometry, never sample and hope):

- **P1–P7 as stated**, over three axes: never inconsistent; exact requests are exact; pure and
  deterministic; local; idempotent; drag is best-effort per axis and never worse; conflict reports
  are actionable.
- **P14 — orientation is a group action.** For every orientation `o`, axis `a`, count `q`, and
  vector `v`: `o.TurnedAbout(a, q).Apply(v) == Rot(a, q)(o.Apply(v))`, and `TurnedAbout` is
  invertible.
- **P15 — the footprint is the projection.** For every box, the axis-aligned bounding rectangle of
  the XY projections of its eight vertices, un-spun by `−Rotation` about the anchor, equals its
  `Footprint`'s rectangle.
- **P16 — the cut list is blind to placement.** For every sketch S and every box b in it,
  `CutList.Of(S)` equals `CutList.Of` of S after any `SetOrientation` or `SetPosition` on b that
  succeeds, and so does the shopping list.
- **P17 — the solid is closed and exact.** For every box, every `SolidFace` boundary closes; every
  side's four corner points are on the two caps; every vertex equals `Anchor +
  Orientation.Apply(local)` for a local point of the outline lifted to 0 or `Depth`.
- **P12 (cuts are invisible to propagation)** and **P13 (a resize leaves every cut fitting)** hold
  with `DragFace` in place of `DragEdge`, with the same `DragFace` carve-out P12 already has.
- **P10 (undo)** holds for `SetOrientation` without change.
- **P18 — a strut's blank is canonical.** For every strut `s` and every combination of swapping
  its ends, negating any one or two of `d`'s components, and translating both ends by one
  vector: `Blank()` is equal by value, and `Frame()` is right-handed with `y · n_ref > 0`.
- **P19 — the exact scalars are the only ones the propagator touches.** For every generated
  sketch containing struts and every exact request: every changed strut scalar is an end
  coordinate, a `Height` or a `Depth`; no `Blank()` value is read by the propagator (assert by
  instrumenting `StrutBlank.Derive` with a counter that must stay zero across
  `DirectUpdater.Apply`, excluding the post-write invariant-17 check inside `Sketch.Validate`,
  which is the one sanctioned reader — §3a.4).
- **P20 — the marker is right.** For every generated strut, `Blank().Length.Exact` is true iff
  `|d|²` and the setback tests pass in `Int128`, and when it is true the double-derived value
  equals the integer one exactly.
- **P1–P7 over sketches that contain struts**, with `SetStrutEnd` and `DragStrutEnd` among the
  generated requests and `StrutEndRef` among the generated places.

### 9.3 GUI workflows

Three `[GuiWorkflow]` scenarios, keyboard and pointer both, ≥5 actions, an assertion after a state
change (`docs/testing/gui-automation.md`):

- **Turn a part.** Open the coffee table, select a leg in the plan, switch to the 3D view by
  keyboard, press the turn-about-X key, assert the leg's `FaceUp` changed and its footprint in
  the plan view (switch back) is now the leg's side; undo; assert it is a square again.
- **Set a part under the top.** In the 3D view, select an apron, drag its Z handle up with the
  pointer until it snaps flush under the top, release, assert the relationship list names a
  `Flush` on the top's bottom face and the apron's top face; then type the apron's depth in the
  properties panel and assert the apron grew downward and the top did not move.
- **The plan is untouched.** Not a new scenario: the whole existing `GUI-DRAW`, `GUI-CUT` and
  `GUI-VIEW` suite, passing unchanged, is the third workflow.
- **Build a sawhorse** (§3a). New document; draw the rail with the stock tool as a 2x6 on edge in
  the plan; switch to the 3D view by keyboard; pick the strut tool; click the floor grid then the
  rail's visible lower edge with the pointer, choose 2x4, assert a strut exists with `FromCut =
  ToCut = Z` (both defaulted, §3a.7) and an `AxisDistance` along Z on its top to the rail's
  bottom face; repeat for the other three legs
  (duplicate and move feet by keyboard is acceptable); open the cut list and assert one row of
  four with a `≈`-marked length; raise the rail by typing a Z position in the properties panel;
  assert the row's length changed and the feet did not move; undo; assert the original length.

---

## 10. Implementation plan

For an Opus-tier implementer, once Marc signs off. Ordered so that each step leaves the build
green, and so that steps 1–6 are a kernel-and-format slice with no new GUI in it — a file whose
parts are placed in space, that opens, lists and exports exactly as before — which is what
CLAUDE.md's "core functionality first" asks for. The plan canvas keeps working from step 2 on and
its GUI suite is the gate for every later step.

1. **Value types and orientation** (`Napkin.Core.Geometry`). `Axis.Z`; `Point3.cs`, `Vector3.cs`;
   `BoxFace`, `BoxLevel`, `BoxFeature`; `Orientation` with the tip table, `Image`, `Normal`,
   `Apply`, `Unapply`, `TurnedAbout`. Tests 1–4, 7, P14. Nothing else references these yet, so the
   step is pure addition.
2. **The box in space.** `Box` gains `Point3 Anchor`, `Depth`, `FaceUp`; `Part` loses
   `OutOfPlane`; `Box.Vertex`, `Center`, `Footprint`; `BoxDepthRef`; `Part.SizeOn` reads `Depth`;
   `StockAssignment.RequestsFor` sets the third dimension through the updater; `BoxGeometry`,
   `SnapResolver`, `RectangleTool`, `StockTool`, `DesignEditor`, the properties panel and the
   viewer read the footprint. Tests 5, 6, 19; the plan GUI suite passes unchanged. This step is
   the biggest mechanical change and is worth landing on its own, because it proves §7 before any
   3D relationship exists.
3. **References and relationships.** `PlaceRef`, `FeatureRef` replacing `CornerRef` and
   `BoxEdgeRef`, `Sketch.PlaceOf`, the legality rules of §2.3, `PlacesNotComparable`, the checker
   over three axes and `Depth`, invariants 12 and 13. Tests 8, 11 (the rejections), the checker's
   own goldens.
4. **The propagator and the updater.** `ScalarKind.Z` and `.Depth`; coordinates through
   `BoxFeature`; `SetPosition(Point3)`, `SetOrientation`, `Drag(Vector3)`, `DragFace`;
   `OrientationWithRelationships`; `RigidGroup` per three axes; `ChangeSet.AppliedDelta` as
   `Vector3?`. Tests 9–13, 15–17; P1–P7 and P10–P13 over three axes.
5. **Format** (`Napkin.Core.Project`, `docs/file-format.md`). `FormatStamp.CurrentVersion` 4; the
   field changes in the table below in `SceneBinder`, `SceneWriter`, `SceneNames`; all three samples gain
   `anchor.z`, `depth`, `faceUp`, lose `part.outOfPlane`, and rewrite their references in the
   `feature` shape, with derivations in their design files; the format document updated in the
   same commit. Tests 20–22, P16.
6. **The solid.** `Solid.cs` and `Box.Solid()`. Tests 14, 18, P17. Still no GUI.
7. **The 3D view** (`Napkin.App`). `Camera` (pure, tested without a window: projection, ray,
   isometric default, fit); the painter's renderer over `DrawingContext`; picking; the per-axis
   move handles and face handles; the turn commands; `SnapResolver` over three axes; the mode
   toggle sharing selection and `DesignEditor`; the virtual-feature markers. The three GUI
   workflows of §9.3, and the existing suite still green.

The strut (§3a) comes **after** the axis-aligned slice, because it uses `Point3`, `PlaceRef`, the
propagator's Z scalars and the 3D view, and because the sawhorse is worth nothing until a rail
can be placed in space for its legs to meet. Three further steps, each leaving the build green:

8. **The strut in the kernel** (`Napkin.Core.Geometry`). `Strut`, `EndCut`, `StrutEnd`,
   `StrutFrame` (integer), `StrutBlank.Derive` with `DerivedLength` and the `Int128` exactness
   proofs; invariants 14–17 in `Sketch.Validate`; `StrutEndRef`, `StrutHeightRef`,
   `StrutDepthRef`; `Sketch.PlaceOf` for an end; the per-end propagation rule; `SetStrutEnd`,
   `DragStrutEnd`, `Drag` and `Anchored` over struts; the three rejection reasons; `Strut.Solid()`.
   Tests 23–29, 32, P18–P20, P1–P7 over struts. No GUI, no format: a strut can exist only in a
   test until step 9.
9. **Format and the sawhorse sample** (`Napkin.Core.Project`, `Napkin.Modules.Furniture`,
   `samples/`). The `strut` record and `strutEnd` reference below; `Part.SizeOn(strut)`;
   `CutListRow.LengthExact` and the marker in `CutListCsv.Text` and the on-screen list; the
   `sawhorse` sample with its design file and hand-derived expectations; `docs/file-format.md`
   updated in the same commit. Tests 30, 31, 33. **This is its own format bump**, 4 → 5, unless
   it lands in the same commit as step 5, in which case there is one bump; per beta policy
   either is fine and neither converts anything.
10. **The strut in both views** (`Napkin.App`). The strut tool (two clicks, both views), the
    silhouette and grips in the plan, the solid in the 3D view, the end handles, the properties
    panel's ends, cuts and marked derived readouts, and the end snap. The fourth GUI workflow of
    §9.3, and the existing suite still green.

Whoever lands each step bumps the minor version (CLAUDE.md); nothing is tagged.

**The file format, one bump: `formatVersion` 3 → 4.** Following shaped-parts §5's level of detail:

```json
{ "id": "…", "type": "box", "layer": "…", "name": "Leg, south-west",
  "anchor": { "x": 1536, "y": 1536, "z": 0 },
  "width": 2560, "height": 2560, "depth": 16640,
  "faceUp": "top", "rotation": 0,
  "part": { "stock": null, "species": null, "quantity": 1,
            "planAxes": { "x": "width", "y": "thickness" } },
  "cuts": [] }
```

| Change | Shape | Refused when |
|---|---|---|
| `anchor` | gains required `z`, integer units | missing, or not an integer |
| `depth` | required, integer units > 0 | missing, not an integer, or ≤ 0 |
| `faceUp` | required, one of `top`, `bottom`, `north`, `south`, `east`, `west` | missing or not one of the six |
| `rotation` | unchanged | as today |
| `part.outOfPlane` | **removed** | present |
| references `corner`, `boxEdge` | **removed**, replaced by `{ "kind": "feature", "box": "…", "faces": ["south", "west"] }` — one, two or three faces in `BoxFace` order | old kinds present; a face repeated; opposite faces together; out of order; fewer than one or more than three |
| references `node`, `center`, `segment` | unchanged | — |
| `param` kinds | gain `boxDepth` | — |
| `axis` | gains `"z"` on `axisDistance` and `centered` | a `dimension` whose measurand leaves the plan after the owning box's orientation is applied — `"axis": "z"`, or a size kind whose local axis the box's `faceUp` stands vertical, such as `boxDepth` on `top` or `boxWidth` on `east` (invariant 13) |
| `flush`, `coincident` | `a` and `b` are any reference; legality per §2.3 checked at load | the pair is not comparable |

**The strut's format (step 9; §3a).** A fourth entity type beside `box`, `node` and `segment`:

```json
{ "id": "…", "type": "strut", "layer": "…", "name": "Leg, south-west",
  "from": { "x": -3072, "y": -9216, "z": 0 },     "fromCut": "z",
  "to":   { "x": 3072,  "y": 0,     "z": 27648 }, "toCut": "z",
  "height": 3584, "depth": 1536,
  "part": { "stock": "2x4", "species": null, "quantity": 1,
            "planAxes": { "x": "length", "y": "width" } } }
```

| Change | Shape | Refused when |
|---|---|---|
| `strut` entity | `from`, `to`: three integer units each; `fromCut`, `toCut`: one of `square`, `x`, `y`, `z`; `height`, `depth`: integer units > 0; `part` as for a box; no `cuts`, no `rotation`, no `faceUp` | a field missing or of the wrong shape; invariants 14–17; a `cuts` field present |
| reference `strutEnd` | `{ "kind": "strutEnd", "strut": "…", "end": "from" \| "to" }` — a place fixing X, Y, Z | the strut id is unknown; used in a `flush` (a point fixes three axes, never one) |
| `param` kinds | gain `strutHeight`, `strutDepth`; there is no `strutLength` | `strutLength` present |
| `anchored` | may name a strut | — |
| `part.planAxes` on a strut | `x` must be `length` | otherwise |

No derived value — length, setback, angle, frame — is ever written. `Load(Save(s)) == s` holds
because the file stores only the strut's exact fields, and the loader re-derives the blank to
check invariant 17 exactly as it re-derives a box's outline to check a cut's fit.

**On compatibility, both halves honestly.** The *model* is a strict superset: a `Top` box at Z = 0
is today's box in every respect. The *files* are not untouched: every field is required
(`file-format.md`), so the version is bumped, a version-3 file is refused with the message the
reader already gives, and all three committed samples change in the same commit, as they did for
`"cuts": []`. Per beta policy there is no converter.

---

## 11. Decisions for Marc

Only what is genuinely a judgment call. Each has a working recommendation that stands until he
says otherwise, and beta policy makes changing any of them cheap.

1. **A box's rotation in 3D is restricted to the 24 axis-aligned orientations (recommended) — or
   arbitrary 3D rotation of a box is supported.** §1.3, §3. Axis-aligned keeps every coordinate
   exact, keeps every box on the direct updater with no solver and no `double` in the kernel, and
   matches the stated need — assembly, not sculpture. Arbitrary rotation of a box puts every 3D
   edit on the solver's tolerance-class path before the solver exists. **Revised after Marc's
   correction:** the sawhorse and the splayed stool are no longer the case this excludes; they
   are struts (§3a, decision 17), which keep this decision intact by not being boxes. What this
   decision still excludes is a shaped, related rectangle turned to an angle, which stays #28's.
2. **Orientation stored as `FaceUp` plus the existing `Rotation` (recommended) — or as a single
   24-valued field replacing `Rotation`.** §1.3. The factoring keeps every existing box the `Top`
   case with identical meaning, keeps one spelling per orientation, and keeps the solver's rotation
   scalar alive; the single field is one field fewer and deletes the non-right-angle path #28
   relies on.
3. **The third size is `Box.Depth` along local Z (recommended); `Height` keeps its name.** §1.2.
   `Depth` is the 3D-graphics word and `Height` is baked into `BoxHeightRef`, `boxHeight`, the
   cut-list readers and every test; renaming `Height` to something plan-neutral (`Extent`,
   `SizeY`) would be honest about the trap and cost a rename across the solution. The alternative
   name `Thickness` for the third size is wrong for a leg.
4. **Extend `Box` in place (recommended) — or a separate 3D entity.** §1.1.
5. **One `FeatureRef` over a `BoxFeature` replaces `CornerRef` and `BoxEdgeRef` (recommended) —
   or three new reference kinds, one per feature dimension.** §2.2. One vocabulary, one rule in
   the propagator, runtime rather than compile-time refusal of a nonsensical pairing.
6. **Reorienting a box that has positional relationships is refused, and the canvas offers to
   remove them (recommended) — or the relationships are removed automatically — or re-mapped
   through the turn.** §2.4. Refusing is today's `SetRotation` rule and the physical workflow;
   auto-removal is a silent loss of what the person stated; re-mapping is guessing which face they
   meant.
7. **A rectangle drawn with the rectangle tool gets a default depth of ¾″, visible and editable
   in the properties panel (recommended) — or the tool prompts — or a rectangle must be placed
   from stock.** §7.2. The stock tool already knows its depth; the bare rectangle tool needs one
   because every box now has one, and a visible default is the least ceremony. Requiring stock
   would refuse the coffee-table fixture as drawn (shaped-parts §11.10).
8. **Dimensions along Z are refused in this slice (recommended) — or accepted as data and left
   undrawn.** §7.3. Refusing keeps invariant 13 simple and the plan canvas honest; accepting
   would store an annotation nothing can show.
9. **`formatVersion` 3 → 4, samples gain the required fields with hand-derived Z values, and no
   Z relationships are added to the coffee-table fixture (recommended) — or the fixture also
   gains `Flush` relationships holding the legs under the top.** §10, §9 case 22. The
   shaped-parts §11.7 precedent is that Marc's fixture is not redesigned by a feature landing; the
   required fields are forced by the format, the relationships are not. A second sample that
   *is* assembled in Z, the way `rounded-corner-table` carries the cuts, is the natural home for
   a worked 3D assembly. **Revised:** that sample is the **sawhorse** of §9 case 30, authored
   when step 9 lands — it is the one fixture that exercises struts, the per-end propagation rule
   and the `≈` machinery at once, and a sawhorse is the design Marc named.
10. **Orthographic camera only (recommended) — or perspective too.** §8.1. Parallel projection is
    the drawing convention, makes picking and axis-drag one projection each, and keeps lengths
    readable; perspective is a later toggle if wanted.
11. **A hand-rolled renderer over Avalonia's 2D drawing (recommended) — or a third-party 3D
    engine.** §8.4. No dependency to vet, no macOS regression, testable projection math.
12. **Turning is three quarter-turn commands (recommended) — or a rotation gizmo.** §8.3.
13. **The 3D view is a mode of the main window sharing selection and the editor (recommended) —
    or a second pane beside the plan.** §8.1. A mode is the workshop's precedent and keeps one
    selection, one undo stack and one `DesignEditor`; a split view is nicer to look at and doubles
    the surface the GUI suite drives.
14. **DESIGN.md §2 ("Not 3D in the first betas") and §9 ("Beta scope is 2D only"), geometry-model
    §2.1 ("Two dimensions"), and parts-and-cut-list §1.3 ("Not a solid") are updated on sign-off
    to point here.** They record a decision Marc has now changed; leaving them contradicts this
    document. Geometry-model §10.5's `ScalarKind` note stays true and gains the two new kinds.
15. **Overlapping solids are allowed and not reported (recommended) — or a later "parts overlap"
    report lists pairs of boxes that share volume.** §6. Not detecting is the no-physics rule;
    a report is a cheap, exact interval test on axis-aligned boxes and would be honest about a
    design error, but it is a separate feature and not in this slice.
16. **Nodes and segments stay plan-plane construction geometry at Z = 0 (recommended).** §1.4. No
    listed use needs a free point in space.

Added after Marc's correction of 2026-09-22 (§3a):

17. **An angled member is a `Strut` stored by its two exact ends (recommended) — or a `Box` given
    a tilt through the solver (#28) — or not designable.** §3a.1, §3a.2. Storing the ends keeps
    every relationship a strut takes part in exact and keeps the whole feature off the solver;
    the price is that the strut's length and end setbacks are derived, rounded once and marked,
    and that `CUT-002`'s letter does not hold for it. A tilted box would make the assembly
    inexact instead and wait on #28 for a sawhorse. Not designable is what Marc rejected.
18. **A strut's ends are the points where its centreline meets its cut planes (recommended) — or
    a named corner of each end face.** §3a.2. Whichever is chosen the others are irrational; the
    centreline keeps sizes and positions independent (a stock change does not move the leg) and
    is what a person sets out from.
19. **A strut's ends take part in propagation, each end as its own set of scalars (recommended)
    — or a strut is placed once and re-typed by hand when something moves.** §3a.5. Participation
    is what makes a taller sawhorse have longer legs, it uses only the existing relationship
    kinds, and it costs one stated rule (an implication on an end moves that end, not the strut)
    whose precedent is the node. Placing once would make the strut the only entity relationships
    cannot reach.
20. **A strut's derived numbers are marked `≈` unless proven exact in integer arithmetic
    (recommended) — or always marked `≈`.** §3a.4. A 3-4-5 leg is exact end to end and should
    say so; the proof is a perfect-square test in `Int128`, cheap and deterministic. Always
    marking is simpler and wrong for that leg.
21. **An end cut is one of the three axis-aligned planes through the end, or square (recommended)
    — or any exact-vector normal.** §3a.2. Everything a strut can meet is axis-aligned; the only
    extra plane an exact vector adds is a 45° one with no present use.
22. **The strut tool lives in both views — two clicks on faces in the 3D view, two clicks at Z = 0
    plus typed heights in the plan (recommended) — or the 3D view only.** §3a.7. The plan-only
    route keeps a sawhorse designable by someone who never opens the 3D view, at the cost of two
    typed numbers.
23. **A typed tilt or azimuth is a later entry mode that computes the far end and rounds it once
    (recommended as later, not this slice) — or part of step 10.** §3a.7. It is the analogue of
    shaped-parts' angle entry for a mitre and adds nothing to the model; it can wait until
    someone wants it.
24. **A strut carries no shaped-parts cuts beyond its two end cuts (recommended) — or its derived
    blank accepts a `Cuts` list.** §3a.8. Shaped-parts stores cuts against a stored blank and
    checks their fit exactly; a strut's blank is derived and rounded, and cuts on it would be
    validated against a value that moves by a unit when a foot moves. A tapered splayed leg is
    named as out of scope rather than half-supported.
