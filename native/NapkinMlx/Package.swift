// swift-tools-version: 6.2
//
// napkin's MLX bridge: one dynamic library, libNapkinMlx.dylib, exposing the C ABI of
// include/napkin_mlx.h (docs/design/mlx-runtime.md §1). Built by tools/scripts/build-mlx.sh with
// xcodebuild — SwiftPM's command line cannot compile MLX's Metal shaders (mlx-swift README).
// Never built by `dotnet build` or the gate.
//
// Pins (mlx-runtime.md §1.1, Decision 1, signed off 2026-09-27):
//   mlx-swift-lm      main @ ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb (has MLXGuidedGeneration)
//   mlx-swift         0.31.6 (what mlx-swift-lm's `.upToNextMinor(from: "0.31.6")` resolves to)
//   swift-transformers 1.3.4 (Tokenizers only; napkin never uses its Hub downloader)
// Package.resolved is committed; every transitive pin is in it.

import PackageDescription

/// Keep in step with `Build.mlxSwiftLmRevision` (Sources/NapkinMlx/Errors.swift); a test compares
/// that constant with Package.resolved's pin.
let mlxSwiftLmRevision = "ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb"

let package = Package(
    name: "NapkinMlx",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "NapkinMlx", type: .dynamic, targets: ["NapkinMlx"]),
        // The spike harness: dlopens out/libNapkinMlx.dylib exactly as napkin will (§10 of the
        // note), so it tests the real library and MLX's colocated-metallib lookup, not a copy.
        .executable(name: "napkin-mlx-spike", targets: ["NapkinMlxSpike"]),
    ],
    dependencies: [
        .package(url: "https://github.com/ml-explore/mlx-swift-lm", revision: mlxSwiftLmRevision),
        .package(url: "https://github.com/ml-explore/mlx-swift", exact: "0.31.6"),
        .package(url: "https://github.com/huggingface/swift-transformers", exact: "1.3.4"),
    ],
    targets: [
        // The header as real C: the bridge and the harness import the same declarations.
        .target(
            name: "CNapkinMlx",
            path: "include",
            sources: ["napkin_mlx.c"],
            publicHeadersPath: "."
        ),
        // napkin's two proposal schemas, shared by the bridge (pre-warm) and the spike.
        .target(
            name: "NapkinMlxSchemas",
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .target(
            name: "NapkinMlx",
            dependencies: [
                "CNapkinMlx",
                "NapkinMlxSchemas",
                // MLXLLM registers the LLM architectures (qwen3, phi3, ...); the bridge calls
                // LLMModelFactory directly rather than through the NSClassFromString registry.
                .product(name: "MLXLLM", package: "mlx-swift-lm"),
                .product(name: "MLXLMCommon", package: "mlx-swift-lm"),
                .product(name: "MLXGuidedGeneration", package: "mlx-swift-lm"),
                .product(name: "MLX", package: "mlx-swift"),
                .product(name: "Tokenizers", package: "swift-transformers"),
            ],
            swiftSettings: [
                // The C boundary (process-wide state, `any Tokenizer`, ModelContext) is guarded
                // by locks the Swift 6 checker cannot see; Swift 5 mode keeps the diagnostics
                // to what the compiler can actually prove.
                .swiftLanguageMode(.v5)
            ],
            linkerSettings: [
                // With a macOS 14 deployment target the dylib links
                // @rpath/libswiftCompatibilitySpan.dylib. macOS 26 has it in /usr/lib/swift (the
                // dylib's only other rpath); on macOS 14–15 dyld finds the copy build-mlx.sh puts
                // beside the dylib, through this rpath.
                .unsafeFlags(["-Xlinker", "-rpath", "-Xlinker", "@loader_path"])
            ]
        ),
        .executableTarget(
            name: "NapkinMlxSpike",
            dependencies: ["CNapkinMlx", "NapkinMlxSchemas"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .testTarget(
            name: "NapkinMlxTests",
            dependencies: [
                "NapkinMlx",
                "CNapkinMlx",
                "NapkinMlxSchemas",
                .product(name: "MLX", package: "mlx-swift"),
                .product(name: "MLXLMCommon", package: "mlx-swift-lm"),
                .product(name: "MLXGuidedGeneration", package: "mlx-swift-lm"),
                .product(name: "Tokenizers", package: "swift-transformers"),
            ],
            // Fixtures are read through #filePath, not bundled.
            exclude: ["Fixtures"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
    ]
)
