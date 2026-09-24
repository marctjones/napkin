using Avalonia;
using Avalonia.Media;

using Napkin.App.Settings;
using Napkin.App.Viewing;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>The sketch look's hand-drawn lines are repeatable, bounded, and change nothing but how a line is drawn (#142).</summary>
public sealed class SketchStrokeTests : IDisposable
{
    static readonly Point A = new(10, 20), B = new(410, 90);

    readonly string _dir = Path.Combine(Path.GetTempPath(), "napkin-sketch-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(SketchLine.Pencil)]
    [InlineData(SketchLine.Carpenter)]
    public void The_same_line_is_the_same_every_time_and_another_seed_is_another_line(SketchLine line)
    {
        Assert.Equal(SketchStroke.Wobble(A, B, 7, line), SketchStroke.Wobble(A, B, 7, line));
        Assert.NotEqual(SketchStroke.Wobble(A, B, 7, line), SketchStroke.Wobble(A, B, 8, line));
    }

    [Theory]
    [InlineData(SketchLine.Pencil)]
    [InlineData(SketchLine.Carpenter)]
    public void No_point_strays_further_than_the_kind_of_pencil_allows_and_the_ends_stay_put(SketchLine line)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            IReadOnlyList<Point> points = SketchStroke.Wobble(A, B, seed, line);
            Assert.Equal(A, points[0]);
            Assert.Equal(B, points[^1]);
            double length = Math.Sqrt((400 * 400) + (70 * 70));
            foreach (Point p in points)
            {
                double distance = Math.Abs(((B.X - A.X) * (A.Y - p.Y)) - ((A.X - p.X) * (B.Y - A.Y))) / length;
                Assert.True(distance <= SketchStroke.MaxOffset(line) + 1e-9, $"{distance} from the line, seed {seed}.");
            }
        }

        Assert.Equal(1.2, SketchStroke.MaxOffset(SketchLine.Pencil));
        Assert.Equal(1.8, SketchStroke.MaxOffset(SketchLine.Carpenter));
    }

    [Fact]
    public void A_line_wanders_somewhere()
    {
        double worst = SketchStroke.Wobble(A, B, 3, SketchLine.Carpenter)
            .Max(p => Math.Abs(((B.X - A.X) * (A.Y - p.Y)) - ((A.X - p.X) * (B.Y - A.Y))) / 405.9);
        Assert.True(worst > 0.2, "a sketched line that is straight is not sketched.");
    }

    [Fact]
    public void A_short_segment_the_clean_line_and_a_dot_are_straight()
    {
        Point near = new(13, 24);
        Assert.Equal([A, near], SketchStroke.Wobble(A, near, 1, SketchLine.Pencil));
        Assert.Equal([A, B], SketchStroke.Wobble(A, B, 1, SketchLine.Clean));
        Assert.Equal(2, SketchStroke.Wobble(A, A, 1, SketchLine.Carpenter).Count);
    }

    [Fact]
    public void A_long_line_is_broken_about_every_forty_pixels()
    {
        IReadOnlyList<Point> points = SketchStroke.Wobble(new Point(0, 0), new Point(400, 0), 1, SketchLine.Pencil);
        Assert.Equal(11, points.Count);
        Assert.Equal(3, SketchStroke.Wobble(new Point(0, 0), new Point(70, 0), 1, SketchLine.Pencil).Count);
    }

    [Fact]
    public void Drawn_from_the_other_end_it_is_the_same_curve()
    {
        IReadOnlyList<Point> there = SketchStroke.Wobble(A, B, 5, SketchLine.Carpenter);
        IReadOnlyList<Point> back = SketchStroke.Wobble(B, A, 5, SketchLine.Carpenter);
        Assert.Equal(there, back.Reverse());
        Assert.Equal(SketchStroke.SeedOf(1, 2, 30, 40), SketchStroke.SeedOf(30, 40, 1, 2));
        Assert.NotEqual(SketchStroke.SeedOf(1, 2, 30, 40), SketchStroke.SeedOf(1, 2, 30, 41));
    }

    [Fact]
    public void The_look_is_remembered_and_the_default_is_the_napkin_in_carpenters_pencil()
    {
        string file = Path.Combine(_dir, SettingsStore.FileName);
        Assert.Equal(SketchPaper.Napkin, new SettingsStore(file).Current.SketchPaper);
        Assert.Equal(SketchLine.Carpenter, new SettingsStore(file).Current.SketchLine);
        Assert.True(new SketchLook().IsClean);

        new SettingsStore(file).Update(s => s with { SketchPaper = SketchPaper.Graph, SketchLine = SketchLine.Pencil });

        SettingsStore again = new(file);
        Assert.Equal(SketchPaper.Graph, again.Current.SketchPaper);
        Assert.Equal(SketchLine.Pencil, again.Current.SketchLine);
    }

    [Fact]
    public void An_unknown_paper_or_an_old_file_without_the_fields_gives_the_default()
    {
        Directory.CreateDirectory(_dir);
        string file = Path.Combine(_dir, SettingsStore.FileName);

        File.WriteAllText(file, "{\"Version\":1,\"ShowRulers\":true}");
        SettingsStore old = new(file);
        Assert.True(old.Current.ShowRulers);
        Assert.Equal(new SketchLook(SketchPaper.Napkin, SketchLine.Carpenter), new SketchLook(old.Current.SketchPaper, old.Current.SketchLine));

        File.WriteAllText(file, "{\"Version\":1,\"SketchPaper\":\"Parchment\"}");
        SettingsStore unknown = new(file);
        Assert.Equal(SketchPaper.Napkin, unknown.Current.SketchPaper);
        Assert.NotNull(unknown.Notice);
    }

    [Theory]
    [InlineData(SketchPaper.Graph)]
    [InlineData(SketchPaper.Plain)]
    [InlineData(SketchPaper.Napkin)]
    public void Ink_reads_on_every_sheet_and_the_sheet_is_light_in_either_theme(SketchPaper paper)
    {
        foreach (SketchLine line in new[] { SketchLine.Pencil, SketchLine.Carpenter })
        {
            Color ink = SketchColours.Ink(line)!.Value;
            Assert.True(Contrast(ink, SketchColours.Sheet(paper)) >= 4.5, $"{line} on {paper}.");
        }

        foreach (Avalonia.Styling.ThemeVariant theme in new[] { Avalonia.Styling.ThemeVariant.Dark, Avalonia.Styling.ThemeVariant.Light })
        {
            CanvasPalette palette = CanvasPalette.For(theme, new SketchLook(paper, SketchLine.Clean));
            Assert.Equal(SketchColours.Sheet(paper), palette.Background);
            Assert.True(Contrast(palette.Label, palette.Background) >= 4.5);
            Assert.All(palette.Styles.Values, style => Assert.True(Contrast(style.Stroke, palette.Background) >= 3, $"{style.Stroke} on {paper}."));
        }
    }

    [Fact]
    public void The_clean_look_is_the_theme_palette_untouched_and_a_line_on_screen_paper_keeps_the_theme()
    {
        Assert.Same(CanvasPalette.Dark, CanvasPalette.For(Avalonia.Styling.ThemeVariant.Dark, new SketchLook()));
        CanvasPalette pencilOnDark = CanvasPalette.For(Avalonia.Styling.ThemeVariant.Dark, new SketchLook(SketchPaper.Screen, SketchLine.Pencil));
        Assert.Equal(CanvasPalette.Dark.Background, pencilOnDark.Background);
        Assert.Equal(SketchLine.Pencil, pencilOnDark.Look.Line);
    }

    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    static double Luminance(Color c)
    {
        static double Linear(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }
}
