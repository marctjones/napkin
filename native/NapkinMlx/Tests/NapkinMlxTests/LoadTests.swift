// napkin_mlx_load's refusals: pure file checks that run before init is required and before
// swift-transformers or MLX reads anything (mlx-runtime.md §7.1: "a load of a folder without
// config.json returns ERROR and the sentence names the file").

import CNapkinMlx
import Foundation
import XCTest

@testable import NapkinMlx

final class LoadTests: XCTestCase {
    /// A folder with everything mlx-swift-lm and swift-transformers read; the weights are one
    /// empty file, never read because every test here stops before loading.
    private func completeFolder() throws -> URL {
        let folder = try temporaryFolder()
        let files: [String: String] = [
            "config.json": #"{"model_type":"qwen3"}"#,
            "tokenizer.json": "{}",
            "tokenizer_config.json": #"{"tokenizer_class":"Qwen2Tokenizer"}"#,
            "model.safetensors": "",
        ]
        for (name, contents) in files {
            try contents.write(to: folder.appendingPathComponent(name), atomically: true, encoding: .utf8)
        }
        return folder
    }

    private func load(_ path: String?) -> (status: napkin_mlx_status, sentence: String?, model: OpaquePointer?) {
        var model: OpaquePointer? = OpaquePointer(bitPattern: 0x1)
        let (status, sentence) = withErrorSentence { napkin_mlx_load(path, nil, &model, $0) }
        return (status, sentence, model)
    }

    func testAMissingFileIsRefusedByName() throws {
        for name in ["config.json", "tokenizer.json", "tokenizer_config.json"] {
            let folder = try completeFolder()
            try FileManager.default.removeItem(at: folder.appendingPathComponent(name))
            let (status, sentence, model) = load(folder.path)
            XCTAssertEqual(status, NAPKIN_MLX_ERROR, name)
            XCTAssertEqual(sentence, "The folder \(folder.path) has no \(name).")
            XCTAssertNil(model, "out is NULL on failure")
        }
    }

    func testAFolderWithoutWeightsIsRefused() throws {
        let folder = try completeFolder()
        try FileManager.default.removeItem(at: folder.appendingPathComponent("model.safetensors"))
        XCTAssertEqual(load(folder.path).sentence, "The folder \(folder.path) has no .safetensors weights.")
    }

    func testATokenizerConfigWithoutATokenizerClassIsRefused() throws {
        // swift-transformers would otherwise reach Bundle.module, which aborts outside an app bundle.
        let folder = try completeFolder()
        try #"{"eos_token":"<|im_end|>"}"#.write(
            to: folder.appendingPathComponent("tokenizer_config.json"), atomically: true, encoding: .utf8)
        XCTAssertEqual(
            load(folder.path).sentence,
            "The folder's tokenizer_config.json names no tokenizer_class, which napkin needs to read the tokenizer from the folder alone."
        )
    }

    func testATokenizerConfigThatIsNotJsonIsRefused() throws {
        let folder = try completeFolder()
        let config = folder.appendingPathComponent("tokenizer_config.json")
        try "not json".write(to: config, atomically: true, encoding: .utf8)
        XCTAssertEqual(
            load(folder.path).sentence,
            "The folder's tokenizer_config.json is not a JSON object: \(config.path).")
    }

    func testAMissingFolderAndAFileAreRefused() throws {
        XCTAssertEqual(load("/nonexistent/napkin/model").sentence, "There is no folder at /nonexistent/napkin/model.")
        let folder = try completeFolder()
        let file = folder.appendingPathComponent("config.json").path
        XCTAssertEqual(load(file).sentence, "\(file) is a file, not a model folder.")
    }

    func testACompleteFolderStillNeedsInit() throws {
        // The test process never passes init (its metallib is not beside this binary), so a
        // folder that passes every file check stops at the init check, before any read.
        XCTAssertFalse(Bridge.isInitialized)
        let folder = try completeFolder()
        let (status, sentence, model) = load(folder.path)
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "napkin_mlx_init has not succeeded in this process, so no model can be loaded.")
        XCTAssertNil(model)
    }

    func testNullArgumentsAreErrors() {
        XCTAssertEqual(load(nil).sentence, "napkin_mlx_load was given no model folder")
        let (status, sentence) = withErrorSentence { napkin_mlx_load("/tmp", nil, nil, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "napkin_mlx_load was given nowhere to put the model")
    }

    func testTheCancelFlagIsReadAtomically() {
        var flag: Int32 = 0
        XCTAssertFalse(CancelFlag(&flag).isSet)
        napkin_mlx_internal_store_flag(&flag, 1)
        XCTAssertTrue(CancelFlag(&flag).isSet)
        XCTAssertFalse(CancelFlag.never.isSet)
    }

    func testRunBlockingCancelsTheWorkWhenTheFlagIsSet() {
        let flag = UnsafeMutablePointer<Int32>.allocate(capacity: 1)
        flag.initialize(to: 0)
        defer { flag.deallocate() }
        DispatchQueue.global().asyncAfter(deadline: .now() + .milliseconds(50)) {
            napkin_mlx_internal_store_flag(flag, 1)
        }
        let started = Date()
        let outcome = runBlocking(cancel: CancelFlag(UnsafePointer(flag))) { () async throws -> Int in
            try await Task.sleep(nanoseconds: 10_000_000_000)
            return 1
        }
        XCTAssertLessThan(Date().timeIntervalSince(started), 2, "cancelled well before the 10 s sleep")
        guard case .failure(let error) = outcome else {
            return XCTFail("expected the sleep to be cancelled")
        }
        XCTAssertTrue(error is CancellationError, "\(error)")
    }
}
