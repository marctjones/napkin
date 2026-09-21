# napkin — implementation plan and model assignments

How the work is sequenced, and which Claude model leads each step. The issues on GitHub are the
work items; this file is the reasoning behind their `model/*` and `review/*` labels. Design
decisions themselves live in [`DESIGN.md`](./DESIGN.md); detailed designs under
[`docs/design/`](./docs/design/) — the first is the
[geometry model](./docs/design/geometry-model.md) for #4.

## Beta policy: no 1.0, no compatibility, no rush

napkin is a pre-1.0 beta indefinitely (DESIGN.md §12). Nothing below works toward a "v1"; the
phases are an order of work, not release milestones, and the first working beta is whichever
release first does Phase 1 usefully — its number is not decided in advance. Breaking changes are
always allowed: no deprecation, no migration shims, no compatibility concern between betas; a
project file from an older beta gets a clear "unsupported version" error. Scope is unchanged by
this — four adopted codes and permit-date code locking are features, not legacy. Versioning and
release tagging are proposed in DESIGN.md §12 and tracked in #30, awaiting Marc's confirmation.

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
| **Phase 1 — Furniture and cut lists** |||||
| 1 | CI: build and test on Windows and macOS | — | Sonnet | Opus |
| 2 | CI: dependency license gate | — | Opus | Opus |
| 3 | Normalize line endings | — | Sonnet | Opus |
| 4 | **Design: lengths, geometry model, update interface** — drafted, implementation authorized | **Fable** | — | Marc |
| 5 | Core.Geometry implementation | (#4) | Opus | **Fable** |
| 6 | Core.Project file format | (#4) | Opus | **Fable** |
| 7 | Materials and hardware reference library | — | Opus | Opus |
| 8 | Furniture: parts and cut list | — | Opus | Opus |
| 9 | Furniture: materials list and takeoff | — | Opus | Opus |
| 10 | Canvas: drawing, snapping, live dimensions | — | Opus | Opus |
| 11 | Canvas: undo/redo, layers, fraction display | — | Opus | Opus |
| **Phase 2 — Building core and rules engine** |||||
| 12 | **Design: rules-engine data model and overlays** | **Fable** | — | Marc |
| 13 | Rules engine: evaluator and citations | (#12) | Opus | **Fable** |
| 14 | Code pack: Connecticut 2026 | (#12) | Opus | **Fable** vs. source |
| 15 | Code pack: Connecticut 2022 | (#12) | Opus | **Fable** vs. source |
| 16 | Code pack: Massachusetts 780 CMR 10th ed. | (#12) | Opus | **Fable** vs. source |
| 17 | Code pack: Pennsylvania UCC | (#12) | Opus | **Fable** vs. source |
| 18 | Building: walls, openings, header and bracing | — | Opus | Opus |
| 19 | Per-project adopted-code picker | — | Opus | Opus |
| **Phase 3 — Decks and site plan** |||||
| 20 | Deck module | (#12) | Opus | **Fable** vs. source |
| 21 | Site plan with survey underlay | — | Opus | Opus |
| 22 | Pennsylvania municipal amendments | **Fable** | Opus | Marc |
| **Phase 4 — Interop** |||||
| 23 | DXF export | — | Opus | Opus |
| 24 | SketchUp import — license conflict | Opus researches | Opus | **Marc decides** |
| **Phase 5 — Polish and packaging** |||||
| 25 | PDF sheets with title blocks | — | Opus | Opus |
| 26 | Sheet-goods nesting | — | Opus | Opus |
| 27 | Unsigned installers and first-run docs | — | Sonnet | Opus |
| 30 | Versioning: beta numbering and pre-release tags | Marc confirms | Sonnet | Opus |
| **Parallel workstream — off the critical path** |||||
| 28 | Constraint solver: time-boxed spike behind the #4 interface | **Fable** | Opus | **Fable** |

## Sequencing

The two Fable designs are the critical path, and they don't depend on each other:

```
#1 CI ──► #2 license gate
#4 geometry design (Fable) ──► #5 geometry ──► #7 materials ──► #8 cut list ──► #9 materials list
                                    │                                  
                                    ├──► #6 file format                
                                    ├──► #10 canvas ──► #11 undo/layers
                                    │
                                    └╌╌► #28 solver spike (off the critical path; time-boxed;
                                          nothing waits for it)
#12 rules design (Fable) ──► #13 evaluator ──► #14 CT 2026 first ──► #15–#17 other states
```

- **#4 and #12 can be designed in parallel now.** #12 is Phase 2 work, but designing it early
  means the file format (#6) and the geometry model (#5) are built knowing what the rules engine
  will need from them.
- **CI and the license gate (#1, #2) come first**, because they protect everything after them and
  depend on no design decision.
- **Connecticut 2026 is encoded first**, then the three 2021-IRC-based packs (DESIGN.md §8).
- **The solver (#28) starts after #5 lands and must not delay the first working beta.** #4
  delivers the seams (relationships as data, one update interface, explicit result types, the
  fixed-point/double boundary); #5 builds them; the solver is a second implementation behind
  them. The spike has a time box and continue/defer criteria in #28. If it is hard, it waits.
