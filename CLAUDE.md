# Working in this repository

For any Claude session or subagent working in napkin. Design decisions and the roadmap live in
[`DESIGN.md`](./DESIGN.md) and [`PLAN.md`](./PLAN.md) — read those first. This file is process:
how to build, test, merge and report in this repo, so it doesn't need retyping into every prompt.

## The one rule that overrides the others

**Core functionality first.** napkin's core is a person designing with real dimensions (M2),
getting a cut list (M3), and getting code-cited structural sizing (M4/M5) — that order. Before
starting or approving anything, ask: *does this make one of those real, or does it help a future
session verify/ship that?* If not — packaging, release pipelines, installers, exhaustive docs,
extra test infrastructure, roadmap rewrites — it waits. Land one runnable, mergeable slice of real
capability before writing docs or screenshots for it; report by pushing a green PR, not by
finishing a turn with an unpushed plan. Marc has pushed back on this twice (2026-09-21); see
memory `core-functionality-first` for the specifics.

## Beta policy (DESIGN.md §12)

napkin is pre-1.0 beta indefinitely. No v1.0 target, no deprecation, no migration code, no
compatibility shims — breaking changes are always allowed; an old project file gets a clear
"unsupported version" error, never a converter. Don't hedge or add speculative flexibility for a
future you can just build when it arrives.

**Versioning: the minor number is bumped by the pull request that lands the work**, in
`Directory.Build.props`'s single `<VersionPrefix>0.N.0</VersionPrefix>`. Every PR's last commit
before merge bumps it by exactly one. A tag (`v0.N.0-beta`) is cut only when Marc says a milestone
is worth naming — nothing is tagged or published without that.

## Build and test locally — do this, don't skip to CI

Marc wants local builds run as part of the work, not deferred to CI:

```sh
dotnet build napkin.sln --configuration Debug
dotnet test napkin.sln --configuration Debug --no-build \
  --settings ratchet/coverage.runsettings --collect:"XPlat Code Coverage" \
  --logger trx --results-directory artifacts/test-results
dotnet run --project tools/Napkin.Tools -- ratchet check
dotnet run --project tools/Napkin.Tools -- scorecard stubs   # only if you added/changed catalog features or tests with new [Trait("Feature",...)]
```

In a **worktree-isolated subagent**, prefix builds with `nice -n 19`; a chained/complex command
(`nice … && …`) or `nice … dotnet test …` may be refused by the sandbox guard as "too complex to
verify it stays in the worktree" — split into plain, separate commands instead of working around
it. If a build is denied outright, stop and report the exact denial text; never route around a
permission denial (including by asking a peer session to do it for you).

## The merge ritual (every PR, no exceptions)

1. Branch from `main` (or from wherever your task says).
2. Build, test with coverage, `ratchet check` locally before pushing.
3. Commit ends with `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`; PR body ends with
   `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
4. Push, open a **draft** PR against `main` (or the stacked base your task names), wait for CI on
   **both** `macos-latest` and `windows-latest`, fix red.
5. If `main` moved while you worked: `git merge origin/main` on your branch, rebuild/retest, bump
   the version in that same merge commit, re-push, re-confirm CI — don't skip this; a stale branch
   silently reverts someone else's version bump or ratchet floor.
6. Mark ready, merge with a real merge commit (not squash — commit history documents the process).
7. `ratchet update` only for the assembly/GUI count you actually changed, and only after coverage
   truly rose (see `docs/testing/ratchet.md`); never touch another area's floor.
8. Clean up: remove the worktree, delete the merged branch (local and `origin`).

**Parallel agents:** when several agents run at once, each owns a disjoint set of files (stated in
its task) to avoid merge conflicts — the recurring collision points are `napkin.sln`,
`Directory.Build.props`, `ratchet/baseline.json`, and `tests/Napkin.Features.Tests/PlannedFeatures.g.cs`
(generated — regenerate with `scorecard stubs`, never hand-edit). An integration branch that merges
several agents' branches together, verified once as a whole, is the way to catch cross-branch
issues (like a wrong catalog ID format, or a coverage-merge bug) before they reach `main`.

## Test layers (docs/testing/*.md)

- **Unit/golden tests** — normal correctness tests, one per assembly's test project.
- **Coverage ratchet** (`docs/testing/ratchet.md`) — per-assembly line/branch floors that only
  rise; a PR gate. `Napkin.App` is excluded (a GUI shell isn't usefully covered by unit tests).
- **GUI workflow suite** (`docs/testing/gui-automation.md`, `tests/Napkin.App.GuiTests`) — real
  keyboard/pointer input via Avalonia.Headless, driving multi-step scenarios
  (`[GuiWorkflow("ID")]`, ≥5 actions, both keyboard and pointer, an assertion after a state
  change). Its passing-workflow count is a second PR gate that only rises.
- **Feature catalog and scorecard** (`docs/testing/scorecard.md`, `features/catalog.json`) —
  `[Trait("Feature","ID")]` on a test claims a catalog feature. **Never gates anything** — it
  measures progress for a person to read, nothing more. IDs are `<AREA>-<NNN>` or
  `GUI-<AREA>-<NN>`; don't invent a different shape.

None of this is optional infrastructure to skip under "core first" — it's what lets several agents
and sessions build on each other's work without silently breaking it. What's optional is *adding
more of it* beyond what a real change needs.

## Data and citations

Never write a building-code value, a lumber/sheet-good dimension, a standard length, a license
claim, or any other fact-with-a-source from memory. Fetch and read the primary source in the same
task; cite it (source, section/table, retrieval date) next to the data; if it can't be fetched or
read, leave the row out and say so in the report. This applies to the materials library (#7) and
every rules-engine code pack (#13-#17) without exception.

## Decisions that are Marc's, not an agent's

Flag these rather than guessing: signing off on a Fable design doc before implementation starts
(`docs/design/*.md` headers say when this applies); the copyright stance on transcribing
state-adopted code tables (blocks #13-#17); cutting the first release tag; anything that trades
off product scope rather than implementing an already-decided design.
