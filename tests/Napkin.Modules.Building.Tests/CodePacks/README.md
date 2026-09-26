# SYNTHETIC TEST DATA - NOT CODE VALUES

Two packs roots for the code-check tests (issue #18). Every number here is made up to exercise the
engine and the building module; none is a building-code value and none may be copied into src/,
packs/ or samples/.

- `one/`: `us-zz-frame` revision 1 (table ZZ-HEADER) and `us-zz-other` revision 1 (ZZ-OTHER-HEADER).
- `two/`: `us-zz-frame` revision 2 — row r.s30.b changed from (2) 2x10 to (2) 2x12.
- `three/`: `us-zz-interp` (ZZ-INTERP-HEADER, columns 30/50/70 psf) with an overlay footnote e shaped like
  Connecticut's R602.7(1) footnote e: substitute-input below 30 psf and interpolate between 30 and 50 psf.
  (1) 2x8 spans 6'-0" at 30 and 4'-0" at 50, so 5'-0" at 40.

The member names (2x8, 2x10, 2x12) are lumber names the materials library carries, so a sized
header can be bought; the spans, loads and stud counts beside them are not from any code.
- `brace/`: two wall-bracing packs (#39), `us-zz-brace-a` ("ZZ BRACE A", section ZZ-BRACE.1: 2" step,
  4'-0" per 10'-0" of line up to 99 mph and 5'-0" up to 199 mph, × 5/4 above 150 mph, + 1'-0" for walls
  over 9'-0", out of scope over 12'-0"; methods zz-panel (minimum 2'-0" up to 8'-0" walls, 2'-6" up to
  12'-0", cap 6'-0") and zz-board (minimum 4'-0", no cap)) and `us-zz-brace-b` ("ZZ BRACE B", ZZ-BRACE-B.7:
  1" step, 3'-0" per 8'-0", out of scope over 10'-0"; only zz-panel, minimum 3'-0", no cap). No header
  tables. Golden files under `brace/golden/`.
- `reno/`: `us-zz-reno` ("ZZ RENO", #161) with two one-row tables for the renovation note's code-check routing
  (docs/design/renovation-sketches.md §4.3, §10.1): ZZ-RENO-HEADER, exterior-bearing, answers (2) 2x6 with 1 jack
  and 1 king each side for zz-roof at snow ≤ 30 and a span ≤ 4'-1" (example 2's 3'-0" window); ZZ-RENO-INTERIOR,
  interior-bearing, answers (2) 2x8 for the same request, so the route taken shows in the answer.
- `deck/`: `us-zz-deck` ("ZZ DECK", #198) — deck tables for the M11 checks, every number made up and chosen not to
  coincide with any published value: ZZ-DECK-JOIST (zz-deck, zz-fir, 2x8 at 16" allowed 11'-1"), ZZ-DECK-BEAM
  ((2) 2x10 carrying joists up to 10'-0": 6'-10"), ZZ-RAFTER, ZZ-DECK-LEDGER (zz-bolts, staggered, 17" up to a
  12'-0" joist span), ZZ-DECK-FOOTING (a lower-bound soil column: up to 40 sq ft on at least 2000 psf,
  "zz 15 in square"), ZZ-GUARD.1 (guard trigger 28", minimum 34", opening 5"; riser 8 1/4", tread 9",
  difference 1/2", handrail at 3 risers, width 32") and a frost.json of 3'-6". It also carries ZZ-DECK-HEADER,
  a copy of ZZ-RENO-HEADER, so a porch's walls size under the same pack.
