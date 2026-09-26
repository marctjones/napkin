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

    [GuiWorkflow("GUI-DECK-02")]
    public void Tick_a_decks_guard_and_stair_type_its_risers_and_buy_them() => GuiWorkflow.Run(app =>
    {
        // GUI-DECK-01's deck: 96 × 96 against the sample's existing wall, 3'-0" up, its north edge the ledger.
        MainWindow window = (MainWindow)app.Target;
        OpenSample(app, window, "Window in an existing wall");
        app.Wheel(At(window, Point2.Inches(72, -48)), new Vector(0, -4));
        app.Click(At(window, Point2.Inches(72, -140)));
        app.Press(Key.D, KeyModifiers.Shift);
        app.Drag(At(window, Point2.Inches(24, 0)), At(window, Point2.Inches(72, -48)), At(window, Point2.Inches(120, -96)));

        // Guard: napkin's starting layout on the three open edges.
        ClickInPanel(app, window, window.DeckTicks.Guard);
        app.Expect("the deck has a guard, and the check says why it cannot say one is required", () =>
        {
            Assert.NotNull(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Deck is not null).Deck!.Guard);
            Assert.Contains("Guard: ", window.DeckCheckLines, StringComparison.Ordinal);
        });

        // Stair: on the first open edge; with no adopted code there is no maximum riser, so it asks for the count.
        ClickInPanel(app, window, window.DeckTicks.Stair);
        app.Expect("the stair asks for its riser count", () =>
            Assert.Contains("Type the riser count", window.DeckCheckLines, StringComparison.Ordinal));

        // Five risers of 36 ÷ 5 = 7 1/5″ and four treads.
        TypeInto(app, window, window.DeckRisers, "5");
        app.Expect("the stair is laid out with five risers and four treads", () =>
        {
            Assert.Equal(5, window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => box.Deck is not null).Deck!.Stair!.Risers);
            Assert.Contains("Stair: Lay out 5 risers of", window.DeckCheckLines, StringComparison.Ordinal);
            Assert.Contains("4 treads", window.DeckCheckLines, StringComparison.Ordinal);
        });

        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the Deck section buys the guard's balusters, cap and rails and the stair's stringers", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingDeck);
            foreach (string lumber in new[] { "2x2", "2x6", "2x4", "2x12" })
            {
                Assert.Contains(list.DeckRows.Sorted, row => row.Material == lumber);
            }
        });

        // Two posts respan the beam: 96 − 7 = 89″.
        window.CutList!.Close();
        window.Activate();
        TypeInto(app, window, window.DeckControls.PostCount, "2");
        app.Expect("with two posts the beam spans 7'-5\" and the guard and stair stay", () =>
        {
            Assert.Contains("on 2 posts spanning 7'-5\"", window.DeckFrameLine, StringComparison.Ordinal);
            Assert.Contains("Stair: Lay out 5 risers of", window.DeckCheckLines, StringComparison.Ordinal);
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

    [GuiWorkflow("GUI-PORCH-02")]
    public void Build_the_worked_examples_porch_from_a_new_sheet() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        if (window.Editor.Selection.Count > 0)
        {
            app.Press(Key.Escape);
        }

        // Wheel out at the origin until the whole porch, 20 ft of house and 10 ft out, is on the paper.
        bool OnPaper(Point2 world) => new Rect(window.Canvas.Bounds.Size).Contains(window.Canvas.View.ToScreen(world));
        for (int i = 0; i < 20 && !(OnPaper(Point2.Inches(250, 10)) && OnPaper(Point2.Inches(-10, -140))); i++)
        {
            app.Wheel(At(window, Point2.Inches(0, 0)), new Vector(0, -2));
        }

        Assert.True(OnPaper(Point2.Inches(250, 10)) && OnPaper(Point2.Inches(-10, -140)), "the porch does not fit on the paper.");

        // The house: a 20 ft wall, its south face on y = 0, marked existing with Edit → Phase → Existing.
        app.Press(Key.W);
        app.Drag(At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(120, 1)), At(window, Point2.Inches(240, 1)));
        app.Click(CentreOf(window, window.EditMenuItem));
        app.Click(CentreOf(window, window.PhaseMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("PhaseExistingMenuItem")!));

        // §9's deck, 12'-0" along the house and 10'-0" out, once a click on empty paper gives the drawing the keys back.
        app.Click(At(window, Point2.Inches(120, -200)));
        app.Press(Key.D, KeyModifiers.Shift);
        app.Drag(At(window, Point2.Inches(48, 0)), At(window, Point2.Inches(120, -60)), At(window, Point2.Inches(192, -120)));
        app.Expect("Deck 1, 12'-0\" × 10'-0\", against the existing house wall", () =>
        {
            Assert.True(Deck.All(window.CurrentDesign!.Sketch).Length == 1, window.MessageOnScreen + " | tool " + window.Canvas.Tool + " | on screen " + At(window, Point2.Inches(192, -120)) + " canvas " + window.Canvas.Bounds);
            Deck deck = Assert.Single(Deck.All(window.CurrentDesign!.Sketch));
            Assert.Equal((Length.Inches(144), Length.Inches(120), DeckEdge.North), (deck.Box.Width, deck.Box.Height, deck.Ledger(window.CurrentDesign.Sketch).Edge));
        });

        // The front wall along the deck's south edge: drawn inside its outline, it stands on the decking; say it is bearing.
        app.Press(Key.W);
        app.Drag(At(window, Point2.Inches(48, -120)), At(window, Point2.Inches(120, -120)), At(window, Point2.Inches(192, -120)));
        Reveal(app, window, window.BearingControl);
        app.Click(CentreOf(window, window.BearingControl));
        app.Press(Key.Down);
        app.Press(Key.Enter);
        app.Expect("the front wall stands on the decking, 3'-0\" up, and is bearing", () =>
        {
            Wall front = Assert.Single(Wall.All(window.CurrentDesign!.Sketch), wall => wall.Box.Phase == Phase.New);
            Assert.Equal((Length.Inches(36), Length.Inches(144)), (front.Box.Anchor.Z, front.Length));
            Assert.True(front.Box.WallInputs!.Bearing);
        });

        // A window in it: Draw → Window, a click on the wall.
        app.Click(CentreOf(window, window.DrawMenuItem));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("WindowToolMenuItem")!));
        app.Click(At(window, Point2.Inches(120, -118)));

        // Shift+R on the deck, and 5 in 12: §9.5's 10 rafters of 141 3/8″ on the front wall's 3 1/2″ plates.
        app.Press(Key.R, KeyModifiers.Shift);
        app.Click(At(window, Point2.Inches(120, -60)));
        TypeInto(app, window, window.RoofControls.Pitch, "5 in 12");
        app.Expect("the roof bears on the front wall with ten 2x8 rafters, 11'-9 3/8\" long", () =>
        {
            Box roof = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>(), box => box.Roof is not null);
            Assert.Equal((Length.Inches(144), Length.Inches(120), Length.Inches(50), Length.Inches(132)), (roof.Width, roof.Height, roof.Depth, roof.Anchor.Z));
            Assert.IsType<WallLowEnd>(roof.Roof!.LowEnd);
            Assert.StartsWith("Roof 1: 5 in 12", window.RoofHeadlineText, StringComparison.Ordinal);
            Assert.StartsWith("10 rafters 2x8 × 11'-9 3/8\" at 16\"", window.RoofFrameLines, StringComparison.Ordinal);
            Assert.DoesNotContain("mark it bearing", window.RoofFrameLines, StringComparison.Ordinal);
        });

        // 36 × 42 of glass over 144 × 96 of wall and 144 × 141 3/8 of roof: 1512 ÷ 34182 = 4.4 %.
        app.Chord(Key.L, KeyModifiers.Shift);
        app.Expect("the Roof section buys the rafters and the sunroom line is 4.4 %", () =>
        {
            CutListWindow list = window.CutList!;
            Assert.True(list.IsShowingRoof);
            Assert.StartsWith("Roof 1: 10 rafters 2x8 × 11'-9 3/8\"", list.RoofNoteText, StringComparison.Ordinal);
            Assert.Contains(list.RoofRows.Sorted, row => row.Material == "2x8");
            Assert.StartsWith("Roof 1: Glazing 4.4 % of walls and roof", list.SunroomText, StringComparison.Ordinal);
            Assert.Contains("under the 40 % line", list.SunroomText, StringComparison.Ordinal);
        });
    });

    /// <summary>Scrolls the Part panel with the mouse wheel, either way, until <paramref name="control"/> is inside it.</summary>
    static void Reveal(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        double Bottom(Visual v) => v.TranslatePoint(new Point(0, v.Bounds.Height), window)!.Value.Y;
        double Top(Visual v) => v.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        for (int i = 0; i < 30; i++)
        {
            if (Bottom(control) > Bottom(scroller))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
            }
            else if (Top(control) < Top(scroller))
            {
                app.Wheel(CentreOf(window, scroller), new Vector(0, 1));
            }
            else
            {
                return;
            }
        }
    }

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

    /// <summary>A control in the Part panel, scrolled into sight, clicked.</summary>
    static void ClickInPanel(AppDriver app, MainWindow window, Control control)
    {
        ScrollViewer scroller = window.FindControl<ScrollViewer>("PropertiesScroller")!;
        for (int i = 0; i < 30 && control.TranslatePoint(new Point(0, control.Bounds.Height), window)!.Value.Y > scroller.TranslatePoint(new Point(0, scroller.Bounds.Height), window)!.Value.Y; i++)
        {
            app.Wheel(CentreOf(window, scroller), new Vector(0, -1));
        }

        app.Click(CentreOf(window, control));
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
