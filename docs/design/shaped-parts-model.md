# Shaped parts: cuts on a rectangular blank

Status: **Signed off by Marc, 2026-09-22.** §11.1 (setbacks, not a stored angle) and §11.3
(`CurvedEdge` in the first slice) are accepted as recommended. §11.7 is decided the third way —
neither option offered: the coffee-table fixture is **left as drawn** (square corners), and a
**new, separate sample** demonstrates `RoundedCorner` instead (§8 updated). §11.11 (a relationship
that keeps a cut equal to another cut or to a size — what a full mitre needs under resize, and
what template propagation needs) is confirmed deferred, and is [issue #67](https://github.com/marctjones/napkin/issues/67),
to be designed and built in its own session, not this slice. Every other decision in §11 stands as
recommended. Authorized for implementation. No issue filed yet for §1–§10 themselves; this is
M2/M3-adjacent scope — it extends the parts model (#8) and the editor (#10) and changes the file
format (#6) — and it belongs to no milestone until Marc places it.

Design document written by Fable per [`PLAN.md`](../../PLAN.md): a foundation that every later
phase builds on, where a wrong choice is a silent, expensive one. It decides how a part that is
not a plain rectangle is stored, drawn, related to other parts, listed in the cut list, and saved.

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024-inch grid and
angles in integer arcseconds ([`geometry-model.md`](./geometry-model.md) §1); a box is
parametric — it stores the width and height that were typed and derives its corners (§2.3);
relationships are stored as data behind one update interface with explicit result types (§3, §4);
the solver is a separate workstream that slots in behind the same interface (§5); a part is
fields on a box, with two in-plan dimensions on the box and one out of plane on the part
([`parts-and-cut-list.md`](./parts-and-cut-list.md) §1.1); the cut list reads stored parameters,
never derived corners (`CUT-002`); strict reading with no migration and no shims
([`../file-format.md`](../file-format.md), DESIGN.md §12).

**The product decision, made by Marc and not re-litigated here:** a part may be a shape cut from
a rectangular board with the tools a home woodworker has — a circular saw, a table saw, a jigsaw.
That means straight cuts at any angle (a corner cut off, a mitred end, a taper, a triangle) and
simple rounding (rounded corners, a simple curved edge) done freehand with a jigsaw. The
vocabulary is "the kind of parts you would find in a DIY or home woodwork magazine or
instructions". It is *not* a freeform polygon or spline editor, and arcs and curves are not
drawing primitives. §6 states the non-goals so that scope does not creep past that. Marc added
four requirements in the same conversation, and each has its own section: a shaped part starts
from **real stock** a yard sells, not an arbitrary rectangle (§1.2); a shape designed once must be
**duplicable** as independent parts (§2.6); snapping must work **between shaped parts** and
between a shaped part and a plain board (§2.5); and making a shape "probably needs its own
subworkflow" — a two-level editing model (§7.1).

---

## 1. What a shaped part is

### 1.1 The blank stays a `Box`; cuts are operations on it

A shaped part is **a rectangular blank with a list of cuts**. The blank is the `Box` that exists
today — its `Width` and `Height` are the stock dimensions, and nothing about them changes. Each
cut is a small record naming a corner or an edge of the blank, in the box's own local frame, and
saying what was taken off there. The part's outline is *derived* from the blank and its cuts, the
same way a box's corners are derived from its anchor and sizes.

```csharp
public sealed record Box(
    EntityId Id, LayerId Layer,
    Point2 Anchor, Length Width, Length Height, Angle Rotation) : Entity(Id, Layer)
{
    public Part? Part { get; init; }

    /// What has been cut off the blank, in site order (§1.6). Empty for a plain rectangle.
    public ImmutableList<Cut> Cuts { get; init; } = [];

    /// The shape that is left: derived, never stored (§1.5).
    public Outline Outline();
}
```

This is the same "parametric, not corner-based" choice geometry-model §2.3 made for the
rectangle, applied one level up: the model stores what the person *decided* — this blank, these
cuts — and derives what that produces. It is what gives the cut list its traceability for free:
"start from a 24″ × 48″ blank, round the four corners to a 1″ radius" is the data, not something
reconstructed from a polygon.

Three alternatives were considered and rejected:

- **A `Shape` entity storing an ordered boundary** (straight segments, optional per-vertex
  rounding). It is the general answer and it is the wrong one here. The cut list would have to
  *infer* the blank (a bounding box) and *infer* the cuts from the boundary — which is exactly the
  silent failure this design must prevent: a part drawn as a clipped rectangle would be listed by
  its bounding box, which may or may not be the board it was cut from, with no way to say how.
  Every `CornerRef`, `BoxEdgeRef`, `BoxWidthRef`, every request and the whole `Propagator` assume
  a rectangle; a boundary entity would need a second reference vocabulary and a second updater
  path. And the solver would have to own vertices, which puts every shaped part on the solver's
  critical path (§3).
- **A `ShapedPart` wrapper entity referencing a box by id.** Two ids for one piece: selection,
  undo, layers, names and the cut list would all have to know that these two entities are one
  thing. The geometry design already rejected side tables for `Part` for this reason
  (parts-and-cut-list §7 step 2).
- **Cuts on `Part` rather than on `Box`.** Cuts are geometry — the canvas draws them, the updater
  validates them, a rotation rotates them — and `Part` is cut-list metadata that is `null` on a
  wall or an opening. A wall with an angled end is a plausible future use of the same data, and
  the kernel should not have to reach into a nullable module record to draw a box. Cuts live on
  the box; the *description* of a cut in bench vocabulary lives in the furniture module (§4.4).

`Box` is not renamed to `Board`. A wall is a box too, and the word costs a rename across the
solution for no change in meaning. In prose, a box seen as stock is called "the blank".

### 1.2 The blank starts from stock

A shaped part is cut from something a person walks into a yard and buys, and the blank should
say which. **Nothing new is needed to say it**: `Part.Stock` already names a materials-library
item (parts-and-cut-list §2.2), the library already resolves "1x6" to its dressed ¾″ × 5½″ with a
citation (`LumberStock.Thickness`/`.Width`, PS 20-20 Table 3), and parts-and-cut-list §1.2 already
states the rule that makes the blank *be* that stock: a stock item fixes some of the part's three
named dimensions, `planAxes` says which of those land in the plan, and on assignment the box's
fixed parameters are **set through the updater** so relationships propagate and a conflict is
reported like any other. What that rule gives a blank, per stock kind:

| Stock | The blank's plan dimensions | Cuts are bounded by |
|---|---|---|
| `LumberStock` (1x6, 2x4) lying flat — `{ x: length, y: width }` | width fixed at the dressed width; length free | the fixed width on one axis; the typed length on the other |
| `LumberStock` as a footprint — `{ x: width, y: thickness }` | **both fixed**: the blank is the cross-section | a chamfer or roundover the full length (§1.4) |
| `PanelStock` (¾ plywood) lying flat | thickness fixed and out of plane; both plan dimensions free | whatever was typed |
| `HardwoodStock` (4/4) | thickness fixed; both plan dimensions free (random widths) | whatever was typed |
| no stock | nothing fixed | whatever was typed |

So "start from a 1x6" is `SetPart` with `Stock = "1x6"` followed by the updater setting the
fixed dimension — and from then on that dimension is not draggable, the way a driven size is not
(`Rejected(DrivenSize)`), because the yard drives it. Changing the stock (1x6 → 1x4) is a resize
through the updater, and §2.3's check refuses it if a cut no longer fits the narrower blank —
"the 1″ corner radius does not fit a 3½″ board" is the message, not a silently clipped part.

Two things this section deliberately does **not** do:

- **It does not make stock mandatory.** A blank with `Stock = null` stays legal, as it is today:
  the coffee-table fixture states finished dimensions only, a hardwood glue-up has no stock name,
  and the cut list already reports "no stock chosen" honestly. The shape workshop (§7.1) leads
  with the stock picker and shows the readout the properties panel already has, so the *normal*
  path starts from stock; it does not refuse the other. Open decision §11.10.
- **It does not turn stock lengths or sheet sizes into geometry constraints.** A 24″ piece is cut
  from an 8′ board; that a 14′ piece fits no stocked length, or a 5′ × 9′ panel exceeds a sheet,
  is what the shopping list already reports (parts-and-cut-list §4), and it stays there.

**What this makes real that is not built yet.** `SetPart` today stores the stock name and nothing
else (`DirectUpdater.ApplySetPart`); the properties panel resolves the name for a readout but sets
no dimension. Parts-and-cut-list §1.2's "set through the updater" is designed and unimplemented.
This design depends on it — a blank that claims to be a 1x6 but is 4″ wide is exactly the silent
error the whole cut list exists to prevent — so it is step 2 of §10, ahead of the cuts themselves.

### 1.3 The three cut kinds

Every cut is named in the blank's **local frame, before rotation**, by the corner or edge it is
made at — exactly as `BoxCorner` and `BoxEdge` already name things for references. Every stored
value is an exact `Length`.

```csharp
public abstract record Cut
{
    private protected Cut() { }
    public abstract CutSite Site { get; }        // the corner or edge it is made at
}

/// A straight cut across a corner. From the point AlongX from the corner on the edge that runs
/// along local X, to the point AlongY from the corner on the edge that runs along local Y. The
/// triangle between them is removed.
public sealed record CornerCut(BoxCorner Corner, Length AlongX, Length AlongY) : Cut;

/// A quarter-circle of the given radius, tangent to both edges Radius from the corner.
public sealed record RoundedCorner(BoxCorner Corner, Length Radius) : Cut;

/// One whole edge replaced by a circular arc through three points on the grid.
public sealed record CurvedEdge(BoxEdge Edge, Bow Bow, Length Depth) : Cut;

public enum Bow
{
    /// The middle of the edge stays; the two corners come in. The arc starts Depth from each
    /// corner along the two adjacent edges and passes through the middle of this edge. A bowed
    /// table-top end, a curved shelf front.
    Outward,

    /// The two corners stay; the middle goes in by Depth. A scalloped apron, a cut-out for a
    /// hand hold on a plain edge.
    Inward,
}

/// Where a cut is made: one of the four corners or one of the four edges.
public readonly record struct CutSite
{
    public static CutSite Corner(BoxCorner corner);
    public static CutSite Edge(BoxEdge edge);
    // fixed ordering: SouthWest, SouthEast, NorthEast, NorthWest, South, East, North, West
}
```

**What each kind covers, in magazine terms.** One `CornerCut` is a clipped corner. A `CornerCut`
whose setback along one edge is the *whole* of that edge is a mitred end (the cut runs from a
point on one long edge to the far corner of the end): 45° when both setbacks are equal, any other
angle otherwise. A `CornerCut` whose setback along the long edge is most of its length is a taper.
A `CornerCut` with both setbacks equal to the full edges is the diagonal, and what remains is a
right triangle. Two `CornerCut`s meeting at the middle of an end make a point (a picket). A
`RoundedCorner` is a rounded corner. A `CurvedEdge` is the one curve a jigsaw and a thin batten
produce: mark three points, bend a batten through them, draw, cut.

**Why two setbacks and not an angle.** A miter is set on a saw by *angle*, and geometry-model §1.6
chose exact arcseconds precisely because every common miter angle is exact. But a cut has two
descriptions — the angle it is cut at, and the two marks it is laid out from — and on a fixed grid
only one of them can be stored exactly: a stored angle of 30° makes the cut's endpoint on the
edge irrational (5″ · tan 30° = 2.8868…″), and stored setbacks of 3″ and 5″ make the angle
irrational (atan 5⁄3 = 59.04…°). One must be the stored form and the other an *entry mode* with
exactly one explicit, flagged rounding — the pattern §1.4 of the geometry model already uses for
decimal input.

The stored form is the **setbacks**, because:

- every vertex of the outline is then on the grid, and no trigonometry enters the kernel — the
  same property that keeps the first beta's rectilinear geometry exact;
- a taper, a clipped corner and a triangle are natively two lengths ("3″ back along the end, 5″
  along the edge"), and they are the majority of magazine cuts;
- the one angle a magazine uses far more than any other, 45°, is exact as equal setbacks;
- a setback is what a person actually marks on the board before cutting, whatever they set the
  saw to.

The price is that a typed 30° miter is stored as its setback rounded to the grid and reads back
as "≈ 30°" — with the `≈` marker the display already uses for anything not exactly on the grid.
The alternative (store the typed angle for mitres, accept off-grid outline vertices) is open
decision §11.1.

### 1.4 Every cut is square through the plan

A cut is made **perpendicular to the plan view and runs the full out-of-plane dimension** of the
part. That is the definition, not a limitation to work around: it is what a circular saw with the
blade at 0° tilt, a table saw with the blade square, and a jigsaw all do. It has two consequences
worth stating now, because the second will surprise people:

1. **Bevels are not in the model.** A blade tilted to 30° cuts an edge that looks straight from
   above; the plan view cannot show it, so the model cannot hold it (§6).
2. **A cut is only expressible in the plane the part is drawn in.** The coffee-table legs are
   drawn as footprints, 2½″ × 2½″, with the length out of plane (parts-and-cut-list §1.1). A
   `RoundedCorner` on that footprint is a **roundover along the whole length of the leg**, and a
   `CornerCut` is a **chamfer along the whole length** — both real, both cuttable, and the cut
   list says "for the full length" (§4.4). But a *taper* on that leg is not drawable, because it
   lives in the elevation. A person who wants a tapered leg draws the leg lying flat — a
   16¼″ × 2½″ box with `planAxes { x: length, y: width }` — and tapers that; napkin does not
   mind that the "plan" of that part is really its face. Marc's "tapering a piece" is covered for
   any part drawn lying flat, and is not covered for a part drawn as its cross-section. This is
   the plan-view-only model of parts-and-cut-list §1.3 ("not a solid") made visible.

### 1.5 The derived outline

```csharp
/// The closed boundary of what is left of the blank, in world coordinates, counter-clockwise
/// in the box's local frame (south edge first, from the south-west corner).
public sealed record Outline(ImmutableArray<OutlineSegment> Segments);

public abstract record OutlineSegment(Point2 From, Point2 To);
public sealed record StraightSegment(Point2 From, Point2 To) : OutlineSegment(From, To);
/// A circular arc whose centre is on the grid: a rounded corner. Radius = |From − Center|.
public sealed record ArcByCenter(Point2 From, Point2 To, Point2 Center) : OutlineSegment(From, To);
/// A circular arc through three grid points: a curved edge. The centre and radius are derived
/// in double by whoever draws it; nothing stored depends on them.
public sealed record ArcThrough(Point2 From, Point2 Through, Point2 To) : OutlineSegment(From, To);
```

`Box.Outline()` walks the four edges in local order. At each corner, a `CornerCut` or
`RoundedCorner` stops the incoming edge at the cut's point on that edge, emits the cut segment,
and starts the outgoing edge at the cut's point on it. A `CurvedEdge` replaces the whole edge:
`Outward` stops the two adjacent edges `Depth` short of the corners and emits an `ArcThrough`
whose `Through` is the middle of the edge; `Inward` runs the adjacent edges to their corners and
emits an `ArcThrough` whose `Through` is the middle of the edge moved in by `Depth`. A straight
run that a cut has consumed entirely is omitted, not emitted at zero length. The local points are
then rotated by `Box.Rotation` and offset from `Anchor`, exactly as `Box.Corner` does.

Every point on the outline is **exact under right-angle rotations**, for the same reason the
corners are: a `CornerCut`'s two points are the corner offset by a stored length along an axis; a
`RoundedCorner`'s two tangent points and its centre are the corner offset by `Radius` along one or
both axes; a `CurvedEdge`'s three points are corners, corners offset by `Depth`, and the middle of
the edge — which, like `Box.Center`, rounds by half a unit when the edge is an odd number of units
long, and that is the only rounding in the outline. Under a non-right-angle rotation the outline
rounds through `Length.FromInches` exactly as the corners do today, and only the solver path
reaches that.

The arcs themselves — the points along a rounded corner or a curved edge — are never stored and
never compared. The canvas draws them from the two exact endpoints and the exact centre (a
rounded corner) or the exact third point (a curved edge), in `double`, like every other pixel.

### 1.6 Invariants

Added to geometry-model §2.5's list. A `Box` that fails any of these is invalid; `Sketch.Validate`
checks them; the updater never returns a sketch containing one; the loader refuses a file
containing one (§5).

5. **Cuts are in site order and no site is used twice.** `Cuts` is sorted by `CutSite`'s fixed
   order, so that two boxes with the same cuts are equal by value and a saved file has one
   spelling.
6. **One cut of any kind per corner, and a curved edge counts at both its corners.** No corner
   cut, rounded corner or *second curved edge* may touch either end of a `CurvedEdge`; two
   adjacent edges cannot both be curved. (An outward curve has already removed those corners; an
   inward curve keeps them, and letting a second operation land on the arc's endpoint is the
   kind of interaction this design keeps out — one operation per site, and a curve is an
   operation on three sites.)
7. **Every stored value is positive**, and **every setback fits its edge**: for a `CornerCut`,
   `0 < AlongX ≤ Width` and `0 < AlongY ≤ Height`; for a `RoundedCorner`, `0 < Radius ≤ Width` and
   `≤ Height`; for an `Outward` curve, `0 < Depth ≤` the length of each adjacent edge; for an
   `Inward` curve, `0 < Depth <` the blank's size perpendicular to the edge, strictly.
8. **The claims on an edge sum to at most its length.** Each edge has a budget equal to its
   length; a cut at either end claims part of it (a `CornerCut` claims `AlongX` from an edge that
   runs along X and `AlongY` from one that runs along Y; a `RoundedCorner` claims `Radius`; an
   `Outward` curve on an adjacent edge claims `Depth`). The two claims on an edge sum to at most
   the edge. Equality is allowed — that is a mitre that runs the whole end, or two cuts meeting in
   a point.

   **The same budget applies across the blank**, so that a curve's sag can never reach a feature
   on the opposite edge: any `CurvedEdge` claims `Depth` from the blank's size perpendicular to
   it (an outward curve's lowest point and an inward curve's apex both sit `Depth` in from the
   edge line), and any cut at a corner of the *opposite* edge claims its reach in that direction
   — `AlongY` for a corner cut facing a north or south curve, `AlongX` facing an east or west
   one, `Radius` for a rounded corner, `Depth` for a curve. The two sides' claims sum to at most
   the size, and **strictly less** when either side is an inward curve — two scallops that meet
   at the centre leave two lobes and no part. With this, no two segments of an outline can cross:
   straight cuts cannot interleave on a convex boundary once invariant 8 holds on every edge,
   and an arc stays within `Depth` of its edge line.
9. **What is left has positive area.** The polygon through the outline's straight vertices and
   each curved edge's `Through` point has strictly positive area, computed exactly in `Int128`
   (geometry-model §1.3's `Area` is this arithmetic). Invariant 8 does not catch everything: two
   full-diagonal `CornerCut`s at opposite corners pass it and leave nothing; this does not.

Invariants 7 and 8 are the ones a person hits by resizing (§2.3); 9 is the catch-all for the
degenerate-area cases 8 leaves. All are cheap — a box has at most eight cuts.

### 1.7 What a cut is not

- **Not joinery.** A tenon, a dado, a rabbet and a mitre *joint* change how two parts meet, and
  napkin still does not know about them (parts-and-cut-list §1.3). A mitred end here is a shape,
  and the cut list lists it as a cut on a blank of the stated length; it does not lengthen the
  blank to make a mitre joint close.
- **Not a relationship.** A cut references nothing outside its own box. "Cut this end to match
  that part's angle" is a relationship between two boxes' cuts, and it is not in this design.
- **Not an entity.** A rounded corner is not an `Arc`; `Tangent` and `Radius` (geometry-model
  §3.2, reserved for the solver "with arcs, when arcs exist") do not apply to it and are not
  extended here. When the solver workstream adds `Arc`, it adds it beside this, not under it.

---

## 2. Relationships, references and the updater

### 2.1 Every reference binds to the blank

`CornerRef`, `BoxEdgeRef`, `CenterRef`, `BoxWidthRef` and `BoxHeightRef` mean exactly what they
mean today: a corner, edge line, centre or size **of the blank**. No new reference kinds. The
whole relationship set of geometry-model §3.2 is reused unchanged, with these readings:

- `Flush(A.east, B.west)` where A's north-east corner is cut off: the two *edge lines* coincide,
  as always; the parts touch along the run of A's east edge that the cut left. That is what
  flush means at a bench, too — you register the blank's edge, not the clipped corner.
- `Coincident(CornerRef(A, NorthEast), …)` where that corner is rounded or cut: the point is the
  blank's *virtual corner*, where the two edge lines would meet. The canvas marks a referenced
  virtual corner with a small cross so nothing looks bound to thin air (§7). This is how a
  drafter dimensions a rounded corner, and it is the only reading under which the relationship
  survives adding or removing the cut without moving anything.
- `AxisDistance`, `Centered`, `ParamValue`, `EqualParam`, `Anchored`: unchanged; they are about
  blank corners and blank sizes.
- `Flush` against an edge that carries a `CurvedEdge`: the blank's edge line. For an outward
  curve, the arc touches that line at the middle of the edge, so another part flush to it meets
  the curve at its apex. For an inward curve, the arc's ends are on the line.

Because cuts move with the blank, **the `Propagator` never sees them**: it assigns anchor X/Y,
width and height exactly as today, and the outline follows. `Drag`, `SetPosition` and
`SetRotation` need no change — a cut is in the local frame, so rotating a box with cuts by a
right-angle multiple is the same exact swap-and-negate it is for the corners.

A reference to a *cut point* (the end of a taper, the tangent point of a fillet) is possible
later as a `CutPointRef` — it would be exact, and its scalar would be a setback the propagator
could assign. It is not in this design: no magazine instruction dimensions a part from the end of
a taper, and adding a scalar kind to the propagator is real work for no present need.

### 2.2 Requests

Two exact requests, neither of which moves geometry:

```csharp
/// Adds a cut, or replaces the cut at the same site. Exact.
public sealed record SetCut(EntityId Box, Cut Cut) : Request;

/// Removes the cut at a corner or edge. Exact.
public sealed record RemoveCut(EntityId Box, CutSite Site) : Request;
```

`SetCut` is `Rejected(CutSiteTaken)` when the site is claimed by a curved edge (or the cut is a
curved edge and one of its corners has a cut) — invariant 6; `Rejected(CutDoesNotFit)` when
invariants 7–9 fail on the box as it is; otherwise `Solved`, with the box in `ChangeSet.Modified`
(geometry-model §10.3: the set for what is neither moved nor resized). `RemoveCut` is
`Rejected(NoSuchCut)` when there is nothing at the site. Three new `RejectionReason` values, no
new result type. `AddEntity` of a box with cuts validates the same invariants.

The canvas commits one `SetCut` per gesture (§7) and uses `ApplyQuietly` for the live preview
between pointer moves, exactly as a drag does.

### 2.3 Resizing a blank that has cuts

A setback is an absolute length, so a blank can be made too small for its cuts. This can happen
by a typed dimension (`SetParameter`, `AddRelationship(ParamValue …)`), by propagation (an
`EqualParam` that copies a smaller width onto a box with a 1″ radius corner), by a stock
assignment that fixes a cross-section (parts-and-cut-list §1.2, which is a geometry request), or
by a solver run. The `Propagator` knows nothing about cuts and should not; the seam is **after the
write, before the return**:

- **Exact requests.** `DirectUpdater.Apply` writes the propagator's assignments into a new
  sketch, then checks invariants 7–9 on every box the change set lists as resized. Any failure
  → `Rejected(CutDoesNotFit)` naming the box and the site, and the original sketch is returned
  untouched by construction. The canvas says which cut does not fit and offers to remove it.
- **`DragEdge`.** Best effort, as always: the delta is clamped so that the edge is never dragged
  past what the cuts on it claim (invariant 8 with the new length), and the applied delta is
  reported. A blank cannot be dragged shorter than its cuts, the way it cannot be dragged
  through an anchored neighbour.
- **The solver** (§3) runs the same check at geometry-model §5.2 step 5, and a failure is
  `OverConstrained(NoRepresentableSolution)` there, because by then a whole solution is being
  verified.

Why a rejection and not a conflict report: no relationship is involved, so there is nothing for
`ConflictReport.Relationships` to name honestly, and geometry-model §4.2's rule is that the
conflict report should never have to cover for something that is not a relationship conflict. Why
not scale the cuts with the blank: a stated radius is a stated fact, and "round the corners to a
1″ radius" on a top that grows from 24″ to 30″ should still be a 1″ radius. Open decision §11.5.

### 2.4 Snapping and hit testing

- **Grips** stay on the blank's corners and edge midpoints (`BoxGeometry` is unchanged). A
  rounded corner still has its resize handle at the virtual corner.
- **Snap targets** stay the blank's axis-aligned edges (`BoxGeometry.AxisAlignedEdges`,
  `SnapResolver`): what a person aligns to is the edge line, and the relationship a snap creates
  is a `Flush` on that line (§2.1).
- **Selection hit testing** uses the outline, so a click in a clipped-off corner selects what is
  under it, not the part whose corner was cut. Point-in-outline is exact for straight segments
  and for a rounded corner (a squared distance to an exact centre, in `Int128`); for a curved
  edge it is a `double` test against a fitted arc. That is a canvas decision about a click, and
  nothing stored depends on it.

### 2.5 Snapping between shaped parts

Because every reference binds to the blank (§2.1), snapping a shaped part to a shaped part, a
shaped part to a plain board, or a plain board to a shaped part is the same `Flush` on two blank
edge lines, produced by the same `SnapResolver` reading the same `AxisAlignedEdges`. Nothing
new is needed; this section says so with two examples and names the one case that is out of reach.

**Two gussets on a board.** Gusset A is a 6″ × 6″ blank cut corner to corner —
`CornerCut(NorthEast, 6″, 6″)` — leaving the triangle south-west, south-east, north-west. Gusset
B is the mirror: `CornerCut(NorthWest, 6″, 6″)` leaves south-west, south-east, north-east. C is a
2x4 lying flat. Drop A onto C: `Flush(A.south, C.north)` — A's blank south edge is also the
triangle's base, so the two parts touch along it. Drop B beside A: `Flush(B.west, A.east)` —
A's blank *east* edge is a line the diagonal removed entirely (the triangle meets it only at
south-east), and the relationship holds the two blank lines coincident anyway; then
`Flush(B.south, C.north)` seats B on the board too. On screen the two triangles' points meet at
C's edge with a V between them, which is what two such gussets look like. Drag A and B follows;
resize C and nothing on A or B moves, because `Flush` ties lines, not extents. The snap indicator draws the blank's edge line (the faint line of §7.2) so a person
sees what they are snapping to when it is not a drawn edge. Three pairings, one relationship kind.

**A mitred picture-frame corner** — the case Marc named, and it works, for a reason worth
stating. Top rail A runs along X, `w` wide, with `CornerCut(SouthEast, AlongX = w, AlongY = w)`:
a 45° mitre from its inner corner up to its outer north-east corner. Right rail B runs along Y
(`Width = w`, `planAxes { x: width, y: length }`) with `CornerCut(NorthWest, AlongX = w,
AlongY = w)`: from its outer north-east corner down to its inner corner. Hold them with
`Coincident(CornerRef(A, NorthEast), CornerRef(B, NorthEast))` — **both blanks share the outer
corner** — and `EqualParam(BoxHeightRef(A), BoxWidthRef(B))`. The two blanks overlap in a `w × w`
square; A keeps its upper-left triangle, B its lower-right, and the two cut segments are **the
same line with the same two endpoints, exactly**: from the shared outer corner to the shared
inner corner. Lengthen either rail and the joint holds, because the coincident corner and both
cuts are at the same end. Every cut-face-to-cut-face meeting between two *axis-aligned* blanks is
this shape: coincident blank corners plus equal setbacks.

What the frame does **not** get, stated as the real limitation it is:

- **Nothing keeps a mitre "full".** Both setbacks equal the width because a person typed them so
  (or used the workshop's "full mitre" entry, §7.2, which sets them once). Change `w` through the
  `EqualParam` and the setbacks stay at the old `w`: the cut still *fits* (§2.3 does not refuse
  it) and is no longer a full mitre. Keeping a cut equal to a size, or to another part's cut, is a
  relationship — the natural shape in this codebase is a `SameCut`/`CutEqualsSize` kind, stored
  as data and propagated by the updater like `EqualParam` — and it needs setback scalars in the
  propagator, the same extension `CutPointRef` needs (§2.1). It is the *same* missing piece as
  template propagation (§2.6). Not in this design; open decision §11.11.
- **A cut face against a part that is not axis-aligned** — an octagonal top's eight mitred
  rails, a diagonal brace's mitred ends against a post, a gusset's hypotenuse lying along a
  brace — needs the meeting part rotated by an angle that is not a right-angle multiple, which
  is the solver's domain already (geometry-model §4.4, §5). A `Flush` against a cut face itself
  would be a `CutFaceRef` as an `EdgeRef` whose direction is not axis-aligned: tolerance class,
  solver. This design adds no limitation the direct updater does not already have, and removes
  none.

### 2.6 Duplicates are values

The workflow is: design one gusset, then make four. **A duplicate is a value copy of the whole
box** — blank, cuts, part (stock, species, `planAxes`, out-of-plane, quantity), name — with a new
id and an offset anchor, and **no relationships**: it is unrelated until it is snapped, like any
part just drawn. Records already work this way: the canvas builds
`AddEntity(source with { Id = EntityId.New(), Anchor = source.Anchor + offset })` and puts it
through the updater; no new request kind, and undo, save and the change set see an ordinary
addition. Nothing tracks back to the source.

That is enough for the stated requirement, because of §4.3: four duplicates of one gusset have
the same blank, the same stock and the same cuts by value, so the cut list groups them into **one
row of four** — "cut 4 of these from a 1x6" — exactly as four hand-drawn legs group today.
`Part.Quantity` remains the other way to say "four of these": one box standing for four pieces
that are not placed individually. A duplicate copies the quantity as it is (it is a value copy; a
duplicate of a box that stands for four stands for four), and which of the two to use is the
person's call: quantity for parts nobody places, duplicates for parts that each get snapped.

**Template and instance: later, and probably as a relationship.** What a template adds over a
value copy is one thing — editing the recipe once and having every copy follow. In this codebase
that is not a template entity; it is a relationship between two boxes' cuts (`SameCut(a, b)`,
the analogue of `EqualParam`), stored as data and propagated by the updater, which is also what
keeps a mitre full (§2.5) and what a `CutPointRef` would need (§2.1). Three needs point at one
later feature, which is a good sign it is the right shape and no reason to build it before
someone asks: with value copies and by-value grouping, the cut list is already right. Per beta
policy and core-first, it waits. Open decision §11.11.

---

## 3. The solver seam

Cuts do not participate in the solver, and the recommendation is that they never need to.

The solver's variables (geometry-model §5.2) are anchor X/Y, width, height, rotation and node
X/Y. Setbacks, radii and depths are not among them: no relationship reads or writes one, so there
is nothing for a Jacobian to have a column for. The solver loads the blank, solves the blank,
rounds once, repairs through the `Propagator` — all unchanged — and at step 5 verifies the
repaired sketch, where the check of §2.3 runs alongside `RelationshipChecker`. A box whose cuts no
longer fit its solved blank is a solution napkin cannot represent, which is what
`NoRepresentableSolution` already means.

The outline of a box rotated by a non-right angle rounds through `Length.FromInches` at every
vertex, exactly as its corners do (geometry-model §2.1). Nothing in this design makes that
worse: a cut adds two or three points per site, each an offset of a stored length along a local
axis, rounded once.

If a `CutPointRef` ever exists (§2.1), a setback becomes an assignable scalar and joins the
propagator's `ScalarKind` — the same extension the solver workstream already needs for rotation
(geometry-model §10.5). That is the only route by which a cut could ever reach the solver, and it
is not taken here.

---

## 4. The cut list and the shopping list

### 4.1 The blank is the row

The cut list already reads the box's stored `Width` and `Height` and the part's `OutOfPlane`,
never the corners (`CUT-002`). With cuts on the box, **that is still exactly the right thing to
read**: the three finished dimensions of a shaped part are the dimensions of the blank you start
from, and a row says "cut a 24″ × 48″ × ¾″ piece" whether or not its corners are then rounded.
No bounding box is ever computed, and a taper never shrinks the listed size. `CUT-001`, `CUT-002`
and `CUT-003` hold unchanged.

### 4.2 The row carries its cuts

A `CutListRow` is a value with no formatting decisions in it (parts-and-cut-list §3.1), so the
row carries the cuts structurally and the sentence is derived from them the way `MaterialText` is
derived from `Material`:

```csharp
public sealed record CutListRow(
    string Label, int Quantity,
    Length Length, Length Width, Length Thickness,
    string Material, bool Unresolved, StockItem? Stock,
    ImmutableArray<Cut> Cuts,        // the blank's cuts, in site order; empty for a rectangle
    PlanAxes PlanAxes,               // so a describer can say which edge is which
    PartDimension OutOfPlane,        // so it can say "for the full length" when it must
    ImmutableArray<EntityId> Members)
{
    /// What to do to the blank, in plain words, one sentence per cut or group of like cuts.
    /// Rendered with LengthFormat.Default, as CutListCsv.Text already is.
    public ImmutableArray<string> CutText => CutDescription.Describe(Cuts, PlanAxes, OutOfPlane);
}
```

`CutDescription` lives in `Napkin.Modules.Furniture` beside `CutList`: the vocabulary of ends,
edges and radii is the furniture module's, not the kernel's.

**A value-equality trap, named so it is not fallen into.** `CutListRow` already writes its own
`Equals`/`GetHashCode` because `ImmutableArray` compares by the identity of the array it wraps;
`Cuts` must join both, compared **by sequence**, or two rows with different cuts would compare
equal and "the same design gives the same list" would pass for the wrong reason. The same holds
for the group key below: a record struct with an `ImmutableArray<Cut>` field would compare it by
reference and silently never group two shaped parts.

### 4.3 Grouping

The cuts join the group key (parts-and-cut-list §3 step 4): two parts are one row when their
three finished dimensions, stock, species **and cut lists** are equal — exact record equality on
the cuts, in site order, so the same cuts at the same sites with the same values group, and
nothing else does. Consequences worth stating:

- Four legs with the same chamfer on the same corner are one row of four. Two legs whose
  chamfers are on mirror-image corners are **two rows**, each saying which corner. A magazine
  would write "make two of each hand"; that is a nicety this design does not do (open decision
  §11.6) — two honest rows are better than one row that hides which corner.
- A rectangle and the same rectangle with a cut are two rows, as they should be: they are not
  the same piece.

Ordering keeps its existing keys (descending length, width, thickness, then label, then material
— `CutList.Of` has no tie-break beyond material today) and **adds the cut sequence as the final
tie-break**: a rectangle sorts before the same rectangle with cuts, and two rows that differ only
in their cuts sort by site order then by value. Without that the order of two such rows would
depend on enumeration order, and the list would not be the same on every machine.

### 4.4 Describing a cut at the bench

The description names corners and edges by **compass, in the part's own frame as drawn** —
"the north-east corner", "the north edge" — because that is the one frame the file, the
references, the canvas grips and the conflict messages already share, and the person at the bench
holds the part the way they drew it. On screen, each row with cuts also draws a **thumbnail of
its outline** — the magazine's "see drawing" — so the words are rarely the only guide. The CSV has
only the words. The vocabulary is open decision §11.4.

One function, `CutDescription.Describe`, with these phrasings (the exact strings are pinned by
golden tests, not by this document):

| Cut | Says |
|---|---|
| `CornerCut`, neither setback a full edge | "Cut off the north-east corner: mark 3″ along the north edge and 5″ along the east edge, and cut between the marks." |
| `CornerCut`, one setback a full edge (a mitre, a taper) | "Mitre the east end: from 3″ in along the north edge to the south-east corner (≈ 31° off square)." The angle is `atan(short setback ⁄ full edge)`, shown to the nearest half degree with `≈` unless exact — equal setbacks give exactly "45°". |
| `CornerCut`, both setbacks full edges | "Cut corner to corner, from the north-west corner to the south-east corner." |
| `RoundedCorner` | "Round the north-east corner to a 1″ radius." |
| `CurvedEdge`, outward | "Curve the north edge: mark 1″ down from each end on the east and west edges, draw a fair curve from mark to mark through the middle of the north edge, and cut it (≈ 24′-0½″ radius)." |
| `CurvedEdge`, inward | "Scallop the north edge: mark 2″ in at the middle, draw a fair curve from corner to corner through the mark, and cut it (≈ … radius)." |

Two rules on top of the table:

- **Like cuts are said once.** Cuts of the same kind and values at several sites collapse to one
  sentence listing the sites — "Round the north-east and north-west corners to a 1″ radius" —
  and "all four corners" when it is all four.
- **When the out-of-plane dimension is not the thickness, say so.** A rounded corner on a leg's
  footprint is a roundover the whole length of the leg, and the sentence ends "for the full
  16¼″ length". When the out-of-plane dimension is the thickness (a top lying flat) nothing is
  added: a cut through the thickness is what "cut" means.

Every length is rendered by `LengthFormat.Default` with the same `≈` rule the rest of the list
uses; a derived angle or radius is always marked `≈` unless it is exact (only 45° and
corner-to-corner are).

### 4.5 CSV

The CSV gains one trailing column, `Cuts`, holding the row's sentences joined by "; " and quoted;
empty for a plain rectangle. The header line's statement — finished sizes before saw kerf and
joinery allowance — stays, and it is still true: the sizes are the blank's. The parse-back test
(parts-and-cut-list §7 step 5) extends to the new column.

### 4.6 The shopping list is unchanged

It consumes rows, and it reads the three blank dimensions and the stock (parts-and-cut-list §4).
A shaped part buys exactly the board its blank needs. Nothing in §4 of that design changes, and
no test in it changes.

---

## 5. The file format

One change, one bump: **`formatVersion` 2 → 3.** Every box gains a required `cuts` array,
written in site order:

```json
{ "id": "…", "type": "box", "layer": "…", "name": "Top",
  "anchor": { "x": 0, "y": 0 }, "width": 49152, "height": 24576, "rotation": 0,
  "part": { … },
  "cuts": [
    { "kind": "roundedCorner", "corner": "southWest", "radius": 1024 },
    { "kind": "cornerCut",     "corner": "southEast", "alongX": 3072, "alongY": 5120 },
    { "kind": "curvedEdge",    "edge": "north", "bow": "inward", "depth": 2048 }
  ] }
```

| `kind` | Fields | Refused when |
|---|---|---|
| `cornerCut` | `corner`; `alongX`, `alongY`: integer units | a value is not a positive integer, or the cut fails invariants 7–9 against the box |
| `roundedCorner` | `corner`; `radius`: integer units | as above |
| `curvedEdge` | `edge`; `bow`: `outward` or `inward`; `depth`: integer units | as above, or `bow` is not one of the two |

Also refused: an unknown `kind`; two cuts at one site, or a corner claimed by a curved edge
(invariants 5 and 6); cuts not in site order — the file has one spelling, so that `Load(Save(s))
== s` holds and a file is refused rather than quietly re-ordered, the same stance the format takes
on an un-normalised rotation. Corner and edge names are the ones references already use
(`southWest` …, `south` …).

**On compatibility, both halves honestly.** The *model* is a strict superset: a box with an empty
`Cuts` list is today's box in every respect — same corners, same relationships, same cut-list row,
same drawing — so there is no migration code, no shim and no behaviour change for anything that
exists. The *files* are not untouched: the reader has no optional fields (file-format.md, "Every
field listed in this document is required"), so `"cuts": []` is written on every box, the version
is bumped, a version-2 file is refused with the message the reader already gives, and both
committed samples gain `"cuts": []` in the same commit — exactly what #8 did for `"part": null`
(parts-and-cut-list §8.1). Per beta policy there is no converter.

---

## 6. Non-goals

Stated so that scope does not creep past "the kind of parts you would find in a DIY magazine".
Each is a real thing; none is this design.

- **Bevels and compound angles.** A tilted blade cuts an edge that is not square to the face;
  a compound mitre tilts *and* swings. Neither is visible in a plan view (§1.4), so neither is in
  the model. Every cut here is square through the plan.
- **Arcs, circles and splines as drawing primitives.** No `Arc` entity, no curve tool, no
  bezier. Rounding exists only as a corner radius and a three-point edge curve on a blank. When
  the solver workstream adds `Arc`, it is a separate entity (geometry-model §2.3), not a change to
  this.
- **Interior cut-outs, pockets, holes, slots.** A hole for a cable, a hand-hold cut in the middle
  of a panel, a pocket for hardware: a different feature (an interior boundary), designed when
  wanted.
- **More than one operation per corner, or cuts that cross each other.** One `CornerCut` or one
  `RoundedCorner` per corner, one `CurvedEdge` per edge, and a curved edge owns its corners. A
  clipped-then-rounded corner is not expressible, on purpose.
- **Relationships on cuts.** "Keep this mitre full", "keep these two cuts the same", "match
  that angle": a `SameCut`/`CutEqualsSize` relationship kind and a `CutFaceRef` are the natural
  later shapes (§2.5, §2.6); neither is here — this is
  [issue #67](https://github.com/marctjones/napkin/issues/67), a separate design-and-build pass.
- **Templates, instances and a shape library.** A duplicate is a value copy that tracks nothing
  (§2.6). Saving a shape to reuse across projects is a later feature, if ever.
- **Toolpaths, kerf, tool choice, cut order.** The cut list says what to cut, not how to set the
  fence. Kerf stays out (parts-and-cut-list §8.5).
- **Joinery allowance.** A mitre joint is two mitred ends and no lengthening; parts-and-cut-list
  §1.3 stands.
- **Mirror-image grouping in the cut list** ("two of each hand"): §4.3, open decision §11.6.
- **Sheet-goods nesting of shaped parts.** Nesting is parked (PLAN.md); when it returns, a
  shaped part's blank is what it nests.
- **Grain direction.** Still not here.

---

## 7. Editing, at a design level

Short, because the UI plan is a later step; what is decided here is the two-level split, the
shape of the gesture, and where it goes through the editor.

### 7.1 Two levels: the canvas arranges, the shape workshop cuts

Marc: "making custom shapes probably needs its own subworkflow." Agreed, and the split is:

- **The canvas arranges.** Draw blanks, move, snap, relate, dimension, duplicate (§2.6) — parts
  shaped or plain, against each other. The `Select` and `Rectangle` tools, unchanged.
- **The shape workshop makes one blank into a shape.** Pick the stock (§1.2), set the free
  dimensions, add and edit cuts, watch the outline. Entered from the canvas with one part
  selected ("Shape…"), left to return to the canvas with the part where it was.

The workshop is **a mode the window is in, not a second document**, the way `EditTool.Rectangle`
is a mode and not a second canvas. Its scope is one box; every edit it makes is a request through
`DesignEditor.Apply` — `SetPart`, a `ParamValue` for a typed size, `SetCut`, `RemoveCut` — on
the same `Sketch`, so there is no second mutation path (CVS-005), undo and save see ordinary
edits, and a relationship the part already has still holds: a stock change that a `Flush` will not
allow is reported in the workshop exactly as it would be on the canvas. It shows the blank in its
**own frame, unrotated and large, with the compass corners labelled** — the frame the cut list's
sentences use (§4.4), so the words and the picture agree — with issue #7's stock picker and
readout at the top and the list of cuts with their typed values beside it.

Why not one continuous gesture on the canvas: at canvas zoom the corner grips already mean
*resize*, and overloading them with *cut* trades one gesture's clarity for another's; and picking
stock, typing a setback, and reading which corner is which all want room a canvas tool does not
have. Whether the workshop is a panel, a sheet or a window is UI, and not decided here.

### 7.2 The gesture inside the workshop, and what the canvas shows

- **Targets.** With the workshop open, the blank's corners and edge midpoints show as targets —
  the same points `BoxGeometry.GripPoint` already knows — and hovering names what a press would
  do.
- **Press on a corner and drag inward** to clip it: the two setbacks follow the pointer, snapped
  to the grid, so a person sees the triangle they are removing grow. A modifier holds the two
  setbacks equal (45°). **Press on a corner with another modifier and drag** to round it: the
  radius follows the pointer. **Press on an edge midpoint and drag** to curve it: inward when the
  pointer goes into the part, outward otherwise, and the depth follows the pointer.
- **The gesture is one `SetCut`.** A `CutTool` state machine, pure like `RectangleTool` — two
  model points and a target, no pixels — produces the candidate `Cut` for each pointer move; the
  workshop applies it with `DesignEditor.ApplyQuietly` for the live preview, and commits the last
  one with `Apply` on release, inside `BeginGesture`/`EndGesture` so undo sees one step. Nothing
  reaches an entity except through the updater (CVS-005), and a cut that does not fit is refused
  with the site named, like any other refused edit.
- **Typing is exact.** A selected cut shows its numbers in the properties panel — two setbacks
  or an angle-and-setback for a corner cut, a radius, a depth — and typing one is a `SetCut` with
  the new value. An angle typed for a corner cut is converted to the setback it implies with one
  explicit rounding, and the field shows `≈` when the stored setback is not exactly what the
  angle asked for (§1.3). This is the "resize by typing a dimension" pattern applied to a cut.
  A "full mitre" entry sets both setbacks to the blank's width in one go — once; see §2.5 for
  what it does not keep.
- **Removing a cut** is selecting it and pressing delete: `RemoveCut`.
- **The canvas draws the outline**, not the rectangle, for a box with cuts; it draws the blank's
  removed edges as a faint line when a relationship references a virtual corner or a cut edge's
  line (§2.1), and while a snap is catching one (§2.5), so a `Flush` to a rounded-off corner
  never looks bound to nothing.
- **Duplicate** is a canvas command on a selected part (§2.6): one `AddEntity` of a value copy,
  offset by a grid step, selected on completion so the next thing a person does is drag it into
  place and snap it.

Grips, snapping and dimensions stay on the blank (§2.4), so the `Select` tool does not change.

---

## 8. Fixtures: the coffee table stays square; a new sample carries the rounded corners

**Decided (§11.7): two fixtures, not one changed.** `samples/coffee-table.*` is left exactly as
drawn — five plain rectangles, square corners, no cuts — because it is Marc's design and this
document does not get to edit it by adding a feature to its top. A shaped part earns its own
sample instead, the way every other feature that needed a worked example got one
(`samples/README.md`).

**A new sample, `samples/rounded-corner-table` (name Marc's call at authoring time).** Same top as
the coffee table's — a chance to reuse hand-derived numbers rather than invent a new size — with
`RoundedCorner(radius 1024)` at all four corners, so the one thing this sample exists to show is
legible against a familiar shape:

- The top is `anchor (0, 0), width 49152, height 24576` (48″ × 24″). With
  `RoundedCorner(radius 1024)` at all four corners, the outline's straight vertices are the eight
  tangent points, each 1024 units from a corner along an edge — `(1024, 0)`, `(48128, 0)`,
  `(49152, 1024)`, `(49152, 23552)`, `(48128, 24576)`, `(1024, 24576)`, `(0, 23552)`, `(0, 1024)`
  — and the four arc centres are `(1024, 1024)`, `(48128, 1024)`, `(48128, 23552)`,
  `(1024, 23552)`. Every value is a multiple of 1024; nothing rounds.
- The cut-list row: `Top | 1 | 4'-0" | 2'-0" | 3/4" | —` — the same finished dimensions a plain
  top of this size would have — plus one sentence: "Round all four corners to a 1″ radius." The
  out-of-plane dimension is the thickness, so no "for the full …" suffix.
- The shopping list is unchanged by the cuts (§4.6): the top still buys the same sheet.
- Legs and aprons: whatever the new sample needs to be a complete, standalone design (not just a
  top floating alone) is authored fresh when the sample is built — this document fixes only the
  one part that demonstrates the feature, not the whole fixture.

The legs, drawn as footprints, cannot carry a taper (§1.4); a ¼″ chamfer on one corner of each
would demonstrate the "for the full 16¼″ length" phrasing, and is a natural addition to the new
sample if whoever builds it wants a second worked example in the same file — not required.
`samples/README.md`'s rule applies to the new sample as much as to any other: its expectations
file is derived by hand, with a `derivation` string on every number and every sentence,
independent of napkin's own output.

---

## 9. Test plan

Feature-catalog ids are the next free `GEO-`, `CUT-` and `GUI-DRAW-` numbers, assigned when the
catalog is edited (`GEO-015` and `CUT-006` are the last today); the shapes are not invented here.

### 9.1 Golden cases

Model and outline (`Napkin.Core.Geometry.Tests`):

1. A box with no cuts: `Outline()` is four `StraightSegment`s through the four corners, in the
   stated order.
2. Each cut kind at each of its sites, rotation 0, on a 48″ × 24″ blank: the outline's segments
   and every point, exactly. The rounded corner's `ArcByCenter` has its centre `Radius` in from
   the corner on both axes; the curved edge's `Through` is the middle of the edge (or the middle
   moved in by `Depth`).
3. The same under the other three right-angle rotations: the outline equals the rotation-0
   outline rotated about the anchor, point for point, exactly.
4. A mitre (one setback the full edge), a taper, a diagonal (both full), two cuts meeting in a
   point: each outline exact, each omitting the consumed straight run rather than emitting it at
   zero length.
5. Each invariant 5–9 violated in turn: `Sketch.Validate` names the box and the site; `AddEntity`
   is `Rejected`. The two-diagonals case is caught by 9 and not by 8.
6. `Cuts` order: a box constructed with cuts out of site order holds them in site order (the
   initialiser sorts — sorting is lossless, so the model normalises; the *file* is refused, §5),
   two boxes with the same cuts are equal by value, and a duplicate site is a `Validate` failure.

Updater:

7. `SetCut` on a free corner: `Solved`, box in `Modified`, nothing moved, no relationship
   disturbed. Same site again with a different value: replaced. A `CornerCut` on a corner a
   `CurvedEdge` claims: `Rejected(CutSiteTaken)`. `RemoveCut` with nothing there:
   `Rejected(NoSuchCut)`.
8. `SetParameter` shrinking a width below a rounded corner's radius: `Rejected(CutDoesNotFit)`
   naming box and site; sketch unchanged. Growing: `Solved`. The same through an `EqualParam`
   from another box: rejected the same way, the whole batch untouched.
9. `DragEdge` toward a corner cut: applied delta clamped to what the cut leaves; away: full.
10. `SetRotation` on a box with cuts and no relationships: `Solved`, outline rotated exactly.
11. `Flush` between a cut box and a plain one, then resize the plain one: the cut box translates,
    cuts and all; the edge lines still coincide. `Coincident` on a rounded corner: holds at the
    virtual corner before and after `RemoveCut`.
11a. **The gussets** (§2.5): `Flush(B.west, A.east)` on A's cut-away east edge is accepted, holds,
    and drags both; `Flush(A.south, C.north)` against the plain board the same.
11b. **The picture-frame corner** (§2.5): two rails, coincident outer corners, equal widths, full
    mitres — the two cut `StraightSegment`s have the same two endpoints, exactly. Lengthen
    either rail: still exact. Change the width through the `EqualParam`: the joint no longer
    closes and the test *documents* that (the §2.5 gap), rather than asserting it away.
11c. **Stock makes the blank** (§1.2): assigning "1x6" to a flat part sets its width to 5½″
    (5632 units) through the updater, exactly, and a drag on that edge is `Rejected(DrivenSize)`;
    assigning "1x4" to a part with a 4″ setback is `Rejected(CutDoesNotFit)` and nothing is
    half-assigned; a footprint part assigned "2x4" has both plan dimensions set.
11d. **Duplicate** (§2.6): the copy equals the source in every field but id and anchor, carries
    no relationships, and the cut list lists source and copy as one row of two.

Cut list (`Napkin.Modules.Furniture.Tests`):

12. A part with cuts lists the blank's three dimensions (the `CUT-002` analogue); the row's
    `Cuts` equals the box's.
13. Four legs with the same chamfer: one row of four. Two legs with mirror chamfers: two rows. A
    rectangle and the same rectangle rounded: two rows.
14. Description goldens, one per phrasing in §4.4: clip, mitre with `≈ 31°`, 45° exactly,
    corner-to-corner, radius, outward and inward curve with `≈` radius, "all four corners",
    "for the full 16¼″ length" on a footprint part and its absence on a flat one.
15. CSV: the `Cuts` column round-trips through `CutListCsv.Parse`; an empty column for a
    rectangle.
16. The shopping list of a design with cuts equals the shopping list of the same design without
    them.

Format (`Napkin.Core.Project.Tests`):

17. `Load(Save(s)) == s` for a sketch holding every cut kind. Refusals, each named: unknown kind,
    duplicate site, out of site order, a cut that does not fit its box, a zero or negative value,
    `bow` misspelt, and a `formatVersion: 2` file.

Fixture:

18. The new rounded-corner sample's (§8) top outline and its cut-list sentence against its own
    `expected.json`, derived by hand and independent of napkin's output. The coffee-table fixture
    and its existing expectations are untouched by this design.

### 9.2 Properties

Extending geometry-model §7.2's generator to produce boxes with valid cuts (derive the cuts from
the blank's sizes, never sample and hope):

- **P1 (never inconsistent)** now includes invariants 5–9: every `Succeeded` sketch validates.
- **P11 — the outline is closed and positive.** For every valid box, consecutive segments share
  their endpoint, the last returns to the first, and the polygon area of §1.6's invariant 9 is
  positive.
- **P12 — cuts are invisible to propagation.** For every sketch S and request R that is not a
  `SetCut`/`RemoveCut`, applying R to S and to S-with-all-cuts-removed gives results whose blanks
  are identical, unless the cut result is `Rejected(CutDoesNotFit)`. **`DragEdge` is excluded**
  (found landing step 3): it is already a best-effort request that clamps its delta at other
  existing limits (§2.3), and §2.3 has it clamp at what a cut claims too, rather than reject — so
  a cut can make a `DragEdge` on the cut-bearing sketch apply a *smaller* delta than the same
  request on the cuts-removed sketch, both `Solved`. That is §2.3's own stated behavior for
  `DragEdge`, not a hole in this property; P12 as written only ever meant it for the exact-request
  path.
- **P13 — a successful resize leaves every cut fitting** (invariants 7–9 hold on the result).
- **P10 (undo)** holds for `SetCut`/`RemoveCut` without change: each result is a whole new
  `Sketch`.

### 9.3 GUI workflow

Two `[GuiWorkflow]` scenarios, keyboard and pointer both, ≥5 actions, an assertion after a state
change (docs/testing/gui-automation.md):

- **Shape one part.** Draw a rectangle, select it, open the workshop by keyboard, pick a stock,
  press on a corner and drag inward with the pointer, release, leave the workshop, assert the
  canvas outline changed and the cut-list window shows the sentence, undo, assert the rectangle
  is back.
- **Duplicate and snap.** With a shaped part selected, duplicate it, drag the copy until it
  snaps flush to the original, assert the relationship list names the `Flush` and the cut list
  shows one row of two.

---

## 10. Implementation plan

For an Opus-tier implementer, once Marc signs off. Ordered so that each step leaves the build
green and the first five are a runnable slice with almost no GUI in it — a blank that is really a
1x6, a file with cuts that opens, lists and exports correctly — which is what CLAUDE.md's "core
functionality first" asks for.

1. **Model** (`Napkin.Core.Geometry`). `Cut.cs` (`Cut`, `CornerCut`, `RoundedCorner`,
   `CurvedEdge`, `Bow`, `CutSite`), `Box.Cuts`, `Outline.cs` with `Box.Outline()`, the polygon
   area helper beside `Area`, invariants 5–9 in `Sketch.Validate`. Tests 1–6.
2. **Stock makes the blank** (parts-and-cut-list §1.2 made real). "Which dimensions does this
   stock fix, given `planAxes`" is a pure function of `(Box, Part, StockItem)` → requests, so it
   lives in `Napkin.Modules.Furniture` beside `CutList` — `StockAssignment.RequestsFor(…)` — for
   the same reason the cut list does (`Napkin.App` is outside the coverage ratchet), and the
   properties panel only calls it. It composes, in one `Batch` so nothing is half-assigned: a
   `ParamValue` on each in-plan dimension the stock fixes — or `SetParameter` on the one already
   driving it (geometry-model §4.1), so a width typed before the stock was chosen is not a
   `DuplicateRelationship` — and the `SetPart` carrying `OutOfPlane` from the stock when the fixed
   dimension is the out-of-plane one (a flat 1x6's ¾″ thickness), since parts §1.2 sets that value
   on the part directly. A driven in-plan size is then an ordinary one: a drag on it is already
   `Rejected(DrivenSize)`, a conflict with a `Flush` is already reported. Changing the stock is the
   same function again. The relationship list reads "width is 5½″ (1x6)". Test 11c minus the cut
   clause. This step has no cuts in it and is worth landing on its own.
3. **Updater.** `SetCut`, `RemoveCut`, the three rejection reasons, the post-write fit check in
   `DirectUpdater.Apply`, the `DragEdge` clamp. Tests 7–11d, P11–P13.
4. **Format** (`Napkin.Core.Project`, `docs/file-format.md`). `FormatStamp.CurrentVersion` 3;
   `cuts` in `SceneBinder`, `SceneWriter`, `SceneNames`; both samples gain `"cuts": []`; the
   format document updated in the same commit. Test 17.
5. **Cut list** (`Napkin.Modules.Furniture`). `CutListRow.Cuts`/`PlanAxes`/`OutOfPlane`, the
   group key, `CutDescription`, the CSV column. Tests 12–16. Then the new sample (§8):
   `samples/rounded-corner-table.scene.json` and its `expected.json`, by hand — the coffee-table
   fixture is untouched. Test 18.
6. **Canvas** (`Napkin.App`). Draw the outline; hit-test with it; the virtual-corner marker and
   the faint blank line under a snap; the thumbnail in the cut-list table; **Duplicate**.
7. **The shape workshop and the Cut tool.** The mode of §7.1; `CutTool`, live preview, the typed
   fields with angle entry and "full mitre". The GUI workflows of §9.3.

Whoever lands each step bumps the minor version (CLAUDE.md); nothing is tagged.

---

## 11. Decisions for Marc

Only what is genuinely a judgment call. Each has a working recommendation that stands until he
says otherwise, and beta policy makes changing any of them cheap.

1. **Decided (2026-09-22): setbacks, with angle as an entry mode, as recommended.** §1.3.
2. **Cuts on `Box`, in the geometry kernel (recommended) — or on `Part`, in the furniture
   module.** §1.1. On the box, the kernel draws and validates them and a wall may carry one; on
   the part, the kernel stays smaller and walls cannot be shaped.
3. **Decided (2026-09-22): yes, `CurvedEdge` is in the first slice, as recommended.**
4. **Bench vocabulary: compass words in the part's own frame plus an on-screen thumbnail
   (recommended).** §4.4. The alternative is naming ends and edges by their dimension ("the 48″
   edge", "the east end"), which reads better for a flat board and worse for a footprint or a
   square, and which the CSV cannot draw a picture beside.
5. **A resize that no longer fits a cut is refused (recommended) — or the cuts scale with the
   blank — or are clamped to fit.** §2.3. Refusing keeps a stated radius a stated fact; scaling
   keeps a taper's proportions when a leg gets longer; clamping never refuses and never asks.
6. **Mirror-image parts are separate rows (recommended) — or one row saying "two of each hand".**
   §4.3.
7. **Decided (2026-09-22): neither offered option — leave the coffee-table fixture as drawn, and
   add a new, separate sample for the rounded-corner top.** §8.
8. **`formatVersion` 2 → 3.** §5. It refuses every version-2 file, including both committed
   samples until step 4 updates them and any project anyone has saved since #8 landed. Beta
   policy working as designed, said out loud.
9. **Cuts on a box that is not a part** (a wall, an opening) are allowed, drawn, and ignored by
   everything but the canvas (recommended) — or refused until something needs them.
10. **Stock is the normal path, not a requirement (recommended).** §1.2. The workshop leads with
    the picker and a blank without stock is flagged "no stock chosen" in the workshop and the
    cut list, as today; it is not refused. Requiring stock would refuse the coffee-table fixture
    as drawn, every hardwood glue-up, and every part whose stock the library does not carry yet.
11. **Decided (2026-09-22): yes, relationships on cuts wait — spun out to
    [issue #67](https://github.com/marctjones/napkin/issues/67), designed and built in its own
    session, not this slice.** §2.5, §2.6. Until #67 lands, a duplicate is a value copy and a
    full mitre is one a person typed, exactly as §2.5 and §2.6 describe.
