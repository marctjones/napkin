# The in-process MLX runtime: a Swift bridge over mlx-swift-lm, called from napkin on a Mac

Status: **DRAFT awaiting Marc's sign-off.** Milestone **M14 Assistant (local LLM)**; issue #237
(slice I of [`llm-assistant.md`](./llm-assistant.md)); umbrella #228. Slices A–E of this note are
filed as **#239–#243** (§9) and none starts until the note is signed off.

Design note written by Fable per [`PLAN.md`](../../PLAN.md) for Marc's decision of 2026-09-27 on
#237: napkin runs the model **in its own process**, **MLX on the Mac first**; DESIGN.md §11's
".NET-native, no native interop" rule is lifted **for the LLM runtime only** (the solver keeps it);
the route is a **small Swift bridge over Apple's mlx-swift-lm exposing a C ABI** that a new napkin
assembly calls through P/Invoke; Mac-only, Windows keeps the loopback runtime (#231) until
LLamaSharp later; a model is **picked on disk or downloaded with consent**; the loopback runtime
stays as an option. The orchestrator records the §11 change in DESIGN.md when this lands; this note
does not touch DESIGN.md or PLAN.md.

Settled and not re-argued: the seam `IAssistantModel` / `ModelRequest` / `ModelReply` and the answer
guard (llm-assistant §2.1, §4.2, as built §14); proposals are JSON against napkin's own schemas,
parsed strictly (§4.3–§4.5) — slices E (#233) and F (#234) need **schema-constrained JSON**; the
runtime never touches the drawing, the network or a code pack (§1); settings v3 and the *Where the
model runs…* dialog as built in slice C (§16); dependencies permissive or weak-copyleft only
(DESIGN.md §2.1); no code signing and no notarization, arm64 keeps the toolchain's ad-hoc
signature (§6.6); the beta policy (no shims, no converters).

**The data rule (CLAUDE.md) governs this note.** Every fact below about MLX, mlx-swift,
mlx-swift-lm, xgrammar, swift-transformers and its dependencies, Xcode, the GitHub runner image,
the .NET runtime, a Hugging Face repository, a model's licence, size or files was read from the
page or file named beside it on **2026-09-27**. Anything that could not be read is marked
**unverified** and nothing is built on it; §11 lists every such item in one place. Nothing here
states a code value, a lumber size, a span or a load.

§0 what exists and what this note does with it; **How to use it**; §1 the bridge (packages,
versions, licences, toolchain, the C ABI); §2 structured output; §3 build and packaging; §4 the
.NET side; §5 models; §6 the consented download; §7 tests; §8 risks; §9 slices; §10 try it;
§11 unverified; §12 decisions for Marc.

---

## 0. What exists today, and what this note does with it

| Today | Where | This note |
|---|---|---|
| `IAssistantModel` — `Whereabouts`, `AskAsync(ModelRequest, CancellationToken) → ModelReply` (`Text` / `Json` / `Refused`); `ModelRequest(System, Context, Question, Schema: string?)` | `Napkin.Modules.Assistant` (§14 item 1: the schema is JSON text) | `MlxModel` is one more implementation; nothing above the seam changes (§4.1) |
| `Napkin.Assistant.LocalServer` — Ollama / llama-server over loopback; `Guidance` (every fact beside its source); `LocalServerModel.UserMessage` (system alone, then `Context:` + pack + `Question:`); `DefaultTemperature` 0.2; `DefaultTimeout` 30 s | `src/Napkin.Assistant.LocalServer` (§16) | Stays, unchanged, as *A program on this machine*; the MLX runtime sends the same two messages (§1.4) |
| `AssistantSettings(Provider, Endpoint, Model, Temperature)`, `AssistantProvider { None, LocalServer }`, `UserSettings.CurrentVersion = 3` | `src/Napkin.App/Settings/UserSettings.cs` | Version 4: `Provider` gains `Mlx`, the record gains `ModelFolder` (§4.3) |
| `AssistantModels.FromSettings(UserSettings, HttpMessageHandler?)` — the only place a runtime assembly is referenced | `src/Napkin.App/AssistantModels.cs` | Gains the `Mlx` case; takes the native seam and the download handler the same way it takes `http` (§4.4) |
| *Assistant → Where the model runs…*: radios *None* / *A program on this machine*, address, Check, model list, temperature, Test, Use these settings; `Guidance` lines; `MainWindow.AssistantHttp` lets the GUI suite put a stub under it | `src/Napkin.App/AssistantWindow.axaml(.cs)` | A third radio, *In napkin, on this Mac (MLX)*, a folder picker, a *Download…* button that opens the consent sheet (§4.5, §6) |
| `SettingsStore.ConfigDirectory()` → `~/Library/Application Support/napkin` on macOS | `src/Napkin.App/Settings/SettingsStore.cs` | Downloaded models live under it, in `models/` (§6.4) |
| Release: `dotnet publish … --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` per RID (`win-x64`, `osx-arm64`, `osx-x64`); the macOS step asserts the executable's ad-hoc signature and refuses any Developer ID or notarization ticket; `ReleaseWorkflowTests.cs` fails if the workflow gains a signing step | `.github/workflows/release.yml` | The `osx-arm64` artifact gains `native/libNapkinMlx.dylib` + `native/mlx.metallib` beside the executable, kept out of the single file; the *check* extends to the dylib; no signing step is added (§3.5) |
| CI: `windows-latest` + `macos-latest`, restore → build Debug and Release → licences → test → ratchet (macOS gates) | `.github/workflows/ci.yml` | Unchanged; a **separate** macOS job builds the bridge and uploads it (§3.6) |
| `gate.sh`: build, `licenses check` (NuGet only), test with coverage, `ratchet check` | `tools/scripts/gate.sh` | Unchanged — no Swift in the gate (§3.4) |
| `Napkin.Interop.Dxf`: one outside dependency, its licence noted in the csproj, its own floor | `src/Napkin.Interop.Dxf` | `Napkin.Assistant.Mlx` follows the pattern; its outside dependency is a native library rather than a package, so the licences go in `docs/third-party-notices.md` (§3.7) |
| The eval command `assistant eval --endpoint … --model …` (slice G, #235, not built yet) | `tools/Napkin.Tools` | Gains `--mlx <folder>`; plus `assistant mlx-smoke` — the only places real weights are ever loaded (§7.4) |

Nothing in the geometry kernel, the rules engine, the materials library, the file format, the
context pack, the guard or the parsers changes. The bridge is a way of *running* the same
requests; everything it returns goes through the same guard and the same strict parsers.

---

## How to use it (the finished feature, in one place)

- **On an Apple silicon Mac**, *Assistant → Where the model runs…* has a third choice: **In
  napkin, on this Mac (MLX)**. Under it: a folder box with **Choose…** for a model you already
  have (a folder holding `config.json`, `tokenizer.json` and `model*.safetensors`, as
  `mlx-community` publishes them), and one button, **Download Qwen3-4B-4bit (2.28 GB,
  Apache-2.0)…**.
- **The download asks first, every time.** A sheet lists the repository, the exact commit, every
  file with its size and SHA-256, the licence with a link to the model card, and where it will be
  put (`~/Library/Application Support/napkin/models/…`). Nothing starts until you tick *"Download
  these files from huggingface.co"* and press **Download**; a progress bar and **Cancel** follow;
  each file is checked against its SHA-256 as it lands and the whole folder appears only when every
  file passed. napkin never fetches anything on launch, never silently, never to a path you did not
  see, and never fetches a `.py` file (§6).
- **Test** loads the model (the first load reads 2.3 GB; the line says *"loading…"* with the
  seconds) and asks it to reply "ok". **Use these settings** saves the folder, not a URL.
- **Ask, Explain, Sketch from words, Edit in words** work exactly as before. The note's last line
  reads *"In napkin (MLX): Qwen3-4B-4bit from ~/Library/Application Support/napkin/models/… —
  nothing leaves this machine."* Escape cancels a question at the next token.
- **A proposal is JSON that matches the schema by construction**: the bridge constrains every
  token to napkin's schema (§2), and napkin's parser still checks every field (llm-assistant §4.3).
- **On Windows, an Intel Mac, or a Mac build without the bridge**, the third choice is shown
  greyed with one sentence — *"MLX runs on Apple silicon; use a program on this machine
  (Ollama or llama-server) instead."* — and everything else is as today.
- **What does not change:** the guard, the disclaimer, the tick before any edit, no transcript, no
  telemetry, no key, no account.

---

## 1. The bridge

### 1.1 The packages, their versions and licences

Read on 2026-09-27 from each repository's `Package.swift`, `LICENSE` and the GitHub API's licence
field (`api.github.com/repos/<owner>/<name>`), tags (`/tags`) and latest release (`/releases/latest`).

| Package | What napkin uses | Version to pin | Licence | Platform floor |
|---|---|---|---|---|
| **mlx-swift-lm** (`github.com/ml-explore/mlx-swift-lm`) | `MLXLLM` (model factory, must be linked or loading throws — the Package.swift test-target comment: *"Without it, `loadModelContainer` throws `.noModelFactoryAvailable`"*), `MLXLMCommon` (load, `UserInput`, `generate`), `MLXGuidedGeneration` (§2) | **`main` at commit `ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb`** (2026-09-22, *"Extract the model cache and stop whole-cache eviction abandoning an in-flight load (#603)"*) — the latest release **3.31.4** (2026-06-30) has **no `MLXGuidedGeneration`**: its `Package.swift` lists only `MLXLLM`, `MLXVLM`, `MLXLMCommon`, `MLXEmbedders`, `MLXHuggingFace`, and the release notes say *"#334 — a big PR that adds two new libraries"* is upcoming. Decision 1 (§12) | MIT (`LICENSE`: *"Copyright (c) 2024 ml-explore"*; API licence field `MIT`) | `Package.swift`: `.macOS(.v14)`; `swift-tools-version: 6.2` on main (6.1 at 3.31.4) |
| **mlx-swift** (`github.com/ml-explore/mlx-swift`) | pulled by mlx-swift-lm: `.upToNextMinor(from: "0.31.6")` | **0.31.6** (2026-07-02, *"Fixes build issue on iOS"*), the latest tag | MIT (`LICENSE`: *"Copyright (c) 2023 ml-explore"*) | `.macOS("14.0")`; **`swift-tools-version: 6.3;(experimentalCGen)`** at tag 0.31.6 and on main — what the `(experimentalCGen)` suffix demands beyond Swift 6.3 is **unverified** |
| **MLX** (C++; `github.com/ml-explore/mlx`) | vendored as a submodule inside mlx-swift's `Cmlx` target (`Source/Cmlx/mlx`, `mlx-c`) | whatever 0.31.6 pins (its `Package.swift` on main defines `MLX_VERSION "0.32.0"`; the 0.31.6 tag's value is **unverified**) | MIT (API licence field) | *"MLX is only available on devices running macOS >= 14.0 and higher"*, Apple silicon ([ml-explore.github.io/mlx/build/html/install.html](https://ml-explore.github.io/mlx/build/html/install.html)) |
| **xgrammar** (`github.com/mlc-ai/xgrammar`) | vendored inside mlx-swift-lm as `Libraries/MLXCXGrammar/xgrammar` (`VERSION`: *"Pinned to the upstream release tag v0.1.30"*), compiled only when `MLXGuidedGeneration` is linked (its README) | v0.1.30 as vendored | Apache-2.0 (vendored `LICENSE`, `NOTICE`: *"Copyright (c) 2024 by XGrammar Contributors"*); its vendored `3rdparty/picojson/picojson.h` header is the two-clause BSD text (*"Copyright 2009-2010 Cybozu Labs, Inc. Copyright 2011-2014 Kazuho Oku"*); `3rdparty/dlpack/include/dlpack/dlpack.h` carries **no licence text** — **unverified** (§11) | C++17; *"no @available floor beyond the package's macOS 14 / iOS 17 minimum"* (Package.swift comment) |
| **swift-transformers** (`github.com/huggingface/swift-transformers`) | `Tokenizers` — `AutoTokenizer.from(modelFolder:)` reads `tokenizer_config.json` + `tokenizer.json` from a local folder (README, *"Offline CoreML tokenizers"*), and `applyChatTemplate(messages:tools:additionalContext:)` | **1.3.4** (2026-09-02) | Apache-2.0 (`LICENSE`; API field) | `.macOS(.v13)`, `swift-tools-version: 5.9` |
| swift-jinja (`huggingface/swift-jinja`) | transitive, chat templates | ≥ 2.4.2 (latest 2.5.1) | Apache-2.0 (API field) | — |
| swift-huggingface (`huggingface/swift-huggingface`) | transitive via `Tokenizers → Hub`; **napkin does not use it to download** (§6) | ≥ 0.8.1 (latest 0.11.0) | Apache-2.0 (API field) | — |
| swift-collections, swift-crypto (`apple/…`) | transitive | as resolved | Apache-2.0 (API field) | — |
| yyjson (`ibireme/yyjson`) | transitive, `exact: "0.12.0"` | 0.12.0 | MIT (API field) | — |
| swift-numerics (`apple/swift-numerics`) | transitive of mlx-swift | as resolved | Apache-2.0 (API field) | — |
| swift-syntax (`swiftlang/swift-syntax`) | a dependency of mlx-swift-lm for its `MLXHuggingFaceMacros` target; **not linked** by napkin (§1.3), so it is resolved and, since only the macro target needs it, expected not to be compiled — **unverified** until the spike's build log says so | `"602.0.0" ..< "604.0.0"` | Apache-2.0 (API field) | — |

Every licence above is on `licenses/policy.json`'s allowlist. The `licenses check` tool reads NuGet
packages only, so these are recorded by hand in `docs/third-party-notices.md` (slice E, §9). Three
things vendored inside mlx-swift's `Cmlx` target — `metal-cpp`, `fmt`, `json` — were seen in the
directory listing and **not read** (§11); slice E reads their licence files before the notice is
written.

**Integration route.** mlx-swift-lm 3.x *"decoupled from tokenizer and downloader packages"*
(README). Its `using.md` names three ways: implement the `Downloader` / `Tokenizer` /
`TokenizerLoader` protocols yourself, use an integration package (only swift-transformers is
listed), or use the `MLXHuggingFace` macros. napkin **implements the two tokenizer protocols
itself** (about forty lines adapting `Tokenizers.Tokenizer` to `MLXLMCommon.Tokenizer`, the shape
`using.md` shows for the downloader) and needs no `Downloader` at all — *"Not needed if you have
local weights"* (`using.md`) — because the consented download is napkin's, in .NET (§6). So the
macro target and swift-syntax are never compiled into the bridge. (`MLXLMCommon/README.md` shows
`import MLXLMTokenizers` and `TokenizersLoader()`; no such product exists in `Package.swift`; the
snippet is not relied on.)

### 1.2 Toolchain: what builds it, and where

| | Required | Where it comes from |
|---|---|---|
| Xcode | **≥ 26.4** in practice: mlx-swift 0.31.6 declares `swift-tools-version: 6.3`, and Xcode 26.4 is the first release shipping Swift 6.3 (xcodereleases.com data: 26.3 → Swift 6.2.4; 26.4 → 6.3; 26.6 → 6.3.3; 27.0 → 6.4 — a secondary source, §11). Apple's own note: *"Xcode 26 requires a Mac running macOS Sequoia 15.6 or later"* ([Xcode 26 release notes](https://developer.apple.com/documentation/xcode-release-notes/xcode-26-release-notes)) | Marc's machine: Xcode 27.0 (27A266a), Swift 6.4, macOS 26.6.2 — enough. GitHub `macos-latest` = macOS 26 arm64, default Xcode **26.6** (17F113), 26.0.1–26.6 installed ([runner-images README](https://github.com/actions/runner-images/blob/main/README.md), [macos-26-arm64 image](https://github.com/actions/runner-images/blob/main/images/macos/macos-26-arm64-Readme.md), image 20260907.0351.1) — enough |
| **Metal Toolchain** | Since Xcode 26 it is a **separate download**: `xcodebuild -downloadComponent MetalToolchain`. Without it: *"error: cannot execute tool 'metal' due to missing Metal Toolchain; use: xcodebuild -downloadComponent MetalToolchain"* (run on Marc's machine today, where `xcodebuild -showComponent MetalToolchain` reports **`Status: uninstalled`**) | The GitHub runner maintainers said they would ship it for release Xcode 26 versions ([actions/runner-images#13080](https://github.com/actions/runner-images/issues/13080), closed 2025-11-28); a 2026-03-25 comment asks again, and today's image README does not list it — **unverified**; the workflow checks with `-showComponent` and downloads if uninstalled (§3.6). The issue's author calls it *"a ~700 MB download"* — secondary |
| The build tool | **`xcodebuild`, not `swift build`**: *"SwiftPM (command line) cannot build the Metal shaders so the ultimate build has to be done via Xcode"*; *"Although SwiftPM (command line) cannot build the Metal shaders, xcodebuild can and it can be used to do command line builds"* (mlx-swift README, *Installation*). mlx-swift-lm's own CI runs `xcodebuild build-for-testing … -scheme mlx-swift-lm-Package -destination 'platform=macOS'` after *"Infra: verify MetalToolchain installed: xcodebuild -showComponent MetalToolchain"* (`.github/workflows/pull_request.yml`) on **`[self-hosted, macos]`** runners | §3.2 |
| Apple silicon | MLX: *"Using Apple silicon"*; the docs warn against an x86 shell under Rosetta (install page). mlx-swift's `Package.swift` excludes the Metal backend only `#if os(Linux)`; nothing read says the Metal build runs on an Intel Mac, and nothing says it does not — **unverified**, so napkin does not try (§3.3) | — |

### 1.3 What the bridge is

`native/NapkinMlx/` — a SwiftPM package with **one dynamic library product**, built by `xcodebuild`:

```
native/NapkinMlx/
  Package.swift                 // .library(name: "NapkinMlx", type: .dynamic, targets: ["NapkinMlx"])
  Package.resolved              // committed: exact pins of §1.1
  Sources/NapkinMlx/
    ABI.swift                   // the @_cdecl functions of §1.4, nothing else public
    Bridge.swift                // load, generate (plain and guided), cancel, the handle table
    TokenizerAdapter.swift      // swift-transformers → MLXLMCommon.Tokenizer / TokenizerLoader
    Errors.swift                // every failure as a status + a sentence
  Sources/NapkinMlx/include/
    napkin_mlx.h                // the C header: the whole surface, versioned
  Tests/NapkinMlxTests/         // Swift unit tests that need no model (§7.1)
  build.sh                      // what tools/scripts/build-mlx.sh calls; outputs to out/
  out/                          // gitignored: libNapkinMlx.dylib, mlx.metallib
```

`Product.library(name:type:targets:)` with `type: .dynamic` is *"A dynamically linked library"*
([PackageDescription docs](https://developer.apple.com/documentation/packagedescription/product/library(name:type:targets:))).
Targets linked: `MLXLLM`, `MLXLMCommon`, `MLXGuidedGeneration` (mlx-swift-lm), `Tokenizers`
(swift-transformers). Deployment target macOS 14 (the packages' floor). `@_cdecl` is what makes a
Swift function C-callable; Swift's own reference says it *"uses the C calling convention"*, *"Type
bridging is not done, so the parameter and return types should correspond directly to types
accessible in C"*, and that it *"doesn't have very well-defined semantics"*
([UnderscoredAttributes.md](https://github.com/swiftlang/swift/blob/main/docs/ReferenceGuides/UnderscoredAttributes.md)) —
§8.1 for the risk; the mitigation is that the ABI uses only C scalars, C strings and opaque
pointers, so nothing but the C calling convention is relied on.

### 1.4 The C ABI — `napkin_mlx.h`, version 1

Kept to what napkin's seam needs and nothing more. Every string is UTF-8, NUL-terminated. Every
pointer the bridge **returns** is owned by the bridge and freed by the matching `_free`; every
pointer napkin **passes** is read during the call and never kept. No callbacks: cancellation is a
flag napkin owns and the bridge polls.

```c
#include <stdint.h>

typedef struct napkin_mlx_model napkin_mlx_model;          // opaque: a loaded model

typedef enum {
    NAPKIN_MLX_OK          = 0,
    NAPKIN_MLX_ERROR       = 1,   // *error says what, in a sentence; nothing was produced
    NAPKIN_MLX_CANCELLED   = 2,   // the cancel flag was set; nothing was produced
    NAPKIN_MLX_BUSY        = 3,   // a generation is already running on this model
    NAPKIN_MLX_NO_METAL    = 4,   // no Metal device (headless, virtualized, or not Apple silicon)
    NAPKIN_MLX_INCOMPLETE  = 5,   // guided: max_tokens ran out before the schema was satisfied
} napkin_mlx_status;

typedef struct {
    int32_t  has_metal;                     // 0/1
    char     architecture[64];              // Metal's device.architecture.name, or "Unknown"
    uint64_t memory_bytes;                  // hw.memsize
    uint64_t recommended_working_set_bytes; // Metal's recommendedMaxWorkingSetSize, 0 if none
    char     mlx_swift_lm_revision[48];     // the commit the bridge was built against
} napkin_mlx_device_info;

typedef struct {
    const char* text;               // the whole reply (guided: the JSON document), bridge-owned
    int32_t  prompt_tokens;
    int32_t  generated_tokens;
    int32_t  stop_reason;           // 0 stop token, 1 length, 2 cancelled, 3 schema complete
    double   prompt_seconds;
    double   generation_seconds;
} napkin_mlx_result;

int32_t           napkin_mlx_abi_version(void);                                   // 1
napkin_mlx_status napkin_mlx_init(const char* metallib_path, char** error);       // once per process
napkin_mlx_status napkin_mlx_device(napkin_mlx_device_info* out, char** error);   // no model needed
napkin_mlx_status napkin_mlx_load(const char* model_dir, const int32_t* cancel,
                                  napkin_mlx_model** out, char** error);
napkin_mlx_status napkin_mlx_generate(napkin_mlx_model* model,
                                      const char* system, const char* user,
                                      const char* json_schema,       // NULL = free text
                                      int32_t max_tokens, float temperature, float top_p,
                                      int32_t enable_thinking,       // 0/1, chat-template flag
                                      const int32_t* cancel,         // polled every token
                                      napkin_mlx_result** out, char** error);
void              napkin_mlx_result_free(napkin_mlx_result*);
void              napkin_mlx_string_free(char*);
void              napkin_mlx_unload(napkin_mlx_model*);
```

What each does, against the library as read:

- **`init`** sets `GPU.metallib` — *"The metallib override path. This can be set early in the
  process to override or provide the metallib load path."* (mlx-swift `Source/MLX/GPU+Metal.swift`)
  — to the path napkin passes (§3.3), then creates the Metal device once so a missing GPU is a
  status, not a crash later: MLX throws *"No Metal device available. This typically occurs in
  headless, sandboxed, or virtualized macOS sessions where the GPU is not accessible."*
  (`mlx/backend/metal/device.cpp`, `load_device`) → `NAPKIN_MLX_NO_METAL`.
- **`device`** fills the struct from `GPU.deviceInfo()` (`architecture`, `memorySize` from
  `hw.memsize`, `maxRecommendedWorkingSetSize`; `"Unknown"` and zeros when
  `MTLCreateSystemDefaultDevice()` is nil — `GPU+Metal.swift`). The dialog's memory line uses it
  (§4.5).
- **`load`** calls `loadModelContainer(from: URL, using: TokenizerLoader)` (`MLXLMCommon/ModelFactory.swift`)
  with napkin's adapter over `AutoTokenizer.from(modelFolder:)`. Which files it reads:
  `model.safetensors.index.json` when it names files that exist, else `model*.safetensors`
  (`Load.swift`, `WeightFileSelection.automatic`); the tokenizer's `tokenizer_config.json` and
  `tokenizer.json`; `config.json` for the architecture. Loading is not interruptible inside
  mlx-swift-lm; the flag is read before and after, and a cancel seen after unloads and returns
  `CANCELLED`. **Blocks the calling thread** for the whole load (§1.5).
- **`generate`** builds `UserInput(chat: [.system(system), .user(user)], additionalContext:
  ["enable_thinking": enable_thinking != 0])` — `additionalContext` is *"Additional values provided
  for the chat template rendering context"* (`UserInput.swift`), and `enable_thinking` is exactly
  the kwarg Qwen3's template reads (§5.2) — prepares it through `context.processor.prepare(input:)`,
  then:
  - `json_schema == NULL`: `MLXLMCommon.generate(input:parameters:context:) → AsyncStream<Generation>`
    with `GenerateParameters(maxTokens:, temperature:, topP:)` (`Evaluate.swift`; the struct's
    default temperature is 0.6, napkin passes its own 0.2), concatenating `.chunk(text)` and reading
    `.info` for the token counts and times. Cancellation: the consuming `Task` is cancelled when the
    flag is seen, and the token loop is `tokenLoop: while !Task.isCancelled` with `stopReason =
    .cancelled` (`Evaluate.swift`, `generateTokenTask`).
  - `json_schema != NULL`: the guided loop of §2; `stop_reason = 3` when the grammar accepted.
  - One generation at a time per handle: a second concurrent call returns `BUSY` at once. A model's
    grammar constraints are cached per schema text and `clone()`d per call (§2).
- **`unload`** drops the container; the caller's handle is dead after it. **`init`** may be called
  once; a second call is a no-op returning `OK`.
- **Errors** are `ERROR` plus a sentence in the library's or napkin's words — *"the folder has no
  config.json"*, the Swift error's `localizedDescription` for a load failure — never a crash for
  anything the bridge can catch. A Swift `fatalError` inside MLX is not catchable and takes napkin
  with it (§8.6).

### 1.5 Threading

- The bridge functions **block the thread that calls them**. napkin calls them from a thread-pool
  thread (`Task.Run`), never from the UI thread, and never from a Swift cooperative thread — the
  guided README warns: *"Don't call it from `@MainActor` — run it in `Task.detached` or on a
  background executor."* Inside, the bridge runs the async Swift work in a detached `Task` and
  waits on a `DispatchSemaphore` on the caller's thread, which is a .NET thread and so is not one
  of Swift's cooperative pool.
- The cancel flag is a `const int32_t*` that napkin allocates in unmanaged memory
  (`NativeMemory.Alloc`) per request and writes from any thread; the bridge reads it as an atomic
  every token. A cancel lands at the next token (§8.7).
- One `init` per process; one load at a time per process (a lock); one generation at a time per
  model (`BUSY` otherwise). No state is shared between handles.

---

## 2. Structured output: what mlx-swift-lm supports, and what napkin does with it

**Finding: mlx-swift-lm supports schema-constrained decoding, on `main`, through
`MLXGuidedGeneration`.** Its README: *"Guided (constrained) generation for MLX. It forces a
language model's output to conform to a JSON Schema, an EBNF grammar, or an XGrammar structural
tag by masking the token logits at every decoding step, so the result is always structurally valid.
It works with any MLX language model and runs on macOS 14 / iOS 17 and later."* The engine is
xgrammar, vendored and namespace-renamed so it *"cannot collide with any other XGrammar linked into
the same binary"*. The standalone path, exactly as its README shows and as `XGrammarBridge.swift`
and `GuidedGenerationLoop.swift` declare it:

1. `TokenizerVocabExtractor.extractForGrammar(from: tokenizer)` → the vocab in xgrammar's shape;
2. `GrammarTokenizer(vocab:vocabType:eosTokenId:)`;
3. `GrammarConstraint(tokenizer:jsonSchema:fastForward:hostTokenizer:)` — the compiled schema;
   `clone()` gives an independent copy, so the bridge compiles once per schema text and clones per
   request;
4. `GuidedGenerationLoop.run(input:context:constraint:maxTokens:vocabSize:…, emit: (String) -> Bool)
   throws -> Int` — *"emit: Callback for each text delta. Return `false` to stop"*; throws
   `GuidedGenerationError.incompleteOutput` *"if maxTokens is exhausted before the grammar reaches a
   stop state"* and `.prematureEOS` *"if the model emits EOS before the grammar accepts"*; checks
   `Task.checkCancellation()` every token.

Two properties of that loop shape napkin's use of it:

- **It samples by argmax.** `applyMaskAndSample` is documented *"Apply a prebuilt grammar mask and
  optional bias to logits, then argmax"*; `run` takes no temperature. So a proposal is greedy under
  the grammar, whatever napkin's temperature setting says; the dialog's temperature applies to
  answers only, and the note says so beside the field. Qwen's card's *"DO NOT use greedy decoding"*
  is stated for thinking mode (§5.2); whether greedy-under-grammar hurts napkin's small proposals is
  what the eval set measures (§7.4, risk §8.4).
- **The first call per schema compiles the grammar on the calling thread** — *"can block for
  hundreds of milliseconds on a cold grammar compile … Later calls that reuse the same compiled
  grammar and tokenizer skip the compile. Pre-warming the expected schema … removes the blocking
  window entirely"* (README). The bridge compiles both of napkin's schemas (sketch, edit) at the end
  of `load`, so the first proposal pays nothing extra and the load's own line already says
  *"loading…"*.

`GenerationComponents.logitProcessorFactory` (`MLXLMCommon/GenerationComponents.swift`) is the
other hook — a custom `LogitProcessor` composed into the ordinary `generate`, which would keep
temperature — but `MLXGuidedGeneration` ships no `LogitProcessor` wrapping `GrammarConstraint`
(only `CompositeLogitProcessor`, a chain), so that route would mean napkin writing its own mask
processor against xgrammar's bridge. Not recommended for M14; noted as the upgrade if greedy
proposals prove poor.

**The fallback, if Marc pins the 3.31.4 release instead (Decision 1).** No constrained decoding
exists there. Then `json_schema` is honoured by *prompting*: the schema text is appended to the
system prompt with *"Reply with JSON matching this schema and nothing else"*, the reply is stripped
of a code fence if present, parsed by napkin's strict parser; on failure the bridge is asked once
more with the parser's complaint appended; a second failure is `ModelReply.Refused("the assistant's
reply was not a proposal napkin could read")`. Cost: two generations in the worst case (twice the
seconds), a proposal rate the eval set must measure, and a `Refused` where the constrained path
would have produced a document. napkin's parser is strict either way (llm-assistant §4.3), so the
guard on what lands is the same; only how often a proposal arrives differs.

What xgrammar's JSON-Schema support covers (which keywords, `enum`, `required`, array bounds) was
**not read** in this task — **unverified**; napkin's two schemas use objects, arrays, strings,
integers and `required` only, and slice A's spike (§9) asserts both compile.

---

## 3. Build and packaging

### 3.1 The rule: `dotnet build` never builds Swift

The gate (`gate.sh`) and CI's `build-and-test` stay exactly as they are: no Xcode, no Metal
toolchain, no Swift in the path that lands every change, on either platform. The bridge is built by
**one script**, on demand, and `Napkin.App.csproj` **includes its output if it exists**:

```xml
<!-- The MLX bridge (docs/design/mlx-runtime.md §3). Built separately by tools/scripts/build-mlx.sh;
     present only on a Mac where that ran, or in the release job that downloaded it. On any other
     machine the folder is absent and nothing is included: the dialog then says MLX is not in this
     build. Kept out of the single file so the metallib stays beside the dylib (§3.3). -->
<ItemGroup Condition="Exists('..\..\native\NapkinMlx\out\libNapkinMlx.dylib')">
  <Content Include="..\..\native\NapkinMlx\out\libNapkinMlx.dylib;..\..\native\NapkinMlx\out\mlx.metallib"
           Link="native\%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest"
           ExcludeFromSingleFile="true" />
</ItemGroup>
```

`ExcludeFromSingleFile` is the documented way to *"place some files in the publish directory but not
bundle them in the file"* ([single-file overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
So `dotnet build napkin.sln` on Windows, on a Mac without Xcode, and on a Mac that never ran the
script all behave as today, and the same commit builds everywhere.

### 3.2 `tools/scripts/build-mlx.sh`

```sh
xcodebuild -showComponent MetalToolchain | grep -q 'Status: installed' \
  || { echo "Metal Toolchain missing: run  xcodebuild -downloadComponent MetalToolchain"; exit 2; }
xcodebuild build -scheme NapkinMlx -configuration Release -destination 'platform=macOS,arch=arm64' \
  -derivedDataPath native/NapkinMlx/.derived -skipPackagePluginValidation
# copy libNapkinMlx.dylib and the metallib into native/NapkinMlx/out/ ; print sizes; nm -gU | grep napkin_mlx_
codesign -dvv native/NapkinMlx/out/libNapkinMlx.dylib   # expect Signature=adhoc, no Authority
```

The exact product paths under DerivedData, and the metallib's name and location as Xcode emits it
for a SwiftPM package (MLX's own code looks for `<SWIFTPM_BUNDLE>.bundle` = `mlx-swift_Cmlx.bundle`
with `default.metallib` in its resources — `device.cpp`, `try_load_bundle`; `Package.swift` defines
`SWIFTPM_BUNDLE "mlx-swift_Cmlx"` and `METAL_PATH "default.metallib"`), are **unverified** until
slice A's spike runs the build; the script is written then, against what the build produced.

### 3.3 Where the metallib goes, and why the dylib is not folded into the single file

MLX's search order for its Metal library (`device.cpp`, `load_default_library`): an explicit
override path first; then *"the colocated mlx.metallib"* — `current_binary_dir() / "mlx"` with
`.metallib` appended, where `current_binary_dir()` is `dladdr` on MLX's own code
(`mlx/backend/common/utils.cpp`), i.e. **the directory of the dylib MLX is compiled into**; then
`Resources/mlx.metallib`; then the SwiftPM bundle; then `Resources/default.metallib`; then the
compile-time default. napkin does two things, either of which alone would work:

1. ships `mlx.metallib` **beside `libNapkinMlx.dylib`** in `native/`, and
2. passes that path to `napkin_mlx_init`, which sets the override (`GPU.metallib`) — so the
   lookup is deterministic and the error, if the file is missing, names the path.

This is why the dylib is **excluded from the single file**: with
`IncludeNativeLibrariesForSelfExtract=true` (release.yml) a bundled native library is *"extracted to
disk before the app starts"* under `$HOME/.net` (single-file overview), where its neighbour would
not be. Kept beside the executable, `AppContext.BaseDirectory` finds both — the page's own advice:
*"To access files next to the executable, use AppContext.BaseDirectory."*

### 3.4 Apple silicon only; what an Intel Mac gets

The bridge is built for `arch=arm64` only and shipped in the **`osx-arm64`** artifact only. `osx-x64`
is *"a plain cross-architecture publish"* (release.yml) and stays exactly that: no `native/` folder.
At runtime `MlxAvailability` (§4.2) is true only when `OperatingSystem.IsMacOS()`,
`RuntimeInformation.OSArchitecture == Architecture.Arm64`, and both files exist; otherwise the
dialog's third radio is disabled with its one sentence. napkin never dlopens the bridge on an Intel
Mac (Rosetta or not), because nothing read says MLX's Metal backend runs there (§1.2).

### 3.5 Signing: what the release check asserts

Nothing is signed by napkin (DESIGN.md §6.6). arm64 code carries the toolchain's automatic ad-hoc
signature; whether `xcodebuild` applies one to a SwiftPM dynamic library the same way the .NET
apphost gets one is **unverified** until the spike's `codesign -dvv`. Either way the rule is the
same as today's: the release workflow's *check* step extends to `native/libNapkinMlx.dylib` —
`Signature=adhoc` required on arm64, any `Authority=` or notarization ticket refused — and **no
`codesign` step is added** (`ReleaseWorkflowTests.cs` fails if one is). If the spike finds the dylib
unsigned and macOS refuses to load it, the fix is the toolchain's own `-adhoc` linker behaviour, a
build setting, not a signing step; that would be a follow-up decision for Marc.

### 3.6 CI

- `ci.yml`: **unchanged** for `build-and-test`. A new job **`mlx-bridge`** on `macos-latest`
  (macOS 26 arm64, Xcode 26.6 default — §1.2): `sudo xcode-select -s /Applications/Xcode_26.6.app`
  (explicit, so a future default of Xcode 27 with different Swift is a deliberate change),
  `xcodebuild -showComponent MetalToolchain` and `-downloadComponent MetalToolchain` if not
  installed, then `build-mlx.sh`, then `nm -gU` asserting the eight exported symbols of §1.4, then
  `codesign -dvv` as §3.5, then upload `native/NapkinMlx/out/` as an artifact. It runs on push to
  `main` and on changes under `native/**` or the workflow; it does not gate `build-and-test`. It
  never loads a model — MLX itself says Metal is unavailable in *"headless, sandboxed, or virtualized
  macOS sessions"*, and mlx-swift-lm's own tests run on self-hosted Macs (§1.2).
- `release.yml`: the `osx-arm64` build downloads the `mlx-bridge` artifact into
  `native/NapkinMlx/out/` before `dotnet publish`, so the publish includes it (§3.1); the Stage
  listing shows it; the signature check covers it; the launch smoke test is unchanged (the app must
  start without touching the bridge — it does, because nothing loads until the person chooses MLX).
  `SOURCE.txt` gains one line naming the bridge and `docs/third-party-notices.md`.
- Build time and artifact size (a C++ MLX with Metal kernels, xgrammar, Swift) are **unverified**;
  the spike reports both (§8.3).

### 3.7 Notices

`docs/third-party-notices.md` gains a section *"The MLX bridge (macOS arm64 builds only)"* listing
every row of §1.1 with its licence, the commit or tag, and the sentence *"napkin's own bridge code
in `native/NapkinMlx` is AGPL-3.0 like the rest of napkin"*; `SOURCE.txt` in the arm64 zip points
at it. The MIT and Apache notices travel in the zip as `native/NOTICES.txt`: Apache-2.0 §4(d) —
read from the vendored xgrammar `LICENSE` — says a redistribution *"must include a readable copy of
the attribution notices contained within such NOTICE file"*, and xgrammar ships one (*"XGrammar,
Copyright (c) 2024 by XGrammar Contributors"*).

---

## 4. The .NET side

### 4.1 `Napkin.Assistant.Mlx` — its own assembly, its own floor

The `Napkin.Interop.Dxf` / `Napkin.Assistant.LocalServer` pattern: one outside thing, behind a
seam, tested through a fake. It references `Napkin.Modules.Assistant` only. No package
dependencies. `AllowUnsafeBlocks` is on, which `LibraryImport` requires (*"The project must be
marked unsafe using AllowUnsafeBlocks"* — [P/Invoke source generation](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation)).

```csharp
namespace Napkin.Assistant.Mlx;

/// The C ABI of native/NapkinMlx/include/napkin_mlx.h, one method per function, with C# types.
/// The real one P/Invokes; the tests' fake scripts replies and records calls.
public interface INativeMlx
{
    int AbiVersion();
    MlxStatus Init(string metallibPath, out string? error);
    MlxStatus Device(out MlxDeviceInfo info, out string? error);
    MlxStatus Load(string modelFolder, CancelFlag cancel, out MlxModelHandle handle, out string? error);
    MlxStatus Generate(MlxModelHandle model, string system, string user, string? jsonSchema,
                       int maxTokens, float temperature, float topP, bool enableThinking,
                       CancelFlag cancel, out MlxGeneration result, out string? error);
    void Unload(MlxModelHandle model);
}

public enum MlxStatus { Ok = 0, Error = 1, Cancelled = 2, Busy = 3, NoMetal = 4, Incomplete = 5 }
public readonly record struct MlxDeviceInfo(bool HasMetal, string Architecture, ulong MemoryBytes, ulong RecommendedWorkingSetBytes, string MlxSwiftLmRevision);
public sealed record MlxGeneration(string Text, int PromptTokens, int GeneratedTokens, MlxStopReason StopReason, TimeSpan Prompt, TimeSpan Generation);
```

- **`NativeMlx : INativeMlx`** — the only file that P/Invokes. `[LibraryImport("NapkinMlx",
  StringMarshalling = StringMarshalling.Utf8)]` on `static partial` methods (the source generator:
  *"removing the need for the generation of an IL stub at runtime"*; UTF-8 *"a first-class
  option"*); `NativeLibrary.SetDllImportResolver(typeof(NativeMlx).Assembly, …)` (*"Only one import
  resolver can be set for a given assembly"* — [native library loading](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading))
  resolving `"NapkinMlx"` to `Path.Combine(AppContext.BaseDirectory, "native", "libNapkinMlx.dylib")`
  and returning `IntPtr.Zero` for any other name. The runtime would also try `libNapkinMlx.dylib`
  on its own (*"the runtime will try prepending `lib` and appending the canonical shared library
  extension"*, same page), but the resolver makes the location explicit and the failure message
  name the path. Reads `napkin_mlx_result` through a blittable struct and
  `Marshal.PtrToStringUTF8`, then frees through the bridge. The class is
  `[ExcludeFromCodeCoverage]` with the reason in a comment (§7.2, Decision 6).
- **`CancelFlag`** — a `SafeHandle` over four bytes of `NativeMemory.Alloc`, set by
  `Volatile.Write` from the `CancellationToken`'s registration, freed on dispose.
- **`MlxModel : IAssistantModel, IDisposable`** — takes `INativeMlx`, a `ModelFolder`, the
  temperature, and the metallib path; `Init` and `Load` happen on the first `AskAsync` (or on
  `TestAsync`) on `Task.Run`, once, under a lock; every subsequent `AskAsync` is one `Generate` on
  `Task.Run` with `LocalServerModel.UserMessage(request)` as the user message and `request.System`
  as the system message (the same two messages as slice C), `request.Schema` as `json_schema`,
  `enableThinking: false` always (§5.2), `maxTokens` 1024 for text and 2048 for a proposal (napkin's
  numbers), `topP` 1.0. Replies: `Ok` + schema null → `Text`; `Ok` + schema → `Json`; `Incomplete` →
  `Refused("the assistant ran out of room before finishing the proposal")`; `NoMetal` →
  `Refused("MLX found no Metal device on this Mac")`; `Busy` → `Refused("the assistant is still
  answering the last question")`; `Error` → `Refused(sentence)`; `Cancelled` → the
  `OperationCanceledException` the note expects. The **30-second question timeout** (slice C's,
  llm-assistant §5.2) starts **after** the load: a first load reads 2.3 GB and cannot sit under it
  (§8.2); while loading, `LoadingState` is observable so the note's thinking line reads *"loading
  the model (2.3 GB)… 12 s"*. `Whereabouts`: *"In napkin (MLX): Qwen3-4B-4bit from ~/Library/…/models/mlx-community--Qwen3-4B-4bit--4dcb3d101c2a — nothing leaves this machine."*
  (the folder's display name, home-relative). `Dispose` unloads.
- **`ModelFolder`** — a validated path: refuses, with the file named, a folder without
  `config.json`, without `tokenizer.json` or `tokenizer_config.json`, or without any
  `*.safetensors`; reads `config.json`'s `model_type` and `quantization` (`{"group_size": 64,
  "bits": 4}` in every §5 repo) for the dialog's line; reads a `README.md`'s YAML `license:` line if
  present (the card's front matter) else *"licence not stated in the folder"*. Pure, tested on
  temp folders.
- **`MlxAvailability`** — `Available(OperatingSystem-is-macOS, Architecture, Func<string,bool>
  fileExists, baseDirectory)` → the reason it is not, or null; the platform predicate is injected so
  every branch is a test.

Dependency graph, restated: `Assistant.Mlx → Modules.Assistant` (+ `Assistant.LocalServer` for
`UserMessage`, or that helper moves into the module — slice B's call, one line). `App → all`.
Nothing references `App`.

### 4.2 Settings: version 3 → 4

```csharp
public enum AssistantProvider { None, LocalServer, Mlx }

public sealed record AssistantSettings(
    AssistantProvider Provider,
    string? Endpoint,          // LocalServer, as today
    string? Model,             // LocalServer, as today
    double Temperature,        // answers only under Mlx (§2)
    string? ModelFolder);      // Mlx: the folder chosen or downloaded; kept when the provider changes
```

`UserSettings.CurrentVersion = 4`; the remark gains *"4: `Mlx` and `ModelFolder`"*. A version-3
file gives the defaults and the store's notice, as a version-2 file does today — the beta policy.
Slice C's §16.2 item 2 records that `System.Text.Json` would have read this addition *without* a
bump; the bump is what the task asks for and keeps the version honest about what a file may hold
(Decision 7).

### 4.3 `AssistantModels.FromSettings`

```csharp
public static IAssistantModel FromSettings(UserSettings settings, HttpMessageHandler? http = null, INativeMlx? mlx = null)
```

`Mlx` → `MlxAvailability` says yes, `ModelFolder.TryParse` says yes → `new MlxModel(mlx ?? new
NativeMlx(), folder, temperature, metallibPath)`; anything else → `ScriptedModel()` (the no-model
state), never a partial. The GUI suite passes a `FakeNativeMlx` the way it passes a stub handler
(`MainWindow.AssistantHttp` gains a sibling `AssistantMlx`).

### 4.4 The dialog

*Where the model runs…* gains, under a third radio **In napkin, on this Mac (MLX)**:

- a folder box + **Choose…** (the window's `StorageProvider` folder picker — `SitePlanWindow`
  already uses the same provider for files), and under it the
  `ModelFolder` line — *"qwen3, 4-bit (group 64), licence: apache-2.0"* or the refusal naming the
  missing file;
- **Download Qwen3-4B-4bit (2.28 GB, Apache-2.0)…** which opens the consent sheet (§6.3); when the
  folder already exists with every file present the button reads **Downloaded** and is disabled;
- the memory line, now from `napkin_mlx_device` when available — *"This Mac: 24 GiB memory, Metal
  recommends up to N GiB for the GPU (Apple's number); the model needs about 2.3 GB plus its
  working memory (napkin's estimate)"* — with the same "too big for this machine by napkin's rule"
  wording as today when the folder's weights exceed it;
- **Test** loads (line: *"loading… n s"*, cancellable) then asks for "ok" as today;
- the temperature field's hint gains *"applies to answers; a proposal is decoded greedily under its
  schema"* (§2);
- when `MlxAvailability` says no: the radio disabled and its sentence (*How to use it*).

`Guidance` gains the MLX lines with their sources, as slice C did for Ollama's.

---

## 5. Models

Every fact from the Hub's API (`GET /api/models/<repo>?blobs=true` and `GET
/api/models/<repo>/tree/<commit>`), the repo's `config.json`, `tokenizer_config.json` and
`README.md` at the pinned commit, and the base model's card, read 2026-09-27.

| Repo (commit) | Converted from | Base or post-trained? | Licence (card) | Files napkin fetches | Total | Context |
|---|---|---|---|---|---|---|
| **mlx-community/Qwen3-4B-4bit** (`4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25`, 2025-04-28) — *recommended default* | *"converted to MLX format from Qwen/Qwen3-4B using mlx-lm version 0.24.0"* (card) | **Post-trained.** `Qwen/Qwen3-4B`'s card declares `base_model: Qwen/Qwen3-4B-Base` and describes *"seamless switching between thinking mode … and non-thinking mode"* — it is the chat model, not the Base; the 4-bit card's `base_model: Qwen/Qwen3-4B` | `apache-2.0`, `license_link: huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE` | 8 (§6.1) | **2,277,297,903 bytes** (2.28 GB; 2.12 GiB) | `max_position_embeddings 40960` |
| mlx-community/Qwen3-4B-Instruct-2507-4bit (`50d427756c6b1b2fe0c0a10f67fbda1fc8e82c1b`, 2026-01-02) — *alternative, Decision 4* | from `Qwen/Qwen3-4B-Instruct-2507` (card) | post-trained; the base card *"supports only non-thinking mode"* (llm-assistant §16.1 item 1) | `apache-2.0` | 10 (adds `chat_template.jinja`, `generation_config.json`) | 2,277,297,844 bytes | `262144` |
| mlx-community/Phi-4-mini-instruct-4bit (`ac1c269cb4222a4e136a3d09edad301056c1f36a`, 2025-03-05) — *the MIT alternative* | *"converted to MLX format from microsoft/Phi-4-mini-instruct using mlx-lm version 0.21.5"* (the card is titled `pcuenq/Phi-4-mini-instruct-Q4-mlx`, re-hosted under mlx-community; tag `mlx-my-repo`) | post-trained (`base_model: microsoft/Phi-4-mini-instruct`, whose card: *"The model is licensed under the MIT license"*) | `mit` | 8 — the repo also holds `configuration_phi3.py`, `modeling_phi3.py`, `sample_finetune.py`, which napkin **never fetches** (§6.1) | 2,177,574,851 bytes (2.18 GB) | `131072` |

All three: `quantization {"group_size": 64, "bits": 4}`, `torch_dtype bfloat16`, one
`model.safetensors` (no shards). Architectures `qwen3` and `phi3` are both in mlx-swift-lm's
`LLMTypeRegistry` (`LLMModelFactory.swift`: `"phi3": create(Phi3Configuration.self, …)`, `"qwen3":
createQwen3CompatibleModel`). Gemma and Llama stay excluded for their terms (llm-assistant §5.3).

### 5.1 Why the default stays Qwen3-4B

It is the model the signed-off note names (§5.3, §13.3), Apache-2.0, and under MLX there is no
`qwen3:4b`-tag ambiguity (§16.1 item 1) — the repo is exactly the Qwen3-4B checkpoint. The
2507-Instruct repo is newer and never thinks; whether to switch is Marc's (Decision 4), as §16.1
already asked.

### 5.2 Thinking off, exactly

The 4-bit repo's `tokenizer_config.json` carries the chat template (4,116 characters), which ends:

```jinja
{%- if add_generation_prompt %}
    {{- '<|im_start|>assistant\n' }}
    {%- if enable_thinking is defined and enable_thinking is false %}
        {{- '<think>\n\n</think>\n\n' }}
    {%- endif %}
{%- endif %}
```

So passing `enable_thinking: false` in the template context writes an empty think block and the
model answers directly — Qwen's card: *"When `enable_thinking=False` … the model will not generate
think content and will not include a `<think>...</think>` block."* mlx-swift-lm carries this as
`ReasoningConfig.thinkTagsWithEnableThinking` — `promptStrategy: .templateFlag(key:
"enable_thinking", defaultOn: true)` (`ReasoningConfig.swift`) — and the bridge sets it through
`UserInput.additionalContext` (§1.4). Phi-4-mini has no such flag; the key is ignored by a
template that does not read it. The card's sampling suggestion for non-thinking mode
(*"Temperature=0.7, TopP=0.8, TopK=20, and MinP=0"*) stays what the dialog quotes beside napkin's
0.2, as today.

---

## 6. The consented download

### 6.1 The catalog — data napkin carries, read in this task

`ModelCatalog` in `Napkin.Assistant.Mlx` is a static table, one entry per repo of §5, each with the
repo id, the **commit**, the licence and card URL, and every file to fetch with its **byte size**
and **SHA-256**. The file set is exactly what mlx-swift-lm's loader wants —
`modelDownloadPatterns = ["*.safetensors"] + ["*.json", "*.jinja"]` (`ModelFactory.swift`) applied
to the repo's tree — so `.gitattributes`, `README.md`, `merges.txt` and every `.py` are left out.
The default entry:

| File | Bytes | SHA-256 | How the hash was read |
|---|---|---|---|
| `model.safetensors` | 2,263,022,529 | `e240c0bdc0ebb0681bf0da0f98d9719fd6ebe269a3633f81542c13e81345651d` | the Hub's LFS pointer: `lfs.oid` in `/tree/<commit>` and `/api/models?blobs=true`; the same value is the `x-linked-etag` of the resolve URL |
| `tokenizer.json` | 11,422,654 | `aeb13307a71acd8fe81861d94ad54ab689df773318809eed3cbe794b4492dae4` | LFS pointer, and confirmed by hashing the bytes |
| `vocab.json` | 2,776,833 | `ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910` | hashed from the bytes at the commit (`x-repo-commit` checked) |
| `model.safetensors.index.json` | 63,924 | `f7825defe5865d179c3b593173d37056be5f202dcb7153985cf74e75ecf1628b` | hashed |
| `tokenizer_config.json` | 9,706 | `253153d0738ceb4c668d2eff957714dd2bea0b56de772a9fdccd96cbf517e6a0` | hashed |
| `config.json` | 937 | `b5efdcf3b0035a3638e7228dad4d85f5c4a23f156eb7cdb0b44c8366a5d34d9b` | hashed |
| `added_tokens.json` | 707 | `c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680` | hashed |
| `special_tokens_map.json` | 613 | `76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd` | hashed |

Repo `mlx-community/Qwen3-4B-4bit`, commit `4dcb3d101c2a062e5c1d4bb173588c54ea6c4d25`, total
**2,277,297,903 bytes**. The same table for the other two repos (their LFS hashes:
Phi-4-mini `model.safetensors` `9dcfcdc0a579494283f2d2317ad686123782ce461d1111690ede7c904ff3f87b`,
`tokenizer.json` `382cc235b56c725945e149cc25f191da667c836655efd0857b004320e90e91ea`; 2507-Instruct
`model.safetensors` `2a73c6c248601ab904e035548abd8e6abb65ea27dcb5f342fb0a8910eb44173f`; every small
file's hash was computed the same way and goes into the table in slice D from the values recorded
in this task's report). A test asserts every entry's shape: a 40-hex commit, a size, a 64-hex hash,
no file outside the loader's patterns, no `.py`.

### 6.2 The URLs and the wire

- Each file is `https://huggingface.co/<repo>/resolve/<commit>/<file>` — the commit, **never
  `main`**. Verified today: `…/resolve/4dcb3d10…/config.json` answers `307` with `x-repo-commit:
  4dcb3d10…`, `accept-ranges: bytes`, `x-linked-etag: "<git blob sha>"`; `…/model.safetensors`
  answers `302` to `https://us.aws.cdn.hf.co/…` with `x-repo-commit`, `x-linked-size: 2263022529`,
  `x-linked-etag: "e240c0bd…"` (the SHA-256), `accept-ranges: bytes`, and the CDN answers `200`
  with `accept-ranges: bytes`. The Hub documents that *"Downloads follow HTTP redirects from
  `huggingface.co` to these hostnames"* (`us.aws.cdn.hf.co`, `cdn-lfs-us-1.hf.co` and the rest —
  [models-downloading](https://huggingface.co/docs/hub/en/models-downloading)), so the client
  follows redirects **only to hosts on that documented list or `huggingface.co`** and refuses any
  other — the loopback handler's discipline (§16.2 item 1), inverted.
- `HttpClient` over a handler napkin builds (`AllowAutoRedirect = false`, redirects followed by
  napkin's code against the allow-list, no proxy surprises hidden); a test passes a stub handler and
  no test opens a socket.
- **Resume**: a file is written to `<file>.part`; on retry the request carries `Range:
  bytes=<length>-` and expects `206`; `200` restarts the file. **Cancel**: the token aborts the
  request and leaves the `.part` for a later resume; nothing else is touched.
- **Verification**: SHA-256 is computed as bytes arrive (`IncrementalHash`); a mismatch at the end
  deletes the `.part` and refuses with the file name and both hashes; a size mismatch likewise.
  The folder is assembled under `<target>.downloading/` and renamed to `<target>/` only when every
  file passed, so a folder that exists is a folder that is whole.
- **Never on launch, never silently**: the only caller is the consent sheet's Download button;
  `FromSettings` never downloads; the catalog is data.

### 6.3 The consent sheet

A sheet over the dialog (the Firm up shape), listing: the repository and commit; *"8 files,
2,277,297,903 bytes (2.28 GB)"*; every file with its size and its SHA-256 (mono, scrollable); the
licence — *"Apache-2.0, per the model card: huggingface.co/mlx-community/Qwen3-4B-4bit"* — as a
link; the destination folder; the sentence *"napkin will connect to huggingface.co and its content
servers for this download and for nothing else; nothing about you or your design is sent"*; a
checkbox **"Download these files from huggingface.co"** (unticked); **Download** (enabled by the
checkbox) and **Cancel**; then the progress bar with bytes and the current file, **Cancel** while
running, and at the end *"Downloaded and checked: 8 files"* or the refusal. Closing the sheet
mid-download cancels.

### 6.4 Where it is stored

`Path.Combine(SettingsStore.ConfigDirectory(), "models", $"{owner}--{name}--{commit[..12]}")` —
`~/Library/Application Support/napkin/models/mlx-community--Qwen3-4B-4bit--4dcb3d101c2a` on macOS,
the same `models/` under `%APPDATA%\napkin` or `$XDG_CONFIG_HOME/napkin` elsewhere (not reachable
there in M14, since the button is disabled off Apple silicon, but the function is one and
platform-tested). The settings store the folder path; deleting the folder in Finder gives the
no-model state and the dialog's *Download…* button comes back.

---

## 7. Tests

Nothing in the unit or GUI suites loads weights, needs Metal, dlopens the bridge, or opens a
socket. Feature ids `AST-007…`, workflows `GUI-AST-07`, `-08`.

### 7.1 The Swift package (`native/NapkinMlx/Tests`)

Small, model-free, run by `build-mlx.sh --test` and the `mlx-bridge` CI job (they need the
toolchain, not a GPU — **unverified** whether linking MLX without a device lets a test process start
on the runner; the spike says): the tokenizer adapter over a fixture `tokenizer.json` (a tiny one
committed under `Tests/Fixtures`, not a model's); `napkin_mlx_abi_version() == 1`; every status has
a sentence; a `load` of a folder without `config.json` returns `ERROR` and the sentence names the
file; `init` with a missing metallib path returns `ERROR` naming the path (the device probe is
skipped when `NAPKIN_MLX_NO_DEVICE_PROBE=1`, for exactly this test).

### 7.2 `tests/Napkin.Assistant.Mlx.Tests` (`AST-007`)

Through **`FakeNativeMlx : INativeMlx`** — scripted statuses and replies per call, records every
argument (system, user, schema, flags, temperature), can delay and observe the cancel flag:

- `MlxModel`: `Init`+`Load` happen once, on the first ask, off the calling thread; `Generate` gets
  `request.System` as system and `LocalServerModel.UserMessage(request)` as user, `enableThinking`
  false, the schema passed through verbatim, `maxTokens` 1024/2048; `Ok` → `Text` or `Json` by
  schema; each non-`Ok` status → the exact `Refused` sentence of §4.1; a fake that observes the flag
  set mid-generation returns `Cancelled` and the task cancels; `Busy` when two asks overlap; the
  timeout starts after the load (a fake load of 40 s followed by a 1 s generate is not a
  timeout); `Whereabouts` verbatim; `Dispose` unloads exactly once.
- `ModelFolder`: each missing file refused by name; `model_type`, `quantization`, licence line
  read from a temp folder; a folder that is a file refused.
- `MlxAvailability`: the eight combinations of (macOS, arm64, dylib present, metallib present)
  → the reason or null, verbatim.
- `ModelCatalog`: shape as §6.1; the default entry's total equals the sum of its sizes; every URL
  is `https://huggingface.co/<repo>/resolve/<commit>/<file>`.
- `ModelDownload` through a stub handler: nothing is requested until `Download` is called with
  consent `true` (a call with `false` throws before any request); redirects to a documented host
  followed, to any other host refused with the host named; `Range` sent from a `.part`'s length and
  `206` appended, `200` restarted; a wrong hash deletes the `.part` and refuses naming the file
  and both hashes; a short body refuses on size; cancel leaves the `.part`; the rename happens only
  after the last file passed; progress reports bytes and the file name.
- `NativeMlx` itself is `[ExcludeFromCodeCoverage]` (Decision 6): its lines are the generated
  P/Invoke stubs and the resolver, and the only honest test of them loads the real library.

The assembly gets its floor in `ratchet/baseline.json` on slice B's landing.

### 7.3 GUI workflows (`tests/Napkin.App.GuiTests`)

The harness passes `FakeNativeMlx` and a stub `HttpMessageHandler`; `MlxAvailability` is forced
available through the same test seam so the workflows run on Windows CI too.

- **GUI-AST-07 Choose MLX and ask.** Open the dialog by menu (pointer); click the third radio;
  type a folder path (a temp folder the workflow fills with the three required files); press
  **Test** (pointer) — assert *"loading…"* then the fake's "ok" line; **Use these settings**; assert
  settings v4 written with `Provider = Mlx` and the folder; Ctrl/Cmd+Shift+A; type a question;
  Enter; assert the answer and the whereabouts line verbatim; Escape.
- **GUI-AST-08 The consented download.** Dialog → third radio → **Download…** (pointer); assert
  the sheet lists 8 files, the total, the licence link, the destination, and that the stub handler
  saw **no request**; press **Download** with the box unticked — nothing; tick it (pointer);
  **Download**; assert requests only to the pinned URLs, the progress line, and *"Downloaded and
  checked: 8 files"* (the stub serves bytes whose hashes the workflow computed and put in a test
  catalog — the real catalog's 2.3 GB are never served); a second run with one wrong byte: the
  refusal names the file. Escape closes the sheet.

### 7.4 The opt-in path: real weights, a person's machine

- `napkin-tools assistant mlx-smoke --model <folder>`: `init`, `device` (printed), `load` (time
  and memory printed), one text ask (*"Reply with ok."*), one proposal ask against the sketch
  schema, `unload`; exits 0 whatever the answers say and prints them. Not in `gate.sh`, not in CI.
- `assistant eval --mlx <folder>` (slice G's command, #235, gaining one option): the twenty cases
  through `MlxModel` on the real bridge, the same table as for Ollama — this is where "greedy under
  the schema" (§2) and "a 4B model misreads the pack" are measured.
- What CI can and cannot do: it can build the bridge, assert its exports and signature, and run
  the Swift package's model-free tests (if the runner can start a process that links MLX — §7.1,
  unverified); it **cannot** load a model, because MLX finds no Metal device in a virtualized
  session (§1.4). So the smoke and the eval are run by a person, and slice A's landing report
  carries their output.

---

## 8. Risks

1. **API churn on `main`.** Pinning a commit of mlx-swift-lm (Decision 1) means every upgrade is a
   deliberate re-pin with the smoke and the eval re-run; the bridge's surface is small enough that
   a rename is an afternoon. Pinning the 3.31.4 release instead loses constrained decoding (§2).
   `@_cdecl`'s *"not very well-defined semantics"* (§1.3) is the same kind of risk, bounded by the
   ABI using nothing but C scalars, C strings and opaque pointers.
2. **First load is long.** 2.26 GB read from disk plus two grammar compiles; on a cold disk this is
   many seconds, and it must never sit under the 30-second question timeout — §4.1 separates the
   loading state. The model stays loaded until the settings change or napkin exits (no keep-alive
   window, unlike Ollama's 5 minutes), which is memory held (next item).
3. **Memory on an 8 GB Mac.** Weights 2.26 GB, plus the KV cache for a 6,000-word pack, plus MLX's
   buffer cache. No page read gives a figure for the working set; the bridge reports Metal's
   `recommendedMaxWorkingSetSize` and `hw.memsize`, `GPU.set(cacheLimit:)` exists to bound the
   cache (`GPU+Metal.swift`), and the dialog keeps napkin's own "too big for this machine" rule,
   labelled as napkin's. The smoke prints peak memory (`GPU.peakMemory`) so the number becomes
   known on Marc's 24 GiB machine first.
4. **Greedy proposals.** The guided loop is argmax (§2); Qwen's card warns off greedy decoding for
   thinking mode. If the eval shows worse proposals than Ollama's schema-constrained sampling, the
   upgrade is a `LogitProcessor` over xgrammar's mask inside the ordinary sampler — more bridge
   code, not a design change.
5. **Toolchain lockstep.** Xcode ≥ 26.4 for Swift 6.3, the Metal Toolchain as a separate ~700 MB
   download (secondary figure), and `(experimentalCGen)` in mlx-swift's tools-version line whose
   meaning is unverified. The spike is where these stop being risks; the script refuses early with
   the exact command to run.
6. **A native crash takes the drawing with it.** This is the argument DESIGN.md §11 made for the
   solver, now accepted for the runtime by Marc's decision. Bounded: nothing native runs until the
   person chooses MLX and asks; every Swift error is a status; what is left is a Metal or MLX abort.
   Whether napkin should write a recovery copy of an unsaved design before the first load is a
   scope question (Decision 9).
7. **Cancellation is per token.** A cancel lands at the next token, and a load cannot be interrupted
   at all (§1.4). The note's Escape therefore hides the thinking line at once while the bridge
   finishes its current token in the background; slice B's generation counter (§15 item 5) already
   guards the note from a late reply.
8. **CI cannot prove the bridge works**, only that it builds, exports and is signed (§7.4). A broken
   Metal path would be found by the smoke on a Mac, not by a green check. Landing the bridge slice
   therefore requires the smoke's output in the landing report — a process rule, not a gate.
9. **Build time and artifact size** of MLX + xgrammar are unverified; if the CI job proves slow,
   its trigger narrows to `native/**` and releases, with the artifact cached by the `Package.resolved`
   hash.
10. **Ad-hoc signature on the dylib** (§3.5) and **Metal under the runner's test process** (§7.1)
    are unverified until the spike; both have a stated fallback.
11. **The Hub moves.** A pinned commit is immutable, but a repo can be deleted or gated; the
    download then fails with the server's status in the sheet, and *Choose…* still takes a folder
    from anywhere. napkin never falls back to `main`.

---

## 9. Slices

Each lands alone on `main` through `tools/scripts/gate.sh` (A's Swift build is run by hand and its
output pasted into the landing report, since the gate has no Swift). Files are disjoint except the
recurring collision points (`napkin.sln`, `Directory.Build.props`, `ratchet/baseline.json`,
`PlannedFeatures.g.cs`) and `AssistantWindow.axaml(.cs)`, which C and D touch in sequence. **None
starts until Marc signs this note off.**

| Slice | What | Model | Files | Depends on | Issue |
|---|---|---|---|---|---|
| **A** | The bridge: `native/NapkinMlx` (Package.swift with the §1.1 pins, `Package.resolved`, `napkin_mlx.h`, the twelve functions of §1.4, the tokenizer adapter, guided generation for a non-null schema with the two schemas pre-warmed, cancellation on both paths, the Swift tests of §7.1), `tools/scripts/build-mlx.sh`, `.gitignore` for `out/`; **the spike report** in the landing report: the exact build products and metallib path, `codesign -dvv` of the dylib, `nm -gU`, dylib and metallib sizes, build time, load time and `GPU.peakMemory` for Qwen3-4B-4bit on Marc's Mac, a text answer and a schema-constrained sketch proposal printed | **Opus** — Swift concurrency across a C boundary, a cancel that must reach both loops, Metal library placement; a mistake looks like a hang | `native/NapkinMlx/**`, `tools/scripts/build-mlx.sh`, `.gitignore` | sign-off; Marc runs `xcodebuild -downloadComponent MetalToolchain` once | #239 |
| **B** | `Napkin.Assistant.Mlx`: `INativeMlx`, `NativeMlx` (`LibraryImport`, the resolver, `[ExcludeFromCodeCoverage]`), `CancelFlag`, `MlxModel`, `ModelFolder`, `MlxAvailability`, `FakeNativeMlx` in the module (the `ScriptedModel` precedent: the GUI suite needs it); `tests/Napkin.Assistant.Mlx.Tests` §7.2 except the download; `napkin.sln`; the floor; `assistant mlx-smoke` in `Napkin.Tools`; `Napkin.App.csproj`'s conditional `native/` items (§3.1) | **Opus** — the marshalling and the thread/timeout rules of §4.1 | `src/Napkin.Assistant.Mlx/**`, `tests/Napkin.Assistant.Mlx.Tests/**`, `tools/Napkin.Tools/AssistantSmoke.cs`, `src/Napkin.App/Napkin.App.csproj`, `napkin.sln`, `ratchet/baseline.json`, `features/assistant.json` | A (for the smoke to mean anything; B's tests need only the fake) | #240 |
| **C** | Settings v4 (`Mlx`, `ModelFolder`), `FromSettings`'s `Mlx` case with the injected seam, the dialog's third radio, folder picker, `ModelFolder` line, memory line from `device`, Test with the loading line, the disabled state with its sentence, `Guidance` lines with sources; `GUI-AST-07`; `docs/assistant.md`'s MLX paragraph (if slice G's page exists by then) | Sonnet — well specified; the dialog already has the shape | `src/Napkin.App/Settings/UserSettings.cs`, `SettingsStore.cs`, `AssistantModels.cs`, `AssistantWindow.axaml(.cs)`, `MainWindow.Assistant.cs` (the seam property), `tests/Napkin.App.GuiTests/Workflows/AssistantWorkflows.cs`, `tests/…/SettingsStoreTests.cs` | B | #241 |
| **D** | The consented download: `ModelCatalog` (the three entries, every hash from this task's report), `ModelDownload` (allow-listed redirects, resume, cancel, SHA-256, size, the `.downloading` rename), the consent sheet and progress in the dialog, *Downloaded* state; tests §7.2's download rows; `GUI-AST-08` | **Opus** — consent and integrity are the whole point; a silent fetch or an unchecked file is the failure this slice exists to make impossible | `src/Napkin.Assistant.Mlx/ModelCatalog.cs`, `ModelDownload.cs`, `AssistantWindow.axaml(.cs)`, `DownloadSheet.axaml(.cs)`, tests, workflows | C | #242 |
| **E** | Packaging and CI: the `mlx-bridge` job in `ci.yml`, `release.yml`'s artifact download into `native/NapkinMlx/out/` for `osx-arm64`, the signature check on the dylib, `SOURCE.txt`'s line, `docs/third-party-notices.md`'s section (reading `metal-cpp`, `fmt`, `json` and dlpack's licences first — §11), `native/NOTICES.txt`; `assistant eval --mlx`; `ReleaseWorkflowTests.cs` updated to assert the check and still refuse a signing step | Sonnet, with a `review/fable` pass on the notices | `.github/workflows/ci.yml`, `release.yml`, `docs/third-party-notices.md`, `native/NOTICES.txt`, `tests/Napkin.Tools.Tests/ReleaseWorkflowTests.cs`, `tools/Napkin.Tools/AssistantEval.cs` | A, B; #235 for the eval | #243 |

**Versioning:** each slice bumps the minor. **Ratchet:** B adds `Napkin.Assistant.Mlx`'s floor; C
and D raise the workflow count. **DESIGN.md §11's one-line change** (the rule lifted for the LLM
runtime only) is the orchestrator's, recorded when A lands.

---

## 10. Try it (for Marc, after slice A)

1. **Once:** `xcodebuild -downloadComponent MetalToolchain` (Apple's command, quoted by
   `xcodebuild` itself on this machine today; the runner-images issue calls it about 700 MB).
2. `tools/scripts/build-mlx.sh` → `native/NapkinMlx/out/libNapkinMlx.dylib` and `mlx.metallib`,
   sizes printed, `codesign -dvv` printed.
3. `dotnet run --project tools/Napkin.Tools -- assistant mlx-smoke --model <a folder>` — with a
   folder you already have, or after step 5 the downloaded one — prints device, load time, peak
   memory, "ok", and a sketch proposal as JSON.
4. After B–D: `dotnet run --project src/Napkin.App` → **Assistant → Where the model runs…** →
   *In napkin, on this Mac (MLX)* → **Download Qwen3-4B-4bit…** → read the sheet, tick, **Download**
   → **Test** → **Use these settings** → open a sample, Ctrl/Cmd+Shift+A, ask.
5. Or, without napkin's download, put any `mlx-community` folder anywhere and **Choose…** it.

---

## 11. Unverified — everything this note could not read

- What `swift-tools-version: 6.3;(experimentalCGen)` (mlx-swift 0.31.6) demands beyond Swift 6.3.
- Per-Xcode Swift versions and macOS requirements: from xcodereleases.com, a secondary source;
  Apple's own note read here says only *"Xcode 26 requires … macOS Sequoia 15.6 or later"*.
- Whether today's `macos-26-arm64` runner image has the Metal Toolchain installed (the issue says
  it would; the README does not list it; the workflow checks and downloads).
- The exact build products of `xcodebuild` for a SwiftPM dynamic library: the dylib's path, and the
  metallib's name and location (`mlx-swift_Cmlx.bundle/…/default.metallib` by MLX's own lookup
  code, but not seen built).
- Whether the built dylib carries the toolchain's ad-hoc signature (§3.5).
- Whether a test process linking MLX starts on a GitHub-hosted runner with no Metal device (§7.1).
- Whether mlx-swift's Metal build runs on an Intel Mac (§3.4) — napkin does not try.
- Build time, dylib size, metallib size, first-load time, peak memory (§8.3, §8.9) — the spike.
- Which JSON-Schema keywords xgrammar v0.1.30 supports (§2) — the spike asserts napkin's two
  schemas compile.
- The vendored dlpack header's licence (no text in the header; upstream is `dmlc/dlpack`, not read);
  `metal-cpp`, `fmt`, `json` vendored in mlx-swift's `Cmlx` (listed, not read) — slice E reads all
  four before writing the notices.
- `MLX_VERSION` at mlx-swift tag 0.31.6 (main says 0.32.0).
- Whether swift-syntax stays uncompiled when `MLXHuggingFace` is not linked (§1.1) — the spike's
  build log.
- The Metal Toolchain download's size (~700 MB is the issue author's figure).
- A memory figure for the working set of any model under MLX — no page states one; napkin's rule
  stays napkin's, and the smoke measures.

---

## 12. Decisions for Marc

In plain words, each with the default I recommend; "use the recommended defaults" is a complete
answer.

1. **Which mlx-swift-lm to pin.** The latest release (3.31.4) has no constrained decoding; `main`
   does (`MLXGuidedGeneration`, xgrammar). *Recommended:* **pin `main` at commit `ee673d6a…`**
   (2026-09-22), re-pinned deliberately and re-smoked each time; proposals then match the schema by
   construction. Alternative: pin 3.31.4 and prompt-parse-retry (§2's fallback) — no `main` churn,
   weaker proposals, until the next release ships the library.
2. **The Swift build stays out of `dotnet build` and the gate.** One script builds it; the app
   includes the output when present; a separate CI job builds and uploads it; the release job
   downloads it for `osx-arm64`. *Recommended:* yes. Alternative: an MSBuild target that runs
   `xcodebuild` on a Mac — every `dotnet build` on a Mac then needs Xcode and the Metal Toolchain,
   and the gate slows by the bridge's build time.
3. **Apple silicon only; an Intel Mac keeps the loopback runtime.** *Recommended:* yes — MLX's
   own requirement, and nothing read says otherwise.
4. **The default model.** *Recommended:* **`mlx-community/Qwen3-4B-4bit`** (the signed-off model,
   Apache-2.0, 2.28 GB, thinking off through the template flag). Alternative:
   `Qwen3-4B-Instruct-2507-4bit` (never thinks, newer, same size, same licence) — the switch
   §16.1 already asked about. Phi-4-mini-instruct-4bit (MIT, 2.18 GB) stays in the catalog as the
   other licence.
5. **Where downloaded models live.** *Recommended:* `~/Library/Application Support/napkin/models/`,
   one folder per repo and commit, deletable in Finder. Alternative: ask for a folder each time.
6. **The P/Invoke class is excluded from the coverage ratchet** by attribute, with the reason in
   the code; everything else in the assembly has a floor. *Recommended:* yes — the alternative is a
   stub native library compiled in the test run, which puts a C compiler in the gate for a dozen
   lines.
7. **Settings 3 → 4.** *Recommended:* bump, as the task says, so the number says what a file may
   hold. Alternative (slice C's precedent, §16.2): add the two members without a bump; a v3 file
   then keeps its theme and paper.
8. **Greedy proposals under the schema** (§2, risk 4): accept for M14 and let the eval measure.
   *Recommended:* yes. Alternative: write the mask processor for the ordinary sampler in slice A —
   more bridge code before any evidence it is needed.
9. **An in-process crash can take an unsaved design.** *Recommended:* accept for the beta, as your
   decision implies; the note's whereabouts line says *"In napkin"* so a person knows where it runs.
   Alternative: a recovery copy written before the first load — a small, separate slice if you
   want it.
10. **Cancellation lands at the next token, and a load cannot be interrupted.** *Recommended:*
    accept; the note hides its thinking line at once and ignores the late reply.
11. **The consent sheet shows every file's SHA-256.** *Recommended:* yes, in mono, scrollable — the
    person can compare it with the Hub's page. Alternative: sizes only, hashes in a tooltip.
12. **The name in the dialog.** *Recommended:* *"In napkin, on this Mac (MLX)"* beside *"A program
    on this machine"*. Alternative: *"Built in"*.
