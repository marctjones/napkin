using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant;

/// <summary>
/// One line of a proposal sheet (docs/design/llm-assistant.md &#xA7;4.3): the sentence the person
/// reads beside its tick, and the requests ticking it stands for — napkin's own edit requests and
/// nothing else. A line napkin refused carries no request; its sentence says why.
/// </summary>
/// <param name="Sentence">What the line says: "Top: 48 × 2 × 3/4 at (0, 16)", or "Leg 1: refused, a size of 0".</param>
/// <param name="Requests">What accepting it puts to the updater, in order; empty for a refused line.</param>
public sealed record ProposalLine(string Sentence, ImmutableArray<Request> Requests)
{
    /// <summary>A line napkin refused: shown with its reason and no tick to give, and never applied.</summary>
    /// <param name="sentence">The refusal, in napkin's words.</param>
    public static ProposalLine Refusal(string sentence) => new(sentence, []);

    /// <summary>Whether napkin refused this line, so there is nothing to accept on it.</summary>
    public bool Refused => Requests.IsEmpty;
}

/// <summary>What accepting a proposal did, for the sheet's lines and the message bar.</summary>
/// <param name="Landed">How many ticked lines the updater accepted.</param>
/// <param name="Rejections">What the updater refused, in its own words; the rest still landed.</param>
/// <param name="Stale">Whether the design had changed since the plan was made, so nothing was applied.</param>
public sealed record ProposalOutcome(int Landed, ImmutableArray<string> Rejections, bool Stale);

/// <summary>
/// The proposal sheet (docs/design/llm-assistant.md &#xA7;4.3): a list of lines, all ticked, made
/// against one <see cref="Sketch"/>. Accepting is exactly Firm up's acceptance
/// (<see cref="FirmUp.Accept"/>): one gesture named <see cref="What"/>, each ticked line's requests put
/// to <see cref="DesignEditor.Apply"/> in order, a refusal reported in the updater's words while the
/// rest still land — one undo step for the whole acceptance.
/// </summary>
/// <param name="What">The undo step's name: "Assistant sketch" (or "Assistant edit").</param>
/// <param name="MadeAgainst">The design the plan was made against; accepting it against any other is refused (the stale rule).</param>
/// <param name="Lines">The sheet's lines, in the reply's order.</param>
public sealed record ProposalPlan(string What, Sketch MadeAgainst, ImmutableArray<ProposalLine> Lines)
{
    /// <summary>
    /// What accepting a plan made against a design that has since changed says (&#xA7;4.3): a model
    /// takes seconds and the person may draw meanwhile.
    /// </summary>
    public const string StaleText = "The design changed while the assistant was thinking — ask again.";

    /// <summary>
    /// Whether this plan was made against <paramref name="sketch"/>. <see cref="Sketch"/> is a value,
    /// so this is one equality — the comparison <see cref="DesignEditor.HasUnsavedChanges"/> uses.
    /// </summary>
    public bool IsFor(Sketch sketch) => MadeAgainst.Equals(sketch);

    /// <summary>
    /// Accepts the ticked lines as one gesture, and so one undo step named <see cref="What"/>. A plan
    /// made against another design is refused whole with <see cref="StaleText"/> and nothing is
    /// applied; a refused line or a line not on this sheet is never applied. The editor is left
    /// saying <paramref name="summary"/> of how many lines landed — a problem when any was rejected.
    /// </summary>
    /// <param name="editor">The editor to apply the lines through.</param>
    /// <param name="ticked">The lines the person left ticked.</param>
    /// <param name="summary">The message-bar line for a number of lines landed.</param>
    public ProposalOutcome Accept(DesignEditor editor, IEnumerable<ProposalLine> ticked, Func<int, string> summary)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(ticked);
        ArgumentNullException.ThrowIfNull(summary);

        if (!IsFor(editor.Sketch))
        {
            editor.Say(EditSeverity.Problem, StaleText);
            return new ProposalOutcome(0, [], Stale: true);
        }

        ProposalLine[] chosen = [.. ticked.Where(line => !line.Refused && Lines.Contains(line))];
        List<string> rejections = [];
        int landed = 0;

        editor.BeginGesture(What);
        foreach (ProposalLine line in chosen)
        {
            Request request = line.Requests.Length == 1 ? line.Requests[0] : Batch.Of([.. line.Requests]);
            if (editor.Apply(request, What) is Succeeded)
            {
                landed++;
            }
            else
            {
                // Apply always leaves a message, in the updater's own words (DesignEditor, CVS-008).
                rejections.Add(editor.LastMessage!.Text);
            }
        }

        editor.EndGesture();

        editor.Say(rejections.Count == 0 ? EditSeverity.Done : EditSeverity.Problem, summary(landed));
        return new ProposalOutcome(landed, [.. rejections], Stale: false);
    }
}
