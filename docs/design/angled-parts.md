# Angled parts: splayed legs, raked backs, angled shelves and compound mitres

Status: **DRAFT — awaiting Marc's sign-off.** Written by Fable for
[issue #139](https://github.com/marctjones/napkin/issues/139), milestone M9 Shape. Nothing here is
authorized for implementation until Marc signs off; §11 lists the decisions that are his, each in
plain words with a recommended default. §8 lists the slices, each filed as its own issue.

**Where this note stands relative to what is already decided.** [`assembly-model.md`](./assembly-model.md)
§3a — the **strut**, an angled member stored by its two exact endpoints — was signed off by Marc on
2026-09-23 (decisions 17–24). **It has not been built**: `grep Strut src` finds nothing, and no issue
covers it. So the first thing this note does is *not* re-argue #139's question from scratch: the
strut is the representation of an angled part, for the reasons §3a.1 gives, and slices 1–4 below
are §3a's own build plan. What this note adds is the part §3a stopped short of — the magazine
splayed-leg stool whose legs need a **compound mitre**, the **raked chair back**, the **angled
shelf**, **joinery** on an angled end, and **snapping** to a leaning leg's face — and each addition
is marked *"changes §3a.n"* where it touches the signed-off text, and appears in §11 for Marc.

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024″ grid and angles in
integer arcseconds ([`geometry-model.md`](./geometry-model.md) §1); a box is parametric and its 24
orientations are signed permutations, exact by construction (assembly-model §1.3); relationships are
stored data behind one update interface with explicit result types (geometry-model §3–§4); the
solver is a separate workstream (#28) working in `double` on a copy, rounded once and repaired
(§5); a cut on a box is stored as setbacks, never as an angle, and an angle is an *entry mode* with
one flagged rounding (shaped-parts §1.3, decision 11.1); bevels on a box are not in the model
(shaped-parts §6); a strut stores its two ends and derives its board, its length rounded once and
marked `≈` unless proven exact (assembly-model §3a.1, §3a.4); a joint is a relationship between
two faces and never moves anything ([`joinery-and-fasteners.md`](./joinery-and-fasteners.md) §1,
§4); strict reading, no migration (DESIGN.md §12); sample expectations are hand-derived
(`samples/README.md`).

**The data rule (CLAUDE.md) governs every number here.** Stock sizes in the worked examples are
read from the shipped library in this task — a 2x2 is 1½″ × 1½″
(`src/Napkin.Core.Materials/data/softwood-lumber.json`, PS 20-25 Table 3, Dry columns, retrieved
2026-09-21) and ¾ plywood carries ¾″ as its Performance Category
(`src/Napkin.Core.Materials/data/sheet-goods-plywood.json`, PS 1-19 Table 10, retrieved 2026-09-21).
Seat heights, splays, runs and rises are **design choices for the fixtures, chosen so the
arithmetic is exact**, and are labelled so; napkin carries no default for any of them.

---

## The finding, in one paragraph

An angled part is a **strut**: two exact points in space, a stock cross-section, and a statement
of which axis-aligned plane each end is cut to. Nothing about that changes. What #139 adds is one
stored field — **which world axis the strut's wide face stays parallel to** — because for a leg
that leans two ways at once that choice decides *which board you cut*: keep the wide face vertical
and each end is a plain mitre (the sawhorse); keep it parallel to the apron and each end is a
compound mitre (the magazine stool). Both are derived boards from the same two exact points, so
the kernel stays exact, the solver stays out, and the cut list says *mitre α, bevel β, long point at
this corner* with the same `≈` discipline it already has. Snapping to a leaning leg is possible
exactly when the leg leans one way (its side faces are then axis-aligned planes) and refused,
by name, when it leans two ways. A butt joint on an angled end works; every other joint type is
refused. Curved parts, free rotation of a box, tapered splayed legs and a strut meeting a plane
that is not axis-aligned stay out.

---

## 1. Representation: how a lean is stored

### 1.1 The options

Today a part is a `Box` in one of 24 orientations, every vertex an anchor component plus or minus
a stored size (assembly-model §1.3). #139 asks how a *lean* — a tilt about one or two axes by a
stated angle — is added while stored values stay exact. Four candidates, argued against the one
fact that decides them (assembly-model §3a.1): **a member that is not axis-aligned cannot be exact
in all of its numbers, whatever is stored.** Either the far end is exact and the length and angle
are irrational, or the angle and length are exact and the far end is off the grid. So the question
is *which numbers are exact and which are derived, rounded once and marked.*

| Option | Stored | What is exact | What is not | Verdict |
|---|---|---|---|---|
| **A. `Box.Lean`** — one or two `Angle`s (arcseconds) on the existing box | anchor, sizes, orientation, lean angles | the stored fields | every far vertex: anchor + size × (irrational unit vector). Every `Flush`, `Coincident` and `AxisDistance` on the box becomes tolerance class; the propagator's "signed permutation" premise (assembly-model §3.1) fails for it | **rejected** — puts every angled part on the solver (#28), which is the outcome assembly-model §3.3 reached for a tilted box and Marc overrode for the sawhorse |
| **B. `Strut` by endpoints** (assembly-model §3a, signed off) | `From`, `To`, cross-section, end cuts | both ends, every relationship on them, the cross-section | the length, the setbacks, the angles — derived, rounded once, `≈` on the cut list | **recommended**, unchanged |
| **C. `Strut` by angle** — `From`, exact tilt and azimuth in arcseconds, exact length | one end, the angles, the length | those | the far end, and so every relationship on it: a leg's top under a seat is tolerance class; a taller stool needs the solver | rejected — it stores what a person types on a mitre saw and loses what they set out on the floor |
| **D. Free rotation of a box through the solver** (#28) | anchor, sizes, a general rotation | nothing but the stored fields | everything derived | not this note; it remains the fallback for what §6 excludes (an octagon's mitred rails, a shaped gusset turned 30°) |

**A stated angle is an entry mode, not storage.** Assembly-model decision 23 already names it and
defers it. This note pulls it into a slice (§8, slice F) because #139 is *about* a "stated angle":
the person types a tilt (and, for a two-way splay, an azimuth) and a run or a rise; napkin computes
the far end, rounds it once to the grid, and stores the point. The readout shows the angle the
stored points imply, `≈ 16½°` to the nearest half degree (§2.4), which over any leg a person can
cut differs from what was typed by far less than a half degree. The typed angle is not remembered:
two sources of truth for one leg is what geometry-model §3.3's "one number, one owner" forbids.

### 1.2 What is new: the reference axis (changes §3a.2 and §3a.3 step 1)

Assembly-model §3a.3 fixes the strut's drawn plane from its cuts: the local frame is built from
the highest-priority cut axis (`Z`, then `Y`, then `X`), so that a sawhorse leg's wide face is
vertical and both its cuts are plain mitres in that face. That rule silently decides which board a
two-way-splayed leg becomes, and it decides it the sawhorse way. The magazine stool is the other
way: the legs' wide faces are kept *parallel to the aprons*, so the aprons can be screwed to them
flat, and the price is that every end is a compound cut. Both are real furniture; the second is the
more common DIY plan. So the strut gains one stored field:

```csharp
public sealed record Strut(
    EntityId Id, LayerId Layer,
    Point3 From, Point3 To,            // exact, as §3a.2
    EndCut FromCut, EndCut ToCut,      // Square, X, Y, Z — as §3a.2
    Axis Reference,                    // NEW: the world axis the wide (drawn) face stays parallel to
    Length Height, Length Depth)       // the cross-section, as §3a.2
    : Entity(Id, Layer);
```

- **The frame** is §3a.3's, with `Reference` in place of `n_ref`: local X is `d = To − From`;
  local Z is `z = d × r̂` where `r̂` is the unit vector of `Reference`; local Y is `y = z × d`.
  Both are integer vectors; `z` is nonzero for any axis because `d` is not axis-aligned
  (invariant 14). The drawn plane — the wide face, where `Height` lies — contains `d` and the
  reference axis. `d` is oriented so that `d · r̂ > 0` (§3a.3 step 2), which keeps the derived
  blank canonical for mirror-image legs.
- **The tool defaults `Reference` to §3a.3 step 1's axis** — the highest-priority cut axis, `Z`
  when both ends are square — so a leg drawn floor-to-underside gets a vertical wide face and plain
  mitres unless the person says otherwise. The properties panel offers the three axes with plain
  words: *"keep the wide face vertical"* (`Z`), *"keep the wide face parallel to the long side"*
  (`X`), *"… to the short side"* (`Y`). §11 decision 1.
- **For a one-way lean the choice is cosmetic.** When `d` has a zero component, the two frames a
  person might pick give the same physical board with `Height` and `Depth` swapped, and the same
  cut (a bevel with the wide face down is the same cut as a mitre with the narrow face down —
  §3a.3's "why it looks compound"). For a two-way lean it is not cosmetic: §9's footstool derives
  **three different boards** for `Z`, `X` and `Y`. That is why it is stored, and why it is in the
  file with one spelling rather than inferred.

### 1.3 What is new: an end cut may be compound (changes §3a.3 and invariant 15)

§3a.3 refuses a strut whose two cut axes are not both perpendicular to local Z
(`Rejected(StrutNeedsBevel)`), because such a cut is not a plain mitre in the drawn face. With a
stored `Reference` that refusal would forbid the magazine stool outright. The refusal is
**retired**, and an end whose cut normal `n` has a component along local Z is a **compound end**,
derived and reported as a mitre plus a bevel (§2.2).

This is not a reversal of shaped-parts §6 ("bevels and compound angles are not in the model"),
and the distinction is worth stating plainly so it is not read as one. A box's cuts are **stored**
against a stored blank, and a bevel on a box would be a stored thing the plan view cannot show. A
strut's blank is **derived**: the compound end is a *description of the derived board*, computed
from two exact points and an axis, and nothing about it is stored. The box rule stands
unchanged; the strut never had a stored cut to begin with. `StrutNeedsBevel` survives only as a
message in the one construction that still cannot be built — a two-way-splayed leg lying *flat*
against a box's vertical side (§3.4): the strut is fine, the box would need a bevel, and the
`Flush` is what is refused.

### 1.4 Invariants (replacing assembly-model 14–16; 17 unchanged)

14. `d` is not axis-aligned: the ends differ in at least two coordinates. (`StrutIsAxisAligned`.)
15. `From ≠ To`, `Height > 0`, `Depth > 0`. (No coplanarity condition any more.)
16. If the strut has a `Part`, `Part.PlanAxes.X` is `length` **or `width`** — the derived
    dimension is whichever of the two the person names; `thickness` on X is refused. This is what
    lets an angled shelf list as a 30″ × 10″ shelf rather than a 10″ × 30″ one (§1.5).
17. The derived blank satisfies shaped-parts invariants 7–9 on its rounded values
    (`StrutTooShortForItsCuts`), as §3a.5 says.

### 1.5 The named examples, each as a strut

- **A splayed-leg stool or bench** — the worked example, §9. One-way lean: `d = (0, run, rise)`.
  Two-way: `d = (run_x, run_y, rise)`. `Z` cuts at both ends (floor and the seat's underside).
  `Reference = Z` for a sawhorse-style leg (plain mitres), `X` or `Y` for a magazine-style leg
  (compound).
- **A raked chair back.** A straight rear leg leaning back is a one-way lean, `d = (run, 0, rise)`,
  `Z` at the foot, `Z` or `Square` at the top. Its two faces normal to Y are axis-aligned planes
  (§3.2), so a side seat rail can be held `Flush` to the leg's inside face — but the rail's *end*
  meets the leg's tilted front face, and that end is a `CornerCut` on the rail (a mitre, shaped-parts
  §1.3) whose setback is `rail height × run / rise`, typed by the person or entered as the rake
  angle. Nothing keeps that mitre matched to the rake when the rake changes: that is #67's
  relationship-on-a-cut, and it is named here, not built. The *bent* rear leg — vertical below the
  seat, raked above, cut from a wide board — needs two cuts on one edge of a blank, which
  shaped-parts invariant 6 forbids; it is out (§6).
- **An angled shelf** (a display shelf tilted about its long axis between two sides). A strut
  *across* the tilt: `From` at the back edge's centreline, `To` at the front edge's, `Height` the
  shelf's thickness, **`Depth` the shelf's length** along the sides, `d = (0, run, rise)`. A `Y` cut
  at the back (it butts to the back panel's face) and a `Y` cut at the front (a plumb front edge);
  with `Reference = Y` the drawn face is the shelf's *end profile* and both cuts are plain mitres in
  it. What a person calls *a bevel along the front edge* is, in napkin, a mitre in the end profile
  **for the full length** — the same reading shaped-parts §1.4 gives a chamfer on a leg drawn as
  its footprint, and the cut-list sentence carries the same suffix (§2.3). `PlanAxes { x: width,
  z: length }` names the derived dimension the width, per invariant 16, so the row reads
  30″ × 10″ × ¾″.
- **A compound mitre on a box** (a picture-frame corner with the sides tilted, a crown moulding):
  not a strut and not this note. A box's cuts are square through the plan (shaped-parts §1.4) and
  stay so. Named in §6.

---

## 2. The cut list for a leaning leg

### 2.1 The board: long-point length

A strut's row lists the board to buy and cut: the **long-point length** `L`, then `Height` and
`Depth` through `PlanAxes`, as assembly-model §3a.6 says. With `c = |d|` the centreline length and,
for each end, the cut normal `n` (a world axis; a square end contributes nothing), the components
of `n` in the strut's frame are

```
n_d = (n · d) / |d|        n_y = (n · y) / |y|        n_z = (n · z) / |z|
```

and the long-point length is

```
L = c + Σ over the two ends of  ( ½·Height·|n_y| + ½·Depth·|n_z| ) / |n_d|
```

Each end's term is how far the farthest corner of the cross-section reaches beyond the centreline
point before its edge line meets the cut plane. When `n_z = 0` — a plain mitre — the term is
`½·Height·|n_y|/|n_d| = ½·s` with `s = Height·tan α` the setback, and `L = c + (s₁ + s₂)/2`,
which is assembly-model §3a.4's formula exactly; §9's bench confirms it (7/16″ setback, 7/32″ per
end). The dot products and squared norms are exact integers (in `Int128`); `L` is formed in
`double` from them in one fixed operation order and rounded **once** through
`Length.FromInches(…, HalfToEven)`, per §3a.4 — never a sum of separately rounded terms.

**Exactness** keeps §3a.4's proof for a plain mitre (perfect-square tests in `Int128`). A compound
end's term is **always marked `≈`** and no proof is attempted: it is a sum of two irrational
contributions and the case where both happen to be exact is not worth the arithmetic. §11
decision 5.

### 2.2 The end: a plain mitre, or a compound angle

Two angles describe any end cut, defined geometrically so that they do not depend on anyone's
saw. With the blank's wide face — the drawn face, local XY — as the reference:

- **mitre** `α`: the swing of the cut across the wide face, from square. `tan α = |n_y| / |n_d|`.
- **bevel** `β`: the tilt of the cut from perpendicular to the wide face. `sin β = |n_z|`.

| The end | Condition | Reported as | Derived blank carries |
|---|---|---|---|
| square | the end is `Square` | nothing | no cut |
| **plain mitre** | `n_z = 0` (the cut axis is perpendicular to local Z: `n · z = 0`, one integer dot product) | `CornerCut` on the drawn face, setback `s = Height·tan α`, as §3a.3 step 5–6; "Mitre the west end: from *s* in along the south edge to the north-west corner (≈ α off square)" | `DerivedCut(CornerCut, Exact)` — unchanged from §3a |
| **compound** | `n_z ≠ 0` | "Cut the west end at a compound angle: mitre ≈ α, bevel ≈ β, with the top face on the saw table; long point at the south-bottom corner." | `DerivedCompoundEnd(StrutEnd, Angle Mitre, Angle Bevel, StrutCorner LongPoint)` |

**Which cuts need a compound mitre, stated as a rule a person can apply:** an end is compound iff
the plane it is cut to is *not* perpendicular to the strut's wide face — that is, iff the cut axis
is not in the plane spanned by the strut's direction and its `Reference` axis. Concretely: a leg
that leans **one way** never needs one (both its faces are either vertical or perpendicular to the
lean — pick either as the reference); a leg that leans **two ways** needs one at every axis-cut
end **unless `Reference` is the cut's own axis** (a `Z`-cut leg with `Reference = Z` — the
sawhorse — is a plain mitre; the same leg with `Reference = X` is compound at both ends).

**The long point** is named by the blank's own compass — `South`/`North` for local ∓Y (the two
edges of the wide face), `Bottom`/`Top` for local ∓Z (the two wide faces themselves). With `n`
oriented along the strut (`n · d > 0`), the long point at the `From` end is the corner whose
local-Y sign is `sign(n · y)` and whose local-Z sign is `sign(n · z)`; at the `To` end both signs
flip. For a plain mitre `n · z = 0` and the long point is a whole edge, which is what the
`CornerCut` sentence already names. §9's footstool with `Reference = X` pins this: long point
south-bottom at the foot, north-top at the seat.

### 2.3 The sentences

`CutDescription.Describe` (shaped-parts §4.4) gains the compound form above and is otherwise
unchanged; the plain-mitre sentence is the existing one. Two rules carried over and one added:

- **Like cuts are said once.** A parallelogram's two equal mitres collapse to "Mitre both ends: …".
  Two equal compound ends collapse the same way, naming both long points.
- **When the out-of-plane dimension is not the thickness, say so.** A strut whose `Depth` is its
  `length` by `PlanAxes` (the angled shelf, §1.5) ends its sentence "for the full 30″ length" — the
  rule shaped-parts §4.4 already has, now reached by a strut. The derivation passes `PlanAxes` and
  the out-of-plane name to `Describe` as a box row does.
- **The saw setup is named by the blank's face, not by "wide face down".** "With the top face on
  the saw table" — the blank's compass, the one frame the file, the thumbnail and the conflict
  messages share. How a person then holds the board is theirs (joinery §1 rule 3).

### 2.4 Rounding and display of angles — the rule, stated once

- An angle on a cut-list row is **derived, never stored**: `atan` or `asin` of ratios of exact
  integers, in `double`, display only. It is never a `Length`, never an `Angle` in the sketch.
- It is shown **to the nearest half degree, rounded half away from zero** — the rule shaped-parts
  §4.4 already applies to a box's mitre ("shown to the nearest half degree with `≈` unless exact").
  A half degree is finer than a mitre saw's scale and what a magazine prints; showing 16.26° would
  claim a precision no saw has. Whole degrees were considered and rejected because 22½° is a
  detent on every mitre saw.
- It is marked **`≈` unless proven exact in integers**: a mitre is exact only at 0° or 45°
  (`|n · y|·|d| = |n · d|·|y|`, which with the frame's construction is an equality of two
  integer squares); a bevel only at 0° (then it is not printed) or 30° (`4·(n · z)² = |z|²`).
  Any other angle a saw scale names — 15°, 22½° — is a *display rounding* of an irrational value
  and keeps its `≈`. Exactness is proven, never inferred from the double (assembly-model §3a.4).
- **Tilt and azimuth readouts** on the properties panel (assembly-model §3a.7) follow the same
  rule. They are the same numbers as `α` and the plan angle only for a `Z` cut with
  `Reference = Z`; the panel says which it is showing.
- **Platform determinism.** A half-degree rounding flips only if the true angle lies within the
  runtime's `atan` error (~10⁻¹³°) of a boundary; §9's cases sit tenths of a degree from any. The
  *lengths* are bit-identical across machines by §3a.4's fixed order and IEEE 754's correctly
  rounded square root; the angles are display text, and a golden test that pins one pins a value
  far from a boundary. §10 names the residual risk.

### 2.5 Grouping

The group key compares the derived blank — `L`, the cuts in site order, the compound ends by
value — plus stock and species (assembly-model §3a.6). Two additions:

- **A compound blank is compared up to a half-turn of the board about any of its own three axes**
  (end for end, face for face, edge for edge) — the rule joinery §6.3 already uses for a part's
  joinery set, applied to the long-point corners. That is what makes the four legs of a symmetric
  magazine stool, whose long points fall at four different corner names, **one row of four**: each
  is another turned over or end for end (§9 case 6 works it through). Mirror images that no
  half-turn relates stay separate rows, as shaped-parts decision 11.6 says.
- **The derived dimension groups under whichever name `PlanAxes` gives it** (invariant 16), so an
  angled shelf and a plain shelf of the same three sizes are still two rows — one has cuts.

---

## 3. Relationships and snapping for leaning parts

### 3.1 What assembly-model §3a.5 already decides (collated, not redesigned)

- A strut's **ends** are places fixing X, Y and Z (`StrutEndRef`); they take part in `Coincident`,
  `AxisDistance` and `Centered` under the existing legality rules, and nothing else.
- **A positional implication on an end moves that end only** — the per-end rule, with the node as
  precedent; `Drag` moves both ends as a rigid group. That is what makes a taller stool have
  longer legs and a wider seat have more splay, exactly, through relationships that already exist.
- `Height` and `Depth` are ordinary size refs (`ParamValue`, `EqualParam`, stock assignment).
  There is **no `StrutLengthRef`**: a typed length is `Distance`, tolerance class, #28's.
- `Anchored(strut)` pins all eight scalars (now nine with `Reference`, which is discrete and never
  a scalar — like `FaceUp`, it is a fact the propagator is handed, assembly-model §3.2).
- The floor is not an entity; feet are held level with each other by a ring of `AxisDistance`s.

### 3.2 What is new: flush to a leaning leg's face (changes §3a.5 "a strut's body is not a place")

§3a.5 says no `Flush` to a strut's face, because its faces are irrational. That is true of a leg
that leans two ways and **false of a leg that leans one way**: when `d` has a zero component on
world axis `a`, one of the strut's local cross-section axes lies along `a` — for `d = (0, 7, 24)`
with `Reference = Z`, `z = d × Z = (7, 0, 0)` is world X — and the two faces normal to it are
axis-aligned planes at the centreline coordinate ± half the size along that axis. A raked chair's
side rail flush to its rear leg, a bench's stretcher flush to a one-way-splayed leg's inside face,
a leaning bookcase side's shelves flush to it: all exact.

```csharp
/// One of a strut's four long faces, in its own frame: South/North are the edges of the wide
/// face (local ∓Y), Bottom/Top the two wide faces (local ∓Z).
public enum StrutFace { South, North, Bottom, Top }
public sealed record StrutFaceRef(EntityId Strut, StrutFace Face) : PlaceRef;   // fixes ONE axis, or none
```

- `Sketch.PlaceOf(StrutFaceRef)` fixes one axis when the face's normal (local `y` or `z`, an integer
  vector) has exactly one nonzero component, and **nothing** otherwise; a `Flush` to a face that
  fixes nothing is `Rejected(PlacesNotComparable)` with the message *"the leg leans two ways, so
  its faces are not square to anything; napkin can't hold this"*. This is the "say so, don't
  solve" answer #139 asks for: a `Flush` to a two-way-splayed leg's face is the solver's
  (`PointOnEdge`/`Flush` on rotated geometry, tolerance class, #28), and the direct updater names
  it rather than approximating it.
- The face's coordinate is `From.Component(a) ± size/2`. It is exact when the size is **even in
  units**; when odd, the reference is refused with the same rejection and a message naming the
  size. Every library stock is a multiple of 1/64″ = 16 units, so this bites only a hand-typed
  odd size. A half-unit tolerance like `Centered`'s was considered and rejected: `Flush` is exact
  class everywhere else and one odd case is not worth a second class for it. §11 decision 3.
- **In the propagator**, a `Flush` on a `StrutFaceRef` implies a value for one component of the
  centreline — of **both ends**, since `d`'s component along `a` is zero and stays zero (moving one
  end off the face's axis would end the lean being one-way; it is a contradiction, reported,
  naming the `Flush`). This is the one place a strut's two ends are coupled by a relationship,
  and it is the coupling the person asked for. The rigid group for `Drag` already includes both
  ends.
- **What can still not be held**, named: a part flush to a two-way-splayed leg (above); a part's
  corner `Coincident` to a corner of a strut's end face (irrational, §3a.5); a stretcher's end
  *matched* to a leg's rake (#67, relationships on cuts); a leg's *length* (`Distance`, #28);
  "these legs all lean the same" as a relationship (`EqualParam` on the two `AxisDistance`s from
  top to foot does it exactly, and is what the fixture uses — no new kind).

### 3.3 Snapping

Assembly-model §3a.7's snap rules stand: a strut's **end** snaps to a box's upright edges,
vertices and the grid and produces `Coincident` or `AxisDistance` on the end; a box feature snaps to
a strut end as to any point. Added:

- A box's face snaps to a strut's **axis-aligned face** (a `StrutFaceRef` that fixes an axis) and
  produces a `Flush`, drawn with the same faint edge line shaped-parts §7.2 uses for a virtual
  corner. A strut face that fixes nothing is not a snap target and shows no indicator.
- The **plan canvas** (`SnapResolver`, `BoxGeometry.AxisAlignedEdges`) sees a one-way-splayed
  strut's axis-aligned faces as two more axis-aligned edge lines; a two-way strut's silhouette is
  drawn and not snapped to.

### 3.4 The construction that is still refused

A leg that leans two ways with its wide face *flat against a box's vertical side* — the
legs-against-a-bevelled-beam sawhorse (assembly-model §3a.3). The strut is valid (`Reference =
X`, compound ends). What fails is the `Flush` between its face and the beam's side:
`PlacesNotComparable`, because the leg's face is not axis-aligned. Flat contact would need a bevel
on the *beam*, a box, and shaped-parts §6 keeps bevels off boxes. The message names both:
*"the leg's east face isn't square to anything and the beam can't be bevelled"*. Nothing here
changes that verdict; it moves from a refusal of the strut to a refusal of the relationship, which
is the honest place for it.

---

## 4. The 3D view, the plan and the standard views

- **The solid** (assembly-model §3a.7, extended). A prism on the cross-section, clipped by the two
  end planes: six planar faces, four quads along the length and two end polygons. For a compound
  end the end face is still one planar polygon (a plane cutting a rectangular prism), so the
  `Solid` record needs nothing new; every vertex is computed in `double` from the exact ends, the
  exact integer frame and the exact sizes, and the end planes are the exact planes through `From`
  and `To`, so a foot sits flat on the floor in the picture even when its `≈` length is a hair
  off. The three-tone shading keys on the dominant world component of each face's normal, as now.
- **The free 3D view**: cull-and-paint, no hidden pass (assembly-model §8.4). A strut is convex,
  so it never self-hides; between parts the painter's order is what it is today.
- **The plan view**: the silhouette from above in `double`, ends as grips at their exact XY
  (§3a.7). A one-way strut whose faces are axis-aligned also draws those two face lines faint when
  a snap is catching one.
- **The standard views** ([`standard-views.md`](./standard-views.md) §2.3): `HiddenEdges` is
  "valid only when every drawn polygon is parallel to the screen", and a strut's long faces are
  not. The pass is generalised in one place: a polygon's depth at a screen point is the depth of
  its *plane* there (linear in the screen coordinates, since every polygon is planar), and
  "strictly nearer" is judged at the sub-segment's midpoint against that plane rather than at
  `Points[0]`. The completeness argument survives — a convex solid seen from anywhere has no
  self-hidden edge, so hidden edges still arise only from other parts — and the axis-view case
  reduces to today's constant depth. Light dashes, toggle, cost: unchanged. Cases in §9.
- **The parts view** ([`parts-view.md`](./parts-view.md)): a strut's cell draws its derived blank
  in its own frame — the parallelogram or trapezoid, or for a compound end the wide face's outline
  with the bevel shown as a second, dashed line offset by `Depth·tan β` — with the long point
  labelled. That is the "see drawing" beside the compound sentence.

---

## 5. Joinery on angled parts

Joinery §6.6 waits for #139 to "say what a strut face is". The answer, in joinery's own terms:

- **A strut's end face is an axis-aligned plane** — the cut axis, at the end's coordinate — so it
  can be one side of a joint. A strut's four long faces are axis-aligned only for a one-way lean
  (§3.2) and are never a joint face in this note: the joint types that would use one need a cut
  into the strut, which decision 24 keeps out.

```csharp
public sealed record StrutEndFaceRef(EntityId Strut, StrutEnd End) : PlaceRef;   // fixes the cut axis; illegal for a Square end
// Joint.Receiving / Joint.Inserted: a FeatureRef with exactly one face, or a StrutEndFaceRef.
```

| Joint type (joinery §3.1) | On a strut end | Why |
|---|---|---|
| `butt` | **yes** — the strut's end is always the *inserted* face; the receiving face is a box face, or another strut's end face (an A-frame's apex, two `Y` ends `Coincident`) | nothing is cut; the strut's end sits on a face. Fastenings: `pocketScrews` (the classic splayed-leg attachment, holes in the leg's end from one of its four `StrutFace`s), `screws`, `dowels`, `nails`, `none`; glue as ever |
| `groove`, `rabbet`, `halfLap`, `tabletop` | **refused** (`Sketch.Validate`, greyed in the popover) | each cuts into a part along a face; a strut's derived blank carries no cuts beyond its ends (decision 24), and a groove *for* a strut's end in the receiving box would be an angled dado — the let-in brace, §6 |
| `tenon` (later) | later, as for boxes: the strut is the inserted part and its end grows by `depth` along `d` — which is exact on the centreline (`To + depth·d̂` is not, but the *listed* length simply adds `depth`) | named so the later type does not have to rediscover it |

- **The contact** is the intersection of the receiving face's rectangle with the strut's end
  polygon, whose corners are irrational. It is computed in `double` (`JointGeometry.Of` gains one
  arm), and its long side — the **joint length** the count recipes read — is rounded once to the
  grid and marked `≈` in the tooltip. A count of `max(2, ⌈L/2⌉)` pocket screws does not care about
  the 1/1024″.
- **Satisfied** means the end's plane and the receiving face fix the same axis at the same
  coordinate (exact, as a `Flush` is judged) and the polygon intersection is non-empty (`double`);
  unsatisfied draws hollow, as today.
- **Sentences.** Pocket holes on a strut: "Drill 2 pocket holes in the west end from the bottom
  face." — the blank's compass. No allowance for a butt. The receiving box says nothing, as now.
- **The half-turn grouping** (joinery §6.3) applies to a strut's joinery set as to a box's.
- **Refused pairings** get the existing `PlacesNotComparable`/validation messages; a joint naming a
  `Square` end is refused at load and in the popover ("a square end meets nothing flat").

---

## 6. Simplicity rules — what is out, and why

Each is real; none is this design. Stated so scope does not creep past "a splayed stool from a
magazine".

1. **Curved parts, arcs, splines, compound curves.** Not drawing primitives (shaped-parts §6); a
   strut is straight. A curved chair back is a box with a `CurvedEdge` if it is flat, and out if it
   is not.
2. **Arbitrary free rotation of a box.** `Box.Rotation` past a quarter turn, or any tilt of a box,
   stays #28's (assembly-model §3.3). An octagon's mitred rails, a shaped gusset at 30°, a picture
   frame with tilted sides: out.
3. **A rolled strut** at any roll but the three axis references. `Reference` is `X`, `Y` or `Z`;
   "roll the leg 10° so it looks right" is out — no axis-aligned surface it could meet needs it,
   and the exact integer frame depends on it.
4. **Cuts on a strut beyond its two ends** — a tapered splayed leg, a chamfered brace: assembly-model
   decision 24 stands. The stand-in is a box leg drawn lying flat with its taper, which does not
   splay; or a splayed leg that does not taper. Named, not half-supported.
5. **A strut meeting a plane that is not axis-aligned** — braces mitred to each other, an
   unsymmetric A-frame's apex, a strut cut to another strut's long face (§3a.8). The cut plane's
   normal is irrational; nothing on the grid can state it.
6. **A bevel on a box.** Shaped-parts §6 stands. The two-way-splayed leg against a beam's side
   (§3.4) is the case people will ask for, and the answer is "one-way splay, or a `Reference = X`
   leg with a gap the aprons cover".
7. **The bent (dog-leg) rear leg**, two cuts on one edge of a blank (shaped-parts invariant 6).
8. **Relationships on a strut's derived numbers** — length, angle, "same rake as" — and a typed
   length: `Distance`/`AngleBetween`, tolerance class, #28. "Same lean" is two `EqualParam`s on
   the top-to-foot `AxisDistance`s, exact and already available.
9. **Joinery beyond a butt on the end** (§5), the let-in brace's angled dado in particular.
10. **A floor entity, physics, stability checks** ("will this stool tip?"): #154's research note,
    not geometry.

---

## 7. File format

One bump, **`formatVersion` 10 → 11**, covering everything in this note. Every field required;
a version-10 file is refused with the existing message; every `samples/*.scene.json` and
`*.expected.json` is rewritten in the same commit (slice A).

```json
{ "id": "…", "type": "strut", "layer": "…", "name": "Leg, south-west",
  "from": { "x": 4096, "y": -4096, "z": 0 },
  "to":   { "x": 4096, "y": 3072, "z": 24576 },
  "fromCut": "z", "toCut": "z",
  "reference": "z",
  "height": 1536, "depth": 1536,
  "part": { "stock": "2x2", "species": null, "quantity": 1,
            "planAxes": { "x": "length", "y": "width", "z": "thickness" }, "hardware": [] } }
```

| Field | Type | Refused when |
|---|---|---|
| `from`, `to` | integer `x`, `y`, `z` | missing; equal; differing in fewer than two coordinates (invariant 14) |
| `fromCut`, `toCut` | `square`, `x`, `y`, `z` | any other value |
| `reference` | `x`, `y`, `z` | any other value (there is no default in the file; the tool chose one) |
| `height`, `depth` | integer > 0 | ≤ 0 |
| `part.planAxes.x` | `length` or `width` | `thickness` (invariant 16) |
| (the derived blank) | — | invariant 17 fails: `StrutTooShortForItsCuts` |

New reference kinds, each spelled like `feature`: `strutEnd { strut, end }` (as §3a.5),
`strutFace { strut, face }` (§3.2), `strutEndFace { strut, end }` (§5). Refused: a `flush` naming a
`strutEnd`; a `flush` naming a `strutFace` whose strut leans two ways, or whose size along the fixed
axis is odd; a `joint` naming a `strutEndFace` on a `square` end, or with a type other than `butt`;
a `param` of kind `strutLength`. Loading re-derives every strut's blank once for invariant 17, as
§3a.4 says. `Load(Save(s)) == s` holds; the derived blank is never in the file.

---

## 8. Slices

Six slices, each an issue in M9 Shape, each sized for Sonnet with the design in hand. Slices A–D
are assembly-model §3a's own build plan (unbuilt since its sign-off) plus the small deltas §1
makes to it; E and F are the additions. Catalog ids are the next free ones when the catalog is
edited (`CUT-019`, `GEO-020`, `GUI-STRUT-01` onward); the shapes are not invented here.

| Slice | What lands | Where | Model |
|---|---|---|---|
| **A. The strut and its blank** | `Strut` with `Reference`; `StrutEnd`, `EndCut`, `StrutFace`; invariants 14–17; the integer frame; `StrutBlank.Derive` with plain and compound ends (§2.1–§2.2), the `Int128` perfect-square proofs, the fixed operation order; `Sketch.Validate`; `formatVersion` 11 with the `strut` entity and the three reference kinds; samples rewritten | `Napkin.Core.Geometry`, `Napkin.Core.Project` | Sonnet |
| **B. The strut in the propagator** | `StrutEndRef`, `StrutFaceRef` as places; the per-end rule; `SetStrutEnd`, `DragStrutEnd`, `Drag` on a strut, `Anchored(strut)`, size refs and stock assignment; the two-end coupling under a `StrutFaceRef` `Flush`; `PlacesNotComparable` messages; checker arms; `UnsupportedRequest` for box-only requests | `Napkin.Core.Geometry` | Sonnet — **escalate to Opus if the per-end rule's tests (§9 cases 8–9) do not converge in one session**; a wrong rule here is a silent geometric error |
| **C. The cut list and the two samples** | `Part.SizeOn(strut)` under invariant 16; `CutListRow.LengthExact`; the compound sentence, the "for the full length" suffix reached by a strut, the half-degree rule (§2.4) in one function with its exactness proofs; half-turn grouping of compound blanks; CSV; the `splayed-bench` and `splayed-footstool` samples with hand-derived expectations (§9) | `Napkin.Modules.Furniture`, `samples/` | Sonnet |
| **D. Drawing and the strut tool** | the solid with compound ends; plan silhouette and grips; `HiddenEdges` generalised to planar depth (§4); picking; the two-click tool in both views with the `Reference` default and the three plain-words choices; the properties panel with ends, cuts, reference and the marked readouts; parts-view cell; `GUI-STRUT-01` (draw the bench: seat, four legs by clicks, cut list shows one row of four) | `Napkin.Modules.Editing`, `Napkin.App`, `Napkin.App.GuiTests` | Sonnet |
| **E. Joinery on a strut's end** | `StrutEndFaceRef`; `Joint` accepting it; `JointGeometry` contact and once-rounded joint length; butt-only validation; pocket face from `StrutFace`; sentences; the join tool's popover on a strut; the footstool sample gains pocket-screw joints and a fastener list | `Napkin.Core.Geometry`, `Napkin.Modules.Furniture`, `Napkin.App` | Sonnet |
| **F. Angle entry, the raked back and the angled shelf** | the entry mode: typed tilt (and azimuth) plus a run or rise places the far end, rounded once and flagged; `PlanAxes.X = width` in the tool; a `raked-chair-frame` sample (one-way lean, side rails `Flush` to the legs, rail ends as typed mitres) and an `angled-shelf` sample; `GUI-STRUT-02` | `Napkin.Modules.Editing`, `Napkin.App`, `samples/` | Sonnet |

Issues: A [#189](https://github.com/marctjones/napkin/issues/189), B
[#190](https://github.com/marctjones/napkin/issues/190), C
[#191](https://github.com/marctjones/napkin/issues/191), D
[#192](https://github.com/marctjones/napkin/issues/192), E
[#193](https://github.com/marctjones/napkin/issues/193), F
[#194](https://github.com/marctjones/napkin/issues/194).
Order: A → B → C → D, then E and F in either order. Each lands directly on `main` per CLAUDE.md,
with its own build, test and ratchet run; the version bumps once per slice.

---

## 9. Test plan, with the hand-derived worked examples

Every number below was derived by hand (and checked with a throwaway script that is not part of
the repository); the samples' `expected.json` files carry the same derivations as `derivation`
strings, per `samples/README.md`, independent of napkin's output. Stock sizes are the library's
(header of this note); every other number is a fixture choice made so the arithmetic lands on the
grid.

### 9.1 The splayed bench — one-way lean, plain mitres

**The design.** A ¾ plywood seat 36″ × 12″ (`Width 36864, Height 12288, Depth 768`), top at
Z = 24¾″, so its underside is at **Z = 24″ = 24576 units**, `Anchored`. Four 2x2 legs (`Height 1536,
Depth 1536`, `{ x: length, y: width, z: thickness }`), `Z` cuts at both ends, `Reference = Z`. Each
leg's top centre is 4″ in from a seat end and 3″ in from a seat edge; each foot is directly below
in X and **7″ further out** in Y. So for the south-west leg: `To = (4096, 3072, 24576)`, `From =
(4096, −4096, 0)`, `d = (0, 7168, 24576)` — **run 7″, rise 24″**. The other three mirror it in X
and Y.

**Relationships** (all exact class): per leg, `AxisDistance(seat.Face(Bottom), leg.To, Z, 0)`,
`AxisDistance(seat.Face(West), leg.To, X, ±4″)`, `AxisDistance(seat.Face(South), leg.To, Y, ±3″)`,
and foot-to-top `AxisDistance(leg.To, leg.From, X, 0)`, `(…, Y, ∓7″)`, `(…, Z, −24″)`; the four
`Y` splays tied by `EqualParam` is not needed — they are four typed `AxisDistance`s. A wider seat
moves the tops and, through the top-to-foot distances, the feet: the bench rescales and every
leg stays the same board.

**The derivation** for one leg (`d = (0, 7, 24)` in inches; 7² + 24² = 625 = 25², a Pythagorean
triple — the fixture choice):

| Quantity | Formula | Value | Exact? |
|---|---|---|---|
| frame | `z = d × Z = (7, 0, 0)`, `y = z × d = (0, −168, 49)` | integers | — |
| centreline `c` | `√(7² + 24²)` | **25″ = 25600** | yes (625 is a perfect square) |
| `n_z` for the `Z` cut | `Z · z / |z|` | 0 → **plain mitre** at both ends | — |
| setback `s` | `Height · run / rise = 1½ × 7 / 24` | **7/16″ = 448** | yes (`1536 × 7 = 10752 = 24 × 448`) |
| long-point length `L` | `c + (s + s)/2 = 25 + 7/16` | **25 7/16″ = 26048** | yes |
| mitre `α` | `atan(7/24) = 16.2602°` | shown **≈ 16½°** | no (only 45° is) |
| corner sites | §3a.3 step 6, `n · y > 0` → the long point is the north edge at the foot | `CornerCut(SouthWest, 448, 1536)`, `CornerCut(NorthEast, 448, 1536)` — a parallelogram | — |

**The cut list** (two rows; the four legs derive blanks equal by value — mirroring `d_y` or `d_x`
changes no norm and, by §3a.3 step 2's canonical orientation, no corner site):

| Row | Qty | L × W × T | Material | Cuts |
|---|---|---|---|---|
| Seat | 1 | 36 × 12 × ¾ | 3/4 plywood | — |
| Leg | 4 | **25 7/16** × 1½ × 1½ | 2x2 | Mitre both ends: 7/16″ in along the edge to the far corner, a parallelogram (≈ 16½° off square). |

No `≈` on any length: `LengthExact` is true, both cuts are exact, only the angle is marked.

### 9.2 The splayed footstool — two-way lean, both boards

**The design.** A ¾ plywood seat 12″ × 12″, top at 12¾″, underside at **Z = 12″ = 12288**,
`Anchored`. Four 2x2 legs, `Z` cuts. Each top centre is 3″ in from both seat edges; each foot is
**3″ out in X and 4″ out in Y** from its top. South-west leg: `To = (3072, 3072, 12288)`, `From =
(0, −1024, 0)`, `d = (3072, 4096, 12288)` — in inches `(3, 4, 12)`, and 3² + 4² + 12² = 169 = 13²,
a Pythagorean quadruple with plan run √(3² + 4²) = 5 exactly. The fixture choice.

**Board 1 — `Reference = Z`** (the tool's default: wide face vertical, sawhorse style):

| Quantity | Value | Exact? |
|---|---|---|
| `c` | **13″ = 13312** | yes |
| `z = d × Z = (4, −3, 0)`, `y = (−36, −48, 25)` | — | — |
| `n_z` for `Z` | 0 → **plain mitre** | — |
| `s = 1½ × 5 / 12` | **5/8″ = 640** | yes |
| `L = 13 + 5/8` | **13 5/8″ = 13952** | yes |
| `α = atan(5/12) = 22.6199°` | **≈ 22½°** | no |
| plan azimuth `atan(4/3) = 53.1301°` | readout **≈ 53°** | no |

One row: `Leg | 4 | 13 5/8 × 1½ × 1½ | 2x2 | Mitre both ends: 5/8″ in … (≈ 22½° off square).`

**Board 2 — `Reference = X`** (wide face parallel to the long side, magazine style): the same
four points, the same two exact planes, a different board.

| Quantity | Formula | Value |
|---|---|---|
| `z = d × X` | `(0, 12, −4)`, `|z|² = 160` | integers |
| `y = z × d` | `(160, −12, −36)`, `|y|² = 27040` | integers |
| `n_d`, `n_y`, `n_z` for `Z` | `12/13`, `−36/√27040`, `−4/√160` | `0.92308`, `−0.21893`, `−0.31623` |
| mitre `α` | `atan(|n_y| / n_d) = 13.3424°` | **≈ 13½°**, marked |
| bevel `β` | `asin(|n_z|) = 18.4349°` | **≈ 18½°**, marked |
| per-end term | `(¾ · 0.21893 + ¾ · 0.31623) / 0.92308` | `0.434813″` |
| `L` | `13 + 2 × 0.434813 = 13.869626″` | `14202.497` units → **14202**, `≈ 13 7/8″` at 1/16 |
| long point | `sign(n · y) < 0`, `sign(n · z) < 0` | **south-bottom at the foot, north-top at the seat** |

The row: `Leg | 4 | ≈ 13 7/8 × 1½ × 1½ | 2x2 | Cut both ends at a compound angle: mitre ≈ 13½°,
bevel ≈ 18½°, with the top face on the saw table; long point at the south-bottom corner of the
west end and the north-top corner of the east end.` The four legs' long points fall at four
different corner pairs — (south-bottom, north-top), (north-bottom, south-top), (south-top,
north-bottom), (north-top, south-bottom) — and each is another under a half-turn about local Z
or local X, so §2.5 groups them as **one row of four**.

`14202.497` is 0.003 units from a rounding boundary: this case is kept as the **operation-order
test** — the derivation's fixed order must give exactly `14202`, on every platform, and a
reordering that gives `14203` fails it — and not as the sample's headline number.

### 9.3 Golden cases

`Napkin.Core.Geometry.Tests` unless said otherwise. Assembly-model §9 cases 23–33 stand and are
claimed by slices A–D; these are the additions.

1. **The reference axis.** `d = (0, 7, 24)`: `Reference` `Z` and `X` give frames whose `z` and `y`
   are swapped up to sign; both derive `L = 26048`, `s = 448`, one a `CornerCut` on the wide face,
   the other on the narrow — the same board, `Height`/`Depth` swapped. `d = (3, 4, 12)`: `Z` gives
   plain mitres and `13952`; `X` gives compound ends and `14202`; `Y` gives compound ends and
   `14212` (`α ≈ 18°`, `β ≈ 14°`). Three boards from one pair of points.
2. **Compound reduces to plain.** For every `d` and cut in cases 23–28 of assembly-model §9, the
   general formula of §2.1 with `n_z = 0` gives the same `L` as `c + (s₁ + s₂)/2`, bit for bit.
3. **Coplanarity retired.** `(Z, Y)` cuts with `d = (1024, 9216, 27648)` — assembly-model case 24's
   `StrutNeedsBevel` — is now **accepted** with `Reference = Z`: the `Z` end a plain mitre, the `Y`
   end compound; the test asserts both kinds in one blank.
4. **The half-degree rule.** `atan(7/24)` → "≈ 16½°"; `atan(5/12)` → "≈ 22½°"; equal setbacks →
   "45°" unmarked; `4·(n · z)² = |z|²` → "30°" unmarked bevel; `16.2602` and `16.25` (exactly
   between) round away from zero to `16½`.
5. **Exactness proofs.** Case 9.1's leg: every flag true, no `≈`. Case 9.2 board 1: the same.
   Board 2: `LengthExact` false, both angles marked, and no proof is attempted for a compound term.
6. **Half-turn grouping.** The four `Reference = X` footstool legs group to one row; a fifth leg
   with `d = (3, 4, 12)` and `Reference = Y` does not join them; a leg with `Reference = X` and
   `Depth 1024` does not either.
7. **Long point.** Board 2's corners as §9.2 states; each mirrored leg's corners per §2.2's sign
   rule; a plain mitre reports an edge, not a corner.
8. **Flush to a one-way leg** (`Propagator`). Bench leg, `Reference = Z`: `Flush(rail.Face(West),
   StrutFaceRef(leg, Top))` — the leg's local-Z faces are normal to world X at `4096 ± 768`.
   Resize the rail's width with the rail anchored: both of the leg's ends move in X by the delta,
   `Y` and `Z` untouched, the derived blank equal by value to before. `Flush` to `StrutFace.South`
   (normal to no world axis here): `Rejected(PlacesNotComparable)`, message names the two-way
   claim or the face. The footstool leg (`d` has no zero component): every `StrutFaceRef` is
   refused. A leg with `Depth 1535`: refused, message names the odd size.
9. **Coupling contradiction.** The same `Flush`, then `SetStrutEnd(leg, From, (5120, −4096, 0))`
   moving one end in X only: `OverConstrained`, naming the `Flush` — the lean would stop being
   one-way.
10. **Hidden edges** (`Napkin.Modules.Views.Tests` or wherever `HiddenEdges` lives). The bench in
    Front view: the two far legs' long edges are dashed where the near legs cross them and solid
    elsewhere, judged by planar depth; a leg's own edges are never dashed by its own faces; every
    existing axis-view case gives its previous answer (constant depth is the special case).
11. **Joinery** (`Napkin.Modules.Furniture.Tests`). Footstool, `Reference = Z`: `Joint(butt,
    receiving seat.Face(Bottom), inserted StrutEndFaceRef(leg, To), pocketScrews from
    StrutFace.Bottom, glue)`: satisfied; contact polygon non-empty; joint length once-rounded and
    marked; count `max(2, ⌈L/2⌉)`; the sentence "Drill 2 pocket holes in the east end from the
    bottom face." on the leg's row. A `groove` on a strut end: refused. A joint on a `Square` end:
    refused. Two struts' `Y` ends `Coincident` (an A-frame apex) with a `butt` between their end
    faces: satisfied.
12. **Format** (`Napkin.Core.Project.Tests`). Round-trip of a strut with every `Reference`, a
    `strutFace` in a `Flush`, a `strutEndFace` in a `joint`; each refusal row of §7; a version-10
    file refused by the existing message.
13. **The two samples** (`Napkin.Core.Project.Tests`, `Napkin.Modules.Furniture.Tests`). Both load,
    validate, check with zero violations, `Load(Save(s)) == s`, and give §9.1's and §9.2's rows
    exactly, from hand-derived `expected.json` files. Every committed sample still gives its
    unchanged expectations.
14. **Angle entry** (slice F). Typing tilt 16.2602° and rise 24″ on a one-way leg places the foot
    at `Y = 7168 ± 1` unit and flags the rounding; typing 45° and rise 24″ places it exactly at
    24576 with no flag.

### 9.4 Properties

Extending assembly-model §9.2: for random non-axis-aligned `d` on the grid, each `Reference`, each
`EndCut` pair and random even `Height`/`Depth`: the frame is integer and right-handed; `L ≥ c`;
`L` is invariant under swapping `From`/`To` and under mirroring any component of `d`, up to the
half-turn grouping of the long-point corners; `n_z = 0` iff the general and the plain formulas
agree; `Load(Save(s)) == s`.

### 9.5 GUI workflows

`GUI-STRUT-01` (slice D): draw the bench's seat with the stock tool, place four legs with two clicks
each in the 3D view (feet on the grid, tops on the seat's lower edge), open the cut list, assert
one row of four legs at 25 7/16″. `GUI-STRUT-02` (slice F): the footstool by the angle entry mode,
switch one leg's reference to `X` in the panel, assert its row leaves the group and shows a
compound sentence.

---

## 10. Risks

- **The per-end propagation rule** (assembly-model §3a.5) is the one piece of this design where a
  wrong implementation is a silent geometric error rather than a refusal. Mitigation: it is its own
  slice (B), its cases are pinned before drawing exists, and the slice says when to escalate.
- **A signed-off design is being amended.** §1.2, §1.3, §1.4, §2.5, §3.2 and §5 change §3a's text;
  each is a numbered decision in §11 so Marc accepts or declines it explicitly. If he declines all
  of them, slices A–D still build §3a as signed off and E is unaffected.
- **Two boards from one placement** (§1.2) is a new idea for a user: "the same four points, a
  different leg". The tool's default keeps the sawhorse answer and the three plain-words choices
  are in the panel; whether that is enough is a UI question slice D will surface.
- **Scope pull toward the excluded list** (§6): the tapered splayed leg and the let-in brace are
  the two things people will ask for next. Both are named with their reason; neither is a small
  change.
- **Platform determinism of displayed angles** (§2.4): a golden string holding "≈ 16½°" could in
  principle flip on a runtime whose `atan` differs at the boundary; the fixtures' angles are
  tenths of a degree from any boundary, and lengths are deterministic by construction. CI on
  Windows is the after-the-fact check.
- **`HiddenEdges` generalisation** (§4) touches a pure class with a completeness argument; the
  argument is restated for planar depth and the axis-view cases must give byte-identical output.
- **`double` in one more place.** `StrutBlank.Derive` already holds the kernel's one `double`
  (§3a.4); the compound formula adds terms to it, not a second site. The propagator, checker and
  loader still never call it except for invariant 17.

---

## 11. Decisions for Marc

In plain words, each with a recommended default that stands until he says otherwise. Beta policy
makes changing any of them cheap.

1. **When a leg leans two ways, which of its faces stays square to the room?** A leg that leans
   both sideways and forwards can be made two ways: with its wide face *straight up and down*
   (then each end is a single angled cut, like a sawhorse leg), or with its wide face *parallel to
   the seat's side* (then the aprons screw on flat, and each end is a compound cut — the magazine
   stool). napkin has to know which, because they are different pieces of wood. **Recommended:**
   store the choice on the leg (§1.2); the tool picks "straight up and down" unless told
   otherwise, and the panel offers the three choices in plain words. *Alternative:* always the
   sawhorse way, as the signed-off design does — simpler, and the magazine stool is not
   designable.
2. **Allow compound cuts on a leg's end?** The signed-off design refuses a leg whose end cut would
   need the blade tilted as well as swung. This note allows it and prints both angles (§1.3, §2.2).
   **Recommended:** allow. It is a description of the board napkin worked out, not a stored bevel,
   so the rule that a box never has a bevel is untouched. *Alternative:* keep the refusal; then
   decision 1 collapses to its alternative.
3. **Snapping a part flush to a leaning leg.** When a leg leans one way only, two of its faces are
   still square to the room and a rail can be snapped flush to them exactly; when it leans two
   ways, none are, and napkin says "can't hold this" rather than guessing (§3.2). **Recommended:**
   yes to the one-way case, refuse the two-way case by name. The one wrinkle: the face sits half
   the leg's thickness off its centreline, so a hand-typed thickness that is an odd number of
   1/1024″ is refused for this (no real stock is). *Alternative:* no flush to any leaning leg —
   the signed-off text — and a raked chair's rails are placed by distance instead.
4. **Angles on the cut list: to the nearest half degree.** A mitre saw's scale is in whole degrees
   with a detent at 22½°; napkin shows "≈ 16½°" rather than "16.26°" (§2.4). **Recommended:** half
   degrees, marked `≈` unless the angle is exactly 45° (or a bevel exactly 30°). *Alternative:*
   whole degrees (loses 22½°), or a tenth of a degree (claims a precision no saw has).
5. **How honest to be about a compound leg's length.** For a single angled cut napkin proves when a
   length is exact (a 3-4-5 leg prints no `≈`). For a compound cut it always prints `≈` and does
   not try to prove otherwise (§2.1). **Recommended:** always `≈` for compound ends. *Alternative:*
   prove those too — more arithmetic for a case that is almost never exact.
6. **Four legs, one row.** A magazine stool's four compound-cut legs are the same board turned
   over or end for end, and napkin lists them as one row of four (§2.5), the way it already treats
   pocket-hole patterns. **Recommended:** yes. *Alternative:* list mirror images separately, as it
   does for a box with a chamfer on one corner (shaped-parts decision 6) — four rows of one for a
   stool would look wrong.
7. **An angled shelf's "length".** A tilted shelf is stored across its tilt, so the number napkin
   works out is the shelf's *width*; the signed-off rule insists that number is called the length.
   **Recommended:** let the person name it width (§1.4 invariant 16), so the row reads
   30″ × 10″ and not 10″ × 30″. *Alternative:* keep the rule and accept the odd row.
8. **Joinery on an angled leg: butt joints only.** Pocket screws or screws or dowels through the
   leg's angled end into the seat; no grooves, rabbets, half-laps or tabletop clips on a leaning
   part (§5). **Recommended:** butt only. *Alternative:* none at all until someone asks — but the
   pocket-screwed splayed leg *is* the DIY stool.
9. **The stated angle is typed, not stored.** A person types "lean 15°"; napkin places the foot,
   rounds it to the grid once, and from then on shows "≈ 15°" worked back from the points (§1.1).
   **Recommended:** yes, as slice F. *Alternative:* also remember the typed angle — two numbers
   for one leg that can disagree, which the rest of napkin forbids.
10. **What stays out** (§6): curved parts, a freely rotated box, a leg rolled to any angle but the
    three axes, a tapered splayed leg, braces mitred to each other, a bevel on a box, the bent
    chair leg, joinery beyond a butt, a stability check. **Recommended:** all out, each named with
    its reason. Each has a later home (#28, #67, #120, #154); none is a small addition.
11. **Escalation on slice B.** The propagator slice is Sonnet-sized with the design in hand, but if
    its per-end tests do not converge in one session it goes to Opus (§8). **Recommended:** as
    stated. *Alternative:* Opus from the start, at the cost of the other five slices' budget.
