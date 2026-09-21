# Lengths, the geometry model and the update interface

Status: DRAFT — implementation of #5 authorized by Marc on 2026-09-21 ("start implementing the
plan"). He did not answer the four decisions in §9 individually, so their recommendations are the
working choices; he can override any of them, and beta policy makes changing course cheap.

Design document for issue #4, written by Fable per [`PLAN.md`](../../PLAN.md). It decides the
foundations that #5 (Core.Geometry), #6 (Core.Project), #10/#11 (canvas, undo/redo) and #28 (the
constraint solver workstream) build on. Settled decisions from [`DESIGN.md`](../../DESIGN.md) are
taken as given and not re-argued here: imperial only (§10), no floating-point source of truth
(§5.1), relationships stored as data behind one update interface with explicit result types (§11),
a .NET-native solver as its own workstream (§11), and the beta policy (breaking changes always
allowed, no migration code — DESIGN.md "Versioning and releases").

Where this document refines the wording of DESIGN.md §11, it says so (§4.2 below), and DESIGN.md
is updated to match.

## 1. Length representation

### 1.1 The type

```csharp
/// One inch is 1024 units. Immutable, exact, ordered. Never constructed from a double
/// except by the explicit rounding entry points in §1.4.
public readonly record struct Length(long Units) : IComparable<Length>
{
    public const long UnitsPerInch = 1024;
    public const long UnitsPerFoot = 12 * UnitsPerInch;

    public static readonly Length Zero = new(0);
    public static Length Inches(long whole, long numerator = 0, long denominator = 1);
    public static Length FeetInches(long feet, long inches, long numerator = 0, long denominator = 1);
    // ... arithmetic and comparison operators, see §1.3
}
```

- **Unit: 1/1024 of an inch, stored as a `long`.** A power-of-two sub-inch grid represents every
  fraction a tape measure or a lumber standard uses exactly — 1/2 … 1/64, 23/32″ plywood, 1½″ ×
  3½″ for a 2×4, 5/4 decking at 1″, 6′-0″ header-span limits — and it represents the midpoint of
  any 1/64″ value exactly, four halvings deep. The grid is 16× finer than the finest display
  precision (1/64″), so rounding that happens below it (from a solver, or from a decimal entry
  like 3.505″) is invisible at any display precision the app offers.
- **Why `long` and not `int`:** `int` at 1/1024″ overflows at about 33 miles; not a problem for a
  lot plan, but `long` (about 1.4 × 10¹¹ miles) removes overflow from the list of things anyone
  has to think about in ordinary arithmetic, at no cost.
- **Every `long` count of units below 2⁵³ converts to `double` exactly.** That is what makes the
  fixed-point/double boundary in §5 lossless on the way in: the solver sees the exact stored
  value, and rounding happens only once, on the way back.

The table from issue #4 that ruled out integer millimetres, with this representation:

| Value | Units | Read back | Exact? |
|---|---|---|---|
| 1/16″ | 64 | 1/16″ | yes |
| 3½″ (2×4 width) | 3584 | 3½″ | yes |
| 23/32″ (¾″ nominal plywood) | 736 | 23/32″ | yes |
| 6′-0″ (a header-table span limit) | 73 728 | 6′-0″ | yes |
| 1/3″ (three equal shelves in 1″) | 341 (rounded) | 0.33301″ | no — flagged, see §1.4 |

The last row is the honest limitation of any fixed grid and the reason exact rationals were
considered. They were rejected because nobody can cut to a third of an inch, the denominators grow
without bound under repeated arithmetic, and their cost is paid on every operation to serve a case
the display would round anyway. The design instead makes rounding *explicit and reported* (§1.4).

### 1.2 Why not the alternatives

- **`double` / `float` inches.** 0.1 has no exact binary representation; sums drift; equality
  becomes a tolerance question everywhere; and a header table's "≤ 6 ft" row compared against
  72.000000001 gives a false *out of scope* — the exact class of silent error DESIGN.md exists to
  prevent.
- **Integer millimetres.** Ruled out in issue #4: 6′-0″ becomes 1829 mm = 72.008″.
- **`decimal`.** Exact for every binary fraction (they all terminate in decimal), but it hides
  rounding behind 28 digits: 1/3 × 3 is 0.999…9, not 1, and equality quietly fails. Fixed-point
  integers make every rounding a visible call.
- **A finer or coarser power of two.** 1/64″ (the finest tape mark) was the runner-up: storage
  equals the finest display precision, so nothing is ever hidden below the display. It loses
  exact midpoints of 1/64″ values and leaves no headroom for solver rounding. 1/1024″ keeps both;
  the trade is that two lengths can differ by an amount no display precision shows, which is
  why equality of *values* is never how a relationship is enforced (§3). This is a "Decision for
  Marc" (§9) only because it is a preference; the recommendation is 1/1024″.

### 1.3 Arithmetic

All arithmetic is in `checked` context; an overflow throws `OverflowException` and is treated as a
programming error, not a user-facing state.

| Operation | Signature | Result |
|---|---|---|
| add, subtract, negate | `Length + Length`, `-Length` | exact |
| multiply by integer | `Length * long` | exact |
| divide by integer | `Length.Divide(long divisor, Rounding)` | rounded, see §1.4 |
| exact divide | `Length.TryDivideExact(long divisor, out Length)` | `false` if not on the grid |
| scale by a ratio | `Length.Scale(long numerator, long denominator, Rounding)` | rounded |
| ratio of lengths | `Length / Length` → `double` | for display and proportion only |
| product of lengths | `Area.Of(Length, Length)` → `Int128` units² | exact; see below |
| compare | `<`, `<=`, `==`, `CompareTo`, `Min`, `Max`, `Abs` | exact |

There is deliberately no `Length * double` operator. Anything that needs a double (a solver, a
rendering transform, a board-foot total) converts with `Length.ToInches()` → `double` and comes
back only through §1.4.

`Area` exists so that a takeoff (board feet = L × W × T / 144) is computed exactly and rounded
once at the end; `Int128` (available since .NET 7) holds the product of any two lengths below
2⁶³ units. Area is a small helper, not a geometry type; it is not needed for #5's core and may be
added by #9.

### 1.4 Rounding

Rounding happens in exactly four places, each explicit:

1. `Length.Divide` / `Length.Scale` — rounding mode is a required argument.
2. `Length.FromInches(double, Rounding)` — the only entry from `double`, used by the solver boundary
   (§5) and by decimal input.
3. `Length.Parse` — reports whether the parsed value was on the grid (`WasRounded`).
4. `Length.Format` — reports whether the displayed value equals the stored one (`IsExact`).

Two rounding rules, each for one purpose:

- **Internal arithmetic: round half to even** (`Rounding.HalfToEven`). Unbiased under repeated
  computation; it is what the solver boundary and `Scale` use.
- **Display: round half away from zero** (`Rounding.HalfAwayFromZero`). It is how a tape measure
  is read: 1/32″ shown at 1/16″ precision reads as 1/16″, not 0.

Nothing rounds silently. `Format` returns a `FormattedLength { Text, IsExact }` and the canvas
(#11) marks inexact display, for example with a leading `≈`, so a cut list never shows "10″" for a
part that is 10 1/1024″ without saying so. In practice the direct updater never produces
off-grid values from on-grid input, so `≈` appears only after decimal entry or a solver run.

### 1.5 Parsing and display stay out of storage

Feet-inch-fraction text is a *view* of a `Length`, never its representation. Both directions are
pure functions in `Core.Geometry` (no UI dependency; #11 only calls them):

```csharp
public static bool TryParse(string text, out Length value, out bool wasRounded);
// Accepts: 6'-3 5/16"   6' 3-5/16"   6ft 3in   75 5/16   75.3125   3/4   -1/2
// Rejects: units it does not know (mm, cm), empty, or malformed text.

public static FormattedLength Format(Length value, LengthFormat format);
// LengthFormat: FeetInches | InchesOnly | DecimalInches(digits), with a precision
// denominator in {1, 2, 4, 8, 16, 32, 64}. Fractions are reduced (8/16 → 1/2);
// 12" carries into feet in FeetInches mode; negatives keep their sign.
```

Decimal input is parsed to the nearest unit and flagged `wasRounded` when it was not exactly on
the grid. Metric never enters: per DESIGN.md §10 a future metric layer is display-only, which in
this design is one more `LengthFormat` case producing text from the same `Length` — the stored
value is imperial by construction and no metric threshold is ever compared.

### 1.6 Angles

Rotation is a separate quantity with its own type, not a `double` and not a pair of lengths:

```csharp
/// Integer arcseconds. 90° is 324 000. Normalised to [0, 360°).
public readonly record struct Angle(long Arcseconds)
{
    public static Angle Degrees(long deg, long min = 0, long sec = 0);
    public static readonly Angle Right = Degrees(90);
    public bool IsRightAngleMultiple { get; }        // 0, 90, 180, 270
    public int QuarterTurns { get; }                 // valid only when IsRightAngleMultiple
    public Angle Rotate90(int quarterTurns);         // exact
}
```

Arcseconds because a survey plat gives bearings in degrees-minutes-seconds ("N 45°30′15″ E"),
which the site plan (#21) must store exactly; and because every common miter angle (45°, 30°,
22.5°, 15°, 7.5°, 11.25°) is an exact integer of arcseconds. Half an arcsecond over a ten-foot
part moves an endpoint by about 0.3 units, well below the tolerance in §5.3. Right-angle multiples
are recognised exactly, and rotation by them is done by swapping and negating coordinates, never
by trigonometry — that is what keeps the first beta's rectilinear geometry exact.

## 2. Geometry model

### 2.1 Coordinate system and value types

- Two dimensions. X increases to the right, Y increases *upward* (the CAD, DXF and PDF
  convention); the canvas applies the screen flip in its view transform and nowhere else.
- Plan view for parts and walls; elevation views are a later concern and are not designed here.

```csharp
public readonly record struct Point2(Length X, Length Y);
public readonly record struct Vector2(Length Dx, Length Dy);   // Point2 - Point2
```

Rotation of a `Vector2` by an `Angle` that is a right-angle multiple is exact. Rotation by any
other angle goes through `double` and returns through `Length.FromInches(…, HalfToEven)`; it is
only reachable from the solver path (§5).

### 2.2 Entity identity

```csharp
public readonly record struct EntityId(Guid Value)
{
    public static EntityId New() => new(Guid.CreateVersion7());   // .NET 9+ API (unverified
                                                                  // by build; Guid.NewGuid() is
                                                                  // an acceptable fallback)
}
```

Ids are stable for the life of an entity: they survive save/load (#6), undo/redo (#11), and they
are what relationships and dimensions refer to. A GUID rather than a per-file counter so that
copy/paste between projects never collides and so a file never needs a "next id" field. Version 7
only for locality when sorting; nothing depends on it.

### 2.3 Entities

The first beta's objects — furniture parts, and later walls and openings — are rectangles in
plan view. The model has four entity kinds; all are immutable records.

```csharp
public abstract record Entity(EntityId Id, LayerId Layer);

/// A free point. Used for construction geometry and as the endpoints of segments.
public sealed record Node(EntityId Id, LayerId Layer, Point2 Position) : Entity(Id, Layer);

/// A straight segment between two nodes. Construction lines, wall centerlines later.
public sealed record Segment(EntityId Id, LayerId Layer, EntityId Start, EntityId End)
    : Entity(Id, Layer);

/// A rectangle defined by its parameters, not its corners. Furniture parts, walls, openings.
public sealed record Box(
    EntityId Id, LayerId Layer,
    Point2 Anchor,          // the local origin corner (south-west in local frame)
    Length Width,           // along local X; must be > 0
    Length Height,          // along local Y; must be > 0
    Angle Rotation)         // about Anchor; first beta: right-angle multiples only
    : Entity(Id, Layer)
{
    public Point2 Corner(BoxCorner which);   // derived; exact when Rotation is a right-angle multiple
    public Point2 Center { get; }            // derived; may round by half a unit
}

/// An annotation that measures something, drawn on the canvas. See §3.3 for how it relates to
/// driving relationships.
public sealed record Dimension(
    EntityId Id, LayerId Layer,
    Measurand Measures,     // what it measures, §3.3
    RelationshipId? Drives, // null for a reference dimension
    DimensionPlacement Placement)   // offset and side, canvas-only data
    : Entity(Id, Layer);
```

**Parametric, not corner-based, on purpose.** A `Box` stores what the user typed — its width and
height — and derives its corners. The cut list (#8) reads `Width` and `Height`, so a 10″ part is
10″ even when it is rotated 37° and its rounded corners are 10.0004″ apart. Corners of
right-angle-rotated boxes are exact sums and never round.

**Walls and openings** are boxes. A wall in plan view is a `Box` whose `Width` is its length and
`Height` its thickness, rotation restricted to right-angle multiples in the first beta. An opening
is a `Box` related to its wall: `Flush` on the wall's two long edges, and an `AxisDistance` from a
wall corner fixing where it sits along the wall. The building module (#18) adds its attributes
(what the wall supports, hazard inputs) on its own types that *reference* the box by id; the
geometry kernel knows nothing about headers. Doors, windows and sliders are openings with a kind.

**Arcs and circles** are not entities in the first beta: no phase-1 object needs one. The
`Entity` hierarchy is open to them, and the solver workstream (#28) will add `Arc` when angled or
curved furniture arrives. Designing them now would be guessing.

**Layers** are an attribute of every entity (`LayerId`), defined on the sketch; #11 owns the UI.

### 2.4 The sketch is a value

```csharp
public sealed record Sketch(
    ImmutableDictionary<EntityId, Entity> Entities,
    ImmutableDictionary<RelationshipId, Relationship> Relationships,
    ImmutableList<Layer> Layers)
{
    public static readonly Sketch Empty;
    public Sketch With(...);                       // structural-sharing updates
    public ValidationResult Validate();            // referential integrity, §2.5
}
```

The sketch is immutable. Every update produces a new `Sketch`; the previous one is untouched.
This decides three things at once:

- **Undo/redo (#11) is a stack of `Sketch` values.** Undo restores the previous value; ids are
  stable, so selection and canvas visuals map straight across. There are no inverse commands to
  write and nothing to keep in sync.
- **The update interface is a pure function** `(Sketch, Request) → UpdateResult`, so both the
  direct updater and a solver are testable without a UI, and property tests (§7) can generate
  sketches freely.
- **The solver works on a copy by construction.** Its failure modes cannot leave the model
  half-updated; either a new sketch comes back or the old one stands.

Sketch-scale data (tens to low hundreds of entities) makes the copying cost irrelevant;
`ImmutableDictionary` shares structure anyway.

### 2.5 Invariants

A `Sketch` that fails any of these is invalid, and the updater must never return one:

1. Every `EntityId` and `RelationshipId` referenced by a segment, dimension or relationship exists.
2. Every `Box` has `Width > 0` and `Height > 0`.
3. Every relationship holds — exactly for the exact class, within tolerance for the tolerance
   class (§3.2, §5.3).
4. No two relationships are structurally identical (same kind, same references).

`Sketch.Validate()` checks 1, 2 and 4 cheaply; `RelationshipChecker.Check(sketch)` (§3.4)
checks 3 and is what tests and the solver boundary use.

## 3. Relationships as data

### 3.1 References

Relationships never point at coordinates; they point at *what* on *which entity*:

```csharp
public abstract record PointRef;
public sealed record NodeRef(EntityId Node) : PointRef;
public sealed record CornerRef(EntityId Box, BoxCorner Corner) : PointRef;
public sealed record CenterRef(EntityId Box) : PointRef;

public abstract record EdgeRef;
public sealed record SegmentRef(EntityId Segment) : EdgeRef;
public sealed record BoxEdgeRef(EntityId Box, BoxEdge Edge) : EdgeRef;   // N/S/E/W in local frame

public abstract record ParamRef;
public sealed record BoxWidthRef(EntityId Box) : ParamRef;
public sealed record BoxHeightRef(EntityId Box) : ParamRef;
public sealed record SegmentLengthRef(EntityId Segment) : ParamRef;
```

### 3.2 The relationship set

```csharp
public readonly record struct RelationshipId(Guid Value);
public abstract record Relationship(RelationshipId Id);
```

**Handled by the direct updater in the first beta** (the rectilinear set). "Exact" means the
relationship is enforced by integer copying or addition and holds exactly after any update.

| Relationship | Meaning | Class |
|---|---|---|
| `Anchored(EntityId)` | The entity does not move or resize in response to other entities. | exact |
| `Coincident(PointRef, PointRef)` | Two points are the same point. | exact |
| `Horizontal(EdgeRef)`, `Vertical(EdgeRef)` | Segment is axis-aligned. (Box edges are axis-aligned by rotation; this is for segments.) | exact |
| `Flush(EdgeRef, EdgeRef)` | Two parallel axis-aligned edges lie on the same line (a shelf flush with a side). | exact |
| `AxisDistance(PointRef, PointRef, Axis, Length)` | Signed distance between two points along X or Y. This is what a driving linear dimension between two points is. | exact |
| `ParamValue(ParamRef, Length)` | A box width/height or segment length is a given value. What a driving dimension on a part's size is. | exact |
| `EqualParam(ParamRef, ParamRef)` | Two sizes are equal (four identical legs). | exact |
| `Centered(PointRef, PointRef, PointRef, Axis)` | The first point is midway between the other two along an axis. | exact when the span is even in units; otherwise the midpoint rounds by half a unit and the relationship is checked to that half unit — the only tolerance the direct updater ever needs |

**Reserved for the solver** (#28). The kinds exist as records from #5 so that the file format and
the UI have names for them, but the direct updater returns `Rejected(UnsupportedRelationship)`
for any request that adds one:

| Relationship | Meaning |
|---|---|
| `Parallel(EdgeRef, EdgeRef)`, `Perpendicular(EdgeRef, EdgeRef)` | Between edges that are not both axis-aligned. |
| `AngleBetween(EdgeRef, EdgeRef, Angle)` | A miter. |
| `Distance(PointRef, PointRef, Length)` | Euclidean, not along an axis. |
| `PointOnEdge(PointRef, EdgeRef)` | |
| `Symmetric(PointRef, PointRef, EdgeRef)` | |
| `Tangent`, `Radius` | With arcs, when arcs exist. |

Two rules about what is *not* a relationship:

- Rectilinearity is not a relationship; it is a property of `Box.Rotation`. A box rotated by a
  right-angle multiple has axis-aligned edges by construction.
- Relationships are stored, never inferred from position. Two corners at the same coordinates are
  not coincident unless a `Coincident` says so; two equal widths are not `EqualParam` unless one
  says so. The canvas (#10) creates relationships when the user snaps, and shows them.

### 3.3 Dimensions and relationships: one number, one owner

A `Dimension` is an annotation. It measures a `Measurand` — a `ParamRef`, or a pair of
`PointRef`s with an axis — and draws itself. Its *value* is always computed from the geometry it
measures; a dimension never stores a length of its own.

A **driving** dimension has `Drives` set to the id of a `ParamValue` or `AxisDistance`
relationship over the same measurand. That relationship owns the number. "Resize by typing a
dimension" is therefore `SetParameter(relationshipId, newLength)` — a request against the
relationship, which the updater satisfies or reports as conflicting (§4). A **reference**
dimension has `Drives == null`; editing its value is `Rejected(ReferenceDimension)`, and the canvas
offers to make it driving, which is `AddRelationship` plus setting `Drives`.

Consequences: #6 serializes one number, in the relationship; #10 draws dimensions from geometry
and never caches a value; deleting a driving relationship turns its dimension into a reference
dimension rather than deleting it.

### 3.4 Checking

```csharp
public static class RelationshipChecker
{
    /// Evaluates every relationship against the sketch's current geometry.
    public static CheckReport Check(Sketch sketch, Tolerances tolerances);
}
public sealed record CheckReport(ImmutableList<Violation> Violations)
{
    public bool AllHold => Violations.IsEmpty;
}
public sealed record Violation(RelationshipId Relationship, Length Residual, bool Exact);
```

One checker, shared by the direct updater's tests, the solver boundary (§5) and the property
tests (§7). It is the definition of invariant 3 in §2.5. Exact-class relationships are checked
with zero tolerance regardless of the `Tolerances` passed.

## 4. The update interface

### 4.1 Requests

```csharp
public abstract record Request;

// Structure
public sealed record AddEntity(Entity Entity) : Request;
public sealed record RemoveEntity(EntityId Id) : Request;            // cascades, see §4.4
public sealed record AddRelationship(Relationship Relationship) : Request;
public sealed record RemoveRelationship(RelationshipId Id) : Request;
public sealed record SetLayer(EntityId Id, LayerId Layer) : Request;

// Geometry — exact: satisfied exactly or reported as conflicting
public sealed record SetParameter(RelationshipId Driving, Length Value) : Request; // dimension edit
public sealed record SetPosition(EntityId Id, Point2 Anchor) : Request;            // typed coordinates
public sealed record SetRotation(EntityId Box, Angle Rotation) : Request;

// Geometry — best effort: ends as near the target as relationships allow
public sealed record Drag(EntityId Id, Vector2 Delta) : Request;                   // move
public sealed record DragEdge(EntityId Box, BoxEdge Edge, Length Delta) : Request; // handle-resize

public sealed record Batch(ImmutableList<Request> Requests) : Request;             // all or nothing
```

Two cases the canvas (#10) needs that follow from "relationships are stored, never inferred":

- **Typing a size on a box that nothing drives** is a stated fact, so it creates the
  `ParamValue` — `AddRelationship(ParamValue(BoxWidthRef b, v))` — rather than poking the box's
  field. It does not create a `Dimension` annotation; the canvas may offer one. If a `ParamValue`
  already drives that size, the same edit is `SetParameter` on it.
- **Dragging a resize handle** is `DragEdge`: best-effort, moves that one edge (the box's size
  changes and, if the edge dragged is the anchor side, so does the anchor) as far as
  relationships allow, and reports the applied delta. If a `ParamValue` drives that size the
  request is `Rejected(DrivenSize)` and the canvas points at the dimension to edit instead — a
  drag never silently overrides a number the user typed.

Two semantics, chosen to match what the user meant:

- **Exact requests** — typing a dimension, adding a relationship — either produce a sketch in
  which the request holds exactly, or `OverConstrained`. Typing "30" and getting 29 15/16 is
  never acceptable.
- **Best-effort requests** — dragging — move the entity as close to the target as its
  relationships allow and report the delta actually applied in the change set. A part flush
  against an anchored one slides along the free axis instead of refusing to move. This is the
  same operation a solver calls "drag" (minimum-movement from the current state), so the seam
  holds for both implementations.

`Batch` is atomic: it is how the canvas commits a multi-step edit as one undo step, and how the
solver boundary applies a whole solution.

### 4.2 Results

```csharp
public abstract record UpdateResult;

/// The request was applied; every relationship holds (invariant 3, §2.5).
public abstract record Succeeded(Sketch Sketch, ChangeSet Changes) : UpdateResult;

/// Applied; every relationship holds; and the updater either analysed freedom and found none
/// remaining, or did not analyse freedom at all (the direct updater never does).
public sealed record Solved(Sketch Sketch, ChangeSet Changes) : Succeeded(Sketch, Changes);

/// Applied; every relationship holds; and the updater analysed freedom and found that some
/// entities could still move without breaking any relationship. This is the NORMAL state of a
/// drag-and-drop drawing, not a failure. The UI shows it, at most, as a hint ("this part is not
/// pinned to anything").
public sealed record UnderConstrained(Sketch Sketch, ChangeSet Changes, FreedomReport Freedom)
    : Succeeded(Sketch, Changes);

/// No geometry satisfies the request together with the existing relationships. The sketch is
/// unchanged. The report names what conflicts, in terms a non-CAD user can act on.
public sealed record OverConstrained(ConflictReport Conflict) : UpdateResult;

/// The request itself cannot be interpreted by this updater — not a geometric state. The sketch
/// is unchanged.
public sealed record Rejected(RejectionReason Reason) : UpdateResult;
```

```csharp
public sealed record ChangeSet(
    ImmutableHashSet<EntityId> Added, ImmutableHashSet<EntityId> Removed,
    ImmutableHashSet<EntityId> Moved, ImmutableHashSet<EntityId> Resized,
    ImmutableHashSet<RelationshipId> RelationshipsAdded,
    ImmutableHashSet<RelationshipId> RelationshipsRemoved,
    Vector2? AppliedDelta);            // for Drag: what actually happened

public sealed record FreedomReport(ImmutableHashSet<EntityId> FreeEntities, int FreeDegrees);
// The direct updater never produces this: it does no degree-of-freedom analysis, so a success
// from it is always Solved — which, by Solved's definition above, claims nothing about freedom.
// That is DESIGN.md §11's "the direct updater only ever returns solved", made precise.

public enum ConflictKind { Contradictory, NoRepresentableSolution, NotConverged }

public sealed record ConflictReport(
    ConflictKind Kind,
    ImmutableList<RelationshipId> Relationships,  // the smallest set found that cannot all hold
    ImmutableList<EntityId> Entities,             // the entities those relationships touch
    ImmutableList<Derivation> Derivations,        // how each side of the contradiction was reached
    string Summary);                              // "Leg A's width is set to 30″ by dimension D1
                                                  //  but equal to Leg B's width, anchored at 28″"

/// What an assignment landed on: a size (ParamRef) or one axis of a point (PointRef + Axis).
public abstract record AssignmentTarget;
public sealed record ParamTarget(ParamRef Param) : AssignmentTarget;
public sealed record PointAxisTarget(PointRef Point, Axis Axis) : AssignmentTarget;

public sealed record Derivation(
    AssignmentTarget Target, Length Value, ImmutableList<RelationshipId> Via);

public enum RejectionReason
{
    UnknownEntity, UnknownRelationship, UnsupportedRelationship, NonPositiveSize,
    ReferenceDimension, DuplicateRelationship, DanglingReference, RotationNotSupported,
    RotationWithRelationships, DrivenSize
}
```

**On the four result types.** DESIGN.md §11 asks for three states — solved, under-constrained,
over-constrained. This design keeps those three as the *geometric* states and adds `Rejected`
for requests that are not about geometry at all (an unknown id, a zero-width box, a relationship
kind this updater does not implement). Folding those into `OverConstrained` would make the
conflict report lie; letting them throw would make the UI's error path an exception handler.
Both `Succeeded` cases carry a valid sketch, so a caller that only wants the sketch matches on
`Succeeded`. This refinement is listed in §9 for Marc's confirmation and reflected in DESIGN.md
§11.

### 4.3 The interface

```csharp
public interface IGeometryUpdater
{
    /// Which relationship kinds AddRelationship will accept. The canvas only offers these.
    ImmutableHashSet<Type> SupportedRelationships { get; }

    /// Pure: never mutates the input sketch. Deterministic: same inputs, same output.
    UpdateResult Apply(Sketch sketch, Request request);
}
```

The direct updater (`DirectUpdater`, in `Napkin.Core.Geometry`) is the first implementation. The
solver (`SolverUpdater`, in `Napkin.Core.Solver`, #28) is the second. Callers — the canvas, the
building module, tests — hold an `IGeometryUpdater` and nothing else. Swapping implementations
changes no caller.

### 4.4 The direct updater's algorithm

The direct updater handles the rectilinear set from §3.2 by **worklist propagation** over the
relationship graph. It is exact, deterministic and small.

**Two components, on purpose.** The propagation engine is its own internal type, `Propagator`,
which takes a sketch, a set of seed assignments and the set of relationship ids to honour, and
returns either the assignment table or a conflict. `DirectUpdater.Apply` is a thin wrapper: it
validates the request, checks the rectilinear precondition below, seeds the `Propagator` with
all exact-class relationships, and writes the result into a new sketch. The solver boundary
(§5.2 step 4) calls the `Propagator` directly, on a rotated sketch, with the exact-class
relationships only — so the `Propagator` itself must *not* check the rotation precondition; the
wrapper does. Without this split, #28's exactness-repair pass would start with a refactor of #5.

**Pre-conditions (checked by `DirectUpdater.Apply`, not by the `Propagator`).** Every box's
rotation is a right-angle multiple; otherwise the sketch is outside this updater's domain and the
request is `Rejected(RotationNotSupported)`. (Such a sketch can only come from a solver-written
file; the app picks the updater that can handle the file.)

**State.** A working table of *assignments*: for each scalar the request can affect — a box's
anchor X, anchor Y, width, height; a node's X, Y — either *unassigned* (keeps its current value
unless something moves it) or *assigned* (a new value plus the derivation that produced it).
Anchored entities' scalars are pre-assigned to their current values with an `Anchored`
derivation, so any attempt to move them is a contradiction with a nameable cause.

**Steps for an exact request** (`SetParameter`, `SetPosition`, `AddRelationship`,
`SetRotation`):

1. Validate the request against the sketch (ids exist, values positive, kind supported). Failure
   → `Rejected`.
2. Seed the worklist with the scalars the request sets directly. For `SetParameter` on a
   `ParamValue(BoxWidthRef b, v)`: assign `b.Width = v`. For `AddRelationship`, seed from the
   relationship's own implication (a `Coincident(p, q)` assigns `q := p` unless `q`'s owner is
   anchored and `p`'s is not, in which case `p := q`).
3. Pop a scalar. For every relationship that mentions its owner (iterated in `RelationshipId`
   order — never dictionary order — so results are reproducible), compute what the relationship
   implies for the other side:
   - `Coincident`, `Flush`, `AxisDistance`: a position on the other entity. A moved corner moves
     the *whole* other entity by the same delta (translation), unless that entity is being
     resized by this same update, in which case the far side of it stays and only the near side
     moves — see the anchor rule below.
   - `EqualParam`: the other parameter takes the same value.
   - `Centered`: the midpoint, rounded half-to-even; or, if the midpoint is what moved, the two
     ends move by the same delta.
4. For each implied assignment: if the target is unassigned, assign it and push it. If assigned
   to the same value, do nothing. If assigned to a different value, **stop**: build a
   `ConflictReport(Contradictory)` from the two derivations (each is a chain of relationship ids
   back to the request or to an `Anchored`), and return `OverConstrained`. The original sketch is
   returned untouched by construction — the working table was never written back.
5. When the worklist empties, write the assignments into a new `Sketch`, run
   `RelationshipChecker.Check` as a debug-mode assertion (it must pass; a failure is a bug, not a
   result), and return `Solved` with the `ChangeSet`.

**The anchor rule for resizing.** When a box's width or height changes, which side moves? The
box's `Anchor` corner stays put and the opposite side moves, *unless* a relationship pins the
opposite side (a `Coincident` or `Flush` to something that is anchored or already assigned) and
nothing pins the anchor side, in which case the anchor moves and the far side stays. If both sides
are pinned, the resize is a contradiction and the report names both pins. This is a fixed,
explainable rule rather than a "least movement" heuristic, and it is what the tests in §7 pin
down.

**`SetRotation`.** Edge and corner references are in the box's local frame, so rotating a box
that has a `Flush`, `Coincident`, `AxisDistance` or `Centered` relationship would turn a
relationship between parallel edges into one between perpendicular edges. Rather than guess
what the user meant, `SetRotation` on a box with any relationship other than `Anchored` or
`ParamValue`/`EqualParam` on its own sizes is `Rejected(RotationWithRelationships)`, naming the
relationships; the canvas offers to remove them first. A box with no such relationships rotates
about its anchor exactly (right-angle multiples only in this updater).

**Steps for a best-effort request** (`Drag(id, delta)`):

1. Compute the rigid group: the entity plus everything reachable from it through `Coincident`,
   `Flush`, `AxisDistance` and `Centered` — the things that move with it.
2. If the group contains an anchored entity, the axis components of `delta` that would move that
   entity are set to zero (a `Flush` on a vertical edge blocks X, not Y; an `AxisDistance` along X
   blocks X). What remains is the applied delta; if it is zero, the result is still `Solved` with
   `AppliedDelta = (0, 0)` — a drag that goes nowhere is not a conflict.
3. Translate the group by the applied delta; write; check; return `Solved`.

`DragEdge(box, edge, delta)` is the same idea for one edge: if a `ParamValue` drives the size that
edge controls, `Rejected(DrivenSize)`; otherwise the edge moves by as much of `delta` as the
relationships on that edge allow (a `Flush` to an anchored box blocks it entirely), the box's size
changes accordingly, and the applied delta is reported.

Dragging is never `OverConstrained` by design: a drag is a question, not a demand.

**Structural requests.** `AddEntity` and `RemoveEntity` are exact and simple. `RemoveEntity`
cascades: relationships that reference the entity are removed; dimensions whose measurand
references it are removed; a dimension whose *driving* relationship was removed but whose
measurand survives becomes a reference dimension. The change set lists everything removed, so
the canvas can say "also removed 2 dimensions". `RemoveRelationship` never moves anything: the
geometry stays where it is, it is simply no longer held there.

**What the direct updater does not do.** It never rounds an on-grid value except the `Centered`
midpoint of an odd span; it never rotates by trigonometry; it never returns `UnderConstrained`;
it never guesses. Anything outside the rectilinear set is `Rejected` with the kind named, so the
canvas can say "napkin can't hold that relationship yet".

## 5. The solver seam, and fixed-point versus double

### 5.1 Where the solver lives

`Napkin.Core.Solver` is its own assembly, referencing `Napkin.Core.Geometry` and nothing else in
the solution (a permissively-licensed linear-algebra package is allowed if it passes the #2 gate;
the matrices here are small enough that hand-written dense QR/LM is also fine). It contains
`SolverUpdater : IGeometryUpdater`. Nothing in `Core.Geometry` references it. The app chooses
which updater to construct. DESIGN.md §6.2's layout gains this project.

This document does *not* design the solver's numerics; that is #28's spike. It designs the
boundary the solver must respect, which is what makes the spike safe to defer.

### 5.2 The boundary: load exactly, solve in double, round once, repair, verify

The solver's variables are the same scalars the direct updater assigns (anchor X/Y, width,
height, rotation, node X/Y), in `double` inches and degrees.

1. **Load.** `Length.ToInches()` and `Angle.ToDegrees()` on every scalar. Exact for every value
   below 2⁵³ units — the solver starts from precisely the stored geometry, so a sketch that
   already satisfies its relationships has zero initial residual and a drag starts from where the
   user sees things.
2. **Solve** in double. Residuals, Jacobian, damped Newton or Levenberg–Marquardt, rank for the
   freedom report — #28's business.
3. **Round once.** Every solved scalar goes through `Length.FromInches(v, HalfToEven)` or
   `Angle.FromDegrees(v, HalfToEven)`. Rounding is the only lossy step and it happens exactly
   once per scalar.
4. **Repair.** The rounded sketch is handed to the `Propagator` (§4.4 — the engine inside the
   direct updater, called directly so the rectilinear precondition does not apply) with an
   empty seed and the exact-class relationships only. This re-derives every integer-copy
   relationship from the rounded values: if the solver set two widths equal to 27.9999″ and
   28.0001″ they rounded to the same unit anyway; if a `Coincident` between two rotated corners
   rounded to points one unit apart, the pass snaps the dependent corner onto the other; a
   `Perpendicular` between two rotated boxes becomes `rotationB := rotationA + 90°` exactly. The
   repair pass runs *once*. If it hits a contradiction, the result is
   `OverConstrained(NoRepresentableSolution)` — no second attempt, no nudging.
5. **Verify** on the repaired sketch with `RelationshipChecker.Check`. Exact-class relationships
   must hold exactly (the repair pass guarantees this, and the check is the proof); tolerance-
   class relationships must be within §5.3's tolerances. Any violation →
   `OverConstrained(NoRepresentableSolution)` with the violating relationships named. The
   solution is never silently accepted.
6. **Return** `Solved` or `UnderConstrained` (from the rank analysis) with the repaired sketch.

Order matters and is fixed: verification runs on the *repaired* sketch, because repair moves
things. A solver that has not converged reports `OverConstrained(NotConverged)`, which the UI
distinguishes from `Contradictory` ("these dimensions can't all be true" versus "napkin couldn't
find a shape for this — try changing one dimension at a time").

### 5.3 What "satisfied" means after rounding: the tolerance policy

Two classes of relationship, decided by what enforcing them costs in a fixed-point world:

- **Exact class** (§3.2's first table, plus `Parallel`/`Perpendicular`/`AngleBetween` when
  *both* edges are `BoxEdgeRef`s — their directions are stored `Angle`s the repair pass can copy;
  with any `SegmentRef` involved the direction comes from two node positions and the relationship
  is tolerance class): every one is a copy, a sum, or an exact rotation of integers. They
  hold exactly after the repair pass, and the checker uses zero tolerance for them. This is the
  class the first beta uses exclusively, so the first beta has no tolerance anywhere except the
  half-unit `Centered` case.
- **Tolerance class** (`Distance` between points on rotated geometry, `PointOnEdge`, `Symmetric`,
  `Tangent`, `Radius`, and `Coincident` when either corner is on a non-right-angle rotation):
  these involve an irrational quantity and cannot hold exactly on any grid. They are satisfied
  when the residual is within:

  | Tolerance | Value | Derivation |
  |---|---|---|
  | `Tolerances.Position` | 4 units (1/256″) | Each rounded coordinate is within ½ unit; a rotated corner adds ½·(|cos θ| + |sin θ|) ≤ 0.71 units from width/height rounding and ≤ 0.3 units from angle rounding over 10 ft; the difference of two such corners is within ~3 units. 4 is the next power of two. |
  | `Tolerances.Angle` | 2 arcseconds | Two independently rounded rotations. |

  Both are constants in one place (`Tolerances.Default`), with a test that recomputes the
  positional bound from `Length.UnitsPerInch` so a change to the grid cannot leave the tolerance
  stale. They are far below display precision: 1/256″ is a quarter of the finest tape mark.

A relationship's class is a property of the relationship *kind and the rotations involved*, not
of the updater: `RelationshipChecker` decides it, so the direct updater, the solver and the tests
agree on what "holds" means.

### 5.4 What the solver may not do

- Return a sketch it has not verified through the checker.
- Round more than once per scalar.
- Write a value into a `Length` by any route other than `Length.FromInches(…, HalfToEven)`.
- Accept an under-constrained sketch as a failure. It is a success with a freedom report.
- Mutate the input sketch.

## 6. Serialization implications for #6

`scene.json` stores the sketch; `manifest.json` stores what is needed to refuse it.

```json
// manifest.json
{
  "formatVersion": 1,
  "appVersion": "0.N.0-beta",
  "units": { "length": "inch/1024", "angle": "arcsecond" },
  "adoptedCode": null
}
```

```json
// scene.json (excerpt)
{
  "layers": [ { "id": "…", "name": "Default" } ],
  "entities": [
    { "id": "0192…", "type": "box", "layer": "…",
      "anchor": { "x": 0, "y": 0 }, "width": 30720, "height": 3584, "rotation": 0 },
    { "id": "0192…", "type": "node", "layer": "…", "position": { "x": 1024, "y": 2048 } },
    { "id": "0192…", "type": "dimension", "layer": "…",
      "measures": { "kind": "boxWidth", "box": "0192…" },
      "drives": "0192…",
      "placement": { "offset": 8192, "side": "north" } }
  ],
  "relationships": [
    { "id": "0192…", "kind": "paramValue",
      "param": { "kind": "boxWidth", "box": "0192…" }, "value": 30720 },
    { "id": "0192…", "kind": "anchored", "entity": "0192…" },
    { "id": "0192…", "kind": "flush",
      "a": { "box": "0192…", "edge": "north" }, "b": { "box": "0192…", "edge": "south" } }
  ]
}
```

- **Lengths and angles are integers in stored units**, with the unit named once in the manifest.
  Exact, compact, and readable by anyone with the format document. Decimal strings were
  considered (every unit value terminates in decimal) and rejected as ugly and inviting parsers to
  use `double`.
- **Ids are GUID strings.** Relationships reference entities by id; dimensions reference
  relationships by id. Referential integrity is checked on load with `Sketch.Validate()`; a
  dangling reference is a load failure, not a repair.
- **`formatVersion` is an integer, bumped on every change to what the file means.** The loader
  accepts exactly the version the app writes. Anything else — older or newer — fails with
  `UnsupportedFormatVersion(found, supported)` and a message that names both, before any of the
  scene is parsed. There is no migration code, per the beta policy; unknown fields in a file with
  the right version are also a load failure (strict), because with the version pinned there is no
  legitimate reason for them.
- **Loading re-checks relationships.** After `Validate()`, `RelationshipChecker.Check` runs; a
  file whose geometry does not satisfy its own relationships (it was written by a buggy build) is
  refused with the violations listed, never opened "approximately".
- **Round trip is an identity.** `Load(Save(sketch))` must equal `sketch` by value — that is the
  #6 acceptance test, and immutability plus record equality make it a one-line assertion.
- Reserved-for-solver relationship kinds serialize by the same scheme when they exist; a file
  containing one is loadable only by an app with the solver; the direct-updater-only app refuses
  it with `UnsupportedRelationship` naming the kind, not a crash.

## 7. Test plan for #5

### 7.1 Golden cases

`Length`, `Angle`, parsing and formatting:

- The issue-#4 table: 1/16″, 3½″, 6′-0″ construct, read back and compare exactly; `6′-0″ <=
  6′-0″` is true (the header-span threshold that integer mm broke).
- 23/32″, 15/32″, 5/4 decking (= 1″), 1½″ × 3½″, 16″ and 24″ on-centre.
- Parse/format round trips for every syntax in §1.5; `8/16 → 1/2`; `12″ → 1′-0″`; `-3/4″`;
  `0 → 0″`; `wasRounded` true for `1/3` and `3.505`, false for `3.5`.
- Format at 1/16″ of a value at 1/32″: `HalfAwayFromZero` gives 1/16″ and `IsExact = false`.
- `Divide(3)` of 1″ rounds to 341 units; `TryDivideExact(3)` is false; `TryDivideExact(4)` gives
  1/4″.
- Checked overflow throws.
- `Angle.Degrees(22, 30) == Angle.Degrees(22) + Angle.Degrees(0, 30)`; `Right * 3` is a
  right-angle multiple with `QuarterTurns == 3`; `Degrees(45)` is not.

Direct updater (each is a small sketch, a request, and an exact expected sketch or report):

1. Resize a lone box by `SetParameter` on its width: anchor stays, far corner moves.
2. Box B `Flush` on A's east edge; resize A: B translates by the width delta; B's size unchanged.
3. Same, but B is `Anchored`: A's anchor moves west, its east edge stays (the anchor rule).
4. Same, but A is also `Anchored`: `OverConstrained(Contradictory)` naming both `Anchored`
   relationships and the `Flush`; sketch unchanged.
5. `EqualParam(A.width, B.width)`; set A's width: B's width follows. Then add
   `ParamValue(B.width, other)`: `OverConstrained` naming `EqualParam`, both `ParamValue`s.
6. `Centered` with an odd span: midpoint rounds half-to-even; checker reports it holds within
   half a unit; with an even span it holds exactly.
7. `Drag` a box flush against an anchored box: applied delta has zero X, full Y.
8. `Drag` a box coincident to an anchored node on both axes: applied delta is zero; result is
   `Solved`, not `OverConstrained`.
9. `RemoveEntity` of a box with a driving dimension and a `Flush`: both relationships gone, the
   dimension gone, change set lists all three.
10. `RemoveRelationship` of a `ParamValue` under a dimension: dimension survives with
    `Drives == null`; geometry unchanged.
11. `AddRelationship(Parallel(...))`: `Rejected(UnsupportedRelationship)`.
12. `SetParameter` on a reference dimension's measurand with no relationship: `Rejected`.
13. A wall box with an opening box (`Flush` × 2 + `AxisDistance`): resize the opening's width —
    the opening's anchor stays, the wall does not move; change the `AxisDistance` — the opening
    slides; `Drag` the opening along the wall — Y component zeroed.
14. `Batch` of two requests where the second conflicts: nothing applied, report from the second.

### 7.2 Properties

Generated with a property-testing library that passes the #2 license gate (FsCheck is the usual
choice — BSD-3-Clause, unverified — CsCheck is an alternative, licence unverified; a hand-rolled
seeded generator under xunit `[Theory]` is acceptable if neither passes). The generator produces
valid rectilinear sketches: 1–20 boxes and nodes, 0–30 relationships drawn from §3.2's first
table, constructed so that the initial sketch satisfies them (build relationships by *deriving*
positions, never by sampling positions and hoping).

- **P1 — Never inconsistent.** For every valid sketch S and every request R: `Apply(S, R)` is
  `Rejected` or `OverConstrained` with S returned unchanged, or `Succeeded` with a sketch that
  passes `Sketch.Validate()` and `RelationshipChecker.Check` with no violations. This is
  DESIGN.md §6.5's "a dimension change should never silently produce a geometrically inconsistent
  state", stated as a test.
- **P2 — Exact requests are exact.** If `SetParameter(rel, v)` succeeds, the relationship's
  measurand equals `v` in the new sketch, exactly.
- **P3 — Purity and determinism.** `Apply(S, R)` called twice gives structurally equal results,
  and S is unchanged after the call. Shuffling the insertion order of S's dictionaries does not
  change the result (this is the "iterate by id, not by dictionary order" rule).
- **P4 — Locality.** Entities not connected to R's target through the relationship graph are
  identical in the result.
- **P5 — Idempotence.** Applying the same `SetParameter` to its own result is a no-op with an
  empty change set.
- **P6 — Drag is best-effort and never worse.** After `Drag(id, d)`, `AppliedDelta` has each
  component equal to `d`'s or zero, and the entity moved by exactly `AppliedDelta`.
- **P7 — Conflict reports are actionable.** Every `OverConstrained` names at least two
  relationships (or one relationship and the request), every id named exists in S, and removing
  all the named relationships from S makes the same request succeed. The last clause is the one
  that keeps the report honest.
- **P8 — Length algebra.** `a + b - b == a`; `(a * n).TryDivideExact(n) == a`; `Scale` is within
  half a unit of the real quotient; `Parse(Format(a, precision: 1/1024))` round trips for all `a`.
- **P9 — Serialization (for #6).** `Load(Save(S)) == S`.
- **P10 — Undo (for #11).** For any sequence of successful requests, the stack of sketches
  restores each earlier value exactly by reference equality of its entities where unchanged.

### 7.3 What is not tested here

Solver numerics (#28), canvas behaviour (#10), file container details beyond round trip (#6).

## 8. Implementation plan for #5

For an Opus-tier implementer. One pull request for the issue is fine, but the steps are ordered
so each leaves the build green and the tests meaningful. Everything lives in
`src/Napkin.Core.Geometry` and `tests/Napkin.Core.Geometry.Tests`; zero Avalonia references
(DESIGN.md §6.2). `Class1.cs` placeholders are deleted.

**Step 1 — `Length`, `Rounding`, `Angle`.** Files `Length.cs`, `Rounding.cs`, `Angle.cs`.
Operators, `Divide`/`TryDivideExact`/`Scale`, `FromInches(double, Rounding)`, `ToInches()`,
`IComparable`, `Min`/`Max`/`Abs`. `Angle` normalised to `[0, 360°)` with `IsRightAngleMultiple`,
`QuarterTurns`, `FromDegrees(double, Rounding)`. Tests: the §7.1 golden cases for these types and
P8. Do not add `Area` unless #9 needs it.

**Step 2 — Parsing and formatting.** `LengthFormat.cs`, `LengthParser.cs`, `FormattedLength`.
Support the syntaxes in §1.5; fraction reduction; feet carry; `IsExact`/`wasRounded`. Tests: the
parse/format goldens and the round-trip half of P8.

**Step 3 — Value types and entities.** `Point2.cs`, `Vector2.cs`, `EntityId.cs`, `LayerId.cs`,
`Entity.cs` (`Node`, `Segment`, `Box`, `Dimension`), `BoxCorner`/`BoxEdge` enums,
`Box.Corner(...)` exact for right-angle rotations (swap/negate), rounded via `FromInches` otherwise
(implement, but the direct updater will never call it with a non-right rotation). Tests: corner
derivation for all four rotations; `Center` rounding.

**Step 4 — References and relationships.** `Refs.cs` (`PointRef`, `EdgeRef`, `ParamRef`),
`Relationship.cs` with every kind in §3.2 — including the reserved ones — as sealed records, and
`Measurand`. Tests: structural equality of relationships (needed for invariant 4).

**Step 5 — `Sketch` and `RelationshipChecker`.** `Sketch.cs` with `Empty`, `With…` helpers,
`Validate()` (invariants 1, 2, 4). `RelationshipChecker.cs` with `Tolerances`, class decision per
§5.3, residual per kind (exact class: integer compare; tolerance class: implement for the
reserved kinds too, so the solver inherits it). Tests: a hand-built valid sketch passes; each
invariant violated in turn is reported; the tolerance-derivation test from §5.3.

**Step 6 — Requests and results.** `Request.cs`, `UpdateResult.cs`, `ChangeSet`, `FreedomReport`,
`ConflictReport`, `Derivation`, `RejectionReason`, `IGeometryUpdater`. No logic; tests are the
compile.

**Step 7 — `DirectUpdater`: structure.** `AddEntity`, `RemoveEntity` with cascade, `AddRelationship`
for the supported set (validation and the initial implication only — propagation comes in step
8), `RemoveRelationship` with the dimension demotion, `SetLayer`, `Batch`. Tests: §7.1 cases 9,
10, 11, 12.

**Step 8 — `Propagator`, then `DirectUpdater` propagation.** `Propagator.cs` is the assignment
table and worklist from §4.4 as a standalone internal type: inputs are a sketch, seed
assignments and the relationship ids to honour; output is the assignment table or a conflict.
It records derivations from the start (do not bolt the conflict report on afterwards — it is the
same data structure), iterates relationships sorted by id, and does *not* check box rotations —
`DirectUpdater.Apply` does that before calling it (§4.4, "Two components"). Then `SetParameter`,
`SetPosition`, `SetRotation` (right-angle multiples only, and `Rejected(RotationWithRelationships)`
per §4.4) in `DirectUpdater`, and typed sizes on undriven boxes via `AddRelationship(ParamValue)`.
The anchor rule. Tests: §7.1 cases 1–6, 13, 14, plus a `Propagator`-only test on a sketch with a
45° box that the wrapper would reject, honouring an `EqualParam` — the shape #28 will rely on.

**Step 9 — `DirectUpdater`: drag.** `Drag`: rigid group, blocked axes, applied delta. `DragEdge`:
`Rejected(DrivenSize)` when a `ParamValue` drives that size, else the edge moves as far as
allowed. Tests: cases 7, 8, and a `DragEdge` against a `Flush` to an anchored box (applied delta
zero) and against a driven size (`Rejected`).

**Step 10 — Property tests.** The generator (derive-not-sample), then P1–P7 and P10. P9 waits for
#6. Run with a fixed seed in CI and a larger count locally.

**Step 11 — Documentation.** XML doc comments on every public type, and a
`docs/design/geometry-model.md` update if the implementation had to deviate — the deviation is
recorded here, not left in a commit message.

Definition of done for #5: every golden case and property passes on both CI platforms; the
`Class1.cs` placeholders are gone; `Napkin.Core.Geometry` has no package references beyond the
BCL (the property-testing package is a test-project reference only); Fable review per PLAN.md.

## 9. Decisions for Marc

Short, and only what is genuinely a preference. Everything else above is decided.

1. **Grid: 1/1024″ (recommended) or 1/64″.** 1/1024″ gives exact midpoints, headroom for the
   solver, and thousandth-inch decimal entry; 1/64″ guarantees that storage and the finest display
   coincide, so nothing is ever hidden below the display. Either works with everything else here.
   Recommendation: 1/1024″, with the `IsExact` display flag as the guard against hidden residue.
2. **`Rejected` as a fourth result type** alongside DESIGN.md §11's three (§4.2). Recommendation:
   yes — a bad request is not a geometric state, and the conflict report should never have to
   lie to cover one.
3. **Reference dimensions in the first beta.** The design allows dimensions that only measure
   (`Drives == null`). The canvas could instead make every dimension driving. Recommendation: keep
   reference dimensions; a measurement between two parts you have not decided to pin is exactly
   what a homeowner reaches for a tape to do.
4. **Solver spike time box and gate** (in #28, not here): the recommendation is a bounded spike
   after #5 lands, off the critical path to the first working beta, with the gate criteria in
   #28. Confirm the box, or that the spike waits until furniture with angled parts is actually on
   the roadmap.

## 10. Implementation notes (#5)

Where the implementation had to deviate from the design above, or had to decide something the
design left open. Per §8 step 11 and #5's acceptance criteria, the deviation is recorded here,
not left in a commit message. Written by the Opus implementer of #5; nothing here overrides a
decision above without saying so.

### 10.1 Lengths, angles, parsing and formatting

- **`Angle` declares its `Arcseconds` property explicitly** rather than taking the synthesised
  positional one, so that the constructor normalises into [0°, 360°). The type is still the
  positional `readonly record struct Angle(long Arcseconds)` of §1.6; the property is get-only,
  which means `with` cannot be used to write an un-normalised angle.
- **`Length.Inches(whole, numerator, denominator)` throws** when the fraction does not land on the
  grid (a third of an inch). §1.4 says rounding happens in exactly four places, and this
  constructor is not one of them, so it cannot round silently; `FromInches` and `TryParse` are the
  entry points for values that may need rounding.
- **Format precision accepts 1024, not only §1.5's 1…64.** Property P8 (§7.2) asks for
  `Parse(Format(a, precision: 1/1024))` to round trip for every `a`, which the tape-measure
  precisions cannot express. The denominator is validated as a power of two from 1 to 1024.
- **Parsing rounds half away from zero.** §1.4 names two rounding rules but does not say which one
  `Parse` uses. User input is display-side, so it follows the display rule. Parsing accumulates an
  exact rational and never goes through `double`, so `wasRounded` reports the grid, not floating
  point.
- **`Length.ToString()` renders inches at 1/1024″**, which is always exact, so that a failing
  assertion shows the stored value rather than a rounded one.
- **In feet-inch mode, whole inches are always shown when feet are shown**: `6'-0 5/16"`, not
  `6'-5/16"`. §1.5 does not say, and the drawing convention is to show the zero.

### 10.2 The sketch and the checker

- **`Sketch` has structural equality.** Record equality alone compares `ImmutableDictionary`
  fields by reference, which would make §6's `Load(Save(s)) == s` round-trip assertion — and P3's
  determinism check — quietly false. `Equals` and `GetHashCode` are written by hand to compare
  contents.
- **`Sketch.Empty` carries one layer**, `Layer.Default`, with a well-known id, so that an empty
  sketch is the same value in every process and an entity always has a layer to be on.
- **`Validate()` also checks layers and entity kinds.** §2.5's invariant 1 says "every id
  referenced … exists"; a layer id and the kind of the entity an id names (a `CornerRef` on a
  node) are the same class of referential error, so they are reported too — for every relationship
  kind and for a dimension's measurand, not just for a segment's endpoints. §6's loader validates
  and then checks, so a reference of the wrong kind has to be a load error rather than an
  exception out of the checker. (Fable review of #35, finding 4.)
- **`Centered` follows §3.2's half-unit rule**, and the checker measures a middle point to the
  nearer of the two units the true midpoint of an odd span falls between. An earlier draft of this
  implementation checked against the half-to-even midpoint with zero tolerance, which looked
  stricter and was wrong: it is not translation-invariant, so moving a centred span by an odd
  number of units landed on the other side of the tie and turned an ordinary drag into a thrown
  exception. `ResolveCentered` uses the same test, so the propagator never "corrects" a middle
  that is already good. (Fable review of #35, finding 2.)
- **Angular kinds are judged against `Tolerances.Angle`**, and the `Length` in their `Violation`
  is the positional deviation that angular error produces at the far end of the longer edge.
  §3.4's `Violation` carries only a `Length`, so one number has to be comparable across kinds.
- **`Tangent` and `Radius` exist as records but cannot hold.** §3.2 specifies them "with arcs,
  when arcs exist", and #5 has no arc entity. The checker reports them as violations rather than
  passing over them, so a sketch cannot quietly claim to satisfy a relationship nothing evaluated.
  The solver workstream (#28) gives them meaning when it adds `Arc`.
- **`Flush` between edges that are not both axis-aligned the same way** is tolerance class, with
  the residual measured as the furthest of the second edge's ends from the first edge's line. §3.2
  assumes two parallel axis-aligned edges; this is what the checker does when a solver-written
  sketch does not have them.

### 10.3 The direct updater and the propagator

- **`Anchored` pins position and size, as §3.2 says, and stands aside for the number the user is
  editing.** The emphasis in "does not move or resize *in response to other entities*" is on the
  last clause: the propagator pre-assigns an anchored entity's X, Y, width and height, and skips
  whichever scalars the request is itself setting — the size behind a dimension being typed, and
  for `DragEdge` the anchor corner that a west or south handle necessarily drags with it. So an
  `EqualParam` cannot quietly resize an anchored part, `SetPosition` on one is still a conflict
  naming the anchor, §7.1 case 4 still reports both anchors and the `Flush` (the request's seed
  carries an empty derivation, so the `ParamValue` drops out of it), and all four resize handles
  behave the same way. An earlier draft pinned position only, which made case 4's report work but
  let an `EqualParam` resize an anchored box and made the east handle work while the west one
  silently refused. (Marc's decision on open question (a); Fable review of #35, finding 8.)
- **The propagator runs in two phases**: size relationships (`ParamValue`, `EqualParam`) to a
  fixed point, then positional ones. No positional relationship ever assigns a size, and a corner
  offset depends on the box's size, so a single pass could compute a corner from a size a later
  relationship changes and then read the stale derivation as a contradiction. §4.4's algorithm does
  not say this; it is an implementation requirement of it.
- **The anchor rule is not special-cased.** A constraint moves whichever side is free to move,
  where "free" means none of the position scalars the side is built on has been assigned. §4.4's
  anchor rule, its seeding rule for `AddRelationship(Coincident(p, q))`, and "the *to* point
  follows" for `AxisDistance` all fall out of that one rule plus one tie-break: when neither side
  is driving, the second side follows the first.
- **`RejectionReason.DuplicateEntity` and `RejectionReason.UnsupportedRequest` were added** to
  §4.2's enum. `AddEntity` with an id the sketch already holds is not a dangling reference and not
  a geometric state; `UnsupportedRequest` covers the unreachable arm for a request kind this
  updater does not implement, which `UnsupportedRelationship` described wrongly. `SetPosition` and
  `Drag` against something with no coordinates of its own — a dimension — say `DanglingReference`,
  whose meaning already covers a reference "of the wrong kind". (Fable review of #35, finding 9.)
- **`RejectionReason.ReferenceDimension` is unreachable from §4.1's requests.** §3.3 says editing a
  reference dimension's value is `Rejected(ReferenceDimension)`, but no request in §4.1 identifies a
  dimension: `SetParameter` takes a `RelationshipId`, and a reference dimension has none. The canvas
  therefore cannot form the request at all. The value is kept in the enum because #10 may add a
  request that can reach it. §7.1 case 12 is implemented as `SetParameter` against an id the sketch
  does not have, which is `Rejected(UnknownRelationship)`. The feature catalog's GEO-013 says
  "editing a reference dimension … returns Rejected with the named reason"; that is true of the
  result but not of the reason, and it will stay that way until #10 adds a request that names a
  dimension.
- **`Anchored` requires a box or a node.** Nothing else has a position of its own to hold still,
  and an anchor on a segment or a dimension was accepted and then did nothing at all: the drag
  group never saw it. (Fable review of #35, finding 3.)
- **`Horizontal` and `Vertical` apply to segments only**, as §3.2's own parenthetical says. On a
  `BoxEdgeRef` they are check-only — the checker evaluates them geometrically — and the direct
  updater refuses a sketch containing one, because a box edge's direction is a property of
  `Box.Rotation` and not something to propagate.
- **`ParamValue` and `EqualParam` over a `SegmentLengthRef` are not propagated.** A segment's
  length is not one number the updater can assign; which of its two nodes should move is a question
  §4.4 does not answer. The checker still evaluates them, so the file format and #28 keep them.
  The direct updater refuses a sketch that contains one, the way §6 says it refuses a file holding a
  relationship kind it cannot handle.
- **Segment-based relationships stay check-only in the first beta**, confirmed as Marc's decision
  on open question (c). `ParamValue`/`EqualParam` over a `SegmentLengthRef`, and
  `Horizontal`/`Vertical` on a `BoxEdgeRef`, are evaluated by the checker but never propagated,
  and the direct updater refuses a sketch that contains one. The rule that would match the rest of
  the design, when segment lengths are wanted, is: for an axis-aligned segment the end follows the
  start along the axis, as `AxisDistance` does; a diagonal length is Euclidean and belongs to the
  solver.
- **`IGeometryUpdater.SupportedRelationships` is a set of `Type`s, but actual support is by kind
  *and reference kind*.** A canvas that "only offers these" (§4.3) will offer `ParamValue` on a
  segment length, or `Horizontal` on a box edge, and get `Rejected(UnsupportedRelationship)`. The
  API is left as §4.3 specifies; #10 needs to know that the set is an upper bound rather than an
  exact answer. (Fable review of #35, finding 10.)
- **The precondition applies to geometry requests only.** §4.4 states it for `Apply`, but a sketch
  holding a kind the updater cannot propagate would then be a dead end: the user could not remove
  the offending relationship. `AddEntity`, `RemoveEntity`, `RemoveRelationship` and `SetLayer` move
  nothing, so they are allowed on any sketch.
- **`Drag`'s rigid group includes `Horizontal` and `Vertical`.** §4.4's list of coupling kinds omits
  them, but they tie a segment's two nodes together along one axis just as firmly, and a drag that
  ignored them would leave the relationship broken.
- **`DragEdge` is all-or-nothing, and positive means outward.** §4.1 says the edge moves "as much of
  the delta as the relationships allow"; in the rectilinear set an edge is either free or pinned, so
  there is no partial case to find, and §4.4's own example ("a `Flush` to an anchored box blocks it
  entirely") is the whole story. The sign convention — positive grows the box — is not stated in the
  design; this is the choice.
- **The post-write check throws rather than asserting.** §4.4 step 5 calls for a debug-mode
  assertion. On .NET a failed `Debug.Assert` with no debugger attached fail-fasts the process, which
  would kill the test host and leave CI with an opaque log. `DirectUpdater` throws an
  `InvalidOperationException` listing the violations instead, in every configuration. It is still a
  bug, not a result.
- **`Apply` assumes its input sketch already satisfies its own relationships** (invariant 3). It
  checks referential integrity, rotations and supported kinds, because those decide whether the
  request is in its domain, but it does not re-run the checker on the input: that is the loader's
  job (§6).
- **`ChangeSet` gains a `Modified` set**, beyond §4.2's `Added`, `Removed`, `Moved` and `Resized`,
  for what is none of those: a layer change, a rotation about the anchor, and a dimension demoted
  to a reference dimension. #10 and #11 are the consumers, #5 owns the type, and a canvas that
  could not tell a dimension had stopped driving would draw it wrong. (Marc's decision on open
  question (b).)

### 10.4 The test plan

- **§7.1 case 13's drag clause contradicts §4.4 and was resolved toward §4.4.** Case 13 expects
  dragging the opening along its wall to zero the Y component and apply X. But the opening is fixed
  along the wall by an `AxisDistance` on X, and §4.4's rule is that an `AxisDistance` along X blocks
  X; the two `Flush` relationships on the wall's long edges block Y. So the applied delta is (0, 0)
  while that number exists. This is the same principle as `Rejected(DrivenSize)`: a drag never
  overrides a number the user typed. The test asserts (0, 0) with the `AxisDistance` in place and
  (dx, 0) once it is removed.
- **P7's last clause is a loop, not a single removal.** §7.2 says removing all the named
  relationships makes the same request succeed. That cannot hold in general: a sketch can hold two
  independent conflicts with the same request, and a report that names "the smallest set found"
  names only the one it hit. The property is tested as: the report always names something that can
  be removed, removing it strictly reduces the relationship count, and repeating resolves the
  request. P7's other clauses — two derivations, every named id present, a non-empty summary — are
  tested as written. Two carve-outs, both forced by §4.1: the relationship a `SetParameter` is
  about is never removed (there would be nothing left to ask for), and an `AddRelationship`'s own
  new relationship is not expected to be in the sketch already.
- **No property-testing package.** FsCheck and CsCheck both have unverified licences for the #2
  gate, and §7.2 allows a hand-rolled seeded generator. The properties are seeded loops under
  xUnit theories; every assertion prints the seed and the iteration. A further test counts the
  outcomes the generator reaches across all seeds and fails if conflicts, rejections or blocked
  drags never occur, so the properties cannot pass by never reaching the cases they are about.

### 10.5 Known follow-ups

Findings from Fable's review of PR #35 that are deliberately not addressed in #5, so that the next
person to touch this code does not have to rediscover them.

- **Chains of three or more parts resolve `OverConstrained` when a solution exists** — filed as
  #49. A, B and C in a row, flush to each other, with C anchored and A dimensioned: resizing A
  moves B east, and the second `Flush` then finds both sides assigned. Moving A's anchor west
  would satisfy everything. The implementation is faithful to §4.4's rule as written ("pins the
  opposite side … to something that is anchored or already assigned"), so this is a design
  decision for Marc rather than a defect. The cheap route, if he wants the friendlier answer, is a
  pinned closure per axis over `Coincident`/`Flush`/`AxisDistance`/`Centered` — `DirectUpdater`'s
  `RigidGroup` already computes exactly that shape for `Drag` — treating a side as not adjustable
  when its base is in an anchored closure.
- **The `Propagator`'s state is narrower than §5.2 step 4 implies.** `ScalarKind` covers X, Y,
  width and height, so the repair pass cannot perform `rotationB := rotationA + 90°` for
  exact-class `Parallel`/`Perpendicular`/`AngleBetween`. That is #28's extension by this design's
  own scoping — the two-phase structure and the `Side` model accommodate a rotation scalar without
  a rewrite — but the seam is narrower than §5.2 reads.
- **`Angle.FromDegrees` rounds a product that is not exact in binary.** `degrees * 3600` can land
  either side of a true tie. This is inherent to the double boundary and it still rounds exactly
  once, as §5.2 requires; noted rather than fixed.
- **A fully blocked drag does not offer the dimension to edit.** §7.1 case 13's opening will not
  slide while a driving `AxisDistance` fixes it, and the result is a zero applied delta with
  nothing to act on. #10 should turn a drag blocked by a driving relationship into an offer to
  edit that dimension, the way `Rejected(DrivenSize)` points at one.
- **Propagation order can decide the answer for equal-width chains** (Fable's second review of
  #35; pre-existing, not a regression of the review fixes; belongs with #49). Three boxes flush in a
  row with equal widths (`ParamValue(A.Width)`, two `EqualParam`s, two `Flush`es): after
  `SetParameter(A.Width, 10 → 20)` the result depends on relationship ids and argument order. One
  ordering gives `OverConstrained` on a solvable sketch; another gives `Solved` with A grown west
  and C unmoved, which satisfies every relationship but is not what the user expected. The root
  cause is the id-ordered initial worklist plus single assignment, so a candidate fix is to order
  the initial enqueue breadth-first from the changed scalars. That candidate is untested. Pin
  today's four outcomes in a test, or skip it against #49, before changing anything.
- **A `Centered` conflict with both ends anchored names only the first end's anchor** (LOW).
  Removing the relationship the report names still leaves a conflict; the report should name both.
