using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>What a joint does to the wood (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;3.1).</summary>
public enum JointType
{
    /// <summary>One part's end or face against another's face; nothing is cut.</summary>
    Butt,

    /// <summary>A slot along (or across) the receiving part that the inserted panel's edge sits in.</summary>
    Groove,

    /// <summary>A step cut at the receiving part's end or edge that the inserted part's end sits in.</summary>
    Rabbet,

    /// <summary>Two parts overlap and half the thickness is cut from each, so they lie in one plane.</summary>
    HalfLap,

    /// <summary>A top held to an apron so it can move with the seasons.</summary>
    Tabletop,
}

/// <summary>What holds a joint together (&#xA7;3.2). Glue is a separate flag on <see cref="Joint"/>.</summary>
public enum FasteningKind
{
    /// <summary>Held by the joint itself.</summary>
    None,

    /// <summary>Screws driven through angled pocket holes in the inserted part.</summary>
    PocketScrews,

    /// <summary>Screws through one face into the other.</summary>
    Screws,

    /// <summary>Thin nails.</summary>
    Brads,

    /// <summary>Common or finish nails.</summary>
    Nails,

    /// <summary>Wooden pins in matched holes.</summary>
    Dowels,

    /// <summary>Compressed wooden ovals in slots.</summary>
    Biscuits,

    /// <summary>Tabletop fasteners: figure-8, Z-clip, screws in slotted holes.</summary>
    Clips,
}

/// <summary>What a fastener choice or a fastener-list row is about (&#xA7;7.2), in the order the list is sorted.</summary>
public enum FastenerKind
{
    /// <summary>A pocket screw.</summary>
    PocketScrew,

    /// <summary>A wood screw.</summary>
    WoodScrew,

    /// <summary>A brad.</summary>
    Brad,

    /// <summary>A nail.</summary>
    Nail,

    /// <summary>A dowel.</summary>
    Dowel,

    /// <summary>A biscuit.</summary>
    Biscuit,

    /// <summary>A tabletop clip.</summary>
    TabletopClip,
}

/// <summary>How a joint is held: the kind, how many (or the recipe's number), and where pocket holes are drilled from.</summary>
/// <param name="Kind">What holds it.</param>
/// <param name="Count">A typed count of fasteners, or <see langword="null"/> for the recipe's (&#xA7;7.2).</param>
/// <param name="PocketFace">For pocket screws only: the face of the inserted part the holes are drilled from.</param>
public sealed record Fastening(FasteningKind Kind, int? Count, BoxFace? PocketFace)
{
    /// <summary>Held by the joint itself.</summary>
    public static readonly Fastening None = new(FasteningKind.None, null, null);
}

/// <summary>
/// Two parts stuck together: a relationship between one face of each (&#xA7;4.1 c). Stored data the
/// updater neither propagates nor refuses because of (&#xA7;4.3); the geometry that a joint implies —
/// the contact, the groove's offset — is derived from the two boxes, never stored.
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Receiving">The face of the part that receives (exactly one face).</param>
/// <param name="Inserted">The face of the part that is inserted (exactly one face).</param>
/// <param name="Type">What the joint does to the wood.</param>
/// <param name="Depth">How deep a groove or rabbet is; required for those and null for the others.</param>
/// <param name="Fastening">What holds it.</param>
/// <param name="Glue">Whether it is glued.</param>
public sealed record Joint(
    RelationshipId Id,
    FeatureRef Receiving,
    FeatureRef Inserted,
    JointType Type,
    Length? Depth,
    Fastening Fastening,
    bool Glue) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Receiving.Box, Inserted.Box];
}

/// <summary>The rules a joint's own fields must obey (&#xA7;3.3, &#xA7;4.4), shared by the loader and the editor.</summary>
public static class JointRules
{
    /// <summary>Whether a type needs a depth.</summary>
    public static bool NeedsDepth(JointType type) => type is JointType.Groove or JointType.Rabbet;

    /// <summary>The fastenings a type may be held with (&#xA7;3.3).</summary>
    public static ImmutableArray<FasteningKind> AllowedFastenings(JointType type) => type switch
    {
        JointType.Butt =>
        [
            FasteningKind.None, FasteningKind.PocketScrews, FasteningKind.Screws, FasteningKind.Brads,
            FasteningKind.Nails, FasteningKind.Dowels, FasteningKind.Biscuits,
        ],
        JointType.Groove => [FasteningKind.None, FasteningKind.Brads],
        JointType.Rabbet => [FasteningKind.None, FasteningKind.Screws, FasteningKind.Brads, FasteningKind.Nails],
        JointType.HalfLap => [FasteningKind.None, FasteningKind.Screws, FasteningKind.Dowels],
        JointType.Tabletop => [FasteningKind.Clips],
        _ => [],
    };

    /// <summary>The face on the other side of a box from <paramref name="face"/>.</summary>
    public static BoxFace Opposite(BoxFace face) => face switch
    {
        BoxFace.South => BoxFace.North,
        BoxFace.North => BoxFace.South,
        BoxFace.East => BoxFace.West,
        BoxFace.West => BoxFace.East,
        BoxFace.Bottom => BoxFace.Top,
        _ => BoxFace.Bottom,
    };

    /// <summary>
    /// Everything wrong with a joint's own fields, in the words a refusal uses; empty when it is
    /// well formed. Whether the two faces touch is not judged here (&#xA7;4.4).
    /// </summary>
    public static IEnumerable<string> Errors(Joint joint)
    {
        ArgumentNullException.ThrowIfNull(joint);

        string who = $"Joint {joint.Id}";

        if (joint.Receiving.Feature.Faces.Length != 1)
        {
            yield return $"{who} \"receiving\" names {joint.Receiving.Feature.Faces.Length} faces; a joint takes exactly one face of each part.";
        }

        if (joint.Inserted.Feature.Faces.Length != 1)
        {
            yield return $"{who} \"inserted\" names {joint.Inserted.Feature.Faces.Length} faces; a joint takes exactly one face of each part.";
        }

        if (joint.Receiving.Box == joint.Inserted.Box)
        {
            yield return $"{who} joins a part to itself (\"receiving\" and \"inserted\" are one box); a joint is between two different parts.";
        }

        if (NeedsDepth(joint.Type))
        {
            if (joint.Depth is null)
            {
                yield return $"{who} is a {joint.Type} and needs a \"depth\"; it has none.";
            }
        }
        else if (joint.Depth is not null)
        {
            yield return $"{who} is a {joint.Type}, which has no \"depth\"; it says {joint.Depth}.";
        }

        if (joint.Depth is { } depth && depth <= Length.Zero)
        {
            yield return $"{who} has a \"depth\" of {depth}; a depth is greater than zero.";
        }

        Fastening fastening = joint.Fastening;
        if (!AllowedFastenings(joint.Type).Contains(fastening.Kind))
        {
            yield return $"{who} is a {joint.Type} held with \"fastening.kind\" {fastening.Kind}, which napkin does not allow for that type (design note §3.3).";
        }

        if (fastening.Count is { } count)
        {
            if (count < 1)
            {
                yield return $"{who} has a \"fastening.count\" of {count}; a count is at least 1, or null for the recipe.";
            }

            if (fastening.Kind == FasteningKind.None)
            {
                yield return $"{who} has a \"fastening.count\" but no fastening.";
            }
        }

        if (fastening.PocketFace is { } pocket)
        {
            if (fastening.Kind != FasteningKind.PocketScrews)
            {
                yield return $"{who} names a \"fastening.pocketFace\" but is not held with pocket screws.";
            }
            else if (joint.Inserted.Feature.Faces is [var own] && (pocket == own || pocket == Opposite(own)))
            {
                yield return $"{who} has \"fastening.pocketFace\" {pocket}, which is the inserted part's own contact face or the one opposite it.";
            }
        }
    }
}

/// <summary>What a person builds with typed in the fastener-choices panel: a size for a kind of fastener in a stock thickness (&#xA7;7.3).</summary>
/// <param name="Kind">Which fastener.</param>
/// <param name="Thickness">The thickness of the fastened-through part, or <see langword="null"/> for a kind that does not depend on it.</param>
/// <param name="Size">The builder's own text, never napkin's data.</param>
/// <param name="PackSize">How many come in a pack, or <see langword="null"/> for no pack arithmetic.</param>
public sealed record FastenerChoice(FastenerKind Kind, Length? Thickness, string Size, int? PackSize);

/// <summary>A line of the supplies checklist: typed text only (&#xA7;8).</summary>
/// <param name="Item">What to buy; not empty.</param>
/// <param name="Note">A note, possibly empty.</param>
public sealed record SupplyLine(string Item, string Note);

/// <summary>A counted item typed onto a part: a slide, a pull, a hinge (&#xA7;7.5).</summary>
/// <param name="Name">What it is.</param>
/// <param name="Quantity">How many per copy of the part; at least 1.</param>
public sealed record HardwareItem(string Name, int Quantity);
