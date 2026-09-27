// The tokenizer adapter over a fixture tokenizer (not a model's: 256 byte tokens, one merge, Qwen's
// special tokens and a Qwen-shaped chat template), and napkin's two schemas compiled by the
// vendored xgrammar over it — pure CPU, no GPU, no weights (mlx-runtime.md §7.1, §2).

import CNapkinMlx
import Foundation
import Metal
import MLX
import MLXGuidedGeneration
import MLXLMCommon
import NapkinMlxSchemas
import XCTest

@testable import NapkinMlx

private let fixtures = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
    .appendingPathComponent("Fixtures", isDirectory: true)
private let tinyTokenizer = fixtures.appendingPathComponent("tiny-tokenizer", isDirectory: true)

private func loadTokenizer() throws -> any MLXLMCommon.Tokenizer {
    let outcome = runBlocking(cancel: .never) {
        try await TransformersTokenizerLoader().load(from: tinyTokenizer)
    }
    return try outcome.get()
}

final class TokenizerAdapterTests: XCTestCase {
    func testEncodeDecodeRoundTripsThroughTheAdapter() throws {
        let tokenizer = try loadTokenizer()
        let text = "Top: 4'-0\" × 3/4\" at (0, 16)"
        let ids = tokenizer.encode(text: text, addSpecialTokens: false)
        XCTAssertFalse(ids.isEmpty)
        XCTAssertEqual(tokenizer.decode(tokenIds: ids, skipSpecialTokens: false), text)
        XCTAssertEqual(tokenizer.encode(text: "ok", addSpecialTokens: false), [256], "the one merge applies")
    }

    func testSpecialTokensAndIds() throws {
        let tokenizer = try loadTokenizer()
        XCTAssertEqual(tokenizer.eosToken, "<|im_end|>")
        XCTAssertEqual(tokenizer.eosTokenId, 259)
        XCTAssertEqual(tokenizer.convertTokenToId("<|im_start|>"), 258)
        XCTAssertEqual(tokenizer.convertIdToToken(256), "ok")
        XCTAssertNil(tokenizer.bosToken)
    }

    func testEnableThinkingReachesTheChatTemplateAsABool() throws {
        let tokenizer = try loadTokenizer()
        let messages: [[String: any Sendable]] = [
            ["role": "system", "content": "Be brief."], ["role": "user", "content": "Hi"],
        ]
        let off = try tokenizer.applyChatTemplate(
            messages: messages, tools: nil, additionalContext: ["enable_thinking": false])
        let on = try tokenizer.applyChatTemplate(
            messages: messages, tools: nil, additionalContext: ["enable_thinking": true])
        XCTAssertEqual(
            tokenizer.decode(tokenIds: off, skipSpecialTokens: false),
            "<|im_start|>system\nBe brief.<|im_end|>\n<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n"
        )
        XCTAssertEqual(
            tokenizer.decode(tokenIds: on, skipSpecialTokens: false),
            "<|im_start|>system\nBe brief.<|im_end|>\n<|im_start|>user\nHi<|im_end|>\n<|im_start|>assistant\n"
        )
    }

    func testAFolderWithoutTheTokenizerIsAnErrorNotACrash() throws {
        let empty = try temporaryFolder()
        let outcome = runBlocking(cancel: .never) {
            try await TransformersTokenizerLoader().load(from: empty)
        }
        guard case .failure = outcome else { return XCTFail("expected a failure") }
    }
}

final class GrammarTests: XCTestCase {
    private func grammar() throws -> (GrammarTokenizer, VocabType, any MLXLMCommon.Tokenizer) {
        let tokenizer = try loadTokenizer()
        let (grammar, vocabType) = try GuidedEngine.grammarTokenizer(for: tokenizer)
        return (grammar, vocabType, tokenizer)
    }

    func testTheFixtureIsAByteLevelVocabulary() throws {
        let (grammar, vocabType, _) = try grammar()
        XCTAssertEqual(vocabType, .byteLevel)
        XCTAssertEqual(grammar.vocabSize, 262)
    }

    func testBothNapkinSchemasCompile() throws {
        let (grammar, _, tokenizer) = try grammar()
        for (name, text) in NapkinSchemas.all {
            XCTAssertNoThrow(try GuidedEngine.compile(text, grammar: grammar, host: tokenizer), name)
        }
    }

    func testTheSchemasAreJsonObjectsWithRequiredMembers() throws {
        for (name, text) in NapkinSchemas.all {
            let object = try JSONSerialization.jsonObject(with: Data(text.utf8)) as? [String: Any]
            XCTAssertEqual(object?["type"] as? String, "object", name)
            XCTAssertNotNil(object?["required"] as? [String], name)
            XCTAssertEqual(object?["additionalProperties"] as? Bool, false, name)
        }
    }

    func testANonSchemaIsAnErrorSentence() throws {
        let (grammar, _, tokenizer) = try grammar()
        XCTAssertThrowsError(try GuidedEngine.compile(#"{"type": 42}"#, grammar: grammar, host: tokenizer)) {
            guard case BridgeError.failed(let sentence) = $0 else { return XCTFail("\($0)") }
            XCTAssertTrue(sentence.hasPrefix("The JSON schema did not compile into a grammar: "), sentence)
        }
    }

    func testCloneIsUnsupportedAtThePinnedXgrammar() throws {
        // xgrammar v0.1.30 has no GrammarMatcher::Fork (shim.cc). If a re-pin makes this fail,
        // clone() works: the bridge then clones per request (Guided.swift) — update the note.
        let (grammar, _, tokenizer) = try grammar()
        let constraint = try GuidedEngine.compile(NapkinSchemas.sketch, grammar: grammar, host: tokenizer)
        XCTAssertThrowsError(try constraint.clone()) {
            guard case GrammarError.forkFailed = $0 else { return XCTFail("\($0)") }
        }
    }

    /// Feeds a document token by token through a matcher without fast-forward; true when every
    /// token is accepted and the grammar can then stop.
    private func accepts(_ document: String, schema: String) throws -> Bool {
        let (grammar, _, tokenizer) = try grammar()
        let constraint = try GrammarConstraint(tokenizer: grammar, jsonSchema: schema)
        for token in tokenizer.encode(text: document, addSpecialTokens: false) {
            do {
                _ = try constraint.commitToken(Int32(token))
            } catch {
                return false
            }
        }
        let mask = try constraint.computeMask()
        let eos = tokenizer.eosTokenId!
        let eosAllowed = (mask.mask[eos / 32] >> (eos % 32)) & 1 == 1
        return eosAllowed || mask.isTerminated
    }

    func testTheSketchSchemaAcceptsTheNotesExampleAndRefusesAnUnknownMember() throws {
        // llm-assistant.md §4.4's example document.
        let example =
            #"{"parts":[{"name":"Top","width":"4'-0\"","height":"2\"","depth":"3/4\"","x":"0","y":"1'-4\"","quantity":1}],"note":"A plank bench: top on two legs with a stretcher between."}"#
        XCTAssertTrue(try accepts(example, schema: NapkinSchemas.sketch))
        let extra =
            #"{"parts":[{"name":"Top","width":"4'-0\"","height":"2\"","depth":"3/4\"","x":"0","y":"0","quantity":1,"stock":"2x4"}],"note":""}"#
        XCTAssertFalse(try accepts(extra, schema: NapkinSchemas.sketch))
        XCTAssertFalse(try accepts(#"{"parts":[]}"#, schema: NapkinSchemas.sketch), "note is required")
    }

    func testTheEditSchemaAcceptsEachOfTheSixEditsAndNothingElse() throws {
        let edits = [
            #"{"edit":"resize","part":"[1]","dimension":"length","length":"1'-6\""}"#,
            #"{"edit":"move","part":"Leg","x":"0","y":"2'-0\""}"#,
            #"{"edit":"rename","part":"[2]","name":"Seat"}"#,
            #"{"edit":"stock","part":"[2]","stock":"2x12"}"#,
            #"{"edit":"quantity","part":"Leg","quantity":4}"#,
            #"{"edit":"remove","part":"[3]"}"#,
        ]
        XCTAssertTrue(try accepts(#"{"edits":[\#(edits.joined(separator: ","))]}"#, schema: NapkinSchemas.edit))
        XCTAssertFalse(
            try accepts(#"{"edits":[{"edit":"joint","part":"[1]"}]}"#, schema: NapkinSchemas.edit),
            "not one of the six")
        XCTAssertFalse(
            try accepts(#"{"edits":[{"edit":"resize","part":"[1]","dimension":"height","length":"1\""}]}"#, schema: NapkinSchemas.edit),
            "a dimension outside length/width/thickness")
    }
}

final class MlxErrorTests: XCTestCase {
    func testAnMlxErrorInsideWithErrorIsAThrowNotAnAbort() throws {
        // mlx-swift's default handler is fatalError; the bridge wraps every MLX call in withError.
        // Skipped without Metal: MLX 0.31's load_device() aborts on an empty device list, which is
        // why init probes Metal before touching MLX.
        try XCTSkipIf(MTLCreateSystemDefaultDevice() == nil, "no Metal device")
        XCTAssertThrowsError(
            try withError { error in
                let sum = MLXArray(0 ..< 10, [2, 5]) + MLXArray(0 ..< 15, [3, 5])
                try error.check()
                return sum
            }
        ) { error in
            guard case MLXError.caught(let message) = error else { return XCTFail("\(error)") }
            XCTAssertFalse(message.isEmpty)
        }
    }
}

final class RevisionTests: XCTestCase {
    func testTheReportedRevisionIsPackageResolvedsPin() throws {
        let resolved = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("Package.resolved")
        let object = try JSONSerialization.jsonObject(with: Data(contentsOf: resolved)) as? [String: Any]
        let pins = object?["pins"] as? [[String: Any]] ?? []
        let pin = pins.first { ($0["identity"] as? String) == "mlx-swift-lm" }
        let state = pin?["state"] as? [String: Any]
        XCTAssertEqual(state?["revision"] as? String, Build.mlxSwiftLmRevision)
        XCTAssertLessThan(Build.mlxSwiftLmRevision.utf8.count, 48, "fits napkin_mlx_device_info's field")
    }
}
