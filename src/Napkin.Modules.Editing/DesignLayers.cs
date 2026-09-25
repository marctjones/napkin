namespace Napkin.Modules.Editing;

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
    public const string Wall = Napkin.Modules.Building.BuildingLayers.Wall;

    /// <summary>Openings cut into a wall.</summary>
    public const string Opening = Napkin.Modules.Building.BuildingLayers.Opening;

    /// <summary>Annotation: dimensions and, later, notes and callouts.</summary>
    public const string Dimensions = "Dimensions";

    /// <summary>
    /// The layer name an entity is styled by: "Wall" or "Opening" for a box napkin reads as one
    /// (on that layer, or called that, as the <c>wall-with-window</c> sample's are), its own
    /// layer's name otherwise.
    /// </summary>
    public static string StyleName(Napkin.Core.Geometry.Sketch sketch, Napkin.Core.Geometry.Entity entity, IReadOnlyDictionary<Napkin.Core.Geometry.LayerId, string> layerNames)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(layerNames);
        if (entity is Napkin.Core.Geometry.Box box)
        {
            if (Napkin.Modules.Building.Opening.Is(sketch, box))
            {
                return Opening;
            }

            if (Napkin.Modules.Building.Wall.Is(sketch, box))
            {
                return Wall;
            }
        }

        return layerNames.TryGetValue(entity.Layer, out string? name) ? name : string.Empty;
    }
}
