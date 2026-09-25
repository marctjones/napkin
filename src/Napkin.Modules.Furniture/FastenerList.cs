using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>One joint's contribution to a fastener line: the arithmetic that makes the line traceable (&#xA7;7.4).</summary>
/// <param name="Joint">The joint.</param>
/// <param name="Inserted">The inserted part's name.</param>
/// <param name="Receiving">The receiving part's name.</param>
/// <param name="JointLength">The contact rectangle's long side, or null when the parts do not touch.</param>
/// <param name="Typed">Whether the count was typed on the joint rather than the recipe's.</param>
/// <param name="Each">Fasteners in one copy of the joint; 0 when there is neither a typed count nor a contact.</param>
/// <param name="Copies">How many copies of the inserted part there are (its quantity).</param>
public sealed record FastenerSource(RelationshipId Joint, string Inserted, string Receiving, Length? JointLength, bool Typed, int Each, int Copies)
{
    /// <summary>What this joint adds to the line.</summary>
    public int Count => Each * Copies;
}

/// <summary>One line of the fastener list (&#xA7;7.4): a kind, the thickness it goes through, the builder's size text and how many.</summary>
/// <param name="Kind">The fastener.</param>
/// <param name="Thickness">The inserted part's thickness, or null for a kind that does not depend on it.</param>
/// <param name="SizeText">The builder's typed size, empty when none has been chosen.</param>
/// <param name="Count">How many, over every joint.</param>
/// <param name="PackSize">Fasteners in a pack, or null.</param>
/// <param name="Sources">The joints that add up to <paramref name="Count"/>, in joint order.</param>
public sealed record FastenerRow(
    FastenerKind Kind,
    Length? Thickness,
    string SizeText,
    int Count,
    int? PackSize,
    ImmutableArray<FastenerSource> Sources)
{
    /// <summary>Packs to buy, when a pack size is set.</summary>
    public int? Packs => PackSize is { } pack ? (Count + pack - 1) / pack : null;

    /// <summary>"size not chosen" when no size is typed for this kind and thickness; a note about joints apart; otherwise empty.</summary>
    public string Note
    {
        get
        {
            List<string> notes = [];
            if (SizeText.Length == 0)
            {
                notes.Add("size not chosen");
            }

            int apart = Sources.Count(source => source is { JointLength: null, Typed: false });
            if (apart > 0)
            {
                notes.Add($"{apart} joint{(apart == 1 ? string.Empty : "s")} apart, not counted");
            }

            return string.Join("; ", notes);
        }
    }

    /// <summary>
    /// Which parts: "Apron, back &#x2192; Leg, north-west + Leg, north-east; Front rail &#x2192; ..." &#x2014; each inserted part
    /// once, first seen first, with the parts it is fastened to (&#xD7; n when there are several joints between the same two).
    /// </summary>
    public string For => string.Join(
        "; ",
        Sources.GroupBy(source => source.Inserted).Select(group =>
            group.Key + " \u2192 " + string.Join(
                " + ",
                group.GroupBy(source => source.Receiving).Select(to => to.Count() == 1 ? to.Key : $"{to.Key} \u00d7 {to.Count()}"))));

    /// <summary>The sum written out: "3 + 3 + 2 = 8".</summary>
    public string Derivation => string.Join(" + ", Sources.Select(source => source.Copies == 1 ? $"{source.Each}" : $"{source.Each}×{source.Copies}")) + $" = {Count}";
}

/// <summary>
/// The fasteners a design needs, derived from its joints by recipe and counted, never placed
/// (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;7). No size is ever napkin's: the size text is the
/// builder's typed choice for (kind, thickness), and a missing choice says "size not chosen".
/// </summary>
public static class FastenerList
{
    /// <summary>
    /// The fixed core of what the stock toolbox says about a fastener it cannot arm to draw with
    /// (#179): napkin counts and lists fasteners, but never places one on the drawing.
    /// </summary>
    public const string NotPlacedOnDrawing = "napkin does not place fasteners on the drawing yet";

    /// <summary>The typed choice for a kind of fastener in a thickness, or null when nothing is typed.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="kind">The fastener.</param>
    /// <param name="thickness">The inserted part's thickness, or null for a kind that does not depend on it.</param>
    public static FastenerChoice? ChoiceFor(Sketch sketch, FastenerKind kind, Length? thickness)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return sketch.FastenerChoices.FirstOrDefault(choice => choice.Kind == kind && choice.Thickness == thickness);
    }

    /// <summary>
    /// Whether a joint's fasteners are bought: when at least one of its parts is New
    /// (renovation-sketches §6.2). A joint between two existing parts is already fastened.
    /// </summary>
    public static bool Buys(Box inserted, Box receiving)
    {
        ArgumentNullException.ThrowIfNull(inserted);
        ArgumentNullException.ThrowIfNull(receiving);
        return inserted.Phase == Phase.New || receiving.Phase == Phase.New;
    }

    /// <summary>The list, by kind then thickness descending.</summary>
    /// <param name="sketch">The design.</param>
    public static ImmutableArray<FastenerRow> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        Dictionary<(FastenerKind Kind, long? Thickness), List<FastenerSource>> lines = [];
        foreach (Joint joint in sketch.RelationshipsInOrder.OfType<Joint>())
        {
            if (Recipes.FastenerOf(joint.Fastening.Kind) is not { } kind
                || sketch.Find<Box>(joint.Inserted.Box) is not { Part: { } part } inserted
                || sketch.Find<Box>(joint.Receiving.Box) is not { } receiving
                || !FastenerList.Buys(inserted, receiving))
            {
                continue;
            }

            Length? length = JointGeometry.Contact(sketch, joint)?.JointLength;
            int each = joint.Fastening.Count ?? (length is { } long_ ? Recipes.Count(joint, long_) : 0);
            int copies = part.Quantity;
            Length? thickness = Recipes.DependsOnThickness(kind) ? part.SizeOn(inserted).Thickness : null;

            (FastenerKind, long?) key = (kind, thickness?.Units);
            if (!lines.TryGetValue(key, out List<FastenerSource>? sources))
            {
                lines[key] = sources = [];
            }

            sources.Add(new FastenerSource(joint.Id, inserted.Name, receiving.Name, length, joint.Fastening.Count is not null, each, copies));
        }

        return
        [
            .. lines
                .OrderBy(line => line.Key.Kind)
                .ThenByDescending(line => line.Key.Thickness ?? long.MaxValue)
                .Select(line =>
                {
                    Length? thickness = line.Key.Thickness is { } units ? new Length(units) : null;
                    FastenerChoice? choice = ChoiceFor(sketch, line.Key.Kind, thickness);
                    return new FastenerRow(
                        line.Key.Kind,
                        thickness,
                        choice?.Size.Trim() ?? string.Empty,
                        line.Value.Sum(source => source.Count),
                        choice?.PackSize,
                        [.. line.Value]);
                }),
        ];
    }
}
