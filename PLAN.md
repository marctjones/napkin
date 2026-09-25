# napkin — implementation plan and model assignments

How the work is sequenced, and which Claude model leads each step. The issues on GitHub are the
work items; this file is the reasoning behind their `model/*` and `review/*` labels. Design
decisions themselves live in [`DESIGN.md`](./DESIGN.md); detailed designs under
[`docs/design/`](./docs/design/) — the first is the
[geometry model](./docs/design/geometry-model.md) for #4.

## Beta policy: no 1.0, no compatibility, no rush

napkin is a pre-1.0 beta indefinitely (DESIGN.md §12). Nothing below works toward a 1.0. The
milestones are an order of work; each earns a tagged pre-release when it is done, and the version
number is assigned at that moment rather than planned — which is why the milestones have names
and not numbers. Breaking changes are always allowed: no deprecation, no migration shims, no
compatibility concern between betas; a project file from an older beta gets a clear "unsupported
version" error. Scope is unchanged by this — all four adopted codes and permit-date code locking
are features, not legacy; they simply arrive one at a time (M4 ships Connecticut 2026, M5 adds
Connecticut 2022, and Massachusetts and Pennsylvania come after that). Nothing is scheduled
toward a date. Versioning is decided (DESIGN.md §12, #30): the minor number is bumped in each merged pull
request and a release is tagged when features improve significantly.

## Focus: core functionality first

Marc's rule (2026-09-21): build the product before anything around it. The core is a person
**designing something with real dimensions** (M2), getting a **cut list and a shopping list** from
it (M3), and getting **code-cited structural sizing** for a wall opening (M4, M5). Packaging,
releases, installers, export formats and optimizers do not advance that, and they are parked.
Before starting or approving any task, ask: *does this make design, cut lists or code-checked
sizing real?* If not, park it. Nothing is published, tagged or released until Marc asks.

**The core path:** #49 and #10 and #11 (draw and edit) with #6 (save and reopen) → #7, #8, #9
(the cut list) → #12, #13, #14, #18, #19 (headers with citations) → #15, #39 (bracing, and a
second code). The rules-engine work waits on two decisions of Marc's: sign-off on the #12 design,
and the stance on transcribing code tables (design §13, decision 7).

**Parked on 2026-09-21**, and when to reopen each:

| Status | Issues | Reopen when |
|---|---|---|
| Closed, delivered | #4 geometry design, #33 GUI suite, #36 viewer, #38 release workflow (dormant: it runs only on a version tag or when its own file changes; nothing has been tagged) | — |
| Closed, not planned | #27 installers and first-run docs; #56 and PR #57 (the macOS launch bug: an executable *file* named `*.App` is killed on macOS 26, fixed in #57); #3 line endings; #24 SketchUp import; #26 sheet-goods nesting | Packaging resumes (#27, #56, #57); mixed line endings cause diff noise (#3); importing SketchUp becomes a real need (#24); the cut list is in use (#26) |
| Deferred to Backlog | deck (#20, #40-#43), site plan (#21), DXF (#23), PDF sheets (#25), the MA and PA packs (#16, #17), PA municipal overlays (#22), the constraint solver (#28), the dependency-license gate (#2) | The core path is done and Marc picks one up |

## Milestones: what you can see and play with

Approved by Marc on 2026-09-21. Each milestone is a thing a person can hold, not a layer of the
architecture — that is the point of them. Every release is a tagged pre-release.

| Milestone | What you can see and play with | Issues |
|---|---|---|
| **M1 Look** | **Done.** Open a hand-crafted sample design from a file and look at it: pan with the wheel, a drag or the keyboard, zoom about the cursor, zoom to fit, read dimension labels in feet, inches and fractions, and see a refused file explained. Read-only. Underneath it: exact lengths, the geometry model, a strict scene reader and hand-computed fixtures. Downloadable builds are parked. | #4, #5, #6 (stage 1), #30, #32, #33, #34, #36, #37 (all closed) |
| **M2 Draw** | **Done (0.93.0-beta, 2026-09-25).** Draw a rectangle by dragging, select, move and resize parts, type a dimension as feet-inch-fraction text, snap one part flush to another and see the relationship that snap created, watch live dimensions while you drag, undo and redo a long chain, and save a design and reopen it to find the same design. Bad dimension text is explained, not guessed at; two dimensions that cannot both be true produce a named conflict, not a wrong number. | #6 (stage 2), #10, #11 (all closed) |
| **M3 Cut** | **Done (0.93.0-beta, 2026-09-25).** Build the coffee table end to end — top, four legs, aprons — assign materials, then open two different outputs: a cut list of what you actually cut, with a 2×4 resolved to its true size, and a shopping list of the stock to buy with board feet and sheet counts. Both on screen and as CSV, both checked against expectations computed by hand. | #7, #8, #9 (all closed) |
| **M4 Check** | Draw an exterior wall, enter what it supports and the site hazard values yourself, place a window on it, and see a header size and stud count **with the code edition, table and row it came from**. Resize the opening and the answer follows. Push it past what the table covers and napkin says so and stops, citing the limit. **One** adopted code pack — Connecticut 2026 — and the per-project picker that selects it; the other three packs come later. Designed in [`docs/design/rules-engine-model.md`](./docs/design/rules-engine-model.md). | #12, #13, #14, #18, #19 (the picker) |
| **M5 Brace and compare** | Enter the bracing that already exists along a wall line, widen an opening step by step, and watch napkin flag the wall when the line runs short — naming the section and the shortfall. Then switch the project to a second pack (Connecticut 2022) and watch every result recompute, with anything that no longer holds re-flagged instead of quietly carrying over. Two packs is what turns code locking from an assertion into something you can see happen. | #15, #19 (recompute), #39 |
| **Backlog** | Wanted, not scheduled: the deck in its four prescriptive pieces under an umbrella issue, the site plan, PDF and DXF export, the Massachusetts and Pennsylvania packs and municipal overlays, sheet-goods nesting, SketchUp import (blocked on a license decision), installers, and the solver spike. | #16, #17, #20–#26, #28, #40–#43 |

### Alongside the milestones, not inside them

Two lines of work run underneath all five and belong to no one milestone:

- **The constraint-solver spike (#28).** It starts once Core.Geometry (#5) has landed, runs in
  parallel, and **gates no milestone**. #4 fixed the seams it plugs into, so it can be deferred at
  any point without a rewrite. If it is hard, it waits.
- **Test infrastructure**: the coverage ratchet (#32), the GUI workflow suite (#33) and the
  feature scorecard (#34). The first two are pull-request gates whose floors only ever go up; the
  scorecard measures progress and gates nothing. See [`features/README.md`](./features/README.md)
  for the catalog those tools read, and DESIGN.md §6.5 for how the layers fit together. All three
  start in M1 and grow with every milestone after it.

## The rule: assign models by step type, not by issue

Every issue has up to three steps — **design**, **implement**, **review** — and the right model
depends on the step, not the topic.

| Model | Use it for | Why |
|---|---|---|
| **Fable** | Designs that every later phase builds on, and reviews of work where a mistake is *silent* | These are the decisions that are expensive to discover wrong later. A wrong length unit or a wrong header-table row doesn't crash — it quietly produces a plausible, incorrect answer. That is where the strongest reasoning pays for itself. |
| **Opus** | Implementing well-specified issues — most of the work | Once a design exists, implementation is against a clear spec. Opus writes the code and its tests. |
| **Sonnet** | Mechanical, tightly bounded work | CI configuration, repository hygiene, packaging scripts: the specification *is* the task. |
| **Marc** | Decisions that trade off goals, and sign-off on the two foundational designs | Some questions are preferences, not analysis — such as whether a proprietary SDK is worth an exception to the license policy. |

### Why the building-code tables are Opus-then-Fable, not Fable alone

Encoding a code table is transcription: the specification is the published table itself. The risk
is a transcription error, not a reasoning error — and a transcription error is best caught by an
independent reviewer checking every row against the primary source. So Opus encodes and writes
golden tests, and Fable reviews against the state's adopted text. Having one model do both would
remove the independence that makes the review worth doing.

### Why not Fable for everything

Cost and speed. The benefit of the strongest model is concentrated where errors are silent and
decisions are hard to reverse; for implementation against a clear design, it adds little.
Revisit these assignments with evidence: if Opus's own reviews catch what Fable's do on the first
few issues, shift reviews down a tier.

## Workflow per issue

1. **Design** (where needed): a design document committed to the repo under `docs/design/`, with
   a step-by-step implementation plan and test cases. For the two foundational designs (#4, #12),
   Marc signs off before implementation starts. #4's document is drafted
   ([`docs/design/geometry-model.md`](./docs/design/geometry-model.md)); Marc authorized
   implementation on 2026-09-21 without answering its §9 decisions one by one, so their
   recommendations stand as working choices until he says otherwise.
2. **Implement**: code and tests on a branch, one pull request per issue.
3. **Review**: the reviewing model reviews the pull request. For `code-data` issues, the review
   checks every encoded row against the primary source.

## Assignments

| # | Issue | Design | Implement | Review |
|---|---|---|---|---|
| **M1 Look** |||||
| 1 | CI: build and test on Windows and macOS | — | Sonnet | Opus |
| 4 | **Design: lengths, geometry model, update interface** — drafted, implementation authorized | **Fable** | — | Marc |
| 5 | Core.Geometry implementation | (#4) | Opus | **Fable** |
| 6 | Core.Project file format — stage 1, the reader | (#4) | Opus | **Fable** |
| 30 | Versioning: beta numbering and pre-release tags | Marc confirms | Sonnet | Opus |
| 32 | Coverage ratchet | — | Opus | Opus |
| 33 | GUI automation suite and its workflow ratchet | — | Opus | Opus |
| 34 | Feature catalog and progress scorecard | — | Opus | Opus |
| 36 | Read-only viewer: open, pan, zoom, dimension labels | — | Opus | Opus |
| 37 | Sample fixtures with hand-computed expectations | — | Opus | Opus, re-checking the arithmetic |
| **M2 Draw** |||||
| 6 | Core.Project file format — stage 2, writer and container | (#4) | Opus | **Fable** |
| 10 | Canvas: drawing, snapping, live dimensions | — | Opus | Opus |
| 11 | Canvas: undo/redo, layers, fraction display | — | Opus | Opus |
| **M3 Cut** |||||
| 7 | Materials and hardware reference library | — | Opus | Opus vs. the cited standards |
| 8 | Furniture: parts and cut list | — | Opus | Opus |
| 9 | Furniture: materials list and takeoff | — | Opus | Opus |
| **M4 Check** |||||
| 12 | **Design: rules-engine data model and overlays** | **Fable** | — | Marc |
| 13 | Rules engine: evaluator and citations | (#12) | Opus | **Fable** |
| 14 | Code pack: Connecticut 2026 | (#12) | Opus | **Fable** vs. source |
| 18 | Building: walls and openings with header sizing | — | Opus | Opus |
| 19 | Per-project adopted-code picker | — | Opus | Opus |
| **M5 Brace and compare** |||||
| 15 | Code pack: Connecticut 2022 | (#12) | Opus | **Fable** vs. source |
| 39 | Building: wall-bracing check after an opening changes | (#12) | Opus | **Fable** vs. source |
| **Backlog** |||||
| 16 | Code pack: Massachusetts 780 CMR 10th ed. | (#12) | Opus | **Fable** vs. source |
| 17 | Code pack: Pennsylvania UCC | (#12) | Opus | **Fable** vs. source |
| 20 | Deck module (umbrella) | (#12) | Opus | **Fable** |
| 40 | Deck: ledger attachment | (#12) | Opus | **Fable** vs. source |
| 41 | Deck: joist and beam spans | (#12) | Opus | **Fable** vs. source |
| 42 | Deck: footings | (#12) | Opus | **Fable** vs. source |
| 43 | Deck: guards and stairs | (#12) | Opus | **Fable** vs. source |
| 21 | Site plan with survey underlay | — | Opus | Opus |
| 22 | Pennsylvania municipal amendments | **Fable** | Opus | Marc |
| 23 | DXF export | — | Opus | Opus |
| 25 | PDF sheets with title blocks | — | Opus | Opus |
| 2 | CI: dependency license gate | — | Opus | Opus |
| 28 | Constraint solver: time-boxed spike behind the #4 interface | **Fable** | Opus | **Fable** |

Two issues appear twice because they are staged across milestones: **#6** (the scene reader in M1,
the writer and container in M2) and **#19** (the picker in M4, recompute-on-change in M5). The
`features/catalog.json` entries carry the per-milestone truth for both. Issues closed or dropped on
2026-09-21 are listed under "Focus: core functionality first" above.

## Sequencing

The two Fable designs are the critical path, and they don't depend on each other:

```
#1 CI (in place)
#4 geometry design (Fable) ──► #5 geometry ──► #6 reader ──► #37 fixtures ──► #36 viewer   [M1]
                                    │              └──► #6 writer/container                [M2]
                                    ├──► #10 canvas ──► #11 undo/layers                    [M2]
                                    │       └──► #7 materials ──► #8 cut list ──► #9 lists [M3]
                                    │
                                    └╌╌► #28 solver spike (off the critical path; time-boxed;
                                          gates no milestone; nothing waits for it)
#12 rules design (Fable) ──► #13 evaluator ──► #14 CT 2026 ──► #18 headers, #19 picker     [M4]
                                                     └──► #15 CT 2022, #39 bracing         [M5]
                                                            └──► #16, #17, decks, overlays [backlog]
```

- **#4 and #12 can be designed in parallel now.** #12 is M4 work, but designing it early means
  the file format (#6) and the geometry model (#5) are built knowing what the rules engine will
  need from them.
- **CI (#1) is in place**, and the coverage and GUI ratchets guard every pull request. The
  dependency-license gate (#2) is deferred: each new package's license is checked by hand.
- **Connecticut 2026 is encoded first** (M4). Connecticut 2022 follows in M5 — a second pack is
  what makes code locking and recompute something you can watch happen rather than something the
  design asserts. Massachusetts and Pennsylvania come after that (DESIGN.md §8, §11).
- **The header check and the bracing check are separate work.** #18 sizes a header over one
  opening; #39 checks a whole wall line's bracing after that opening changes. DESIGN.md §5.3 notes
  the bracing check is the one most DIY openings miss, and it is deliberately not bundled into the
  header issue.
- **The solver (#28) starts after #5 lands and gates no milestone.** #4 delivers the seams
  (relationships as data, one update interface, explicit result types, the fixed-point/double
  boundary); #5 builds them; the solver is a second implementation behind them. The spike has a
  time box and continue/defer criteria in #28. If it is hard, it waits.
- **Test infrastructure (#32, #33, #34) starts in M1 and grows with every milestone.** The
  coverage ratchet and the GUI workflow ratchet are pull-request gates whose floors only go up;
  the feature scorecard measures and gates nothing (DESIGN.md §6.5).
