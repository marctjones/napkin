using System.Collections.Immutable;
using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// One set of direction words for both views (#81): the compass for the four sides, up and down
/// only for height, and each named by the way it faces <em>now</em>.
/// </summary>
/// <remarks>
/// <para>
/// The model names a part's faces in its own frame (<c>docs/design/assembly-model.md</c> &#xA7;1.5),
/// and so does the kernel's <see cref="PlaceRules.InWords"/>. That is the right record and the wrong
/// thing to read: once a board is stood on edge, its "top face" faces west. What a person needs is
/// the face they are looking at, so here each face of a feature is put through the part's
/// orientation first. The plan canvas already draws north up, so "south" is the bottom of the
/// page there; the words used to be the page's ("bottom edge", "left of"), which collided with the
/// 3D view's bottom — the underside.
/// </para>
/// <para>
/// An edge standing upright in the world is called a <em>corner</em>, the way the plan shows it and
/// the way a person says "the leg's north-west corner"; an edge lying level is "top south edge"; a
/// vertex is "top north-west corner".
/// </para>
/// </remarks>
public static class WorldWords
{
    /// <summary>A feature of a box, by the way its faces face now: "south face", "north-west corner".</summary>
    public static string Feature(Box? box, BoxFeature feature)
    {
        if (box is not { Orientation.IsExact: true })
        {
            return PlaceRules.InWords(feature);
        }

        ImmutableArray<(Axis Axis, bool Positive)> facing = [.. feature.Faces.Select(box.Orientation.Normal)];
        string? northSouth = Word(facing, Axis.Y);
        string? eastWest = Word(facing, Axis.X);
        string? cap = Word(facing, Axis.Z);
        string sides = string.Join("-", new[] { northSouth, eastWest }.OfType<string>());

        return facing.Length switch
        {
            1 => $"{sides}{cap} face",
            2 when cap is null => $"{sides} corner",
            2 => $"{cap} {sides} edge",
            3 => $"{cap} {sides} corner",
            _ => PlaceRules.InWords(feature),
        };
    }

    /// <summary>Which way a world direction is, as a word: east, west, north, south, top, bottom.</summary>
    public static string Facing(Axis axis, bool positive) => (axis, positive) switch
    {
        (Axis.X, true) => "east",
        (Axis.X, false) => "west",
        (Axis.Y, true) => "north",
        (Axis.Y, false) => "south",
        (Axis.Z, true) => "top",
        _ => "bottom",
    };

    /// <summary>A world axis, as the direction a size runs along it: "East–west", "North–south", "Up".</summary>
    public static string Along(Axis axis) => axis switch
    {
        Axis.X => "East–west",
        Axis.Y => "North–south",
        _ => "Up",
    };

    /// <summary>A signed distance along a world axis, as a preposition: "east of", "south of", "above".</summary>
    public static string Direction(Axis axis, Length distance) => axis switch
    {
        Axis.X => distance >= Length.Zero ? "east of" : "west of",
        Axis.Y => distance >= Length.Zero ? "north of" : "south of",
        Axis.Z => distance >= Length.Zero ? "above" : "below",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>A world axis, as a phrase for what is centred along it: "east to west".</summary>
    public static string Across(Axis axis) => axis switch
    {
        Axis.X => "east to west",
        Axis.Y => "north to south",
        Axis.Z => "top to bottom",
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>
    /// How a box is turned, in a few words the plan can print inside it (#82, assembly-model §7.2):
    /// which of its sizes stands up now — "↑ length" for a leg lying on its side — or "turned over";
    /// <see langword="null"/> for a box as drawn, which is the plan's ordinary case and needs no mark.
    /// </summary>
    /// <remarks>
    /// A part's sizes are its own dimension names through <see cref="Part.PlanAxes"/>, so a board
    /// stood on edge reads "↑ width"; a plain box's are width, height and depth.
    /// </remarks>
    public static string? Stance(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.FaceUp == BoxFace.Top || !box.Orientation.IsExact)
        {
            return null;
        }

        if (box.FaceUp == BoxFace.Bottom)
        {
            return "turned over";
        }

        Axis upright = box.FaceUp is BoxFace.North or BoxFace.South ? Axis.Y : Axis.X;
        string size = box.Part is { } part
            ? SceneWords.Of(upright == Axis.X ? part.PlanAxes.X : part.PlanAxes.Y).ToLowerInvariant()
            : upright == Axis.X ? "width" : "height";
        return $"↑ {size}";
    }

    static string? Word(ImmutableArray<(Axis Axis, bool Positive)> facing, Axis axis)
    {
        foreach ((Axis each, bool positive) in facing)
        {
            if (each == axis)
            {
                return Facing(each, positive);
            }
        }

        return null;
    }
}
