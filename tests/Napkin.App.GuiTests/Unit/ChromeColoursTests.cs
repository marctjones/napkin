using Avalonia.Media;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>The chrome colour pairs of the napkin look (docs/design/napkin-look.md) keep readable contrast.</summary>
public class ChromeColoursTests
{
    static double Luminance(Color c)
    {
        static double Lin(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Lin(c.R)) + (0.7152 * Lin(c.G)) + (0.0722 * Lin(c.B));
    }

    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Fact]
    public void Text_on_the_bench_and_on_the_sheet_and_its_notes_reads_at_4_5_to_1_or_better()
    {
        (string Name, Color Text, Color Ground)[] pairs =
        [
            ("bench heading on bench", ChromeColours.BenchHeading, ChromeColours.Bench),
            ("bench text on bench", ChromeColours.BenchText, ChromeColours.Bench),
            ("carpenter ink on napkin sheet", SketchColours.CarpenterInk, SketchColours.Napkin),
            ("carpenter ink on napkin note", SketchColours.CarpenterInk, ChromeColours.NapkinNote),
            ("pencil ink on graph sheet", SketchColours.PencilInk, SketchColours.Graph),
            ("pencil ink on plain sheet", SketchColours.PencilInk, SketchColours.Plain),
        ];

        foreach ((string name, Color text, Color ground) in pairs)
        {
            Assert.True(Contrast(text, ground) >= 4.5, $"{name}: {Contrast(text, ground):0.0}:1");
        }
    }

    [Fact]
    public void The_bench_is_one_colour_and_the_styles_file_says_the_same()
    {
        string styles = File.ReadAllText(Path.Combine(RepositoryLayout.RepositoryRoot, "src", "Napkin.App", "Styles", "NapkinLook.axaml"));
        foreach ((string key, Color colour) in new[]
        {
            ("NapkinBenchColor", ChromeColours.Bench),
            ("NapkinBenchHeadingColor", ChromeColours.BenchHeading),
            ("NapkinBenchTextColor", ChromeColours.BenchText),
            ("NapkinBenchRuleColor", ChromeColours.BenchRule),
            ("NapkinNoteColor", ChromeColours.NapkinNote),
        })
        {
            string hex = $"#{colour.R:x2}{colour.G:x2}{colour.B:x2}";
            Assert.Contains($"x:Key=\"{key}\">{hex}<", styles, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_note_on_the_napkin_is_lighter_than_the_sheet_and_the_screen_look_keeps_its_own()
    {
        Assert.True(Luminance(ChromeColours.Note(SketchPaper.Napkin, SketchColours.Napkin)) > Luminance(SketchColours.Napkin));
        Assert.Equal(SketchColours.Graph, ChromeColours.Note(SketchPaper.Graph, SketchColours.Graph));
        Assert.Null(ChromeColours.NoteRule(SketchPaper.Screen));
        Assert.Equal(SketchColours.NapkinBorder, ChromeColours.NoteRule(SketchPaper.Napkin));
    }
}
