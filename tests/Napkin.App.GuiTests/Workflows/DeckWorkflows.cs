using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using Xunit;

using Point = Avalonia.Point;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Draw → Deck (docs/design/deck-and-porch.md §8, §11.3), driven the way a person drives it: a deck
/// dragged out from an existing wall, its frame read in the panel, a post count typed, and the deck's
/// boards on the shopping list.
/// </summary>
public class DeckWorkflows
{
    [GuiWorkflow("GUI-DECK-01")]
    public void Drag_a_deck_from_the_house_type_its_posts_and_buy_its_frame() => GuiWorkflow.Run(app =>
    {
        // The sample's existing wall runs x 0 to 144″ with its south face on y = 0. A deck dragged
        // from (24, 0) to (120, −96) is 96 × 96, its north edge on the face: the ledger.
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Window in an existing wall");
        app.Wheel(At(window, Point2.Inches(72, -48)), new Vector(0, -4));
        app.Click(At(window, Point2.Inches(72, -140)));

        app.Press(Key.D, KeyModifiers.Shift);
        app.Drag(At(window, Point2.Inches(24, 0)), At(window, Point2.Inches(72, -48)), At(window, Point2.Inches(120, -96)));
        app.Expect("a deck 8'-0\" square, 3'-0\" high, against the wall, with its frame in the panel", () =>
        {
            Box deck = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(), box => box.Deck is not null);
            Assert.Equal((Length.Inches(96), Length.Inches(96), Length.Inches(36)), (deck.Width, deck.Height, deck.Depth));
            Assert.Equal(DeckEdge.North, new Deck(deck).Ledger(window.CurrentDesign.Sketch).Edge);
            Assert.True(window.IsShowingDeck);
            Assert.StartsWith("Deck 1: 8'-0\" × 8'-0\", 3'-0\" above grade", window.DeckHeadlineText, StringComparison.Ordinal);

            // 96 wide: joists at 0 … 80 (six) and the end joist at 94 1/2; 3 posts: (96 − 10 1/2) ÷ 2 = 42 3/4″.
            Assert.StartsWith("Frame: ledger, 7 joists 2x8 at 16\", rim, (2) 2x10 beam on 3 posts spanning 3'-6 3/4\"", window.DeckFrameLine, StringComparison.Ordinal);
            Assert.Contains("its north edge is the ledger", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // Two posts: 96 − 7 = 89″ between them.
        TypeInto(app, window, window.DeckControls.PostCount, "2");
        app.Expect("with two posts the beam spans 7'-5\"", () =>
        {
            Assert.Equal(2, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Deck is not null).Deck!.PostCount);
            Assert.Contains("on 2 posts spanning 7'-5\"", window.DeckFrameLine, StringComparison.Ordinal);
        });

        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the shopping list has a Deck section with the seven 2x8 joists' boards and the deck's line", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingDeck);
            Assert.StartsWith("Deck 1: ledger, 7 joists 2x8", list.DeckNoteText, StringComparison.Ordinal);
            Assert.Contains(list.DeckRows.Sorted, row => row.Material == "2x8");
            Assert.Contains(list.DeckRows.Sorted, row => row.Material == "5/4x6");
        });
    });

    [GuiWorkflow("GUI-PORCH-01")]
    public void Roof_a_deck_type_its_pitch_buy_its_rafters_and_undo_it() => GuiWorkflow.Run(app =>
    {
        // A 96 × 96 deck against the sample's existing wall, as GUI-DECK-01 draws it: the ledger its north edge.
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Window in an existing wall");
        app.Wheel(At(window, Point2.Inches(72, -48)), new Vector(0, -4));
        app.Click(At(window, Point2.Inches(72, -140)));
        app.Press(Key.D, KeyModifiers.Shift);
        app.Drag(At(window, Point2.Inches(24, 0)), At(window, Point2.Inches(72, -48)), At(window, Point2.Inches(120, -96)));

        // Shift+R and a click on the deck: no wall stands on its far edge, so a beam on posts carries the low end.
        app.Press(Key.R, KeyModifiers.Shift);
        app.Click(At(window, Point2.Inches(72, -48)));
        app.Expect("a roof over the deck at napkin's starting 4 in 12, on a beam, 8'-0\" above the decking", () =>
        {
            Box roof = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(), box => box.Roof is not null);
            Assert.Equal((Length.Inches(96), Length.Inches(96), Length.Inches(32), Length.Inches(132)), (roof.Width, roof.Height, roof.Depth, roof.Anchor.Z));
            Assert.Equal(RoofTool.StartingBeam, roof.Roof!.LowEnd);
            Assert.True(window.IsShowingRoof);
            Assert.StartsWith("Roof 1: 4 in 12, 8'-0\" along the house, a run of 8'-0\"", window.RoofHeadlineText, StringComparison.Ordinal);
            Assert.Contains("Drew Roof 1 over Deck 1, high at the ledger, low on a (2) 2x10 beam", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Equal("Glazing: no wall stands on the deck yet, so there is nothing to take a ratio of.", window.RoofGlazingLine);
        });

        // 5 in 12 over a 96″ run is a 40″ rise and a whole 12 : 5 : 13 triangle, hypotenuse 104:
        // rafter run 96 − 1 1/2 + 12 = 106 1/2, length 106 1/2 × 104 ÷ 96 = 115 3/8 = 9'-7 3/8"; rafters at 0 … 80 and 94 1/2: seven.
        TypeInto(app, window, window.RoofControls.Pitch, "5 in 12");
        app.Expect("the rise is 3'-4\" and the rafters are seven 2x8s, 9'-7 3/8\" along the slope", () =>
        {
            Assert.Equal(Length.Inches(40), window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Roof is not null).Depth);
            Assert.StartsWith("Roof 1: 5 in 12", window.RoofHeadlineText, StringComparison.Ordinal);
            Assert.StartsWith("7 rafters 2x8 × 9'-7 3/8\" at 16\"", window.RoofFrameLines, StringComparison.Ordinal);
            Assert.Contains("set the square at 5 and 12", window.RoofFrameLines, StringComparison.Ordinal);
            Assert.StartsWith("Rafters 2x8 at 16\" o.c.", window.RoofCheckLine, StringComparison.Ordinal);
        });

        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the shopping list has a Roof section buying the rafters, the beam and its posts", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingRoof);
            Assert.StartsWith("Roof 1: 7 rafters 2x8", list.RoofNoteText, StringComparison.Ordinal);
            Assert.Contains(list.RoofRows.Sorted, row => row.Material == "2x8");
            Assert.Contains(list.RoofRows.Sorted, row => row.Material == "2x10");
            Assert.Contains(list.RoofRows.Sorted, row => row.Material == "4x4");
            Assert.Contains("2x8", list.RoofCsv, StringComparison.Ordinal);
        });

        // Edit → Undo takes back the pitch, then the roof (Cmd+Z with the pitch box still holding the keys
        // would take back its typing instead).
        window.CutList!.Close();
        window.Activate();
        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.UndoMenuEntry));
        app.Expect("the pitch is back to 4 in 12", () =>
            Assert.Equal(Length.Inches(32), window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Roof is not null).Depth));
        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.UndoMenuEntry));
        app.Expect("the roof is gone and the deck stays", () =>
        {
            Assert.DoesNotContain(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(), box => box.Roof is not null);
            Assert.Single(window.CurrentDesign.Sketch.Entities.Values.OfType<Box>(), box => box.Deck is not null);
        });
    });

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.SamplesMenuItem));
        MenuItem item = window.GetVisualDescendants().OfType<MenuItem>().Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    /// <summary>A text box in the Part panel, scrolled into sight: a click, the text over what was there, then Enter.</summary>
    static void TypeInto(AppDriver app, MainWindow window, TextBox box, string text)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        for (int i = 0; i < 30 && box.TranslatePoint(new Point(0, box.Bounds.Height), window)!.Value.Y > scroller.TranslatePoint(new Point(0, scroller.Bounds.Height), window)!.Value.Y; i++)
        {
            app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
        }

        app.Click(CentreOf(window, box));
        app.Press(Key.A, AppDriver.CommandModifier);
        app.Type(text);
        app.Press(Key.Enter);
    }

    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        return topLeft + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
    }
}
