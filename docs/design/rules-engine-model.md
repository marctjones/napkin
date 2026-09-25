# Rules engine: adopted-code packs, amendment overlays and code locking

Status: **Implemented through M4 (0.97.0-beta) and M5 (0.101.0-beta), 2026-09-25.** #13-#19 shipped
at Marc's direction ("Implement M4"); #12, tracking this document's own sign-off, is still open.
What was built, and where it departs from the text below, is in "Scope of the first
implementation" just after the milestones paragraph.

Design document for issue #12, written by Fable per [`PLAN.md`](../../PLAN.md). It decides what
#13 (evaluator, out-of-scope results, citations), #14-#17 (the four adopted-code packs), #19 (the
per-project code picker with recompute) and #22 (Pennsylvania municipal overlays) build on, and
what #6 (project file) and #18 (walls and openings) need to know about the rules engine. Settled
decisions from [`DESIGN.md`](../../DESIGN.md) are taken as given: packs keyed by adopted code,
not IRC year (§11); every result cites edition, table and row (§2, §7); "out of prescriptive
scope" is a first-class result, never an extrapolation (§5.4); site hazards are user-entered, never
defaulted (§5.3); imperial only (§10); exact fixed-point lengths, never doubles
([`geometry-model.md`](./geometry-model.md) §1); and the beta policy — breaking changes always
allowed, no migration code (§12).

**This document designs the container and the process, not the content.** Nothing in it states
what any building code says. Every table value, band boundary, footnote and rounding rule is
"unverified — must be read from the primary source" until a transcriber reads it from the
adopted text and an independent reviewer checks it (§8). Examples below that look like table
rows use obviously synthetic values and are marked as such.

**Milestones.** Marc approved: **M4 "Check"** = walls and openings with Connecticut 2026 header
sizing (IRC 2024 base, R602.7), citations and out-of-scope results, one pack, per-project picker;
**M5 "Brace and compare"** = the wall-bracing check (R602.10) plus a second pack (CT 2022) proving
code locking and recompute. MA, PA and PA municipal overlays come later. Each section below says
what M4 needs, what M5 adds, and what waits.

## Scope of the first implementation (#13, 2026-09-25)

**Edition.** M4 is built on the **Connecticut 2022 State Building Code (2021 IRC as amended)**,
pack id `us-ct-2022`. The CT 2026 code is not in force (delayed; only a public-comment draft
exists), so everything below that says "CT 2026 / IRC 2024" for M4 now reads "CT 2022 / IRC 2021";
the 2026 code becomes a second pack once adopted (PLAN.md M4/M5 rows).

**No real table rows ship yet.** Marc decided (#157, 2026-09-25) that napkin may ship transcribed
code tables, each read from a primary or official source in the task that writes it and cited
beside the data; none has been transcribed, so no IRC table values are in the repo. The engine is built and tested
on SYNTHETIC fixtures under `tests/Napkin.Core.RulesEngine.Tests/Fixtures` and `Golden/` (ids like
`TEST-HEADER-TABLE`, "IRC 2099", a banner in every file). Packs load from a **packs root on disk**
(`layers/`, `packs/`) through `DirectoryPackSource` — the directory a person fills from their own
copy of the code (`docs/rules-engine.md`); embedded packs (§1.2) and Decision 6 wait for real data.

**Deviations from the text, and why.**
- *Result kinds (§3.1).* `HeaderResult` has four members: `Sized`, `OutOfScope`, `InputMissing`,
  `NoData`. `NoData` exists because a pack a person fills may legitimately lack a table (an
  incomplete pack, not a broken one), and a project may have no pack; the honest answer is "no
  data", never a guess. `InputMissing` exists because a hazard is required only by the tables
  that use it: `SiteInputs` fields are nullable meaning "not entered", there are no default
  values anywhere (a reflection test checks), and an unset input a table needs is named in the
  result, never defaulted.
- *Table schema (§1.4).* A header table declares `wallKind` (so the engine picks a table without
  knowing table numbers); column names come from a closed set of request fields (`supports`,
  `groundSnowLoad`, `ultimateWindSpeed`, `seismicDesignCategory`, `buildingWidth`, `frostDepth`,
  `headerSpan`); footnotes declare `appliesTo` (`table` or `rows`) and an `as-limit` footnote a
  `limit` (`above` a value or `equals` a category); rows may list their footnotes; every file may
  carry a `notes` string. The loader is the schema; `Packs/schema/*.json` is not committed.
- *Overlays (§1.3).* Row operations `add`/`amend`/`delete` and table operations
  `add-table`/`amend-table` (metadata, rows kept)/`delete-table`. Base-layer tables cite the
  layer's own `sources`; an overlay cites the sources of the pack that owns it. Every table below
  needs an overlay file in each overlay layer, even with no operations.
- *Interpolation and rounding (§4.4).* **DECIDED 2026-09-25 (Marc, #157):** a footnote that
  permits interpolation is applied automatically. Footnotes may be encoded `as-operations` with a
  `substitute-input` and an `interpolate` operation (semantics in §4.4); overlays gain
  `amend-footnote`, which is *pending* (listed on `LoadedPack.Pending`, not an error) while the
  base table is not loaded. Connecticut's R602.7(1) footnote e and R602.7(3) footnote b are
  encoded this way. `roofLiveLoad` is a site input (scene format 7), asked for only when a
  substitution's condition is reached. No other rounding rule is implemented.
- *Band validation (§4.3).* Rows are compared by value, not file order (so P4's order
  independence holds): a duplicate bound in a cell is an overlap; every combination of
  upper-bound bands must have rows (no holes); each upper-bound column's top band must equal its
  declared `domain.max`.
- *Not built yet:* the loosening lint (§6.3), checklist-hash tests (§8.3 steps 3–4), the
  municipal revision check (§6.1), `BracingResult`, `results.json`. `CodeSelection` (pack id,
  revision, locked or following, lock date), `Recompute.Headers` and `Recompute.Diff` exist for
  #19; `ChangeKind` is adapted to header results.

## 0. Two facts from the primary sources that shape everything below

Read in this task from the [2022 Connecticut State Building Code
PDF](https://portal.ct.gov/-/media/DAS/Office-of-State-Building-Inspector/2022-State-Codes/2022-CSBC-Final.pdf)
(title page, table of contents and introduction only; no table content was read):

1. **The state's adopted text is a list of amendments that incorporates the model code by
   reference.** The document's introduction adopts the 2021 International Residential Code "as
   amended herein"; the body is organised as "Amendments to the 2021 International Residential
   Code", and each entry is annotated **Add**, **Amd** (substitution) or **Del** (deletion) against
   the referenced standard. The IRC text itself is not reproduced. So "the value published in the
   state's adopted text" is, for an unamended table, the model-code value as incorporated — and
   the state document is the authority on *which* rows survive and which are replaced. This is
   the shape the pack format mirrors (§1.3, §6): a model-code base layer plus an overlay whose
   operations are exactly Add / Amd / Del.
2. **Locking is to the permit application date.** The same introduction states that the 2022
   code applies to all work for which a permit application was made on or after the date of
   adoption. That is the rule DESIGN.md §5.4 anticipated, now sourced for CT. Whether MA and PA
   state the same rule, and PA's contract-date transition, are unverified (DESIGN.md §11).

Also read: the document header carries "(w/ Errata #1)". A pack must therefore record which
printing or errata state of the source it was transcribed from (§1.1).

Not read (fetches failed in this task): the CT 2026 code document, the MA 780 CMR pages. The
[PA municipal register](https://www.pa.gov/agencies/dli/programs-services/labor-management-relations/bureau-of-occupational-and-industrial-safety/uniform-construction-code-home/ucc-municipal-code-change-ordinances)
was fetched and its structure is described in §6.4.

## 1. The adopted-code pack

### 1.1 Identity

A **pack** is the selectable unit in the per-project picker (#19). Its identity is the adoption,
never the model-code year:

```json
// packs/us-ct-2026/pack.json  (values illustrative; source fields unverified until #14)
{
  "schemaVersion": 1,
  "id": "us-ct-2026",
  "revision": 1,
  "jurisdiction": { "country": "US", "state": "CT", "municipality": null },
  "adoption": {
    "name": "2026 Connecticut State Building Code",
    "shortName": "CT 2026",
    "adoptedBy": "Connecticut Department of Administrative Services, Office of State Building Inspector",
    "inForce": { "from": "2026-09-18", "to": null },
    "appliesTo": "permit-application-date",
    "transitionNotes": null
  },
  "baseCode": { "publisher": "ICC", "code": "IRC", "year": 2024 },
  "layers": [ "irc-2024", "amendments" ],
  "sources": [
    {
      "id": "csbc-2026",
      "title": "2026 Connecticut State Building Code",
      "publisher": "Conn. DAS, Office of State Building Inspector",
      "url": "UNVERIFIED — to be recorded by #14 from the DAS site",
      "printing": "UNVERIFIED — record the errata state shown on the document header",
      "retrievedOn": "2026-09-21",
      "sha256": "UNVERIFIED — hash of the PDF as retrieved"
    }
  ],
  "review": { "status": "unreviewed", "checklist": null }
}
```

- `id` is the stable key stored in project manifests (#6). Lower-case: country, state, then the
  **adoption's own designation** where it has one (`us-ct-2022`, `us-ct-2026`,
  `us-ma-780cmr-10`) and otherwise the year the adoption came into force (`us-pa-2026` for the
  PA UCC in force Jan 1, 2026). Never the model-code year: `us-pa-2021` would be the IRC year
  masquerading as identity. A municipal pack (§6.4) appends the municipality:
  `us-pa-2026-<municipality>`.
- `revision` is an integer bumped on **any** change to the pack's data — a corrected row, a new
  table, a changed citation. It is stored in the project manifest alongside `id` so that a
  project can say which data its results were computed against, and so that a revision change
  triggers a recompute-and-diff on open (§7.3). No compatibility between revisions is promised or
  implemented (beta policy); the only thing the app does with an old revision number is show the
  diff.
- `schemaVersion` is the pack *format* version, exact-match like `formatVersion` in the project
  file (geometry-model §6): the loader accepts exactly the version it was built for and refuses
  anything else, naming both numbers. No migration code.
- `adoption.inForce` and `appliesTo` are what the picker shows and what the permit-date guidance
  in §7.2 uses. `appliesTo` is an enum: `permit-application-date` (CT, sourced in §0),
  `permit-issuance-date`, or `see-notes` with `transitionNotes` holding the sourced rule text
  (Pennsylvania's contract-date transition, once #17 has verified it against 34 Pa. Code).
- `baseCode` is descriptive, not identity. It is what the picker shows after the dash
  ("CT 2026 — IRC 2024") and what a citation prints as the model code.
- `sources` lists every document the pack's rows were read from, with retrieval date and hash.
  Every citation (§2) points at one of these by `id`.
- `review` records the two-role process of §8: `status` is `unreviewed`, `in-review` or
  `signed-off`; `checklist` is the repo path of the signed-off checklist. What the app does with
  an unreviewed pack is Decision 5 in §13.

**Packs are keyed by adopted code, so three packs sharing the 2021 IRC are three packs.** CT 2022,
MA 780 CMR 10th and PA UCC 2021 each get their own `pack.json`, in-force dates, sources, overlays,
golden tests and review checklists. What they share is a base layer (§1.3), which is an
ingredient, never a selectable code.

### 1.2 Directory layout

Packs are data files under version control inside `Napkin.Core.RulesEngine`, embedded as
resources so the app is offline and self-contained (DESIGN.md §7). Discovery is dynamic: the
catalog enumerates embedded `pack.json` files at startup, so adding a pack adds a picker entry
with no UI change (DESIGN.md §5.4). User-installed packs from disk are not in the first betas
(Decision 6).

```
src/Napkin.Core.RulesEngine/
  Packs/
    schema/
      pack.schema.json              // JSON Schema for pack.json
      layer.schema.json             // for a base layer's manifest
      table.header-sizing.schema.json
      table.wall-bracing.schema.json
      table.fastener-schedule.schema.json
      overlay.schema.json
    layers/                         // model-code base layers: ingredients, not selectable
      irc-2024/
        layer.json                  // publisher, code, year, printing, sources
        tables/
          r602.7-1.json             // one file per table; the table id lives inside
          r602.7-2.json
      irc-2021/                     // arrives with the first 2021-based pack (M5: CT 2022)
        ...
    packs/                          // adopted codes: what the picker lists
      us-ct-2026/
        pack.json
        amendments/
          r602.7-1.json             // overlay for one table; may contain zero operations
          r602.7-2.json
      us-ct-2022/                   // M5
        pack.json
        amendments/...
      us-ma-780cmr-10/              // later
      us-pa-2026/                   // later
      us-pa-2026-<municipality>/    // later; a pack whose layers add a municipal overlay
docs/code-packs/
  reviews/
    us-ct-2026/
      r602.7-1.md                   // the signed-off row-by-row checklist (§8.3)
tests/Napkin.Core.RulesEngine.Tests/
  Fixtures/                         // synthetic packs with made-up values (§11.1)
  Golden/
    us-ct-2026/
      r602.7-1.golden.json          // authored from the source, never from the pack (§8.2)
```

File names avoid parentheses and spaces (`r602.7-1.json` for Table R602.7(1)); the exact table
designation is a field inside the file, and it is what citations print.

### 1.3 Layers and composition

A pack is an ordered list of **layers**. The first is a model-code base layer (`layers/irc-2024`);
each later one is an overlay directory inside the pack (`amendments/`) or, for a municipal pack,
a reference to the state pack's overlay followed by the municipal overlay (§6.4). Composition is
per table, top-down, by row operations that mirror the annotations the CT document itself uses:

| Overlay operation | CT annotation | Meaning |
|---|---|---|
| `add` | Add | A new row (or a new table) not in the layer below. Fails if the id exists. |
| `amend` | Amd | Replaces the row (or the whole table, or a table's metadata) with the same id. Fails if the id does not exist below. |
| `delete` | Del | Removes the row or table. Fails if the id does not exist below. |

```json
// packs/us-ct-2026/amendments/r602.7-1.json  (illustrative; whether CT amends this table is
// UNVERIFIED — an overlay with an empty "operations" list is the normal case for an unamended
// table, and is still required, so that "not amended" is a reviewed statement, not an omission)
{
  "schemaVersion": 1,
  "table": "R602.7(1)",
  "source": "csbc-2026",
  "location": "UNVERIFIED — page or section of the amendment, or the statement that none exists",
  "operations": []
}
```

An operation carries its own `source` and `location` (§2), so a composed row knows which layer
produced it. Rules enforced at load (§9): no two operations in one overlay target the same id;
every `amend`/`delete` target exists below; every `add` target does not; the composed table
passes the band validation of §4.3. Two overlays in one pack are applied in `layers` order; the
municipal overlay is always last.

**Why a shared base layer and not a flat pack per state.** Both work with everything else in
this document; the choice is Decision 1 in §13, recommended as follows. The state document *is*
an overlay (§0): encoding it as one makes the amendment list an explicit, reviewable diff instead
of a composition done in the transcriber's head and invisible afterwards. Three 2021-based packs
share one transcription of the unamended rows, each with its own overlay, golden tests and review
— the review is of the composed values against the state's adopted text either way (§8), so the
layering costs no independence and saves two transcriptions. And the municipal layer (§6) needs
the overlay machinery regardless, so a flat design would build it anyway, later. The cost is that
M4 implements composition; it is a small function (three operations over dictionaries keyed by
id) and the M4 pack exercises it with an empty overlay, which is the trivial case.

### 1.4 Tables as data

Every table file declares a `kind`, which selects both its JSON schema and the evaluator that
reads it (§3.4). The first betas have three kinds:

| `kind` | Tables | Milestone |
|---|---|---|
| `header-sizing` | R602.7(1), R602.7(2) | M4 |
| `wall-bracing` | R602.10 (a method with sub-tables, §3.5) | M5 |
| `fastener-schedule` | R602.3(1) | later (§10) |

A table of unknown kind makes the pack invalid (§9). Adding a kind is code (a schema and an
evaluator); adding a table of an existing kind is data.

Sketch of a `header-sizing` table. **Every value here is synthetic** — column names, bands and
results are placeholders to show the shape, chosen to look unlike any real row:

```json
{
  "schemaVersion": 1,
  "kind": "header-sizing",
  "table": "R602.7(1)",
  "title": "UNVERIFIED — the table's published caption",
  "source": "irc-2024",
  "location": "UNVERIFIED — page",
  "inputs": [
    { "name": "supports",         "type": "enum",   "values": ["roof-ceiling", "roof-ceiling-one-floor"] },
    { "name": "groundSnowLoad",   "type": "psf",    "band": "upper-bound" },
    { "name": "buildingWidth",    "type": "length", "band": "upper-bound" },
    { "name": "headerSpan",       "type": "length", "band": "capacity" }
  ],
  "outputs": [
    { "name": "header",    "type": "member" },
    { "name": "jackStuds", "type": "count" },
    { "name": "kingStuds", "type": "count" }
  ],
  "footnotes": [
    { "id": "a", "text": "UNVERIFIED — transcribed footnote text", "encodedAs": "not-encoded" }
  ],
  "rows": [
    {
      "id": "rc.snow-le-99.width-le-77ft.hdr-999",
      "supports": "roof-ceiling",
      "groundSnowLoad": 99,
      "buildingWidth": "77ft 0in",
      "header": { "plies": 9, "nominal": "2x99" },
      "headerSpan": "99ft 9in",
      "jackStuds": 9,
      "kingStuds": 9,
      "location": "UNVERIFIED — page and row"
    }
  ]
}
```

- `inputs` declares, per column, its type and its **band semantics** (§4). The evaluator is
  generic over the declared columns; it does not know the IRC's column names.
- `footnotes` are transcribed verbatim and each one says how it is encoded: `not-encoded` (the
  footnote is shown to the user with the result but no logic implements it), `as-rows` (its effect
  is already expressed in the rows), or `as-limit` (it narrows the table's scope and is cited by
  an out-of-scope result). A footnote the transcriber has not classified makes the table invalid.
  This is where interpolation and rounding rules from the code text land — see §4.4.
- `rows[].id` is stable across revisions and is what a golden test and a citation name. It is
  human-readable so a reviewer can find the row in the source; it is never parsed for meaning.
- Every row has a `location` in the source. A row without one is invalid.

### 1.5 Numbers: exact, never doubles

| Quantity | JSON representation | C# type | Rule |
|---|---|---|---|
| lengths (spans, widths, depths) | string in feet-inch syntax: `"6ft 0in"`, `"3-1/2in"` | `Length` | parsed by `Length.TryParse`; `wasRounded == true` makes the pack invalid |
| loads (ground snow, live) | integer, unit named by the column `type` (`psf`) | `int` | non-integer JSON number is invalid |
| wind speed | integer `mph` | `int` | same |
| seismic design category | string from a closed enum | `SeismicDesignCategory` | unknown value invalid |
| counts (studs, plies) | integer | `int` | same |
| members | object `{ "plies": n, "nominal": "2x10" }` — `nominal` resolved through the materials library (#7) to actual dimensions when displayed | `MemberSpec` | unknown nominal size invalid once #7 exists; until then a closed list in the schema |

No JSON number is ever read into a `double`, `float` or `decimal`. The loader uses
`System.Text.Json` with `JsonNumberHandling.Strict` and integer targets; a `6.0` in a pack file is
a load error, not a rounding.

**Why strings for lengths here when the project file stores integer units.** geometry-model §6
stores `73728` for 6′-0″ because the project file is written and read by the app. A pack file is
written by a transcriber and checked by a reviewer against a PDF row that says "6′-0″"; `73728`
is a transcription-error surface with nothing to catch it, `"6ft 0in"` is checked by eye. The
parse is exact or the pack is refused, so nothing is lost. The `ft`/`in` word syntax from
geometry-model §1.5 is used rather than `6'-0"` to avoid escaping quotes in JSON; if
`Length.TryParse` needs a syntax the packs use (`"3-1/2in"`), #13 adds it in `Core.Geometry` with
tests. This divergence from #6's "integers in stored units" rule is deliberate and is Decision 2.

A load-like quantity with a fractional published value (unverified whether any table in scope has
one) would be handled by changing that column's `type` to a finer integer unit (`psf/10`), a data
change plus a schema bump — never by admitting a double.

### 1.6 Schema for `pack.json`

The pack manifest's schema, JSON Schema draft 2020-12, abbreviated to the constraints that
matter (`additionalProperties: false` everywhere — unknown fields are a load error, §9.1). The
per-kind table schemas, `layer.schema.json` and `overlay.schema.json` follow the same style and
are #13 step 2 deliverables; the shapes they encode are §1.3 and §1.4.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "napkin:pack.schema.json",
  "type": "object", "additionalProperties": false,
  "required": ["schemaVersion", "id", "revision", "jurisdiction", "adoption", "baseCode",
               "layers", "sources", "review"],
  "properties": {
    "schemaVersion": { "const": 1 },
    "id":            { "type": "string", "pattern": "^[a-z]{2}-[a-z]{2}-[a-z0-9-]+$" },
    "revision":      { "type": "integer", "minimum": 1 },
    "jurisdiction":  { "type": "object", "additionalProperties": false,
                       "required": ["country", "state", "municipality"],
                       "properties": { "country": { "const": "US" },
                                       "state": { "type": "string", "pattern": "^[A-Z]{2}$" },
                                       "municipality": { "type": ["string", "null"] } } },
    "adoption":      { "type": "object", "additionalProperties": false,
                       "required": ["name", "shortName", "adoptedBy", "inForce", "appliesTo",
                                    "transitionNotes"],
                       "properties": {
                         "name": { "type": "string" }, "shortName": { "type": "string" },
                         "adoptedBy": { "type": "string" },
                         "inForce": { "type": "object", "additionalProperties": false,
                                      "required": ["from", "to"],
                                      "properties": { "from": { "type": "string", "format": "date" },
                                                      "to": { "type": ["string", "null"], "format": "date" } } },
                         "appliesTo": { "enum": ["permit-application-date", "permit-issuance-date",
                                                 "see-notes"] },
                         "transitionNotes": { "type": ["string", "null"] } } },
    "baseCode":      { "type": "object", "additionalProperties": false,
                       "required": ["publisher", "code", "year"],
                       "properties": { "publisher": { "const": "ICC" }, "code": { "const": "IRC" },
                                       "year": { "type": "integer" } } },
    "layers":        { "type": "array", "minItems": 1, "items": { "type": "string" } },
    "sources":       { "type": "array", "minItems": 1,
                       "items": { "type": "object", "additionalProperties": false,
                                  "required": ["id", "title", "publisher", "url", "printing",
                                               "retrievedOn", "sha256"],
                                  "properties": { "id": { "type": "string" }, "title": { "type": "string" },
                                                  "publisher": { "type": "string" }, "url": { "type": "string" },
                                                  "printing": { "type": "string" },
                                                  "retrievedOn": { "type": "string", "format": "date" },
                                                  "sha256": { "type": "string", "pattern": "^[0-9a-f]{64}$" } } } },
    "review":        { "type": "object", "additionalProperties": false,
                       "required": ["status", "checklist"],
                       "properties": { "status": { "enum": ["unreviewed", "in-review", "signed-off"] },
                                       "checklist": { "type": ["string", "null"] } },
                       "if": { "properties": { "status": { "const": "signed-off" } } },
                       "then": { "properties": { "checklist": { "type": "string" } } } }
  }
}
```

Two things the schema cannot say and the loader checks instead (§9.1): that every `layers`
entry resolves and every `source` id is referenced correctly, and that `sha256` matches the
checklist's recorded hash when `status` is `signed-off`. The `UNVERIFIED — …` placeholders in
§1.1's example would fail this schema's `sha256` pattern, which is the point: a pack cannot
ship with a placeholder where a hash belongs.

## 2. The citation model

Every result — sized, out of scope, passes, fails — carries a `Citation`, and the citation is
complete enough to find the row in the paper source without the app:

```csharp
public sealed record Citation(
    AdoptedCodeRef Code,          // pack id, revision, display name ("CT 2026"), base code ("IRC 2024")
    string Table,                 // "R602.7(1)" as printed
    string RowId,                 // pack-stable id, §1.4
    string RowLabel,              // human text built from the row's inputs: what the row covers
    CitationLayer Layer,          // ModelCode | StateAmendment | MunicipalAmendment
    SourceRef Source,             // which document, where in it, retrieved when
    ImmutableList<string> Footnotes,   // footnote ids attached to the row or table
    ImmutableList<BandMatch> Trace);   // how each input landed in its band, §4.2

public sealed record AdoptedCodeRef(string PackId, int Revision, string ShortName, string BaseCode);
public sealed record SourceRef(string SourceId, string Title, string Location, string? Url,
                               DateOnly RetrievedOn, string? Sha256);
public enum CitationLayer { ModelCode, StateAmendment, MunicipalAmendment }
```

- `Layer` is set by composition (§1.3): a row that came unchanged from the base layer cites the
  model code *as adopted by* the pack ("IRC 2024 Table R602.7(1), as adopted by CT 2026"); a row
  produced by an `amend`/`add` cites the amendment and its location in the state document.
- `Trace` is the "show your work" line: for each banded input, the value the user entered and the
  band it matched ("ground snow load 35 psf → ≤ 50 psf column"). It is what makes a result
  auditable by hand (DESIGN.md §2) and it is displayed, not tucked in a tooltip (§7).
- An `OutOfScope` result's citation is the citation of the **limit** that excluded the input: the
  last row of the band it exceeded, the footnote that narrows scope, or the table's declared
  domain (§4.3). "Get an engineer" always says which line of which table stopped.
- A citation is a value; it is serialisable and is what an exported sheet (#25) prints.

## 3. Result types

### 3.1 The principle: no silent third state

An evaluator returns exactly the states its table can produce, as a closed set of record types.
There is no `Unknown`, `Error`, `NotEvaluated` or `null`. Two classes of thing that tempt a third
state are handled elsewhere so they never appear in a result:

- **Missing inputs** (no snow load entered yet) are a project state, not a result. The input
  records in §3.2 have no optional hazard fields; the building module (#18) cannot construct a
  `HeaderRequest` until the site inputs exist, and the UI shows "enter the ground snow load" as a
  precondition banner, not as a result of a lookup that never ran (§5).
- **Broken packs** are refused at load (§9), so an evaluator never holds an invalid table.
- A request that is impossible (a zero span, a negative count) is a programming error in the
  caller and throws `ArgumentException`, the same stance geometry-model §1.3 takes on overflow.

### 3.2 Header sizing (M4)

```csharp
public sealed record SiteInputs(                     // §5; all required
    int GroundSnowLoadPsf,
    int UltimateWindSpeedMph,
    SeismicDesignCategory Seismic,
    Length FrostDepth,
    Length BuildingWidth,                            // definition per the code text, read in #14
    InputProvenance Provenance);

public sealed record HeaderRequest(
    WallSupports Supports,                           // what the wall carries; enum per table
    WallKind Kind,                                   // exterior-bearing | interior-bearing → picks the table
    Length HeaderSpan,                               // computed by #18 per the table's definition of span
    SiteInputs Site);

public abstract record HeaderResult
{
    private HeaderResult() { }                       // closed: only the nested types below exist

    /// A row covers the request. The header, stud counts and the citation of that row.
    public sealed record Sized(MemberSpec Header, int JackStuds, int KingStuds, Citation Citation)
        : HeaderResult;

    /// No row covers the request. Which limit stopped it, and its citation. This is a correct,
    /// expected answer that the UI must surface as "outside the prescriptive tables — get an
    /// engineer", never an error state and never a number.
    public sealed record OutOfScope(OutOfScopeReason Reason, Citation Limit, string Explanation)
        : HeaderResult;
}

public enum OutOfScopeReason
{
    SpanExceedsTable,        // span longer than the longest row in the matched bands
    InputAboveTableBands,    // e.g. snow load heavier than the heaviest column
    InputBelowTableBands,    // a table whose bands start above zero (unverified whether any do)
    ConditionNotCovered,     // e.g. a `supports` value the table has no rows for
    NarrowedByFootnote,      // a footnote encoded `as-limit` excludes this case
    NotPrescriptive          // the code text itself says to consult an engineer for this case
}
```

The private constructor plus nested sealed records is how C# expresses a closed union today; a
reflection test asserts that `HeaderResult` has exactly these two subtypes, so adding a third is
a visible act. Pattern matching in the UI is exhaustive by convention and checked by that test.

`Sized` and `OutOfScope` both carry a citation. A `Sized` result also carries the `Trace` in its
citation, so (synthetic values) "(9) 2×99, 9 jack, 9 king — IRC 2024 Table R602.7(1) row X, as
adopted by CT 2026; snow 98 psf → ≤ 99 column; width 70 ft → ≤ 77 ft column; span 99′-0″ ≤
99′-9″" is one value.

### 3.3 Wall bracing (M5)

R602.10 is a method, not a lookup: a required braced length derived from sub-tables and
adjustment factors, compared against what the wall line provides. Its exact inputs, sub-tables
and arithmetic must be read from the adopted text in #14/#15 — nothing below asserts them. What
is designed now is the result shape, so that #18 and the UI have something stable to build on:

```csharp
public sealed record BracingRequest(
    BracedWallLine Line,                 // length, spacing, storey, provided panel segments and method
    SiteInputs Site);

public abstract record BracingResult
{
    private BracingResult() { }

    /// The line provides at least the required braced length.
    public sealed record Passes(Length Required, Length Provided, Citation Citation) : BracingResult;

    /// The line provides less than required. A valid, expected answer: it tells the user what to
    /// add. Never a number to build to on its own.
    public sealed record Fails(Length Required, Length Provided, Length Shortfall, Citation Citation)
        : BracingResult;

    /// The method's own limits exclude this line (storey count, wind, seismic, spacing ...).
    public sealed record OutOfScope(OutOfScopeReason Reason, Citation Limit, string Explanation)
        : BracingResult;
}
```

**Implemented 2026-09-25 (#39), as a mechanism proven on synthetic packs** — the schema,
semantics and limits are in [`../rules-engine.md`](../rules-engine.md#wall-bracing). Deviations
from the sketch above: `BracedWallLine` is one wall (its length, height and solid segments, each
with the method a person assigned or none); `Passes` and `Fails` also carry the whole working
(`BracingWorking`); the engine's `InputMissing` and `NoData` are members too; a pack's own limit is
`OutOfScope(NotPrescriptive)`; `CheckBracing` is on `IRulesEngine` and on the static `RulesEngine`
(for a project with no pack); the diff is `Recompute.DiffBracing` with its own `BracingChangeKind`.

`Fails` is a third member, and it is not silent: it is the result DESIGN.md §5.3 calls "the check
most DIY openings miss". The rule is not "two states"; it is "every state is a named, cited,
expected answer, and there is no catch-all".

### 3.4 The evaluator API

```csharp
public interface IRulesEngine
{
    AdoptedCodeRef Code { get; }                     // the pack this engine was built from
    HeaderResult SizeHeader(HeaderRequest request);
    BracingResult CheckBracing(BracingRequest request);         // M5
    // Fastener schedule: later (§10)
}

public static class RulesEngine
{
    /// Builds an engine over a loaded, validated, composed pack. Pure: same pack, same request,
    /// same result. Never touches the file system after construction.
    public static IRulesEngine For(LoadedPack pack);
}
```

One `IRulesEngine` per pack; the building module holds the one for the project's adopted code
and asks it questions. Evaluators are pure functions over the composed tables. There is no
global state and no "current code" — the code is a parameter, which is what makes recompute (§7)
a loop over elements with a different engine.

### 3.5 What an evaluator may not do

- Return a number for a request no row covers. Extrapolation does not exist in this codebase.
  Interpolation exists **only** as a footnote's declared `interpolate` operation, strictly between
  the two columns it names (§4.4) — **DECIDED 2026-09-25 (Marc, #157)**, superseding "the word
  interpolate in `Core.RulesEngine` should fail review". Interpolation anywhere else, or of
  anything but the span, still should.
- Read a hazard value from anywhere but the request.
- Choose a band by rounding the input (§4.2: bands are chosen by comparison, exactly).
- Return a result without a citation, or a citation whose `RowId` is not in the composed table
  (property P3, §11.3).

## 4. Lookup semantics for banded inputs

### 4.1 Band kinds

A table column's `band` declares how an input value selects a row. The kinds are few and are
about *direction*, because direction is where a lookup goes quietly wrong:

| `band` | Column holds | Row matches when | Conservative direction |
|---|---|---|---|
| `exact` | a category (`supports`) | input equals the value | none; no match → `ConditionNotCovered` |
| `upper-bound` | the largest input the row covers (snow "≤ 30 psf", width "≤ 28 ft") | input ≤ bound, choosing the row with the **smallest** bound that is still ≥ input | up: a heavier load or wider building than the bound moves to the next, more demanding band |
| `capacity` | the largest demand the row's result can serve (max header span) | input ≤ capacity, choosing the row with the **smallest** capacity ≥ input among rows in the same bands | up: the smallest member that still spans the opening |

Rows are grouped by their `exact` and `upper-bound` inputs; within a group the `capacity`
column is what turns "which row" into "which member". This describes a header table's shape
generically; whether R602.7(1) reads exactly this way is for #14 to confirm from the text, and
if a column needs a kind not listed here (a `lower-bound`, a two-sided range), that is a schema
addition with its own tests, not a special case in the evaluator.

### 4.2 Selection, exactly

Comparisons are on `Length` and `int`, exact. Six feet is 73 728 units and "≤ 6′-0″" is
`span <= Length.FeetInches(6, 0)`, which is why integer millimetres were ruled out in #4. There is
no epsilon anywhere in the engine. A span of 6′-0″ against a 6′-0″ row is `Sized`; a span of
6′-0 1/1024″ is `OutOfScope(SpanExceedsTable)` citing the 6′-0″ row as the limit — and the trace
shows both numbers so the user sees how close it was. The building module's job (#18) is to
compute the span the table means from the geometry; the engine's job is to compare it honestly.

Each banded input's match is recorded as a `BandMatch(column, inputValue, bandChosen, rowIds)` in
the citation's `Trace` (§2).

### 4.3 Gaps and overlaps are rejected at load

For every group of rows that share their `exact` inputs, and for every `upper-bound` and
`capacity` column within it, the loader checks:

- bounds are strictly increasing in row order (a duplicate bound is an overlap; a decrease is a
  transcription error);
- the table declares each banded column's `domain` (`{ "min": 0, "max": <largest bound> }` for
  loads; the same for lengths) and the bands cover it without holes — with `upper-bound`
  semantics a hole can only appear at the bottom (a table whose first band starts above zero),
  which the domain's `min` must then state explicitly;
- every group has at least one row, and every row's outputs are present.

A failure here is a pack load error naming the table, column and rows (§9). This is a
transcription check, not a code-interpretation check: it catches "the 36-ft column got typed
twice", not "the code meant something else".

### 4.4 Rounding and interpolation come from the text, not from this document

Some model-code tables carry footnotes about how to treat inputs that fall between columns or
how to round. The design's stance:

- The default, absent a footnote, is the conservative direction in §4.1 and no interpolation.
- **DECIDED 2026-09-25 (Marc, #157): if a footnote permits interpolation, napkin applies it
  automatically**, exactly as the pack's footnote declares it (`encodedAs: "as-operations"`,
  operation `interpolate` with `input`, `between: [lower, upper]` and `quantity: "headerSpan"`).
  The engine never decides to interpolate on its own: no footnote, no interpolation. The
  semantics, chosen as the conservative readings of Connecticut's text ("For ground snow loads
  between 30 and 50 psf, linear interpolation is permitted", 2022 CSBC p. 145):
  1. Only for an input **strictly between** the two declared columns. At a column exactly the
     plain row is used; outside the pair nothing is interpolated (between 50 and 70 psf the
     70 psf column applies, per §4.1).
  2. The two columns must be adjacent bands present for every group of rows (a load check).
     Rows at the two columns are paired by member (plies and nominal); each member's permitted
     span is `lowerSpan + (upperSpan − lowerSpan) × (x − lower)/(upper − lower)`, computed as an
     exact fraction (Int128 numerator and denominator) of 1/1024″ units, never a double, and the
     opening is compared to it exactly. The smallest member whose interpolated span covers the
     opening is chosen, as for a table row. A member with a row at only one of the two columns is
     not offered.
  3. Count columns (jack and king studs) are never interpolated: the larger of the two rows is
     used, and the trace says so.
  4. The interpolated span is **displayed** rounded **down** to a whole 1/16″, never up; the
     comparison uses the exact value.
  5. The citation names the upper column's row (the row a plain lookup would give) and carries
     both rows, their spans, the weight as an exact fraction, the exact and shown span, and the
     footnote id, verbatim text and source page.
- A footnote that *substitutes* an input declares `substitute-input` (`input`, `below`, `use`,
  `when: {input, atMost}`): Connecticut's "Use 30 psf ground snow load for cases in which ground
  snow load is less than 30 psf and the roof live load is equal to or less than 20 psf". An input
  below `below` is taken as `use` when the condition input is at most `atMost`; when it is
  greater, the request is out of scope (`NarrowedByFootnote`) citing the footnote, because the
  text gives the table no meaning there; when it is not entered, `InputMissing`. Substitution
  runs before any band is chosen, and `use` may not be below `below`.
- If a footnote *changes* which band applies in some other way (a rounding rule for building
  width, say), the transcriber encodes its effect as rows (`as-rows`) where possible, or the
  schema gains an explicit, named operation for it, with its own tests at the boundaries. Never
  an unnamed adjustment inside the evaluator.

## 5. Site and hazard inputs

Ground snow load, ultimate wind speed, seismic design category and frost depth are properties of
the **site**, entered once per project, not attributes of each wall. (DESIGN.md §5.3 lists them
under walls; this design puts them on a project-level `SiteInputs` record that walls reference,
and reports the wording difference — a wall carries what it *supports* and its bracing, not the
snow map.) Building width is also project-level and is defined by the code text (#14 reads the
definition; #18 computes or asks for it accordingly).

- **Never defaulted.** `SiteInputs` has no parameterless constructor and no nullable hazard
  field. A project without site inputs cannot construct a `HeaderRequest`; the UI shows the
  precondition, and no lookup runs. "0 psf" is not a default either — it is a value the user must
  type, and the pack's domain check (§4.3) will typically make it `InputBelowTableBands` or a
  legitimate band, whichever the text says.
- **Provenance is recorded.** `InputProvenance` is free text plus a date ("Town of Bloomfield
  building department, phone, 2026-09-14"; "ASCE 7 Hazard Tool, 2026-09-14"). It is optional
  text, printed with the results, because an inspector will ask where the number came from.
- **The pack declares which inputs each table needs** (`inputs[]` in §1.4). The UI asks for
  what the selected pack's tables use, so a jurisdiction whose amendments add or remove an input
  (DESIGN.md §11 notes Massachusetts sets its own snow and frost criteria — unverified until #16)
  changes the form by data. An overlay may `amend` a table's `inputs` declaration.
- Jurisdiction-provided site values (a state table of snow loads by town, if one exists in an
  adopted text) would be a table kind of their own, offered to the user as a *suggestion with a
  citation* that they accept explicitly — never applied silently. Not in scope for M4/M5.

## 6. Amendment overlays

### 6.1 One mechanism, three layers

The overlay format of §1.3 is the only amendment mechanism. It is used identically for a state's
amendments over the model code and for a municipality's amendments over the state — the same
`add`/`amend`/`delete` operations, the same schema, the same validation, the same golden-test
and review requirements. A municipal pack differs from a state pack only in its `layers` list
(one more entry) and its `jurisdiction.municipality`.

```
layers/irc-2021            ← model code (ingredient; transcribed once; no golden tests of its own)
  + packs/us-pa-2026/amendments           ← state overlay      → pack "us-pa-2026"
      + packs/us-pa-2026-<town>/amendments  ← municipal overlay → pack "us-pa-2026-<town>"
```

A municipal pack's `layers` names the state pack's overlay by path rather than copying it, so a
correction to the state overlay flows through, and the state pack's `revision` is recorded in
the municipal pack's manifest and checked at load (a mismatch is a build/test failure, not a
runtime surprise — packs ship together).

### 6.2 Golden tests per layer

Golden tests are written against **packs**, i.e. composed results, because that is what the
state (or town) published. Each layer's tests are additive:

- The state pack's golden file covers every composed row (§8.2) — including the unamended ones,
  because the claim under test is "this is the value in force in Connecticut", not "this is what
  the IRC says".
- A municipal pack's golden file covers every row its overlay touches, plus a **no-change
  assertion**: for every table the overlay does not touch, the composed table equals the state
  pack's composed table (a structural equality test, generated, not hand-written). That is how
  an overlay that accidentally drops a row is caught.
- The model-code base layer has no golden tests of its own. It is not a selectable code and
  nothing cites it directly; its rows are tested through every pack that includes it.

### 6.3 Conflict detection

At load, and therefore in CI for every shipped pack:

| Condition | Result |
|---|---|
| two operations in one overlay target the same row or table id | invalid pack |
| `amend`/`delete` of an id that does not exist in the composed layer below | invalid pack |
| `add` of an id that already exists below | invalid pack |
| composed table fails band validation (§4.3) | invalid pack |
| overlay `amend`s a table's `inputs` so a golden case's inputs no longer match the declaration | golden test failure |
| municipal overlay loosens a numeric limit relative to the state layer (a larger max span, a heavier snow bound for the same member) | **warning**, reported by a lint test, not a load error |

The last row is deliberate. DESIGN.md §11 says Pennsylvania municipalities may adopt *stricter*
amendments (unverified against Act 45 §503 in this task). The engine can notice a loosening on a
monotone column, and should say so loudly, but whether an amendment is "stricter" is a legal
determination the reviewer makes; the lint points, the human decides, and the decision is
recorded in the review checklist (§8.3).

### 6.4 Scope for the first betas: the mechanism, zero municipalities

The [PA DLI register of municipal code-change
ordinances](https://www.pa.gov/agencies/dli/programs-services/labor-management-relations/bureau-of-occupational-and-industrial-safety/uniform-construction-code-home/ucc-municipal-code-change-ordinances)
was fetched in this task. What it is: an alphabetical register of well over two hundred
municipalities, each entry giving the municipality, county, a phone number, a narrative
description of the proposed changes and a narrative status (under review, approved, enacted,
with dates in prose). What it is not: a set of ordinance PDFs, ordinance numbers or structured
dates. Encoding a municipality therefore means obtaining and reading the enacted ordinance from
the municipality itself, then transcribing and reviewing it like any table.

Recommendation for #22 (Decision 4): the first betas ship the overlay mechanism, exercised by a
synthetic municipal fixture in tests (§11.1), and **zero real municipal packs**. In the UI, a
Pennsylvania project shows a persistent note: "Pennsylvania towns may adopt stricter amendments;
check the DLI register for <your municipality> and ask your building department" with the
register link. A real municipal pack is added when a user with a permit in a specific town needs
one, encoded from that town's enacted ordinance, and it goes through §8 like any other pack.
This keeps the honest-scope principle: the app never implies a town has no amendments.

## 7. Code locking, recompute and stale results

### 7.1 What the project stores

`manifest.json` (#6, geometry-model §6) gains:

```json
"adoptedCode": { "pack": "us-ct-2026", "revision": 1 },
"permit": { "applicationDate": "2026-10-02", "notes": "Bloomfield, applied in person" }
```

`adoptedCode` is `null` for a project that has not chosen a code — the state a new project is in
and the state a project falls into when its pack is unavailable (§9.3). `permit` is optional
free data the user may fill in; it drives guidance (§7.2), never selection.

### 7.2 Locking is a user choice, guided by dates, never automatic

The picker (#19) lists every valid, selectable pack as "<shortName> — <baseCode>, in force
<from>[ to <to>]" (DESIGN.md §11's wording), sorted by jurisdiction then date. If the project
has a `permit.applicationDate`, packs whose `inForce` window contains it are marked "matches
your permit date" and the others are not hidden. If the chosen pack's window does not contain
the date, a warning is shown on the picker and beside every result; the choice stands. Reasons
this is guidance and not automation: transition rules exist that are not pure date windows
(DESIGN.md §11 records one for PA, unverified), a user may know the town's practice, and a wrong
automatic choice would be exactly the silent error the design exists to prevent. The picker's
copy makes the rule explicit: "napkin applies the code you choose; the code that governs your
permit is set by your building department."

### 7.3 Results are derived; recompute is total; changes are shown

Results are never authoritative in the file. The building module (#18) holds an `IRulesEngine`
for the project's pack and every wall's and opening's result is a function of (geometry, site
inputs, pack). Three events cause a **total recompute** — every element, no incremental
shortcut — followed by a diff shown to the user:

1. The user changes the adopted code in the picker.
2. The project opens and the installed pack's `revision` differs from the manifest's.
3. Site inputs change (no pack change, but every result may move).

```csharp
public sealed record RecomputeReport(
    AdoptedCodeRef Before, AdoptedCodeRef After,
    ImmutableList<ResultChange> Changes,          // one per element whose result differs
    int Unchanged);

public sealed record ResultChange(EntityId Element, string ElementLabel,
    RuleResultSnapshot Before, RuleResultSnapshot After, ChangeKind Kind);

public enum ChangeKind { SizedToSized, SizedToOutOfScope, OutOfScopeToSized, PassToFail,
                         FailToPass, ToOutOfScope, FromOutOfScope, CitationOnly }
```

For event 1 the "before" results are computed in memory under the old engine at the moment of
the switch, so nothing needs to have been stored. For event 2 the old data is no longer
available, which is the reason the project file stores a **result snapshot**: `results.json`,
one entry per element with the result, its citation and the pack id/revision it was computed
under. The snapshot exists only to be diffed against; on open it is never displayed as current.
The app recomputes, compares, shows the report ("the CT 2026 data was revised (rev 1 → 2): 2 of
14 results changed"), and rewrites the snapshot. This is a #6 format matter and is flagged there;
`results.json` is a small addition to the container in DESIGN.md §6.4.

**Nothing stale survives.** There is no path by which a result computed under one pack is shown
under another: the manifest records the pack, the snapshot records the pack, and a mismatch
between either and the running engine forces the recompute before anything is drawn. `ChangeKind`
exists so the UI can rank the report — a `SizedToOutOfScope` is shown first and in red; a
`CitationOnly` (same member, same studs, different row label after a table was renumbered) is
listed last. Every change is listed; none is collapsed.

**M4 / M5.** M4 ships the picker with one pack and total recompute on events 3 (site inputs)
and, trivially, 1 (re-selecting the same pack). M5 ships the second pack, which is what makes
event 1 a real comparison, and `results.json` for event 2. The report type exists in M4 so M5
adds a pack, not a mechanism.

## 8. The golden-test format and the two-role process

### 8.1 What a golden test asserts

One test case per encoded row, asserting the value published in the state's adopted text —
not "the pack says what the pack says". A case names its row by `RowId`, gives the inputs that
land in that row, gives the expected output, and cites where in the source the transcriber read
the expected output. The test constructs the request from the inputs, runs the real evaluator
over the real pack, and asserts the result *and* that the result's citation names the expected
row. A case therefore tests transcription, band selection and citation together.

Per row, additionally, **boundary pairs**: for each `capacity` column, a case exactly at the
limit (→ `Sized`, this row) and a case one unit (1/1024″) over (→ either the next row in the
same group or `OutOfScope(SpanExceedsTable)` citing this row as the limit). For each
`upper-bound` column the same at the top of the band. These are where a result flips, and they
are generated from the row's own bounds (so they do not need a source citation of their own),
but the generator's output is committed as data, not recomputed at test time, so a reviewer
sees it.

### 8.2 File format

```json
// tests/Napkin.Core.RulesEngine.Tests/Golden/us-ct-2026/r602.7-1.golden.json
// Authored by the transcriber FROM THE SOURCE, never by exporting the pack. (Synthetic values.)
{
  "pack": "us-ct-2026",
  "table": "R602.7(1)",
  "source": "csbc-2026",
  "transcriber": { "who": "Opus (session id or PR number)", "on": "2026-10-01" },
  "cases": [
    {
      "row": "rc.snow-le-99.width-le-77ft.hdr-999",
      "inputs": { "supports": "roof-ceiling", "groundSnowLoad": 99,
                  "buildingWidth": "77ft 0in", "headerSpan": "99ft 9in" },
      "expect": { "sized": { "header": { "plies": 9, "nominal": "2x99" },
                             "jackStuds": 9, "kingStuds": 9 } },
      "location": "UNVERIFIED — page, column heading and row label as printed"
    },
    {
      "row": "rc.snow-le-99.width-le-77ft.hdr-999",
      "inputs": { "supports": "roof-ceiling", "groundSnowLoad": 99,
                  "buildingWidth": "77ft 0in", "headerSpan": "99ft 9-1/1024in" },
      "expect": { "outOfScope": { "reason": "SpanExceedsTable", "limitRow": "rc.snow-le-99.width-le-77ft.hdr-999" } },
      "generated": "boundary"
    }
  ]
}
```

The test project has one xunit `[Theory]` per table kind, fed by `[MemberData]` that enumerates
every golden file for every pack; the test display name is `pack/table/row/case` so a failure
names the row. A **coverage test** asserts, per pack and table, that every `RowId` in the
composed table appears in at least one hand-authored (non-`generated`) case, and that every
case's `row` exists — an orphan case is a failure, because it means a row was renamed without
the golden file being re-read. Every golden case also carries `[Trait("Feature", ...)]` for the
scorecard (#34) via the theory, not per case.

Independence: the golden file lives under `tests/`, is written by reading the source, and is
never generated from the pack. If the transcriber copies the pack's values into the golden file,
the file is worthless and the reviewer's checklist (§8.3) is the only defence — which is why the
checklist is row-by-row against the source and not "the tests pass".

### 8.3 The two-role process

PLAN.md assigns Opus to transcribe and Fable to review, for independence. The process:

1. **Transcriber** (the implementer of #14-#17): obtains the primary document, records it in
   `sources` with URL, printing/errata state, retrieval date and SHA-256; encodes the base layer
   rows (if new) and the overlay; writes the golden file from the source; classifies every
   footnote (§1.4); opens the pull request with `review.status = "in-review"`.
2. **Reviewer** (independent; Fable per PLAN.md): with the same document open, walks the
   checklist below and commits it to `docs/code-packs/reviews/<pack>/<table>.md` with their name,
   date, and the source hash they reviewed against. Only then does the PR set
   `review.status = "signed-off"` and `review.checklist` to that path.
3. A test asserts that every pack whose `review.status` is `signed-off` has a checklist file at
   the path it names, that the checklist's source hash equals the pack's, and that the checklist
   lists every `RowId` in the composed table. A pack cannot claim sign-off it does not have.
4. Any later change to the pack's data bumps `revision`, resets `review.status` to `in-review`,
   and requires a new (possibly delta-scoped, but committed) checklist. A test enforces the
   reset: a pack whose data hash differs from the one recorded in its checklist is not
   `signed-off`.

Checklist file, one per table per pack:

```markdown
# Review: us-ct-2026 / R602.7(1)
Reviewer: <name/model>  Date: 2026-10-08  Source: csbc-2026 sha256 <…>  Pack data hash: <…>
Read against: <document title, printing/errata, page range>

| RowId | Source location | Inputs match source | Outputs match source | Footnotes classified | Golden case present | OK |
|---|---|---|---|---|---|---|
| rc.snow-le-99.width-le-77ft.hdr-999 | p. N | yes | yes | a: not-encoded | yes | ✔ |
...
Overlay operations checked against the amendment list: <count>, all located.
Tables in scope the state does NOT amend, confirmed from the amendment list: R602.7(2).
Loosening lint warnings reviewed (municipal only): none.
Sign-off: <name>, <date>
```

"Looks reasonable" is not a row in this table. Every row is a yes or the PR does not merge.

## 9. Pack validation at load

### 9.1 What is checked

Loading is strict and total; a pack is valid entirely or not at all. In order:

1. `schemaVersion` of every file equals the loader's; otherwise `UnsupportedPackSchema(found,
   supported)` naming the file.
2. Every file deserialises with unknown fields **rejected** (strict, same policy as the project
   file), numbers strict, lengths parsed exactly (§1.5), enums closed.
3. `pack.json` references: every `layers` entry resolves; every `source` id used by a table,
   row or operation exists in `sources`; `review.checklist`, if set, exists.
4. Each table file's `kind` is known and the file validates against that kind's schema.
5. Composition (§1.3) succeeds with no conflicts (§6.3).
6. Every composed table passes band validation (§4.3) and has a `location` on every row and a
   classification on every footnote.
7. Fastener references resolve against the materials catalog (§10), once that kind exists.

A CI test loads every embedded pack and fixture and asserts validity, so an invalid shipped
pack is a red build, never a runtime discovery. The same loader runs at app start; it is cheap
(the data is small) and it is the only way a pack enters memory.

### 9.2 Errors are values

```csharp
public abstract record PackLoadResult
{
    private PackLoadResult() { }
    public sealed record Loaded(LoadedPack Pack) : PackLoadResult;
    public sealed record Invalid(string PackId, ImmutableList<PackProblem> Problems) : PackLoadResult;
}
public sealed record PackProblem(string File, string? Table, string? RowOrOperation, string Message);
```

All problems are collected, not just the first, so a transcriber fixes a pack in one pass.

### 9.3 What the app does with an invalid or unsupported pack

- **Catalog.** `PackCatalog.Discover()` returns every pack as `Loaded` or `Invalid`. The picker
  lists only `Loaded` packs. Invalid ones are listed in a diagnostics panel with their problems
  — visible, because a pack that silently vanishes from the picker would look like "napkin
  doesn't support CT".
- **A project whose pack is missing or invalid** (a pack was removed, or this build's loader
  refuses it) opens with its geometry intact and `adoptedCode` treated as unselected: no results
  are drawn, a banner says which pack the file named and why it is unavailable, and the picker
  is offered. The project is not modified until the user chooses; if they cancel, the manifest
  keeps naming the unavailable pack, so nothing is lost by opening the file to look.
- **Unreviewed packs** (`review.status != "signed-off"`): what the picker does with them is
  Decision 5. The recommendation is that they are listed with a persistent "UNREVIEWED — values
  not yet checked against the source" label and every result carries the same label, in the
  betas; a build flag can hide them entirely for a tagged release.

## 10. Sharing the fastener catalog with the fastener schedule

DESIGN.md §5.6 wants one fastener catalog feeding both the materials list (#7, #9) and the
fastener-schedule table (R602.3(1)). The dependency direction decides where it lives:
`Core.RulesEngine` cannot reference `Modules.Furniture` (the module consumes the engine, not the
reverse), and `Modules.Furniture` should not reference the rules engine to get at nail sizes.

- **The catalog is its own assembly, `Napkin.Core.Materials`**, referenced by
  `Modules.Furniture`, `Core.RulesEngine` and `Modules.Building`. It holds the #7 data (lumber,
  sheet goods, decking, fasteners) as embedded data files with sources, using `Length`. This is
  a change to the solution layout in DESIGN.md §6.2 (not this document's section; reported).
- Fasteners have stable ids (`nail.common.16d`, `screw.wood.8x2in` — naming per #7) with their
  actual dimensions and their source. A `fastener-schedule` table row references fasteners by
  id, plus spacing as `Length` and a connection description; the pack loader (§9.1 step 7)
  refuses an id the catalog does not have.
- The engine never stores a fastener's dimensions; it stores the id and asks the catalog. So a
  correction to a nail's length in #7 corrects both the shopping list and the schedule display,
  and a schedule row can never disagree with the catalog about what a 16d is.
- Whether the adopted text's fastener designations map one-to-one onto catalog ids, or a row
  needs a list of acceptable fasteners, is unverified until R602.3(1) is read; the schema for the
  `fastener-schedule` kind is finalised then. It is not in M4 or M5.

## 11. Test plan

### 11.1 Fixture packs (synthetic values)

The evaluator's *semantics* are tested against packs whose numbers are made up, so that a
semantics bug and a transcription error can never be confused, and so that the engine's tests
pass before any real table has been read. `tests/.../Fixtures/` holds:

- `fx-base` — a base layer with one `header-sizing` table: two `supports` groups, three snow
  bands, two width bands, three capacity rows per group, all values obviously fake (99s and
  77s), footnotes of each `encodedAs` kind.
- `fx-state` — a pack over `fx-base` with one overlay per operation kind: an `add`, an `amend`,
  a `delete`, and an `amend` of a table's `inputs`.
- `fx-town` — a municipal pack over `fx-state` with one row amended stricter and one looser
  (the lint must warn on the second).
- `fx-bad-*` — one invalid pack per validation rule in §9.1 and §6.3: unknown field, `6.0`,
  off-grid length, duplicate bound, decreasing bound, hole at the bottom without a declared
  `min`, unknown `kind`, unknown enum, missing `location`, unclassified footnote, conflicting
  operations, `amend` of a missing id, `add` of an existing id, dangling `source`, wrong
  `schemaVersion`, sign-off without a checklist, checklist hash mismatch.

### 11.2 Golden cases

Loader and composition, on fixtures:

1. `fx-base` loads; composed tables equal the files (no overlay = identity).
2. `fx-state` composes: the added row is present with `Layer = StateAmendment`; the amended row
   has the new values and cites the overlay's location; the deleted row is absent; the amended
   `inputs` change what the evaluator requires.
3. `fx-town` composes over `fx-state`; the no-change assertion (§6.2) holds for untouched
   tables; the loosening lint reports exactly the looser row.
4. Every `fx-bad-*` is `Invalid` with a problem naming the file and, where applicable, the row or
   operation; all problems are collected, not just the first.
5. Every real embedded pack is `Loaded` (this is the CI gate for #14-#17).

Evaluator, on `fx-base`/`fx-state`:

6. A request in the middle of every band → `Sized`, citing the expected row, with a `Trace`
   entry per banded input.
7. Span exactly at a capacity → `Sized` that row; one unit over → the next row in the group, or
   `OutOfScope(SpanExceedsTable)` citing the last row as `Limit` when there is none.
8. Snow load one unit over the top band → `OutOfScope(InputAboveTableBands)` citing the top
   band's row set; a table with declared `min > 0` and an input below it →
   `InputBelowTableBands`.
9. A `supports` value with no rows → `ConditionNotCovered`.
10. A footnote encoded `as-limit` excludes a case → `NarrowedByFootnote` citing the footnote id.
11. The same request against `fx-state`'s amended row returns the amended values with
    `Layer = StateAmendment`; against the deleted row's inputs, the result is whatever the
    remaining rows give — never the deleted values.
12. A `HeaderRequest` cannot be built without `SiteInputs` (compile-time: no such constructor;
    the test asserts by reflection that no optional or nullable hazard field exists).

Recompute (M4 types, M5 behaviour):

13. Two engines (`fx-base` and `fx-state`) over the same elements: the report lists exactly the
    elements whose results differ, with the right `ChangeKind`, and `Unchanged` counts the rest.
14. A snapshot recorded under revision 1, reopened with a pack at revision 2 whose data differs
    for one row: the report has one change and the snapshot is rewritten at revision 2.
15. Site inputs change: every element is re-evaluated (a spy engine counts calls = elements).

Real packs (#14 onward): the golden files of §8, the coverage test, the checklist tests of
§8.3 step 3 and 4.

### 11.3 Properties

Generated over `fx-base`-shaped random tables (valid by construction: bounds drawn increasing)
and random requests, with the same property-testing library #5 chooses:

- **P1 — Monotone in demand.** Increasing `headerSpan`, `groundSnowLoad` or `buildingWidth`
  never turns an `OutOfScope` into a `Sized`, and never selects a row with a smaller capacity or
  a lower band than before.
- **P2 — Every `Sized` is a real row.** The cited `RowId` exists in the composed table and the
  row's inputs cover the request's inputs under §4.1's rules; the result's outputs equal that
  row's outputs.
- **P3 — Every `OutOfScope` cites a real limit**, and no row in the table covers the request
  (the negative of P2: the evaluator never says "out of scope" when a row would have matched).
- **P4 — Determinism and purity.** Same pack, same request, same result by value; the pack is
  unchanged; the result does not depend on row order in the file (rows shuffled before load give
  the same composed table after sorting by id).
- **P5 — Composition is exact.** For random overlays of valid operations, composing then
  validating equals validating a hand-composed table; an overlay with zero operations is the
  identity.
- **P6 — Load round trip.** Serialising a loaded pack back to JSON and loading again gives an
  equal `LoadedPack` (so the format has no hidden state).
- **P7 — Recompute is total.** For random element sets and two engines, every element appears
  in `Changes` or is counted in `Unchanged`; nothing is dropped.

### 11.4 Not tested here

Canvas behaviour and the picker's layout (#19/#10), span computation from geometry (#18 tests
it against the definition read from the text), the project container beyond `results.json`
round trip (#6).

## 12. Implementation plan

### 12.1 #13 — evaluator, out-of-scope results and citations (Opus, Fable review)

Everything in `src/Napkin.Core.RulesEngine` and `tests/Napkin.Core.RulesEngine.Tests`; no
Avalonia references; `Class1.cs` and `UnitTest1.cs` are deleted. Steps leave the build green.

**Step 1 — References and numbers.** Add a project reference from `Napkin.Core.RulesEngine` to
`Napkin.Core.Geometry` (it has none today) for `Length`. If `Length.TryParse` does not accept the
pack syntax in §1.5 (`"3-1/2in"`), add it in `Core.Geometry` with tests. Add a strict
`System.Text.Json` options factory: unknown members rejected, `JsonNumberHandling.Strict`, a
`Length` converter that fails on `wasRounded`, closed-enum converters.

**Step 2 — Pack model.** `PackManifest`, `LayerManifest`, `SourceRef`, `Review`, `TableFile`
(abstract, by `kind`), `HeaderSizingTable` with `InputColumn`/`OutputColumn`/`Footnote`/`Row`,
`OverlayFile` with `Operation` (`Add`/`Amend`/`Delete`). Records, immutable. Commit the JSON
Schema files under `Packs/schema/` as documentation; validating against them at test time with a
permissively-licensed JSON Schema library is optional (JsonSchema.Net is believed MIT —
unverified; must pass the #2 gate) and never a runtime dependency.

**Step 3 — Loader and validation.** `PackLoader.Load(IPackSource) → PackLoadResult` with every
check in §9.1 and §6.3, collecting `PackProblem`s. `EmbeddedPackSource` enumerates
`Packs/layers/*` and `Packs/packs/*` resources; `DirectoryPackSource` reads a folder (tests and
future user packs). Tests: §11.2 cases 1, 4 with the `fx-bad-*` fixtures.

**Step 4 — Composition.** `Composer.Compose(layers) → ComposedPack`, tables keyed by
designation, rows keyed by id, each row tagged with its `CitationLayer` and the `SourceRef` of
the operation that produced it. Band validation (§4.3) runs on the composed table. Tests: cases
2, 3, property P5.

**Step 5 — Band selection.** `BandSelector` over declared `inputs`: group by `exact`, match
`upper-bound` and `capacity` per §4.1, produce `BandMatch` traces. Pure, generic, no IRC
knowledge. Tests: unit tests on tiny tables; properties P1–P3 are written here against a stub
that wraps the selector.

**Step 6 — Results and citations.** `Citation`, `AdoptedCodeRef`, `BandMatch`, `HeaderResult`,
`OutOfScopeReason`, `BracingResult` (type only; evaluator is M5), the closed-union reflection
test.

**Step 7 — Header evaluator.** `HeaderEvaluator.Size(ComposedPack, HeaderRequest) →
HeaderResult`, choosing the table by `WallKind`, applying `as-limit` footnotes, building the
citation with trace. `RulesEngine.For(LoadedPack)` returning an `IRulesEngine`. Tests: cases
6–12, P4.

**Step 8 — Catalog.** `PackCatalog.Discover(IPackSource) → ImmutableList<PackLoadResult>`, with
`Selectable` = `Loaded` and (per Decision 5) the review-status rule. Tests: fixtures appear
with the right status; invalid ones are reported, not dropped.

**Step 9 — Golden harness.** The `[Theory]` per table kind over `Golden/**/*.golden.json`, the
coverage test, the checklist-consistency tests (§8.3 steps 3–4). Runs green with zero real
packs; #14 makes it meaningful.

**Step 10 — Recompute types.** `RecomputeReport`, `ResultChange`, `ChangeKind`,
`RuleResultSnapshot` and `Recompute.Diff(before, after)`. Tests: case 13, P7. The snapshot
file and the open-time diff (cases 14, 15) are wired by #6/#18 in M5.

**Step 11 — Documentation.** XML docs on every public type; this document updated where the
implementation had to deviate, the deviation recorded here.

Definition of done: all fixture goldens and properties pass on both CI platforms; the loader
refuses every `fx-bad-*`; no package references beyond the BCL and the test project's
property-testing library; Fable review.

### 12.2 #14 — the minimal M4 slice of Connecticut 2026 (Opus, Fable review vs. source)

Scope for M4 is **R602.7(1) and R602.7(2) only**. R602.10 moves to M5 (with the bracing
evaluator), R602.3(1) waits for the fastener catalog (§10).

1. Obtain the 2026 CSBC document from DAS; record title, URL, printing/errata state, retrieval
   date and SHA-256 in `pack.json`'s `sources`. Confirm from its introduction which model code
   it adopts and how its amendment list is organised (the 2022 document uses Add/Amd/Del — §0;
   confirm the 2026 one does the same).
2. Read the two tables *as adopted*: the IRC 2024 table text (obtain it lawfully — Decision 7)
   and the CT amendment list for chapter 6. Encode `layers/irc-2024/tables/r602.7-1.json` and
   `r602.7-2.json` including every footnote, classified. Encode the CT overlay for both tables —
   an empty `operations` list with a `location` stating where the amendment list was checked,
   or the amendments, whichever the text says.
3. Write `Golden/us-ct-2026/r602.7-1.golden.json` and `r602.7-2.golden.json` from the source.
   Generate and commit the boundary cases.
4. Read the table's definition of header span and of building width; record both in the table
   file's `title`/`notes` and hand them to #18 as the computation it must perform.
5. Record which `SiteInputs` fields the two tables actually consume; the UI (#18) asks for those.
6. Open the PR with `review.status = "in-review"`. Fable reviews every row against the source
   and commits `docs/code-packs/reviews/us-ct-2026/r602.7-1.md` and `r602.7-2.md`; the PR flips
   to `signed-off`.

M5 adds, in the same shape: `layers/irc-2021` (the tables needed), `packs/us-ct-2022` from the
2022 document already read in §0, the `wall-bracing` kind and evaluator, `results.json`, and the
first real before/after comparison between two packs.

## 13. Decisions for Marc

Short, and only what is genuinely a preference or a legal call. Everything else above is
decided by the design and can be overridden the same way.

1. **Layered packs (model-code base + state overlay + municipal overlay) — recommended — or a
   flat, self-contained pack per state.** The state document is literally an amendment list over
   the model code (§0), so the layered form mirrors the source, makes "what CT changed" a
   reviewable diff, shares one transcription across the three 2021-based packs, and is the
   mechanism the municipal layer needs anyway. Flat packs would avoid composition code in M4 (a
   small function) at the cost of three transcriptions of the same rows and an invisible
   composition step. Recommendation: layered.
2. **Lengths in pack files as strict-parsed feet-inch strings (`"6ft 0in"`) — recommended — or
   integer 1/1024″ units as in the project file.** Strings are what a reviewer can check against
   a PDF by eye; the parse is exact or the pack is refused. Integers would keep one rule across
   both file kinds. Recommendation: strings, for the review surface.
3. **When a footnote permits interpolation — DECIDED 2026-09-25 (Marc, #157): apply it
   automatically**, as a named, tested footnote operation with the semantics in §4.4 (strictly
   between the two declared columns only; exact rational span; smallest covering member; counts
   take the larger row; display rounded down to 1/16″; both rows, the exact weight and the
   footnote cited). The earlier recommendation (never interpolate; use the next more demanding
   band) is superseded. Marc also decided that napkin may ship transcribed code tables, each read
   from a primary source in the task that writes it and cited beside the data.
4. **Municipal scope for the first betas: the mechanism plus zero real municipalities**, with a
   UI note pointing PA users at the DLI register (§6.4). The register has no ordinance texts, so
   each town is a document hunt plus a full transcribe-and-review cycle. Recommendation: none
   until a real user in a real town needs one.
5. **Unreviewed packs in the picker: listed with a persistent UNREVIEWED label on the pack and
   on every result (recommended), or hidden until signed off.** Labelled keeps the packs
   exercisable in betas and honest; hidden is stricter. A build flag for tagged releases can
   give both. Recommendation: labelled in betas, hidden in tagged pre-releases.
6. **User-installed packs from disk: not in the first betas** (recommended). The loader has a
   `DirectoryPackSource` for tests, so enabling it later is a settings toggle, but a pack
   dropped into a folder bypasses the review process in §8, and the label in Decision 5 would be
   the only guard. Recommendation: embedded only until there is a reason.
7. **Copyright of transcribed model-code tables.** The base layer transcribes rows that
   originate in ICC's copyrighted IRC, as incorporated by reference into state law. DESIGN.md
   §5.6 already says "not copied verbatim from any single publisher's compiled table". This
   document assumes that encoding the adopted values, cited to the adopting state's document,
   with the model-code text obtained lawfully, is acceptable — but that is a legal judgement
   for the project's owner, not a design decision. Recommendation: Marc confirms the stance
   before #14 transcribes, and records it in DESIGN.md §2.1.
8. **`results.json` in the project container** (§7.3): a stored snapshot used only to diff
   against after a pack revision change, never displayed as current. Alternative: no snapshot,
   and a revision change simply recomputes with no before/after. Recommendation: store it —
   "the data changed and here is what moved" is the point of recording the revision at all.
