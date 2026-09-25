using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// The stock a rough part's sizes are nearest, for Firm up to suggest and a person to accept
/// (<c>docs/design/sketch-mode.md</c> &#xA7;3.3). A suggestion is never applied on its own.
/// </summary>
/// <remarks>
/// A candidate is any library item <see cref="StockAssignment.Fixes"/> reports at least one
/// dimension for, where every fixed dimension is within <see cref="Tolerance"/> of the part's
/// finished value for that dimension name. Best first: items that fix more dimensions (a 2x4
/// claims the width and the thickness, a panel the thickness only, and a panel would otherwise
/// win every thin strip by matching one number), then the smallest sum of absolute differences
/// over the fixed dimensions, then the name, ordinal. Fasteners fix nothing and never appear;
/// species is never suggested. The values are the library's, read at run time.
/// </remarks>
public static class StockSuggestion
{
    /// <summary>How far a fixed dimension may be from the part's: one inch.</summary>
    public static readonly Length Tolerance = Length.Inches(1);

    /// <summary>Stock items near the part's finished sizes, best first; empty when nothing is near.</summary>
    /// <param name="size">The part's finished length, width and thickness.</param>
    /// <param name="library">The materials library.</param>
    public static ImmutableArray<StockItem> For(FinishedSize size, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        List<(StockItem Item, int Fixed, long Difference)> candidates = [];
        foreach (StockItem item in library.Items)
        {
            ImmutableArray<FixedDimension> fixes = StockAssignment.Fixes(item);
            if (fixes.IsEmpty)
            {
                continue;
            }

            long difference = 0;
            bool near = true;
            foreach (FixedDimension fixedDimension in fixes)
            {
                Length off = Length.Abs(ValueOf(size, fixedDimension.Dimension) - fixedDimension.Value);
                if (off > Tolerance)
                {
                    near = false;
                    break;
                }

                difference += off.Units;
            }

            if (near)
            {
                candidates.Add((item, fixes.Length, difference));
            }
        }

        return
        [
            .. candidates
                .OrderByDescending(candidate => candidate.Fixed)
                .ThenBy(candidate => candidate.Difference)
                .ThenBy(candidate => candidate.Item.Name, StringComparer.Ordinal)
                .Select(candidate => candidate.Item),
        ];
    }

    private static Length ValueOf(FinishedSize size, PartDimension dimension) => dimension switch
    {
        PartDimension.Length => size.Length,
        PartDimension.Width => size.Width,
        _ => size.Thickness,
    };
}
