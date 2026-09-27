// napkin-mlx-spike: slice A's spike harness (mlx-runtime.md §9, "the spike report").
//
// It dlopens libNapkinMlx.dylib by absolute path and calls the eight functions through dlsym —
// exactly what napkin's P/Invoke will do — so it exercises the real library, its signature, its
// dependencies and MLX's colocated-metallib lookup, not a statically linked copy. Without --model
// it measures what needs no weights; with --model <folder> it loads the model and asks it things.
//
// Run with NAPKIN_MLX_DIAGNOSTICS=1 to have the bridge print what the ABI cannot carry: load
// time, MLX's peak memory, the schema compile times, tokens per second.
//
//   out/napkin-mlx-spike                       # model-free: dlopen, device, init
//   out/napkin-mlx-spike --model <folder>      # plus load, a text answer, two proposals, BUSY, cancel

import CNapkinMlx
import Darwin
import Foundation
import NapkinMlxSchemas

// MARK: - Arguments

var dylibPath: String?
var modelFolder: String?
var arguments = CommandLine.arguments.dropFirst().makeIterator()
while let argument = arguments.next() {
    switch argument {
    case "--dylib": dylibPath = arguments.next()
    case "--model": modelFolder = arguments.next()
    case "-h", "--help":
        print("usage: napkin-mlx-spike [--dylib <libNapkinMlx.dylib>] [--model <mlx-community folder>]")
        exit(0)
    default:
        FileHandle.standardError.write(Data("unknown argument \(argument)\n".utf8))
        exit(2)
    }
}
let executableDirectory = URL(fileURLWithPath: CommandLine.arguments[0]).resolvingSymlinksInPath()
    .deletingLastPathComponent()
let dylib = URL(fileURLWithPath: dylibPath ?? executableDirectory.appendingPathComponent("libNapkinMlx.dylib").path)
    .standardizedFileURL.path
let metallib = URL(fileURLWithPath: dylib).deletingLastPathComponent().appendingPathComponent("mlx.metallib").path

// MARK: - Helpers

func now() -> Double { Date().timeIntervalSinceReferenceDate }
func ms(_ seconds: Double) -> String { String(format: "%.1f ms", seconds * 1000) }
func line(_ text: String) { print(text); fflush(stdout) }

/// The process's peak physical footprint (what Activity Monitor calls Memory), from the kernel.
func peakFootprint() -> String {
    var info = rusage_info_v4()
    let status = withUnsafeMutablePointer(to: &info) {
        $0.withMemoryRebound(to: rusage_info_t?.self, capacity: 1) {
            proc_pid_rusage(getpid(), RUSAGE_INFO_V4, $0)
        }
    }
    guard status == 0 else { return "unknown" }
    return String(format: "%.2f GB", Double(info.ri_lifetime_max_phys_footprint) / 1_000_000_000)
}

func statusName(_ status: napkin_mlx_status) -> String {
    switch status {
    case NAPKIN_MLX_OK: "OK"
    case NAPKIN_MLX_ERROR: "ERROR"
    case NAPKIN_MLX_CANCELLED: "CANCELLED"
    case NAPKIN_MLX_BUSY: "BUSY"
    case NAPKIN_MLX_NO_METAL: "NO_METAL"
    case NAPKIN_MLX_INCOMPLETE: "INCOMPLETE"
    default: "status \(status.rawValue)"
    }
}

func fixed<T>(_ field: T) -> String {
    withUnsafeBytes(of: field) { String(cString: $0.bindMemory(to: CChar.self).baseAddress!) }
}

// MARK: - dlopen and dlsym

line("napkin-mlx-spike")
line("dylib     \(dylib)")
line("metallib  \(metallib)")
var started = now()
guard let library = dlopen(dylib, RTLD_NOW | RTLD_LOCAL) else {
    line("dlopen FAILED: \(String(cString: dlerror()))")
    exit(1)
}
line("dlopen    \(ms(now() - started)) (cold: first load in this process)")

func symbol<T>(_ name: String, as type: T.Type) -> T {
    guard let address = dlsym(library, name) else {
        line("dlsym FAILED: \(name) is not exported")
        exit(1)
    }
    return unsafeBitCast(address, to: type)
}

typealias AbiVersionFn = @convention(c) () -> Int32
typealias InitFn = @convention(c) (UnsafePointer<CChar>?, UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?) -> napkin_mlx_status
typealias DeviceFn = @convention(c) (UnsafeMutablePointer<napkin_mlx_device_info>?, UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?) -> napkin_mlx_status
typealias LoadFn = @convention(c) (UnsafePointer<CChar>?, UnsafePointer<Int32>?, UnsafeMutablePointer<OpaquePointer?>?, UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?) -> napkin_mlx_status
typealias GenerateFn = @convention(c) (OpaquePointer?, UnsafePointer<CChar>?, UnsafePointer<CChar>?, UnsafePointer<CChar>?, Int32, Float, Float, Int32, UnsafePointer<Int32>?, UnsafeMutablePointer<UnsafeMutablePointer<napkin_mlx_result>?>?, UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>?) -> napkin_mlx_status
typealias ResultFreeFn = @convention(c) (UnsafeMutablePointer<napkin_mlx_result>?) -> Void
typealias StringFreeFn = @convention(c) (UnsafeMutablePointer<CChar>?) -> Void
typealias UnloadFn = @convention(c) (OpaquePointer?) -> Void

let abiVersion = symbol("napkin_mlx_abi_version", as: AbiVersionFn.self)
let initialize = symbol("napkin_mlx_init", as: InitFn.self)
let device = symbol("napkin_mlx_device", as: DeviceFn.self)
let load = symbol("napkin_mlx_load", as: LoadFn.self)
let generate = symbol("napkin_mlx_generate", as: GenerateFn.self)
let resultFree = symbol("napkin_mlx_result_free", as: ResultFreeFn.self)
let stringFree = symbol("napkin_mlx_string_free", as: StringFreeFn.self)
let unload = symbol("napkin_mlx_unload", as: UnloadFn.self)
line("dlsym     all eight functions found")

/// Calls a function taking `char** error`; returns the status and the freed sentence.
func call(_ body: (UnsafeMutablePointer<UnsafeMutablePointer<CChar>?>) -> napkin_mlx_status) -> (napkin_mlx_status, String?) {
    var error: UnsafeMutablePointer<CChar>?
    let status = body(&error)
    let sentence = error.map { String(cString: $0) }
    stringFree(error)
    return (status, sentence)
}

// MARK: - Model-free

line("abi       \(abiVersion())")

var info = napkin_mlx_device_info()
let (deviceStatus, _) = call { device(&info, $0) }
line(
    "device    \(statusName(deviceStatus)): has_metal \(info.has_metal), \(fixed(info.architecture)), "
        + String(format: "memory %.1f GiB, Metal recommends %.1f GiB", Double(info.memory_bytes) / 1_073_741_824, Double(info.recommended_working_set_bytes) / 1_073_741_824)
        + ", mlx-swift-lm \(fixed(info.mlx_swift_lm_revision))")

let (wrongStatus, wrongSentence) = call { initialize("/nonexistent/mlx.metallib", $0) }
line("init(bad) \(statusName(wrongStatus)): \(wrongSentence ?? "-")")

started = now()
let (initStatus, initSentence) = call { initialize(metallib, $0) }
line("init      \(statusName(initStatus)) in \(ms(now() - started)) (includes the Metal probe and one GPU evaluation)\(initSentence.map { ": " + $0 } ?? "")")
guard initStatus == NAPKIN_MLX_OK else {
    line("peak footprint \(peakFootprint())")
    exit(initStatus == NAPKIN_MLX_NO_METAL ? 4 : 1)
}
started = now()
let (againStatus, _) = call { initialize(metallib, $0) }
line("init x2   \(statusName(againStatus)) in \(ms(now() - started)) (a no-op)")

guard let modelFolder else {
    line("no --model: load, answers, proposals, BUSY and cancel are pending a model folder")
    line("peak footprint \(peakFootprint())")
    exit(0)
}

// MARK: - With a model

started = now()
var model: OpaquePointer?
let (loadStatus, loadSentence) = call { load(modelFolder, nil, &model, $0) }
line("load      \(statusName(loadStatus)) in \(String(format: "%.2f s", now() - started)); peak footprint \(peakFootprint())\(loadSentence.map { ": " + $0 } ?? "")")
guard loadStatus == NAPKIN_MLX_OK, let model else { exit(1) }

let system = "You are the assistant inside napkin, a free desktop app for designing furniture with real dimensions. Answer briefly."

struct Reply {
    var status: napkin_mlx_status
    var sentence: String?
    var text: String?
    var seconds: Double
    var detail: String
}

func ask(
    _ user: String, schema: String?, maxTokens: Int32, cancel: UnsafeMutablePointer<Int32>? = nil
) -> Reply {
    var result: UnsafeMutablePointer<napkin_mlx_result>?
    let started = now()
    let (status, sentence) = call {
        generate(model, system, user, schema, maxTokens, 0.2, 1.0, 0, cancel.map { UnsafePointer($0) }, &result, $0)
    }
    let seconds = now() - started
    var text: String?
    var detail = ""
    if let result {
        let r = result.pointee
        text = String(cString: r.text)
        let rate = r.generation_seconds > 0 ? Double(r.generated_tokens) / r.generation_seconds : 0
        detail = String(
            format: "prompt %d tokens in %.2f s, %d tokens in %.2f s (%.1f tok/s), stop_reason %d",
            r.prompt_tokens, r.prompt_seconds, r.generated_tokens, r.generation_seconds, rate, r.stop_reason)
        resultFree(result)
    }
    return Reply(status: status, sentence: sentence, text: text, seconds: seconds, detail: detail)
}

func report(_ label: String, _ reply: Reply) {
    line("\(label) \(statusName(reply.status)) in \(String(format: "%.2f s", reply.seconds)); \(reply.detail)\(reply.sentence.map { "; " + $0 } ?? "")")
    if let text = reply.text { line("    text: \(text)") }
}

report("text 1   ", ask("Reply with ok.", schema: nil, maxTokens: 32))
report("text 2   ", ask("In two sentences, what is a cut list?", schema: nil, maxTokens: 160))
report(
    "sketch 1 ",
    ask(
        "Description: a plank bench, 4 feet long and 16 inches tall: a top on two legs with a stretcher between.",
        schema: NapkinSchemas.sketch, maxTokens: 2048))
report(
    "edit 1   ",
    ask(
        "Context:\n[1] Leg (rough plank 2 x 16 x 3/4)\n[2] Top (rough plank 48 x 12 x 3/4)\n\nQuestion: make the legs 18 inches tall and rename the top to Seat.",
        schema: NapkinSchemas.edit, maxTokens: 2048))
report(
    "sketch 2 ",
    ask(
        "Description: a small side table, 18 inches square and 24 inches tall.",
        schema: NapkinSchemas.sketch, maxTokens: 2048))
report(
    "schema x ",
    ask(
        "Name one wood species.",
        schema: #"{"type":"object","properties":{"species":{"type":"string"}},"required":["species"]}"#,
        maxTokens: 64))

// BUSY: a second request while the first is generating.
let longAsk = "Write a long, detailed essay about the history of woodworking joinery, with many paragraphs."
let busyGroup = DispatchGroup()
var firstOfTwo: Reply?
busyGroup.enter()
DispatchQueue.global().async {
    firstOfTwo = ask(longAsk, schema: nil, maxTokens: 96)
    busyGroup.leave()
}
Thread.sleep(forTimeInterval: 0.5)
report("busy     ", ask("Reply with ok.", schema: nil, maxTokens: 8))
busyGroup.wait()
if let firstOfTwo { report("busy's 1st", firstOfTwo) }

// Cancel, on each path: set the flag after a second; measure how long the call takes to return.
for (label, schema) in [("cancel txt", nil as String?), ("cancel gd ", NapkinSchemas.sketch)] {
    let flag = UnsafeMutablePointer<Int32>.allocate(capacity: 1)
    flag.initialize(to: 0)
    var setAt = 0.0
    DispatchQueue.global().asyncAfter(deadline: .now() + 1.0) {
        setAt = now()
        napkin_mlx_internal_store_flag(flag, 1)
    }
    let reply = ask(
        schema == nil ? longAsk : "Description: a bookcase with twenty shelves, each one listed as its own part.",
        schema: schema, maxTokens: 1024, cancel: flag)
    let latency = setAt > 0 ? ms(now() - setAt) : "flag never set"
    report(label, reply)
    line("    returned \(latency) after the flag was set")
    flag.deallocate()
}

started = now()
unload(model)
line("unload    \(ms(now() - started))")
line("peak footprint \(peakFootprint())")
