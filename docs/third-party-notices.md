# Third-party notices

napkin's own code is AGPL-3.0 ([`LICENSE`](../LICENSE)). It bundles the following third-party
material. Each keeps its own license, and each license's notice travels with every copy.

## IBM Plex (fonts)

`src/Napkin.App/Assets/Fonts/` bundles IBM Plex Mono (Regular, Medium), IBM Plex Serif (Regular,
Italic) and IBM Plex Sans (Regular, Medium, SemiBold), unmodified, from IBM's official releases
(`github.com/IBM/plex`: plex-mono 2.5.0, plex-serif 2.0.0, plex-sans 1.1.0).

- **License:** SIL Open Font License 1.1. Copyright © 2017 IBM Corp., with Reserved Font Name
  "Plex". The full text is in `LICENSE-IBM-Plex-mono.txt`, `-serif.txt` and `-sans.txt` beside the
  fonts, and the build copies those three files into a `licenses/` folder next to the executable.
- **What it lets us do:** use, embed and bundle the fonts in software, including software that is
  sold; not sell the font files by themselves; keep any derivative under the OFL and off the name
  "Plex". napkin ships them unmodified.
- **Where they come from:** the design system repository, `marctjones/skepticalengineering-design`
  (`fonts/`), which carries the same license files.

## Skeptical Engineering design system

`src/Napkin.App/Theme/` and `Assets/napkin*` are vendored from
`marctjones/skepticalengineering-design` (the commit is in each file's first line). They are the
same author's work and follow napkin's license.

## The MLX bridge (macOS arm64 builds only)

The `osx-arm64` build ships napkin's MLX bridge in a `native/` folder beside the executable
([`docs/design/mlx-runtime.md`](./design/mlx-runtime.md)): `libNapkinMlx.dylib`, `mlx.metallib` and
`libswiftCompatibilitySpan.dylib`. The `osx-x64` and Windows builds carry none of it. napkin's own
bridge code in `native/NapkinMlx` is AGPL-3.0 like the rest of napkin. Everything else in those
three files is third-party, listed below with the version `native/NapkinMlx/Package.resolved` pins
and the licence **as read from the component's own licence file at that revision on 2026-09-27**.
The licence texts themselves travel in the zip as `native/NOTICES.txt` (from
[`native/NOTICES.txt`](../native/NOTICES.txt) in this repository), because the MIT, BSD and Apache
licences ask that their notices go with every binary copy.

`licenses check` reads NuGet packages only, so this table is kept by hand. Re-pinning a package in
`Package.resolved` means re-reading its licence and updating this table and `native/NOTICES.txt` in
the same change.

### Compiled into `libNapkinMlx.dylib` (and MLX's kernels in `mlx.metallib`)

Linked, as the bridge's Release link list names them: `MLXLLM`, `MLXLMCommon`,
`MLXGuidedGeneration`, `MLXCXGrammar`, `MLX`, `Cmlx`, `MLXNN`, `MLXOptimizers`, `Numerics`,
`RealModule`, `ComplexModule`, `Tokenizers`, `Hub`, `Jinja`, `HuggingFace`, `EventSource`,
`Crypto`, `OrderedCollections`, `yyjson`.

| Component | Version / revision | Licence | Read from |
|---|---|---|---|
| [mlx-swift-lm](https://github.com/ml-explore/mlx-swift-lm) | `main` @ `ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb` | MIT — "Copyright (c) 2024 ml-explore" | `LICENSE` |
| [XGrammar](https://github.com/mlc-ai/xgrammar), vendored in mlx-swift-lm (`Libraries/MLXCXGrammar/xgrammar`) | v0.1.30 (its `VERSION`) | Apache-2.0; `NOTICE`: "XGrammar / Copyright (c) 2024 by XGrammar Contributors" | `xgrammar/LICENSE`, `xgrammar/NOTICE`; every source header in `MLXCXGrammar` names only "Contributors", picojson or DLPack |
| picojson, vendored in XGrammar | as vendored | BSD-2-Clause — "Copyright 2009-2010 Cybozu Labs, Inc. / Copyright 2011-2014 Kazuho Oku" | `3rdparty/picojson/picojson.h` (the licence is in the header) |
| DLPack header, vendored in XGrammar | as vendored | Apache-2.0 — the header says only "Copyright (c) 2017 by Contributors"; upstream [dmlc/dlpack](https://github.com/dmlc/dlpack) `LICENSE` (main @ `94485e2`): Apache-2.0, "Copyright 2017 by Contributors" | `3rdparty/dlpack/include/dlpack/dlpack.h`; upstream `LICENSE` |
| [mlx-swift](https://github.com/ml-explore/mlx-swift) | 0.31.6 @ `0bb916c67f4b9e5c682cbe02a42c701c93ab5021` | MIT — "Copyright (c) 2023 ml-explore" | `LICENSE` |
| [MLX](https://github.com/ml-explore/mlx) (C++), mlx-swift's `Source/Cmlx/mlx` submodule | 0.31.1 @ `ce45c52505c8158ea48d2a54e8caae05efd86bfe` | MIT — "Copyright © 2023 Apple Inc." | `LICENSE`; its `ACKNOWLEDGMENTS.md` names two third-party works, the next two rows |
| PocketFFT, in MLX (`mlx/3rdparty/pocketfft.h`, used by the CPU FFT) | as vendored | BSD-3-Clause — "Copyright (C) 2010-2022 Max-Planck-Society", "Copyright (C) 2019-2020 Peter Bell", and for the odd-sized DCT-IV transforms Matteo Frigo and MIT | the header; MLX's `ACKNOWLEDGMENTS.md` |
| metal-cpp (Apple), vendored in mlx-swift (`Source/Cmlx/metal-cpp`) | Apple's `metal-cpp_macOS15_iOS18-beta.zip`, patched (`vendor-README.md`) | Apache-2.0 — "Copyright © 2024 Apple Inc." | `metal-cpp/LICENSE.txt` |
| [mlx-c](https://github.com/ml-explore/mlx-c), mlx-swift's `Source/Cmlx/mlx-c` submodule | @ `0726ca922fc902c4c61ef9c27d94132be418e945` | MIT — "Copyright (c) 2023 ml-explore" | `LICENSE` |
| [fmt](https://github.com/fmtlib/fmt), vendored in mlx-swift (`Source/Cmlx/fmt`) | 12.1.0 (`vendor-README.md`) | MIT with fmt's "Optional exception to the license" — "Copyright (c) 2012 - present, Victor Zverovich and {fmt} contributors" | `fmt/LICENSE` |
| [nlohmann/json](https://github.com/nlohmann/json), vendored in mlx-swift (`Source/Cmlx/json`, header only) | 3.11.3 (`vendor-README.md`) | MIT — "Copyright (c) 2013-2022 Niels Lohmann" | `json/LICENSE.MIT` |
| [swift-transformers](https://github.com/huggingface/swift-transformers) | 1.3.4 @ `c21fdcde390313a6d98d8e33a346f2c3486c3ab0` | Apache-2.0 — "Copyright 2022 Hugging Face SAS." | `LICENSE` |
| [swift-jinja](https://github.com/huggingface/swift-jinja) | 2.5.1 @ `4588064a20f3fc093c95f2f7d3359999bf30cae5` | Apache-2.0 — "Copyright 2022 Hugging Face SAS." | `LICENSE` |
| [swift-huggingface](https://github.com/huggingface/swift-huggingface) (linked through `Hub`; napkin never calls it) | 0.11.0 @ `f2f99991f2d7d8fdb3187e4fd539cd2facf5c13d` | Apache-2.0 — "Copyright 2025 Hugging Face SAS." | `LICENSE` |
| [EventSource](https://github.com/mattt/EventSource) | 1.5.1 @ `86b5096ac59ab46e66bd1f6377c604bc1dab0bc2` | MIT — "Copyright 2025 Mattt (https://mat.tt)" | `LICENSE.md` |
| [swift-crypto](https://github.com/apple/swift-crypto) (`Crypto` only; on macOS it defers to CryptoKit and its vendored BoringSSL is not compiled — `Package.swift` adds it only for Linux, Android, Windows, WASI and OpenBSD) | 4.5.2 @ `da9d28d69ebe3894b18376c8f2395c2f37b8448f` | Apache-2.0; `NOTICE.txt`: "Copyright 2019 The SwiftCrypto Project" | `LICENSE.txt`, `NOTICE.txt`, `Package.swift` |
| [swift-collections](https://github.com/apple/swift-collections) (`OrderedCollections`) | 1.7.1 @ `98ef3c98609a1e31b7e157b5b619579001a789d6` | Apache-2.0 with the Runtime Library Exception (SPDX `Apache-2.0 WITH Swift-exception`) — "Copyright (c) 2021 - 2026 Apple Inc. and the Swift project authors" | `LICENSE.txt`; a source header |
| [swift-numerics](https://github.com/apple/swift-numerics) | 1.1.1 @ `0c0290ff6b24942dadb83a929ffaaa1481df04a2` | Apache-2.0 with the Runtime Library Exception — "Copyright (c) 2019-2025 Apple Inc. and the Swift Numerics project authors" | `LICENSE.txt`; a source header |
| [yyjson](https://github.com/ibireme/yyjson) | 0.12.0 @ `8b4a38dc994a110abaec8a400615567bd996105f` | MIT — "Copyright (c) 2020 YaoYuan" | `LICENSE` |

### Beside it: `libswiftCompatibilitySpan.dylib` (the Swift runtime)

With its macOS 14 deployment target, the bridge links `@rpath/libswiftCompatibilitySpan.dylib`, the
Swift 6.2 back-deployment library for the standard library's `Span` types; macOS 26 and later have
their own copy in `/usr/lib/swift`, which the bridge's first rpath finds before the copy beside it.
`tools/scripts/build-mlx.sh` copies the one in the building Xcode's toolchain
(`usr/lib/swift-6.2/macosx/`), unmodified and still carrying Apple's signature.

- **Licence: Apache-2.0 with the Runtime Library Exception.** The library is built from
  `stdlib/toolchain/CompatibilitySpan` in [swiftlang/swift](https://github.com/swiftlang/swift)
  (its `CMakeLists.txt`: `swiftCompatibilitySpan`, `BACK_DEPLOYMENT_LIBRARY 6.2`, compiling
  `stdlib/public/core/Span/*.swift`; last changed at `56c13c6`, 2026-08-10), whose source headers
  read "Licensed under Apache License v2.0 with Runtime Library Exception", "See
  https://swift.org/LICENSE.txt for license information". That file
  ([`LICENSE.txt`](https://github.com/swiftlang/swift/blob/main/LICENSE.txt), read 2026-09-27) is
  the Apache License 2.0 followed by the exception; the GitHub API's licence field for the
  repository is `Apache-2.0`. Apache-2.0 is on `licenses/policy.json`'s allowlist (DESIGN.md §2.1).
- **The exception is not relied on.** It lifts the attribution duties only for portions "embedded
  into the binary product" as a result of compiling; this library ships as a separate file, so
  napkin gives the attribution anyway: the Apache text, the exception and the copyright line are in
  `native/NOTICES.txt`.
- **The bytes are Apple's build, from Xcode.** Xcode's own `Acknowledgments.pdf` lists "Apple Inc.
  and Swift project authors ( Swift )" as "Licensed under Apache License v2.0 with Runtime Library
  Exception", and the Xcode and Apple SDKs Agreement (§2.7, Xcode 27.0's `License.rtf`) excepts from
  its restrictions what "may be permitted by licensing terms governing use of open-sourced
  components … included with the Apple Software". Its §2.7 also says that, unless Apple permits it
  in writing, a licensee may not "redistribute, or sublicense the Apple Software … in whole or in
  part"; napkin reads the open-source carve-out as covering this open-source runtime library, which
  Apple's own tools copy into every app bundle that back-deploys `Span` (an Apple engineer in
  [apple/swift-collections#732](https://github.com/apple/swift-collections/issues/732#issuecomment-5823499281),
  2026-09-24: "For app targets, Xcode automatically takes care of copying this dylib into the
  correct location in the app bundle"; a secondary source, not licence evidence). Marc may want to
  read §2.7 himself; raising the bridge's floor to macOS 26 would remove the file (mlx-runtime.md
  §17).

### Resolved, not shipped

In `Package.resolved` but in neither file napkin ships (not in the link list above):
[swift-asn1](https://github.com/apple/swift-asn1) 1.7.3 @ `3b6410f7dee09eb33cdd26260c5fd47fda19b0e2`
(Apache-2.0, `LICENSE.txt` and `NOTICE.txt`; resolved for swift-crypto's `CryptoExtras`, which is
not built); [swift-argument-parser](https://github.com/apple/swift-argument-parser) 1.8.2 @
`6a52f3251125d74daf04fcbd5e6f08a75d074382` (Apache-2.0 with the Runtime Library Exception; built only
for mlx-swift's `CudaBuild` build-tool plugin); [swift-syntax](https://github.com/swiftlang/swift-syntax)
603.0.2 @ `79e4b74a295b6eb74a8b585e3a39d29e70c1dbd1` (Apache-2.0 with the Runtime Library Exception;
not compiled).
