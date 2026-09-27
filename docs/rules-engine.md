# The rules engine: packs, results and your own tables

`Napkin.Core.RulesEngine` sizes a wall-opening header and checks a wall line's bracing from an
**adopted-code pack**, and cites the edition, table or section and row every answer came from. Design: [`design/rules-engine-model.md`](design/rules-engine-model.md).

## Data status: no real tables ship

napkin ships **no IRC header, bracing or base-layer table values**. Transcribed tables may ship when
each is read from a primary or official source in the same task and cited beside the data (Marc,
2026-09-25, #157; DESIGN.md §2.1), and only these do: Connecticut's Appendix AY (#210) and **DCA
6-2015 Tables 2 and 3A**, the deck joist spans and overhangs and the deck beam spans (#41, below). The engine is tested on synthetic
fixtures (`tests/Napkin.Core.RulesEngine.Tests/Fixtures`, `Golden/`, marked `SYNTHETIC TEST DATA - NOT
CODE VALUES`) and on the shipped Connecticut pack described below, whose own golden files are in
`packs/golden/`. With no pack, or a pack without a table, the answer is `NoData` - napkin never guesses.

**`packs/` (repo root, shipped beside the executable)** holds the first real pack, **Connecticut
2022** (`packs/packs/us-ct-2022`, on the 2021 IRC as amended; CT 2026 is not in force yet). It was
read from Connecticut's own document (2022 CSBC w/ Errata #1, ED October 1, 2022; sha256 and URL in
`pack.json`, retrieved 2026-09-25):

- Loaded and cited (`ct-overlay-data.json`): Table R301.2 seismic design category B and frost line
  depth 42" (p. 131), snow and wind "as set forth in Appendix AY" (p. 131), and the verbatim R602.7
  amendments, Table R602.7(1) footnote e and Table R602.7(3) footnote b (p. 145). Both are also
  **encoded** in `amendments/` as `amend-footnote` operations (the 30 psf substitution and the
  30-50 psf interpolation, below); they are pending until the IRC base tables are loaded, then
  apply automatically.
- Appendix AY (pp. 157-160), the per-municipality table of wind speeds and ground snow loads, is
  **transcribed** (#210, 2026-09-26) into `packs/packs/us-ct-2022/site-values.json`: all 169 towns
  as printed, each row with its page, every row checked against the rendered pages. The app offers
  a town's values in **Project → Adopted code and site** (below) and applies them only when you
  accept.
- The base layer `irc-2021` is **empty** ("base tables not loaded"): fill it from your own copy of
  the IRC (Tables R602.7(1)-(3), R602.3, R602.10.3 ...). Until then `SizeHeader` returns `NoData`,
  and `LoadedPack.StatusLabel` starts `base tables not loaded` for a picker to show.
- **Revision 2** (#41, 2026-09-27) declares the guide layer `packs/layers/dca6-2015`: AWC's
  *Prescriptive Residential Wood Deck Construction Guide* (DCA 6), a guide on the **2015** IRC, read
  from AWC's PDF (URL and sha256 in its `layer.json`). It carries its two caveats (p. 1), its scope
  (items 1, 2, 4, 8 and 9, p. 2; p. 8), its eight species (Table 1, p. 3) and **Table 2** (p. 4), all
  36 joist rows with their allowable span and overhang, each note verbatim. The status label adds
  `deck tables from DCA 6-2015, a guide`. Its golden file is
  `packs/golden/us-ct-2022/dca6-table-2.golden.json`; the independent review checklist is
  `docs/code-packs/reviews/us-ct-2022/dca6-table2.md` (reviewed clean, #41 C1).
- **Revision 3** (#41 slice B2, 2026-09-27) adds the guide's **Table 3A** (p. 6; the document has a
  second "Table 3A", joist hanger capacity on p. 9, which is not this one): dimension-lumber deck beam
  spans L_B, 168 rows — Southern Pine's eight built-up sizes and the other seven species' twelve printed
  sizes, a two-name cell ("3x8 or 2-2x8") being two rows citing the same cell — by joist span L in
  upper-bound bands of 6' to 18', each note verbatim; and two scope notes from p. 5 (the beam may extend
  L_B/4 past the post face; joists do not frame in from opposite sides). Table 3B (glulam) is not
  transcribed. Golden file `packs/golden/us-ct-2022/dca6-table-3a.golden.json`; checklist
  `docs/code-packs/reviews/us-ct-2022/dca6-table3a.md`, unfilled until the independent review is done.
  `review.status` is pack-wide and stays `unreviewed`, so every line says UNREVIEWED.

**Where the app looks for packs roots** (`PackLocations.All()`): `packs/` beside the executable, then
the per-user `<config>/napkin/packs` (`%APPDATA%\napkin`, `~/Library/Application Support/napkin`, or
`$XDG_CONFIG_HOME/napkin`) for your own. Each is a packs root as described below.

## In the app

**Project → Adopted code and site…** lists every pack found in those folders as "<shortName> —
<baseCode>, in force <from>" with its id, revision, status and review state, for example "CT 2022
— IRC 2021, in force Oct 1, 2022 (pack us-ct-2022 rev 3): base tables not loaded; deck tables from
DCA 6-2015, a guide (UNREVIEWED)".
A pack that fails to load is shown with its problems, not hidden. napkin never picks one: the
choice is stored with the design (format 6), locked to a revision (with the date) or following
the newest installed revision. The same window takes the site values; empty means not entered.
When the chosen pack carries a `site-values.json` (Connecticut's does), a **Town** picker appears:
choosing a town shows the values the adopted code prints for it and where ("Bloomfield, as CT 2022
prints it: ground snow load 30 psf and ultimate wind speed 120 mph (Appendix AY, p. 157 …); seismic
design category B statewide (Table R301.2 …)"), and **Use these values** sets them, keeps every
other site value, and records the source. Nothing is filled until you press it.
Each wall says what it supports in its panel, from the values the pack's table declares, and
which bracing method is already on each of its solid segments, from the pack's methods
([building.md](building.md#wall-bracing)).

Every opening's header is then checked ([building.md](building.md)): **Sized** with the citation,
**Out of scope** citing the limit, **Input missing** naming the input and where to type it, or
**No data**. With only the shipped Connecticut pack every check is No data: "The loaded pack CT
2022 has no header table for exterior-bearing walls, so napkin cannot size this header. Nothing
is guessed: add the table to the pack directory from your copy of the code (docs/rules-engine.md).
Where to add tables: docs/rules-engine.md". To get sized headers, author the IRC tables (below) in
your per-user packs folder, as a `layers/irc-2021/tables/<table>.json` and the pack that uses it
(copy `packs/us-ct-2022` and give it your own id or a higher revision), run your golden tests,
then restart napkin and choose it.

## Author a pack from your own copy of the code

A **packs root** is a folder with this layout (all paths lower case, no spaces):

```
my-packs/
  layers/irc-2021/layer.json                 model-code base layer: publisher, code, year, sources
  layers/irc-2021/tables/<table>.json        one header-sizing table per file
  packs/us-ct-2022/pack.json                 the adopted code: id, revision, adoption, layers, sources, review
  packs/us-ct-2022/amendments/<table>.json   the state's operations on that table (may be empty)
  golden/us-ct-2022/<table>.golden.json      your golden cases, written from the source
```

1. **`pack.json`**: `schemaVersion` 1; `id` (`us-ct-2022`); `revision` (bump on any data change);
   `jurisdiction`; `adoption` (name, shortName, adoptedBy, `inForce.from`/`to`, `appliesTo`, e.g.
   `permit-application-date`); `baseCode` (`ICC`/`IRC`/year); `layers` (`["irc-2021",
   "amendments"]`); `sources`; `review` (`unreviewed` until an independent check, design §8.3).
2. **Sources** (in `pack.json` and `layer.json`): `id`, `title`, `publisher`, `url`, `printing`
   (the errata state on the document header), `retrievedOn` (yyyy-MM-dd), `sha256` of the file you
   read. Every table, row and operation names one by `source`; that is what citations print.
3. **A table** (`kind: "header-sizing"`): `table` (as printed, e.g. `R602.7(1)`), `title`,
   `wallKind` (`exterior-bearing` or `interior-bearing`), `source`, `location` (page), `inputs`,
   `outputs` (exactly header/member, jackStuds/count, kingStuds/count), `footnotes`, `rows`.
   - `inputs`: each `{name, type, band}`. Names: `supports`, `seismicDesignCategory` (`enum`,
     `exact`, with `values`); `groundSnowLoad` (`psf`), `ultimateWindSpeed` (`mph`),
     `buildingWidth`, `frostDepth` (`length`), all `upper-bound`; `headerSpan` (`length`,
     `capacity`, required). Banded columns declare `domain: {min, max}`.
   - Numbers are whole (`30`, never `30.0`); lengths are exact text (`"6ft 0in"`, `"3-1/2in"`).
   - **Footnotes**: transcribe the text, set `appliesTo` (`table` or `rows`) and classify each:
     `not-encoded` (shown with results), `as-rows` (already in the rows), `as-limit` with `limit:
     {input, above}` or `{input, equals}`, or `as-operations` with `operations` (below). An
     unclassified footnote makes the table invalid.
   - **Rows**: a stable `id`, one value per input, `header: {plies, nominal}`, `jackStuds`,
     `kingStuds`, `location` (page and row as printed), optional `footnotes: [ids]`.
4. **Amendments**: one file per table with `table`, `source`, `location` (where you checked the
   amendment list) and `operations`. Empty means "not amended" — and saying so is required. Ops:
   `add`/`amend` (with `row`), `delete` (with `rowId`), `add-table`/`amend-table` (with `table`),
   `delete-table`; each with its own `source` and `location`. They mirror the state's Add/Amd/Del.
   `amend-footnote` (with `footnote`: a whole footnote object, same id) replaces one footnote of
   the table below and keeps its rows. On a table the base layer does not have yet, it is
   **pending**: the pack still loads, `LoadedPack.Pending` lists it, and it applies the moment
   the table is added. Connecticut's R602.7(1) footnote e and R602.7(3) footnote b ship this way
   in `packs/packs/us-ct-2022/amendments/`.
5. **Golden cases** (design §8.2), written **from the source, never copied from your pack**:
   `pack`, `table`, `source`, `transcriber {who, on}`, `cases`: each has `row`, `inputs`,
   `location`, and `expect` = `sized {header, jackStuds, kingStuds}`, `outOfScope {reason,
   limitRow}`, `inputMissing {inputs}` or `noData {reason}`. Every row needs a hand-authored case;
   boundary cases (at a limit, and 1/1024" over) carry `"generated": "boundary"`.

**Run your golden tests:**

```sh
NAPKIN_PACKS_ROOT=/path/to/my-packs dotnet test tests/Napkin.Core.RulesEngine.Tests
```

Each case is its own test named `pack/table/row/index`. A pack that fails to load lists every
problem (file, table, row, message) at once. Then have someone else check every row against the
source (design §8.3) before setting `review.status` to `signed-off` with its checklist path.

### Site values by town (`packs/<id>/site-values.json`)

Optional, beside `pack.json`. Strict like every pack file (unknown fields, a missing citation, a town
listed twice or a value below 1 refuse the whole pack):

```json
{ "schemaVersion": 1, "kind": "site-values", "notes": "…", "source": "<a pack.json source id>",
  "title": "<the table's title as printed>",
  "statewide": { "seismicDesignCategory": { "value": "B", "location": "<where printed>" } },
  "municipalities": [ { "name": "Andover", "ultimateWindSpeedMph": 120, "nominalWindSpeedMph": 93,
                        "groundSnowLoadPsf": 30, "hurricaneProne": true, "location": "<where printed>" } ] }
```

`statewide` may be `{}`. The nominal wind speed and the hurricane-prone flag are carried as printed and
not used.

## Footnote operations: substitution and interpolation

Decided 2026-09-25 (Marc, #157): where the adopted text says so, napkin applies it automatically.
The engine does these two things **only** where a table's footnote declares them
(`encodedAs: "as-operations"`, `appliesTo: "table"`); nothing else in the engine interpolates.
Connecticut's footnote e, as encoded:

```json
{ "id": "e", "text": "Use 30 psf ground snow load for cases in which …", "encodedAs": "as-operations", "appliesTo": "table",
  "operations": [
    { "op": "substitute-input", "input": "groundSnowLoad", "below": 30, "use": 30,
      "when": { "input": "roofLiveLoad", "atMost": 20 } },
    { "op": "interpolate", "input": "groundSnowLoad", "between": [30, 50], "quantity": "headerSpan" } ] }
```

- **`substitute-input`**: an input strictly below `below` is taken as `use` when the `when`
  input is at most `atMost`, and the trace says so, citing the footnote. When the `when` input
  is above `atMost` the request is `OutOfScope` (`NarrowedByFootnote`) citing the footnote; when
  it is not entered, `InputMissing` names it. It is asked for only then: a snow load of 30 psf or
  more never needs a roof live load. `use` may not be below `below`. The condition inputs are the
  numeric header inputs plus `roofLiveLoad` (a site value, whole psf, entered in *Project → Adopted
  code and site*).
- **`interpolate`**: only for an input **strictly between** the two declared columns, which must
  be adjacent bands of an upper-bound column present for every group of rows. Rows at the two
  columns are paired by member; each member's span is `lowerSpan + (upperSpan − lowerSpan) ×
  (input − lower) / (upper − lower)`, kept as an exact fraction of 1/1024″ units (no floating
  point) and compared exactly to the opening; the smallest member whose span covers the opening
  is chosen. The span is **shown** rounded **down** to 1/16″. Stud counts are never interpolated:
  the larger of the two rows is used, and the trace says so. A member with a row at only one of
  the two columns is not offered. `quantity` is always `headerSpan`.
- **At a column exactly** the plain row is used; **outside** the pair nothing is interpolated
  (between 50 and 70 psf the 70 psf column applies, as for any table).
- The result's citation names the upper column's row and carries `Interpolation`: both rows and
  spans, the weight as an exact fraction, the exact and shown span, and the footnote verbatim
  with its source and page. The app shows "Interpolated between the 30 psf row (…) and the 50 psf
  row (…) (CT 2022 footnote e, p. 145)" under the header.
- Load checks: an unknown operation, a bound that is not a band, bands between the two bounds,
  two interpolations or substitutions of one input, a member twice in one cell of an
  interpolated column, or operations on a footnote not encoded `as-operations` make the pack
  invalid.

## Wall bracing

Issue #39 (M5), decided 2026-09-25: the bracing check is a **mechanism**, proven only on
SYNTHETIC packs (`tests/Napkin.Modules.Building.Tests/CodePacks/brace`, NOT CODE VALUES). No real
bracing tables ship: the shipped Connecticut pack has none, and napkin says so (**No data**).
Transcribing them from a primary source is a Backlog issue. The engine builds in **no** code's
arithmetic: everything below is data the pack declares.

A base layer may carry one file `layers/<layer>/bracing/<name>.json`:

```json
{ "schemaVersion": 1, "kind": "wall-bracing", "section": "ZZ-BRACE.1", "title": "…",
  "source": "<source id>", "location": "p. …",
  "step": "2in", "unitLength": "10ft 0in",
  "inputs":   [ { "name": "ultimateWindSpeed", "type": "mph", "band": "upper-bound", "domain": { "min": 1, "max": 199 } } ],
  "required": [ { "id": "q.w99", "ultimateWindSpeed": 99, "length": "4ft 0in", "location": "…" }, … ],
  "factors":  [ { "id": "f.wind", "section": "…", "location": "…", "when": { "input": "ultimateWindSpeed", "above": 150 }, "multiply": "5/4" },
                { "id": "f.tall", "section": "…", "location": "…", "when": { "input": "wallHeight", "above": "9ft 0in" }, "add": "1ft 0in" } ],
  "limits":   [ { "id": "l.tall", "section": "…", "location": "…", "when": { "input": "wallHeight", "above": "12ft 0in" }, "text": "…" } ],
  "methods":  [ { "id": "zz-panel", "name": "ZZ panel (synthetic)", "section": "…", "location": "…", "cap": "6ft 0in",
                  "minimumPanel": { "wallHeightDomain": { "min": "0in", "max": "12ft 0in" },
                                    "rows": [ { "id": "m.h8", "wallHeight": "8ft 0in", "length": "2ft 0in", "location": "…" }, … ] } } ],
  "footnotes": [ { "id": "a", "text": "…", "encodedAs": "not-encoded", "appliesTo": "table" } ] }
```

(The numbers are the synthetic fixture's, made up.)

**Semantics**, for one wall line (a wall's solid segments, [building.md](building.md#wall-bracing)):

1. **Inputs** a column or a condition may name: `ultimateWindSpeed` (mph), `groundSnowLoad` (psf),
   `seismicDesignCategory` (category), `buildingWidth` (length) — the project's site values — and
   `wallHeight`, the wall's own. A site value any column or condition names and the project has not
   entered is **InputMissing**; nothing is defaulted.
2. **Limits**, in order: the first whose condition holds makes the line **OutOfScope**
   (`NotPrescriptive`), citing the limit's section and id.
3. **Base row**: category columns select by equality, upper-bound columns by the smallest bound at
   least the input, exactly as header tables do. A category with no rows, or an input below the
   column's domain or above its largest band, is **OutOfScope** citing the row or table that stopped it.
4. **Required** = base length × (line length / `unitLength`) × every applicable `multiply` factor,
   then + every applicable `add` length, in the file's order; computed as an exact fraction of
   1/1024″ and **rounded UP** to `step` once. A factor applies when its `when` holds: `above` (strictly
   greater, a number or length) or `equals` (a category). Multipliers are exact fractions written
   `"n"` or `"n/d"`, never decimals.
5. **Provided** = the sum of each segment's contribution. A segment with no method contributes
   nothing ("not braced"); so does one whose method this pack does not have. Otherwise the method's
   minimum panel length is looked up by wall height (smallest `wallHeight` bound at least the wall's);
   a wall outside `wallHeightDomain` is **OutOfScope** citing the method's row; a segment shorter than
   the minimum contributes nothing; a longer one contributes min(its length, `cap`) (`cap: null` for
   none) **rounded DOWN** to `step`.
6. **Passes** when provided ≥ required, else **Fails** with the shortfall; both cite the section and
   base row and carry the whole working (`BracingWorking`: base row, each factor with its condition,
   section and location, the exact and rounded required length, each segment's contribution and why).
   With no pack, or a pack with no bracing file, **NoData**.

**Load checks** (the pack is `Invalid` with every problem listed): unknown fields; `kind` other
than `wall-bracing`; a non-positive `step`, `unitLength`, `cap` or minimum panel; an input napkin
cannot supply or of the wrong type; a category column not `exact`, any other not `upper-bound`;
gaps (bands stopping short of the domain's max, a combination of bands without a row) and overlaps
(two rows for one cell, two minimum-panel rows for one height) in any band; a factor or limit
without a non-blank `section` and `location` (every factor is cited); a factor with both or neither
of `multiply`/`add`, or a multiplier that is not a positive whole fraction; a condition with both or
neither of `above`/`equals`; duplicate ids; an unclassified footnote, and any footnote not
`not-encoded`/`as-rows` or not applying to the whole section (limits and factors are declared as
such, not as footnotes); more than one bracing file in a layer.

**Golden files** for bracing use `"section"` instead of `"table"`. Each case gives `inputs`
(`lineLength`, `wallHeight`, `segments: [{ length, method }]` and the site values) and expects
`passes { required, provided, factors }`, `fails { required, provided, shortfall, factors }`
(`factors`: the ids applied, in order), `outOfScope { reason, limitRow }`, `inputMissing { inputs }`
or `noData { reason }`. Every base row and every factor needs a hand-authored case.

**Recompute**: `Recompute.Bracing(pack, lines)` and `Recompute.DiffBracing(before, after)` —
`PassToFail` and `ToFail` (newly short), `ToOutOfScope`, `ToNoAnswer` (no longer computable),
`FailChanged`, `PassChanged`, `FailToPass`, `ToPass`, and the rest; `NewlyFlagged` and
`NoLongerComputable` for a summary. The app uses them on every edit and on a code switch.

**Not yet expressible**, and deliberately so: which of these a real adopted text needs is unknown
until its bracing provisions are read from a primary source, so none is guessed at here. The schema
cannot yet say: a line made of several walls, the spacing between lines, or a storey; a condition on
more than one input at once, or a range; a factor that depends on the line's own length, on where a
segment sits along the line, or on its method; a contribution that is not min(length, cap) — one that
scales with the segment's length or height, or that differs at the line's ends; a minimum panel that
depends on anything but wall height; rules for mixing methods on one line; interpolation of any kind;
overlays amending the bracing file (a pack with other provisions uses another base layer); inputs
beyond the five above. When the tables are read, whatever they need that is missing is added to the
schema then, with its own load checks and golden cases.

## Deck tables (#198)

A base layer may carry a `deck/` directory ([`deck-and-porch.md`](./design/deck-and-porch.md) §3), each
file one of four kinds, at most one of each (and one `member-span` table per `use`):

| `kind` | inputs it may declare | outputs per row |
|---|---|---|
| `member-span`, `use: deck-joist` | `supports`, `species`, `member` (category, exact); `spacing` (length, exact) | `span`, and `overhang` when the table declares `overhangLimit` |
| `member-span`, `use: deck-beam` | `supports`, `species`, `member` ("(2) 2x10"); `joistSpan` (length, upper-bound) | `span` |
| `member-span`, `use: rafter` | `species`, `member`; `spacing` (exact); `groundSnowLoad`, `roofLiveLoad` (psf, upper-bound) | `span` |
| `deck-ledger` | `member`; `joistSpan` (upper-bound) | `fastener` (text as printed), `spacing` |
| `deck-footing` | `tributaryArea` (`sqft`, upper-bound); `soilBearing` (psf, **`lower-bound`**) | `footing` (text as printed) |
| `deck-guard-stair` | none: a provisions file, `guard` and `stair` objects whose items may each be `null` ("not covered by this pack") | — |

A **`lower-bound`** column selects the largest bound *at most* the input — a stronger soil is never
rounded up to a column it does not reach — and an input below the smallest bound is out of scope; its
bands must start at the domain's `min`. Header tables refuse it. Every deck footnote is
`not-encoded`: it is shown with the result. A pack may also carry `packs/<id>/frost.json`
(`kind: frost`, a cited `frostLineDepth`), which the deck check offers as a suggestion, never
applies.

**Overhangs** (#41, deck-guide-pack §3.1). A deck-joist table whose source prints an allowable overhang
gives every row an `overhang` length and declares, once, the cap the source puts on it as a fraction of
the span: `"overhangLimit": { "fraction": "1/4", "of": "span", "location": "…" }` (DCA 6: "the lesser of
allowable overhang, L_O, or one fourth the joist span, L/4", p. 3). All or none: a table with the cap
gives every row its overhang, and a row with an overhang needs the cap; the fraction is a positive
exact `"n/d"` of at most 1, `of` is `span`; no other kind or use may carry either.
`DeckEvaluator.CheckCantilever(pack, joists, cantilever)` asks the joists' row (same scope, species
group and lookup as the span check) and answers **Passes** or **Short** against the lesser of the row's
overhang and the fraction of the actual joist span, compared exactly (the fraction of the span is
rounded down to 1/1024″, which is exact for a verdict on a whole-unit cantilever). A joist table
without `overhangLimit` does not cover an overhang: a cantilever under it is **Out of scope**
(column `cantilever`). The deck's code check adds a **Cantilever** line whenever the deck has one.

**Beams** (#41 slice B2, deck-guide-pack §3.2). A `deck-beam` table's `member` is napkin's name for the
beam, plies and nominal size ("(2) 2x10", "(1) 4x8"); a printed "2-2x10" is `(2) 2x10`, and a cell printed
with two sizes is two rows citing the same cell. Its `joistSpan` column bands on the joists' span L as the
joist table defines it (face of support to face of support). The deck check asks it about the **beam span
post centreline to post centreline** — `DeckFraming.BeamSpan`, (deck width − post width) ÷ (posts − 1),
exact, the source's own measure (DCA 6 Appendix B, p. B2; Decision 8 of the design note) — and the line
says "post centre to post centre".

`DeckEvaluator.CheckSpan` answers **Passes** or **Short** (by how much), `SizeLedger` and `SizeFooting`
**Sized** (the ledger with napkin's own fastener count, ⌈length ÷ spacing⌉ + 1), and every one of
them **Out of scope** (citing the scope limit that held, or naming the column no row covers),
**Input missing** or **No data** as the header check does. Every deck line that answers from a
pack's data says `UNREVIEWED: values not yet checked against the source.` until the pack is signed
off. The synthetic tables are in `tests/Napkin.Modules.Building.Tests/CodePacks/deck` (NOT CODE VALUES);
the real tables are DCA 6-2015 Tables 2 and 3A under the Connecticut pack (above); the rest are M10's (#40, #42, #43).

## Guide layers, scope limits and species groups (#238)

Design: [`design/deck-guide-pack.md`](design/deck-guide-pack.md). A pack's deck tables may come from a
**guide**: a document that is not the adopted code (AWC's DCA 6, a guide on the 2015 IRC, under a pack
adopting the 2021 IRC). A guide is never a pack's identity and never a base layer; the pack declares it:

```
my-packs/
  layers/<guide-id>/layer.json         kind "guide": what it is, its caveats, scope, species, sources
  layers/<guide-id>/deck/<table>.json  the same deck kinds a base layer's deck/ may carry
  packs/<id>/pack.json                 "guides": [ { "id": "<guide-id>", "notes": "…" } ]
```

```json
{ "schemaVersion": 1, "kind": "guide", "id": "zz-guide-2099",
  "guide": { "shortName": "ZZ GUIDE", "title": "…", "publisher": "…", "basis": { "publisher": "ICC", "code": "IRC", "year": 2098 } },
  "caveats": [ { "id": "irc-governs", "text": "<verbatim>", "location": "p. 1" } ],
  "scope": { "limits": [ { "id": "s.loads", "when": { "input": "supports", "notIn": ["zz-deck"] }, "text": "<verbatim>", "location": "…" },
                         { "id": "s.snow",  "when": { "input": "groundSnowLoad", "above": 77 }, "text": "…", "location": "…" },
                         { "id": "s.shape", "when": { "input": "deckLength", "aboveInput": "deckWidth" }, "text": "…", "location": "…" } ],
             "notes":  [ { "id": "n.single", "text": "<verbatim>", "location": "…" } ] },
  "species": [ "zz-fir", "zz-hem", "zz-cedar" ],
  "sources": [ { "id": "…", "title": "…", "publisher": "…", "url": "…", "printing": "…", "retrievedOn": "2026-09-27", "sha256": "…" } ] }
```

(The synthetic guide's words and numbers, made up.)

- **What every answer says.** A line from a guide's table cites the table and row and adds napkin's own
  clause, composed from the manifests: "… — a guide on the 2098 IRC, not ZZ DECK's adopted IRC 2099; the
  IRC governs where they differ (p. 1)". The last part appears only when the guide has a caveat with the
  reserved id `irc-governs` (its own statement that the model code governs), citing its location. The
  deck's code check starts with the guide's paragraph: what it is and is not, every caveat, limit and note
  verbatim with its location (`DeckGuide.Paragraph`). The picker's status adds "deck tables from ZZ GUIDE,
  a guide". Guard and stair provisions from a guide carry the clause too.
- **No precedence.** A kind (for `member-span`, a `use`) that both the base layer and a guide declare,
  or two guides, makes the pack invalid, naming both files: remove the guide entry when the adopted code's
  table arrives.
- **Scope limits**, before every lookup in the guide's tables: the guide's `scope.limits`, then the
  table's own `limits` (any deck table may carry them, same shape), in order. The first whose input is not
  entered is **Input missing** naming it; the first that holds is **Out of scope** citing its text and
  location ("Beyond the scope of ZZ GUIDE: "…" (ZZ GUIDE p. 2, item 9). Get it engineered."). Inputs:
  `supports`, `species`, `member` (categories; `member` is the lookup's own member, so a footing has none —
  put member limits on the table they concern), `groundSnowLoad` (the site's, psf), `deckLength` (out from
  the house) and `deckWidth` (along it). Forms, exactly one per limit: `above` (a number or length, strictly
  greater), `equals`, `in`, `notIn` (categories), `aboveInput` (a length above another length). The deck
  check supplies them (`DeckScopeInputs`; `SizeLedger`/`SizeFooting` take them as an argument, `SpanRequest`
  carries `DeckLength`/`DeckWidth`).
- **Species groups.** A table may print its species in groups (`speciesGroups`: `group`, `species`,
  `location`), its `species` column's values then being the group names; the typed species is read as its
  group, and the line says which. Under a guide a table with a species column must declare its groups,
  and they place every species the guide lists, each once.
- **Load checks** (all listed at once): a guide listed twice, not resolving, naming the base layer, with no
  `deck/` files, or whose `layer.json` is not `kind: "guide"`; a `kind` on a base layer; a guide without a
  `supports` limit (the porch case must always have an answer); an unknown input or form, two forms, `above`
  on a category, `in`/`notIn`/`equals` on a number or length, `aboveInput` not between two lengths; a limit,
  note or caveat without its text or location; an id used twice among a guide's limits and notes and a
  table's limits; empty or repeated species; species groups that are not the column's values, place a
  species twice, place one the guide does not list, or miss one it does; a deck file whose source is not
  the guide's.

**Deck golden files** use `"deck": "<table>"` (in place of `"table"`). A case's `inputs` are the table
kind's own — `member`, `span` (the actual span), `spacing`, `joistSpan`, `roofLiveLoad` for a span table;
`member`, `joistSpan`, `ledgerLength` for the ledger; `tributaryArea` (whole square feet, or two lengths
multiplied) and `soilBearing` for a footing — plus a guide's scope inputs `supports`, `species`,
`groundSnowLoad`, `deckLength`, `deckWidth`. `expect` is one of `passes { allowed }`, `short { allowed,
over }`, `sized { text, spacing?, count? }` (each with the case's `row`), `outOfScope { limit }` or
`outOfScope { column }`, `inputMissing { input }` or `noData {}`. A deck-joist case may add `cantilever`:
it then asks the cantilever check, `span` being the actual joist span, and `allowed` is the lesser of
the row's overhang and the cap. Every row and every scope limit of the
table **and its guide** needs a hand-authored case, so each table proves it applies the scope; a row with
an overhang also needs a hand-authored cantilever case whose `allowed` is the row's own overhang (ask on a
span long enough that it governs), so the transcribed overhang is checked, not only the cap. The boundary
pairs (each row's span at and 1/1024″ past its allowed span; each row's cantilever at and 1/1024″ past
what it allows on its first cantilever case's span; each banded input at its bound and one step
past; each `above`/`aboveInput` limit at its bound and one step over) are generated by
`GoldenRunner.DeckBoundaries(pack, json)` from the rows and the file's own hand cases, and committed with
`"generated": "boundary"`; a file whose committed pairs are not the generator's fails and prints them. The
synthetic files are in `tests/Napkin.Modules.Building.Tests/CodePacks/deck/golden/`; the shipped packs'
are in `packs/golden/` and run with every test run.

**Recompute**: `Recompute.DiffDeck(before, after)` over `DeckCheckKey(element, check)` —
`PassToShort`, `ToShort`, `ToOutOfScope` (newly flagged), `ToNoAnswer` (no longer computable),
`RowChanged`, `OutOfScopeChanged`, `ShortToPass`, `ToAnswer`, `NoAnswerToOutOfScope`, `NoAnswerChanged`,
`SpanMoved` (the same row, only the span asked about moved; not said) and `CitationOnly` — comparing what
a result says, never object identity. `DeckCheck.Report`/`Changes` say them per deck on every edit and on a
code switch, whose summary counts them.

## Using it from code

```csharp
PackLoadResult r = PackLoader.Load(packsRoot, "us-ct-2022");      // or PackCatalog.Discover(packsRoot)
HeaderRequest request = new(supports, WallKind.ExteriorBearing, span, site); // site: SiteInputs, null = not entered
HeaderResult h = RulesEngine.SizeHeader((r as PackLoadResult.Loaded)?.Pack, request); // no pack → NoData
```

```csharp
BracingRequest bracing = new(new BracedWallLine(lineLength, wallHeight, segments), site); // segments: BracedSegment(label, length, method or null)
BracingResult b = RulesEngine.CheckBracing(pack, bracing); // Passes, Fails, OutOfScope, InputMissing or NoData
```

`HeaderResult` is exactly one of `Sized` (member, jack and king studs, `Citation` with the row,
layer, source and a band-by-band trace), `OutOfScope` (reason, the cited limit, "get an
engineer"), `InputMissing` (which site values to enter) or `NoData`.

## What the engine refuses to do

- Guess: no extrapolation, rounding of inputs or epsilon, and no interpolation except where a
  footnote declares it (above); a request no row covers is `OutOfScope` citing the limit that
  stopped it.
- Default a hazard: an unset snow load, wind speed or width is `InputMissing`, never 0.
- Load a doubtful pack: unknown fields, duplicate keys, `6.0`, off-grid lengths, unknown enums,
  gaps or overlaps between bands, an unclassified footnote, a dangling source, a conflicting
  or dangling overlay operation, a file over 1 MiB, or `schemaVersion` other than 1 — the pack is
  `Invalid` with every problem listed; there is no converter.
- Use a floating-point number anywhere (a test checks the public API).
