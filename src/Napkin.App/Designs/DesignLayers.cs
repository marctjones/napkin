namespace Napkin.App.Designs;

/// <summary>
/// The layer names the canvas knows how to style.
/// </summary>
/// <remarks>
/// <para>
/// Styling keys off the layer's <em>name</em> rather than off a type the App invents, so that a
/// sketch the project reader (#6) produces is drawn the same way a built-in sample is: a file that
/// puts its parts on a layer called "Parts" gets the parts look, and a file with layers napkin has
/// never heard of still draws, in the neutral style. Layer appearance becomes real, per-layer, user
/// data with the layer UI in #11; this is the placeholder until then, not a format.
/// </para>
/// </remarks>
public static class DesignLayers
{
    /// <summary>Furniture parts: the things a cut list will list.</summary>
    public const string Parts = "Parts";

    /// <summary>Framing members — studs, plates, headers.</summary>
    public const string Framing = "Framing";

    /// <summary>Walls, in plan.</summary>
    public const string Wall = "Wall";

    /// <summary>Openings cut into a wall.</summary>
    public const string Opening = "Opening";

    /// <summary>Annotation: dimensions and, later, notes and callouts.</summary>
    public const string Dimensions = "Dimensions";
}
