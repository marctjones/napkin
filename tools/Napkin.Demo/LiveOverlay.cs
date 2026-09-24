using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

using Path = Avalonia.Controls.Shapes.Path;

namespace Napkin.Demo;

/// <summary>
/// What a person watching a live run sees on top of the window (#151): a pointer, because injected
/// input does not move the OS cursor; a ripple where a button goes down; and a caption bar, in the
/// napkin look's bench colours and Plex Mono, saying what the scenario is doing, the last key, and
/// each expectation — a tick, or the failure in rust.
/// </summary>
/// <remarks>
/// It lives in the window's overlay layer, where the live host's menus open too (it runs popups in
/// the overlay layer), and keeps the highest z-index there so it is drawn above them. Nothing in it
/// is hit-test visible, so it never takes the input it illustrates.
/// </remarks>
sealed class LiveOverlay : Canvas
{
    static readonly Geometry Arrow = Geometry.Parse("M0,0 L0,18 L4.6,13.8 L7.8,21 L10.9,19.7 L7.8,12.7 L13.5,12.7 Z");

    readonly Path _pointer;
    readonly TextBlock _say;
    readonly TextBlock _key;
    readonly TextBlock _expect;
    readonly Border _caption;
    readonly IBrush _text;
    readonly IBrush _moss;
    readonly IBrush _rust;

    LiveOverlay(Window window, string scenario)
    {
        IsHitTestVisible = false;
        ZIndex = int.MaxValue;

        IBrush bench = Resource(window, "SeBenchBrush", Brushes.DimGray);
        _text = Resource(window, "SeBenchTextBrush", Brushes.WhiteSmoke);
        IBrush quiet = Resource(window, "SeBenchQuietBrush", Brushes.Gray);
        _moss = Resource(window, "SeMossBrush", Brushes.LightGreen);
        _rust = Resource(window, "SeRustBrush", Brushes.OrangeRed);
        FontFamily mono = window.TryFindResource("SeFontMono", out object? font) && font is FontFamily family
            ? family : FontFamily.Default;

        _say = new TextBlock { Foreground = _text, FontFamily = mono, FontSize = 15, TextWrapping = TextWrapping.Wrap };
        _key = new TextBlock { Foreground = quiet, FontFamily = mono, FontSize = 12, Margin = new Thickness(0, 0, 14, 0) };
        _expect = new TextBlock { Foreground = _moss, FontFamily = mono, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var title = new TextBlock { Text = scenario + " · live", Foreground = quiet, FontFamily = mono, FontSize = 11 };

        _caption = new Border
        {
            Background = bench,
            BorderBrush = bench,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10),
            MaxWidth = 760,
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    title,
                    _say,
                    new StackPanel { Orientation = Orientation.Horizontal, Children = { _key, _expect } },
                },
            },
        };
        Children.Add(_caption);

        _pointer = new Path
        {
            Data = Arrow,
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 1.3,
            StrokeJoin = PenLineJoin.Round,
        };
        Children.Add(_pointer);
    }

    /// <summary>Adds an overlay to a shown window's overlay layer, kept the size of the window.</summary>
    public static LiveOverlay AttachTo(Window window, string scenario)
    {
        OverlayLayer layer = OverlayLayer.GetOverlayLayer(window)
            ?? throw new InvalidOperationException("The window has no overlay layer to draw the pointer in.");
        var overlay = new LiveOverlay(window, scenario);
        layer.Children.Add(overlay);
        overlay.Fit(window.ClientSize);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty)
            {
                overlay.Fit(window.ClientSize);
            }
        };

        // A popup opened later is added after us; move back to the top whenever the layer changes.
        layer.Children.CollectionChanged += (_, _) =>
        {
            if (layer.Children.Count > 0 && layer.Children[^1] != overlay)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (layer.Children.Remove(overlay))
                    {
                        layer.Children.Add(overlay);
                    }
                });
            }
        };
        return overlay;
    }

    public void MovePointer(Point at, bool pressed)
    {
        SetLeft(_pointer, at.X);
        SetTop(_pointer, at.Y);
        _pointer.Fill = pressed ? _rust : Brushes.White;
    }

    /// <summary>A ring that grows and fades where a button went down.</summary>
    public void Ripple(Point at)
    {
        var ring = new Ellipse { Stroke = _rust, StrokeThickness = 2.5 };
        Children.Insert(0, ring);
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            double t = (DateTime.UtcNow - started).TotalMilliseconds / 450;
            if (t >= 1)
            {
                timer.Stop();
                Children.Remove(ring);
                return;
            }

            double radius = 5 + (t * 22);
            ring.Width = ring.Height = radius * 2;
            ring.Opacity = 1 - t;
            SetLeft(ring, at.X - radius);
            SetTop(ring, at.Y - radius);
        };
        timer.Start();
    }

    public void Say(string text)
    {
        _say.Text = text;
        _expect.Text = "";
        _key.Text = "";
    }

    public void ShowKey(string key) => _key.Text = "⌨ " + key;

    public void Expect(string caption, bool passed)
    {
        _expect.Text = caption;
        _expect.Foreground = passed ? _moss : _rust;
        if (!passed)
        {
            _caption.BorderBrush = _rust;
        }
    }

    /// <summary>The end of the run, in the caption bar.</summary>
    public void Finish(string text, bool passed)
    {
        _say.Text = text;
        _say.Foreground = passed ? _text : _rust;
        _key.Text = "";
    }

    void Fit(Size size)
    {
        Width = size.Width;
        Height = size.Height;
        _caption.MaxWidth = Math.Max(200, Math.Min(760, size.Width - 48));
        _caption.Measure(new Size(_caption.MaxWidth, double.PositiveInfinity));
        SetLeft(_caption, 24);
        SetBottom(_caption, 44);
    }

    static IBrush Resource(Window window, string key, IBrush fallback) =>
        window.TryFindResource(key, window.ActualThemeVariant, out object? value) && value is IBrush brush ? brush : fallback;
}
