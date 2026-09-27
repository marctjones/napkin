// Every failure the bridge can catch, as a status and one sentence (mlx-runtime.md §1.4:
// "never a crash for anything the bridge can catch").

import CNapkinMlx
import Foundation

/// Facts about this build of the bridge.
enum Build {
    /// napkin_mlx_abi_version().
    static let abiVersion: Int32 = 1

    /// The mlx-swift-lm commit the bridge is built against (Package.swift's pin; a test checks it
    /// against Package.resolved). Reported in napkin_mlx_device_info.mlx_swift_lm_revision.
    static let mlxSwiftLmRevision = "ee673d6a71d76e67b532dc7eaf91d92edc3bb8bb"
}

/// A failure, as napkin sees it: a status other than OK and the sentence *error carries.
enum BridgeError: Error, Equatable {
    /// napkin passed something the bridge cannot use (a NULL, a zero max_tokens). ERROR.
    case argument(String)
    /// Anything else that went wrong, in the library's or napkin's words. ERROR.
    case failed(String)
    /// The cancel flag was set. CANCELLED.
    case cancelled
    /// A generation is already running on this model. BUSY.
    case busy
    /// No Metal device. NO_METAL.
    case noMetal
    /// Guided: max_tokens ran out before the grammar accepted. INCOMPLETE.
    case incomplete(maxTokens: Int)

    var status: napkin_mlx_status {
        switch self {
        case .argument, .failed: NAPKIN_MLX_ERROR
        case .cancelled: NAPKIN_MLX_CANCELLED
        case .busy: NAPKIN_MLX_BUSY
        case .noMetal: NAPKIN_MLX_NO_METAL
        case .incomplete: NAPKIN_MLX_INCOMPLETE
        }
    }

    var sentence: String {
        switch self {
        case .argument(let sentence), .failed(let sentence):
            sentence
        case .cancelled:
            BridgeError.sentence(for: NAPKIN_MLX_CANCELLED)
        case .busy:
            BridgeError.sentence(for: NAPKIN_MLX_BUSY)
        case .noMetal:
            BridgeError.sentence(for: NAPKIN_MLX_NO_METAL)
        case .incomplete(let maxTokens):
            "The reply used all \(maxTokens) tokens it was allowed before it matched the schema, so there is no proposal."
        }
    }

    /// The sentence each status carries when nothing more specific is known. Every non-OK status
    /// has one (a test enumerates them).
    static func sentence(for status: napkin_mlx_status) -> String {
        switch status {
        case NAPKIN_MLX_OK:
            "OK."
        case NAPKIN_MLX_ERROR:
            "The MLX bridge failed."
        case NAPKIN_MLX_CANCELLED:
            "Cancelled; nothing was produced."
        case NAPKIN_MLX_BUSY:
            "This model is already answering; it answers one request at a time."
        case NAPKIN_MLX_NO_METAL:
            "MLX found no Metal device on this Mac (a headless or virtualized session, or not Apple silicon)."
        case NAPKIN_MLX_INCOMPLETE:
            "The reply ran out of tokens before it matched the schema, so there is no proposal."
        default:
            "Unknown status \(status.rawValue)."
        }
    }
}

/// A Swift error as one sentence: the library's own words when it gave any. A plain Swift enum's
/// `localizedDescription` is "The operation couldn't be completed", which says nothing; its
/// `String(describing:)` names the case and its payload.
func describe(_ error: Error) -> String {
    if let bridge = error as? BridgeError {
        return bridge.sentence
    }
    if let localized = error as? LocalizedError, let description = localized.errorDescription {
        return description
    }
    return String(describing: error)
}
