using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>A part's grain and show face on the cut list and in the Parts view (#140).</summary>
public class GrainTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static Box Board(string name, Part part, long x = 0)
        => Box.AsDrawn(EntityId.New(), LayerId.Default, new Point2(new Length(x), Length.Zero), Length.Inches(24), Length.Inches(3, 1, 2), new Length(1536), Angle.Zero) with { Name = name, Part = part };

    private static readonly Part TwoByFour = new("2x4", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));

    [Fact]
    public void AStatedGrainAndShowFaceAreNotesOnTheRowAndSplitItFromTheUnsaid()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(
            Sketch.Empty
                .WithEntity(Board("Rail", TwoByFour with { Grain = PartDimension.Length, ShowFace = BoxFace.Top }))
                .WithEntity(Board("Rail", TwoByFour, x: 30720)),
            Library);

        Assert.Equal(2, rows.Length);
        CutListRow said = Assert.Single(rows, row => row.Grain is not null);
        Assert.Equal(["Grain along its length.", "Show face: top."], said.Flags);
        Assert.Empty(Assert.Single(rows, row => row.Grain is null).Flags);
        Assert.Contains("Grain along its length.; Show face: top.", CutListCsv.ToCsv(rows), StringComparison.Ordinal);
    }

    [Fact]
    public void ABoardAskedToRunItsGrainAcrossItselfIsWarned()
    {
        CutListRow across = Assert.Single(CutList.Of(Sketch.Empty.WithEntity(Board("Rail", TwoByFour with { Grain = PartDimension.Width })), Library));
        Assert.Contains(across.Flags, flag => flag.StartsWith("Check the grain: a board's grain runs its length", StringComparison.Ordinal));

        // A sheet good's grain may run either way on the sheet: no warning.
        Part panel = new("3/4 plywood", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)) { Grain = PartDimension.Width };
        CutListRow sheet = Assert.Single(CutList.Of(Sketch.Empty.WithEntity(Board("Panel", panel)), Library));
        Assert.Equal(["Grain along its width."], sheet.Flags);
    }

    [Fact]
    public void TheCellSaysWhichWayTheGrainRuns()
    {
        PartsCell cell = Assert.Single(PartsSheet.Of(CutList.Of(Sketch.Empty.WithEntity(Board("Rail", TwoByFour with { Grain = PartDimension.Length })), Library)));

        Assert.Contains("grain along its length", PartsCellText.Details(cell, PartsPicture.Of(cell), notToScale: false), StringComparison.Ordinal);
    }

    [Fact]
    public void GrainAndShowFaceAreInAPartsEquality()
    {
        Part with = TwoByFour with { Grain = PartDimension.Length, ShowFace = BoxFace.North };
        Assert.NotEqual(TwoByFour, with);
        Assert.Equal(with, TwoByFour with { Grain = PartDimension.Length, ShowFace = BoxFace.North });
        Assert.Equal(with.GetHashCode(), (TwoByFour with { Grain = PartDimension.Length, ShowFace = BoxFace.North }).GetHashCode());
    }
}
