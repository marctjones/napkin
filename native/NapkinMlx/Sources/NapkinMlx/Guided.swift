// Schema-constrained generation's per-model state: the grammar tokenizer, the logit biases, and
// the compiled constraints for napkin's schemas (mlx-runtime.md §2).
//
// The note planned "compile once per schema, clone() per request". At the pinned commit that is
// impossible: GrammarConstraint.clone() calls the shim's xg_matcher_fork, which always fails —
// "GrammarMatcher::Fork() was introduced in xgrammar v0.1.34. This build is pinned to v0.1.30"
// (Libraries/MLXCXGrammar/shim.cc) — and each GrammarConstraint builds its own GrammarCompiler, so
// nothing compiled is shared between constraints. What the bridge does instead, per schema:
//   - at load, compile one constraint and keep it unused (the "spare"); the first request takes it;
//   - after a request took the spare, compile the next one in the background;
//   - a request that finds no spare compiles its own;
//   - if clone() ever works (a re-pin with xgrammar >= v0.1.34), the compiled constraint becomes a
//     template that is cloned per request instead — the note's plan, unchanged.
// That is MLXFoundationModels' own fallback (ModelCache.makeConstraint: clone, and on forkFailed
// compile fresh), plus the spare so the first proposal after a load pays nothing.

import Foundation
import MLX
import MLXGuidedGeneration
import MLXLMCommon

/// A constraint handed to one request, and where it came from (for the diagnostics).
struct TakenConstraint: @unchecked Sendable {
    let constraint: GrammarConstraint
    let schema: String
    let provenance: String
    let tookSpare: Bool
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
        var spare: GrammarConstraint?
        var replenishing = false

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

    /// Compiles each schema once at load, so the first proposal pays nothing (§2).
    func prewarm(_ schemas: [(name: String, text: String)]) throws {
        var parts = [summary]
        for (name, text) in schemas {
            let (constraint, seconds) = try compileTimed(text)
            let slot = Slot(
                structuralReserve: CompletionReserve.estimate(
                    schemaJSON: text, tokenizer: hostTokenizer))
            let fork: String
            do {
                _ = try constraint.clone()
                slot.template = constraint
                fork = "clone() works"
            } catch GrammarError.forkFailed {
                slot.spare = constraint
                fork = "clone() unsupported, kept as a spare"
            }
            lock.withLock { slots[text] = slot }
            parts.append("\(name) schema compiled in \(String(format: "%.0f ms", seconds * 1000)) (\(fork))")
        }
        summary = parts.joined(separator: "; ")
    }

    /// A fresh constraint for one request: a clone of the template, else the spare, else a compile.
    func constraint(for schema: String) throws -> TakenConstraint {
        lock.lock()
        let slot = slots[schema]
        if let template = slot?.template {
            lock.unlock()
            return TakenConstraint(
                constraint: try template.clone(), schema: schema, provenance: "cloned",
                tookSpare: false)
        }
        if let slot, let spare = slot.spare {
            slot.spare = nil
            lock.unlock()
            return TakenConstraint(
                constraint: spare, schema: schema, provenance: "the pre-compiled spare",
                tookSpare: true)
        }
        if slot == nil {
            slots[schema] = Slot(
                structuralReserve: CompletionReserve.estimate(schemaJSON: schema, tokenizer: hostTokenizer))
        }
        lock.unlock()
        let (constraint, seconds) = try compileTimed(schema)
        return TakenConstraint(
            constraint: constraint, schema: schema,
            provenance: "compiled for this request in \(String(format: "%.0f ms", seconds * 1000))",
            tookSpare: false)
    }

    /// After a request, compile the schema's next spare in the background unless one exists or is
    /// on its way. A failure leaves no spare; the next request compiles its own.
    func replenish(after taken: TakenConstraint) {
        let schema = taken.schema
        let start: Bool = lock.withLock {
            guard let slot = slots[schema], slot.template == nil, slot.spare == nil,
                !slot.replenishing
            else { return false }
            slot.replenishing = true
            return true
        }
        guard start else { return }
        Task.detached(priority: .utility) { [self] in
            let compiled = try? compileTimed(schema)
            lock.withLock {
                slots[schema]?.spare = compiled?.0
                slots[schema]?.replenishing = false
            }
            if let seconds = compiled?.1 {
                Diagnostics.log("replenished a spare constraint in \(String(format: "%.0f ms", seconds * 1000))")
            }
        }
    }

    /// The loop's reserves, as MLXFoundationModels computes them (MLXLanguageModel.swift,
    /// runSchemaGeneration): soft zone max(3 × the schema's minimal completion, a quarter of the
    /// budget), hard zone 8 × the minimal completion; clamped to the budget.
    func budget(for schema: String, maxTokens: Int) -> (completionReserve: Int, hardReserve: Int) {
        let structural =
            lock.withLock { slots[schema]?.structuralReserve }
            ?? CompletionReserve.estimate(schemaJSON: schema, tokenizer: hostTokenizer)
        return (
            min(max(structural * 3, maxTokens / 4), maxTokens),
            min(structural * 8, maxTokens)
        )
    }
}
