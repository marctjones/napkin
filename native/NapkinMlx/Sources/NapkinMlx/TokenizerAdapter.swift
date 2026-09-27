// swift-transformers' tokenizer behind mlx-swift-lm's Tokenizer / TokenizerLoader protocols.
//
// mlx-swift-lm 3.x is "decoupled from tokenizer and downloader packages" (its README); its
// using.md offers the protocols, an integration package, or the MLXHuggingFace macros. napkin
// implements the two protocols itself — the shape of the macro's expansion
// (Libraries/MLXHuggingFaceMacros/HuggingFaceIntegrationMacros.swift at the pinned commit) — so the
// macro target, and swift-syntax with it, are never compiled into the bridge, and no Downloader
// exists: the model folder is already on disk (napkin's consented download is in .NET, §6).

import Foundation
import MLXLMCommon
import Tokenizers

/// Loads the tokenizer from a local folder: `tokenizer.json` + `tokenizer_config.json` (and a
/// `chat_template.jinja` if present), read by `AutoTokenizer.from(modelFolder:)`. Nothing is fetched.
struct TransformersTokenizerLoader: MLXLMCommon.TokenizerLoader {
    func load(from directory: URL) async throws -> any MLXLMCommon.Tokenizer {
        TransformersTokenizer(try await AutoTokenizer.from(modelFolder: directory))
    }
}

/// One swift-transformers tokenizer, seen as an MLXLMCommon.Tokenizer.
struct TransformersTokenizer: MLXLMCommon.Tokenizer {
    private let upstream: any Tokenizers.Tokenizer

    init(_ upstream: any Tokenizers.Tokenizer) {
        self.upstream = upstream
    }

    func encode(text: String, addSpecialTokens: Bool) -> [Int] {
        upstream.encode(text: text, addSpecialTokens: addSpecialTokens)
    }

    // swift-transformers says decode(tokens:), mlx-swift-lm says decode(tokenIds:).
    func decode(tokenIds: [Int], skipSpecialTokens: Bool) -> String {
        upstream.decode(tokens: tokenIds, skipSpecialTokens: skipSpecialTokens)
    }

    func convertTokenToId(_ token: String) -> Int? {
        upstream.convertTokenToId(token)
    }

    func convertIdToToken(_ id: Int) -> String? {
        upstream.convertIdToToken(id)
    }

    var bosToken: String? { upstream.bosToken }
    var eosToken: String? { upstream.eosToken }
    var unknownToken: String? { upstream.unknownToken }

    func applyChatTemplate(
        messages: [[String: any Sendable]],
        tools: [[String: any Sendable]]?,
        additionalContext: [String: any Sendable]?
    ) throws -> [Int] {
        do {
            return try upstream.applyChatTemplate(
                messages: messages, tools: tools, additionalContext: additionalContext)
        } catch Tokenizers.TokenizerError.missingChatTemplate {
            // mlx-swift-lm's processor catches its own error type and falls back to plain text.
            throw MLXLMCommon.TokenizerError.missingChatTemplate
        }
    }
}
