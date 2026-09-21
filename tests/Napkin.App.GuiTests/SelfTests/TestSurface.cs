using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace Napkin.App.GuiTests.SelfTests;

/// <summary>
/// A control that records the pointer input it receives, standing in for the drawing canvas the
/// application does not have yet.
/// </summary>
/// <remarks>
/// It is a plain <see cref="Control"/> with a background brush so that it is hit-testable: a
/// transparent control would let the pointer pass straight through, which is precisely the kind of
/// mistake these self-tests exist to catch.
/// </remarks>
public sealed class PointerPad : Control, ICustomHitTest
{
    public PointerPad()
    {
        Focusable = true;
        Background = Brushes.LightGray;
    }

    /// <summary>Points at which a press was received.</summary>
    public List<Point> Presses { get; } = [];

    /// <summary>Points through which the pointer moved while the left button was down.</summary>
    public List<Point> DragMoves { get; } = [];

    /// <summary>Points through which the pointer moved with no button down.</summary>
    public List<Point> HoverMoves { get; } = [];

    /// <summary>Points at which a release was received.</summary>
    public List<Point> Releases { get; } = [];

    /// <summary>Wheel deltas received, in order.</summary>
    public List<Vector> WheelDeltas { get; } = [];

    /// <summary>The click count reported for the most recent press.</summary>
    public int LastClickCount { get; private set; }

    /// <summary>The button reported for the most recent press.</summary>
    public MouseButton LastButton { get; private set; }

    /// <summary>The modifiers reported for the most recent press.</summary>
    public KeyModifiers LastPressModifiers { get; private set; }

    /// <summary>The fill drawn behind the pad.</summary>
    public IBrush? Background { get; set; }

    /// <summary>Everything inside the pad's bounds is hit-testable, so the pointer lands on it.</summary>
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    public override void Render(DrawingContext context)
    {
        if (Background is not null)
        {
            context.FillRectangle(Background, new Rect(Bounds.Size));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        Presses.Add(point.Position);
        LastClickCount = e.ClickCount;
        LastButton = point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
            _ => MouseButton.Left,
        };
        LastPressModifiers = e.KeyModifiers;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        (point.Properties.IsLeftButtonPressed ? DragMoves : HoverMoves).Add(point.Position);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Releases.Add(e.GetPosition(this));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        WheelDeltas.Add(e.Delta);
    }
}

/// <summary>
/// A window built for the harness's own tests: a text box, two buttons and a
/// <see cref="PointerPad"/>, laid out at fixed positions so a test can name coordinates.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately <em>not</em> part of the application. The suite's job is to drive what
/// napkin ships, and napkin's shell is still a scaffold; this surface exists only to prove that
/// each driver verb produces the input it claims to produce — that a drag really arrives as a
/// press, ordered moves with the button held, and a release, and so on. When the application grows
/// controls of its own, the workflows move onto them and this surface stays here, testing the
/// driver.
/// </para>
/// <para>Built in C# rather than XAML so the test project needs no XAML compiler.</para>
/// </remarks>
public sealed class TestSurface : Window
{
    public const double Width_ = 400;
    public const double Height_ = 320;

    public TestSurface()
    {
        Width = Width_;
        Height = Height_;

        Field = new TextBox
        {
            Name = "Field",
            Width = 360,
            Height = 32,
            [Canvas.LeftProperty] = 20d,
            [Canvas.TopProperty] = 20d,
        };

        FirstButton = new Button
        {
            Name = "FirstButton",
            Content = "First",
            Width = 100,
            Height = 32,
            [Canvas.LeftProperty] = 20d,
            [Canvas.TopProperty] = 70d,
        };
        FirstButton.Click += (_, _) => FirstButtonClicks++;

        SecondButton = new Button
        {
            Name = "SecondButton",
            Content = "Second",
            Width = 100,
            Height = 32,
            [Canvas.LeftProperty] = 140d,
            [Canvas.TopProperty] = 70d,
        };
        SecondButton.Click += (_, _) => SecondButtonClicks++;

        Pad = new PointerPad
        {
            Name = "Pad",
            Width = 360,
            Height = 180,
            [Canvas.LeftProperty] = 20d,
            [Canvas.TopProperty] = 120d,
        };

        Content = new Canvas { Children = { Field, FirstButton, SecondButton, Pad } };
        KeyDown += (_, e) => KeyChords.Add((e.Key, e.KeyModifiers));
    }

    /// <summary>The text box, first in tab order.</summary>
    public TextBox Field { get; }

    /// <summary>The first button, second in tab order.</summary>
    public Button FirstButton { get; }

    /// <summary>The second button, third in tab order.</summary>
    public Button SecondButton { get; }

    /// <summary>The pointer-driven area, at (20, 120) and 360x180 in size.</summary>
    public PointerPad Pad { get; }

    /// <summary>How many times the first button's Click event fired.</summary>
    public int FirstButtonClicks { get; private set; }

    /// <summary>How many times the second button's Click event fired.</summary>
    public int SecondButtonClicks { get; private set; }

    /// <summary>Every key that reached the window, with its modifiers.</summary>
    public List<(Key Key, KeyModifiers Modifiers)> KeyChords { get; } = [];

    /// <summary>A point inside the pad, given pad-relative coordinates.</summary>
    public static Point InPad(double x, double y) => new(20 + x, 120 + y);

    /// <summary>The centre of the first button, in window coordinates.</summary>
    public static Point FirstButtonCentre => new(70, 86);

    /// <summary>The centre of the second button, in window coordinates.</summary>
    public static Point SecondButtonCentre => new(190, 86);

    /// <summary>The centre of the text box, in window coordinates.</summary>
    public static Point FieldCentre => new(200, 36);

    /// <summary>Opens the surface and lets its first layout pass finish.</summary>
    public TestSurface Open()
    {
        Show();
        Dispatcher.UIThread.RunJobs();
        return this;
    }
}
