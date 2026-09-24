using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// The shape workshop's drawing: one blank, in its own frame, unrotated and large, with its
/// corners named by compass and its cut targets on it
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.1, &#xA7;7.2).
/// </summary>
/// <remarks>
/// <para>
/// <strong>It is a mode, not a second document.</strong> The control draws one box out of the
/// <em>same</em> <see cref="DesignEditor"/> the canvas draws, and every edit it makes is a
/// <see cref="SetCut"/> or a <see cref="RemoveCut"/> through that editor. There is no second
/// sketch and no second mutation path (CVS-005); undo, save and the cut list all see ordinary
/// edits, and a relationship the part already has still holds while it is being shaped.
/// </para>
/// <para>
/// <strong>The frame is the cut list's.</strong> North is up and the part is shown the way it was
/// drawn, not the way it is turned on the canvas, because that is the frame
/// <c>CutDescription</c>'s sentences name corners in (&#xA7;4.4) — so the words and the picture
/// agree about which corner is the north-east one. The outline itself goes through
/// <see cref="OutlineDrawing"/>, the same builder the canvas and the cut-list thumbnail use, so a
/// shaped part cannot look like one thing here and another there.
/// </para>
/// <para>
/// <strong>One gesture is one <see cref="SetCut"/>.</strong> A press on a target starts a
/// <see cref="CutTool"/> and a <see cref="DesignEditor.BeginGesture"/>; each pointer move applies
/// the candidate with <see cref="DesignEditor.ApplyQuietly"/> for the live preview; the release
/// commits the last one with <see cref="DesignEditor.Apply"/> and ends the gesture. That is the
/// same shape as the canvas's resize-by-dragging-a-grip, so undo sees one step (&#xA7;7.2).
/// </para>
/// </remarks>
public sealed class WorkshopView : Control
{

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            InvalidateVisual();
        }
    }

    /// <summary>The breathing space around the blank, in pixels — room for the compass labels.</summary>
    const double Surround = 34;

    /// <summary>Half the width of a target handle, in pixels.</summary>
    const double TargetHalfSize = 5;

    /// <summary>How near a target the pointer has to be to grab it, in pixels.</summary>
    const double TargetGrabPixels = 12;

    /// <summary>Text size for the compass labels, in pixels.</summary>
    const double LabelTextSize = 11;

    readonly CutTool _cut = new();
    DesignEditor? _editor;
    EntityId? _blank;
    CutSite? _selected;
    CutSite? _hovered;
    string _hint = string.Empty;

    static WorkshopView() => FocusableProperty.OverrideDefaultValue<WorkshopView>(true);

    /// <summary>Raised when the cut the workshop has selected changes.</summary>
    public event EventHandler? SelectedCutChanged;

    /// <summary>Raised when what a press would do changes, so a hint line can follow it.</summary>
    public event EventHandler? HintChanged;

    /// <summary>The drawing being edited. The same editor the canvas holds.</summary>
    public DesignEditor? Editor
    {
        get => _editor;
        set
        {
            if (ReferenceEquals(_editor, value))
            {
                return;
            }

            if (_editor is not null)
            {
                _editor.DesignChanged -= OnDesignChanged;
            }

            _editor = value;

            if (_editor is not null)
            {
                _editor.DesignChanged += OnDesignChanged;
            }

            InvalidateVisual();
        }
    }

    /// <summary>Which part the workshop is shaping, or null when it is not open.</summary>
    public EntityId? Blank
    {
        get => _blank;
        set
        {
            _blank = value;
            _cut.Cancel();
            SetSelected(null);
            SetHint(string.Empty);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The blank as the workshop draws it: anchored at the origin, unrotated, cuts and all.
    /// </summary>
    public Box? LocalBlank =>
        _blank is { } id && _editor?.Design.Sketch.Find<Box>(id) is { } box ? CutTool.Local(box) : null;

    /// <summary>The cut the workshop has selected, by its site, or null when none is.</summary>
    public CutSite? SelectedSite => _selected;

    /// <summary>The selected cut itself, or null when nothing is selected or the site is empty.</summary>
    public Cut? SelectedCut =>
        LocalBlank is { } blank && _selected is { } site ? BlankShape.CutAt(blank, site) : null;

    /// <summary>What a press would do, in words. Empty when the pointer is on nothing.</summary>
    public string Hint => _hint;

    /// <summary>Whether a cut is being dragged out right now.</summary>
    public bool IsCutting => _cut.IsCutting;

    /// <summary>The grid step in force at the workshop's scale, in inches.</summary>
    public double GridStepInches => SnapGrid.StepInches(Scale);

    /// <summary>Whether a cut lands on the grid, as on the plan.</summary>
    public bool SnapToGrid { get; set; } = true;

    double SnapStepInches => SnapToGrid ? GridStepInches : 1.0 / Length.UnitsPerInch;

    /// <summary>Selects a cut by its site, or nothing.</summary>
    public void SelectSite(CutSite? site)
    {
        SetSelected(site);
        InvalidateVisual();
    }

    /// <summary>Where a point of the blank's own frame is drawn, in this control's pixels.</summary>
    public Point ToScreen(Point2 local)
    {
        if (LocalBlank is not { } blank)
        {
            return default;
        }

        double scale = Scale;
        double width = blank.Width.ToInches() * scale;
        double height = blank.Height.ToInches() * scale;
        double left = (Bounds.Width - width) / 2;
        double top = (Bounds.Height - height) / 2;

        // North is up, as it is on the canvas and in the cut list's thumbnail.
        return new Point(
            left + (local.X.ToInches() * scale),
            top + height - (local.Y.ToInches() * scale));
    }

    /// <summary>Where a target is drawn, in this control's pixels.</summary>
    public Point? TargetOnScreen(CutSite site) =>
        LocalBlank is { } blank ? ToScreen(CutTool.TargetPoint(blank, site)) : null;

    /// <summary>What a point in this control's pixels is, in the blank's own frame.</summary>
    public Point2 ToLocal(Point screen)
    {
        if (LocalBlank is not { } blank)
        {
            return Point2.Origin;
        }

        double scale = Scale;
        double width = blank.Width.ToInches() * scale;
        double height = blank.Height.ToInches() * scale;
        double left = (Bounds.Width - width) / 2;
        double top = (Bounds.Height - height) / 2;

        return new Point2(
            Length.FromInches((screen.X - left) / scale, Rounding.HalfAwayFromZero),
            Length.FromInches((top + height - screen.Y) / scale, Rounding.HalfAwayFromZero));
    }

    /// <summary>Removes the selected cut, through the updater.</summary>
    /// <returns>Whether anything was asked for.</returns>
    public bool RemoveSelectedCut()
    {
        if (_editor is not { } editor || LocalBlank is not { } blank || _selected is not { } site)
        {
            return false;
        }

        if (BlankShape.CutAt(blank, site) is null)
        {
            editor.Say(EditSeverity.Hint, $"There is no cut at the {BlankShape.Words(site)} to remove.");
            return false;
        }

        string what = $"Removed the cut at {blank.Name}'s {BlankShape.Words(site)}";
        editor.BeginGesture(what);
        editor.Apply(new RemoveCut(blank.Id, site), what);
        editor.EndGesture();

        SetSelected(null);
        InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Puts one cut on the blank as a single edit — what a typed field and the full-mitre entry
    /// both do.
    /// </summary>
    /// <param name="cut">The cut to set.</param>
    /// <param name="what">What the person did, for the message.</param>
    /// <returns>Whether the updater took it.</returns>
    public bool SetCutNow(Cut cut, string what)
    {
        ArgumentNullException.ThrowIfNull(cut);

        if (_editor is not { } editor || _blank is not { } id)
        {
            return false;
        }

        editor.BeginGesture(what);
        UpdateResult result = editor.Apply(new SetCut(id, cut), what);
        editor.EndGesture();

        if (result is Succeeded)
        {
            SetSelected(cut.Site);
        }

        InvalidateVisual();
        return result is Succeeded;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (_editor is not { } editor
            || LocalBlank is not { } blank
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point2 at = ToLocal(e.GetPosition(this));
        if (CutTool.TargetAt(blank, at, ModelLength(TargetGrabPixels)) is not { } site)
        {
            // A press on the paper picks the cut there, if there is one, and nothing otherwise —
            // which is how a person selects a cut to type into or to delete.
            SelectSite(SiteUnder(blank, at));
            return;
        }

        _cut.Begin(blank, site, SnapStepInches);
        SelectSite(site);
        editor.BeginGesture(WhatFor(blank, site, Modifiers(e.KeyModifiers)));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (LocalBlank is not { } blank)
        {
            return;
        }

        Point2 at = ToLocal(e.GetPosition(this));
        CutModifiers modifiers = Modifiers(e.KeyModifiers);

        if (!_cut.IsCutting)
        {
            CutSite? over = CutTool.TargetAt(blank, at, ModelLength(TargetGrabPixels));
            if (over != _hovered)
            {
                _hovered = over;
                InvalidateVisual();
            }

            SetHint(over is { } site ? WhatFor(blank, site, modifiers) : string.Empty);
            return;
        }

        _cut.MoveTo(at, modifiers);
        SetHint(WhatFor(blank, _cut.Site, modifiers));

        if (_cut.Candidate is { } candidate && _editor is { } editor && _blank is { } id)
        {
            // The live preview: the real box really carries the candidate while the button is
            // down, so what is on screen is the shape and not a sketch of one.
            editor.ApplyQuietly(new SetCut(id, candidate));
        }

        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        CompleteCut();
        e.Pointer.Capture(null);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CompleteCut();
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_cut.IsCutting)
        {
            _hovered = null;
            SetHint(string.Empty);
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _selected is null)
        {
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            RemoveSelectedCut();
            e.Handled = true;
        }
    }

    /// <summary>Commits the cut a drag made, as one edit, and ends the gesture.</summary>
    void CompleteCut()
    {
        if (!_cut.IsCutting)
        {
            return;
        }

        CutSite site = _cut.Site;
        bool asked = _cut.TryComplete(out SetCut? request);

        if (_editor is not { } editor)
        {
            return;
        }

        if (asked)
        {
            editor.Apply(request!, WhatFor(LocalBlank, site, request!.Cut));
            SetSelected(site);
        }
        else
        {
            editor.Say(
                EditSeverity.Hint,
                "Drag from a corner or an edge to cut it — a click on its own cuts nothing.");
        }

        editor.EndGesture();
        SetHint(string.Empty);
        InvalidateVisual();
    }

    /// <summary>The cut whose site is under a point: what a press on the paper picks.</summary>
    static CutSite? SiteUnder(Box blank, Point2 at)
    {
        CutSite? nearest = null;
        Length best = Length.Zero;

        foreach (Cut cut in blank.Cuts)
        {
            Point2 target = CutTool.TargetPoint(blank, cut.Site);
            Length distance = Length.Abs(at.X - target.X) + Length.Abs(at.Y - target.Y);
            if (nearest is null || distance < best)
            {
                nearest = cut.Site;
                best = distance;
            }
        }

        return nearest;
    }

    static CutModifiers Modifiers(KeyModifiers keys)
    {
        CutModifiers modifiers = CutModifiers.None;
        if (keys.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= CutModifiers.Equal;
        }

        if (keys.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= CutModifiers.Round;
        }

        return modifiers;
    }

    /// <summary>What a press on a target would do, in words — the hover hint and the undo name.</summary>
    static string WhatFor(Box blank, CutSite site, CutModifiers modifiers) =>
        CutTool.GestureFor(site, modifiers) switch
        {
            CutGesture.Round => $"Rounded {blank.Name}'s {BlankShape.Words(site)}",
            CutGesture.Curve => $"Curved {blank.Name}'s {BlankShape.Words(site)}",
            _ => modifiers.HasFlag(CutModifiers.Equal)
                ? $"Clipped {blank.Name}'s {BlankShape.Words(site)} at 45°"
                : $"Clipped {blank.Name}'s {BlankShape.Words(site)}",
        };

    /// <summary>What a finished gesture did, named after the cut it actually made.</summary>
    static string WhatFor(Box? blank, CutSite site, Cut cut)
    {
        string name = blank?.Name ?? "the part";
        return cut switch
        {
            RoundedCorner rounded => $"Rounded {name}'s {BlankShape.Words(site)} to a {Show(rounded.Radius)} radius",
            CurvedEdge { Bow: Bow.Inward } scallop => $"Scalloped {name}'s {BlankShape.Words(site)} by {Show(scallop.Depth)}",
            CurvedEdge curve => $"Curved {name}'s {BlankShape.Words(site)} by {Show(curve.Depth)}",
            CornerCut clip => $"Clipped {name}'s {BlankShape.Words(site)}, {Show(clip.AlongX)} by {Show(clip.AlongY)}",
            _ => $"Cut {name}'s {BlankShape.Words(site)}",
        };
    }

    static string Show(Length length)
    {
        FormattedLength formatted = length.Format(LengthFormat.Default);
        return formatted.IsExact ? formatted.Text : CutAngle.Approximately + formatted.Text;
    }

    void SetSelected(CutSite? site)
    {
        if (_selected == site)
        {
            return;
        }

        _selected = site;
        SelectedCutChanged?.Invoke(this, EventArgs.Empty);
    }

    void SetHint(string hint)
    {
        if (string.Equals(_hint, hint, StringComparison.Ordinal))
        {
            return;
        }

        _hint = hint;
        HintChanged?.Invoke(this, EventArgs.Empty);
    }

    void OnDesignChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>Pixels per inch: the blank fills the control, whatever shape it is.</summary>
    double Scale
    {
        get
        {
            if (LocalBlank is not { } blank
                || blank.Width <= Length.Zero
                || blank.Height <= Length.Zero
                || Bounds.Width <= 2 * Surround
                || Bounds.Height <= 2 * Surround)
            {
                return 1;
            }

            return Math.Min(
                (Bounds.Width - (2 * Surround)) / blank.Width.ToInches(),
                (Bounds.Height - (2 * Surround)) / blank.Height.ToInches());
        }
    }

    Length ModelLength(double pixels) =>
        Length.FromInches(pixels / Math.Max(Scale, 1e-9), Rounding.HalfAwayFromZero);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        CanvasPalette palette = CanvasPalette.For(ActualThemeVariant);
        context.FillRectangle(new SolidColorBrush(palette.Background), new Rect(Bounds.Size));

        if (LocalBlank is not { } blank || blank.Width <= Length.Zero || blank.Height <= Length.Zero)
        {
            return;
        }

        DrawBlank(context, palette, blank);
        DrawShape(context, palette, blank);
        DrawCompass(context, palette, blank);
        DrawTargets(context, palette, blank);
    }

    /// <summary>
    /// The blank itself, faintly: the rectangle the part was before anything was cut off it, which
    /// is what every relationship and every setback is measured against (&#xA7;2.1).
    /// </summary>
    void DrawBlank(DrawingContext context, CanvasPalette palette, Box blank)
    {
        Pen faint = new(new SolidColorBrush(palette.Dimension, 0.45), 1)
        {
            DashStyle = new DashStyle([3, 3], 0),
        };

        Point southWest = ToScreen(blank.Corner(BoxCorner.SouthWest));
        Point northEast = ToScreen(blank.Corner(BoxCorner.NorthEast));
        context.DrawRectangle(null, faint, new Rect(southWest, northEast).Normalize());
    }

    void DrawShape(DrawingContext context, CanvasPalette palette, Box blank)
    {
        EntityStyle style = palette.StyleFor(DesignLayers.Parts);
        Pen pen = new(new SolidColorBrush(style.Stroke), 1.8) { LineJoin = PenLineJoin.Miter };

        context.DrawGeometry(
            new SolidColorBrush(style.Fill),
            pen,
            OutlineDrawing.GeometryOf(blank.Outline(), ToScreen));
    }

    /// <summary>
    /// The four corners, named by compass — the whole reason the workshop draws the part
    /// unrotated, so that "the north-east corner" on the cut list is the one a person is looking at
    /// (&#xA7;7.1).
    /// </summary>
    void DrawCompass(DrawingContext context, CanvasPalette palette, Box blank)
    {
        foreach ((BoxCorner corner, string label) in (( BoxCorner, string)[])
                 [
                     (BoxCorner.SouthWest, "SW"),
                     (BoxCorner.SouthEast, "SE"),
                     (BoxCorner.NorthEast, "NE"),
                     (BoxCorner.NorthWest, "NW"),
                 ])
        {
            Point at = ToScreen(blank.Corner(corner));
            FormattedText text = Text(label, palette.Label);

            // Outside the blank, on the diagonal away from its middle, so a label never sits on
            // the cut it is naming.
            bool west = corner is BoxCorner.SouthWest or BoxCorner.NorthWest;
            bool south = corner is BoxCorner.SouthWest or BoxCorner.SouthEast;

            context.DrawText(text, new Point(
                west ? at.X - text.Width - 6 : at.X + 6,
                south ? at.Y + 4 : at.Y - text.Height - 4));
        }
    }

    /// <summary>The eight things a press can take hold of, and which one is selected.</summary>
    void DrawTargets(DrawingContext context, CanvasPalette palette, Box blank)
    {
        SolidColorBrush fill = new(palette.Background);
        SolidColorBrush plain = new(palette.Selection);
        SolidColorBrush live = new(palette.Snap);

        foreach (CutSite site in CutTool.Targets())
        {
            Point at = ToScreen(CutTool.TargetPoint(blank, site));
            bool taken = BlankShape.CutAt(blank, site) is not null;
            bool chosen = _selected == site;
            bool hot = _hovered == site || (_cut.IsCutting && _cut.Site == site);

            Pen pen = new(hot ? live : plain, chosen ? 2.4 : 1.4);
            Rect handle = new(
                at.X - TargetHalfSize,
                at.Y - TargetHalfSize,
                TargetHalfSize * 2,
                TargetHalfSize * 2);

            // A site that already carries a cut is filled in, so the eight targets say at a glance
            // which of them have been used.
            context.DrawRectangle(taken ? pen.Brush : fill, pen, handle);
        }
    }

    static FormattedText Text(string text, Color color) => new(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        LabelTextSize,
        new SolidColorBrush(color));
}
