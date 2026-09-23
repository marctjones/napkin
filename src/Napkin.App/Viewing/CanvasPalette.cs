using Avalonia.Media;
using Avalonia.Styling;
using Napkin.App.Designs;

namespace Napkin.App.Viewing;

/// <summary>
/// The colours the canvas draws with, in one place, for one theme variant.
/// </summary>
/// <remarks>
/// A drawing is not a user interface: it wants paper behind it and ink on it, and it has to stay
/// readable whichever theme the machine is in. So the canvas picks its own palette from the theme
/// variant rather than inheriting control colours, and the two palettes below are chosen to give
/// the same contrast ratios on paper-white and on charcoal.
/// </remarks>
/// <param name="Background">The paper.</param>
/// <param name="GridMinor">The finer grid.</param>
/// <param name="GridMajor">The coarser grid, one step up the ladder.</param>
/// <param name="Dimension">Dimension lines, arrowheads and their text.</param>
/// <param name="Label">Part names drawn on parts.</param>
/// <param name="NodeFill">Free points and segment ends.</param>
/// <param name="Selection">
/// What is picked: its outline, its handles, and the dimensions it shows while it is picked.
/// </param>
/// <param name="Snap">A snap indicator, while a part is being dragged onto something.</param>
/// <param name="PreviewFill">The inside of a part that is still being dragged out.</param>
public sealed record CanvasPalette(
    Color Background,
    Color GridMinor,
    Color GridMajor,
    Color Dimension,
    Color Label,
    Color NodeFill,
    Color Selection,
    Color Snap,
    Color PreviewFill)
{
    /// <summary>
    /// Light theme: ink on paper. Neutrals, selection (moss) and problems (rust) come from the
    /// Skeptical Engineering tokens; the layer colours below are napkin's own, since the design
    /// system has no drawing section yet.
    /// </summary>
    public static readonly CanvasPalette Light = new(
        Background: Color.Parse("#F8F8F6"),
        GridMinor: Color.Parse("#E4E4DC"),
        GridMajor: Color.Parse("#DCDCD4"),
        Dimension: Color.Parse("#505048"),
        Label: Color.Parse("#505048"),
        NodeFill: Color.Parse("#686860"),
        Selection: Color.Parse("#4A7C4A"),
        Snap: Color.Parse("#9A4A3A"),
        PreviewFill: Color.Parse("#204A7C4A"))
    {
        Styles =
        {
            [DesignLayers.Parts] = new EntityStyle(Color.Parse("#26B07A3B"), Color.Parse("#8A5A22"), 1.4),
            [DesignLayers.Framing] = new EntityStyle(Color.Parse("#1FA08A5A"), Color.Parse("#9A8253"), 1.1),
            [DesignLayers.Wall] = new EntityStyle(Color.Parse("#D8D3C9"), Color.Parse("#4B4843"), 1.8),
            [DesignLayers.Opening] = new EntityStyle(Color.Parse("#F8F8F6"), Color.Parse("#505048"), 1.6, Dashed: true),
        },
        Neutral = new EntityStyle(Color.Parse("#18000000"), Color.Parse("#5A554C"), 1.3),
    };

    /// <summary>Dark theme: the same drawing, lit from the other side.</summary>
    public static readonly CanvasPalette Dark = new(
        Background: Color.Parse("#1C1C1C"),
        GridMinor: Color.Parse("#2E2E2B"),
        GridMajor: Color.Parse("#3A3A36"),
        Dimension: Color.Parse("#A0A090"),
        Label: Color.Parse("#C8C8C0"),
        NodeFill: Color.Parse("#A0A090"),
        Selection: Color.Parse("#6FA86F"),
        Snap: Color.Parse("#D0806E"),
        PreviewFill: Color.Parse("#286FA86F"))
    {
        Styles =
        {
            [DesignLayers.Parts] = new EntityStyle(Color.Parse("#2ED9A066"), Color.Parse("#D9A066"), 1.4),
            [DesignLayers.Framing] = new EntityStyle(Color.Parse("#22C0B48F"), Color.Parse("#9A9275"), 1.1),
            [DesignLayers.Wall] = new EntityStyle(Color.Parse("#34383E"), Color.Parse("#C2C6CC"), 1.8),
            [DesignLayers.Opening] = new EntityStyle(Color.Parse("#1C1C1C"), Color.Parse("#A0A090"), 1.6, Dashed: true),
        },
        Neutral = new EntityStyle(Color.Parse("#22FFFFFF"), Color.Parse("#A9ADB4"), 1.3),
    };

    /// <summary>How each known layer name is drawn.</summary>
    public Dictionary<string, EntityStyle> Styles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How a layer napkin has never heard of is drawn.</summary>
    public EntityStyle Neutral { get; init; } =
        new(Color.Parse("#18000000"), Color.Parse("#5A554C"), 1.3);

    /// <summary>The palette for a theme variant; dark for dark, light for everything else.</summary>
    public static CanvasPalette For(ThemeVariant? variant) =>
        variant == ThemeVariant.Dark ? Dark : Light;

    /// <summary>The style for a layer name.</summary>
    public EntityStyle StyleFor(string layerName) =>
        Styles.TryGetValue(layerName, out EntityStyle? style) ? style : Neutral;
}

/// <summary>How one class of entity is filled and outlined.</summary>
/// <param name="Fill">The fill colour; may be transparent.</param>
/// <param name="Stroke">The outline colour.</param>
/// <param name="StrokeThickness">
/// The outline width <em>in pixels</em>. Line weights are a property of the drawing's legibility,
/// not of its scale, so they never change with the zoom.
/// </param>
/// <param name="Dashed">Whether the outline is dashed — an opening is a cut, not a part.</param>
public sealed record EntityStyle(Color Fill, Color Stroke, double StrokeThickness, bool Dashed = false);
