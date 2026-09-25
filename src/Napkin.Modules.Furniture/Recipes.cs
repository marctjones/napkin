using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// How many fasteners a joint takes when nobody has typed a count: napkin's own design defaults
/// (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;7.2), <strong>not sourced facts</strong>. They are
/// practice conventions chosen for the design, shown as "recipe: n" and overridden per joint.
/// </summary>
public static class Recipes
{
    /// <summary>
    /// The count for one joint: its own typed count, or the recipe's for its fastening and the joint's length.
    /// </summary>
    /// <param name="joint">The joint.</param>
    /// <param name="jointLength">The contact rectangle's long side (&#xA7;4.2).</param>
    public static int Count(Joint joint, Length jointLength)
    {
        ArgumentNullException.ThrowIfNull(joint);

        return joint.Fastening.Count ?? Recipe(joint.Fastening.Kind, jointLength);
    }

    /// <summary>
    /// The recipe's count for a fastening on a joint of a given length: at least the minimum, and
    /// otherwise the length divided by the spacing, rounded up (&#xA7;7.2). <see cref="FasteningKind.None"/> is none.
    /// </summary>
    /// <param name="kind">The fastening.</param>
    /// <param name="jointLength">The joint's length.</param>
    public static int Recipe(FasteningKind kind, Length jointLength)
    {
        // (least, one per this many units of joint length): 2 in, 6 in, 1 1/2 in, 4 in, 4 in, 8 in, 12 in.
        (int Least, long Spacing) = kind switch
        {
            FasteningKind.PocketScrews => (2, 2048L),
            FasteningKind.Screws => (2, 6144L),
            FasteningKind.Brads => (2, 1536L),
            FasteningKind.Nails => (2, 4096L),
            FasteningKind.Dowels => (2, 4096L),
            FasteningKind.Biscuits => (1, 8192L),
            FasteningKind.Clips => (2, 12288L),
            _ => (0, 1L),
        };

        if (kind == FasteningKind.None)
        {
            return 0;
        }

        long spaced = (jointLength.Units + Spacing - 1) / Spacing;
        return (int)Math.Max(Least, spaced);
    }

    /// <summary>The kind of fastener a fastening is made of, or null for none (&#xA7;7.2).</summary>
    /// <param name="kind">The fastening.</param>
    public static FastenerKind? FastenerOf(FasteningKind kind) => kind switch
    {
        FasteningKind.PocketScrews => FastenerKind.PocketScrew,
        FasteningKind.Screws => FastenerKind.WoodScrew,
        FasteningKind.Brads => FastenerKind.Brad,
        FasteningKind.Nails => FastenerKind.Nail,
        FasteningKind.Dowels => FastenerKind.Dowel,
        FasteningKind.Biscuits => FastenerKind.Biscuit,
        FasteningKind.Clips => FastenerKind.TabletopClip,
        _ => null,
    };

    /// <summary>Whether a kind of fastener's size depends on the thickness it is fastened through (&#xA7;7.3): every kind but the tabletop clip.</summary>
    /// <param name="kind">The fastener.</param>
    public static bool DependsOnThickness(FastenerKind kind) => kind != FastenerKind.TabletopClip;
}
