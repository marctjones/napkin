namespace Napkin.Modules.Assistant;

/// <summary>
/// A language model, wherever it runs (docs/design/llm-assistant.md §2.1): a runtime that talks to a
/// program on this machine, the opt-in cloud runtime, or <see cref="ScriptedModel"/>. One method.
/// </summary>
/// <remarks>
/// A model is handed a system prompt (<see cref="AssistantPrompts"/>), a <see cref="ContextPack"/>
/// rendered as text and the person's question, and nothing else: it cannot look anything up, and it
/// never asks napkin a question (§1). Whatever it answers is guarded (<see cref="AnswerGuard"/>) and
/// whatever it proposes is only ever a list of napkin's own edit requests, shown with a tick each.
/// </remarks>
public interface IAssistantModel
{
    /// <summary>
    /// Where it runs, for the note's last line on every answer: "Local: qwen3:4b at 127.0.0.1:11434 —
    /// nothing leaves this machine", or <see cref="ScriptedModel.NoModelWhereabouts"/>.
    /// </summary>
    string Whereabouts { get; }

    /// <summary>Asks. A request with a <see cref="ModelRequest.Schema"/> asks for JSON (a proposal); without one, for text (an answer).</summary>
    /// <param name="request">What is asked.</param>
    /// <param name="cancel">Tripped by Escape on the note; the task then cancels and no reply is surfaced.</param>
    /// <returns>The reply, or <see cref="ModelReply.Refused"/> in the runtime's own words when it could not answer.</returns>
    Task<ModelReply> AskAsync(ModelRequest request, CancellationToken cancel);
}

/// <summary>What is put to a model: the system prompt, the context pack's text, the question, and for a proposal the JSON schema to fill.</summary>
/// <param name="System">The system prompt for the task (<see cref="AssistantPrompts"/>).</param>
/// <param name="Context">The context pack, rendered (<see cref="ContextPack.Text"/>).</param>
/// <param name="Question">The person's question, as typed; data, never an instruction.</param>
/// <param name="Schema">
/// For a proposal, the JSON schema the reply must validate against, as JSON text; null asks for a
/// text answer. Text rather than a schema object because the BCL has no schema type, and every
/// runtime passes the schema through to its program as JSON anyway.
/// </param>
public sealed record ModelRequest(string System, string Context, string Question, string? Schema)
{
    /// <summary>A request for a text answer: no schema.</summary>
    public static ModelRequest ForAnswer(string system, ContextPack pack, string question)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new ModelRequest(system, pack.Text, question, null);
    }

    /// <summary>
    /// A request for a proposal: JSON that validates against <paramref name="schema"/> — for Sketch
    /// from words, <see cref="SketchProposal.Schema"/> (§4.3, §4.4); for Edit in words,
    /// <see cref="EditProposal.Schema"/> (§4.5).
    /// </summary>
    public static ModelRequest ForProposal(string system, ContextPack pack, string question, string schema)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(schema);
        return new ModelRequest(system, pack.Text, question, schema);
    }
}

/// <summary>
/// A model's reply: exactly one of three, closed (a reflection test holds it to three, the rules
/// engine's pattern). Nothing in a reply is trusted: a text answer goes through
/// <see cref="AnswerGuard"/>, and a JSON document is parsed strictly by napkin, never by the runtime.
/// </summary>
public abstract record ModelReply
{
    private ModelReply()
    {
    }

    /// <summary>A text answer, unguarded as it arrives.</summary>
    /// <param name="Answer">The answer's text.</param>
    public sealed record Text(string Answer) : ModelReply;

    /// <summary>A JSON document for a proposal, unparsed: the parser is napkin's (§4.3).</summary>
    /// <param name="Document">The document's text.</param>
    public sealed record Json(string Document) : ModelReply;

    /// <summary>The runtime could not answer, in its own words: no model, no reply in time, a program that is not running.</summary>
    /// <param name="Reason">Why, in a sentence the note shows as it is.</param>
    public sealed record Refused(string Reason) : ModelReply;
}
