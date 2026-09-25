# The rules engine: packs, results and your own tables

`Napkin.Core.RulesEngine` sizes a wall-opening header from an **adopted-code pack** and cites the
edition, table and row every answer came from. Design: [`design/rules-engine-model.md`](design/rules-engine-model.md).

## Data status: no real tables ship

napkin contains **no building-code table values**. Whether transcribed IRC/state tables may be
shipped is Marc's decision (DESIGN.md, design Decision 7) and is not made. The engine is tested
on synthetic fixtures (`tests/Napkin.Core.RulesEngine.Tests/Fixtures`, `Golden/`, marked
`SYNTHETIC TEST DATA - NOT CODE VALUES`) and on the shipped Connecticut pack described below. With
no pack, or a pack without a table, the answer is `NoData` - napkin never guesses.

**`packs/` (repo root, shipped beside the executable)** holds the first real pack, **Connecticut
2022** (`packs/packs/us-ct-2022`, on the 2021 IRC as amended; CT 2026 is not in force yet). It was
read from Connecticut's own document (2022 CSBC w/ Errata #1, ED October 1, 2022; sha256 and URL in
`pack.json`, retrieved 2026-09-25):

- Loaded and cited (`ct-overlay-data.json`): Table R301.2 seismic design category B and frost line
  depth 42" (p. 131), snow and wind "as set forth in Appendix AY" (p. 131), and the verbatim R602.7
  amendments, Table R602.7(1) footnote e and Table R602.7(3) footnote b (p. 145), both classified
  **not-encoded** (the engine cannot substitute an input, and there is no interpolation).
- Appendix AY (pp. 157-160) is a per-municipality table of wind speeds and ground snow loads. It
  is **not transcribed**; enter your town's values as project site inputs.
- The base layer `irc-2021` is **empty** ("base tables not loaded"): fill it from your own copy of
  the IRC (Tables R602.7(1)-(3), R602.3, R602.10.3 ...). Until then `SizeHeader` returns `NoData`,
  and `LoadedPack.StatusLabel` is `base tables not loaded` for a picker to show.

**Where the app looks for packs roots** (`PackLocations.All()`): `packs/` beside the executable, then
the per-user `<config>/napkin/packs` (`%APPDATA%\napkin`, `~/Library/Application Support/napkin`, or
`$XDG_CONFIG_HOME/napkin`) for your own. Each is a packs root as described below. The picker UI is
issue #19.

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
     {input, above}` or `{input, equals}`. An unclassified footnote makes the table invalid.
   - **Rows**: a stable `id`, one value per input, `header: {plies, nominal}`, `jackStuds`,
     `kingStuds`, `location` (page and row as printed), optional `footnotes: [ids]`.
4. **Amendments**: one file per table with `table`, `source`, `location` (where you checked the
   amendment list) and `operations`. Empty means "not amended" — and saying so is required. Ops:
   `add`/`amend` (with `row`), `delete` (with `rowId`), `add-table`/`amend-table` (with `table`),
   `delete-table`; each with its own `source` and `location`. They mirror the state's Add/Amd/Del.
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

## Using it from code

```csharp
PackLoadResult r = PackLoader.Load(packsRoot, "us-ct-2022");      // or PackCatalog.Discover(packsRoot)
HeaderRequest request = new(supports, WallKind.ExteriorBearing, span, site); // site: SiteInputs, null = not entered
HeaderResult h = RulesEngine.SizeHeader((r as PackLoadResult.Loaded)?.Pack, request); // no pack → NoData
```

`HeaderResult` is exactly one of `Sized` (member, jack and king studs, `Citation` with the row,
layer, source and a band-by-band trace), `OutOfScope` (reason, the cited limit, "get an
engineer"), `InputMissing` (which site values to enter) or `NoData`.

## What the engine refuses to do

- Guess: no interpolation, extrapolation, rounding of inputs or epsilon; a request no row covers
  is `OutOfScope` citing the limit that stopped it.
- Default a hazard: an unset snow load, wind speed or width is `InputMissing`, never 0.
- Load a doubtful pack: unknown fields, duplicate keys, `6.0`, off-grid lengths, unknown enums,
  gaps or overlaps between bands, an unclassified footnote, a dangling source, a conflicting
  or dangling overlay operation, a file over 1 MiB, or `schemaVersion` other than 1 — the pack is
  `Invalid` with every problem listed; there is no converter.
- Use a floating-point number anywhere (a test checks the public API).
