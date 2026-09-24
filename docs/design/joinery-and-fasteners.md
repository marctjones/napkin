# Joinery, fasteners and hardware: sticking pieces of stock together

Status: **DRAFT, awaiting Marc's sign-off.** Written for issues #136 (joinery) and #137
(fasteners, glue and hardware) together, because a fastener is what a joint is held with and
neither can be designed alone. Nothing here is implemented. §11 lists the slices; the follow-up
issues are filed against them.

Design document written by Fable per [`PLAN.md`](../../PLAN.md). It decides how a joint between
two parts is stored, kept under resize, drawn, listed in the cut list with the right finished
sizes and the right cut sentences, and how the fasteners, glue and hardware a build needs reach
the shopping list. The worked example throughout is a **DIY coffee table with two drawers**
(§2), built from stock the shipped materials library carries with a saw, a drill, a pocket-hole
jig, clamps and glue.

Settled decisions taken as given and not re-argued: exact lengths on the 1/1024-inch grid
([`geometry-model.md`](./geometry-model.md) §1); a box is parametric and stores the three sizes
that were typed ([`../file-format.md`](../file-format.md), "Entities"); a part is fields on a
box, with two in-plan dimensions on the box and one out of plane
([`parts-and-cut-list.md`](./parts-and-cut-list.md) §1.1); the cut list reads stored parameters,
never derived corners (`CUT-002`), groups equal parts into one row (§3 step 4) and its sentences
name corners, edges and faces by compass **in the part's own frame as drawn**
([`shaped-parts-model.md`](./shaped-parts-model.md) §4.4, `CutDescription`); relationships are
stored data behind one update interface (`geometry-model.md` §3–§4); two boxes that overlap are
allowed and not flagged ([`assembly-model.md`](./assembly-model.md) §6); strict reading with no
migration and no shims (DESIGN.md §12); expectations in `samples/` are hand-derived and never
regenerated from napkin's output (`samples/README.md`).

**The data rule (CLAUDE.md) governs every number in this note.** No fastener size, screw length,
brad length, clip dimension, slide clearance or pack size is stated here as a fact. Where the
worked example needs one, it is the *builder's typed choice*, labelled as such, and napkin carries
no default for it. The count rules in §7 ("two pocket screws per joint, plus one per …") are
**napkin's own design defaults**, not sourced facts; each is labelled and user-editable.

---

## 1. Simplicity rules — what napkin will not model

A joint in napkin is **a label on the place where two parts touch, plus a few numbers**. It is
not geometry. Specifically, and so that scope does not creep:

1. **No solid modelling of the joint.** No CSG, no boolean subtraction, no tenon or groove drawn
   into the 3D solid. A grooved drawer side draws as the same box it draws today; the groove is a
   sentence in the cut list and a marker on the screen.
2. **No tolerances, no fit, no clearances.** A tenon is the size of the mortise; a groove is the
   thickness of the panel. Anyone who wants a 1/64″ easier fit types it into the saw, not into
   napkin.
3. **No tool paths, no bit or blade sizes, no machine setups.** A sentence says "groove 1/4″ wide,
   1/4″ deep, 1/2″ from the bottom edge"; how that is cut (router, table saw, three passes) is the
   person's business.
4. **No joint-strength engineering.** No screw withdrawal values, no glue-line calculations, no
   "this joint is too weak". M4/M5's rules engine is for structural sizing of building members,
   not for furniture joints.
5. **No fastener placement.** A joint says "three pocket screws"; it does not say where along the
   joint each one goes.
6. **Joints never move parts.** A joint is not a constraint. Parts are held touching by the
   relationships that exist today (`flush`, `axisDistance`, `coincident`); a joint records what
   happens at a contact and reports when the contact has gone away (§4.3).
7. **Joints never generate `Cut`s.** A half-lap is a notch by another name, and #120 may one day
   add a notch cut for drawing; if it does, deriving a drawn notch from a half-lap joint is a
   later slice, not this design.
8. **Nothing is guessed from geometry.** Two parts touching are not joined until a person says so;
   a joint is never inferred, only offered (§5.1).

---

## 2. The worked example: a DIY coffee table with two drawers

Overall 42″ × 22″ × 17″ high. A 3/4 plywood top on four 2x2 legs; 1x6 aprons on three sides; the
front is a 1x2 rail over two drawers side by side, separated by a 1x6 web running front to back.
Drawer boxes are 1/2 plywood with a 1/4 plywood bottom in a groove, on side-mount slides, with
1x6 overlay fronts. Every stock name below **exists in the shipped library**
(`src/Napkin.Core.Materials/data/softwood-lumber.json`, `sheet-goods-plywood.json`): 1x2, 1x6,
2x2, 1/4, 1/2 and 3/4 plywood. Actual sizes are the library's PS 20-25 dry sizes and PS 1-19
thicknesses: 1x2 = 3/4″ × 1 1/2″, 1x6 = 3/4″ × 5 1/2″, 2x2 = 1 1/2″ × 1 1/2″.

The library **lacks nothing this table needs** in lumber and sheet goods. It lacks every fastener
the table needs (§7.6) and all hardware; the design works around that by never needing a
fastener *dimension* from the library, only a count.

### 2.1 Layout

Plan view, origin at the top's south-west corner, X east, Y north, Z up, floor at Z = 0. The leg
frame is inset 1 1/2″ from the top's edge all round: outer 39″ × 19″. Aprons are flush with the
outside faces of the legs. All dimensions are finished dimensions of this design.

```
  y
  ^                           42"
  |  +------------------------------------------------------+  --+
  |  |  +--+     back apron, 1x6, 36"                 +--+  |    |
  |  |  |  |=========================================|  |  |    |
  |  |  +--+  ||                 web  ||            +--+  |    |
  |  |   side  ||     drawer A   1x6  ||  drawer B   side |   22"
  |  |   apron ||   (16 5/8 wide) 17 1/2  (16 5/8)  apron|    |
  |  |   1x6   ||                     ||             1x6  |    |
  |  |  +--+  ||                      ||            +--+  |    |
  |  |  |  |=========================================|  |  |    |
  |  |  +--+     front rail, 1x2, 36"  (drawer fronts overlay it) |
  |  +------------------------------------------------------+  --+
  +--------------------------------------------------------------> x
```

Heights (Z): legs 0 → 16 1/4; top 16 1/4 → 17; aprons and web 10 3/4 → 16 1/4 (5 1/2 tall,
top edges flush under the top); front rail 14 3/4 → 16 1/4; drawer boxes 11 → 14 1/2 (3 1/2
tall, 1/4 below the rail); drawer fronts 10 3/4 → 16 1/4; slide cleats 12 → 13 1/2.

Widths (X): legs 1 1/2 → 3 and 39 → 40 1/2 at each end; the web is centred, 20 5/8 → 21 3/8.
Each drawer cavity is 17 5/8 wide (from the leg's inside face to the web). The builder chose
side-mount slides and typed their length and side clearance from the slide's own packaging —
**16″ long, 1/2″ per side** — so each drawer box is 16 5/8 wide × 16 deep. Napkin carries no slide
data; those two numbers are the builder's, and the sample's `design.md` says so. A 3/4″ cleat on
the inside of each side apron brings the slide's mounting surface flush with the leg's inside
face (the apron is 3/4″ thick, the leg 1 1/2″).

Drawer fronts: 17 3/4″ wide, 1/8″ from the leg, meeting over the web with a 1/4″ gap.

### 2.2 Every part

Drawn sizes are what the box stores; **finished** sizes are what the cut list prints after the
joint allowances of §6.1. `qty` is the box's `part.quantity` × the number of boxes drawn: both
drawers are drawn, so the drawer parts are separate boxes.

| # | Part (name in the file) | Qty | Stock | Drawn L × W × T | Finished | Notes |
|---|---|---|---|---|---|---|
| 1 | Top | 1 | 3/4 plywood | 42 × 22 × 3/4 | same | |
| 2 | Leg, south-west … north-east | 4 | 2x2 | 16 1/4 × 1 1/2 × 1 1/2 | same | 16 1/4 + 3/4 = 17 |
| 3 | Apron, back | 1 | 1x6 | 36 × 5 1/2 × 3/4 | same | 39 − 2 × 1 1/2 |
| 4 | Apron, side, west / east | 2 | 1x6 | 16 × 5 1/2 × 3/4 | same | 19 − 2 × 1 1/2 |
| 5 | Front rail | 1 | 1x2 | 36 × 1 1/2 × 3/4 | same | |
| 6 | Web | 1 | 1x6 | 17 1/2 × 5 1/2 × 3/4 | same | 19 − 3/4 − 3/4 |
| 7 | Slide cleat, west / east | 2 | 1x2 | 16 × 1 1/2 × 3/4 | same | between the legs |
| 8 | Drawer side, left, A / B | 2 | 1/2 plywood | 16 × 3 1/2 × 1/2 | same | grooved, rabbeted |
| 9 | Drawer side, right, A / B | 2 | 1/2 plywood | 16 × 3 1/2 × 1/2 | same | mirror of 8 |
| 10 | Drawer box front, A / B | 2 | 1/2 plywood | 15 5/8 × 3 1/2 × 1/2 | **16 1/8** × 3 1/2 × 1/2 | + 1/4 rabbet each end |
| 11 | Drawer box back, A / B | 2 | 1/2 plywood | 15 5/8 × 3 1/2 × 1/2 | same | |
| 12 | Drawer bottom, A / B | 2 | 1/4 plywood | 15 5/8 × 15 × 1/4 | **16 1/8 × 15 1/2** × 1/4 | + 1/4 groove all round |
| 13 | Drawer front, A / B | 2 | 1x6 | 17 3/4 × 5 1/2 × 3/4 | same | overlay |

15 5/8 = 16 5/8 − 2 × 1/2 (the box's inside width); 15 = 16 − 1/2 − 1/2 (inside depth).
An optional lower shelf (3/4 plywood on 1x2 stretchers) is a natural addition and is **not** in
the sample, to keep the hand-derived expectations short.

### 2.3 Every joint

`Receiving` is the part that is cut or that the fastener goes into; `inserted` is the part whose
end or face enters or bears on it (§4.1 says how that is decided). *Contact* is the rectangle
where the two faces touch, derived from the drawn boxes; its long side is the **joint length**
the count rules of §7.2 read.

| J | Receiving ← inserted | Type | Fastening | Glue | Depth | Contact | Count |
|---|---|---|---|---|---|---|---|
| J1 ×2 | Leg (inside face) ← Apron, back (end) | butt | pocket screws | yes | — | 3/4 × 5 1/2 | 3 each |
| J2 ×4 | Leg ← Apron, side (end) | butt | pocket screws | yes | — | 3/4 × 5 1/2 | 3 each |
| J3 ×2 | Leg ← Front rail (end) | butt | pocket screws | yes | — | 3/4 × 1 1/2 | 2 each |
| J4 ×1 | Apron, back (south face) ← Web (north end) | butt | pocket screws | yes | — | 3/4 × 5 1/2 | 3 |
| J5 ×1 | Front rail (north face) ← Web (south end) | butt | pocket screws | yes | — | 3/4 × 1 1/2 | 2 |
| J6 ×2 | Apron, side (inside face) ← Slide cleat (face) | butt | screws | yes | — | 16 × 1 1/2 | 3 each |
| J7 ×4 | Drawer side (front end, inside face) ← Drawer box front (end) | rabbet | brads | yes | 1/4 | 1/2 × 3 1/2 | 3 each |
| J8 ×4 | Drawer side (inside face) ← Drawer box back (end) | butt | brads | yes | — | 1/2 × 3 1/2 | 3 each |
| J9 ×8 | Drawer side / box front / box back (inside face) ← Drawer bottom (edge) | groove | none | no | 1/4 | 1/4 × 16 or 15 5/8 | — |
| J10 ×2 | Drawer front (back face) ← Drawer box front (front face) | butt | screws | no | — | 15 5/8 × 3 1/2 | 3 → **4** (overridden) |
| J11 ×4 | Top (bottom face) ← Apron, back / side ×2 / Front rail (top edge) | tabletop | clips | no | — | 36, 16, 16, 36 long | 3, 2, 2, 3 |

34 joints; 20 of them glued (J1–J8). The counts are §7.2's defaults; J10 shows the per-joint
override. Hardware, not joints: one pair of slides per drawer box (attached to the box front
part), one pull per drawer front (§7.5).

---

## 3. The joint catalogue, at DIY level

A joint has a **type** (what it does to the wood) and a **fastening** (what holds it). They are
orthogonal, within the table of allowed pairs in §3.3: a butt joint can be held with pocket
screws, screws through the face, brads, dowels or nothing but glue, and the count recipe of §7
reads the pair.

### 3.1 Types

| Type | Plain words | Effect on finished sizes | Cut sentence on the receiving part | Parameters | First? |
|---|---|---|---|---|---|
| `butt` | One part's end or face is stuck against another's face. Nothing is cut. | none | none (pocket holes are a fastening sentence, §6.2) | — | **yes** |
| `groove` | A slot along a part's length that a panel's edge sits in (drawer bottom). Also called a dado when it runs *across* the part (a shelf into a side); napkin uses one type and words the sentence by direction (§6.2). | the **inserted** panel grows by `depth` on each grooved edge | "Groove the … face: *w* wide, *d* deep, *o* from the … edge, full length." | `depth` | **yes** |
| `rabbet` | A step cut along an end or edge so the other part's end sits in it (drawer box front into the sides). | the **inserted** part grows by `depth` at each rabbeted end | "Rabbet the … end on the … face: *w* wide, *d* deep." | `depth` | **yes** |
| `halfLap` | Two parts cross or meet with half the thickness cut from each so they lie in one plane. Drawn **overlapping** (assembly-model §6 allows it); the overlap *is* the lap. | none — drawn is finished | on **both**: "Half-lap the … face at the … end: *l* long, *d* deep, across the width." | none; each lap is half its own part's thickness; refused if the thicknesses differ | **yes** |
| `tabletop` | A solid or sheet top held to an apron so it can move with the seasons: clips in a slot or recess, or screws in slotted holes. It exists as a type because screwing a top down rigidly is the most common DIY mistake. | none | "Fit *n* tabletop clips along the top edge on the … face (slot or recess per the clip's instructions)." | none | **yes** |
| `edge` | Two boards glued edge to edge into a wider panel (a solid top from 1x6s). | none | none | — | later |
| `tenon` | A tongue on the end of a rail into a slot (mortise) in a leg. | the **inserted** rail grows by `depth` at each tenoned end | "Cut a mortise on the … face: *w* wide, *l* long, *d* deep, *o* from the … end." and on the rail "Cut a tenon at the … end: *d* long, *t* thick, *l* wide." | `depth`, `tenonThickness` (design default: a third of the rail's thickness, rounded to the grid) | later |
| `dovetail` | Interlocking pins and tails at a corner. | none (through dovetails: the side's drawn length already includes the front's thickness) | "Dovetail the … end." | none | later |
| `mitre` | Not a joint type: a mitre is two `CornerCut`s (shaped-parts §4.4) plus a `butt` with glue and brads. | — | — | — | already |

Smallest useful first set, in this order: **butt, groove, rabbet, halfLap, tabletop** — the
coffee table needs four of them, and the half-lap is what a stretcher frame or an X-brace needs.
`tenon` is the first later type; `edge` and `dovetail` after it. Dowels and biscuits are
fastenings of a `butt` or `edge` joint, not types.

### 3.2 Fastenings

| Fastening | Plain words | Count recipe reads | Needs a cited size table (§7.6) |
|---|---|---|---|
| `none` | held by the joint itself (a panel in a groove) | — | — |
| `pocketScrews` | screws driven through angled pocket holes in the inserted part | joint length | pocket screw length by stock thickness |
| `screws` | screws through one face into the other | joint length | wood screw gauge × length |
| `brads` | thin nails from a brad nailer or hammer | joint length | brad length (FF-N-105B covers brads) |
| `nails` | common or finish nails | joint length | `fasteners-common-nails.json` exists, unverified (#119) |
| `dowels` | wooden pins in matched holes | joint length | dowel diameter and length |
| `biscuits` | compressed wooden ovals in slots | joint length | biscuit size |
| `clips` | tabletop fasteners: figure-8, Z-clip, or screws in slotted holes | joint length | clip dimensions; none built in |

Glue is a **separate boolean** on the joint, not a fastening, because it accompanies most of the
others (pocket screws *and* glue) and is deliberately absent on some (a tabletop joint, a drawer
front you want to adjust, a panel in a groove).

### 3.3 Allowed pairs

| | none | pocketScrews | screws | brads | nails | dowels | biscuits | clips |
|---|---|---|---|---|---|---|---|---|
| butt | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | |
| groove | ✓ | | | ✓ | | | | |
| rabbet | ✓ | | ✓ | ✓ | ✓ | | | |
| halfLap | ✓ | | ✓ | | | ✓ | | |
| tabletop | | | | | | | | ✓ |
| edge (later) | ✓ | | | | | ✓ | ✓ | |
| tenon (later) | ✓ | | | | | ✓ | | |

A pair outside the table is refused by `Sketch.Validate` and greyed out in the popover (§5).

---

## 4. How a joint is represented

### 4.1 Options weighed

**(a) A feature on one part**, like a `Cut`. Rejected: every joint has two parts, and the numbers
a sentence needs — the groove's width and where it sits — come from the *other* part's drawn box.
A groove stored on the drawer side alone would have to duplicate the bottom's thickness and
position, and go stale when the bottom moved.

**(b) A new entity type.** Rejected: entities have a layer, a name and a place; a joint has none of
those. It would need its own selection, deletion and undo paths.

**(c) A new relationship kind — recommended.** A relationship already relates two places by id,
lives in the sketch's relationship list, is saved, loaded, undone and redone with no new code, is
checked by `RelationshipChecker`, and refers to faces with the `feature` reference the format
already has. The only new work is telling the updater that this kind is neither propagated nor a
reason to refuse (§4.3).

**(d) Derived from touching faces.** Rejected by Simplicity rule 8: touching is not joining, and
pocket screws versus glue is a person's choice.

So:

```csharp
public sealed record Joint(
    RelationshipId Id,
    FeatureRef Receiving,      // exactly one face
    FeatureRef Inserted,       // exactly one face
    JointType Type,            // Butt, Groove, Rabbet, HalfLap, Tabletop (later: Edge, Tenon, Dovetail)
    Length? Depth,             // required for Groove, Rabbet, Tenon; null otherwise
    Fastening Fastening,       // Kind + Count (null = recipe) + PocketFace (pocketScrews only)
    bool Glue) : Relationship(Id);

public sealed record Fastening(FasteningKind Kind, int? Count, BoxFace? PocketFace);
```

**Which is receiving and which inserted.** The faces are stored, not derived, so a file is
unambiguous. The tool proposes them (§5.1): for a butt, rabbet or tenon the inserted part is the
one whose contact face is an **end** (a face perpendicular to its `length`), the receiving part
the one presenting a long face; for a groove the inserted part is the thinner one (the panel); for
a halfLap and an edge joint the order does not matter and the tool stores the lower id first; for
a tabletop the receiving part is the top (the one whose `bottom` face is in the contact). When
both or neither are ends (a face-to-face butt like the slide cleat), the tool asks, defaulting to
the larger part as receiving.

### 4.2 What is derived, never stored

From the two drawn boxes and the two faces, the geometry module derives (`JointGeometry.Of`):

- the **contact rectangle** — the intersection of the two faces' world rectangles, which must be
  non-empty and coplanar (the faces must fix the same world axis at the same coordinate, exactly
  as a `flush` is judged); its long side is the **joint length**, its short side the joint width;
- for a **groove**: its width = the inserted part's thickness; its offset = the distance from the
  contact rectangle to the nearer parallel edge of the receiving face; its direction — along the
  receiving part's `length` (a groove) or across it (a dado, worded so);
- for a **rabbet**: its width = the inserted part's thickness; which end of the receiving part it
  is at;
- for a **halfLap**: the overlap box of the two drawn boxes; its length along each part; and the
  check that both parts are the same thickness;
- for **pocket screws**: which end of the inserted part is in the contact.

Nothing above is written to the file. Resize the drawer bottom and the groove offset follows;
move the drawer side and the contact moves with it.

### 4.3 How a joint survives resizing, and when it is unsatisfied

A joint references features, never coordinates, so every edit that keeps the two faces touching
keeps the joint. `RelationshipChecker.Evaluate` gains one arm: the residual is **zero when the
contact rectangle is non-empty and coplanar, and `NotEvaluable` otherwise** — an unsatisfied joint
draws hollow (§5.3) and the cut list still applies its allowance but marks the row (§6.4). It is
never deleted for you; a person who moved the web 1″ north wants to be told, not tidied.

The updater (`DirectUpdater`, `Propagator.IsUnsatisfied`) gets a named arm for `Joint` that
**neither propagates it nor refuses a request because of it**. This is the one place the existing
code needs care: today a check-only relationship (a `horizontal` on a box edge) makes the updater
refuse a geometry request (file-format.md, "Relationships"); a joint must not, or one joint would
freeze editing. Deleting either part deletes the joint, as deleting a node deletes the
relationships on it today. **Duplicating a set of parts duplicates the joints among them**
(shaped-parts §2.6: a duplicate is a value copy of the whole) — that is how drawer B is made from
drawer A in §9 — and a joint between a copied part and an uncopied one is not duplicated.

### 4.4 In the file

One bump, **`formatVersion` 4 → 5**, covering everything in this note: the `joint` relationship
kind, `hardware` on a part (§7.5), `fastenerChoices` and `supplies` at the scene root (§7.3, §8).
Every field required; a version-4 file is refused with the existing message. Slice A updates
`formatVersion` in every `samples/*.scene.json` and `*.expected.json` in the same commit.

```json
{ "id": "…", "kind": "joint", "type": "rabbet",
  "receiving": { "kind": "feature", "box": "<drawer side A, left>", "faces": ["east"] },
  "inserted":  { "kind": "feature", "box": "<drawer box front A>",  "faces": ["west"] },
  "depth": 256,
  "fastening": { "kind": "brads", "count": null, "pocketFace": null },
  "glue": true }
```

| Field | Type | Refused when |
|---|---|---|
| `type` | one of `butt`, `groove`, `rabbet`, `halfLap`, `tabletop` (later `edge`, `tenon`, `dovetail`) | unknown value |
| `receiving`, `inserted` | a `feature` reference with exactly one face, on two different boxes | any other reference kind, two or three faces, the same box twice |
| `depth` | integer > 0, or `null` | `null` for a type that needs it, non-null for a type that does not, ≤ 0, or ≥ the receiving part's thickness |
| `fastening.kind` | one of §3.2 | unknown, or a pair outside §3.3 |
| `fastening.count` | integer ≥ 1 or `null` (use the recipe) | 0 or negative; non-null when `kind` is `none` |
| `fastening.pocketFace` | a face name or `null` | non-null unless `kind` is `pocketScrews`; a face that is `inserted`'s own face or its opposite |
| `glue` | boolean | not a boolean |

Whether the two faces *touch* is **not** checked at load — position is geometry, and a file with a
joint whose parts have drifted apart opens and shows it unsatisfied, the way a `flush` that no
longer holds does. Whether the pair of faces *could* touch (both fix the same world axis) is
checked at load, the way `flush` is.

---

## 5. Creating a joint, and what it draws

### 5.1 The fewest gestures

**Select the two parts, press J, choose, Enter.** Select part A, shift-click part B (or drag a
marquee over both), press **J** (menu: Part → Join…). If the two do not touch — no contact
rectangle on any pair of their faces — the status line says "Apron, back and Leg, north-east
don't touch" and nothing opens. If they touch on exactly one pair of faces, a popover opens at the
contact's midpoint:

```
 Join  Apron, back  →  Leg, north-east          (swap ⇄)
 Type       (•) Butt  ( ) Groove  ( ) Rabbet  ( ) Half-lap  ( ) Tabletop
 Fastening  [Pocket screws ▾]    Count [recipe: 3]    Pocket holes from [south face ▾]
 Depth      [      ]  (greyed for a butt)
 [x] Glue
                                          Enter to join · Esc to cancel
```

The popover pre-selects the type the contact suggests (an end on a face: butt; a thin panel's
edge on a face: groove; the overlap of two same-thickness parts: half-lap; a `bottom` face on a
top edge: tabletop), the direction by §4.1's rule, the pocket face by the default in §6.2, and
the count the recipe gives, shown as "recipe: 3" so a typed number is visibly an override.

**Join all touching (Shift+J).** With three or more parts selected, one popover applies one type
and fastening to **every touching pair** among them, listing the pairs it will make ("8 joints:
Apron, back → Leg, north-east, …"). That is how the leg frame is joined in one gesture (§9 step
5). Pairs whose suggested type differs from the chosen one are listed and unticked by default.

**Repeat last (Shift+J on two parts)** applies the previous joint's settings without the popover.

A "drag one part onto another with a joint tool" was considered and rejected: it collides with
the move tool, and a joint's two parts are already in position when the person thinks of joining
them.

**Editing:** click a marker to select the joint; Enter or double-click reopens the popover;
Delete removes it. The properties panel of a selected joint shows the same fields.

### 5.2 Where it is drawn

A **marker at the centre of the contact rectangle**, in plan and in the 3D view and the standard
views (standard-views.md draws the same scene through locked cameras, so nothing is view-specific):
a small circle in the carpenter's-pencil stroke with a one-letter code — **B** butt, **G**
groove, **R** rabbet, **L** half-lap, **T** tabletop — and a short tick toward the inserted part.
Solid when satisfied, hollow when the faces no longer touch (§4.3). Markers scale with the view
like dimension text (a fixed screen size), never with the model. Hidden below a zoom where two
markers would overlap. The parts view (parts-view.md) draws each cell's markers at the cell's
edge where the joint is, which is where a person looks to see "this end gets pocket holes".

### 5.3 The tooltip

Hover or select: "Rabbet — Drawer side, left, A ← Drawer box front A. 1/4″ deep, 1/2″ wide.
3 brads (size not chosen), glue." The fastener size text is the choice from §7.3 or "size not
chosen". Unsatisfied: the first line becomes "Rabbet (parts no longer touch)".

No geometry is drawn for any joint: Simplicity rule 1.

---

## 6. Effect on the cut list

`CutList.Of(sketch, library)` stays a pure function and gains the joints as input through the
sketch it already takes. Three things change: finished sizes carry allowances, rows carry joinery
sentences, and the grouping key includes the joinery.

### 6.1 Allowances, stated exactly

For each joint, the **inserted** part's finished size grows along the axis normal to the contact
face, by the joint's `depth`, on that end or edge only:

| Type | Which part grows | By how much | Along which dimension |
|---|---|---|---|
| butt, halfLap, tabletop, edge, dovetail | none | — | — |
| groove | inserted | `depth` per grooved edge | the panel's dimension normal to the receiving face |
| rabbet | inserted | `depth` per rabbeted end | the inserted part's `length` (its end is in the contact) |
| tenon (later) | inserted | `depth` per tenoned end | `length` |

Drawn boxes never overlap for these types (they touch); the allowance is added at list time and
is the hidden material inside the other part. The drawn box is the visible, shoulder-to-shoulder
size, which is what a person draws and dimensions. **Finished = drawn + allowances**, and the row's
`Length`, `Width`, `Thickness` are the finished values; the drawn values stay in the box.

Worked, on §2:

- Drawer box front: drawn length 15 5/8″ = 16000 units; two rabbet joints (J7, one per side) each
  `depth` 1/4″ = 256 at one end → finished 16000 + 256 + 256 = **16512 = 16 1/8″**.
- Drawer bottom: drawn 15 5/8″ × 15″ = 16000 × 15360; four groove joints (J9): two on the length
  (into the sides) and two on the width (into the box front and back), each 256 → finished
  16512 × 15872 = **16 1/8″ × 15 1/2″**.
- Every other part: no allowance; finished = drawn.

An allowance is refused (the joint is unsatisfied, §4.3) when `depth` ≥ the receiving part's
thickness: a 1/4″ groove in 1/4″ plywood is a slot, not a groove.

### 6.2 Sentences, and the phrasing rules

Sentences are derived by `JointDescription.Describe(...)` beside `CutDescription`, in the
**receiving** part's own frame as drawn, with faces and ends named the way `CutDescription`
already names them (compass, `top`, `bottom`; an *end* is a face perpendicular to the part's
`length`, an *edge* one perpendicular to its `width`) and lengths rendered by `CutListCsv.Text`
with the same ≈ marker. Fixed forms, one per joint, so a test can assert them:

| On | Form |
|---|---|
| groove, along the length | "Groove the *face* face: *w* wide, *d* deep, *o* from the *edge* edge, full length." |
| groove, across (a dado) | "Dado the *face* face: *w* wide, *d* deep, *o* from the *end* end, across the width." |
| rabbet | "Rabbet the *end* end on the *face* face: *w* wide, *d* deep." |
| halfLap (both parts) | "Half-lap the *face* face at the *end* end: *l* long, *d* deep, across the width." or "… *o* from the *end* end: …" when the lap is not at an end |
| tabletop (on the apron) | "Fit *n* tabletop clips along the top edge on the *face* face (slot or recess per the clip's instructions)." |
| pocketScrews (on the inserted part) | "Drill *n* pocket holes in the *end* end from the *face* face." — consecutive sentences on the same face merge: "Drill 3 pocket holes in the west end and 3 in the east end, from the south face." |
| an allowance (on the inserted part) | "Length includes 1/4″ into a rabbet at each end." / "… at the north end." / "Length includes 1/4″ into a groove at each end; width includes 1/4″ into a groove at each edge." |
| butt, edge, screws, brads, nails, dowels, biscuits, clips (the count), glue | **no cut-list sentence** — these are assembly, and live on the marker's tooltip and the fastener list |

Only operations done to the blank at the bench are cut-list sentences; that is what keeps four
legs one row (nothing is cut into a leg) while the aprons say where their pocket holes go.

*o* is the offset from the nearer parallel edge of the receiving face, derived (§4.2). *w* is
the inserted part's thickness. Order within a row: allowance sentences first, then joint
sentences by the receiving face in the order `south, east, north, west, bottom, top`, then by
offset.

**Pocket face default.** `pocketFace` is stored (§4.4) because it cannot be inferred; the popover
defaults it to the inserted part's face **nearest the centroid of all parts in the sketch** (the
inside of an apron), and when two faces tie — the web, at the centre — the popover shows both and
the person picks. Napkin's default, editable, and the sample records the builder's pick.

### 6.3 Grouping, and mirrored pairs

The group key of parts-and-cut-list §3 step 4 gains the part's **joinery**: its allowances and
its sentences' structural form (type, face, end, depth, width, offset, count), wrapped for value
equality the way `CutSequence` wraps `Cuts`. Two parts with the same size and stock but different
joinery are two rows.

Two joinery sets are **equal when one is the other under a half-turn of the part about any of its
own three axes** (end-for-end, face-for-face, edge-for-edge): those are the ways a person can pick
up an identical blank and use it in the other position. The row prints the sentences of its first
member in id order. Consequences on §2, both intentional:

- The two side aprons have pocket holes at both ends "from the east face" (west apron) and "from
  the west face" (east apron). A half-turn end-for-end maps one to the other → **one row of two**.
- The drawer sides have a groove near the bottom edge on the inside face and a rabbet at the front
  end on that face. No half-turn maps a left side to a right side (the groove's offset from the
  bottom pins the top; the rabbet pins the front) → **two rows of two**, "Drawer side, left" and
  "Drawer side, right". That is the classic mirrored-pair mistake — four identical sides, two of
  them wrong — and napkin's list catching it is the point of putting joinery in the key.

### 6.4 The row and the CSV

`CutListRow` gains `Joinery` (the structural list above, canonical order) and derived `JointText`
(the sentences, `ImmutableArray<string>`), and a `Flags` note "joint not satisfied" when any of
its members' joints is unsatisfied. The table shows `JointText` under `CutText` in the same
column. The CSV gains a `Joinery` column after `Cuts`, sentences joined by "; " and quoted, and
its header line changes from "before saw kerf and joinery allowance" to **"finished sizes:
joinery allowances included; before saw kerf (#138)"** — every committed `cutListCsv` expectation
changes its first line in slice C.

### 6.5 The worked cut list

Sorted by parts-and-cut-list §3 step 5 (length, width, thickness descending, then label):

| Row | Qty | L × W × T | Material | Joinery |
|---|---|---|---|---|
| Top | 1 | 42 × 22 × 3/4 | 3/4 plywood | — |
| Apron, back | 1 | 36 × 5 1/2 × 3/4 | 1x6 | Drill 3 pocket holes in the west end and 3 in the east end, from the south face. Fit 3 tabletop clips along the top edge on the south face (slot or recess per the clip's instructions). |
| Front rail | 1 | 36 × 1 1/2 × 3/4 | 1x2 | Drill 2 pocket holes in the west end and 2 in the east end, from the north face. Fit 3 tabletop clips along the top edge on the north face (…). |
| Drawer front | 2 | 17 3/4 × 5 1/2 × 3/4 | 1x6 | — |
| Web | 1 | 17 1/2 × 5 1/2 × 3/4 | 1x6 | Drill 3 pocket holes in the north end and 2 in the south end, from the west face. |
| Leg | 4 | 16 1/4 × 1 1/2 × 1 1/2 | 2x2 | — |
| Drawer bottom | 2 | 16 1/8 × 15 1/2 × 1/4 | 1/4 plywood | Length includes 1/4″ into a groove at each end; width includes 1/4″ into a groove at each edge. |
| Drawer box front | 2 | 16 1/8 × 3 1/2 × 1/2 | 1/2 plywood | Length includes 1/4″ into a rabbet at each end. Groove the north face: 1/4″ wide, 1/4″ deep, 1/2″ from the bottom edge, full length. |
| Apron, side | 2 | 16 × 5 1/2 × 3/4 | 1x6 | Drill 3 pocket holes in the south end and 3 in the north end, from the east face. Fit 2 tabletop clips along the top edge on the east face (…). |
| Drawer side, left | 2 | 16 × 3 1/2 × 1/2 | 1/2 plywood | Rabbet the south end on the east face: 1/2″ wide, 1/4″ deep. Groove the east face: 1/4″ wide, 1/4″ deep, 1/2″ from the bottom edge, full length. |
| Drawer side, right | 2 | 16 × 3 1/2 × 1/2 | 1/2 plywood | Rabbet the south end on the west face: 1/2″ wide, 1/4″ deep. Groove the west face: 1/4″ wide, 1/4″ deep, 1/2″ from the bottom edge, full length. |
| Slide cleat | 2 | 16 × 1 1/2 × 3/4 | 1x2 | — |
| Drawer box back | 2 | 15 5/8 × 3 1/2 × 1/2 | 1/2 plywood | Groove the south face: 1/4″ wide, 1/4″ deep, 1/2″ from the bottom edge, full length. |

Thirteen rows, 24 pieces. The web's pocket face is the builder's pick (§6.2). Groove offsets:
the drawer bottom sits at Z 11 1/2 → 11 3/4 in a box whose bottom edge is at Z 11, so the groove
is 1/2″ from the bottom edge and 1/4″ wide.

### 6.6 Interactions with the other Woodworking issues

- **#138 cut layout with kerf** reads the *finished* sizes (allowances in) and adds kerf; nothing
  here changes for it, and the CSV header names it.
- **#139 angled parts**: joints in this design are between axis-aligned faces of boxes. A mitred
  end is two `CornerCut`s and the joint at it is a `butt`; a joint on a strut or a bevelled face
  is out of scope until #139 says what such a face is.
- **#140 grain**: the tabletop joint is *about* grain (a solid top moves across it). When #140
  gives a part a grain direction, the tabletop tooltip can say "clips run across the grain" or
  warn; the `edge` type assumes grain along the length. Nothing stored here changes.
- **#67 relationships on cuts**: untouched; joints are not cuts.
- **#120 notch**: a half-lap's sentence describes a notch; if #120 adds a notch `Cut`, a later
  slice may derive one for drawing (Simplicity rule 7).

---

## 7. Fasteners, glue and hardware

### 7.1 How it works, in one paragraph

Fasteners are **derived from joints by recipes**, never placed. A recipe is keyed by (joint type,
fastening kind) and gives a fastener *kind* and a *count rule* over the joint length; the count on
any one joint can be overridden. A fastener's *size* is never napkin's to say: the person records
their choice once per (fastener kind, stock thickness) in the project's **fastener choices**, as
typed text, and every joint of that kind and thickness uses it; with no choice recorded the list
says "size not chosen" beside the count and never fills in a number. The **fastener list** sums
counts by (kind, size text), divides by a user-typed pack size into packs, and is the third
section of the shopping list. **Hardware** — slides, pulls, hinges — is not derived: it is a
counted item typed onto a part. **Glue** is a boolean per joint; the supplies checklist (§8)
reports how many joints want it.

### 7.2 Recipes and count rules — napkin's design defaults, all editable

*L* is the joint length in inches (the contact rectangle's long side, §2.3). "⌈⌉" rounds up.
These are practice conventions chosen for this design, **not sourced facts**; each is shown in the
popover as "recipe: n" and can be overridden per joint or changed for the project in the fastener
choices panel.

| Joint type + fastening | Kind | Count rule | On §2 |
|---|---|---|---|
| butt + pocketScrews | pocket screw | max(2, ⌈L / 2⌉) | 5 1/2 → 3; 1 1/2 → 2 |
| butt / rabbet / halfLap + screws | wood screw | max(2, ⌈L / 6⌉) | cleat 16 → 3; drawer front 15 5/8 → 3, overridden to 4 |
| butt / rabbet / groove + brads | brad | max(2, ⌈L / 1.5⌉) | 3 1/2 → 3 |
| butt / rabbet + nails | nail | max(2, ⌈L / 4⌉) | — |
| butt / halfLap / tenon / edge + dowels | dowel | max(2, ⌈L / 4⌉) | — |
| butt / edge + biscuits | biscuit | max(1, ⌈L / 8⌉) | — |
| tabletop + clips | tabletop clip | max(2, ⌈L / 12⌉) per apron | 36 → 3; 16 → 2 |

Totals on §2: pocket screws 2·3 + 4·3 + 2·2 + 3 + 2 = **27**; wood screws 2·3 = **6** (cleats,
3/4″ stock) and 2·4 = **8** (drawer fronts, through 1/2″ stock); brads 4·3 + 4·3 = **24**;
tabletop clips 3 + 2 + 2 + 3 = **10**. Glued joints: **20** of 34.

### 7.3 Fastener choices — the user's typed sizes

At the scene root:

```json
"fastenerChoices": [
  { "kind": "pocketScrew", "thickness": 768, "size": "1-1/4 in coarse", "packSize": 100 },
  { "kind": "woodScrew",   "thickness": 768, "size": "#8 x 1-1/4",       "packSize": 100 },
  { "kind": "woodScrew",   "thickness": 512, "size": "#8 x 1",           "packSize": 100 },
  { "kind": "brad",        "thickness": 512, "size": "18 ga x 1",        "packSize": 1000 },
  { "kind": "tabletopClip","thickness": null, "size": "figure-8, with screws", "packSize": 8 }
]
```

`thickness` is the **inserted (fastened-through) part's thickness** in units, or `null` for a
kind that does not depend on it; `size` and `packSize` are free text and an integer ≥ 1
(`packSize` may be `null`: no pack arithmetic). **The values above are the sample builder's typed
text and are recorded in the sample only as such**; napkin ships no default row, and a choice
missing for a (kind, thickness) the joints need produces a "size not chosen" line, never a
guess. A choice is edited in a small panel (Fastener choices…) that lists every (kind, thickness)
the current joints need, with the ones still blank first.

### 7.4 The fastener list

```
FastenerList.Of(sketch) -> FastenerRow[]
  FastenerRow(Kind, ThicknessUnits?, SizeText, Count, PackSize?, Packs?, For, Note)
```

Pure, over the sketch's joints and choices. Rows sorted by kind (the §7.2 order), then thickness
descending. `For` is "Apron, back → Leg × 2, …" in joint-id order. `Packs` = ⌈Count / PackSize⌉
when a pack size is set. `Note` is "size not chosen" or empty. On §2:

| Kind | Stock | Size (typed) | Count | Packs |
|---|---|---|---|---|
| Pocket screw | 3/4″ | 1-1/4 in coarse | 27 | 1 of 100 |
| Wood screw | 3/4″ | #8 x 1-1/4 | 6 | 1 of 100 |
| Wood screw | 1/2″ | #8 x 1 | 8 | 1 of 100 |
| Brad | 1/2″ | 18 ga x 1 | 24 | 1 of 1000 |
| Tabletop clip | — | figure-8, with screws | 10 | 2 of 8 |

### 7.5 Hardware

A counted item on a part, in `part`:

```json
"hardware": [ { "name": "16 in side-mount drawer slide, pair", "quantity": 1 } ]
```

`name` free text, `quantity` integer ≥ 1 per copy of the part. `HardwareList.Of(sketch)` sums
`quantity × part.quantity` by exact name text, in first-seen id order. On §2: the slide pair on
each drawer box front (2 boxes → 2 pairs) and a pull on each drawer front (2). Hinges on a door,
shelf pins, casters and levellers are the same mechanism. Nothing is derived from a slide's name
— the drawer's clearance was the builder's arithmetic (§2.1) — and no hardware table ships.

### 7.6 What would need a cited source, and the file to add it in

Everything below is **absent** from napkin until a task fetches and reads the primary source and
writes a table beside `fasteners-common-nails.json` (`tableVersion`, `kind`, `id`, `title`,
`category: "Fastener"`, a `citation` with designation, standard, publisher, where, url, retrieved,
and `entries` each with a `derivation`). Slice G files the tables; none is a prerequisite for
counting, because the count never needs a dimension.

| Data | Why a table would help | Source status (to be checked in the fetching task, not here) |
|---|---|---|
| Wood screw gauge → diameter, and length by driven-through thickness | offer a size instead of "size not chosen" | ASME B18.6.1 is a paid standard; a recommendation of length by thickness is practice, not a standard, and may have no citable primary source |
| Pocket screw length by stock thickness | same | jig makers publish charts (vendor data); whether to cite a vendor is Marc's call (§14) |
| Brad and finish nail lengths and gauges | same | FF-N-105B (already cited for common nails) covers brads; #119 has the nail rows unverified |
| Tabletop clip dimensions, slot or recess sizes | a numeric sentence instead of "per the clip's instructions" | vendor data only |
| Dowel diameters and lengths, biscuit sizes | same | vendor data; biscuit sizes are a de-facto convention with no standard found in this task |
| Pack sizes | pack arithmetic without typing | a shop fact, not a standard; stays user-typed |

### 7.7 The tabletop joint, because it is the common mistake

A solid-wood top changes width across the grain with the seasons; screwed rigidly to the aprons it
splits or breaks the aprons. Napkin makes the movement-tolerant attachment a **joint type**, so
the demo shows it, the tooltip explains it ("held with clips so the top can move"), and the count
of clips lands on the fastener list. The sample's top is plywood — which does not move much — and
uses the joint anyway, because the demo's job is to show the right habit. What the clip is
(figure-8, Z-clip, a screw in a slotted hole) is the `size` text of the `tabletopClip` choice; the
apron's sentence says "slot or recess per the clip's instructions" because the dimensions are the
clip maker's (§7.6). When #140 lands, a tabletop joint on a solid top can say whether the clips
run across the grain.

---

## 8. Glue, finish and supplies

A short checklist at the scene root, **user-typed lines only, no data**:

```json
"supplies": [ { "item": "Wood glue", "note": "" },
              { "item": "Sandpaper, 120 and 220", "note": "" },
              { "item": "Finish", "note": "" } ]
```

`item` free text, non-empty; `note` free text. Shown as the fourth section of the shopping-list
window, exported with the fasteners and hardware. Napkin adds one derived line it can stand
behind — "Glue: 20 of 34 joints" — and nothing else. Quantities of glue and finish are not
computed; a person who wants a number types it in the note.

---

## 9. The user's flow, start to finish — the storyboard for the demo automation

Written in napkin's terms, in the order the GUI workflow (`GUI-JOIN-*`, §10.4) drives it. Every
number is from §2.

1. **New sketch.** Draw the plan (Top view, the plan as today).
2. **Place the top.** Stock tool → Sheet goods → *3/4 plywood*; draw 42″ × 22″ at the origin; set
   `planAxes` length/width; the depth is the sheet's 3/4. Name it "Top". Raise it to Z 16 1/4 in
   the properties panel.
3. **Place the legs.** Stock → Lumber → *2x2*; draw one 1 1/2″ square at (1 1/2, 1 1/2), depth
   16 1/4, name "Leg, south-west"; duplicate three times to the other corners (duplicates are
   value copies).
4. **Place the aprons, rail and web.** Stock *1x6* standing on edge (`faceUp: north` for an apron
   drawn as a 36″ × 3/4″ box, depth 5 1/2) between the legs at the back and both sides at Z 10
   3/4; *1x2* front rail the same way at Z 14 3/4; *1x6* web 17 1/2″ front to back, centred. Hold
   them with `flush` to the legs' outside faces and the top's underside, as today.
5. **Join the frame in one gesture.** Marquee the four legs, three aprons, the rail and the web;
   **Shift+J**; the popover lists the 10 touching pairs, pre-selects *Butt / Pocket screws / glue*,
   shows "recipe" counts (3, 3, 3, 3, 3, 3, 3, 2, 2, 2 — 27 in all) and each inserted part's pocket face;
   the web's face is a tie, so the person picks *west*; Enter. Ten **B** markers appear.
6. **Attach the top.** Select the top and the back apron; **J**; the contact is the top's bottom
   on the apron's top edge, so *Tabletop / clips* is pre-selected; Enter. Repeat for the two side
   aprons and the rail (Shift+J repeats). Four **T** markers.
7. **Slide cleats.** Two *1x2* pieces, 16″, on the inside of the side aprons at Z 12; **J** each to
   its apron: *Butt / screws / glue*, recipe 3.
8. **Build drawer A.** Stock *1/2 plywood*: two sides 16″ × 1/2″ (depth 3 1/2) at X 3 1/2 and
   X 19 5/8, a box front and back 15 5/8″ between them; stock *1/4 plywood*: the bottom 15 5/8″ ×
   15″ at Z 11 1/2 touching all four. Stock *1x6*: the drawer front, 17 3/4″ overlaying the rail
   at Y 3/4 → 1 1/2.
9. **Join the drawer.** Left side + box front, **J**: *Rabbet*, depth 1/4, brads, glue (the
   popover suggests butt; the person picks rabbet; "recipe: 3"). Shift+J on the right side + box
   front repeats it. Sides + back: *Butt / brads / glue*. Bottom with each of the four: **J**
   pre-selects *Groove* (a thin panel's edge on a face), depth 1/4, no fastening, no glue. Drawer
   front + box front: *Butt / screws*, no glue, count typed **4** over "recipe: 3". Marker letters:
   R, R, B, B, G, G, G, G, B.
10. **Hardware on drawer A.** Select the box front; properties → Hardware → add "16 in side-mount
    drawer slide, pair" × 1. Select the drawer front → add "Drawer pull" × 1.
11. **Duplicate drawer A into drawer B.** Marquee the seven drawer parts; duplicate; move the copy
    18 3/8″ east (the cavity pitch: 21 3/8 − 3, web's east face to the leg's inside face). The nine joints among them and the two hardware items come
    with the copy; joints to the frame (there are none) would not.
12. **Fastener choices.** Shopping list → Fastener choices…: the panel lists pocket screw (3/4″),
    wood screw (3/4″), wood screw (1/2″), brad (1/2″), tabletop clip — all "size not chosen". The
    person types the five sizes and pack sizes from their own jig chart and the shop's packaging
    (§7.3). Supplies: type "Wood glue", "Sandpaper, 120 and 220", "Finish".
13. **Read the cut list** (§6.5): thirteen rows; the drawer sides listed left and right; the box
    front and bottom at their finished sizes with the allowance sentence; the aprons with their
    pocket-hole and clip sentences. Export CSV.
14. **Read the shopping list**: boards and sheets as today (§10.2 has the expected buys); the
    fastener section (§7.4); hardware (2 slide pairs, 2 pulls); supplies with "Glue: 20 of 34
    joints". Export.
15. **Move the web 1″ north.** J4 and J5's markers go hollow, the cut list flags the web's row
    "joint not satisfied"; undo restores both. (This is the resize test, on screen.)

---

## 10. Test plan

Feature-catalog ids are the next free `GEO-`, `CUT-` and `GUI-` numbers, assigned when the catalog
is edited; a new `JOIN-` area is not needed — joints are geometry and cut-list behaviour. The
oracle pattern is `samples/README.md`'s: every expected number hand-derived from §2 with a
`derivation` string, never regenerated.

### 10.1 The sample: `samples/diy-coffee-table-drawers`

`diy-coffee-table-drawers.design.md` — §2's tables and layout, plus a paragraph stating that the
slide length, clearance and every fastener size in the file are the builder's typed choices, not
napkin data. `.scene.json` — the 24 boxes of §2.2 at §2.1's positions, the 34 joints of §2.3,
the five fastener choices of §7.3, the hardware of §7.5 and the supplies of §8, formatVersion 5.
`.expected.json` — the existing sections (counts, overall, boxes, cutList, cutListCsv,
shoppingList, shoppingListCsv) plus `fasteners`, `hardware`, `supplies`, each row with its
`derivation`. `SampleSetTests` and `SampleShoppingListTests` pick it up like the others.

### 10.2 Hand-computed expectations

Cut list: the thirteen rows of §6.5, exactly, in that order, with `lengthUnits` etc. in 1024ths
(16 1/8″ = 16512, 15 1/2″ = 15872, 15 5/8″ = 16000, 17 3/4″ = 18176, 17 1/2″ = 17920, 16 1/4″ =
16640, 5 1/2″ = 5632, 3 1/2″ = 3584, 1 1/2″ = 1536) and the sentences verbatim. Total pieces 24.

Shopping list, boards and sheets, by parts-and-cut-list §4's first-fit decreasing over the
library's stocked lengths (6′ … 16′), to be re-derived by the implementer with its derivation:
2x2 — four 16 1/4″ pieces = 65″ in one **6′**; 1x6 — 36, 17 3/4, 17 3/4, 17 1/2, 16, 16 = 121 1/4″
→ **2 × 6′** (36 + 17 3/4 + 17 3/4 = 71 1/2 in the first; 17 1/2 + 16 + 16 = 49 1/2 in the
second); 1x2 — 36 + 16 + 16 = 68″ → **1 × 6′**; 3/4 plywood — 924 sq in → **1 sheet**; 1/2
plywood — 4 × 56 + 2 × 56 7/16 + 2 × 54 11/16 = 446 1/4 sq in → **1 sheet**; 1/4 plywood — 2 ×
249 15/16 = 499 7/8 sq in → **1 sheet**. Kerf is not in this (#138).

Fasteners: the five rows of §7.4 with counts 27, 6, 8, 24, 10 and packs 1, 1, 1, 1, 2. Hardware:
2 slide pairs, 2 pulls. Supplies: three typed lines and "Glue: 20 of 34 joints".

### 10.3 Golden cases (unit, per assembly)

Geometry (`Napkin.Core.Geometry.Tests`):

1. `Joint` round-trips through `SceneWriter`/`SceneBinder`; each refusal row of §4.4 is hit by one
   malformed file and names the field.
2. `JointGeometry.Of`: the contact rectangle for an end on a face (J1), a face on a face (J6), a
   panel edge on a face (J9, offset 512 from the bottom edge, width 256, along the length), a
   dado (a shelf into a side: across the width), a rabbet at each end, a half-lap overlap; empty
   contact → unsatisfied; non-coplanar faces → refused at load.
3. Direction rule of §4.1 on each case; the face-to-face tie asks.
4. The updater neither propagates nor refuses: a `SetParam` on a box with a joint is `Solved` and
   the joint is unchanged; the checker's residual is zero while the faces touch and
   `NotEvaluable` after a move that separates them; undo restores it.
5. Deleting a part deletes its joints; duplicating a set copies the joints among them and not
   the joints to outside parts (drawer A → drawer B: 9 copied, 0 dangling).
6. `depth` ≥ the receiving thickness is unsatisfied, not refused.

Furniture (`Napkin.Modules.Furniture.Tests`):

7. Allowances: box front 16000 → 16512; bottom 16000 × 15360 → 16512 × 15872; a leg unchanged.
8. Every sentence form of §6.2 once, verbatim, including the merged pocket-hole sentence and the
   ≈ marker on a non-1/16 offset.
9. Grouping: the side aprons one row (half-turn equal); the drawer sides two rows (mirrored);
   four legs one row; two boxes equal in every way but a groove depth, two rows.
10. Sort order with the new rows; CSV header line and `Joinery` column parse back to the rows.
11. Recipes: each count rule of §7.2 at L = 1 1/2, 3 1/2, 5 1/2, 16, 36; the override; `none`
    yields no row.
12. `FastenerList.Of`: the five rows; a missing choice → "size not chosen" and no size; pack
    arithmetic and `null` pack size; sort order.
13. `HardwareList.Of`: quantity × part quantity; name text exact-match grouping.
14. Supplies: the glue line's count.
15. The sample, row for row, against `diy-coffee-table-drawers.expected.json`.

### 10.4 GUI workflows (`tests/Napkin.App.GuiTests`, `[GuiWorkflow]`, ≥ 5 actions, keyboard and pointer)

- `GUI-JOIN-01` two parts, J, popover, Enter → a marker; tooltip text; Delete removes it.
- `GUI-JOIN-02` the frame by Shift+J: 10 joints, the web's pocket face picked by keyboard.
- `GUI-JOIN-03` the drawer: rabbet with a typed depth, groove pre-selected for the bottom, the
  count override typed over "recipe: 3".
- `GUI-JOIN-04` move the web; markers hollow, row flagged; undo.
- `GUI-JOIN-05` fastener choices panel: blank rows first, typing a size updates the list.
- `GUI-JOIN-06` hardware on a part and the hardware section; supplies lines; export.
- `GUI-JOIN-07` the storyboard of §9 end to end, asserting §6.5's row count and §7.4's counts.

---

## 11. Implementation slices

Each lands alone on `main`, in this order, with local build, test, `ratchet check`. Files are
disjoint between slices except the recurring collision points (`napkin.sln`,
`Directory.Build.props`, `ratchet/baseline.json`, `PlannedFeatures.g.cs`), and slice A's sample
bumps touch every `samples/*.json` — land A first and alone.

| Slice | What | Model | Files | Depends on |
|---|---|---|---|---|
| **A** | `Joint` relationship, `Fastening`, enums; `formatVersion` 5 with `joint`, `hardware`, `fastenerChoices`, `supplies`; binder/writer/names with §4.4's refusals; updater and checker arms (§4.3); delete and duplicate semantics; `docs/file-format.md`; bump every sample's `formatVersion`; tests 1, 4, 5 | Sonnet | `Napkin.Core.Geometry/Relationship.cs`, `RelationshipChecker.cs`, `Propagator.cs`, `DirectUpdater.cs`, `Sketch.cs`, `Part.cs`; `Napkin.Core.Project/*`; `docs/file-format.md`; `samples/*.json` (version field only) | — |
| **B** | `JointGeometry.Of`: contact rectangle, direction rule, groove/dado/rabbet/half-lap derivations, unsatisfied cases; `Sketch.Validate` pairs of §3.3; tests 2, 3, 6 | Sonnet | new `Napkin.Core.Geometry/JointGeometry.cs`, `Sketch.cs` (validate) | A |
| **C** | Allowances, `JointDescription`, grouping with half-turn equality, row fields, CSV column and header; the sample (scene + design + expected cut list and shopping list, hand-derived from §2, §6.5, §10.2); update every committed `cutListCsv` header; tests 7–10, 15 (cut list part) | Sonnet | `Napkin.Modules.Furniture/CutList.cs`, `CutListRow.cs`, `CutListCsv.cs`, new `JointDescription.cs`, `JointSequence.cs`; `samples/diy-coffee-table-drawers.*`; every `samples/*.expected.json` (header line) | B |
| **D** | Recipes, `FastenerList.Of`, fastener choices model; tests 11, 12; the sample's `fasteners` section | Sonnet | new `Napkin.Modules.Furniture/Recipes.cs`, `FastenerList.cs`, `FastenerRow.cs`; `samples/diy-coffee-table-drawers.expected.json` | C |
| **E** | `HardwareList.Of`, supplies, the glue line, the shopping-list window's three new sections and their CSV; tests 13, 14 | Sonnet | new `HardwareList.cs`, `SuppliesList.cs`, `SuppliesCsv.cs`; `Napkin.App` shopping-list window | D |
| **F** | The join tool: J / Shift+J popover, direction and type suggestion, pocket-face default, markers in plan, 3D, standard views and parts view, tooltip, properties panel, fastener choices and hardware panels; `GUI-JOIN-01…07` | Sonnet (two commits: F1 tool + markers, F2 panels + storyboard) | `Napkin.App/*` | B for F1; E for F2 |
| **G** | Cited fastener tables per §7.6, one file each, only those whose primary source can be fetched and read; wire "size not chosen" to offer a table row when one exists; `code-data` | Sonnet, data rule strictly | `Napkin.Core.Materials/data/fasteners-*.json`, `MaterialsLibrary` | D; Marc's §14.3 |
| later | `tenon`, `edge`, `dovetail` types and their sentences; dowel and biscuit recipes | — | — | C |

---

## 12. Risks and unknowns

1. **The updater's check-only refusal.** If the arm for `Joint` is missed in one of the three
   switches, a single joint freezes editing. Test 4 exists for this; slice A must not land
   without it.
2. **Half-turn equality is subtle.** Getting the symmetry group wrong merges mirrored drawer sides
   (the mistake the feature exists to catch) or splits identical aprons. Test 9 pins both.
3. **Sentence frames.** Sentences are in the receiving part's own drawn frame; a part drawn on
   edge (`faceUp: north`) has its "bottom edge" where the person expects only if the frame
   mapping of assembly-model §1.3 is applied consistently. The aprons in §2 are exactly this
   case.
4. **Expectation churn.** The CSV header change touches every committed `cutListCsv`; slice C
   pays it once.
5. **Popover size.** Five types, eight fastenings, depth, count, pocket face and glue is a lot for
   a popover; if it grows, the type row and the rest can split into two steps. Not a model risk.
6. **"Join all touching" on a large selection** could propose joints nobody wants (a cleat also
   touches two legs). The list is shown and each pair unticks; the default type filter keeps
   most spurious pairs unticked. Worth watching in `GUI-JOIN-02`.
7. **Data rule pressure.** The first person to use this will want a screw length filled in. §7.6
   lists exactly what a table would need; slice G either cites it or the field stays typed.
8. **Two drawers drawn twice** makes the sample 24 boxes; if the parts-view cell count or the
   plan gets crowded, that is a view concern, not a model one.

---

## 13. Decisions for Marc

1. **Joint as a relationship kind** (§4.1 c), over a feature on a part or a new entity — the
   recommendation, and a `formatVersion` 4 → 5 bump that refuses every version-4 file, including
   every committed sample until slice A rewrites their version field.
2. **The first five types — butt, groove, rabbet, halfLap, tabletop — with tenon, edge and
   dovetail later.** Edge-glued solid tops are common in DIY; the sample's top is plywood so that
   one `tabletop` joint per apron suffices (a board-built top would need one joint per board per
   apron or a one-to-many joint). Say if the solid top matters more than the shorter sample.
3. **No fastener dimensions without a citation, and which sources count.** Wood screws' standard
   is paid; pocket-screw and clip data are vendor charts. Whether a jig maker's chart may be cited
   as a source (with its retrieval date) or whether those fields stay user-typed for good is a
   product and licensing call, the same shape as the code-table stance for #13–#17.
4. **Joinery in the cut-list group key, with half-turn equality** (§6.3) — mirrored parts list
   separately. The alternative is one row of four with a note "2 left, 2 right"; that is simpler
   and hides the mistake.
5. **Hardware as typed text on a part, no hardware library** (§7.5), and supplies as typed lines
   (§8). If Marc wants a picker of common hardware, it is a data task with the same data rule.
6. **The count-rule defaults of §7.2** are napkin's practice conventions. Approve, change, or
   ship with none (every count typed) — the latter makes the fastener list empty until the person
   fills 34 joints, which defeats the demo.
