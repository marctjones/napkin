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
capability before writing docs or screenshots for it; report by pushing a real, verified increment,
not by finishing a turn with an unpushed plan. Marc has pushed back on this twice (2026-09-21); see
memory `core-functionality-first` for the specifics.

## Beta policy (DESIGN.md §12)

napkin is pre-1.0 beta indefinitely. No v1.0 target, no deprecation, no migration code, no
compatibility shims — breaking changes are always allowed; an old project file gets a clear
"unsupported version" error, never a converter. Don't hedge or add speculative flexibility for a
future you can just build when it arrives.

**Versioning: the minor number is bumped by whoever lands each change**, in
`Directory.Build.props`'s single `<VersionPrefix>0.N.0</VersionPrefix>`. The last commit landed on
`main` for a given piece of work bumps it by exactly one. A tag (`v0.N.0-beta`) is cut only when Marc says a milestone
is worth naming — nothing is tagged or published without that.

## Build and test locally — do this, don't skip to CI

Marc wants local builds run as part of the work, not deferred to CI. `tools/scripts/gate.sh` runs
the build/test/ratchet sequence below in one call and exits non-zero if the build has errors, any
test fails, or ratchet check fails (it also prints a one-line pass/fail summary); prefer it over
retyping the commands by hand:

```sh
tools/scripts/gate.sh
```

which runs, in order:

```sh
dotnet build napkin.sln --configuration Debug
dotnet test napkin.sln --configuration Debug --no-build \
  --settings ratchet/coverage.runsettings --collect:"XPlat Code Coverage" \
  --logger trx --results-directory artifacts/test-results
dotnet run --project tools/Napkin.Tools -- ratchet check
```

Run `dotnet run --project tools/Napkin.Tools -- scorecard stubs` separately, only if you added or
changed catalog features or tests with a new `[Trait("Feature",...)]`. `tools/scripts/gui-ratchet.sh`
raises only the GUI workflow floor while restoring the committed coverage floors (for a GUI-only
landing). `tools/scripts/mutate.sh` mutation-tests a guard: it applies a one-shot text
substitution to a file, runs a command that should fail with the mutation applied, then always
restores the file with `git checkout`.

In a **worktree-isolated subagent**, prefix builds with `nice -n 19`; a chained/complex command
(`nice … && …`) or `nice … dotnet test …` may be refused by the sandbox guard as "too complex to
verify it stays in the worktree" — split into plain, separate commands instead of working around
it. If a build is denied outright, stop and report the exact denial text; never route around a
permission denial (including by asking a peer session to do it for you).

## Landing changes: direct to `main`, no pull requests (decided 2026-09-22)

Marc's instruction: no GitHub pull requests, no PR review cycle, no draft/ready toggling. Develop
directly on this repo's `main`. This replaces the earlier PR-based ritual; if you see a stale
reference to "open a PR" anywhere else in this repo's history or docs, this section is current.

A background agent still needs its own git branch and worktree — that's a technical necessity of
running isolated, not a process choice, and it doesn't reintroduce PRs. The steps:

1. Branch from `main` (or from wherever your task says), in your own worktree if you're a
   background agent.
2. Build, test with coverage, `ratchet check` **locally**, via `tools/scripts/gate.sh` — this is
   now the primary gate, since there is no PR checkmark to wait on. Don't skip it or weaken it
   because it's no longer a published check.
3. Commits end with `Co-Authored-By: <the model that did the work> <noreply@anthropic.com>`.
4. **Commit regularly, in sensible groups, as you go — don't let a pile of uncommitted work
   accumulate.** A commit doesn't have to be a finished, fully-verified slice; a coherent step
   (the format-layer change, the model type, the fixture update) each landing as its own commit is
   exactly right, and cheap insurance against losing work if a session ends unexpectedly. The
   orchestrating session may check your worktree's `git status` for durability, but should not
   `git add`/`git commit` inside a worktree you're actively writing to — that's your job; a
   collision, even a harmless one, is unnecessary risk for no benefit once you're already doing it.
5. **Push early and often** — after your first real, building, tested increment, and after each
   further one. Report to whoever's integrating (usually the orchestrating session) by pushing,
   not by finishing a turn with unpushed work. A session crash mid-task loses anything not pushed.
6. Whoever integrates: `git fetch`, merge `main` into the branch if it moved, rebuild/retest,
   bump the version in `Directory.Build.props` in that same step, then merge the branch into
   `main` with a real merge commit (not squash — commit history documents the process) and
   `git push origin main` directly. `tools/scripts/land.sh <branch> "<what>"` does exactly this:
   it refuses if the working tree is dirty or if `origin/main` moved since the branch was cut,
   bumps exactly one minor, merges `--no-ff`, pushes, and deletes the branch. It reads the
   co-author trailer from `$NAPKIN_COAUTHOR`
   (e.g. `export NAPKIN_COAUTHOR='Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>'`).
   No `gh pr create`, no waiting for a PR's CI check — local verification is what gates the merge.
   Push to `main` still triggers CI (`ci.yml`'s
   `push: branches: [main]`), which is a secondary, after-the-fact safety net (it's caught real
   platform-specific bugs before, e.g. a Windows-only CRLF issue) — check it after pushing and fix
   forward with a small follow-up commit if it's red, rather than pretending it didn't happen.
7. `ratchet update` only for the assembly/GUI count that actually changed, and only after coverage
   truly rose (see `docs/testing/ratchet.md`); never touch another area's floor.
8. Clean up: remove the worktree, delete the merged branch (local and `origin`).

**Parallel agents:** when several agents run at once, each owns a disjoint set of files (stated in
its task) to avoid merge conflicts — the recurring collision points are `napkin.sln`,
`Directory.Build.props`, `ratchet/baseline.json`, and `tests/Napkin.Features.Tests/PlannedFeatures.g.cs`
(generated — regenerate with `scorecard stubs`, never hand-edit). Merge each branch into `main`
directly, one at a time, resolving the recurring collision points by hand as they come up; there is
no separate integration branch to stage them on first — verify locally after each merge instead.

## Test layers (docs/testing/*.md)

- **Unit/golden tests** — normal correctness tests, one per assembly's test project.
- **Coverage ratchet** (`docs/testing/ratchet.md`) — per-assembly line/branch floors that only
  rise; a gate on landing on `main`, enforced locally by whoever merges (§"Landing changes" above),
  not by a published PR check. `Napkin.App` is excluded (a GUI shell isn't usefully covered by
  unit tests).
- **GUI workflow suite** (`docs/testing/gui-automation.md`, `tests/Napkin.App.GuiTests`) — real
  keyboard/pointer input via Avalonia.Headless, driving multi-step scenarios
  (`[GuiWorkflow("ID")]`, ≥5 actions, both keyboard and pointer, an assertion after a state
  change). Its passing-workflow count is a second local gate that only rises.
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
(`docs/design/*.md` headers say when this applies); (decided 2026-09-25: napkin MAY ship transcribed code tables, each read from a primary or
official source in the same task and cited beside the data; see issue #157); cutting the first release tag; anything that trades
off product scope rather than implementing an already-decided design.
