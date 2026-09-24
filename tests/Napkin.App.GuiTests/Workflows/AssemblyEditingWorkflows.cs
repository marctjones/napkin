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
        // and hides its upper half from this view.
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

    [GuiWorkflow("GUI-ASSEM-09")]
    public void A_leg_stretched_by_its_top_handle_catches_the_underside_and_says_so_as_it_goes() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box top = BoxNamed(window, "Top");
        Box leg = BoxNamed(window, "Leg, north-east");
        Length underside = top.Anchor.Z;
        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.V);

        // Shrink it first, by its top face's handle, three grid steps down.
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        double step = window.Model.GridStepInches;
        ModelHandle handle = window.Model.FaceHandle(BoxFace.Top)!;
        app.Drag(InModel(window, handle.At), InModel(window, handle.At + new Vector(0, perInch * step * 3)));

        Box shrunk = window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!;
        app.Expect("the leg is shorter and nothing holds its top", () =>
        {
            Assert.True(shrunk.Depth < leg.Depth, "the leg did not get shorter.");
            Assert.DoesNotContain(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(),
                flush => flush.B == new FeatureRef(leg.Id, BoxFeature.Face(BoxFace.Top)));
        });

        // Stretch it back up, stopping short of the release to read the label by the pointer.
        ModelHandle again = window.Model.FaceHandle(BoxFace.Top)!;
        Point near = again.At - new Vector(0, perInch * ((underside - (shrunk.Anchor.Z + shrunk.Depth)).ToInches() - 0.2));
        app.PressAt(InModel(window, again.At));
        app.DragTo(InModel(window, again.At - new Vector(0, perInch)));
        app.DragTo(InModel(window, near));

        app.Expect("mid-drag, the readout gives the leg's length and the underside it caught", () =>
        {
            string readout = window.Model.LiveReadout ?? string.Empty;
            Assert.StartsWith("Length 1'-4 1/4\"", readout, StringComparison.Ordinal);
            Assert.Contains("flush with Top's bottom face", readout, StringComparison.Ordinal);
        });

        app.SaveFrame("stretching-to-the-underside");
        app.ReleaseAt(InModel(window, near));

        app.Expect("the leg's top is exactly at the underside, and a flush says so", () =>
        {
            Box stretched = window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!;
            Assert.Equal(underside, stretched.Anchor.Z + stretched.Depth);
            Assert.Contains(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(),
                flush => flush.A == new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom))
                         && flush.B == new FeatureRef(leg.Id, BoxFeature.Face(BoxFace.Top)));
            Assert.Null(window.Model.LiveReadout);
        });
    });

    [GuiWorkflow("GUI-ASSEM-10")]
    public void Pressing_on_a_part_and_dragging_moves_it_in_both_views_without_clicking_first() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");

        // The fixture's legs are held against the pinned top, so the part to drag is an unrelated
        // copy of one: made, then let go of.
        app.Click(OnPlan(window, BoxNamed(window, "Leg, south-west").Center.XY));
        app.Press(Key.D);
        Box leg = window.CurrentDesign!.Sketch.Find<Box>(window.Editor.OnlySelected!.Value)!;
        app.Press(Key.Escape);

        // In the plan: nothing selected, press on the copy and drag it two inches north.
        ViewTransform planView = window.Canvas.View;
        Point2 from = leg.Footprint().Center;
        app.Drag(OnPlan(window, from), OnPlan(window, from + new Vector2(Length.Zero, Length.Inches(1))), OnPlan(window, from + new Vector2(Length.Zero, Length.Inches(2))));

        app.Expect("the copy moved and is selected, and the plan's view did not move", () =>
        {
            Box moved = window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!;
            Assert.NotEqual(leg.Anchor.Y, moved.Anchor.Y);
            Assert.Equal(leg.Id, window.Editor.OnlySelected);
            Assert.Equal(planView, window.Canvas.View);
        });

        // In the 3D view: nothing selected, press low on the south apron's face and drag it along.
        app.Press(Key.Escape);
        app.Press(Key.V);
        Box apron = BoxNamed(window, "Apron, long, south");
        Camera camera = window.Model.Camera;
        Point3 low = SpaceSnapResolver.Extent(apron).Low;
        Vector3d onFace = new((low.X + apron.Width.Divide(2, Rounding.HalfToEven)).ToInches(), low.Y.ToInches(), low.Z.ToInches() + 0.75);
        Point grab = InModel(window, camera.Project(onFace));

        // Down: nothing holds the apron in Z (assembly-model §11 decision 9).
        Vector down = -camera.ProjectDirection(Vector3d.UnitZ) * window.Model.GridStepInches;
        app.Drag(grab, grab + down, grab + (down * 2));

        app.Expect("the apron slid in the plane of the face pressed, is selected, and the camera did not move", () =>
        {
            Box slid = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.True(slid.Anchor.Z < apron.Anchor.Z, "the apron did not go down.");
            Assert.Equal(apron.Anchor.Y, slid.Anchor.Y);
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            // The message bar may have changed the view's height; where it looks from has not changed.
            Assert.Equal(camera with { Viewport = window.Model.Camera.Viewport }, window.Model.Camera);
        });

        // The top is pinned: a drag on it moves nothing, and says why, with the way out.
        Box top = BoxNamed(window, "Top");
        Point onTop = InModel(window, window.Model.Camera.Project(new Vector3d(24, 12, 17)));
        app.Drag(onTop, onTop + new Vector(20, 0), onTop + new Vector(40, 0));

        int relationships = window.CurrentDesign!.Sketch.Relationships.Count;
        app.Expect("the pinned top did not move, stated nothing, and says it is pinned, with the way out", () =>
        {
            Assert.Equal(top, window.CurrentDesign!.Sketch.Find<Box>(top.Id));
            Assert.Equal(relationships, window.CurrentDesign!.Sketch.Relationships.Count);
            Assert.Equal("Moved Top did not happen: Top is pinned where it is.", window.MessageOnScreen);
            Assert.Equal("Unpin it", window.OfferText);
        });

        app.SaveFrame("pinned-top-refused");
        app.Click(CentreOf(window, window.OfferButton));

        app.Expect("taking the offer unpins the top, and nothing else", () =>
        {
            Assert.DoesNotContain(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Anchored>(),
                pin => pin.Entity == top.Id);
            Assert.Equal(relationships - 1, window.CurrentDesign!.Sketch.Relationships.Count);
            Assert.Equal(top, window.CurrentDesign!.Sketch.Find<Box>(top.Id));
        });
    });

    [GuiWorkflow("GUI-ASSEM-11")]
    public void A_typed_height_moves_a_part_exactly_and_what_is_flush_with_it_follows() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box top = BoxNamed(window, "Top");
        Box apron = BoxNamed(window, "Apron, long, south");
        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.V);

        // The fixture draws the apron touching the underside with nothing holding it there: a drag of
        // its Z arrow away and back states the flush the snap catches.
        ModelHandle arrow = window.Model.MoveHandle(Axis.Z)!;
        app.PressAt(InModel(window, arrow.At));
        app.DragTo(InModel(window, arrow.At + new Vector(0, 20)));
        app.DragTo(InModel(window, arrow.At));
        app.ReleaseAt(InModel(window, arrow.At));

        Flush held = new(RelationshipId.New(), new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)), new FeatureRef(apron.Id, BoxFeature.Face(BoxFace.Top)));
        app.Expect("the apron is held flush under the top", () =>
            Assert.Contains(window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(), flush => flush.A == held.A && flush.B == held.B));

        // Pick the top, and let go of its pin from its row in the list.
        app.Click(InModel(window, window.Model.Camera.Project(new Vector3d(24, 12, 17))));
        app.Click(CentreOf(window, window.RemoveRelationshipButton("Top is pinned where it is.")!));

        // Type how high off the floor it is to be, and press Enter.
        app.Click(CentreOf(window, window.PositionFields.Up));
        app.Chord(Key.A);
        app.Type("20\"");
        app.Press(Key.Enter);

        app.Expect("the top's underside is exactly 20\" up, and the apron held under it came with it", () =>
        {
            Box raised = window.CurrentDesign!.Sketch.Find<Box>(top.Id)!;
            Assert.Equal(Length.Inches(20), SpaceSnapResolver.Extent(raised).Low.Z);
            Assert.Equal(top.Anchor.X, raised.Anchor.X);
            Assert.Equal(top.Anchor.Y, raised.Anchor.Y);

            Box followed = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal(Length.Inches(20), followed.Anchor.Z + followed.Depth);
            Assert.Equal("1'-8\"", window.PositionFields.Up.Text);
        });

        app.SaveFrame("top-raised-by-typing");
    });

    [GuiWorkflow("GUI-ASSEM-12")]
    public void A_length_typed_after_or_during_a_drag_makes_it_exact_in_one_undo_step() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box apron = BoxNamed(window, "Apron, long, south");
        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.V);

        // Drag the Z arrow down a little, then say exactly how far: 3 1/2".
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        ModelHandle arrow = window.Model.MoveHandle(Axis.Z)!;
        app.Drag(InModel(window, arrow.At), InModel(window, arrow.At + new Vector(0, perInch)), InModel(window, arrow.At + new Vector(0, perInch * 2)));
        app.Type("3 1/2");

        app.Expect("what is typed shows by the pointer, waiting for Enter", () =>
            Assert.StartsWith("3 1/2 — Enter", window.Model.LiveReadout, StringComparison.Ordinal));

        app.Press(Key.Enter);

        app.Expect("the apron went down exactly 3 1/2\" from where the drag began", () =>
        {
            Box moved = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal(apron.Anchor.Z - Length.Inches(3, 1, 2), moved.Anchor.Z);
            Assert.Equal((apron.Anchor.X, apron.Anchor.Y), (moved.Anchor.X, moved.Anchor.Y));
            Assert.StartsWith("Moved Apron, long, south down 3 1/2\"", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // The drag and the typed length are one step: one undo puts the apron back.
        app.Chord(Key.Z);

        app.Expect("one undo puts the apron back where it was before the drag", () =>
            Assert.Equal(apron, window.CurrentDesign!.Sketch.Find<Box>(apron.Id)));

        // Now while dragging: grab the top face's handle, pull, type 5 and press Enter before letting go.
        ModelHandle face = window.Model.FaceHandle(BoxFace.Top)!;
        app.PressAt(InModel(window, face.At));
        app.DragTo(InModel(window, face.At - new Vector(0, perInch)));
        app.Type("5");
        app.Press(Key.Enter);
        app.ReleaseAt(InModel(window, face.At - new Vector(0, perInch)));

        app.Expect("typed mid-drag, the apron is exactly 5\" across its top and bottom, grown from its bottom up", () =>
        {
            Box grown = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal(Length.Inches(5), grown.Depth);
            Assert.Equal(apron.Anchor, grown.Anchor);
        });

        app.Chord(Key.Z);

        app.Expect("and one undo takes that back too", () =>
            Assert.Equal(apron, window.CurrentDesign!.Sketch.Find<Box>(apron.Id)));
    });

    [GuiWorkflow("GUI-ASSEM-13")]
    public void Mirror_a_leg_then_duplicate_a_leg_and_its_apron_together_and_move_the_pair_in_3D() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, south-west");
        Box apron = BoxNamed(window, "Apron, long, south");

        // Mirror the south-west leg east to west, by key: its twin lands where the south-east leg is.
        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.M);

        app.Expect("the mirror copy is 44\" to 46 1/2\" east-west, at the leg's north-south place and height", () =>
        {
            Box twin = window.CurrentDesign!.Sketch.Find<Box>(window.Editor.OnlySelected!.Value)!;
            Assert.NotEqual(leg.Id, twin.Id);
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(twin);
            Assert.Equal(new Point3(Length.Inches(44), SpaceSnapResolver.Extent(leg).Low.Y, Length.Zero), low);
            Assert.Equal(Length.Inches(46, 1, 2), high.X);
        });

        app.Chord(Key.Z);

        // Select the leg and the apron against it, and duplicate them together.
        app.Click(OnPlan(window, leg.Center.XY));
        app.Click(OnPlan(window, apron.Center.XY), MouseButton.Left, KeyModifiers.Shift);
        Sketch before = window.CurrentDesign!.Sketch;
        app.Press(Key.D);

        EntityId[] copies = [.. window.Editor.Selection];
        app.Expect("two copies, selected, held to each other the way the originals are", () =>
        {
            Assert.Equal(2, copies.Length);
            Assert.All(copies, copy => Assert.False(before.Entities.ContainsKey(copy)));
            Assert.Contains(
                window.CurrentDesign!.Sketch.RelationshipsInOrder,
                relationship => relationship.References.Distinct().Count() == 2 && relationship.References.All(copies.Contains));
        });

        // In the 3D view the pair has one set of arrows; raise them both by the Z arrow.
        app.Press(Key.V);
        Box firstBefore = window.CurrentDesign!.Sketch.Find<Box>(copies[0])!;
        Box secondBefore = window.CurrentDesign!.Sketch.Find<Box>(copies[1])!;
        ModelHandle arrow = window.Model.MoveHandle(Axis.Z)
            ?? throw new InvalidOperationException("The pair has no Z arrow.");
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        double up = perInch * window.Model.GridStepInches * 2;
        app.Drag(InModel(window, arrow.At), InModel(window, arrow.At - new Vector(0, up / 2)), InModel(window, arrow.At - new Vector(0, up)));

        app.Expect("both copies went up together, by the same amount, and the originals stayed", () =>
        {
            Box first = window.CurrentDesign!.Sketch.Find<Box>(copies[0])!;
            Box second = window.CurrentDesign!.Sketch.Find<Box>(copies[1])!;
            Vector3 moved = first.Anchor - firstBefore.Anchor;
            Assert.True(moved.Dz > Length.Zero, "the pair did not go up.");
            Assert.Equal(moved, second.Anchor - secondBefore.Anchor);
            Assert.Equal(leg, window.CurrentDesign!.Sketch.Find<Box>(leg.Id));
            Assert.Equal("Moved 2 parts.", window.MessageOnScreen);
        });

        app.SaveFrame("pair-raised");
    });

    [GuiWorkflow("GUI-ASSEM-14")]
    public void A_2x4_is_placed_on_the_top_and_dragged_out_on_the_floor_in_the_3D_view() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box top = BoxNamed(window, "Top");
        app.Press(Key.Escape);
        app.Press(Key.V);

        // Pick a 2x4 from the toolbox with the 3D view showing: it stays the 3D view.
        app.Click(CentreOf(window, window.Toolbox.CategoryButtons[Napkin.Core.Materials.StockCategory.DimensionalLumber]));
        app.Click(CentreOf(window, window.Toolbox.ItemButtons.Single(button => (button.Content as string) == "2x4")));

        // Rest the pointer on the top's upper face: the preview lies flat on it.
        Point onTop = InModel(window, window.Model.Camera.Project(new Vector3d(20, 12, 17)));
        app.MoveTo(onTop);

        app.Expect("the 3D view is still showing, and a 2x4 would lie flat on the top", () =>
        {
            Assert.True(window.IsShowingModel);
            PlacementPreview preview = window.Model.PlacementPreview
                ?? throw new InvalidOperationException("No preview under the pointer.");
            Assert.Equal(Length.Inches(17), SpaceSnapResolver.Extent(preview.Box).Low.Z);
            Assert.Equal("2x4", preview.Part!.Stock);
        });

        app.SaveFrame("2x4-over-the-top");
        Sketch before = window.CurrentDesign!.Sketch;
        app.Click(onTop);

        EntityId placed = window.Editor.OnlySelected ?? throw new InvalidOperationException("Nothing was placed.");
        app.Expect("one new 2x4 on the top, held there by a flush of the top's upper face and its underside", () =>
        {
            Sketch after = window.CurrentDesign!.Sketch;
            Box board = after.Find<Box>(placed)!;
            Assert.False(before.Entities.ContainsKey(placed));
            Assert.Equal(before.Entities.Count + 1, after.Entities.Count);
            Assert.Equal("2x4", board.Part!.Stock);
            Assert.Equal(Length.Inches(17), SpaceSnapResolver.Extent(board).Low.Z);
            Assert.Contains(
                after.RelationshipsInOrder.OfType<Flush>(),
                flush => flush.A == new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Top))
                         && flush.B == new FeatureRef(placed, BoxFeature.Face(BoxFace.Bottom)));
            Assert.StartsWith("Placed a 2x4 on Top's top face", window.MessageOnScreen, StringComparison.Ordinal);
        });

        app.SaveFrame("2x4-on-the-top");

        // One undo takes the part, its stock and the flush.
        app.Chord(Key.Z);

        app.Expect("one undo leaves the drawing as it was", () =>
        {
            Assert.Equal(before.Entities.Count, window.CurrentDesign!.Sketch.Entities.Count);
            Assert.Equal(before.Relationships.Count, window.CurrentDesign!.Sketch.Relationships.Count);
        });

        // On the floor, clear of the table: drag 30" north for its length.
        Point from = InModel(window, window.Model.Camera.Project(new Vector3d(60, -10, 0)));
        Point to = InModel(window, window.Model.Camera.Project(new Vector3d(60, 20, 0)));
        app.Drag(from, new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), to);

        app.Expect("a 2x4 as long as the drag lies on the floor, held by nothing", () =>
        {
            Sketch after = window.CurrentDesign!.Sketch;
            Box board = after.Find<Box>(window.Editor.OnlySelected!.Value)!;
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(board);
            Assert.Equal(Length.Zero, low.Z);
            Assert.True(high.Y - low.Y >= Length.Inches(28), $"it is {(high.Y - low.Y).ToInches()}\" long.");
            Assert.Equal(Length.Inches(3, 1, 2), high.X - low.X);
            Assert.DoesNotContain(after.RelationshipsInOrder, relationship => relationship.References.Contains(board.Id) && relationship is not ParamValue);
            Assert.StartsWith("Placed a 2x4 on the floor", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // Escape puts the 2x4 down.
        app.Press(Key.Escape);
        app.Expect("nothing is held any more", () =>
        {
            Assert.False(window.Model.Placement.IsArmed);
            Assert.Null(window.Model.PlacementPreview);
        });
    });

    [GuiWorkflow("GUI-ASSEM-15")]
    public void A_2x4_turned_on_end_before_it_is_placed_stands_on_the_top() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box top = BoxNamed(window, "Top");
        app.Press(Key.Escape);
        app.Press(Key.V);
        app.Click(CentreOf(window, window.Toolbox.CategoryButtons[Napkin.Core.Materials.StockCategory.DimensionalLumber]));
        app.Click(CentreOf(window, window.Toolbox.ItemButtons.Single(button => (button.Content as string) == "2x4")));

        // Over the top, stand it on end: Y, a quarter turn about the north-south axis.
        Point onTop = InModel(window, window.Model.Camera.Project(new Vector3d(20, 12, 17)));
        app.MoveTo(onTop);
        app.Press(Key.Y);

        app.Expect("the preview stands on end on the top, 24\" tall", () =>
        {
            PlacementPreview preview = window.Model.PlacementPreview
                ?? throw new InvalidOperationException("No preview under the pointer.");
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(preview.Box);
            Assert.Equal(Length.Inches(17), low.Z);
            Assert.Equal(Length.Inches(24), high.Z - low.Z);
        });

        app.SaveFrame("2x4-on-end-over-the-top");
        Sketch before = window.CurrentDesign!.Sketch;
        app.Click(onTop);

        EntityId placed = window.Editor.OnlySelected ?? throw new InvalidOperationException("Nothing was placed.");
        app.Expect("it was placed standing on end, resting on the top, held by a flush of the top's upper face", () =>
        {
            Box leg = window.CurrentDesign!.Sketch.Find<Box>(placed)!;
            (Point3 low, Point3 high) = SpaceSnapResolver.Extent(leg);
            Assert.Equal(Length.Inches(17), low.Z);
            Assert.Equal(Length.Inches(24), high.Z - low.Z);
            Assert.Equal("2x4", leg.Part!.Stock);
            Assert.Contains(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(),
                flush => flush.A == new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Top)) && flush.B.Owner == placed);
        });

        app.Chord(Key.Z);

        app.Expect("one undo removes it and what held it", () =>
        {
            Assert.Null(window.CurrentDesign!.Sketch.Find<Box>(placed));
            Assert.Equal(before.Relationships.Count, window.CurrentDesign!.Sketch.Relationships.Count);
        });
    });

    [GuiWorkflow("GUI-ASSEM-16")]
    public void The_3D_view_switches_to_perspective_from_the_menu_and_still_edits() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");

        // A copy of a leg, free of the pinned table, to edit.
        app.Click(OnPlan(window, BoxNamed(window, "Leg, south-west").Center.XY));
        app.Press(Key.D);
        EntityId copy = window.Editor.OnlySelected!.Value;
        app.Press(Key.V);

        app.Expect("the 3D view opens orthographic, as the status bar says, with the switch enabled and ticked", () =>
        {
            Assert.False(window.Model.Camera.IsPerspective);
            Assert.Contains("Orthographic", window.ZoomReadout.Text, StringComparison.Ordinal);
            Assert.True(window.FindControl<MenuItem>("PerspectiveMenuItem")!.IsEnabled);
            Assert.NotNull(window.FindControl<MenuItem>("OrthographicMenuItem")!.Icon);
            Assert.Null(window.FindControl<MenuItem>("PerspectiveMenuItem")!.Icon);
        });

        Camera orthographic = window.Model.Camera;
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("PerspectiveMenuItem")!));

        app.Expect("it is perspective now, framed the same at the centre, and the selection did not change", () =>
        {
            Camera camera = window.Model.Camera;
            Assert.True(camera.IsPerspective);
            Assert.Equal(orthographic.Center, camera.Center);
            Assert.Equal(orthographic.PixelsPerInch, camera.PixelsPerInch);
            Assert.Contains("Perspective", window.ZoomReadout.Text, StringComparison.Ordinal);
            Assert.NotNull(window.FindControl<MenuItem>("PerspectiveMenuItem")!.Icon);
            Assert.Equal(copy, window.Editor.OnlySelected);
        });

        app.SaveFrame("perspective-coffee-table");

        // Orbit with the keyboard; perspective orbits like orthographic does.
        double azimuth = window.Model.Camera.AzimuthDegrees;
        app.Press(Key.Left);
        app.Press(Key.Left);

        app.Expect("the view turned", () => Assert.NotEqual(azimuth, window.Model.Camera.AzimuthDegrees));

        // Pull the copy's top face up by its handle: the drag follows the pointer along the axis.
        Box before = window.CurrentDesign!.Sketch.Find<Box>(copy)!;
        ModelHandle top = window.Model.Handles.Single(handle => handle.Kind == ModelHandleKind.Face && handle.Face == BoxFace.Top);
        Point grab = InModel(window, top.At);
        app.Drag(grab, grab + new Vector(0, -20), grab + new Vector(0, -40));

        app.Expect("the copy grew taller, in perspective, and is still selected", () =>
        {
            Box grown = window.CurrentDesign!.Sketch.Find<Box>(copy)!;
            Assert.True(grown.Depth > before.Depth, $"the copy is {grown.Depth}, it was {before.Depth}.");
            Assert.True(grown.Depth < before.Depth + Length.Inches(24), "the drag ran away with the pointer.");
            Assert.Equal(copy, window.Editor.OnlySelected);
        });

        app.SaveFrame("perspective-drag-taller");
        app.Chord(Key.Z);

        app.Expect("one undo puts it back exactly", () =>
            Assert.Equal(before, window.CurrentDesign!.Sketch.Find<Box>(copy)));

        // O switches back, and the drawing did not change.
        app.Press(Key.O);

        app.Expect("orthographic again, by the keyboard, and the view still looks from the same side", () =>
        {
            Assert.False(window.Model.Camera.IsPerspective);
            Assert.Contains("Orthographic", window.ZoomReadout.Text, StringComparison.Ordinal);
            Assert.Equal(before, window.CurrentDesign!.Sketch.Find<Box>(copy));
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
        Rect panel = BoundsIn(window, window.PropertiesArea);
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
