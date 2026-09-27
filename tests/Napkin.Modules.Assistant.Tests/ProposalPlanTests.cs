using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>
/// The proposal sheet's acceptance (docs/design/llm-assistant.md &#xA7;4.3): the ticked lines in one
/// gesture and one undo step, a refusal in the updater's words while the rest land, and the stale rule.
/// </summary>
public class ProposalPlanTests
{
    /// <summary>The direct updater, except that it refuses to add a box with this name — the direct one never refuses a good plank.</summary>
    sealed class RefusingUpdater(string refused) : IGeometryUpdater
    {
        public ImmutableHashSet<Type> SupportedRelationships => DirectUpdater.Instance.SupportedRelationships;

        public UpdateResult Apply(Sketch sketch, Request request)
            => request is AddEntity { Entity: Box { Name: var name } } && name == refused
                ? new Rejected(RejectionReason.DuplicateEntity)
                : DirectUpdater.Instance.Apply(sketch, request);
    }

    static (DesignEditor Editor, ProposalPlan Plan) QuickBench(IGeometryUpdater? updater = null)
    {
        DesignEditor editor = updater is null ? new() : new(updater);
        ProposalPlan plan = SketchProposal.Parse(SketchProposalTests.QuickBench)!
            .Plan(editor.Sketch, editor.LayerForNewParts(), () => throw new InvalidOperationException("Every part is named."));
        return (editor, plan);
    }

    static string[] Names(DesignEditor editor)
        => [.. editor.Sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id).Select(box => box.Name)];

    [Trait("Feature", "AST-004")]
    [Fact]
    public void Only_the_ticked_lines_land_and_they_land_as_one_undo_step()
    {
        (DesignEditor editor, ProposalPlan plan) = QuickBench();

        // Leg 2 unticked: GUI-AST-04's step, in the module.
        ProposalOutcome outcome = plan.Accept(editor, [plan.Lines[0], plan.Lines[1], plan.Lines[3]], SketchProposal.MessageLine);

        Assert.Equal(3, outcome.Landed);
        Assert.Equal(["Top", "Leg 1", "Stretcher"], Names(editor));
        Assert.Equal(1, editor.History.UndoCount);
        Assert.Equal("Assistant sketch: drew 3 rough parts.", editor.LastMessage!.Text);

        Assert.True(editor.Undo());
        Assert.Empty(editor.Sketch.Entities.Values.OfType<Box>());
        Assert.Equal("Undone: Assistant sketch.", editor.LastMessage!.Text);
        Assert.True(editor.Redo());
        Assert.Equal(["Top", "Leg 1", "Stretcher"], Names(editor));
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_plan_made_against_a_design_that_has_since_changed_is_refused_whole()
    {
        (DesignEditor editor, ProposalPlan plan) = QuickBench();
        Assert.True(plan.IsFor(editor.Sketch));

        // The person drew while the assistant was thinking.
        editor.Apply(new AddEntity(Box.AsDrawn(EntityId.New(), editor.LayerForNewParts(), Point2.Inches(100, 100), Length.Inches(10), Length.Inches(10), Box.DefaultDepth, Angle.Zero)), "Drew a part");
        Sketch drawn = editor.Sketch;
        Assert.False(plan.IsFor(drawn));

        ProposalOutcome outcome = plan.Accept(editor, plan.Lines, SketchProposal.MessageLine);

        Assert.True(outcome.Stale);
        Assert.Equal(0, outcome.Landed);
        Assert.Empty(outcome.Rejections);
        Assert.Same(drawn, editor.Sketch);
        Assert.Equal(1, editor.History.UndoCount);
        Assert.Equal("The design changed while the assistant was thinking — ask again.", editor.LastMessage!.Text);
        Assert.Equal(ProposalPlan.StaleText, editor.LastMessage.Text);
        Assert.Equal(EditSeverity.Problem, editor.LastMessage.Severity);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_rejection_is_reported_in_the_updaters_words_and_the_rest_still_land()
    {
        (DesignEditor editor, ProposalPlan plan) = QuickBench(new RefusingUpdater("Leg 2"));
        Sketch before = editor.Sketch;

        ProposalOutcome outcome = plan.Accept(editor, plan.Lines, SketchProposal.MessageLine);

        Assert.Equal(3, outcome.Landed);
        string rejection = Assert.Single(outcome.Rejections);
        Assert.Equal(EditMessages.For(new Rejected(RejectionReason.DuplicateEntity), SketchProposal.What, before, editor.NameOf, editor.LabelFormat).Text, rejection);
        Assert.Equal(["Top", "Leg 1", "Stretcher"], Names(editor));
        Assert.Equal(1, editor.History.UndoCount);
        Assert.Equal("Assistant sketch: drew 3 rough parts.", editor.LastMessage!.Text);
        Assert.Equal(EditSeverity.Problem, editor.LastMessage.Severity);
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void A_refused_line_and_a_line_from_another_sheet_are_never_applied()
    {
        DesignEditor editor = new();
        ProposalPlan plan = SketchProposal.Parse(SketchProposalTests.QuickBench.Replace("\"x\": \"0\"", "\"x\": \"2000\"", StringComparison.Ordinal))!
            .Plan(editor.Sketch, editor.LayerForNewParts(), () => "unused");
        Assert.Equal("Top: refused, an anchor at (2000, 16), beyond ±1000", plan.Lines[0].Sentence);
        ProposalLine foreign = new("Shelf", [new AddEntity(Box.AsDrawn(EntityId.New(), editor.LayerForNewParts(), Point2.Inches(0, 0), Length.Inches(4), Length.Inches(4), Box.DefaultDepth, Angle.Zero) with { Name = "Shelf" })]);

        ProposalOutcome outcome = plan.Accept(editor, [plan.Lines[0], foreign, plan.Lines[1]], SketchProposal.MessageLine);

        Assert.Equal(1, outcome.Landed);
        Assert.Equal(["Leg 1"], Names(editor));
    }

    [Trait("Feature", "AST-004")]
    [Fact]
    public void Nothing_ticked_draws_nothing_and_leaves_no_undo_step()
    {
        (DesignEditor editor, ProposalPlan plan) = QuickBench();

        ProposalOutcome outcome = plan.Accept(editor, [], SketchProposal.MessageLine);

        Assert.Equal(0, outcome.Landed);
        Assert.Equal(0, editor.History.UndoCount);
        Assert.Equal("Assistant sketch: drew nothing.", editor.LastMessage!.Text);
    }

    [Fact]
    public void A_line_of_several_requests_goes_to_the_updater_as_one_batch()
    {
        // Edit in words (slice F) puts a size and the clearing of the rough mark on one line, as typing does.
        DesignEditor editor = new();
        EntityId id = EntityId.New();
        Box plank = Box.AsDrawn(id, editor.LayerForNewParts(), Point2.Inches(0, 0), Length.Inches(4), Length.Inches(16), Box.DefaultDepth, Angle.Zero) with
        {
            Name = "Leg",
            Part = RoughEntry.Plank(Length.Inches(4), Length.Inches(16)),
        };
        ProposalLine line = new("Leg", [new AddEntity(plank), new SetPart(id, plank.Part! with { Rough = false })]);
        ProposalPlan plan = new("Assistant edit", editor.Sketch, [line]);

        ProposalOutcome outcome = plan.Accept(editor, [line], n => $"Made {n} edit.");

        Assert.Equal(1, outcome.Landed);
        Assert.False(editor.Sketch.Find<Box>(id)!.Part!.Rough);
        Assert.Equal(1, editor.History.UndoCount);
        Assert.Equal("Assistant edit", editor.History.UndoWhat);
        Assert.Equal("Made 1 edit.", editor.LastMessage!.Text);
    }

    [Fact]
    public void A_refusal_line_has_no_request_and_says_so()
    {
        ProposalLine refusal = ProposalLine.Refusal("Leg 1: refused, a size of 0");

        Assert.True(refusal.Refused);
        Assert.Empty(refusal.Requests);
        Assert.Equal("Leg 1: refused, a size of 0", refusal.Sentence);
    }

    [Fact]
    public void Nulls_are_refused_at_the_door()
    {
        (DesignEditor editor, ProposalPlan plan) = QuickBench();
        Assert.Throws<ArgumentNullException>(() => plan.Accept(null!, [], SketchProposal.MessageLine));
        Assert.Throws<ArgumentNullException>(() => plan.Accept(editor, null!, SketchProposal.MessageLine));
        Assert.Throws<ArgumentNullException>(() => plan.Accept(editor, [], null!));
    }
}
