using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Adding and changing parts in the 3D view, driven the way a person drives it: the workflows
/// behind the 3D editing review's issues (#68).
/// </summary>
public class AssemblyEditingWorkflows
{
    [GuiWorkflow("GUI-ASSEM-03")]
    public void The_part_panel_follows_a_resize_in_the_3D_view_and_apply_keeps_it() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box apron = BoxNamed(window, "Apron, long, south");

        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.V);

        // Grow the apron upward by its top face's handle, two grid steps.
        ModelHandle handle = window.Model.FaceHandle(BoxFace.Top)
            ?? throw new InvalidOperationException("The apron's top face has no handle.");
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        double up = perInch * window.Model.GridStepInches * 2;
        app.Drag(
            InModel(window, handle.At),
            InModel(window, handle.At - new Vector(0, up / 2)),
            InModel(window, handle.At - new Vector(0, up)));

        Box grown = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
        string grownText = grown.Depth.Format(window.Editor.LabelFormat).Text;

        app.Expect("the apron grew, and the panel shows the depth it has now, not the one it had", () =>
        {
            Assert.True(grown.Depth > apron.Depth, "the resize did not make the apron deeper.");
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            Assert.Equal(grownText, window.OutOfPlaneField.Text);
        });

        // Rename it in the panel and apply: the resize must survive, and nothing may pin the depth.
        app.Click(CentreOf(window, window.PartNameField));
        app.Chord(Key.A);
        app.Type("Apron, front");
        app.Click(CentreOf(window, window.ApplyPart));

        app.Expect("the rename kept the resized depth and stated no typed depth", () =>
        {
            Box renamed = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal("Apron, front", renamed.Name);
            Assert.Equal(grown.Depth, renamed.Depth);
            Assert.DoesNotContain(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<ParamValue>(),
                value => value.Param == new BoxDepthRef(apron.Id));
        });

        app.SaveFrame("renamed-after-resize");

        // Two undos: the name, then the resize. The panel follows both without a reselect.
        app.Chord(Key.Z);
        app.Chord(Key.Z);

        app.Expect("after both undos the panel shows the original name and depth", () =>
        {
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            Assert.Equal(apron, window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!);
            Assert.Equal(apron.Name, window.PartNameField.Text);
            Assert.Equal(apron.Depth.Format(window.Editor.LabelFormat).Text, window.OutOfPlaneField.Text);
        });
    });

    [GuiWorkflow("GUI-ASSEM-04")]
    public void The_relationship_list_and_the_part_panel_share_one_column_without_overlapping() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        // The coffee table carries 33 relationships: far more rows than a 900x600 window has room
        // for beside the Part panel.
        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");
        app.Click(OnPlan(window, leg.Center.XY));

        app.Expect("in the plan, the list is open above the Part panel, neither over the other or the status bar", () =>
        {
            AssertSidePanelsApart(window);
            Assert.Contains("Leg, north-east", window.RelationshipsOnScreen[0], StringComparison.Ordinal);
        });

        app.SaveFrame("plan-side-panels");

        // The same in the 3D view, and after picking another part there.
        app.Press(Key.V);
        Box apron = BoxNamed(window, "Apron, long, south");

        // Low on the apron's south face, well clear of the top's edge: the top overhangs the apron
        // and hides its upper half from this view (and #89: an edge a few pixels away wins a click).
        Point3 low = SpaceSnapResolver.Extent(apron).Low;
        Vector3d onFace = new((low.X + (apron.Width.Divide(2, Rounding.HalfToEven))).ToInches(), low.Y.ToInches(), low.Z.ToInches() + 0.75);
        Point onApron = InModel(window, window.Model.Camera.Project(onFace));
        app.MoveTo(onApron);
        app.Click(onApron);

        app.Expect("in the 3D view, with the apron picked, the two panels are still apart and its rows come first", () =>
        {
            Assert.True(
                window.Editor.OnlySelected == apron.Id,
                $"the click picked {(window.Editor.OnlySelected is { } picked ? window.Editor.NameOf(picked) : "nothing")}, not the apron.");
            AssertSidePanelsApart(window);
            Assert.Contains("Apron, long, south", window.RelationshipsOnScreen[0], StringComparison.Ordinal);
        });

        app.SaveFrame("3d-side-panels");

        // Nothing selected: the Part panel goes. The list stays open while the pointer rests on the
        // apron (#62's hover), and closes to its badge once the pointer is off every part.
        app.Press(Key.Escape);

        app.Expect("with nothing selected there is no Part panel, and the hovered apron keeps the list open", () =>
        {
            Assert.False(window.IsShowingProperties);
            Assert.True(window.IsRelationshipListExpanded);
        });

        app.MoveTo(InModel(window, new Point(40, window.Model.Bounds.Height / 2)));

        app.Expect("off every part, the list is a badge again", () => Assert.False(window.IsRelationshipListExpanded));
    });

    [GuiWorkflow("GUI-ASSEM-05")]
    public void A_refused_turn_offers_to_let_go_and_turn_in_one_step() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");
        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.V);

        Sketch before = window.CurrentDesign!.Sketch;
        IReadOnlyList<Relationship> holding = SelectionTurn.HeldByPlace(before, leg.Id);
        app.Press(Key.X);

        app.Expect("the turn is refused, the parts holding the leg are outlined, and there is a way out", () =>
        {
            Assert.Same(before, window.CurrentDesign!.Sketch);
            Assert.NotEmpty(holding);
            Assert.Contains("did not happen", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Equal($"Let go of {holding.Count} and turn", window.OfferText);

            // Everything those relationships hold is outlined, the leg among them.
            HashSet<EntityId> held = [.. holding.SelectMany(relationship => relationship.References)];
            Assert.Contains(leg.Id, window.AttentionOnScreen);
            Assert.True(held.SetEquals(window.AttentionOnScreen), "the outline is not the parts the refusal names.");
        });

        app.SaveFrame("turn-refused");
        app.Click(CentreOf(window, window.OfferButton));

        app.Expect("the leg turned in place, exactly the relationships holding it by place are gone, the rest stay", () =>
        {
            Sketch after = window.CurrentDesign!.Sketch;
            Box turned = after.Find<Box>(leg.Id)!;
            Assert.Equal(BoxFace.North, turned.FaceUp);
            Assert.Equal(SpaceSnapResolver.Extent(leg).Low, SpaceSnapResolver.Extent(turned).Low);

            Assert.Empty(SelectionTurn.HeldByPlace(after, leg.Id));
            Assert.Equal(before.Relationships.Count - holding.Count, after.Relationships.Count);
            Assert.All(holding, relationship => Assert.False(after.Relationships.ContainsKey(relationship.Id)));
            Assert.Empty(window.AttentionOnScreen);
        });

        app.SaveFrame("let-go-and-turned");

        // One undo puts the leg and every relationship back.
        app.Chord(Key.Z);

        app.Expect("one undo restores the drawing exactly as it was", () =>
        {
            Sketch restored = window.CurrentDesign!.Sketch;
            Assert.Equal(leg, restored.Find<Box>(leg.Id));
            Assert.Equal(before.Relationships.Count, restored.Relationships.Count);
            Assert.All(holding, relationship => Assert.Equal(relationship, restored.Relationships[relationship.Id]));
        });
    });

    [GuiWorkflow("GUI-ASSEM-06")]
    public void A_relationship_is_removed_from_its_row_in_the_list() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");
        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.V);

        // The leg's rows come first; take the first, and rest the pointer on it.
        Sketch before = window.CurrentDesign!.Sketch;
        string sentence = window.RelationshipsOnScreen[0];
        Relationship said = before.RelationshipsInOrder.Single(relationship =>
            RelationshipText.Describe(before, relationship, window.Editor.NameOf, window.Editor.LabelFormat) == sentence);
        Control row = window.RelationshipRow(sentence)!;
        app.MoveTo(CentreOf(window, row));

        app.Expect("resting on the row outlines the parts it holds, and nothing else", () =>
        {
            Assert.True(said.References.ToHashSet().SetEquals(window.AttentionOnScreen));
            Assert.Contains(leg.Id, window.AttentionOnScreen);
        });

        app.SaveFrame("row-under-pointer");
        app.Click(CentreOf(window, window.RemoveRelationshipButton(sentence)!));

        app.Expect("the relationship is gone, the parts did not move, and the list no longer says it", () =>
        {
            Sketch after = window.CurrentDesign!.Sketch;
            Assert.False(after.Relationships.ContainsKey(said.Id));
            Assert.Equal(before.Relationships.Count - 1, after.Relationships.Count);
            Assert.All(before.Entities.Values.OfType<Box>(), box => Assert.Equal(box, after.Find<Box>(box.Id)));
            Assert.DoesNotContain(sentence, window.RelationshipsOnScreen);
            Assert.StartsWith("Removed:", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.Chord(Key.Z);

        app.Expect("undo brings it back", () =>
        {
            Assert.Equal(said, window.CurrentDesign!.Sketch.Relationships[said.Id]);
            Assert.Contains(sentence, window.RelationshipsOnScreen);
        });
    });

    [GuiWorkflow("GUI-ASSEM-07")]
    public void Zoom_to_fit_frames_the_drawing_beside_the_side_panels_in_both_views() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");
        app.Click(OnPlan(window, leg.Center.XY));
        app.Chord(Key.D0);

        app.Expect("in the plan, every part is framed left of the Part panel", () =>
        {
            Assert.True(window.IsShowingProperties);
            double panelLeft = BoundsIn(window, window.Properties).Left;
            foreach (Box box in window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>())
            {
                (Point3 low, Point3 high) = SpaceSnapResolver.Extent(box);
                double right = OnPlan(window, new Point2(high.X, high.Y)).X;
                Assert.True(right < panelLeft, $"{box.Name} reaches {right}, under the panel at {panelLeft}.");
            }
        });

        app.SaveFrame("plan-fitted-beside-panels");

        // The 3D view fits itself on the way in; Home fits it again from the isometric view.
        app.Press(Key.V);
        app.Press(Key.Home);

        app.Expect("in the 3D view, every corner of every part is framed left of the Part panel", () =>
        {
            Assert.True(window.IsShowingProperties);
            double panelLeft = BoundsIn(window, window.Properties).Left;
            foreach (Box box in window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>())
            {
                foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
                {
                    foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
                    {
                        double x = InModel(window, window.Model.ScreenOf(box.Vertex(corner, level))).X;
                        Assert.True(x < panelLeft, $"{box.Name} reaches {x}, under the panel at {panelLeft}.");
                    }
                }
            }
        });

        app.SaveFrame("3d-fitted-beside-panels");
    });

    [GuiWorkflow("GUI-ASSEM-08")]
    public void The_words_and_the_plan_mark_follow_a_part_when_it_is_turned() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");
        app.Click(OnPlan(window, leg.Center.XY));

        app.Expect("a standing leg's rows read by the way they run, and the list speaks in compass words", () =>
        {
            Assert.Equal(("East–west", "North–south", "Length (up)"), window.PartRowCaptions);
            Assert.All(window.RelationshipsOnScreen, line =>
            {
                Assert.DoesNotContain("left", line, StringComparison.Ordinal);
                Assert.DoesNotContain("right", line, StringComparison.Ordinal);
                Assert.DoesNotContain("bottom edge", line, StringComparison.Ordinal);
            });
            Assert.Contains(
                window.RelationshipsOnScreen,
                line => System.Text.RegularExpressions.Regex.IsMatch(line, "(north|south|east|west)(-(east|west))? (face|corner)"));
            Assert.Null(PartHelpText(window, leg.Id));
        });

        // A copy is free to turn; tip it back, about X.
        app.Press(Key.D);
        EntityId copy = window.Editor.OnlySelected!.Value;
        app.Press(Key.X);

        app.Expect("the tipped copy's rows follow it, and the plan marks it with the size that stands up", () =>
        {
            Assert.Equal(BoxFace.North, window.CurrentDesign!.Sketch.Find<Box>(copy)!.FaceUp);
            Assert.Equal(("East–west", "Up", "Length (north–south)"), window.PartRowCaptions);
            Assert.Equal("↑ thickness", PartHelpText(window, copy));
            Assert.Null(PartHelpText(window, leg.Id));
        });

        app.SaveFrame("plan-tipped-copy-marked");

        // Undo stands it up again, and the mark goes.
        app.Chord(Key.Z);

        app.Expect("stood up again, the copy carries no mark and its rows read as the leg's", () =>
        {
            Assert.Null(PartHelpText(window, copy));
            Assert.Equal(("East–west", "North–south", "Length (up)"), window.PartRowCaptions);
        });
    });

    /// <summary>The help text the plan canvas's automation element for a part carries.</summary>
    static string? PartHelpText(MainWindow window, EntityId part) =>
        Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(window.Canvas)
            .GetChildren()
            .Single(peer => peer.GetAutomationId() == part.Value.ToString("D"))
            .GetHelpText() is { Length: > 0 } text ? text : null;

    static void AssertSidePanelsApart(MainWindow window)
    {
        Assert.True(window.IsShowingProperties, "the Part panel is not showing.");
        Assert.True(window.IsRelationshipListExpanded, "the relationship list is not open.");

        Rect list = BoundsIn(window, window.Relationships);
        Rect panel = BoundsIn(window, window.Properties);
        Rect status = BoundsIn(window, window.StatusLine);

        Assert.False(list.Intersects(panel), $"the list {list} overlaps the Part panel {panel}.");
        Assert.True(list.Bottom <= status.Top, $"the list {list} runs into the status bar {status}.");
        Assert.True(panel.Bottom <= status.Top, $"the Part panel {panel} runs into the status bar {status}.");

        // The list is longer than its room, so it scrolls rather than spilling.
        ScrollViewer scroller = window.FindControl<ScrollViewer>("RelationshipsScroller")!;
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "the list fit, so this proved nothing about overflow.");
    }

    static Rect BoundsIn(Visual root, Visual control) =>
        new(control.TranslatePoint(new Point(0, 0), root)!.Value, control.Bounds.Size);
}
