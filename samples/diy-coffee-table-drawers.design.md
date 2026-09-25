# DIY coffee table with two drawers — the design, as you would write it on paper

A coffee table a person builds in a garage with a saw, a drill, a pocket-hole jig, clamps and glue:
a 3/4 plywood top on four 2x2 legs, 1x6 aprons on three sides, a 1x2 rail over two drawers side by
side and a 1x6 web between them. The drawer boxes are 1/2 plywood with a 1/4 plywood bottom in a
groove, on side-mount slides, behind 1x6 overlay fronts. It exists to show **joinery**: the two
drawers carry every kind of joint the first set has (butt, rabbet, groove, tabletop), so the cut
list has finished sizes longer than the drawn ones, sentences that say what to cut, and mirrored
parts that must stay two rows. The full design is `docs/design/joinery-and-fasteners.md` §2; this
file is the sample's own statement of the numbers.

```
  y
  ^                           42"
  |  +------------------------------------------------------+  --+
  |  |  +--+     back apron, 1x6, 36"                 +--+  |    |
  |  |  |  |=========================================|  |  |    |
  |  |  +--+  ||                 web  ||            +--+  |    |
  |  |   side  ||     drawer A          ||  drawer B  side  |   22"
  |  |   apron ||                       ||            apron |    |
  |  |  +--+  ||                        ||           +--+   |    |
  |  |  |  |=========================================|  |  |    |
  |  |  +--+     front rail, 1x2, 36"  (drawer fronts overlay it) |
  |  +------------------------------------------------------+  --+
  +--------------------------------------------------------------> x
```

## Stated dimensions

Plan view, origin at the top's south-west corner, X east, Y north, Z up, floor at Z = 0. Every box
lies as drawn. Overall 42″ × 22″ × 17″ high.

| What | Dimension |
|---|---|
| Top | 3/4 plywood, 42″ × 22″ × 3/4″, underside at 16 1/4″ |
| Legs (4) | 2x2 (1 1/2″ square), 16 1/4″ tall, inset 1 1/2″ from the top's edge: outer frame 39″ × 19″ |
| Back apron | 1x6, 36″ × 5 1/2″ × 3/4″ between the legs, flush with the legs' outside faces |
| Side aprons (2) | 1x6, 16″ × 5 1/2″ × 3/4″ between the legs, flush outside |
| Front rail | 1x2, 36″ × 1 1/2″ × 3/4″, flush with the front legs' outside faces |
| Aprons and web | 10 3/4″ to 16 1/4″ high (5 1/2″), top edges flush under the top; the rail 14 3/4″ to 16 1/4″ |
| Web | 1x6, 17 1/2″ × 5 1/2″ × 3/4″ (19″ between the back apron and the rail less their 3/4″s), centred at x 21 |
| Slide cleats (2) | 1x2, 16″ × 1 1/2″ × 3/4″ on each side apron's inside face, 12″ to 13 1/2″ high |
| Drawer boxes (2) | 16 5/8″ wide × 16″ deep × 3 1/2″ high, 11″ to 14 1/2″ up, their fronts against the drawer fronts' back faces (y 1 1/2″) |
| Drawer sides | 1/2 plywood, 16″ × 3 1/2″ × 1/2″; the box front and back 15 5/8″ × 3 1/2″ × 1/2″ between them |
| Drawer bottom | 1/4 plywood, 15 5/8″ × 15″ × 1/4″, in a 1/4″ groove 1/2″ up from the sides' bottom edge |
| Drawer fronts (2) | 1x6, 17 3/4″ × 5 1/2″ × 3/4″, 1/8″ from the leg, 1/4″ apart over the web |

**The slide length, the side clearance and every fastener size in the scene are the builder's typed
choices, not napkin data.** The builder read them off the slides' and fasteners' own packaging: a 16″
slide with 1/2″ of clearance per side (which is why the drawer boxes are 16 5/8″ wide in a 17 5/8″
cavity and 16″ deep), and the fastener sizes and pack sizes in the scene's `fastenerChoices`
(1-1/4 in coarse pocket screws, #8 × 1-1/4 and #8 × 1 wood screws, 18 ga × 1 brads, figure-8
tabletop clips). napkin ships no fastener or hardware table (design note §7.6): those texts are the
person's, kept as typed.

## Joints

Thirty-four joints (design note §2.3), twenty of them glued: J1–J2 the aprons' ends into the legs and
J3 the rail's, J4–J5 the web's ends, all butt joints with pocket screws (the web's holes drilled from
its west face, the builder's pick); J6 the cleats on the side aprons, butt with screws; J7 the box
front's ends in a 1/4″ rabbet in each side, J8 the box back butted between them (brads); J9 the
bottom's four edges in 1/4″ grooves (no fastening, no glue); J10 the drawer front screwed to the box
front, four screws typed over the recipe's three; J11 the aprons and rail held to the top with
tabletop clips, so the top can move.

## What the cut list must say

Thirteen rows, twenty-four pieces (`diy-coffee-table-drawers.expected.json`, each row with its
derivation): the drawer box front is **16 1/8″** long (15 5/8″ drawn plus 1/4″ at each rabbeted end),
the drawer bottom **16 1/8″ × 15 1/2″**, and the left and right drawer sides are two rows of two
because they are mirror images. Every number in the expectations file was worked out by hand from the
table above and the design note, and none was copied from napkin's output.
