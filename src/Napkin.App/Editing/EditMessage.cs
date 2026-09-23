using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>How loudly a message should be shown.</summary>
public enum EditSeverity
{
    /// <summary>It worked. What happened, in one line.</summary>
    Done,

    /// <summary>It worked, and there is something worth knowing. Never an error.</summary>
    Hint,

    /// <summary>It did not happen, and this is why.</summary>
    Problem,
}

/// <summary>
/// What to show about the last edit: one sentence, what to highlight, and — when there is one —
/// the way out.
/// </summary>
/// <param name="Severity">How loudly to show it.</param>
/// <param name="Text">The message, in plain words.</param>
/// <param name="Highlight">Relationships the canvas should draw attention to.</param>
/// <param name="OfferToRemove">
/// A relationship the person could remove to resolve a conflict, or null when there is nothing to
/// offer.
/// </param>
/// <param name="OfferText">The wording of that offer, for a button.</param>
public sealed record EditMessage(
    EditSeverity Severity,
    string Text,
    ImmutableList<RelationshipId> Highlight,
    RelationshipId? OfferToRemove = null,
    string? OfferText = null)
{
    /// <summary>A message with nothing to highlight and nothing to offer.</summary>
    public static EditMessage Plain(EditSeverity severity, string text) =>
        new(severity, text, ImmutableList<RelationshipId>.Empty);
}

/// <summary>
/// Turns an <see cref="UpdateResult"/> into something a person can read — the whole of CVS-008.
/// </summary>
/// <remarks>
/// <para>
/// Every one of the four results has a place here, and they are not the same place.
/// <see cref="Solved"/> says what happened. <see cref="UnderConstrained"/> says what happened and
/// adds a hint, because a drawing that is still free to move is the normal state of a sketch and
/// never an error. <see cref="OverConstrained"/> did not happen: it carries the conflict report's
/// own summary, names the relationships that cannot all hold, and offers to remove one.
/// <see cref="Rejected"/> did not happen either, and says which of the refusals it was in words
/// rather than as an enum name.
/// </para>
/// <para>
/// Entity ids are replaced by the names the drawing shows, so a conflict reads "Shelf's width"
/// and not "01a0c67e". The summary comes from <c>Core.Geometry</c>; the substitution is the app's,
/// because the geometry kernel has no notion of a part name.
/// </para>
/// </remarks>
public static class EditMessages
{
    /// <summary>The message for a result.</summary>
    /// <param name="result">What the updater said.</param>
    /// <param name="what">What was attempted, as a sentence with no full stop: "Moved Shelf".</param>
    /// <param name="before">The sketch as it was, for describing relationships that still exist.</param>
    /// <param name="nameOf">What to call an entity.</param>
    /// <param name="format">How to write a length.</param>
    public static EditMessage For(
        UpdateResult result,
        string what,
        Sketch before,
        Func<EntityId, string> nameOf,
        LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(nameOf);

        return result switch
        {
            UnderConstrained under => new EditMessage(
                EditSeverity.Hint,
                $"{what}. {Freedom(under.Freedom, nameOf)}",
                ImmutableList<RelationshipId>.Empty),

            Solved => EditMessage.Plain(EditSeverity.Done, $"{what}."),

            OverConstrained over => Conflict(over.Conflict, what, before, nameOf, format),

            Rejected rejected => EditMessage.Plain(
                EditSeverity.Problem,
                $"{what} did not happen: {Refusal(rejected.Reason)}{Detail(rejected, before, nameOf)}"),

            _ => EditMessage.Plain(EditSeverity.Problem, $"{what} did not happen."),
        };
    }

    /// <summary>
    /// A conflict, in the report's own words plus the relationships that cannot all hold, and an
    /// offer to remove one of them.
    /// </summary>
    static EditMessage Conflict(
        ConflictReport report,
        string what,
        Sketch before,
        Func<EntityId, string> nameOf,
        LengthFormat format)
    {
        string summary = Rename(report.Summary, report.Entities, nameOf);

        List<Relationship> involved =
        [
            .. report.Relationships
                .Select(before.Find)
                .OfType<Relationship>(),
        ];

        string named = involved.Count == 0
            ? string.Empty
            : " These cannot all be true at once: "
              + string.Join(
                  " ",
                  involved.Select(relationship =>
                      RelationshipText.Describe(before, relationship, nameOf, format)));

        // The one to offer is the first the drawing can actually drop. A pin is offered like
        // anything else: unpinning a part is often exactly what the person meant to do.
        Relationship? removable = involved.FirstOrDefault();

        return new EditMessage(
            EditSeverity.Problem,
            $"{what} would not hold. {summary}{named}",
            [.. report.Relationships],
            removable?.Id,
            removable is null
                ? null
                : "Remove: " + RelationshipText.Describe(before, removable, nameOf, format));
    }

    static string Freedom(FreedomReport freedom, Func<EntityId, string> nameOf)
    {
        if (freedom.FreeEntities.IsEmpty)
        {
            return "Nothing is holding it in place yet.";
        }

        string parts = string.Join(", ", freedom.FreeEntities.OrderBy(id => id).Select(nameOf));
        return $"Nothing pins {parts} yet, so it can still be moved.";
    }

    /// <summary>
    /// Replaces the short entity ids the conflict report writes with the names the drawing shows.
    /// </summary>
    public static string Rename(string summary, IEnumerable<EntityId> entities, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(nameOf);

        string text = summary ?? string.Empty;
        foreach (EntityId id in entities)
        {
            text = text.Replace(id.ToString(), nameOf(id), StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>Why a request was refused, in words.</summary>
    public static string Refusal(RejectionReason reason) => reason switch
    {
        RejectionReason.UnknownEntity => "the part it named is not in this drawing any more.",
        RejectionReason.UnknownRelationship => "the relationship it named is not in this drawing any more.",
        RejectionReason.UnsupportedRelationship =>
            "this build cannot hold that kind of relationship yet. It is the direct updater's "
            + "rectilinear set for now; the solver (#28) adds the rest.",
        RejectionReason.NonPositiveSize => "a part has to be wider and taller than nothing.",
        RejectionReason.ReferenceDimension =>
            "that dimension only reports a measurement; nothing is driving it, so there is no "
            + "number to change. Make it a driving dimension first.",
        RejectionReason.DuplicateRelationship => "the drawing already says that.",
        RejectionReason.DuplicateEntity => "there is already a part with that id.",
        RejectionReason.DanglingReference => "it referred to something that is not there.",
        RejectionReason.RotationNotSupported => "this build turns parts by quarter turns only.",
        RejectionReason.OrientationWithRelationships =>
            "turning this part would change what its relationships mean. Remove them first.",
        RejectionReason.DrivenSize =>
            "a typed dimension owns that size, so dragging must not quietly override it. Edit the "
            + "dimension instead.",
        RejectionReason.CutSiteTaken =>
            "something is already cut at that corner or edge. One cut per site, and a curved edge "
            + "claims both of its corners.",
        RejectionReason.CutDoesNotFit =>
            "the cut does not fit the blank it is on. Make the blank bigger, or the cut smaller.",
        RejectionReason.NoSuchCut => "there is no cut there to take off.",
        RejectionReason.UnsupportedRequest => "this build does not do that yet.",
        RejectionReason.PlacesNotComparable =>
            "those two places do not lie along a common line or plane, so that relationship could never hold.",
        _ => "the drawing could not do it.",
    };

    /// <summary>
    /// The refusal's own sentence, which names the box and the site, with ids replaced by names.
    /// </summary>
    /// <remarks>
    /// A cut that does not fit is refused "with the site named"
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.2), and the kernel already writes that
    /// sentence into <see cref="Rejected.Detail"/> — "Box 01a0&#x2026;'s cut at the NorthEast
    /// corner reaches 4&#x2033; across the width, which does not fit the blank's 3 1/2&#x2033;".
    /// Dropping it would leave a person with a refusal that does not say which of eight cuts was
    /// the problem, so it is kept and the ids in it are renamed like any other report.
    /// </remarks>
    static string Detail(Rejected rejected, Sketch before, Func<EntityId, string> nameOf) =>
        rejected.Detail is { Message.Length: > 0 } detail
            ? " " + Rename(detail.Message, before.Entities.Keys, nameOf)
            : string.Empty;
}
