// The C ABI through its C names, exactly as napkin's P/Invoke calls it (mlx-runtime.md §7.1).
// Model-free and GPU-free: every call here fails before MLX is touched, or never touches it.

import CNapkinMlx
import Foundation
import Metal
import XCTest

@testable import NapkinMlx

/// Calls a function that takes `char** error`; returns its status and the sentence, freed through
/// napkin_mlx_string_free as napkin must.
func withErrorSentence(_ body: (UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>) -> napkin_mlx_status)
    -> (status: napkin_mlx_status, sentence: String?)
{
    var error: UnsafeMutablePointer<CChar>? = strdup("not overwritten")
    let original = error
    let status = body(&error)
    defer {
        napkin_mlx_string_free(error)
        if error != original { free(original) }
    }
    return (status, error.map { String(cString: $0) })
}

final class AbiTests: XCTestCase {
    func testAbiVersionIsOne() {
        XCTAssertEqual(napkin_mlx_abi_version(), 1)
    }

    func testHeaderAndImplementationAgreeOnAllEightSignatures() {
        // Both arrays are built from the same typealiases; that they compiled is the check.
        XCTAssertEqual(headerSignatures.count, 8)
        XCTAssertEqual(implementationSignatures.count, 8)
    }

    func testEveryStatusHasASentence() {
        let statuses = [
            NAPKIN_MLX_ERROR, NAPKIN_MLX_CANCELLED, NAPKIN_MLX_BUSY, NAPKIN_MLX_NO_METAL,
            NAPKIN_MLX_INCOMPLETE,
        ]
        let sentences = statuses.map(BridgeError.sentence(for:))
        for (status, sentence) in zip(statuses, sentences) {
            XCTAssertFalse(sentence.isEmpty, "status \(status.rawValue)")
            XCTAssertTrue(sentence.hasSuffix("."), "status \(status.rawValue): \(sentence)")
            XCTAssertFalse(sentence.hasPrefix("Unknown"), "status \(status.rawValue)")
        }
        XCTAssertEqual(Set(sentences).count, sentences.count, "each status says something different")
        XCTAssertEqual(NAPKIN_MLX_OK.rawValue, 0)
        XCTAssertEqual(NAPKIN_MLX_INCOMPLETE.rawValue, 5)
    }

    func testEachBridgeErrorMapsToItsStatus() {
        XCTAssertEqual(BridgeError.argument("x").status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(BridgeError.failed("x").status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(BridgeError.cancelled.status, NAPKIN_MLX_CANCELLED)
        XCTAssertEqual(BridgeError.busy.status, NAPKIN_MLX_BUSY)
        XCTAssertEqual(BridgeError.noMetal.status, NAPKIN_MLX_NO_METAL)
        XCTAssertEqual(BridgeError.incomplete(maxTokens: 7).status, NAPKIN_MLX_INCOMPLETE)
        XCTAssertTrue(BridgeError.incomplete(maxTokens: 7).sentence.contains("7 tokens"))
    }

    func testTheStructsHaveTheHeadersLayout() {
        // What slice B's blittable C# structs must match.
        XCTAssertEqual(MemoryLayout<napkin_mlx_device_info>.size, 136)
        XCTAssertEqual(MemoryLayout<napkin_mlx_device_info>.offset(of: \.memory_bytes), 72)
        XCTAssertEqual(MemoryLayout<napkin_mlx_device_info>.offset(of: \.mlx_swift_lm_revision), 88)
        XCTAssertEqual(MemoryLayout<napkin_mlx_result>.size, 40)
        XCTAssertEqual(MemoryLayout<napkin_mlx_result>.offset(of: \.prompt_seconds), 24)
        XCTAssertEqual(MemoryLayout<napkin_mlx_status>.size, 4)
    }

    // MARK: init

    func testInitWithoutAPathIsAnError() {
        let (status, sentence) = withErrorSentence { napkin_mlx_init(nil, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "napkin_mlx_init was given no metallib path.")
    }

    func testInitWithAMissingMetallibNamesThePath() {
        let path = "/nonexistent/napkin/native/mlx.metallib"
        let (status, sentence) = withErrorSentence { napkin_mlx_init(path, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "There is no Metal library at \(path).")
    }

    func testInitWithAMetallibNotBesideTheBridgeNamesBothPaths() throws {
        let folder = try temporaryFolder()
        let path = folder.appendingPathComponent("mlx.metallib").path
        XCTAssertTrue(FileManager.default.createFile(atPath: path, contents: Data([0])))
        let (status, sentence) = withErrorSentence { napkin_mlx_init(path, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(
            sentence,
            "MLX loads its Metal library only from beside the bridge, \(Location.colocatedMetallib); napkin passed \(path)."
        )
    }

    func testInitAcceptsANullErrorPointer() {
        XCTAssertEqual(napkin_mlx_init(nil, nil), NAPKIN_MLX_ERROR)
    }

    func testTheBinaryPathIsThisImage() {
        XCTAssertFalse(Location.binaryPath.isEmpty)
        XCTAssertTrue(Location.colocatedMetallib.hasSuffix("/mlx.metallib"))
    }

    // MARK: device

    func testDeviceFillsTheStructWithoutInit() {
        var info = napkin_mlx_device_info()
        let (status, sentence) = withErrorSentence { napkin_mlx_device(&info, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_OK)
        XCTAssertNil(sentence, "OK sets *error to NULL")
        XCTAssertEqual(info.has_metal, MTLCreateSystemDefaultDevice() == nil ? 0 : 1)
        XCTAssertGreaterThan(info.memory_bytes, 0)
        let architecture = withUnsafeBytes(of: info.architecture) {
            String(cString: $0.bindMemory(to: CChar.self).baseAddress!)
        }
        let revision = withUnsafeBytes(of: info.mlx_swift_lm_revision) {
            String(cString: $0.bindMemory(to: CChar.self).baseAddress!)
        }
        XCTAssertFalse(architecture.isEmpty)
        XCTAssertEqual(revision, Build.mlxSwiftLmRevision)
        if info.has_metal == 0 {
            XCTAssertEqual(architecture, "Unknown")
            XCTAssertEqual(info.recommended_working_set_bytes, 0)
        }
    }

    func testDeviceWithoutAStructIsAnError() {
        let (status, sentence) = withErrorSentence { napkin_mlx_device(nil, $0) }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "napkin_mlx_device was given no struct to fill")
    }

    // MARK: generate, free, unload without a model

    func testGenerateWithoutAModelIsAnError() {
        var result: UnsafeMutablePointer<napkin_mlx_result>? = nil
        let (status, sentence) = withErrorSentence {
            napkin_mlx_generate(nil, "s", "u", nil, 16, 0.2, 1, 0, nil, &result, $0)
        }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "napkin_mlx_generate was given no model")
        XCTAssertNil(result)
    }

    func testGenerateWithAHandleThatWasNeverLoadedIsAnError() {
        var result: UnsafeMutablePointer<napkin_mlx_result>? = nil
        let bogus = OpaquePointer(bitPattern: 0x1234)
        let (status, sentence) = withErrorSentence {
            napkin_mlx_generate(bogus, "s", "u", nil, 16, 0.2, 1, 0, nil, &result, $0)
        }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(
            sentence, "napkin_mlx_generate was given a model that is not loaded (unloaded already?).")
        XCTAssertNil(result)
    }

    func testGenerateWithZeroMaxTokensIsAnError() {
        var result: UnsafeMutablePointer<napkin_mlx_result>? = nil
        let bogus = OpaquePointer(bitPattern: 0x1234)
        let (status, sentence) = withErrorSentence {
            napkin_mlx_generate(bogus, "s", "u", nil, 0, 0.2, 1, 0, nil, &result, $0)
        }
        XCTAssertEqual(status, NAPKIN_MLX_ERROR)
        XCTAssertEqual(sentence, "max_tokens must be at least 1; it was 0.")
    }

    func testGenerateWithoutMessagesOrResultIsAnError() {
        var result: UnsafeMutablePointer<napkin_mlx_result>? = nil
        let bogus = OpaquePointer(bitPattern: 0x1234)
        XCTAssertEqual(
            withErrorSentence { napkin_mlx_generate(bogus, nil, "u", nil, 16, 0.2, 1, 0, nil, &result, $0) }
                .sentence,
            "napkin_mlx_generate needs both a system and a user message")
        XCTAssertEqual(
            withErrorSentence { napkin_mlx_generate(bogus, "s", "u", nil, 16, 0.2, 1, 0, nil, nil, $0) }
                .sentence,
            "napkin_mlx_generate was given nowhere to put the result")
    }

    func testTheFreesAndUnloadTolerateNull() {
        napkin_mlx_result_free(nil)
        napkin_mlx_string_free(nil)
        napkin_mlx_unload(nil)
        napkin_mlx_unload(OpaquePointer(bitPattern: 0x1234))  // not a live handle: ignored
    }

    func testAResultIsFreedWithItsText() {
        let reply = GenerationReply(
            text: "{\"parts\":[]}", promptTokens: 12, generatedTokens: 5, stopReason: .schemaComplete,
            promptSeconds: 0.5, generationSeconds: 0.25)
        let result = reply.allocateResult()
        XCTAssertEqual(String(cString: result.pointee.text), "{\"parts\":[]}")
        XCTAssertEqual(result.pointee.prompt_tokens, 12)
        XCTAssertEqual(result.pointee.generated_tokens, 5)
        XCTAssertEqual(result.pointee.stop_reason, 3)
        XCTAssertEqual(result.pointee.prompt_seconds, 0.5)
        XCTAssertEqual(result.pointee.generation_seconds, 0.25)
        napkin_mlx_result_free(result)
    }
}

func temporaryFolder() throws -> URL {
    let folder = FileManager.default.temporaryDirectory
        .appendingPathComponent("napkin-mlx-tests-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
    return folder
}
