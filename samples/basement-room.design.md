# Finishing a 12 × 14 ft basement room — the design, as you would write it on paper

The renovation note's worked example 1 ([`docs/design/renovation-sketches.md`](../docs/design/renovation-sketches.md)
§2, §10.2). Plan view; X east, Y north, the origin the south wall's south-west corner.

```
  y  147 1/2 +---------------------- Wall, north (175") -----------------------+
  ^          |W|                                                             |E|
  |          |a|                        Room 14'-0" × 12'-0"                  |a|
  |   93 1/2 |l| Window 1 (3'-0", sill 4'-0")        ceiling 8'-0"            |s|
  |   57 1/2 |l|                                                             |t|
  |    3 1/2 +-+----------[ Door 1, 3'-0" ]----------------------------------+-+
  |        0 +---------------------- Wall, south (175") -----------------------+
  +--------> x   0   3 1/2        60      96                             171 1/2  175
```

## Stated dimensions — every one the builder's typed choice, none a standard

| What | Dimension | Notes |
|---|---|---|
| Walls | 2x4 (3 1/2" thick), 8'-0" tall, all **New** | Side exterior (against the foundation, to be insulated), Bearing **no**, Header **(2) 2x6**, typed; studs at napkin's 16" design default |
| Wall, south / north | 175" long (14'-7"), running full | south at y 0, north at y 147 1/2 |
| Wall, west / east | 144" long (12'-0"), turned a quarter, butting between | x 0..3 1/2 and x 171 1/2..175, y 3 1/2..147 1/2 |
| Room | inside 14'-0" × 12'-0", ceiling 8'-0" | on the walls' inside faces, x 3 1/2..171 1/2, y 3 1/2..147 1/2 |
| Door 1 | 3'-0" × 6'-8", sill 0, 5'-0" along the south wall | New |
| Window 1 | 3'-0" × 2'-0", sill 4'-0", 4'-6" along the west wall (centred) | New |

Finishes ticked, with the builder's typed values from the packages (none of them napkin data):
drywall on walls and ceiling, sheet **4' × 8'**; insulation by area on the exterior walls, a bag
covers **40 sq ft**; paint on walls and ceiling, **2** coats, a gallon covers **350 sq ft**;
flooring, **10 %** waste (napkin's own design default, said so), a box covers **20 sq ft**;
baseboard, stick **8'-0"**. No code is chosen: the walls are not bearing, so no header is checked.

## The frame, by hand

`t` = 1 1/2", `s` = 16", studs 96 − 3t = 91 1/2". Each opening: one jack and one king each side
(napkin's placeholder counts with a header you chose), header w + 3 = **39"** of (2) 2x6 (5 1/2" deep).

- **South, 175"**: layout 0, 16, … 160 (eleven; 176 would not fit) and the end stud at 173 1/2:
  12. The door at 60, 36 wide, clears [57, 99): 64, 80, 96 out — **9 studs**, **2 kings**, **2 jacks**
  80 − 1 1/2 = **78 1/2"**, header 39"; room 93 − 80 = 13", cripples above 13 − 5 1/2 = **7 1/2"** at
  64, 80, 96 (within [58 1/2, 97 1/2]): **3**. No sill. **3 plates** 175".
- **North, 175"**: **12 studs**, 3 plates 175".
- **East, 144"**: 0 … 128 (nine) and 142 1/2: **10 studs**, 3 plates 144".
- **West, 144"**: the window at 54, 36 wide, sill 48, 24 tall (top 72), clears [51, 93): 64, 80 out
  — **8 studs**, **2 kings**, **2 jacks** 72 − 1 1/2 = **70 1/2"**, header 39"; room 93 − 72 = 21",
  cripples above 21 − 5 1/2 = **15 1/2"** at 64, 80: **2**; rough sill **36"**; cripples below
  48 − 3 = **45"** at 64, 80 (wholly inside [54, 90)): **2**. 3 plates 144".

2x4 pieces: 39 studs + 4 kings = **43 × 91 1/2"**, 2 × 78 1/2", 2 × 70 1/2", 2 × 45", 1 × 36",
2 × 15 1/2", 3 × 7 1/2", plates **6 × 175"** and **6 × 144"** — 67 pieces. 2x6: **4 × 39"**.
(The note's hand pass says 71 pieces of 2x4; the list it gives adds to 67, which is the count here.)

**Boards**, first-fit decreasing over the library's 6' … 16' (192"), 1/8" kerf, then each board
shrunk to the shortest length that holds it:

1. The six 175" plates open six boards (a second would not fit).
2. The six 144" plates open six more (144 + 175 > 192).
3. 43 studs: two to a board (183 + 1/8 ≤ 192) on 21 new boards, the 43rd alone on a 22nd.
4. 78 1/2": the first rides with the lone stud (91 1/2 + 78 1/2 + 1/8 = 170 1/8); the second opens a board.
5. 70 1/2": the first joins that 78 1/2 (149 1/8); the second opens a board alone.
6. 45", 45", 36": onto the first three 144" plate boards (189 1/8, 189 1/8, 180 1/8).
7. 15 1/2", 15 1/2": onto the first two 175" plate boards (190 5/8 each).
8. 7 1/2" × 3: two onto the third 175" plate board (182 5/8, then 190 1/4), the third onto the fourth.

Shrunk: nine plate boards with a rider, the other two 175" boards, 21 stud pairs and the
stud-and-jack board are **16'** (31); the three bare 144" plates are **12'**; the jack and 70 1/2"
(149 1/8) **14'**; the lone 70 1/2" **6'**. **2x4: 1 × 6', 3 × 12', 1 × 14', 31 × 16' — 36 boards,
552 lineal feet, 368 board feet** (2 × 4 ÷ 12 × 552). **2x6: 1 × 14'** (4 × 39 + 3/8 = 156 3/8).

## The area takeoff, by hand

`L` 168", `W` 144", `H` 96", `P` = 2(168 + 144) = 624" = 52'-0".

| Line | Exactly | Shown |
|---|---|---|
| walls gross | 624 × 96 = 59904 sq in = 416 sq ft | |
| openings | door 36 × 80 = 2880 = 20 sq ft; window 36 × 24 = 864 = 6 sq ft | 26 sq ft |
| walls net | 390 sq ft | |
| ceiling | 168 × 144 = 24192 sq in = 168 sq ft | |
| drywall | 390 + 168 = 558 ÷ 32 = 17.4375 | 558 sq ft; **18 sheets** (one pool) |
| insulation | all four walls exterior and bounding: 390 ÷ 40 = 9.75 | 390 sq ft; **10 bags** |
| — by bays | S 9 studs + 2 kings − 1 − 1 door = 9; N 12 − 1 = 11; E 10 − 1 = 9; W 8 + 2 − 1 − 1 = 8 | **37 bays**, 91 1/2" tall |
| paint | 558 × 2 = 1116 ÷ 350 = 3.19 | 1116 sq ft to cover; **4 gallons** |
| flooring | 168 × 110 / 100 = 184.8, up to 185; 184.8 ÷ 20 = 9.24 | 185 sq ft; **10 boxes** |
| baseboard | 624 − 36 (the door) = 588" = 49'-0"; 588 ÷ 96 = 6.125 | 49'-0"; **7 sticks** |
