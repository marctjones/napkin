# The feature catalog

Every file matching `features/*.json` is a catalog of features napkin intends to have. The
scorecard tool (#34) merges them all, reads the test results, and reports how much of the design
is actually implemented and passing. **The scorecard measures progress; it never gates a build, a
tag or a release** — the gates are the coverage ratchet (#32) and the GUI workflow ratchet (#33).

A feature is a thing a person can rely on, written so that it can become a test. It is not a task,
not a file, and not a class. The catalog exists so that "how far along is napkin?" has an answer
that comes from the test suite rather than from a status report.

## Files

| File | Owner |
|---|---|
| `features/catalog.json` | the planner — everything below |
| `features/gui-shell.json` | the GUI-automation workstream (#33): the `GUI-SHELL-*` shell workflows |
| `features/assembly.json` | the 3D view (`docs/design/assembly-model.md` §9.3): the `GUI-ASSEM-*` workflows |

Add a new file rather than editing someone else's; the tool merges them and fails on a duplicate
id across files.

## Schema

```json
{
  "schema": 1,
  "features": [
    {
      "id": "GEO-001",
      "area": "geometry",
      "title": "Exact fixed-point length on a 1/1024-inch grid",
      "acceptance": "one concrete, testable sentence stating inputs and the observable result",
      "milestone": "M1",
      "issue": 5,
      "kind": "unit"
    }
  ]
}
```

| Field | Rule |
|---|---|
| `schema` | `1`. Bumped when the shape of a feature entry changes; per the beta policy there is no migration, so the tool accepts exactly the version it knows. |
| `id` | Unique across every `features/*.json`. See the ID rules below. |
| `area` | One of `geometry`, `project-file`, `materials`, `furniture`, `ui`, `rules-engine`, `code-packs`, `building`, `deck`, `site-plan`, `interop`, `packaging`, `release`, `solver`, `gui-shell`. Mirrors the `area/*` labels where one exists, so the scorecard can group the way the issue tracker does. |
| `title` | A short noun phrase. What the feature is, not how it is built. |
| `acceptance` | **One sentence, concrete enough to become a test**: it names the inputs and the observable result. See "Writing an acceptance sentence" below. |
| `milestone` | `M1` … `M8`, or `backlog` — the milestone by the end of which the feature is expected to pass. See `PLAN.md`. |
| `issue` | The issue number that delivers the feature, or `null` while no issue exists for it yet. One issue may deliver many features; a feature belongs to exactly one issue. |
| `kind` | `unit`, `golden` or `workflow`. See below. |

## ID rules

- **Unit and golden features: `<AREA>-<NNN>`** — two to five uppercase letters, a hyphen, and
  exactly three digits. Regular expression: `^[A-Z]{2,5}-\d{3}$`.
- **GUI workflows: `GUI-<AREA>-<NN>`** — `GUI`, the workflow family, and exactly two digits.
  Regular expression: `^GUI-[A-Z]{2,6}-\d{2}$`. The families in `catalog.json` are `VIEW`, `DRAW`,
  `CUT`, `CHECK`, `BRACE`, `JOIN`, `SET`, `RENO`, `SKETCH`, `PARTS`, `WALL`, `DIY` and `STRUT`.
  `SHELL` belongs to `features/gui-shell.json` and `ASSEM` — parts turned and set against one
  another in the 3D view — to `features/assembly.json`; neither is used in `catalog.json`.
- The area token in an id is a stable abbreviation and does not have to equal the `area` field —
  a workflow's `area` is `ui` while its id names the workflow family.
- **Ids are permanent.** Numbers are not renumbered when a feature is removed or two are merged,
  so gaps in a sequence are normal and expected. A removed id is never reused for something else,
  because test traits and old CI runs refer to it.

Area tokens in use: `GEO`, `PRJ`, `MAT`, `CUT`, `CVS`, `RUL`, `CODE`, `BLD`, `SITE`, `DECK`,
`IOP`, `PKG`, `REL`, `SOLV`.

## Kinds

- **`unit`** — the expected result follows from napkin's own design. Ordinary tests over
  `Core.*` and `Modules.*`.
- **`golden`** — the expected value comes from *outside* napkin and the test's job is to prove
  napkin reproduces it: a published code table, a lumber standard, or a fixture's hand-computed
  expectations file. A golden test's expected values are never derived from napkin's own output,
  and a code-pack golden asserts that *every row matches the state's published adopted text* —
  the value itself lives in the pack's data file and its citation, not in this catalog.
- **`workflow`** — a multi-step GUI scenario in the automation suite (#33), driven with real
  pointer and keyboard input. Per that suite's rule a scenario uses at least five input actions
  and both keyboard and pointer, and asserts an observable outcome after a state-changing
  sequence; a one-click test is not a workflow.

## Writing an acceptance sentence

One sentence, present tense, naming the inputs and the result someone could check.

- Good: "Looking up 2x4 returns 1 1/2in by 3 1/2in as exact Length values."
- Bad: "Lumber sizes work correctly." (No input, no observable result.)
- Bad: "Implement `MaterialLibrary.Lookup`." (A task, not a feature.)

**Never put a building-code number, a span, a header size or a table threshold in this file.** A
code-pack feature's acceptance says that every row matches the state's published adopted text;
the numbers live in the pack's data files, each with the citation it was read from, and the
golden tests compare against those. The same goes for lumber and fastener dimensions taken from a
standard: name the standard to consult, not a remembered value.

## How a test claims a feature

A test claims a feature with an xUnit trait:

```csharp
[Fact]
[Trait("Feature", "GEO-002")]
public void SixFeetIsExactlySixFeet() { /* ... */ }
```

- A feature is **Passing** when every test claiming it passes, **Failing** when any claiming test
  fails, **Planned** when its only claiming tests are skip stubs, and **Not started** when no test
  claims it.
- A feature that is not built yet gets a generated skip stub, so the stub list is the checklist:

  ```csharp
  [Fact(Skip = "planned: CUT-004")]
  [Trait("Feature", "CUT-004")]
  public void CoffeeTableCutListMatchesExpectations() { }
  ```

- One test may claim more than one feature, and one feature may be claimed by many tests — a
  workflow scenario in the GUI suite is usually one test claiming one `GUI-*` feature.
- A test whose `Feature` trait matches no catalog entry is reported by the scorecard as an
  unmatched id. That is a catalog bug or a typo, not a build failure.

## Changing the catalog

- Adding a feature is an ordinary change; say in the pull request which issue delivers it.
- Reworking a feature's acceptance sentence is fine and expected — the beta policy allows
  breaking changes, and a sentence that turned out not to be testable should be rewritten rather
  than worked around.
- Removing a feature is fine too. Remove the skip stub in the same change, and do not reuse the
  id.
