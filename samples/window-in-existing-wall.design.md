# A 3 ft window in an existing 12 ft exterior wall — the design, as you would write it on paper

The renovation note's worked example 2 ([`docs/design/renovation-sketches.md`](../docs/design/renovation-sketches.md)
§3, §10.3). Plan view, looking down; X runs along the wall, the origin is its south-west corner.

```
  y
  ^            54"              36"              54"
  |   |<--------------->|<--------------->|<--------------->|
  |   +-----------------+=================+-----------------+  --+ 3 1/2"
  |   |  Wall 1 (existing, faint)   Window 1 (new)          |  --+
  |   +-----------------+=================+-----------------+
  |   0"               54"               90"              144"
  +---------------------------------------------------------------> x
```

## Stated dimensions — every one the builder's typed choice, none a standard

| What | Dimension | Notes |
|---|---|---|
| Wall 1 | 12'-0" long, 3 1/2" thick (a 2x4 wall), 8'-0" tall | **Existing**; Side exterior; Bearing yes; stud spacing not typed, so napkin's 16" design default |
| Window 1 | 3'-0" wide × 4'-0" tall, sill 3'-0" | **New**; centred: 4'-6" along |
| Adopted code | Connecticut 2022 (the shipped pack), locked | its header table is not loaded, so the header check says No data |

What the wall supports is not typed: the shipped pack's table is not loaded, so there is nothing to
choose from. The tests exercise the code check under the synthetic pack `us-zz-reno` (SYNTHETIC
TEST DATA, NOT CODE VALUES), typing its `zz-roof` and a 30 psf snow load in the test, never in this
file.

## The frame, by hand

`t` = 1 1/2" (a 2x's dressed thickness, the library's), `s` = 16", studs `96 − 3t` = 91 1/2".

**As it is** (the existing wall, no openings): layout studs at 0, 16, … 128 (nine: 128 + 1 1/2 ≤
144, 144 is not), and the end stud at 142 1/2: **10 studs** 91 1/2"; **3 plates** 144".

**As it will be** (the new window at `a` = 54, `w` = 36, sill 36, 48 tall, top 84; one jack and
one king each side): studs whose body touches [54 − 3, 90 + 3) = [51, 93) — at 64 and 80 — are left
out: **8 studs**; **2 kings** 91 1/2"; **2 jacks** 84 − 1 1/2 = **82 1/2"**; header 36 + 2 × 1 1/2 =
**39"**; room above 96 − 3 − 84 = 9"; with the synthetic pack's (2) 2x6 (5 1/2" deep) the cripples
above are 9 − 5 1/2 = **3 1/2"**, at the layout positions over the header, [52 1/2, 91 1/2]: 64 and
80, **2**; rough sill **36"**; cripples below 36 − 3 = **33"** at the positions wholly inside
[54, 90): 64 and 80, **2**.

**The diff**, compared by role, length and stock: new — 2 king studs 91 1/2", 2 jack studs 82 1/2",
a header of 2 × 39" 2x6, 2 cripples above 3 1/2", a rough sill 36", 2 cripples below 33"; out —
**2 studs 91 1/2"**, "assuming a regular 16" layout in the existing wall". The plates do not change.
The message bar says: "new — 2 king studs, 2 jack studs, header, sill, 4 cripples; out — 2 studs".

**Boards** for the new material (first-fit decreasing, 1/8" kerf, the library's 6' … 16'): 2x4
pieces longest first 91 1/2, 91 1/2, 82 1/2, 82 1/2, 36, 33, 33, 3 1/2, 3 1/2. Board 1 takes the two
kings (183 + 1/8 ≤ 192); board 2 the two jacks (165 + 1/8); board 3 the 36 and both 33s (102 + 2/8);
the two 3 1/2s ride on board 1 (190 + 3/8 ≤ 192). Shrunk to the shortest length that holds each:
**16'** (190 3/8), **14'** (165 1/8), **10'** (102 1/4; 8' is 96). 2x6: the two 39" plies, 78 1/8:
**one 8'** (6' is 72).

## The checks, on the wall as it will be

- Under the synthetic pack: header (2) 2x6, 1 jack and 1 king each side, cited.
- Under the shipped Connecticut pack (this file's code): **No data** — it has no header table for
  exterior-bearing walls.
- With Bearing switched to **no**: "Not checked: Wall 1 is marked not bearing, …, your choice".
- With Bearing not said: "Say whether Wall 1 is bearing (Part panel)."
- Bracing: two solid segments, "wall start to Window 1" and "Window 1 to wall end", 54" each.
