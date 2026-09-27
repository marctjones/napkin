using System.Collections.Immutable;

namespace Napkin.Modules.Assistant;

/// <summary>One reply in a <see cref="ScriptedModel"/>'s script.</summary>
/// <param name="Reply">What the model replies.</param>
/// <param name="Match">
/// Text the question must contain (ignoring case) for this reply to be used, or null to match any
/// question — a fixed sequence is a script of replies with no match.
/// </param>
/// <param name="Delay">How long the reply takes, so a workflow can watch the thinking line and cancel it; zero for at once.</param>
public sealed record ScriptedReply(ModelReply Reply, string? Match = null, TimeSpan Delay = default)
{
    /// <summary>A text answer.</summary>
    public static ScriptedReply Text(string answer, string? match = null, TimeSpan delay = default)
        => new(new ModelReply.Text(answer), match, delay);

    /// <summary>A JSON document, for a proposal.</summary>
    public static ScriptedReply Json(string document, string? match = null, TimeSpan delay = default)
        => new(new ModelReply.Json(document), match, delay);
}

/// <summary>
/// A model that replies from a script and records every request it received
/// (docs/design/llm-assistant.md §2.1). It is a legitimate implementation of the seam, not test code
/// in the product: the app uses it with no script for "no model configured", and the GUI suite and
/// the unit tests use it with one so no test loads weights or opens a socket.
/// </summary>
/// <remarks>
/// <para>
/// Each reply is used at most once. A question takes the first unused reply whose
/// <see cref="ScriptedReply.Match"/> it contains, or that has no match; a script of replies with no
/// match is therefore a fixed sequence. When nothing is left, the reply is
/// <see cref="ModelReply.Refused"/> with <see cref="ScriptRanOut"/>. A question asked and then
/// cancelled has still used its reply, as a real model would have spent its time on it.
/// </para>
/// <para>
/// With no script at all it is napkin's "no model" state: <see cref="Whereabouts"/> is
/// <see cref="NoModelWhereabouts"/> and every question is refused with <see cref="NoModelReason"/>.
/// </para>
/// </remarks>
public sealed class ScriptedModel : IAssistantModel
{
    /// <summary>The note's last line when no model is set up.</summary>
    public const string NoModelWhereabouts = "No model — Assistant → Where the model runs…";

    /// <summary>What a question gets when no model is set up.</summary>
    public const string NoModelReason = "No model is set up: Assistant → Where the model runs… says how to add one.";

    /// <summary>The note's last line for a scripted model, unless the script says otherwise.</summary>
    public const string ScriptedWhereabouts = "Scripted replies — nothing leaves this machine.";

    /// <summary>What a question gets once the script has no reply left for it.</summary>
    public const string ScriptRanOut = "The script has no reply left for this question.";

    private readonly object _gate = new();
    private readonly ImmutableArray<ScriptedReply> _script;
    private readonly bool[] _used;
    private ImmutableArray<ModelRequest> _requests = [];

    /// <summary>No model: nothing scripted, every question refused with <see cref="NoModelReason"/>.</summary>
    public ScriptedModel()
        : this([], NoModelWhereabouts)
    {
    }

    /// <summary>A model that replies from <paramref name="script"/>.</summary>
    /// <param name="script">The replies, in order.</param>
    public ScriptedModel(params ScriptedReply[] script)
        : this(script, ScriptedWhereabouts)
    {
    }

    /// <summary>A model that replies from <paramref name="script"/> and says it runs at <paramref name="whereabouts"/>.</summary>
    /// <param name="script">The replies, in order.</param>
    /// <param name="whereabouts">The note's last line.</param>
    public ScriptedModel(IEnumerable<ScriptedReply> script, string whereabouts)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(whereabouts);
        _script = [.. script];
        if (_script.Any(reply => reply is null))
        {
            throw new ArgumentException("A script has no empty entries.", nameof(script));
        }

        _used = new bool[_script.Length];
        Whereabouts = _script.IsEmpty ? NoModelWhereabouts : whereabouts;
    }

    /// <inheritdoc/>
    public string Whereabouts { get; }

    /// <summary>Whether this is the "no model" state: nothing was scripted at all.</summary>
    public bool IsNone => _script.IsEmpty;

    /// <summary>Every request received, in the order asked.</summary>
    public ImmutableArray<ModelRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests;
            }
        }
    }

    /// <summary>How many replies are still unused.</summary>
    public int Remaining
    {
        get
        {
            lock (_gate)
            {
                return _used.Count(used => !used);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ModelReply> AskAsync(ModelRequest request, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancel.ThrowIfCancellationRequested();

        ScriptedReply? next = null;
        lock (_gate)
        {
            _requests = _requests.Add(request);
            for (int i = 0; i < _script.Length; i++)
            {
                if (!_used[i] && (_script[i].Match is not { } match || request.Question.Contains(match, StringComparison.OrdinalIgnoreCase)))
                {
                    _used[i] = true;
                    next = _script[i];
                    break;
                }
            }
        }

        if (next is null)
        {
            return new ModelReply.Refused(IsNone ? NoModelReason : ScriptRanOut);
        }

        if (next.Delay > TimeSpan.Zero)
        {
            await Task.Delay(next.Delay, cancel).ConfigureAwait(false);
        }

        return next.Reply;
    }
}
