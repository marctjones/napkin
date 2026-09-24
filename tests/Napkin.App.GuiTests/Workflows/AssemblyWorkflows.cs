using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;

using Xunit;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Parts turned and set against one another in the 3D view, driven the way a person drives it
/// (<c>docs/design/assembly-model.md</c> &#xA7;9.3).
/// </summary>
/// <remarks>
/// <para>
/// &#xA7;9.3 names three workflows. Two are here. The third — "the plan is untouched" — is not a new
/// scenario: it is every existing <c>GUI-DRAW</c>, <c>GUI-CUT</c> and <c>GUI-VIEW</c> workflow
/// passing unchanged beside these two, which the suite checks on every run.
/// </para>
/// <para>
/// Both start in the plan and switch by keyboard, so each asserts on the way that the 3D view is the
/// same drawing with the same selection (&#xA7;8.1) rather than a second document.
/// </para>
/// </remarks>
public class AssemblyWorkflows
{
    [GuiWorkflow("GUI-ASSEM-01")]
    public void Turn_a_part_in_the_3D_view_and_see_its_side_in_the_plan() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box leg = BoxNamed(window, "Leg, north-east");

        // Select the leg in the plan, then switch views by keyboard.
        app.Click(OnPlan(window, leg.Center.XY));
        app.Press(Key.V);

        app.Expect("the 3D view is showing the same drawing with the same part selected", () =>
        {
            Assert.True(window.IsShowingModel, "V did not switch to the 3D view.");
            Assert.False(window.Canvas.IsVisible, "the plan canvas is still showing.");
            Assert.Equal(leg.Id, window.Editor.OnlySelected);
            Assert.All(window.TurnButtons, button => Assert.True(button.IsVisible));
        });

        app.SaveFrame("3d-leg-selected");

        // The fixture's leg is held by flushes and distances named in its own frame, and turning it
        // would change what they mean: the updater refuses, and the message offers to let go of
        // them and turn (§2.4, #76) — not taken here; GUI-ASSEM-05 takes it.
        app.Press(Key.X);

        app.Expect("a leg held in place by its relationships is refused a turn, and nothing changes", () =>
        {
            Box unturned = window.CurrentDesign!.Sketch.Find<Box>(leg.Id)!;
            Assert.Equal(BoxFace.Top, unturned.FaceUp);
            Assert.Equal(leg, unturned);
            Assert.Contains("did not happen", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.Contains("would change what they mean", window.MessageOnScreen, StringComparison.Ordinal);
            Assert.StartsWith("Let go of", window.OfferText, StringComparison.Ordinal);
        });

        // A copy of the leg is unrelated until it is snapped (shaped-parts §2.6): that one turns.
        app.Press(Key.D);
        EntityId copy = window.Editor.OnlySelected!.Value;
        app.Press(Key.X);

        app.Expect("the copy is turned a quarter turn about X: its north face is up now", () =>
        {
            Assert.NotEqual(leg.Id, copy);
            Box turned = window.CurrentDesign!.Sketch.Find<Box>(copy)!;
            Assert.Equal(BoxFace.North, turned.FaceUp);
            Assert.Equal(Angle.Zero, turned.Rotation);

            // The sizes are the leg's: a turn moves nothing and resizes nothing (§2.4).
            Assert.Equal(leg.Width, turned.Width);
            Assert.Equal(leg.Height, turned.Height);
            Assert.Equal(leg.Depth, turned.Depth);
        });

        app.SaveFrame("3d-copy-turned");

        // Back to the plan, by keyboard again: what the plan sees of the turned leg is its side.
        app.Press(Key.V);

        app.Expect("in the plan the turned leg's footprint is its side, 2 1/2\" by 16 1/4\"", () =>
        {
            Assert.False(window.IsShowingModel);
            Assert.True(window.Canvas.IsVisible);

            Footprint footprint = window.CurrentDesign!.Sketch.Find<Box>(copy)!.Footprint();
            Assert.Equal(Length.Inches(2, 1, 2), footprint.PlanWidth);
            Assert.Equal(Length.Inches(16, 1, 4), footprint.PlanHeight);
            Assert.Equal(copy, window.Editor.OnlySelected);
        });

        app.SaveFrame("plan-copy-lying-down");

        // One undo takes the turn back, and the leg stands up again: a square in the plan.
        app.Chord(Key.Z);

        app.Expect("undo stands the copy back up, and its footprint is square again", () =>
        {
            Box standing = window.CurrentDesign!.Sketch.Find<Box>(copy)!;
            Assert.Equal(BoxFace.Top, standing.FaceUp);
            Footprint footprint = standing.Footprint();
            Assert.Equal(footprint.PlanWidth, footprint.PlanHeight);
            Assert.Equal(Length.Inches(2, 1, 2), footprint.PlanWidth);
        });
    });

    [GuiWorkflow("GUI-ASSEM-02")]
    public void Set_an_apron_under_the_top_in_the_3D_view_then_type_its_depth() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;

        OpenSample(app, window, "Coffee table");
        Box top = BoxNamed(window, "Top");
        Box apron = BoxNamed(window, "Apron, long, south");
        Length underside = top.Anchor.Z;

        // Pick the apron in the plan — the top covers it there, and the smaller part wins — and go
        // to the 3D view by keyboard.
        app.Click(OnPlan(window, apron.Center.XY));
        app.Press(Key.V);

        app.Expect("the apron is still the selection, and it carries a move arrow along Z", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.Equal(apron.Id, window.Editor.OnlySelected);
            Assert.NotNull(window.Model.MoveHandle(Axis.Z));
        });

        app.SaveFrame("3d-apron-selected");

        // The fixture draws the apron with its top already at the top's underside, but nothing
        // holds it there (§11 decision 9). Drag its Z arrow down three inches first...
        ModelHandle zArrow = window.Model.MoveHandle(Axis.Z)!;
        double perInch = -window.Model.Camera.ProjectDirection(Vector3d.UnitZ).Y;
        app.Drag(
            InModel(window, zArrow.At),
            InModel(window, zArrow.At + new Vector(0, perInch * 1.5)),
            InModel(window, zArrow.At + new Vector(0, perInch * 3)));

        app.Expect("the apron went down along Z only, and nothing was caught to say about it", () =>
        {
            Box lowered = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.True(lowered.Anchor.Z < apron.Anchor.Z, "the apron did not go down.");
            Assert.Equal(apron.Anchor.X, lowered.Anchor.X);
            Assert.Equal(apron.Anchor.Y, lowered.Anchor.Y);
            Assert.DoesNotContain(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(),
                flush => flush.References.Contains(apron.Id) && flush.References.Contains(top.Id));
        });

        // ...then up again with the pointer until the snap catches the top's underside. The drag is
        // stopped short of the release so the live snap can be looked at (§8.3: candidates until
        // the drop).
        Box before = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
        Length rise = underside - (before.Anchor.Z + before.Depth);
        ModelHandle arrow = window.Model.MoveHandle(Axis.Z)!;
        Point under = arrow.At - new Vector(0, perInch * rise.ToInches());

        app.PressAt(InModel(window, arrow.At));
        app.DragTo(InModel(window, arrow.At - new Vector(0, perInch * rise.ToInches() / 2)));
        app.DragTo(InModel(window, under));

        app.Expect("while dragging, the snap has caught the top's underside with the apron's top face", () =>
        {
            SpaceSnapPlan plan = window.Model.ActiveSnap
                ?? throw new InvalidOperationException("No drag is under way.");
            SpaceSnapHit hit = Assert.Single(plan.Hits);
            Assert.Equal(Axis.Z, hit.Axis);
            Assert.Equal(SnapKind.Edge, hit.Kind);
            Assert.Equal(top.Id, hit.Target);
            Assert.Equal(BoxFace.Bottom, hit.TargetFace);
            Assert.Equal(BoxFace.Top, hit.MovingFace);
        });

        app.SaveFrame("3d-apron-snapping");
        app.ReleaseAt(InModel(window, under));

        app.Expect("the drop states the flush, top's bottom face to the apron's top face, and the list says so", () =>
        {
            Flush flush = Assert.Single(
                window.CurrentDesign!.Sketch.RelationshipsInOrder.OfType<Flush>(),
                candidate => candidate.References.Contains(apron.Id) && candidate.References.Contains(top.Id));
            Assert.Equal(new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)), flush.A);
            Assert.Equal(new FeatureRef(apron.Id, BoxFeature.Face(BoxFace.Top)), flush.B);

            Box set = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal(underside, set.Anchor.Z + set.Depth);

            Assert.Contains(
                "Apron, long, south's top face is flush with Top's bottom face.",
                window.RelationshipsOnScreen);
        });

        // Type the apron's depth in the properties panel: a ParamValue on its depth. The top is
        // pinned and the apron is held flush under it, so the apron grows downward (§9.1 case 9).
        app.Click(CentreOf(window, window.OutOfPlaneField));
        app.Chord(Key.A);
        app.Type("5\"");
        app.Click(CentreOf(window, window.ApplyPart));

        app.Expect("the apron is 5\" deep, its top is still under the top, and the top did not move", () =>
        {
            Box grown = window.CurrentDesign!.Sketch.Find<Box>(apron.Id)!;
            Assert.Equal(Length.Inches(5), grown.Depth);
            Assert.Equal(underside, grown.Anchor.Z + grown.Depth);
            Assert.Equal(underside - Length.Inches(5), grown.Anchor.Z);

            Assert.Equal(top, window.CurrentDesign!.Sketch.Find<Box>(top.Id)!);
        });

        app.SaveFrame("3d-apron-deeper");
    });

    internal static Box BoxNamed(MainWindow window, string name) => window.CurrentDesign!.Sketch.Entities.Values
        .OfType<Box>()
        .Single(box => box.Name == name);

    /// <summary>The window coordinate a model point is drawn at on the plan.</summary>
    internal static Point OnPlan(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    /// <summary>The window coordinate a point of the 3D view is at.</summary>
    internal static Point InModel(MainWindow window, Point inView)
    {
        Point origin = window.Model.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(inView.X + origin.X, inView.Y + origin.Y);
    }

    /// <summary>Opens a sample through the Samples menu, with the mouse.</summary>
    internal static void OpenSample(IGuiDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.SamplesMenuItem));

        MenuItem item = window.GetVisualDescendants()
            .OfType<MenuItem>()
            .Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    internal static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        Size size = control.Bounds.Size;
        return topLeft + new Point(size.Width / 2, size.Height / 2);
    }
}
