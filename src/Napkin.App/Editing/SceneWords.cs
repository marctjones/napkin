using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// The words the properties panel offers for a part's three dimensions, and the reading of them.
/// </summary>
/// <remarks>
/// They are the file format's own three names with a capital letter on the front, because a person
/// picking "Thickness" from a list and a reader parsing <c>"thickness"</c> should not be able to
/// mean different things.
/// </remarks>
public static class SceneWords
{
    /// <summary>Length, width and thickness, in the order a cut list reads them.</summary>
    public static readonly string[] Dimensions = ["Length", "Width", "Thickness"];

    /// <summary>The word for one dimension.</summary>
    /// <param name="dimension">The dimension.</param>
    public static string Of(PartDimension dimension) => dimension switch
    {
        PartDimension.Length => "Length",
        PartDimension.Width => "Width",
        PartDimension.Thickness => "Thickness",
        _ => throw new ArgumentOutOfRangeException(
            nameof(dimension), dimension, "There are three dimensions and this is not one of them."),
    };

    /// <summary>Reads a word back, as the panel does when Apply is pressed.</summary>
    /// <param name="word">The word, as the list shows it.</param>
    /// <param name="dimension">The dimension it names.</param>
    public static bool TryDimension(string? word, out PartDimension dimension)
    {
        switch (word)
        {
            case "Length": dimension = PartDimension.Length; return true;
            case "Width": dimension = PartDimension.Width; return true;
            case "Thickness": dimension = PartDimension.Thickness; return true;
            default: dimension = default; return false;
        }
    }
}
