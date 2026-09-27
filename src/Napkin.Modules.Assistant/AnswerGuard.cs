using System.Collections.Immutable;
using System.Text;

namespace Napkin.Modules.Assistant;

/// <summary>
/// One sentence of an answer, kept or refused by <see cref="AnswerGuard"/>.
/// </summary>
/// <param name="Text">The sentence as the model wrote it.</param>
/// <param name="Separator">The whitespace that followed it, so the answer keeps its line breaks.</param>
/// <param name="Unsupported">The number tokens napkin did not give it, as written, in order, without repeats.</param>
/// <param name="MissingReferences">The item numbers it referred to that the pack does not have.</param>
/// <param name="References">Every item number it referred to, in order, without repeats.</param>
public sealed record GuardedSentence(
    string Text,
    string Separator,
    ImmutableArray<string> Unsupported,
    ImmutableArray<int> MissingReferences,
    ImmutableArray<int> References)
{
    /// <summary>Whether the sentence stands: every number in it is in the pack and every item it refers to exists.</summary>
    public bool Kept => Unsupported.IsEmpty && MissingReferences.IsEmpty;

    /// <summary>What the note shows in its place: the sentence, or <see cref="AnswerGuard.Refusal"/>.</summary>
    public string Shown => Kept ? Text : AnswerGuard.Refusal(Unsupported, MissingReferences);
}

/// <summary>An answer after the guard: its sentences, each kept or refused.</summary>
/// <param name="Sentences">The sentences, in order.</param>
public sealed record GuardedAnswer(ImmutableArray<GuardedSentence> Sentences)
{
    /// <summary>The answer as the note shows it: kept sentences as written, refused ones replaced by their refusal, line breaks kept.</summary>
    public string Text
    {
        get
        {
            StringBuilder text = new();
            foreach (GuardedSentence sentence in Sentences)
            {
                text.Append(sentence.Shown).Append(sentence.Separator);
            }

            return text.ToString().TrimEnd();
        }
    }

    /// <summary>How many sentences were refused.</summary>
    public int Refused => Sentences.Count(sentence => !sentence.Kept);

    /// <summary>
    /// The items the kept sentences refer to, in order of first reference, without repeats: what the
    /// note renders under the answer, verbatim, as "From napkin".
    /// </summary>
    public ImmutableArray<int> References
        => [.. Sentences.Where(sentence => sentence.Kept).SelectMany(sentence => sentence.References).Distinct()];
}

/// <summary>
/// The answer guard (docs/design/llm-assistant.md §4.2): every number token in a text answer must
/// occur, normalised, in the context pack napkin built, and every item it refers to must exist;
/// a sentence that breaks either rule is refused, and the rest of the answer stands.
/// </summary>
/// <remarks>
/// <para>
/// It is what makes "the assistant never states a number napkin did not give it" true rather than
/// hoped for: the prompt asks, the guard enforces. It runs on every text reply, from every runtime.
/// Tokens and their normalisation are <see cref="NumberTokens"/>'s; the same reader runs over the
/// pack, so "in the pack" means "written in the pack in some form that folds to the same key".
/// </para>
/// <para>
/// A sentence ends at <c>.</c>, <c>!</c> or <c>?</c> followed by white space or the end, or at a
/// line break; so <c>R602.7(1)</c> and <c>3.5</c> stay whole. Item references written after the full
/// stop, "… empty. [4]", belong to the sentence before them.
/// </para>
/// </remarks>
public static class AnswerGuard
{
    /// <summary>Checks an answer against the pack it was given.</summary>
    /// <param name="answer">The model's text answer.</param>
    /// <param name="pack">The context pack the question was asked with.</param>
    public static GuardedAnswer Check(string answer, ContextPack pack)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(pack);

        ImmutableHashSet<string> given = pack.Keys;
        List<GuardedSentence> sentences = [];
        foreach ((string text, string separator) in Sentences(answer))
        {
            ImmutableArray<int> references = [.. NumberTokens.ReferencesIn(text).Distinct()];
            ImmutableArray<string> unsupported =
            [
                .. NumberTokens.In(text)
                    .Where(token => !given.Contains(token.Key))
                    .Select(token => token.Text)
                    .Distinct(StringComparer.Ordinal),
            ];
            ImmutableArray<int> missing = [.. references.Where(n => pack.Item(n) is null)];
            sentences.Add(new GuardedSentence(text, separator, unsupported, missing, references));
        }

        return new GuardedAnswer([.. sentences]);
    }

    /// <summary>
    /// What a refused sentence becomes on the note: "[one sentence refused: it said 5'-6″, which
    /// napkin did not give it]".
    /// </summary>
    /// <param name="tokens">The numbers it said that are not in the pack, as written.</param>
    /// <param name="references">The items it referred to that do not exist.</param>
    public static string Refusal(IEnumerable<string> tokens, IEnumerable<int> references)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(references);

        List<string> said = [.. tokens];
        List<string> referred = [.. references.Select(n => $"[{n}]")];
        List<string> parts = [];
        if (said.Count > 0)
        {
            parts.Add("said " + Listed(said));
        }

        if (referred.Count > 0)
        {
            parts.Add("referred to " + Listed(referred));
        }

        return $"[one sentence refused: it {string.Join(" and ", parts)}, which napkin did not give it]";
    }

    /// <summary>The answer split into sentences, each with the white space after it.</summary>
    /// <param name="answer">The answer.</param>
    public static IEnumerable<(string Text, string Separator)> Sentences(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        List<(string Text, string Separator)> found = [];
        string text = answer.Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = 0;
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int end = -1;
            if (c == '\n')
            {
                end = i;
            }
            else if (c is '.' or '!' or '?')
            {
                // Closing punctuation belongs to the sentence: `."`, `.)`, `?!`.
                int j = i + 1;
                while (j < text.Length && text[j] is '.' or '!' or '?' or '"' or '\'' or ')' or ']' or '”' or '’' or '*' or '_')
                {
                    j++;
                }

                if (j == text.Length || char.IsWhiteSpace(text[j]))
                {
                    end = j;
                }
                else
                {
                    i = j;
                    continue;
                }
            }

            if (end < 0)
            {
                i++;
                continue;
            }

            int after = end;
            while (after < text.Length && char.IsWhiteSpace(text[after]))
            {
                after++;
            }

            Add(found, text[start..end], text[end..after]);
            start = after;
            i = after;
        }

        if (start < text.Length)
        {
            Add(found, text[start..], string.Empty);
        }

        return found;
    }

    private static void Add(List<(string Text, string Separator)> found, string sentence, string separator)
    {
        // Only a line break before any sentence at all can leave nothing here; it is not shown.
        string trimmed = sentence.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        // "[4] [6]" standing alone after a full stop refers back to the sentence before it.
        if (found.Count > 0 && OnlyReferences(trimmed))
        {
            (string text, string before) = found[^1];
            found[^1] = (text + before + trimmed, separator);
            return;
        }

        found.Add((trimmed, separator));
    }

    private static bool OnlyReferences(string text)
    {
        string rest = System.Text.RegularExpressions.Regex.Replace(text, @"\[\s*\d+(?:\s*,\s*\d+)*\s*\]", string.Empty);
        return rest.Length < text.Length && rest.All(c => char.IsWhiteSpace(c) || c is '.' or ',' or ';');
    }

    private static string Listed(List<string> items) => items.Count switch
    {
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };
}
