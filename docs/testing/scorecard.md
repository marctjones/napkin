# The feature scorecard

How much of the design the test suite actually proves, feature by feature, rolled up by area and
by milestone. It exists to answer "what should we build next?" — and to make it visible when a
feature has a plan but no test.

**The scorecard never gates anything.** It does not fail a pull request, a tag or a release. Its
only non-zero exit code is for an input it cannot read. The gate in this repository is the
[coverage ratchet](./ratchet.md), which is a different thing entirely.

## The catalog

Every `features/*.json` file is merged into one catalog, in file-name order.

```json
{
  "schema": 1,
  "features": [
    {
      "id": "GEO-LEN-001",
      "area": "geometry",
      "title": "Lengths are exact",
      "acceptance": "A length round-trips through the file format without loss.",
      "milestone": "M1",
      "issue": 5,
      "kind": "unit"
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `id` | The identifier a test claims. Stable: renaming one orphans its tests. |
| `area` | Rollup grouping — `geometry`, `rules`, `furniture`, `interop`, … |
| `title` | One short line, shown in the report and in generated stubs. |
| `acceptance` | One sentence: what a passing test has to demonstrate. |
| `milestone` | `M1`–`M5`, or `backlog`. |
| `issue` | The GitHub issue that delivers it. |
| `kind` | `unit`, `golden` or `workflow`. |

Splitting the catalog across several files is deliberate: the GUI workflows live in their own
file, and a growing catalog stays reviewable. A duplicate id across files is a warning in the
report, not an error — the first definition wins.

## Claiming a feature

A test claims a feature with a trait:

```csharp
[Fact]
[Trait("Feature", "GEO-LEN-001")]
public void ALengthRoundTrips() { }
```

The attribute repeats, so one test can claim several features, and it works on the class as well
as on a method — a class-level trait applies to every test in it.

Claim from the test project for the code being exercised. `tests/Napkin.Features.Tests` is for
generated stubs only; a real test never belongs there.

## Statuses

Judged only from the tests that carry the id:

| Status | When |
|---|---|
| **Not started** | No test carries the id. |
| **Planned** | Every test carrying it is skipped — a generated stub, or a deliberate skip. |
| **Partial** | Some pass, some are skipped, none fail. |
| **Passing** | At least one passes, and none fails or is skipped. |
| **Failing** | At least one test carrying the id fails. |

A test that the run never mentions counts as skipped: a test that did not run has demonstrated
nothing. A theory's cases fold into one outcome for the method, with a failure outranking a pass
and a pass outranking a skip.

The report also lists **feature ids that tests claim but the catalog does not define**. That is
usually a typo or a renamed id, and it is worth fixing because those tests count towards nothing.

## Running it

```sh
dotnet test napkin.sln --configuration Debug --no-build \
  --settings ratchet/coverage.runsettings \
  --collect:"XPlat Code Coverage" --logger trx \
  --results-directory artifacts/test-results

dotnet run --project tools/Napkin.Tools -- scorecard report
```

Markdown goes to stdout. `--summary` also appends it to `$GITHUB_STEP_SUMMARY`, which is how it
reaches the CI job summary; `--json <path>` writes the same content as data. In CI the step is
`continue-on-error: true` and runs on `always()`, so a red test run still produces a scorecard —
which is when it is most useful.

## Generated stubs

```sh
dotnet run --project tools/Napkin.Tools -- scorecard stubs
```

This regenerates `tests/Napkin.Features.Tests/PlannedFeatures.g.cs` with one skipped fact per
catalogued feature that no hand-written test claims yet:

```csharp
[Fact(Skip = "planned: GEO-LEN-001 — Lengths are exact")]
[Trait("Feature", "GEO-LEN-001")]
public void GEO_LEN_001()
{
}
```

So the stub list *is* the checklist. Write the real test carrying that id, regenerate, and the
stub disappears — which is the whole point: a feature cannot silently be forgotten, because
something in the test output keeps saying it is planned.

The generator reads hand-written source only; files ending in `.g.cs` are skipped, so it never
mistakes its own output for the test that covers a feature. Output is sorted by id and carries no
timestamp, so regenerating without a catalog change rewrites nothing and leaves the working tree
clean. Run it after every catalog change and commit the result.

## How trait values reach the report

xunit traits do not survive into test results. This was measured, not assumed: a probe project
with `[Trait("Feature", "GEO-LEN-001")]` on a passing fact produced a TRX whose
`<TestDefinitions>` carry only `className` and `name` — the string `Feature` does not occur
anywhere in the file. The VSTest TRX logger discards arbitrary traits, and xunit 2.5.3 offers no
supported way to put them back.

So the tool reads the traits from the test **source** and joins them to the TRX on
`className` + method name, which TRX does carry (including the `Outer+Nested` spelling for nested
classes). A small purpose-built tokenizer handles comments, string literals and brace depth, so a
`[Trait(...)]` inside a comment or a string is not mistaken for a claim, and a statement inside a
method body is not mistaken for a method.

This is the same scan `scorecard stubs` needs in order to know which features a hand-written test
already claims, so the repository has one mechanism rather than two — and the trait attribute
stays exactly as issue #34 specifies. `TraitScannerTests.TrxCarriesNoTraitsAtAll` asserts the
premise against a real captured TRX, so if a future toolchain starts writing traits into results,
that test fails and the design can be revisited.

## Files

| Path | What it is |
|---|---|
| `features/*.json` | The catalog. Schema 1. |
| `tests/Napkin.Features.Tests/PlannedFeatures.g.cs` | Generated. Never edited by hand. |
| `tools/Napkin.Tools` | The tool, BCL only. |
