using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;
using Napkin.Interop.Pdf;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The permit set's paper as the app makes it (#226): the elevation faces the deck's open side and is
/// the locked view's own drawing; the title block says the site plan is not a survey.
/// </summary>
public class PermitPaperTests
{
    static Sketch Porch() => SampleExpectations.Sample("porch-12x10").Load().Sketch;

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void The_elevation_faces_the_deck_from_the_side_away_from_its_ledger()
    {
        // The porch's deck has its north edge on the house: seen from the south, Front.
        Sketch porch = Porch();
        Assert.Equal(DeckEdge.North, Deck.All(porch)[0].Ledger(porch).Edge);
        Assert.Equal(StandardView.Front, PermitPaper.ElevationFor(porch));
        Assert.Equal(StandardView.Front, PermitPaper.ElevationFor(Sketch.Empty));
    }

    [Fact]
    [Trait("Feature", "PERMIT-002")]
    public void The_deck_set_carries_the_locked_views_elevation_every_result_and_the_survey_disclaimer()
    {
        Sketch porch = Porch();
        DeckSet set = PermitPaper.DeckSet(porch, "Porch", new DateOnly(2026, 9, 27), new FeetInchesFormat(16), CodePacks.None, SheetPaper.Tabloid, hiddenEdges: true);
        DrawingView expected = PaperSheet.View(porch, StandardView.Front, new FeetInchesFormat(16), hiddenEdges: true);
        Assert.Equal(expected.Lines, set.Elevation!.Lines);
        Assert.Equal(expected.Dimensions.Select(dimension => dimension.Label), set.Elevation.Dimensions.Select(dimension => dimension.Label));
        Assert.Equal(PermitItems.Of(porch, CodePacks.None, Napkin.Core.Materials.MaterialsLibrary.Shipped).Select(item => item.What), set.Permit.Items.Select(item => item.What));
        Assert.Equal($"{ScopeDisclaimer.Text} {DeckSetPdf.NotASurvey}", set.Permit.Title.Disclaimer);
        Assert.Equal(SheetPaper.Tabloid, set.Permit.Paper);

        // Printed, every sheet says it is not a complete set: with no code, nothing is sized.
        using MemoryStream stream = new();
        DeckSetPdf.Write(set, stream);
        Excise.Core.Document.PdfDocument document = Excise.Core.Document.PdfDocument.Open(stream.ToArray());
        Assert.All(
            Enumerable.Range(0, document.PageCount),
            page => Assert.Contains("NOT A COMPLETE PERMIT SET", new Excise.Core.Text.TextExtractor(document.Pages[page]).ExtractText(TestContext.Current.CancellationToken), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(DeckEdge.South, StandardView.Back)]
    [InlineData(DeckEdge.East, StandardView.Left)]
    [InlineData(DeckEdge.West, StandardView.Right)]
    public void A_ledger_on_another_side_turns_the_elevation_with_it(DeckEdge ledger, StandardView expected)
    {
        // A 10' × 8' deck against a house wall on the given side of it.
        LayerId wallLayer = LayerId.New(), deckLayer = LayerId.New();
        Box deck = new(EntityId.New(), deckLayer, new Point3(Length.Inches(0), Length.Inches(0), Length.Zero), Length.Inches(120), Length.Inches(96), Length.Inches(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = new DeckInputs(JoistDirection.Out, Length.Inches(16), "2x8", new BeamSpec(2, "2x10"), "6x6", 2, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), false, null, null, null, null, null),
        };
        // A wall's length is its width, so one along the y axis is a 96" wall turned a quarter turn about
        // its anchor: its footprint then runs x (anchor − 6 … anchor), y (anchor … anchor + 96).
        (Point3 at, Length length, Angle turn) = ledger switch
        {
            DeckEdge.South => (new Point3(Length.Zero, Length.Inches(-6), Length.Inches(36)), Length.Inches(120), Angle.Zero),
            DeckEdge.East => (new Point3(Length.Inches(126), Length.Zero, Length.Inches(36)), Length.Inches(96), Angle.Degrees(90)),
            _ => (new Point3(Length.Zero, Length.Zero, Length.Inches(36)), Length.Inches(96), Angle.Degrees(90)),
        };
        Box house = new(EntityId.New(), wallLayer, at, length, Length.Inches(6), Length.Inches(96), BoxFace.Top, turn)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(wallLayer, BuildingLayers.Wall)).WithLayer(new Layer(deckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck);
        Assert.Equal(ledger, Deck.All(sketch)[0].Ledger(sketch).Edge);
        Assert.Equal(expected, PermitPaper.ElevationFor(sketch));
    }
}
