// init, device, load, generate (plain and guided), cancel, unload, and the handle table.
// mlx-runtime.md §1.4–§1.5, as built in slice A (the note's "as built in A" section says where the
// pinned libraries made this differ from the note).
//
// Threading (§1.5): every entry point blocks the thread that calls it — napkin's thread-pool
// thread, never a Swift cooperative thread. Async Swift work runs in a detached Task; the caller's
// thread waits on a semaphore and, while it waits, polls napkin's cancel flag and cancels that
// Task when the flag flips. One init per process, one load at a time per process, one generation
// at a time per model (BUSY otherwise). No state is shared between handles.

import CNapkinMlx
import Foundation
import Metal
import MLX
import MLXGuidedGeneration
import MLXLLM
import MLXLMCommon
import NapkinMlxSchemas

// MARK: - Values that cross the boundary

/// napkin's cancel flag: four bytes napkin owns and writes from any thread. Read atomically.
struct CancelFlag: @unchecked Sendable {
    let pointer: UnsafePointer<Int32>?

    init(_ pointer: UnsafePointer<Int32>?) {
        self.pointer = pointer
    }

    static let never = CancelFlag(nil)

    var isSet: Bool {
        napkin_mlx_internal_load_flag(pointer) != 0
    }
}

struct GenerationRequest: Sendable {
    var system: String
    var user: String
    var jsonSchema: String?
    var maxTokens: Int
    var temperature: Float
    var topP: Float
    var enableThinking: Bool
}

/// napkin_mlx_result.stop_reason.
enum StopReason: Int32, Sendable {
    case stopToken = 0
    case length = 1
    case cancelled = 2
    case schemaComplete = 3
}

struct GenerationReply: Sendable {
    var text: String
    var promptTokens: Int
    var generatedTokens: Int
    var stopReason: StopReason
    var promptSeconds: Double
    var generationSeconds: Double
}

struct DeviceInfo {
    var hasMetal: Bool
    var architecture: String
    var memoryBytes: UInt64
    var recommendedWorkingSetBytes: UInt64
}

// MARK: - The bridge

enum Bridge {
    /// Guards `initialized` and `live`.
    private static let stateLock = NSLock()
    /// Serializes init and load: one at a time per process (§1.5).
    private static let loadLock = NSLock()
    private static var initialized = false
    /// Every handle napkin holds, so a stale or foreign pointer is an ERROR, not a crash.
    private static var live: [UnsafeMutableRawPointer: LoadedModel] = [:]

    static var isInitialized: Bool {
        stateLock.withLock { initialized }
    }

    // MARK: init

    /// Checks, in an order that touches nothing it does not have to:
    /// 1. the path names a file;
    /// 2. it is the mlx.metallib beside this binary — MLX 0.31 (mlx-swift 0.31.6) has no metallib
    ///    override, and loads `mlx.metallib` from the directory of the image MLX is compiled into
    ///    (`current_binary_dir()`, dladdr — mlx/backend/common/utils.cpp);
    /// 3. a Metal device exists — probed through Metal itself, because MLX 0.31's `load_device()`
    ///    indexes an empty device array and aborts rather than throwing (device.cpp);
    /// 4. Metal can load the library;
    /// 5. one tiny evaluation on the GPU inside `withError`, so MLX creates its device and loads the
    ///    library now and a failure is this call's status, not an abort at the first generation.
    /// Steps 1–2 need no GPU, so their ERRORs are testable on a machine without one.
    static func initialize(metallibPath: String?) -> Result<Void, BridgeError> {
        loadLock.lock()
        defer { loadLock.unlock() }
        if isInitialized {
            return .success(())
        }
        let started = Date()
        guard let metallibPath, !metallibPath.isEmpty else {
            return .failure(.argument("napkin_mlx_init was given no metallib path."))
        }
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: metallibPath, isDirectory: &isDirectory),
            !isDirectory.boolValue
        else {
            return .failure(.failed("There is no Metal library at \(metallibPath)."))
        }
        let expected = Location.colocatedMetallib
        guard canonical(metallibPath) == canonical(expected) else {
            return .failure(
                .failed(
                    "MLX loads its Metal library only from beside the bridge, \(expected); napkin passed \(metallibPath)."
                ))
        }
        guard let device = MTLCreateSystemDefaultDevice() else {
            return .failure(.noMetal)
        }
        do {
            _ = try device.makeLibrary(URL: URL(fileURLWithPath: metallibPath))
        } catch {
            return .failure(
                .failed("Metal could not load the library at \(metallibPath): \(describe(error))"))
        }
        do {
            try withError { error in
                let array = MLXArray([1, 2, 3] as [Float]) + 1
                eval(array)
                try error.check()
                let sum = array.sum().item(Float.self)
                try error.check()
                guard sum == 9 else {
                    throw BridgeError.failed(
                        "MLX's first computation on the GPU gave \(sum) where 9 was expected.")
                }
            }
        } catch {
            return .failure(.failed("MLX could not run on the GPU: \(describe(error))"))
        }
        stateLock.withLock { initialized = true }
        Diagnostics.log(
            "init ok in \(Diagnostics.ms(since: started)) (Metal \(device.architecture.name), metallib \(metallibPath))"
        )
        return .success(())
    }

    // MARK: device

    /// Metal and sysctl only (mlx-swift's GPU.deviceInfo()); never touches MLX's own device, so it
    /// is safe before init and on a Mac without a GPU.
    static func deviceInfo() -> DeviceInfo {
        let info = GPU.deviceInfo()
        return DeviceInfo(
            hasMetal: MTLCreateSystemDefaultDevice() != nil,
            architecture: info.architecture,
            memoryBytes: UInt64(info.memorySize),
            recommendedWorkingSetBytes: info.maxRecommendedWorkingSetSize)
    }

    // MARK: load

    static func load(folder: String, cancel: CancelFlag) -> Result<OpaquePointer, BridgeError> {
        // Pure file checks first: no GPU needed, and they keep swift-transformers off its
        // fatalError path (ModelFolder.validate says which).
        if let refusal = ModelFolder.validate(folder) {
            return .failure(refusal)
        }
        guard isInitialized else {
            return .failure(
                .failed("napkin_mlx_init has not succeeded in this process, so no model can be loaded."))
        }
        loadLock.lock()
        defer { loadLock.unlock() }
        if cancel.isSet {
            return .failure(.cancelled)
        }
        let started = Date()
        let loaded = runBlocking(cancel: .never) {
            try await LoadedModel.load(folder: folder)
        }
        switch loaded {
        case .failure(let error):
            return .failure(.failed("napkin could not load the model in \(folder): \(describe(error))"))
        case .success(let model):
            // Loading is not interruptible inside mlx-swift-lm; a cancel seen after it drops the
            // model (§1.4).
            if cancel.isSet {
                return .failure(.cancelled)
            }
            Diagnostics.log(
                "load \(folder): \(Diagnostics.seconds(since: started)); \(model.guided.summary); MLX active \(Diagnostics.gb(Memory.activeMemory)), peak \(Diagnostics.gb(Memory.peakMemory))"
            )
            let handle = Unmanaged.passRetained(model).toOpaque()
            stateLock.withLock { live[handle] = model }
            return .success(OpaquePointer(handle))
        }
    }

    // MARK: generate

    static func generate(handle: OpaquePointer, request: GenerationRequest, cancel: CancelFlag)
        -> Result<GenerationReply, BridgeError>
    {
        guard request.maxTokens > 0 else {
            return .failure(.argument("max_tokens must be at least 1; it was \(request.maxTokens)."))
        }
        guard let model = lookup(handle) else {
            return .failure(
                .argument("napkin_mlx_generate was given a model that is not loaded (unloaded already?)."))
        }
        guard model.beginGeneration() else {
            return .failure(.busy)
        }
        defer { model.endGeneration() }
        if cancel.isSet {
            return .failure(.cancelled)
        }
        let outcome = runBlocking(cancel: cancel) {
            try await model.generate(request, cancel: cancel)
        }
        switch outcome {
        case .success(let reply):
            return .success(reply)
        case .failure(let error as BridgeError):
            return .failure(error)
        case .failure(let error):
            if cancel.isSet || error is CancellationError {
                return .failure(.cancelled)
            }
            return .failure(.failed("The model could not answer: \(describe(error))"))
        }
    }

    // MARK: unload

    /// Drops the bridge's reference. A generation still running on the model keeps its own
    /// reference and finishes first; the handle is dead for napkin at once.
    static func unload(handle: OpaquePointer) {
        let raw = UnsafeMutableRawPointer(handle)
        let removed = stateLock.withLock { live.removeValue(forKey: raw) }
        if removed != nil {
            Unmanaged<LoadedModel>.fromOpaque(raw).release()
        }
    }

    private static func lookup(_ handle: OpaquePointer) -> LoadedModel? {
        stateLock.withLock { live[UnsafeMutableRawPointer(handle)] }
    }

    private static func canonical(_ path: String) -> String {
        URL(fileURLWithPath: path).standardizedFileURL.resolvingSymlinksInPath().path
    }
}

// MARK: - Where this binary is

enum Location {
    /// The file this code was loaded from (libNapkinMlx.dylib in napkin; the test bundle or the
    /// spike when those link the target). MLX is compiled into the same image, so its
    /// `current_binary_dir()` is this file's directory.
    static var binaryPath: String {
        var info = Dl_info()
        guard dladdr(#dsohandle, &info) != 0, let name = info.dli_fname else {
            return ""
        }
        return String(cString: name)
    }

    /// Where MLX 0.31 looks first for its Metal library: `mlx.metallib` beside this binary.
    static var colocatedMetallib: String {
        URL(fileURLWithPath: binaryPath).deletingLastPathComponent()
            .appendingPathComponent("mlx.metallib").path
    }
}

// MARK: - The model folder, checked before anything reads it

enum ModelFolder {
    /// nil when the folder has what mlx-swift-lm and swift-transformers read; otherwise the
    /// refusal, naming what is missing.
    ///
    /// `tokenizer_config.json` with a `tokenizer_class` is required, not optional as it is for
    /// swift-transformers: without it `LanguageModelConfigurationFromHub.tokenizerConfig` falls
    /// back to a config bundled in swift-transformers' Hub resources through `Bundle.module`,
    /// whose generated accessor calls fatalError when the bundle is not beside the host
    /// executable — as it never is beside napkin. Every mlx-community folder of §5 has both.
    static func validate(_ path: String) -> BridgeError? {
        let files = FileManager.default
        var isDirectory: ObjCBool = false
        guard files.fileExists(atPath: path, isDirectory: &isDirectory) else {
            return .failed("There is no folder at \(path).")
        }
        guard isDirectory.boolValue else {
            return .failed("\(path) is a file, not a model folder.")
        }
        let folder = URL(fileURLWithPath: path, isDirectory: true)
        for name in ["config.json", "tokenizer.json", "tokenizer_config.json"] {
            guard files.fileExists(atPath: folder.appendingPathComponent(name).path) else {
                return .failed("The folder \(path) has no \(name).")
            }
        }
        let tokenizerConfig = folder.appendingPathComponent("tokenizer_config.json")
        guard let data = try? Data(contentsOf: tokenizerConfig),
            let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
        else {
            return .failed("The folder's tokenizer_config.json is not a JSON object: \(tokenizerConfig.path).")
        }
        guard object["tokenizer_class"] is String else {
            return .failed(
                "The folder's tokenizer_config.json names no tokenizer_class, which napkin needs to read the tokenizer from the folder alone."
            )
        }
        let names = (try? files.contentsOfDirectory(atPath: path)) ?? []
        guard names.contains(where: { $0.hasSuffix(".safetensors") }) else {
            return .failed("The folder \(path) has no .safetensors weights.")
        }
        return nil
    }
}

// MARK: - A loaded model

final class LoadedModel: @unchecked Sendable {
    let folder: String
    let container: ModelContainer
    let guided: GuidedEngine
    private let lock = NSLock()
    private var generating = false

    private init(folder: String, container: ModelContainer, guided: GuidedEngine) {
        self.folder = folder
        self.container = container
        self.guided = guided
    }

    /// Loads through LLMModelFactory directly: the free function `loadModelContainer` finds its
    /// factory through `NSClassFromString("MLXLLM.TrampolineModelFactory")`, which a dynamic
    /// library that never names MLXLLM's types could lose to dead stripping.
    static func load(folder: String) async throws -> LoadedModel {
        try await withError {
            let directory = URL(fileURLWithPath: folder, isDirectory: true)
            let container = try await LLMModelFactory.shared.loadContainer(
                from: directory, using: TransformersTokenizerLoader())
            let tokenizer = await container.tokenizer
            let guided = try GuidedEngine(tokenizer: tokenizer)
            guided.prewarm(NapkinSchemas.all)
            return LoadedModel(folder: folder, container: container, guided: guided)
        }
    }

    func beginGeneration() -> Bool {
        lock.withLock {
            if generating { return false }
            generating = true
            return true
        }
    }

    func endGeneration() {
        lock.withLock { generating = false }
    }

    func generate(_ request: GenerationRequest, cancel: CancelFlag) async throws -> GenerationReply
    {
        try await withError {
            // enable_thinking is a real Bool: Qwen3's template tests `enable_thinking is false`
            // (tokenizer_config.json's chat_template, §5.2), which an integer would not match.
            let input = UserInput(
                chat: [.system(request.system), .user(request.user)],
                additionalContext: ["enable_thinking": request.enableThinking])
            let prepared = try await container.prepare(input: input)
            if let schema = request.jsonSchema {
                return try await guided(prepared, schema: schema, request: request, cancel: cancel)
            }
            return try await plain(prepared, request: request, cancel: cancel)
        }
    }

    /// Free text: mlx-swift-lm's own generation loop (Evaluate.swift `generateTask`), with
    /// napkin's temperature, top-p and max tokens. The loop is `while !Task.isCancelled`; a cancel
    /// of the waiting Task cancels the generation Task (unstructured, so it is cancelled
    /// explicitly), which stops at the next token with `.cancelled`.
    private func plain(_ input: LMInput, request: GenerationRequest, cancel: CancelFlag)
        async throws -> GenerationReply
    {
        let parameters = GenerateParameters(
            maxTokens: request.maxTokens, temperature: request.temperature, topP: request.topP)
        let (stream, task) = try await container.perform(nonSendable: input) { context, input in
            let iterator = try TokenIterator(
                input: input, model: context.model, parameters: parameters)
            return generateTask(
                promptTokenCount: input.text.tokens.size,
                modelConfiguration: context.configuration,
                tokenizer: context.tokenizer,
                iterator: iterator)
        }
        var text = ""
        var info: GenerateCompletionInfo?
        await withTaskCancellationHandler {
            for await item in stream {
                switch item {
                case .chunk(let chunk): text += chunk
                case .info(let completion): info = completion
                default: break
                }
            }
        } onCancel: {
            task.cancel()
        }
        // The loop may still be finishing its last evaluation; wait for it (Evaluate.swift:
        // "Callers can await the task to observe when the use of the parameters is complete").
        await task.value
        if cancel.isSet || Task.isCancelled || info?.stopReason == .cancelled {
            throw BridgeError.cancelled
        }
        guard let info else {
            throw BridgeError.failed("The generation ended without reporting how it ended.")
        }
        let reply = GenerationReply(
            text: text,
            promptTokens: info.promptTokenCount,
            generatedTokens: info.generationTokenCount,
            stopReason: info.stopReason == .length ? .length : .stopToken,
            promptSeconds: info.promptTime,
            generationSeconds: info.generateTime)
        Diagnostics.logGeneration("plain", reply, constraint: nil)
        return reply
    }

    /// JSON that matches `schema` by construction: MLXGuidedGeneration's loop, which masks every
    /// token to the grammar and samples by argmax (§2 — napkin's temperature does not apply).
    /// Cancel: the loop checks `Task.checkCancellation()` every token, and `emit` returns false
    /// once the flag is set.
    private func guided(
        _ input: LMInput, schema: String, request: GenerationRequest, cancel: CancelFlag
    ) async throws -> GenerationReply {
        let taken = try guided.constraint(for: schema)
        let budget = guided.budget(for: schema, maxTokens: request.maxTokens)
        let engine = guided
        let promptTokens = input.text.tokens.size
        let reply: GenerationReply = try await container.perform(nonSendable: input) {
            context, input in
            var text = ""
            let started = Date()
            var firstEmit: Date?
            var stoppedByFlag = false
            let generated: Int
            do {
                generated = try GuidedGenerationLoop.run(
                    input: input,
                    context: context,
                    constraint: taken.constraint,
                    maxTokens: request.maxTokens,
                    vocabSize: engine.vocabSize,
                    completionReserve: budget.completionReserve,
                    hardReserve: budget.hardReserve,
                    closingBias: engine.closingBias,
                    whitespaceBias: engine.whitespaceBias,
                    whitespaceTokenIDs: engine.whitespaceTokenIDs
                ) { delta in
                    if firstEmit == nil { firstEmit = Date() }
                    text += delta
                    if cancel.isSet {
                        stoppedByFlag = true
                        return false
                    }
                    return true
                }
            } catch GuidedGenerationError.incompleteOutput {
                throw BridgeError.incomplete(maxTokens: request.maxTokens)
            } catch GuidedGenerationError.prematureEOS {
                // Not thrown by the loop at the pinned commit; mapped in case a re-pin adds it.
                throw BridgeError.incomplete(maxTokens: request.maxTokens)
            } catch is CancellationError {
                throw BridgeError.cancelled
            }
            if stoppedByFlag || cancel.isSet {
                throw BridgeError.cancelled
            }
            let finished = Date()
            // The loop prefills inside run(); the time to the first emitted text stands in for
            // the prompt time (it includes the first token).
            let prompt = (firstEmit ?? finished).timeIntervalSince(started)
            return GenerationReply(
                text: text,
                promptTokens: promptTokens,
                generatedTokens: generated,
                stopReason: .schemaComplete,
                promptSeconds: prompt,
                generationSeconds: finished.timeIntervalSince(firstEmit ?? finished))
        }
        Diagnostics.logGeneration("guided", reply, constraint: taken.provenance)
        return reply
    }
}

// MARK: - Blocking on async work

private final class Box<T>: @unchecked Sendable {
    private let lock = NSLock()
    private var stored: T?
    var value: T? {
        get { lock.withLock { stored } }
        set { lock.withLock { stored = newValue } }
    }
}

/// Runs `body` in a detached Task and blocks the calling thread (napkin's, never a cooperative
/// one) until it finishes. While waiting it polls `cancel` every 2 ms and cancels the Task when
/// the flag is set, so a cancel reaches both generation loops within a poll and a token.
func runBlocking<T>(cancel: CancelFlag, _ body: @escaping @Sendable () async throws -> T)
    -> Result<T, Error>
{
    let done = DispatchSemaphore(value: 0)
    let box = Box<Result<T, Error>>()
    let work = Task.detached {
        do {
            box.value = .success(try await body())
        } catch {
            box.value = .failure(error)
        }
        done.signal()
    }
    var cancelled = false
    while done.wait(timeout: .now() + .milliseconds(2)) == .timedOut {
        if !cancelled, cancel.isSet {
            cancelled = true
            work.cancel()
        }
    }
    return box.value ?? .failure(BridgeError.failed("The bridge's worker ended without a result."))
}

// MARK: - Diagnostics (stderr, only when NAPKIN_MLX_DIAGNOSTICS=1)

/// The spike's measurements that the eight functions cannot carry (load time, MLX peak memory,
/// schema compile times, tokens per second). napkin never sets the variable.
enum Diagnostics {
    static let enabled = ProcessInfo.processInfo.environment["NAPKIN_MLX_DIAGNOSTICS"] == "1"

    static func log(_ line: @autoclosure () -> String) {
        guard enabled else { return }
        FileHandle.standardError.write(Data("napkin_mlx: \(line())\n".utf8))
    }

    static func logGeneration(_ kind: String, _ reply: GenerationReply, constraint: String?) {
        guard enabled else { return }
        let rate =
            reply.generationSeconds > 0
            ? String(format: "%.1f", Double(reply.generatedTokens) / reply.generationSeconds) : "-"
        var line =
            "generate \(kind): prompt \(reply.promptTokens) tokens, \(String(format: "%.2f", reply.promptSeconds)) s; "
            + "\(reply.generatedTokens) tokens in \(String(format: "%.2f", reply.generationSeconds)) s (\(rate) tok/s); "
            + "stop \(reply.stopReason); MLX peak \(gb(Memory.peakMemory))"
        if let constraint {
            line += "; constraint \(constraint)"
        }
        log(line)
    }

    static func ms(since start: Date) -> String {
        String(format: "%.0f ms", Date().timeIntervalSince(start) * 1000)
    }

    static func seconds(since start: Date) -> String {
        String(format: "%.2f s", Date().timeIntervalSince(start))
    }

    static func gb(_ bytes: Int) -> String {
        String(format: "%.2f GB", Double(bytes) / 1_000_000_000)
    }
}
