using System.Text.RegularExpressions;

namespace Napkin.Core.Geometry;

/// <summary>A point on an image, in whole pixels from its top-left corner, x right and y down: the finest thing a person can point at.</summary>
/// <param name="X">Pixels right of the left edge.</param>
/// <param name="Y">Pixels down from the top edge.</param>
public readonly record struct Pixel(long X, long Y);

/// <summary>
/// A survey image under the site plan (docs/design/permit-set.md §5.4, format version 16): the asset
/// it shows, named by the SHA-256 of its bytes, and a two-point calibration — two points on the image
/// and the two places on the drawing they are, a typed distance apart. The image's placement (scale,
/// rotation, offset) is derived from them for drawing only; nothing snaps to it and nothing is traced
/// from it. The bytes live beside the sketch, never in it.
/// </summary>
/// <param name="Asset">The image's SHA-256, 64 lowercase hex digits.</param>
/// <param name="ImageA">The first calibration point on the image.</param>
/// <param name="ImageB">The second calibration point on the image.</param>
/// <param name="WorldA">Where the first point is on the drawing.</param>
/// <param name="WorldB">Where the second is: the typed distance from WorldA, along the direction the person clicked, rounded once.</param>
/// <param name="Distance">The distance typed between the two points.</param>
/// <param name="Name">The image's file name, for the sheet's sentence; display only.</param>
public sealed record SurveyUnderlay(string Asset, Pixel ImageA, Pixel ImageB, Point2 WorldA, Point2 WorldB, Length Distance, string Name);

/// <summary>The rules an underlay keeps (§5.4).</summary>
public static partial class SurveyUnderlayRules
{
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256();

    /// <summary>Whether a name is a SHA-256 as napkin writes one: 64 lowercase hex digits.</summary>
    public static bool IsHash(string? text) => text is not null && Sha256().IsMatch(text);

    /// <summary>Why this underlay is refused, in words, or null when it is fine.</summary>
    public static string? Refusal(SurveyUnderlay underlay)
    {
        ArgumentNullException.ThrowIfNull(underlay);
        if (!IsHash(underlay.Asset))
        {
            return "an underlay names its image by the SHA-256 of its bytes: 64 lowercase hex digits";
        }

        if (underlay.ImageA == underlay.ImageB || underlay.WorldA == underlay.WorldB)
        {
            return "an underlay is calibrated from two different points";
        }

        if (underlay.Distance <= Length.Zero)
        {
            return "the distance between the calibration points is longer than zero";
        }

        return underlay.Name.Trim().Length == 0 ? "an underlay says which file it came from" : null;
    }
}
