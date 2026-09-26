using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// A strut's compound ends compared up to a half-turn of the board about any of its own three axes
/// (<c>docs/design/angled-parts.md</c> &#xA7;2.5): end for end, face for face, edge for edge. The four
/// legs of a symmetric magazine stool put their long points at four different corner names, and each
/// is another turned over, so they are one row of four. Mirror images no half-turn relates stay apart.
/// </summary>
/// <remarks>
/// The same idea <see cref="JointSequence"/> applies to a part's joinery. It is used only for a
/// blank with no plain cuts: a plain mitre's site would turn with it, and the plain cuts are compared
/// as they are, as a box's are.
/// </remarks>
internal static class HalfTurns
{
    /// <summary>The one of the four turnings that sorts first, west end first: equal for any two boards that are one board turned.</summary>
    internal static ImmutableArray<DerivedCompoundEnd> Canonical(ImmutableArray<DerivedCompoundEnd> ends)
    {
        if (ends.IsDefaultOrEmpty)
        {
            return [];
        }

        ImmutableArray<DerivedCompoundEnd>[] turnings =
        [
            ends,
            Turned(ends, swapEnds: false, flipY: true, flipZ: true),  // about its length
            Turned(ends, swapEnds: true, flipY: false, flipZ: true),  // about its width
            Turned(ends, swapEnds: true, flipY: true, flipZ: false),  // about its thickness
        ];

        return turnings.OrderBy(Key, StringComparer.Ordinal).First();
    }

    private static ImmutableArray<DerivedCompoundEnd> Turned(ImmutableArray<DerivedCompoundEnd> ends, bool swapEnds, bool flipY, bool flipZ)
        =>
        [
            .. ends
                .Select(end => end with
                {
                    End = swapEnds ? (end.End == BlankEnd.West ? BlankEnd.East : BlankEnd.West) : end.End,
                    LongPoint = new StrutCorner(flipY ? -end.LongPoint.Y : end.LongPoint.Y, flipZ ? -end.LongPoint.Z : end.LongPoint.Z),
                })
                .OrderBy(end => end.End),
        ];

    private static string Key(ImmutableArray<DerivedCompoundEnd> ends)
        => string.Join(
            ";",
            ends.Select(end => string.Create(
                CultureInfo.InvariantCulture,
                $"{end.End}|{end.Mitre.Degrees:R}|{end.Mitre.Exact}|{end.Bevel.Degrees:R}|{end.Bevel.Exact}|{end.LongPoint.Y}|{end.LongPoint.Z}")));
}
