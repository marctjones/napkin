# The coverage ratchet

Test coverage in this repository is allowed to go up and not down. A committed file records what
each assembly has already achieved, and CI fails a pull request that falls below it.

This is a deliberate gate on pull requests — Marc asked for it. It is **not** a gate on tags or
releases, and it has nothing to do with the [feature scorecard](./scorecard.md), which measures
progress and never fails anything.

## What is measured

`ratchet/coverage.runsettings` restricts coverlet to `Napkin.*` assemblies and excludes three
kinds of thing:

| Excluded | Why |
|---|---|
| `Napkin.App` | A GUI shell is not usefully covered by unit tests. Its ratchet is the count of passing GUI workflows (issue #33), not a line rate. |
| `*.Tests` | Covering the tests measures nothing. |
| `Napkin.Tools` | Repository tooling, never shipped. It has its own tests; it is not part of the product's coverage. |

Both line and branch coverage are recorded, per assembly, in percentage points.

An assembly can only be measured if some test project references it. Today that means
`Napkin.Core.Geometry` and `Napkin.Core.RulesEngine`; the rest appear as soon as they have tests.

## Running it

```sh
dotnet build napkin.sln --configuration Debug

dotnet test napkin.sln --configuration Debug --no-build \
  --settings ratchet/coverage.runsettings \
  --collect:"XPlat Code Coverage" \
  --logger trx \
  --results-directory artifacts/test-results

dotnet run --project tools/Napkin.Tools -- ratchet check
```

`ratchet check` prints a table of every instrumented assembly beside its floors, then passes or
lists exactly what failed. Exit codes: `0` passed, `1` a floor was missed, `2` the command line
was wrong, `3` an input file was missing or unreadable.

## The rules

**A drop of more than 0.1 percentage points fails.** The tolerance absorbs rounding, not
regressions.

**An assembly with coverable lines and no baseline entry fails**, and the message says to run
`ratchet update`. This is the case that matters most in practice: the day `Napkin.Core.Geometry`
stops being a placeholder, the ratchet demands that its coverage be recorded rather than quietly
ignoring a new, untested assembly.

**An assembly with no coverable lines is reported `n/a` and never fails.** Every `src` assembly is
an empty placeholder today, which is why the committed baseline is

```json
{ "schema": 1, "coverage": {}, "gui": { "workflowsPassed": 0, "workflowIds": [] }, "log": [] }
```

An entry of `0.0 / 0.0` is deliberately *not* written for those assemblies. It would make the
check pass trivially for ever; leaving the entry out is what makes the check speak up later.

**A baselined assembly that was not measured at all fails.** Deleting the tests that covered an
assembly is a coverage regression, not a way out of one.

**GUI workflows ratchet too.** If `artifacts/gui-metrics.json` exists — written by the GUI
automation suite (issue #33) — the check fails when fewer workflows passed than the baseline
requires, or when a workflow named in the baseline is no longer among those that passed. If the
file is absent and the baseline requires no workflows, the check says so and moves on; if the
baseline does require workflows, their absence is a failure.

## Updating the baseline

```sh
dotnet run --project tools/Napkin.Tools -- ratchet update
```

`update` raises floors to what the run achieved and never lowers one. Values are truncated to two
decimals rather than rounded, so a recorded floor is never above the coverage that produced it. A
drop within the tolerance leaves the higher floor standing. Commit `ratchet/baseline.json` with
the change that earned it.

**Do this after every merge that adds covered code.** The baseline is a fact about the merge
commit, and work happening in parallel — the geometry kernel (#5), the rules engine (#13) — will
each raise it. Re-running `update` is idempotent: with nothing to raise it writes nothing and says
the file is already up to date, so it is safe to run habitually.

## Lowering a floor

Sometimes coverage legitimately falls: a well-tested module is deleted, a chunk of code moves to
an assembly nothing tests yet, a whole approach is replaced. Then, and only then:

```sh
dotnet run --project tools/Napkin.Tools -- ratchet update \
  --allow-lower --reason "removed the half-written DXF writer and its tests"
```

Both options are required together. The reason is appended to the baseline's `log`, with the date,
so the file carries its own history of every time the bar was moved down.

Legitimate reasons look like *what changed in the code*. "CI was red" and "to unblock the merge"
are not reasons; they are the ratchet doing its job.

## Which platform gates, and why

CI collects coverage on both Windows and macOS and uploads it from both, but only **macOS** runs
`ratchet check`.

Coverage can differ between platforms — a platform-specific branch is covered on one and not the
other — so gating on both would need either two baselines or a lowest-common-denominator one.
Gating on one is simpler, and macOS is the one to pick because the baseline is generated there:
Marc's development machine is a Mac, so `ratchet update` locally and `ratchet check` in CI produce
the same numbers from the same code. A floor that passes on the machine that wrote it passes in
CI.

If napkin ever grows genuinely platform-specific code, this becomes a baseline per platform rather
than a single one; until then, one is honest and two would be noise.

The GitHub check is made *required* through the repository's branch protection settings. A
workflow file cannot declare that; all it can do is fail the job.

## Files

| Path | What it is |
|---|---|
| `ratchet/baseline.json` | The committed floors. Schema 1. |
| `ratchet/coverage.runsettings` | What coverlet instruments. |
| `tools/Napkin.Tools` | The tool, BCL only. |
| `tests/Napkin.Tools.Tests` | Its tests, against real captured coverlet and TRX output. |
| `artifacts/test-results` | A run's TRX and Cobertura output. Git-ignored. |
