// The C ABI of include/napkin_mlx.h — the eight @_cdecl functions and nothing else public.
// docs/design/mlx-runtime.md §1.4. Each function converts C arguments to Swift, calls Bridge, and
// converts the outcome back to a status, an optional result and an optional *error sentence.
//
// @_cdecl "uses the C calling convention" and "type bridging is not done" (Swift's
// UnderscoredAttributes.md); every parameter below is a C scalar, a C string, an opaque pointer or
// a struct declared in the header, so nothing but the calling convention is relied on (§8.1).

import CNapkinMlx
import Foundation

// MARK: - Compile-time agreement with the header

// Each signature is written once, as a typealias. The header's declaration and the Swift
// implementation are both assigned to it, so a header edit that the Swift side does not follow
// (or the reverse) is a compile error, not a crash at the first P/Invoke.
typealias AbiVersionFn = @convention(c) () -> Int32
typealias InitFn = @convention(c) (
    UnsafePointer<CChar>?, UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status
typealias DeviceFn = @convention(c) (
    UnsafeMutablePointer<napkin_mlx_device_info>?,
    UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status
typealias LoadFn = @convention(c) (
    UnsafePointer<CChar>?, UnsafePointer<Int32>?, UnsafeMutablePointer<OpaquePointer?>?,
    UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status
typealias GenerateFn = @convention(c) (
    OpaquePointer?, UnsafePointer<CChar>?, UnsafePointer<CChar>?, UnsafePointer<CChar>?,
    Int32, Float, Float, Int32, UnsafePointer<Int32>?,
    UnsafeMutablePointer<UnsafeMutablePointer<napkin_mlx_result>?>?,
    UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status
typealias ResultFreeFn = @convention(c) (UnsafeMutablePointer<napkin_mlx_result>?) -> Void
typealias StringFreeFn = @convention(c) (UnsafeMutablePointer<CChar>?) -> Void
typealias UnloadFn = @convention(c) (OpaquePointer?) -> Void

/// The header's declarations, typed. Referenced by the tests so the check is not dead-stripped.
let headerSignatures: [Any] = [
    napkin_mlx_abi_version as AbiVersionFn,
    napkin_mlx_init as InitFn,
    napkin_mlx_device as DeviceFn,
    napkin_mlx_load as LoadFn,
    napkin_mlx_generate as GenerateFn,
    napkin_mlx_result_free as ResultFreeFn,
    napkin_mlx_string_free as StringFreeFn,
    napkin_mlx_unload as UnloadFn,
]

/// The Swift implementations, typed the same way.
let implementationSignatures: [Any] = [
    abiVersion as AbiVersionFn,
    initialize as InitFn,
    device as DeviceFn,
    load as LoadFn,
    generate as GenerateFn,
    resultFree as ResultFreeFn,
    stringFree as StringFreeFn,
    unload as UnloadFn,
]

// MARK: - The eight functions

@_cdecl("napkin_mlx_abi_version")
public func abiVersion() -> Int32 {
    Build.abiVersion
}

@_cdecl("napkin_mlx_init")
public func initialize(
    _ metallibPath: UnsafePointer<CChar>?,
    _ error: UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status {
    finish(Bridge.initialize(metallibPath: metallibPath.map { String(cString: $0) }), error)
}

@_cdecl("napkin_mlx_device")
public func device(
    _ out: UnsafeMutablePointer<napkin_mlx_device_info>?,
    _ error: UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status {
    guard let out else {
        return finish(.failure(.argument("napkin_mlx_device was given no struct to fill")), error)
    }
    let info = Bridge.deviceInfo()
    out.pointee.has_metal = info.hasMetal ? 1 : 0
    copy(info.architecture, into: &out.pointee.architecture)
    out.pointee.memory_bytes = info.memoryBytes
    out.pointee.recommended_working_set_bytes = info.recommendedWorkingSetBytes
    copy(Build.mlxSwiftLmRevision, into: &out.pointee.mlx_swift_lm_revision)
    return finish(.success(()), error)
}

@_cdecl("napkin_mlx_load")
public func load(
    _ modelDir: UnsafePointer<CChar>?,
    _ cancel: UnsafePointer<Int32>?,
    _ out: UnsafeMutablePointer<OpaquePointer?>?,
    _ error: UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status {
    out?.pointee = nil
    guard let out else {
        return finish(.failure(.argument("napkin_mlx_load was given nowhere to put the model")), error)
    }
    guard let modelDir else {
        return finish(.failure(.argument("napkin_mlx_load was given no model folder")), error)
    }
    let outcome = Bridge.load(folder: String(cString: modelDir), cancel: CancelFlag(cancel))
    if case .success(let handle) = outcome {
        out.pointee = handle
    }
    return finish(outcome.map { _ in () }, error)
}

@_cdecl("napkin_mlx_generate")
public func generate(
    _ model: OpaquePointer?,
    _ system: UnsafePointer<CChar>?,
    _ user: UnsafePointer<CChar>?,
    _ jsonSchema: UnsafePointer<CChar>?,
    _ maxTokens: Int32,
    _ temperature: Float,
    _ topP: Float,
    _ enableThinking: Int32,
    _ cancel: UnsafePointer<Int32>?,
    _ out: UnsafeMutablePointer<UnsafeMutablePointer<napkin_mlx_result>?>?,
    _ error: UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status {
    out?.pointee = nil
    guard let out else {
        return finish(.failure(.argument("napkin_mlx_generate was given nowhere to put the result")), error)
    }
    guard let model else {
        return finish(.failure(.argument("napkin_mlx_generate was given no model")), error)
    }
    guard let system, let user else {
        return finish(.failure(.argument("napkin_mlx_generate needs both a system and a user message")), error)
    }
    let request = GenerationRequest(
        system: String(cString: system),
        user: String(cString: user),
        jsonSchema: jsonSchema.map { String(cString: $0) },
        maxTokens: Int(maxTokens),
        temperature: temperature,
        topP: topP,
        enableThinking: enableThinking != 0)
    let outcome = Bridge.generate(handle: model, request: request, cancel: CancelFlag(cancel))
    if case .success(let reply) = outcome {
        out.pointee = reply.allocateResult()
    }
    return finish(outcome.map { _ in () }, error)
}

@_cdecl("napkin_mlx_result_free")
public func resultFree(_ result: UnsafeMutablePointer<napkin_mlx_result>?) {
    guard let result else { return }
    free(UnsafeMutableRawPointer(mutating: result.pointee.text))
    free(result)
}

@_cdecl("napkin_mlx_string_free")
public func stringFree(_ string: UnsafeMutablePointer<CChar>?) {
    free(string)
}

@_cdecl("napkin_mlx_unload")
public func unload(_ model: OpaquePointer?) {
    guard let model else { return }
    Bridge.unload(handle: model)
}

// MARK: - Helpers

/// Sets *error (a strdup'd sentence, or NULL on OK) and returns the status.
private func finish(
    _ outcome: Result<Void, BridgeError>,
    _ error: UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?
) -> napkin_mlx_status {
    switch outcome {
    case .success:
        error?.pointee = nil
        return NAPKIN_MLX_OK
    case .failure(let failure):
        error?.pointee = strdup(failure.sentence)
        return failure.status
    }
}

/// Copies a string into a fixed C char array (imported as a tuple), always NUL-terminated,
/// truncated to fit.
private func copy<T>(_ string: String, into field: inout T) {
    withUnsafeMutableBytes(of: &field) { buffer in
        guard let base = buffer.baseAddress?.assumingMemoryBound(to: CChar.self) else { return }
        _ = strlcpy(base, string, buffer.count)
    }
}

extension GenerationReply {
    /// A malloc'd napkin_mlx_result whose text is strdup'd; napkin_mlx_result_free frees both.
    func allocateResult() -> UnsafeMutablePointer<napkin_mlx_result> {
        // calloc, so napkin_mlx_result_free's free() matches the allocator exactly.
        let raw = calloc(1, MemoryLayout<napkin_mlx_result>.stride)!
        let typed = raw.bindMemory(to: napkin_mlx_result.self, capacity: 1)
        typed.pointee = napkin_mlx_result(
            text: UnsafePointer(strdup(text)),
            prompt_tokens: Int32(clamping: promptTokens),
            generated_tokens: Int32(clamping: generatedTokens),
            stop_reason: stopReason.rawValue,
            prompt_seconds: promptSeconds,
            generation_seconds: generationSeconds)
        return typed
    }
}
