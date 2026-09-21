using Avalonia;
using Napkin.App.Designs;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The arithmetic behind panning and zooming, tested without a window.
/// </summary>
/// <remarks>
/// These are ordinary unit tests: the view transform is a value with no dependency on Avalonia
/// beyond <see cref="Point"/> and <see cref="Size"/>, which is what makes "the model point under
/// the cursor stays under the cursor" a statement about a function rather than about a gesture. The
/// gestures themselves are covered by the GUI workflows, which drive the real control with real
/// input.
/// </remarks>
public class ViewTransformTests
{
    static readonly Size Viewport = new(900, 600);

    static ViewTransform SomeView => new(30, 12, 8, Viewport);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(48, 20)]
    [InlineData(-144.5, 96.25)]
    [InlineData(1200, -1200)]
    [Trait("Feature", "CVS-001")]
    public void Screen_and_world_are_inverses(double x, double y)
    {
        ViewTransform view = SomeView;

        Point screen = view.ToScreen(x, y);
        (double backX, double backY) = view.ToWorldInches(screen);

        Assert.Equal(x, backX, 9);
        Assert.Equal(y, backY, 9);
    }

    [Fact]
    [Trait("Feature", "CVS-003")]
    public void The_model_point_with_the_larger_y_is_drawn_higher()
    {
        ViewTransform view = SomeView;

        Point low = view.ToScreen(Point2.Inches(10, 0));
        Point high = view.ToScreen(Point2.Inches(10, 12));

        Assert.True(high.Y < low.Y, $"y=12\" drew at {high.Y}, which is not above y=0\" at {low.Y}.");
        Assert.Equal(low.X, high.X, 9);
    }

    [Fact]
    [Trait("Feature", "CVS-003")]
    public void A_foot_to_the_right_is_a_foot_to_the_right_on_screen()
    {
        ViewTransform view = SomeView;

        Point left = view.ToScreen(Point2.Inches(0, 0));
        Point right = view.ToScreen(Point2.Inches(12, 0));

        Assert.Equal(12 * view.PixelsPerInch, right.X - left.X, 9);
    }

    [Fact]
    public void The_centre_of_the_viewport_is_the_centre_of_the_view()
    {
        ViewTransform view = SomeView;

        Point centre = view.ToScreen(view.CenterXInches, view.CenterYInches);

        Assert.Equal(Viewport.Width / 2, centre.X, 9);
        Assert.Equal(Viewport.Height / 2, centre.Y, 9);
    }

    [Theory]
    [InlineData(0, 0, 1.15)]
    [InlineData(900, 600, 1.15)]
    [InlineData(137, 451, 0.5)]
    [InlineData(450, 300, 4)]
    [InlineData(12, 588, 1 / 1.15)]
    [Trait("Feature", "CVS-002")]
    public void Zooming_about_a_point_leaves_the_model_point_under_it(double x, double y, double factor)
    {
        ViewTransform view = SomeView;
        Point anchor = new(x, y);
        (double worldX, double worldY) = view.ToWorldInches(anchor);

        ViewTransform zoomed = view.ZoomAt(anchor, factor);
        Point after = zoomed.ToScreen(worldX, worldY);

        Assert.True(
            Math.Abs(after.X - anchor.X) < 1 && Math.Abs(after.Y - anchor.Y) < 1,
            $"the point under {anchor} moved to {after}.");
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void Zooming_past_a_limit_holds_the_anchor_rather_than_sliding()
    {
        // The requested factor is far past the limit, so the scale that is actually applied is the
        // limit. The anchor still has to be honoured, or a wheel turn at full zoom would drift.
        ViewTransform view = SomeView;
        Point anchor = new(200, 500);
        (double worldX, double worldY) = view.ToWorldInches(anchor);

        ViewTransform zoomed = view.ZoomAt(anchor, 1e6);

        Assert.Equal(ViewTransform.MaxPixelsPerInch, zoomed.PixelsPerInch, 9);
        Point after = zoomed.ToScreen(worldX, worldY);
        Assert.True(
            Math.Abs(after.X - anchor.X) < 1 && Math.Abs(after.Y - anchor.Y) < 1,
            $"the point under {anchor} moved to {after}.");
    }

    [Fact]
    public void The_scale_stays_between_its_limits_however_it_is_reached()
    {
        ViewTransform view = SomeView;

        ViewTransform tiny = view;
        ViewTransform huge = view;
        for (int i = 0; i < 200; i++)
        {
            tiny = tiny.ZoomAtCenter(0.5);
            huge = huge.ZoomAtCenter(2);
        }

        Assert.Equal(ViewTransform.MinPixelsPerInch, tiny.PixelsPerInch, 9);
        Assert.Equal(ViewTransform.MaxPixelsPerInch, huge.PixelsPerInch, 9);
        Assert.Equal(
            ViewTransform.MaxPixelsPerInch,
            (view with { PixelsPerInch = 1e9 }).PixelsPerInch,
            9);
    }

    [Fact]
    [Trait("Feature", "CVS-001")]
    public void A_drag_moves_the_drawing_with_the_hand()
    {
        ViewTransform view = SomeView;
        Point before = view.ToScreen(Point2.Inches(24, 6));

        ViewTransform panned = view.PanByPixels(new Vector(40, -25));
        Point after = panned.ToScreen(Point2.Inches(24, 6));

        Assert.Equal(before.X + 40, after.X, 9);
        Assert.Equal(before.Y - 25, after.Y, 9);
        Assert.Equal(view.PixelsPerInch, panned.PixelsPerInch, 9);
    }

    [Fact]
    [Trait("Feature", "CVS-001")]
    public void An_arrow_key_pans_by_a_fraction_of_the_viewport()
    {
        ViewTransform view = SomeView;

        ViewTransform right = view.PanByViewportFraction(0.1, 0);
        ViewTransform up = view.PanByViewportFraction(0, 0.1);

        // Panning right means the view moves right, so the drawing moves left on screen.
        Assert.Equal(
            view.ToScreen(Point2.Origin).X - (0.1 * Viewport.Width),
            right.ToScreen(Point2.Origin).X,
            9);
        Assert.Equal(
            view.ToScreen(Point2.Origin).Y + (0.1 * Viewport.Height),
            up.ToScreen(Point2.Origin).Y,
            9);
    }

    [Fact]
    public void A_resize_keeps_the_model_point_at_the_centre_and_the_scale()
    {
        ViewTransform view = SomeView;

        ViewTransform resized = view.WithViewport(new Size(640, 400));

        Assert.Equal(view.CenterXInches, resized.CenterXInches, 9);
        Assert.Equal(view.CenterYInches, resized.CenterYInches, 9);
        Assert.Equal(view.PixelsPerInch, resized.PixelsPerInch, 9);
        Assert.Equal(new Point(320, 200), resized.ToScreen(view.CenterXInches, view.CenterYInches));
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void A_fit_frames_the_whole_design_with_a_margin()
    {
        Design design = CoffeeTable();
        WorldBounds extents = SketchExtents.Of(design.Sketch);

        ViewTransform fitted = ViewTransform.Default.FitTo(extents, Viewport);

        AssertFramedWithMargin(fitted, extents);
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void A_fit_frames_a_single_part_with_a_margin()
    {
        WorldBounds one = WorldBounds.Empty
            .Including(Point2.Inches(100, 200))
            .Including(Point2.Inches(102, 205));

        ViewTransform fitted = ViewTransform.Default.FitTo(one, Viewport);

        AssertFramedWithMargin(fitted, one);
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void A_fit_of_something_with_no_extent_keeps_the_scale()
    {
        WorldBounds point = WorldBounds.Empty.Including(Point2.Inches(5, 5));
        ViewTransform view = SomeView;

        ViewTransform fitted = view.FitTo(point, Viewport);

        Assert.Equal(view.PixelsPerInch, fitted.PixelsPerInch, 9);
        Assert.Equal(5, fitted.CenterXInches, 9);
        Assert.Equal(5, fitted.CenterYInches, 9);
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void A_fit_of_nothing_at_all_changes_only_the_viewport()
    {
        ViewTransform view = SomeView;

        ViewTransform fitted = view.FitTo(WorldBounds.Empty, new Size(640, 480));

        Assert.Equal(view.PixelsPerInch, fitted.PixelsPerInch, 9);
        Assert.Equal(view.CenterXInches, fitted.CenterXInches, 9);
        Assert.Equal(new Size(640, 480), fitted.Viewport);
    }

    [Fact]
    [Trait("Feature", "CVS-002")]
    public void A_fit_of_a_whole_site_still_respects_the_zoom_limits()
    {
        // Four hundred feet across, which at any honest scale is below the minimum: the fit takes
        // the limit rather than a scale nothing can be seen at.
        WorldBounds huge = WorldBounds.Empty
            .Including(Point2.Origin)
            .Including(new Point2(Length.Feet(400), Length.Feet(400)));

        ViewTransform fitted = ViewTransform.Default.FitTo(huge, new Size(20, 20));

        Assert.InRange(
            fitted.PixelsPerInch,
            ViewTransform.MinPixelsPerInch,
            ViewTransform.MaxPixelsPerInch);
    }

    [Fact]
    [Trait("Feature", "CVS-001")]
    public void No_view_operation_touches_the_design()
    {
        Design design = CoffeeTable();
        Sketch before = design.Sketch;
        Sketch reference = CoffeeTable().Sketch;

        ViewTransform view = ViewTransform.Default
            .FitTo(SketchExtents.Of(design.Sketch), Viewport)
            .PanByPixels(new Vector(37, -12))
            .ZoomAt(new Point(120, 400), 1.15)
            .PanByViewportFraction(0.1, -0.1)
            .ZoomAtCenter(0.5)
            .WithViewport(new Size(300, 300));

        Assert.NotEqual(ViewTransform.Default, view);
        Assert.Same(before, design.Sketch);
        Assert.Equal(reference, design.Sketch);
    }

    /// <summary>
    /// The coffee table, read from the sample file the application ships. It is a real drawing
    /// rather than a rectangle invented here, which is the point: a fit has to frame a design with
    /// dimension lines standing off it, not just a box.
    /// </summary>
    static Design CoffeeTable() => SampleExpectations.Sample("coffee-table").Load();

    static void AssertFramedWithMargin(ViewTransform fitted, WorldBounds extents)
    {
        Point southWest = fitted.ToScreen(new Point2(extents.MinX, extents.MinY));
        Point northEast = fitted.ToScreen(new Point2(extents.MaxX, extents.MaxY));
        Rect drawn = new Rect(southWest, northEast).Normalize();
        Rect viewport = new(fitted.Viewport);

        Assert.True(viewport.Contains(drawn), $"{drawn} is not inside {viewport}.");

        // The margin is visible on the axis the fit is limited by, and at least as wide as asked
        // for on both.
        double marginX = Math.Min(drawn.Left, viewport.Right - drawn.Right);
        double marginY = Math.Min(drawn.Top, viewport.Bottom - drawn.Bottom);
        Assert.True(
            marginX >= (ViewTransform.FitMarginFraction * viewport.Width) - 0.5,
            $"horizontal margin {marginX} is too tight.");
        Assert.True(
            marginY >= (ViewTransform.FitMarginFraction * viewport.Height) - 0.5,
            $"vertical margin {marginY} is too tight.");
    }
}
