using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// A blank sheet to draw on: <em>File &#x2192; New</em>.
/// </summary>
/// <remarks>
/// <para>
/// A design source like any other, so the window opens a new sheet through exactly the path it
/// opens a file through, and the canvas cannot tell the difference.
/// </para>
/// <para>
/// <strong>This is the one place in the application that builds a sketch</strong>, and it builds
/// the empty one. Everything after it is a <see cref="Request"/> to the updater (CVS-005). The
/// sheet starts with a layer called "Parts" beside the default one, because there is no
/// <c>AddLayer</c> request in <c>Core.Geometry</c> — layers are a property of the sketch, and #11
/// owns the UI for them — so a layer a new part can go on has to exist from the start.
/// </para>
/// </remarks>
public sealed class NewSheet : IDesignSource
{
    /// <summary>What a new sheet is called before it has a file.</summary>
    public const string UntitledName = "Untitled";

    /// <inheritdoc/>
    public string Name => UntitledName;

    /// <inheritdoc/>
    public string Description => "a blank sheet — press R and drag to draw a part";

    /// <summary>A blank design: an empty sketch with somewhere to put parts.</summary>
    public static Design Empty() => Design.Unlabelled(
        UntitledName,
        Sketch.Empty.WithLayer(new Layer(LayerId.New(), DesignLayers.Parts)));

    /// <inheritdoc/>
    public Design Load() => Empty();
}
