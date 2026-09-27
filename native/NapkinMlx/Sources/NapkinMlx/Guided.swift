// Schema-constrained generation's per-model state: the grammar tokenizer, the logit biases, and
// the compiled constraints for napkin's schemas (mlx-runtime.md §2).
//
// The note planned "compile once per schema, clone() per request". At the pinned commit that is
// impossible: GrammarConstraint.clone() calls the shim's xg_matcher_fork, which always fails —
// "GrammarMatcher::Fork() was introduced in xgrammar v0.1.34. This build is pinned to v0.1.30"
// (Libraries/MLXCXGrammar/shim.cc) — and each GrammarConstraint builds its own GrammarCompiler, so
// nothing compiled is shared between constraints. So, per schema:
//   - load compiles one constraint and keeps it unused; the first request takes it (the note's
//     pre-warm: the first proposal pays nothing);
//   - every later request compiles its own — about 40 ms for either napkin schema over Qwen3's
//     151,669-token vocabulary (Release build, measured for the slice A spike), a couple of tokens'
//     time, so nothing is compiled ahead in the background;
//   - if clone() works (a re-pin with xgrammar >= v0.1.34), the compiled constraint becomes a
//     template cloned per request instead — the note's plan, unchanged.
// That is MLXFoundationModels' own fallback (ModelCache.makeConstraint: clone, and on forkFailed
// compile fresh), plus the one pre-compiled constraint.

import Foundation
import MLX
import MLXGuidedGeneration
import MLXLMCommon

/// A constraint handed to one request, and where it came from (for the diagnostics).
struct TakenConstraint: @unchecked Sendable {
    let constraint: GrammarConstraint
    let provenance: String
}

final class GuidedEngine: @unchecked Sendable {
    let grammarTokenizer: GrammarTokenizer
    let hostTokenizer: any MLXLMCommon.Tokenizer
    /// MLXFoundationModels' zone policy (ClosingTokenBias / WhitespaceTokenBias): near the token
    /// budget the loop favours closing tokens, and runs of whitespace-only tokens are penalised —
    /// the usual way argmax-under-grammar runs out of tokens (INCOMPLETE).
    let closingBias: MLXArray
    let whitespaceBias: MLXArray
    let whitespaceTokenIDs: Set<Int>
    private(set) var summary: String

    var vocabSize: Int { grammarTokenizer.vocabSize }

    private final class Slot {
        let structuralReserve: Int
        var template: GrammarConstraint?
        var unused: GrammarConstraint?

        init(structuralReserve: Int) {
            self.structuralReserve = structuralReserve
        }
    }

    private let lock = NSLock()
    private var slots: [String: Slot] = [:]

    init(tokenizer: any MLXLMCommon.Tokenizer) throws {
        let started = Date()
        let grammar = try GuidedEngine.grammarTokenizer(for: tokenizer)
        grammarTokenizer = grammar.tokenizer
        hostTokenizer = tokenizer
        closingBias = ClosingTokenBias.compute(tokenizer: tokenizer, eosTokenId: tokenizer.eosTokenId)
        (whitespaceBias, whitespaceTokenIDs) = WhitespaceTokenBias.compute(tokenizer: tokenizer)
        summary =
            "grammar tokenizer (\(grammar.tokenizer.vocabSize) tokens, \(grammar.vocabType)) and biases in \(Diagnostics.ms(since: started))"
    }

    /// The vocabulary in xgrammar's shape (TokenizerVocabExtractor) and the grammar tokenizer over
    /// it — the README's steps 1–2.
    static func grammarTokenizer(for tokenizer: any MLXLMCommon.Tokenizer) throws
        -> (tokenizer: GrammarTokenizer, vocabType: VocabType)
    {
        let vocab = TokenizerVocabExtractor.extractForGrammar(from: tokenizer)
        let grammar = try GrammarTokenizer(
            vocab: vocab.vocab, vocabType: vocab.vocabType,
            eosTokenId: Int32(tokenizer.eosTokenId ?? 0))
        return (grammar, vocab.vocabType)
    }

    /// Step 3: one constraint for one schema. `fastForward: true` is what GuidedGenerationLoop
    /// requires ("The xgrammar constraint (must have `fastForward: true`)").
    static func compile(
        _ schema: String, grammar: GrammarTokenizer, host: any MLXLMCommon.Tokenizer
    ) throws -> GrammarConstraint {
        do {
            return try GrammarConstraint(
                tokenizer: grammar, jsonSchema: schema, fastForward: true, hostTokenizer: host)
        } catch {
            throw BridgeError.failed("The JSON schema did not compile into a grammar: \(describe(error))")
        }
    }

    private func compileTimed(_ schema: String) throws -> (GrammarConstraint, TimeInterval) {
        let started = Date()
        let constraint = try GuidedEngine.compile(schema, grammar: grammarTokenizer, host: hostTokenizer)
        return (constraint, Date().timeIntervalSince(started))
    }

    private func slot(for schema: String) -> Slot {
        lock.withLock {
            if let slot = slots[schema] { return slot }
            let slot = Slot(
                structuralReserve: CompletionReserve.estimate(schemaJSON: schema, tokenizer: hostTokenizer))
            slots[schema] = slot
            return slot
        }
    }

    /// Compiles each schema once at load, so the first proposal pays nothing (§2). A schema that
    /// does not compile is reported, not fatal: the model still answers in text, and a request
    /// with that schema gets the compile error as its ERROR sentence.
    func prewarm(_ schemas: [(name: String, text: String)]) {
        var parts = [summary]
        for (name, text) in schemas {
            let slot = slot(for: text)
            do {
                let (constraint, seconds) = try compileTimed(text)
                let fork: String
                do {
                    _ = try constraint.clone()
                    lock.withLock { slot.template = constraint }
                    fork = "clone() works"
                } catch {
                    lock.withLock { slot.unused = constraint }
                    fork = "clone() unsupported"
                }
                parts.append("\(name) schema compiled in \(String(format: "%.0f ms", seconds * 1000)) (\(fork))")
            } catch {
                parts.append("\(name) schema did not compile: \(describe(error))")
            }
        }
        summary = parts.joined(separator: "; ")
    }

    /// A fresh constraint for one request: a clone of the template, else the unused pre-compiled
    /// one, else a compile.
    func constraint(for schema: String) throws -> TakenConstraint {
        let slot = slot(for: schema)
        let (template, unused): (GrammarConstraint?, GrammarConstraint?) = lock.withLock {
            let unused = slot.unused
            slot.unused = nil
            return (slot.template, unused)
        }
        if let template {
            return TakenConstraint(constraint: try template.clone(), provenance: "cloned")
        }
        if let unused {
            return TakenConstraint(constraint: unused, provenance: "pre-compiled at load")
        }
        let (constraint, seconds) = try compileTimed(schema)
        return TakenConstraint(
            constraint: constraint,
            provenance: "compiled for this request in \(String(format: "%.0f ms", seconds * 1000))")
    }

    /// The loop's reserves, as MLXFoundationModels computes them (MLXLanguageModel.swift,
    /// runSchemaGeneration): soft zone max(3 × the schema's minimal completion, a quarter of the
    /// budget), hard zone 8 × the minimal completion; clamped to the budget.
    func budget(for schema: String, maxTokens: Int) -> (completionReserve: Int, hardReserve: Int) {
        let structural = slot(for: schema).structuralReserve
        return (
            min(max(structural * 3, maxTokens / 4), maxTokens),
            min(structural * 8, maxTokens)
        )
    }
}
