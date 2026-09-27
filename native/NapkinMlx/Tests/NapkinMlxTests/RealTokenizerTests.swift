// Opt-in: a real model's tokenizer, no weights. Skipped unless NAPKIN_MLX_TOKENIZER_DIR names a
// folder holding a model's config.json, tokenizer.json and tokenizer_config.json (for the spike:
// mlx-community/Qwen3-4B-4bit's three files at the pinned commit, 11.4 MB, hashes in
// mlx-runtime.md §6.1). Prints what load spends on guided generation before any weight is read.
//
//   NAPKIN_MLX_TOKENIZER_DIR=<folder> xcrun xctest -XCTest RealTokenizerTests \
//     native/NapkinMlx/.derived/Build/Products/Debug/NapkinMlxTests.xctest

import Foundation
import MLXGuidedGeneration
import MLXLMCommon
import NapkinMlxSchemas
import XCTest

@testable import NapkinMlx

final class RealTokenizerTests: XCTestCase {
    private func folder() throws -> URL {
        guard let path = ProcessInfo.processInfo.environment["NAPKIN_MLX_TOKENIZER_DIR"] else {
            throw XCTSkip("NAPKIN_MLX_TOKENIZER_DIR is not set")
        }
        return URL(fileURLWithPath: path, isDirectory: true)
    }

    private func milliseconds(_ body: () throws -> Void) rethrows -> Double {
        let started = Date()
        try body()
        return Date().timeIntervalSince(started) * 1000
    }

    func testWhatLoadSpendsOnGuidedGenerationAndTheThinkingFlag() throws {
        let folder = try folder()
        var tokenizer: (any MLXLMCommon.Tokenizer)!
        let loadMs = try milliseconds {
            tokenizer = try runBlocking(cancel: .never) {
                try await TransformersTokenizerLoader().load(from: folder)
            }.get()
        }
        print("real tokenizer: loaded in \(String(format: "%.0f", loadMs)) ms")

        var engine: GuidedEngine!
        let engineMs = try milliseconds { engine = try GuidedEngine(tokenizer: tokenizer) }
        print("real tokenizer: \(engine.summary) — total \(String(format: "%.0f", engineMs)) ms")

        for (name, text) in NapkinSchemas.all {
            let runs = try (0 ..< 3).map { _ in
                try milliseconds {
                    _ = try GuidedEngine.compile(
                        text, grammar: engine.grammarTokenizer, host: tokenizer)
                }
            }
            let budget = engine.budget(for: text, maxTokens: 2048)
            print(
                "real tokenizer: \(name) schema compiles in \(runs.map { String(format: "%.0f ms", $0) }.joined(separator: ", ")); reserves soft \(budget.completionReserve), hard \(budget.hardReserve) of 2048"
            )
        }
        let prewarmMs = milliseconds { engine.prewarm(NapkinSchemas.all) }
        print("real tokenizer: prewarm \(String(format: "%.0f", prewarmMs)) ms — \(engine.summary)")

        // The model's own chat template, rendered through swift-jinja with enable_thinking false:
        // the empty think block of mlx-runtime.md §5.2 closes the prompt.
        let messages: [[String: any Sendable]] = [
            ["role": "system", "content": "S"], ["role": "user", "content": "U"],
        ]
        let off = try tokenizer.applyChatTemplate(
            messages: messages, tools: nil, additionalContext: ["enable_thinking": false])
        let on = try tokenizer.applyChatTemplate(
            messages: messages, tools: nil, additionalContext: ["enable_thinking": true])
        let offText = tokenizer.decode(tokenIds: off, skipSpecialTokens: false)
        let onText = tokenizer.decode(tokenIds: on, skipSpecialTokens: false)
        print("real tokenizer: enable_thinking false renders \(offText.debugDescription)")
        XCTAssertTrue(offText.hasSuffix("<|im_start|>assistant\n<think>\n\n</think>\n\n"), offText)
        XCTAssertTrue(onText.hasSuffix("<|im_start|>assistant\n"), onText)
        XCTAssertEqual(tokenizer.eosToken, "<|im_end|>")
    }
}
