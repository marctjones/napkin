// Vendored from marctjones/skepticalengineering-design@1c15e5b (avalonia/WorkbenchIcon.cs). Do not hand-edit: change it there, then re-copy.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Napkin.App.Theme;

/// <summary>
/// Draws one Workbench icon (<c>docs/iconography.md</c>), scaled uniformly from its 24&#xD7;24
/// source grid to fit this control's bounds.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why not <see cref="PathIcon"/>.</strong> <c>PathIcon</c> stretches its geometry to fill
/// its own bounds on X and Y independently, so a wide icon (<c>minus</c>) and a tall one
/// (<c>folder</c>) placed at the same <c>Width</c>/<c>Height</c> render at different optical
/// sizes — each fills the box, rather than each sitting the same size within it. A Workbench
/// icon is drawn on a fixed 24-unit grid (<c>docs/avalonia.md</c>), so the right transform is one
/// uniform scale that fits the whole 24&#xD7;24 grid inside the control and centres it, the way a
/// glyph in a font is scaled — not a stretch to the control's own aspect ratio.
/// </para>
/// <para>
/// <strong>Why not <see cref="Avalonia.Controls.IconElement"/>.</strong> Avalonia's own
/// <c>IconElement</c>/<c>PathIcon</c> pair renders through a <c>ControlTheme</c> (a <c>Path</c> in
/// a template, bound to <c>Data</c> and <c>Foreground</c>) rather than an overridden
/// <see cref="Render"/> — <c>IconElement</c> declares no <c>Render</c> of its own. Using it here
/// would mean shipping a companion theme resource just to place one bound <c>Path</c> and scale
/// it, for no benefit over drawing it directly. A plain <see cref="Control"/> with a
/// <see cref="Render"/> override is this design system's own house style for a small fixed
/// drawing — see <c>CutThumbnail</c> in napkin's <c>Napkin.App/Viewing</c>, which this is modelled
/// on — and needs nothing else to work.
/// </para>
/// <para>
/// <c>Foreground</c> inherits down the logical tree, the way <c>TemplatedControl.Foreground</c>
/// does, so an icon takes its colour from the text beside it unless one is set directly
/// (<c>docs/iconography.md</c>: colour an icon only to carry state — <c>moss</c>, <c>ochre</c>,
/// <c>rust</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;se:WorkbenchIcon Data="{StaticResource Icon.save}" Width="16" Height="16"/&gt;
/// </code>
/// </example>
public sealed class WorkbenchIcon : Control
{
    /// <summary>The side of the grid a Workbench icon is drawn on, in its own units.</summary>
    public const double GridSize = 24.0;

    /// <summary>The icon's geometry, in the 24&#xD7;24 grid — a <c>StaticResource</c> from <c>WorkbenchIcons.axaml</c>.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<WorkbenchIcon, Geometry?>(nameof(Data));

    /// <summary>What the icon is drawn in. Inherits from an ancestor's <c>Foreground</c>, as text does.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<WorkbenchIcon, IBrush?>(nameof(Foreground), inherits: true);

    static WorkbenchIcon()
    {
        AffectsRender<WorkbenchIcon>(DataProperty, ForegroundProperty);
        AffectsMeasure<WorkbenchIcon>(WidthProperty, HeightProperty);
    }

    /// <inheritdoc cref="DataProperty"/>
    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// A square the same size as whichever of the available width and height is smaller — an icon
    /// with no explicit size asks for one grid unit per pixel, same as a Workbench icon's native
    /// 24px size, rather than for infinite space.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double side = double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height)
            ? GridSize
            : Math.Min(
                double.IsInfinity(availableSize.Width) ? availableSize.Height : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? availableSize.Width : availableSize.Height);

        return new Size(side, side);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Data is not { } data || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        // One uniform scale, fitted to the smaller side and centred on the other — every icon
        // occupies the same fraction of its box, whatever its own aspect ratio within the grid.
        double scale = Math.Min(Bounds.Width / GridSize, Bounds.Height / GridSize);
        double offsetX = (Bounds.Width - (GridSize * scale)) / 2.0;
        double offsetY = (Bounds.Height - (GridSize * scale)) / 2.0;

        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(Foreground ?? Brushes.Black, null, data);
        }
    }
}
