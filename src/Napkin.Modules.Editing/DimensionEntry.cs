using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>What napkin made of what someone typed into a dimension.</summary>
public abstract record DimensionText
{
    private protected DimensionText()
    {
    }
}

/// <summary>The text was a length.</summary>
/// <param name="Value">The length it was.</param>
/// <param name="WasRounded">Whether it had to be moved onto the 1/1024&#x2033; grid to be stored.</param>
public sealed record ReadableLength(Length Value, bool WasRounded) : DimensionText;

/// <summary>
/// The text was not a length, and this says so in the words a person needs: what could not be
/// read, and what would work instead.
/// </summary>
/// <param name="Message">The whole explanation, ready to put on screen beside the field.</param>
public sealed record UnreadableText(string Message) : DimensionText;

/// <summary>
/// Turns what someone typed into a dimension into a length, or into an explanation.
/// </summary>
/// <remarks>
/// <para>
/// The reading is <see cref="Length.TryParse"/>'s and nothing else's: the app has no second
/// opinion about what <c>3' 4 1/2"</c> means (docs/design/geometry-model.md &#xA7;1.5). What this
/// adds is the sentence to show when the answer is no — GUI-DRAW-03 asks for text that says what
/// could not be read and gives an example that works, because "invalid input" tells a person
/// nothing they did not already know.
/// </para>
/// <para>
/// Nothing here touches a sketch. The request to make is <see cref="RequestFor"/>'s job, and it
/// is a request, not an edit.
/// </para>
/// </remarks>
public static class DimensionEntry
{
    /// <summary>Lengths that work, for the end of an explanation.</summary>
    public const string Examples = "3' 4 1/2\", 2'-6\", 40 3/4 or .75";

    /// <summary>Reads a typed dimension.</summary>
    public static DimensionText Interpret(string? text)
    {
        string typed = (text ?? string.Empty).Trim();

        if (typed.Length == 0)
        {
            return new UnreadableText($"Nothing was typed. A length looks like {Examples}.");
        }

        if (!Length.TryParse(typed, out Length value, out bool wasRounded))
        {
            return new UnreadableText(
                $"napkin could not read “{typed}” as a length.{MetricNote(typed)} "
                + $"Try {Examples}.");
        }

        if (value <= Length.Zero)
        {
            return new UnreadableText(
                $"A part cannot be {value.Format(LengthFormat.Default).Text} across. "
                + $"Type a length greater than zero, like {Examples}.");
        }

        return new ReadableLength(value, wasRounded);
    }

    /// <summary>
    /// The request that sets a size to a typed value.
    /// </summary>
    /// <remarks>
    /// Typing a size on a part that nothing drives is a stated fact, so it is
    /// <c>AddRelationship(ParamValue(…))</c>; if a <see cref="ParamValue"/> already owns that
    /// number, the same edit is <see cref="SetParameter"/> on it. That is the whole of
    /// design &#xA7;4.1's "one number, one owner" as the canvas sees it.
    /// </remarks>
    public static Request RequestFor(Sketch sketch, ParamRef size, Length value)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(size);

        return DrivingRelationship(sketch, size) is { } driving
            ? new SetParameter(driving, value)
            : new AddRelationship(new ParamValue(RelationshipId.New(), size, value));
    }

    /// <summary>The <see cref="ParamValue"/> that owns a size's number, or null when nothing does.</summary>
    public static RelationshipId? DrivingRelationship(Sketch sketch, ParamRef size)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(size);

        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (relationship is ParamValue value && value.Param == size)
            {
                return value.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// A note about metric, only when the text looks metric. napkin stores feet and inches
    /// (DESIGN.md &#xA7;10), and a person who typed <c>250mm</c> is owed that sentence rather than
    /// a shrug.
    /// </summary>
    static string MetricNote(string typed)
    {
        string lower = typed.ToLowerInvariant();
        bool metric = lower.EndsWith("mm", StringComparison.Ordinal)
                      || lower.EndsWith("cm", StringComparison.Ordinal)
                      || lower.EndsWith("m", StringComparison.Ordinal)
                      || lower.EndsWith("metres", StringComparison.Ordinal)
                      || lower.EndsWith("meters", StringComparison.Ordinal);

        return metric ? " napkin works in feet and inches, not millimetres or metres." : string.Empty;
    }
}
