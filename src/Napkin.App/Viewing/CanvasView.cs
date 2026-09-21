using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Napkin.App.Designs;
using Design = Napkin.App.Designs.Design;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// Draws a design in plan view, and lets a person move around it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Read-only.</strong> Every gesture here changes <see cref="View"/> and nothing else. The
/// <see cref="Design"/> it is given is a value it never writes to: there is no code path in this
/// control that produces a <see cref="Sketch"/>, which is what makes "panning moved nothing"
/// true by construction rather than by care.
/// </para>
/// <para>
/// <strong>Controls.</strong> Wheel zooms about the pointer; Shift and the wheel pan; dragging with
/// the left or the middle button pans; the arrow keys pan by a tenth of the viewport (a full half
/// with Shift held); <c>+</c> and <c>-</c> zoom about the centre; the command key with <c>0</c>
/// zooms to fit. Left-drag pans because M1 has nothing to select; it becomes the selection gesture
/// when editing lands (#10), and panning keeps the middle button and the space bar.
/// </para>
/// <para>
/// <strong>Line weights are in pixels, not inches.</strong> A 1.4-pixel outline is 1.4 pixels at
/// every zoom, so the drawing reads the same framed on a wall or zoomed to a single joint. Text is
/// the same: a fixed size in pixels, which is what keeps a label readable when it is over a part
/// three pixels wide.
/// </para>
/// </remarks>
public sealed class CanvasView : Control
{
    /// <summary>The design drawn. Null draws an empty sheet.</summary>
    public static readonly StyledProperty<Design?> DesignProperty =
        AvaloniaProperty.Register<CanvasView, Design?>(nameof(Design));

    /// <summary>How much one wheel notch zooms.</summary>
    const double ZoomPerWheelNotch = 1.15;

    /// <summary>How much the +/- keys zoom.</summary>
    const double ZoomPerKeyPress = 1.25;

    /// <summary>How far one wheel notch pans when Shift is held, in pixels.</summary>
    const double PanPixelsPerWheelNotch = 60;

    /// <summary>How much of the viewport an arrow key pans across.</summary>
    const double PanFractionPerKeyPress = 0.1;

    /// <summary>How much of the viewport an arrow key pans across with Shift held.</summary>
    const double FastPanFractionPerKeyPress = 0.5;

    /// <summary>Text size for dimension labels and part names, in pixels.</summary>
    const double LabelTextSize = 12;

    /// <summary>How long an arrowhead is, in pixels.</summary>
    const double ArrowLength = 9;

    /// <summary>Half the width of an arrowhead, in pixels.</summary>
    const double ArrowHalfWidth = 2.75;

    /// <summary>The gap between what is measured and the start of its extension line, in pixels.</summary>
    const double ExtensionGap = 3;

    /// <summary>How far an extension line runs past the dimension line, in pixels.</summary>
    const double ExtensionOvershoot = 5;

    /// <summary>The closest two grid lines are allowed to be drawn, in pixels.</summary>
    const double MinimumGridSpacing = 14;

    /// <summary>
    /// The grid steps, in inches: a quarter inch up to a hundred feet, each a plain number a
    /// person would use.
    /// </summary>
    static readonly double[] GridLadder =
    [
        0.25, 0.5, 1, 3, 6, 12, 24, 48, 96, 144, 288, 600, 1200, 2400, 6000, 12000,
    ];

    bool _fitPending = true;
    bool _panning;
    Point _panFrom;
    ViewTransform _view = ViewTransform.Default;

    static CanvasView()
    {
        FocusableProperty.OverrideDefaultValue<CanvasView>(true);
        DesignProperty.Changed.AddClassHandler<CanvasView>((canvas, _) => canvas.OnDesignChanged());
    }

    /// <summary>Raised whenever the view transform changes, so a status bar can follow it.</summary>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// Raised as the pointer moves, with the model point under it, or null when it leaves.
    /// </summary>
    public event EventHandler<Point2?>? PointerWorldPositionChanged;

    /// <inheritdoc cref="DesignProperty"/>
    public Design? Design
    {
        get => GetValue(DesignProperty);
        set => SetValue(DesignProperty, value);
    }

    /// <summary>Where the view is. Every gesture in this control changes this and nothing else.</summary>
    public ViewTransform View
    {
        get => _view;
        private set
        {
            if (_view == value)
            {
                return;
            }

            _view = value;
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The precision dimension labels are shown at. Fixed at 1/16&#x2033; for M1; the per-project
    /// picker is #11.
    /// </summary>
    public LengthFormat LabelFormat { get; } = new FeetInchesFormat(16);

    /// <summary>The bounding box of everything in the current design.</summary>
    public WorldBounds Extents =>
        Design is { } design ? SketchExtents.Of(design.Sketch) : WorldBounds.Empty;

    /// <summary>
    /// What every dimension in the current design reads, worked out from the geometry now.
    /// </summary>
    /// <remarks>
    /// Nothing is cached: this is the same call <see cref="Render"/> makes, so what a test reads
    /// here is what a person sees on the canvas, and changing the geometry changes both.
    /// </remarks>
    public IReadOnlyList<DimensionMeasurement> Measurements() =>
        Design is { } design ? [.. DimensionLayout.Measure(design.Sketch)] : [];

    /// <summary>What one dimension reads, found by the label of the design it belongs to.</summary>
    public string? LabelOf(EntityId dimension) => Measurements()
        .FirstOrDefault(measurement => measurement.Dimension.Id == dimension)
        ?.Label(LabelFormat);

    /// <summary>Frames the whole design, with a margin.</summary>
    public void ZoomToFit()
    {
        WorldBounds extents = Extents;
        if (extents.IsEmpty || !_view.HasViewport)
        {
            // Nothing to frame, or no viewport to frame it in: fit as soon as there is one.
            _fitPending = true;
            return;
        }

        _fitPending = false;
        View = _view.FitTo(extents, _view.Viewport);
    }

    /// <summary>Zooms in one step, about the centre of the viewport.</summary>
    public void ZoomIn() => View = _view.ZoomAtCenter(ZoomPerKeyPress);

    /// <summary>Zooms out one step, about the centre of the viewport.</summary>
    public void ZoomOut() => View = _view.ZoomAtCenter(1 / ZoomPerKeyPress);

    /// <summary>Pans by a fraction of the viewport: positive x moves the view right.</summary>
    public void PanByFraction(double fractionX, double fractionY) =>
        View = _view.PanByViewportFraction(fractionX, fractionY);

    /// <summary>
    /// Applies a view keystroke, wherever it was received.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the key was a view command, so the caller can leave it alone if
    /// it was not.
    /// </returns>
    public bool HandleViewKey(Key key, KeyModifiers modifiers)
    {
        // Control on Windows, Command on macOS — and Control under the headless platform, which
        // reports no macOS windowing backend. Accepting either means one code path for both.
        bool command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        double step = modifiers.HasFlag(KeyModifiers.Shift)
            ? FastPanFractionPerKeyPress
            : PanFractionPerKeyPress;

        switch (key)
        {
            case Key.D0 or Key.NumPad0 when command:
                ZoomToFit();
                return true;

            case Key.Left:
                PanByFraction(-step, 0);
                return true;

            case Key.Right:
                PanByFraction(step, 0);
                return true;

            case Key.Up:
                PanByFraction(0, step);
                return true;

            case Key.Down:
                PanByFraction(0, -step);
                return true;

            case Key.Add or Key.OemPlus:
                ZoomIn();
                return true;

            case Key.Subtract or Key.OemMinus:
                ZoomOut();
                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size arranged = base.ArrangeOverride(finalSize);
        if (_view.Viewport != arranged)
        {
            // A resize keeps the model point at the centre of the viewport where it is, and keeps
            // the scale: the window shows more or less of the drawing, not a different size of it.
            View = _view.WithViewport(arranged);
        }

        if (_fitPending)
        {
            ZoomToFit();
        }

        return arranged;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerPointProperties properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsMiddleButtonPressed)
        {
            return;
        }

        Focus();
        _panning = true;
        _panFrom = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point position = e.GetPosition(this);
        if (_panning)
        {
            View = _view.PanByPixels(position - _panFrom);
            _panFrom = position;
        }

        // Deliberately no InvalidateVisual: the readout is a label, and redrawing the whole
        // drawing on every pointer sample is how a canvas starts to feel heavy.
        PointerWorldPositionChanged?.Invoke(this, _view.ToWorld(position));
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndPan(e.Pointer);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPan(null);
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PointerWorldPositionChanged?.Invoke(this, null);
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Point position = e.GetPosition(this);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Shift and the wheel pans. A trackpad reports both axes, so it pans in both.
            View = _view.PanByPixels(new Vector(
                e.Delta.X * PanPixelsPerWheelNotch,
                e.Delta.Y * PanPixelsPerWheelNotch));
        }
        else
        {
            double notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            View = _view.ZoomAt(position, Math.Pow(ZoomPerWheelNotch, notches));
        }

        PointerWorldPositionChanged?.Invoke(this, _view.ToWorld(position));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && HandleViewKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        Rect viewport = new(Bounds.Size);
        context.FillRectangle(new SolidColorBrush(palette.Background), viewport);
        DrawGrid(context, palette, viewport);

        if (Design is not { } design)
        {
            return;
        }

        Sketch sketch = design.Sketch;
        Dictionary<LayerId, string> layerNames = sketch.Layers.ToDictionary(
            layer => layer.Id,
            layer => layer.Name);

        foreach (Entity entity in sketch.Entities.Values.OrderBy(item => item.Id))
        {
            string layerName = layerNames.TryGetValue(entity.Layer, out string? name) ? name : string.Empty;
            switch (entity)
            {
                case Box box:
                    DrawBox(context, palette, design, box, layerName);
                    break;

                case Segment segment
                    when sketch.Find<Node>(segment.Start) is { } start
                         && sketch.Find<Node>(segment.End) is { } end:
                    DrawSegment(context, palette, layerName, start.Position, end.Position);
                    break;

                case Node node:
                    DrawNode(context, palette, node.Position);
                    break;
            }
        }

        // Dimensions last, so a dimension line is never hidden under a part.
        foreach (DimensionMeasurement measurement in DimensionLayout.Measure(sketch))
        {
            DrawDimension(context, palette, measurement);
        }
    }

    void OnDesignChanged()
    {
        // A new design is framed the moment there is a viewport to frame it in.
        _fitPending = true;
        ZoomToFit();
        InvalidateVisual();
    }

    void EndPan(IPointer? pointer)
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        Cursor = Cursor.Default;
        pointer?.Capture(null);
    }

    void DrawGrid(DrawingContext context, CanvasPalette palette, Rect viewport)
    {
        double minor = GridLadder.FirstOrDefault(
            step => step * _view.PixelsPerInch >= MinimumGridSpacing);
        if (minor <= 0)
        {
            return;
        }

        double major = GridLadder.FirstOrDefault(
            step => step >= minor * 4 && step * _view.PixelsPerInch >= MinimumGridSpacing * 4);

        Pen minorPen = new(new SolidColorBrush(palette.GridMinor), 1);
        Pen majorPen = new(new SolidColorBrush(palette.GridMajor), 1);

        (double left, double top) = _view.ToWorldInches(viewport.TopLeft);
        (double right, double bottom) = _view.ToWorldInches(viewport.BottomRight);

        foreach (double x in Steps(left, right, minor))
        {
            double screenX = _view.ToScreen(x, 0).X;
            bool isMajor = major > 0 && IsMultiple(x, major);
            context.DrawLine(
                isMajor ? majorPen : minorPen,
                new Point(screenX, viewport.Top),
                new Point(screenX, viewport.Bottom));
        }

        foreach (double y in Steps(bottom, top, minor))
        {
            double screenY = _view.ToScreen(0, y).Y;
            bool isMajor = major > 0 && IsMultiple(y, major);
            context.DrawLine(
                isMajor ? majorPen : minorPen,
                new Point(viewport.Left, screenY),
                new Point(viewport.Right, screenY));
        }
    }

    void DrawBox(
        DrawingContext context,
        CanvasPalette palette,
        Design design,
        Box box,
        string layerName)
    {
        EntityStyle style = palette.StyleFor(layerName);
        Point southWest = _view.ToScreen(box.Corner(BoxCorner.SouthWest));
        Point southEast = _view.ToScreen(box.Corner(BoxCorner.SouthEast));
        Point northEast = _view.ToScreen(box.Corner(BoxCorner.NorthEast));
        Point northWest = _view.ToScreen(box.Corner(BoxCorner.NorthWest));

        StreamGeometry outline = new();
        using (StreamGeometryContext geometry = outline.Open())
        {
            geometry.BeginFigure(southWest, isFilled: true);
            geometry.LineTo(southEast);
            geometry.LineTo(northEast);
            geometry.LineTo(northWest);
            geometry.EndFigure(isClosed: true);
        }

        Pen pen = new(new SolidColorBrush(style.Stroke), style.StrokeThickness)
        {
            LineJoin = PenLineJoin.Miter,
            DashStyle = style.Dashed ? new DashStyle([4, 3], 0) : null,
        };
        context.DrawGeometry(new SolidColorBrush(style.Fill), pen, outline);

        if (design.LabelFor(box.Id) is { } label)
        {
            DrawPartLabel(context, palette, label, southWest, northEast);
        }
    }

    void DrawPartLabel(
        DrawingContext context,
        CanvasPalette palette,
        string label,
        Point southWest,
        Point northEast)
    {
        // The two corners are model corners, so in screen coordinates south is below north and a
        // rotated part may put either one first: normalise before measuring.
        Rect rectangle = new Rect(southWest, northEast).Normalize();
        FormattedText text = Text(label, palette.Label);
        if (text.Width + 8 > rectangle.Width || text.Height + 4 > rectangle.Height)
        {
            // It does not fit. A part too small for its name is better unlabelled than overdrawn;
            // zooming in brings the name back.
            return;
        }

        context.DrawText(text, new Point(
            rectangle.Center.X - (text.Width / 2),
            rectangle.Center.Y - (text.Height / 2)));
    }

    void DrawSegment(
        DrawingContext context,
        CanvasPalette palette,
        string layerName,
        Point2 start,
        Point2 end)
    {
        EntityStyle style = palette.StyleFor(layerName);
        context.DrawLine(
            new Pen(new SolidColorBrush(style.Stroke), style.StrokeThickness),
            _view.ToScreen(start),
            _view.ToScreen(end));
    }

    void DrawNode(DrawingContext context, CanvasPalette palette, Point2 position)
    {
        Point centre = _view.ToScreen(position);
        context.DrawEllipse(new SolidColorBrush(palette.NodeFill), null, centre, 2.5, 2.5);
    }

    void DrawDimension(DrawingContext context, CanvasPalette palette, DimensionMeasurement measurement)
    {
        Point from = _view.ToScreen(measurement.From);
        Point to = _view.ToScreen(measurement.To);
        Point lineFrom = _view.ToScreen(measurement.LineFrom);
        Point lineTo = _view.ToScreen(measurement.LineTo);

        Vector along = lineTo - lineFrom;
        double length = along.Length;
        if (length < 2)
        {
            // Zoomed far enough out that the whole dimension is a dot. Drawing it would be ink
            // over ink; the parts still read.
            return;
        }

        SolidColorBrush ink = new(palette.Dimension);
        Pen pen = new(ink, 1);
        Vector direction = along / length;

        DrawExtensionLine(context, pen, from, lineFrom);
        DrawExtensionLine(context, pen, to, lineTo);

        // A dimension too short for two arrowheads gets them outside, pointing in, and the line
        // stubbed past each end — which is what a draughtsman does with a 1" gap.
        bool tight = length < 3.5 * ArrowLength;
        if (tight)
        {
            context.DrawLine(pen, lineFrom - (direction * ArrowLength), lineTo + (direction * ArrowLength));
        }
        else
        {
            context.DrawLine(pen, lineFrom, lineTo);
        }

        DrawArrowhead(context, ink, lineFrom, tight ? direction : -direction);
        DrawArrowhead(context, ink, lineTo, tight ? -direction : direction);

        FormattedText text = Text(measurement.Label(LabelFormat), palette.Dimension);
        Point centre = new(
            (lineFrom.X + lineTo.X) / 2,
            (lineFrom.Y + lineTo.Y) / 2);

        // A dimension running up the page reads up the page, as it does on a drawing sheet.
        bool vertical = Math.Abs(direction.Y) > Math.Abs(direction.X);
        using (context.PushTransform(
                   Matrix.CreateRotation(vertical ? -Math.PI / 2 : 0)
                   * Matrix.CreateTranslation(centre.X, centre.Y)))
        {
            Rect chip = new(
                -(text.Width / 2) - 3,
                -(text.Height / 2) - 1,
                text.Width + 6,
                text.Height + 2);

            // The chip breaks the dimension line behind the text, which is what a drawing does.
            context.DrawRectangle(
                new SolidColorBrush(palette.Background),
                null,
                new RoundedRect(chip, 2));
            context.DrawText(text, new Point(-(text.Width / 2), -(text.Height / 2)));
        }
    }

    static void DrawExtensionLine(DrawingContext context, Pen pen, Point measured, Point line)
    {
        Vector away = line - measured;
        double length = away.Length;
        if (length <= ExtensionGap + 1)
        {
            return;
        }

        Vector direction = away / length;
        context.DrawLine(
            pen,
            measured + (direction * ExtensionGap),
            line + (direction * ExtensionOvershoot));
    }

    static void DrawArrowhead(DrawingContext context, IBrush brush, Point tip, Vector pointing)
    {
        Vector back = -pointing * ArrowLength;
        Vector side = new Vector(-pointing.Y, pointing.X) * ArrowHalfWidth;

        StreamGeometry head = new();
        using (StreamGeometryContext geometry = head.Open())
        {
            geometry.BeginFigure(tip, isFilled: true);
            geometry.LineTo(tip + back + side);
            geometry.LineTo(tip + back - side);
            geometry.EndFigure(isClosed: true);
        }

        context.DrawGeometry(brush, null, head);
    }

    static FormattedText Text(string text, Color color) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        LabelTextSize,
        new SolidColorBrush(color));

    /// <summary>Every multiple of <paramref name="step"/> in a range, lowest first.</summary>
    static IEnumerable<double> Steps(double from, double to, double step)
    {
        double first = Math.Ceiling(from / step) * step;
        double count = Math.Floor((to - first) / step);
        if (!double.IsFinite(count) || count < 0)
        {
            yield break;
        }

        // A guard, not a policy: the ladder keeps lines at least MinimumGridSpacing apart, so this
        // can only be reached by a viewport far larger than a screen.
        int limit = (int)Math.Min(count, 4000);
        for (int i = 0; i <= limit; i++)
        {
            yield return first + (i * step);
        }
    }

    static bool IsMultiple(double value, double step)
    {
        double ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }
}
