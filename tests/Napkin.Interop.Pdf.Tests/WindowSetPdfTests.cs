using System.Globalization;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.Tests.ReadBack;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The window set (#227, docs/design/permit-set.md §2) against the SYNTHETIC us-zz-frame pack (made-up
/// numbers; CodePacks/README.md): a 12 ft exterior bearing wall carrying zz-roof at snow 30, with a
/// 60″ window 12″ from its start, sill 36″, 42″ tall — ZZ-HEADER row r.s30.b, (2) 2x10, 1 jack and 2 kings.
/// </summary>
public class WindowSetPdfTests
{
    static readonly CodePacks One = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "one")]);
    static readonly LayerId WallLayer = LayerId.New(), OpeningLayer = LayerId.New();

    static Length In(long whole) => Length.Inches(whole);

    static Sketch Wall(string? supports = "zz-roof", bool bearing = true, int windows = 1)
    {
        Box wall = new Box(EntityId.New(), WallLayer, Point3.Origin, In(480), Length.Inches(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs(supports, null) { Side = WallSide.Exterior, Bearing = bearing },
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wall) with
        {
            Code = new CodeChoice("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25)),
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };
        for (int i = 0; i < windows; i++)
        {
            sketch = sketch.WithEntity(new Box(EntityId.New(), OpeningLayer, new Point3(In(12 + (i * 80)), Length.Zero, In(36)), In(60), Length.Inches(3, 1, 2), In(42), BoxFace.Top, Angle.Zero)
            {
                Name = $"Window {i + 1}",
            });
        }

        return sketch;
    }

    static ReadBack Read(Sketch sketch, DrawingView? plan = null)
    {
        PermitSet permit = new(
            new TitleBlock("Window move", new DateOnly(2026, 9, 27), SheetNotes.CodeLine(sketch, One), $"{ScopeDisclaimer.Text} {PermitSheets.NotASurvey}"),
            new FeetInchesFormat(16),
            SheetPaper.Letter,
            PermitItems.Of(sketch, One, MaterialsLibrary.Shipped));
        using MemoryStream stream = new();
        WindowSetPdf.Write(new WindowSet(permit, sketch, One, MaterialsLibrary.Shipped, plan, null), stream);
        return ReadBack.Open(stream.ToArray());
    }

    [Fact]
    [Trait("Feature", "PERMIT-003")]
    public void A_window_set_is_its_site_plan_plan_elevation_header_details_code_page_and_worksheet()
    {
        DrawingView top = new(StandardView.Top, [new DrawingLine(LineKind.Visible, new(0, 0), new(480, 0))], []);
        ReadBack read = Read(Wall(), top);
        string[] titles = ["S1 Site plan", "A1 Plan: Top", "A2 Elevation", "S3 Details", PermitSetPdf.CodePage, PermitSetPdf.Worksheet];
        Assert.Equal(titles.Length, read.Text.Count);
        for (int page = 0; page < titles.Length; page++)
        {
            Assert.Contains(titles[page], read.Text[page], StringComparison.Ordinal);
        }

        // A1 and A2 say where the window is and how high, in the canvas's words.
        Assert.Contains(Squash("Window 1 (window): 5'-0\" wide, its near side 1'-0\" from the start of Wall 1, which is 40'-0\" long."), read.Letters[1], StringComparison.Ordinal);
        Assert.Contains(Squash("Window 1: rough opening 5'-0\" × 3'-6\", sill 3'-0\", head 6'-6\"; Wall 1 8'-0\" tall."), read.Letters[2], StringComparison.Ordinal);
        Assert.Contains("Scale 1:", read.Text[1], StringComparison.Ordinal);

        // S3: the header, sized, with its jacks and kings and its citation as C1 prints it.
        Assert.Contains(Squash("Header: (2) 2x10, 1 jack and 2 king studs each side"), read.Letters[3], StringComparison.Ordinal);
        Assert.Contains("row r.s30.b", read.Text[3], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-003")]
    public void A_header_napkin_cannot_size_prints_a_blank_and_a_not_bearing_one_the_persons_own_choice()
    {
        ReadBack unsized = Read(Wall(supports: null));
        int s3 = unsized.Text.ToList().FindIndex(text => text.Contains("S3 Details", StringComparison.Ordinal));
        Assert.DoesNotContain(Squash("Header: (2)"), unsized.Letters[s3], StringComparison.Ordinal);
        MatchCollection rules = Regex.Matches(unsized.Operators[s3], @"0 G\n0\.8 w\n(\S+) (\S+) m\n(\S+) \2 l\nS");
        Assert.Contains(rules, rule => Math.Abs(double.Parse(rule.Groups[3].Value, CultureInfo.InvariantCulture) - double.Parse(rule.Groups[1].Value, CultureInfo.InvariantCulture) - PermitSheets.SizeBlank) < 1e-6);

        ReadBack notBearing = Read(Wall(bearing: false));
        int page = notBearing.Text.ToList().FindIndex(text => text.Contains("S3 Details", StringComparison.Ordinal));
        Assert.Contains(Squash("Header: not checked: not bearing, no header chosen"), notBearing.Letters[page], StringComparison.Ordinal);
    }

    [Fact]
    public void More_openings_than_a_sheet_holds_run_onto_a_continued_details_sheet()
    {
        ReadBack read = Read(Wall(windows: 5));
        Assert.Contains(read.Text, text => text.Contains("S3 Details", StringComparison.Ordinal) && !text.Contains("continued", StringComparison.Ordinal));
        Assert.Contains(read.Text, text => text.Contains("S3 Details, continued", StringComparison.Ordinal));
        Assert.Contains(read.Letters, letters => letters.Contains(Squash("Window 5: header"), StringComparison.Ordinal));
    }
}
