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
