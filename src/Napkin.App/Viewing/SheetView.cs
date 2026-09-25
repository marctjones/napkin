using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// The sheet (docs/design/standard-views.md §11): Top above Front, Right beside Front and the free 3D
/// view in the spare corner, each a <see cref="ModelView"/> over the same editor, captioned, with the
/// three drawings at one scale so they line up.
/// </summary>
public sealed class SheetView : Panel
{
    /// <summary>The strip under the panes that carries the projection note, in pixels.</summary>
    public const double NoteHeight = 18;

    readonly ModelView _top, _model, _front, _right;
    readonly Dictionary<ModelView, TextBlock> _captions = [];
    readonly TextBlock _note;
    DesignEditor? _editor;
    ModelView? _pointed;
    bool _fitRequested = true;

    /// <summary>A sheet with its four panes, not yet on an editor.</summary>
    public SheetView()
    {
        _top = AddPane(StandardView.Top, "Top");
        _model = AddPane(null, "3D");
        _front = AddPane(StandardView.Front, "Front");
        _right = AddPane(StandardView.Right, "Right");
        _note = new TextBlock
        {
            Text = StandardViewWords.ThirdAngle,
            FontSize = 11,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Brushes.Gainsboro,
        };
        Children.Add(_note);
    }

    /// <summary>The panes in reading order: Top, 3D, Front, Right.</summary>
    public IReadOnlyList<ModelView> Panes => [_top, _model, _front, _right];

    /// <summary>The pane that shows a standard view, or the free 3D pane for null.</summary>
    public ModelView PaneFor(StandardView? view) => view switch
    {
        StandardView.Top => _top,
        StandardView.Front => _front,
        StandardView.Right => _right,
        null => _model,
        _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The sheet shows Top, Front, Right and 3D."),
    };

    /// <summary>The pane commands act on (§11.5): the one the pointer was last over, else Front.</summary>
    public ModelView Active => _pointed ?? _front;

    /// <summary>Raised when the pointer moves onto another pane, which becomes the active one.</summary>
    public event EventHandler? ActiveChanged;

    /// <summary>What the pane captions say, pane by pane, for the GUI suite.</summary>
    public string CaptionOf(ModelView pane) => _captions[pane].Text ?? string.Empty;

    /// <summary>The projection note under the panes.</summary>
    public string Note => _note.Text ?? string.Empty;

    /// <summary>The editor every pane draws and selects on.</summary>
    public DesignEditor? Editor
    {
        get => _editor;
        set
        {
            if (_editor is { } old)
            {
                old.DesignOpened -= OnDesignOpened;
            }

            _editor = value;
            if (value is { } now)
            {
                now.DesignOpened += OnDesignOpened;
            }

            foreach (ModelView pane in Panes)
            {
                pane.Editor = value;
            }

            RequestFit();
        }
    }

    /// <summary>Frames the design in every pane, the three drawings at their one shared scale (§11.4), as soon as the panes have a size.</summary>
    public void RequestFit()
    {
        _fitRequested = true;
        InvalidateArrange();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (Control child in Children)
        {
            child.Measure(availableSize);
        }

        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        IReadOnlyList<SheetPane> layout = SheetLayout.Panes(finalSize.Width, Math.Max(finalSize.Height - NoteHeight, 0));
        foreach (SheetPane pane in layout)
        {
            ModelView view = PaneFor(pane.View);
            view.Arrange(new Rect(pane.Left, pane.Top, pane.Width, pane.Height));
            TextBlock caption = _captions[view];
            caption.Arrange(new Rect(new Point(pane.Left + 8, pane.Top + 6), caption.DesiredSize));
        }

        _note.Arrange(new Rect(4, finalSize.Height - NoteHeight, Math.Max(finalSize.Width - 8, 0), NoteHeight));
        if (_fitRequested && IsVisible && layout.All(pane => pane.Width > 0 && pane.Height > 0))
        {
            _fitRequested = false;
            FitAll(layout);
        }

        return finalSize;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            RequestFit();
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            TintCaptions();
        }
    }

    ModelView AddPane(StandardView? view, string name)
    {
        ModelView pane = new() { ClipToBounds = true };
        if (view is { } locked)
        {
            pane.Locked = locked;
        }

        pane.PointerEntered += (_, _) =>
        {
            if (!ReferenceEquals(_pointed, pane))
            {
                _pointed = pane;
                ActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Children.Add(pane);
        TextBlock caption = new() { Text = name, FontSize = 11, IsHitTestVisible = false };
        _captions[pane] = caption;
        Children.Add(caption);
        return pane;
    }

    void TintCaptions()
    {
        IBrush ink = new SolidColorBrush(CanvasPalette.For(ActualThemeVariant, _top.Look).Label, 0.75);
        foreach (TextBlock caption in _captions.Values)
        {
            caption.Foreground = ink;
        }
    }

    void OnDesignOpened(object? sender, EventArgs e) => RequestFit();

    /// <summary>The shared scale for the three drawings, then a fit in every pane.</summary>
    void FitAll(IReadOnlyList<SheetPane> layout)
    {
        TintCaptions();
        Bounds3 bounds = _editor is { } editor ? Bounds3.Of(editor.Sketch) : Bounds3.Empty;
        List<(SheetPane, double, double)> needs = [];
        if (!bounds.IsEmpty)
        {
            foreach (SheetPane pane in layout)
            {
                if (pane.View is not { } view)
                {
                    continue;
                }

                (Vector3d right, Vector3d up, _) = StandardViews.Axes(view);
                double minAcross = double.PositiveInfinity, maxAcross = double.NegativeInfinity;
                double minUp = double.PositiveInfinity, maxUp = double.NegativeInfinity;
                foreach (Vector3d corner in bounds.CornersInInches())
                {
                    minAcross = Math.Min(minAcross, Vector3d.Dot(corner, right));
                    maxAcross = Math.Max(maxAcross, Vector3d.Dot(corner, right));
                    minUp = Math.Min(minUp, Vector3d.Dot(corner, up));
                    maxUp = Math.Max(maxUp, Vector3d.Dot(corner, up));
                }

                needs.Add((pane, maxAcross - minAcross, maxUp - minUp));
            }
        }

        double? shared = SheetLayout.SharedScale(needs, ViewTransform.FitMarginFraction);
        foreach (ModelView pane in (ModelView[])[_top, _front, _right])
        {
            pane.FitScale = shared;
            pane.ZoomToFit();
        }

        _model.ZoomToFit();
    }
}
