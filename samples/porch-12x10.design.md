# A 12 × 10 ft porch on a deck, under a 5-in-12 shed roof — the design, as you would write it on paper

The deck-and-porch note's worked example ([`docs/design/deck-and-porch.md`](../docs/design/deck-and-porch.md)
§9), cut down to one front wall and one window. Plan view, looking down; the origin is the house's
south-west corner, and the house's south face is on y = 0.

```
  y
  0 +--------------------------------------------------------------+  House (existing), 20'-0" x 5 1/2", 16'-0" tall
    |          48"         Deck 1, 12'-0" x 10'-0", 3'-0" up        |
    |        +-----------------------------------+                 |
    |        |   ledger on the house (north edge)|                 |
    |        |                                   |   Roof 1 covers  |
    |        |                                   |   the same plan,  |
    |        |          54"      36"             |   5 in 12, high   |
-120        +=====================[Window 1]=====+   at the house    |
             Front: new, 3 1/2" thick, 8'-0" tall, on the decking (z = 36"), bearing
```

- **House** — existing, 240" x 5 1/2", 192" tall, at the origin.
- **Deck 1** — 144" along the house from x = 48", 120" out, 36" above grade; 2x8 joists at 16",
  a (2) 2x10 beam on 3 4x4 posts, 5/4x6 decking at a 1/8" gap. Supports, species and footing depth
  are left empty: napkin never defaults them.
- **Front** — the porch's front wall on the deck's south edge, standing on the decking (z = 36"),
  144" x 3 1/2" x 96", exterior and bearing: it carries the roof.
- **Window 1** — glass, 36" wide x 60" tall, 54" along the front wall, sill 24" above the decking.
- **Roof 1** — over the deck's outline, its low end on Front's top plates at z = 132", rise 50"
  over the 120" run: 5 in 12. 2x8 rafters at 16" and a 2x8 ledger, 12" overhang, blocking,
  7/16 OSB, asphalt shingles at 33 sq ft a unit, no waste.

By hand (§9.5): rafter run 120 - 1 1/2 + 12 = 130 1/2"; 12 : 5 : 13, so each rafter is
130 1/2 x 13 / 12 = 141 3/8" = 11'-9 3/8", ten of them (at 0, 16, … 128 and the end one at
142 1/2). Glazing: 36 x 60 = 2160 sq in of glass over the front wall's 13824 and the roof's
144 x 141 3/8 = 20358 — no side walls, so no rake triangles — 2160 / 34182 = 6.3 %, under the 40 %
line.

The sample is locked to the shipped Connecticut pack, revision 3, whose deck joists and beam come from
DCA 6-2015 Tables 2 and 3A, a guide (#41). With Supports and Species empty the joist and beam lines
ask for them; the panel lists the guide's words. napkin reads the guide as covering a deck carrying
only its own loads (Table 2 note 1, p. 4; item 8, p. 2), and this deck carries the roof-bearing Front,
so what it supports is not `deck`: typed as anything else, its joists and beam are out of the guide's
scope — get it engineered. (The beam line reads its span L_B between post faces, as DCA 6's Figure 3,
p. 7, dimensions it: three 4x4s under 144", (144 − 3 × 3 1/2) ÷ 2 = 66 3/4". The footing line's middle
post carries, by DCA 6 Appendix B Eq. B-1, 72" of beam — its centreline to the deck's edge — × half the
joists' 118 1/2", ledger face to the rim's outside face: 4266 sq in, 29.6 sq ft.) The ledger, footing and
rafter checks say **No data** (#40, #42, #209).
