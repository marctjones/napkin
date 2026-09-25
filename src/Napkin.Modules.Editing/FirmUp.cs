using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Editing;

/// <summary>One rough part with no stock, and the stock its sizes are nearest (<c>docs/design/sketch-mode.md</c> &#xA7;3.3).</summary>
/// <param name="Part">The part.</param>
/// <param name="Sentence">"Part 3, 4'-0" × 3 1/2" × 1 1/2"", its three finished sizes.</param>
/// <param name="Candidates">The nearest three at most, best first; empty when nothing is near.</param>
public sealed record FirmUpStockLine(EntityId Part, string Sentence, ImmutableArray<StockItem> Candidates);

/// <summary>One rough part whose width or height nothing states (&#xA7;3.4): keep it as drawn.</summary>
/// <param name="Part">The part.</param>
/// <param name="Sentence">"Part 3, keep 4'-0" × 1'-0" as drawn".</param>
public sealed record FirmUpSizeLine(EntityId Part, string Sentence);

/// <summary>What Firm up offers for a set of parts: the three sections of its popover.</summary>
/// <param name="Relationships">What the touching parts imply (&#xA7;3.2).</param>
/// <param name="Stocks">Rough parts with no stock (&#xA7;3.3).</param>
/// <param name="Sizes">Rough parts with a size nothing states (&#xA7;3.4).</param>
public sealed record FirmUpPlan(
    ImmutableArray<FirmUpProposal> Relationships,
    ImmutableArray<FirmUpStockLine> Stocks,
    ImmutableArray<FirmUpSizeLine> Sizes)
{
    /// <summary>Whether there is nothing to offer.</summary>
    public bool IsEmpty => Relationships.IsEmpty && Stocks.IsEmpty && Sizes.IsEmpty;
}

/// <summary>What accepting Firm up did, for its one-line summary.</summary>
/// <param name="Relationships">How many relationships landed.</param>
/// <param name="Stocks">How many stocks were assigned.</param>
/// <param name="Sizes">How many parts had their drawn sizes stated.</param>
/// <param name="PartsFirmed">How many parts were rough and no longer are.</param>
/// <param name="Rejections">What the updater refused, in its own words; the rest still landed.</param>
public sealed record FirmUpOutcome(int Relationships, int Stocks, int Sizes, int PartsFirmed, ImmutableArray<string> Rejections)
{
    /// <summary>
    /// "Firmed up 4 parts: 4 relationships, 3 stocks, 1 size. Next: Join all touching." — the only
    /// mention of joinery (&#xA7;6.1).
    /// </summary>
    public string Summary
        => $"Firmed up {Count(PartsFirmed, "part")}: {Count(Relationships, "relationship")}, {Count(Stocks, "stock")}, {Count(Sizes, "size")}. Next: Join all touching.";

    private static string Count(int n, string word)
        => $"{n.ToString(CultureInfo.InvariantCulture)} {word}{(n == 1 ? string.Empty : "s")}";
}

/// <summary>
/// Firm up (<c>docs/design/sketch-mode.md</c> &#xA7;3): the relationships the touching parts imply,
/// the stock each rough part is nearest, and its drawn sizes stated — each offered, each accepted or
/// not, and the whole acceptance one undo step.
/// </summary>
public static class FirmUp
{
    /// <summary>The name of the undo step.</summary>
    public const string What = "Firm up";

    /// <summary>How many stock candidates a line offers.</summary>
    public const int CandidateCount = 3;

    /// <summary>
    /// The parts Firm up is about: the selected boxes, or with nothing selected every part on the sheet.
    /// </summary>
    public static ImmutableArray<EntityId> Scope(Sketch sketch, IReadOnlySet<EntityId> selection)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(selection);

        IEnumerable<Box> boxes = sketch.Entities.Values.OfType<Box>().Where(box => box.Part is not null);
        return [.. boxes.Where(box => selection.Count == 0 || selection.Contains(box.Id)).Select(box => box.Id).Order()];
    }

    /// <summary>What Firm up offers for <paramref name="parts"/>.</summary>
    public static FirmUpPlan Plan(Sketch sketch, IReadOnlyCollection<EntityId> parts, MaterialsLibrary library, Func<EntityId, string> nameOf, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(nameOf);
        ArgumentNullException.ThrowIfNull(format);

        Box[] rough = [.. parts
            .Distinct()
            .Order()
            .Select(sketch.Find<Box>)
            .OfType<Box>()
            .Where(box => box.Part is { Rough: true })];

        ImmutableArray<FirmUpStockLine> stocks =
        [
            .. rough
                .Where(box => box.Part!.Stock is null)
                .Select(box =>
                {
                    FinishedSize size = box.Part!.SizeOn(box);
                    string sizes = $"{Text(size.Length, format)} × {Text(size.Width, format)} × {Text(size.Thickness, format)}";
                    return new FirmUpStockLine(box.Id, $"{nameOf(box.Id)}, {sizes}", [.. StockSuggestion.For(size, library).Take(CandidateCount)]);
                }),
        ];

        ImmutableArray<FirmUpSizeLine> sizeLines =
        [
            .. rough
                .Where(box => Unstated(sketch, box).Any())
                .Select(box => new FirmUpSizeLine(box.Id, $"{nameOf(box.Id)}, keep {Text(box.Width, format)} × {Text(box.Height, format)} as drawn")),
        ];

        return new FirmUpPlan(FirmUpProposals.For(sketch, parts), stocks, sizeLines);
    }

    /// <summary>
    /// Accepts what was ticked, as one gesture and so one undo step: the relationships in order, then
    /// each chosen stock (which clears the part's rough mark and states what the yard fixes), then
    /// each part's remaining unstated plan sizes (clearing its mark). A rejected relationship is
    /// reported and the rest still land. The editor is left saying the summary.
    /// </summary>
    public static FirmUpOutcome Accept(
        DesignEditor editor,
        IEnumerable<FirmUpProposal> relationships,
        IEnumerable<(EntityId Part, StockItem Stock)> stocks,
        IEnumerable<EntityId> sizes)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(relationships);
        ArgumentNullException.ThrowIfNull(stocks);
        ArgumentNullException.ThrowIfNull(sizes);

        ImmutableArray<EntityId> roughBefore =
            [.. editor.Sketch.Entities.Values.OfType<Box>().Where(box => box.Part is { Rough: true }).Select(box => box.Id)];
        List<string> rejections = [];
        int related = 0, stocked = 0, sized = 0;

        editor.BeginGesture(What);

        foreach (FirmUpProposal proposal in relationships)
        {
            if (Landed(editor, new AddRelationship(proposal.Relationship), rejections))
            {
                related++;
            }
        }

        foreach ((EntityId id, StockItem stock) in stocks)
        {
            if (editor.Sketch.Find<Box>(id) is { Part: { } part } box
                && Landed(editor, StockAssignment.RequestsFor(editor.Sketch, box, part with { Stock = stock.Name, Rough = false }, stock), rejections))
            {
                stocked++;
            }
        }

        foreach (EntityId id in sizes)
        {
            if (editor.Sketch.Find<Box>(id) is not { Part: { } part } box)
            {
                continue;
            }

            List<Request> requests = [.. Unstated(editor.Sketch, box).Select(size => (Request)new AddRelationship(new ParamValue(RelationshipId.New(), size.Ref, size.Value)))];
            requests.Add(new SetPart(id, part with { Rough = false }));
            if (Landed(editor, Batch.Of([.. requests]), rejections))
            {
                sized++;
            }
        }

        editor.EndGesture();

        int firmed = roughBefore.Count(id => editor.Sketch.Find<Box>(id)?.Part is { Rough: false });
        FirmUpOutcome outcome = new(related, stocked, sized, firmed, [.. rejections]);
        editor.Say(rejections.Count == 0 ? EditSeverity.Done : EditSeverity.Problem, outcome.Summary);
        return outcome;
    }

    private static bool Landed(DesignEditor editor, Request request, List<string> rejections)
    {
        if (editor.Apply(request, What) is Succeeded)
        {
            return true;
        }

        rejections.Add(editor.LastMessage?.Text ?? "The updater refused it.");
        return false;
    }

    /// <summary>The box's two plan sizes that no <see cref="ParamValue"/> states, with their values now.</summary>
    private static IEnumerable<(ParamRef Ref, Length Value)> Unstated(Sketch sketch, Box box)
    {
        (ParamRef Ref, Length Value)[] plan = [(new BoxWidthRef(box.Id), box.Width), (new BoxHeightRef(box.Id), box.Height)];
        return plan.Where(size => DimensionEntry.DrivingRelationship(sketch, size.Ref) is null);
    }

    private static string Text(Length length, LengthFormat format) => length.Format(format).Text;
}
