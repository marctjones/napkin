using System.Collections.Immutable;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Every update result has somewhere to appear, and says something a person can act on.
/// </summary>
/// <remarks>
/// This is CVS-008 as a table: four results, four different things on screen, and none of them an
/// enum name or a GUID. The direct updater never returns <see cref="UnderConstrained"/>, so the
/// one test that needs one uses a stand-in updater — which is also a check that the canvas is
/// really written against the interface and not against the one implementation (design
/// &#xA7;4.3).
/// </remarks>
public class EditMessageTests
{
    static readonly LengthFormat AtSixteenths = new FeetInchesFormat(16);

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void Solved_says_what_happened()
    {
        EditMessage message = EditMessages.For(
            new Solved(Sketch.Empty, ChangeSet.Empty),
            "Moved Part 1",
            Sketch.Empty,
            Name,
            AtSixteenths);

        Assert.Equal(EditSeverity.Done, message.Severity);
        Assert.Equal("Moved Part 1.", message.Text);
        Assert.Null(message.OfferToRemove);
    }

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void UnderConstrained_applies_and_is_a_hint_never_an_error()
    {
        EditMessage message = EditMessages.For(
            new UnderConstrained(
                Sketch.Empty,
                ChangeSet.Empty,
                new FreedomReport([EditingBuilder.Id(0)], 2)),
            "Moved Part 1",
            Sketch.Empty,
            Name,
            AtSixteenths);

        Assert.Equal(EditSeverity.Hint, message.Severity);
        Assert.StartsWith("Moved Part 1.", message.Text, StringComparison.Ordinal);
        Assert.Contains("Part 1", message.Text, StringComparison.Ordinal);
        Assert.Contains("still be moved", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void OverConstrained_names_the_relationships_and_offers_to_remove_one()
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 10, 12),
            EditingBuilder.At(10, 0, 10, 12));

        RelationshipId flushId = RelationshipId.New();
        RelationshipId pinId = RelationshipId.New();
        Sketch sketch = design.Sketch
            .WithRelationship(new Flush(
                flushId,
                new BoxEdgeRef(EditingBuilder.Id(0), BoxEdge.East),
                new BoxEdgeRef(EditingBuilder.Id(1), BoxEdge.West)))
            .WithRelationship(new Anchored(pinId, EditingBuilder.Id(1)));

        EditMessage message = EditMessages.For(
            new OverConstrained(new ConflictReport(
                ConflictKind.Contradictory,
                [flushId, pinId],
                [EditingBuilder.Id(0), EditingBuilder.Id(1)],
                [],
                $"The X of {EditingBuilder.Id(0)} is 30\" by one route and 24\" by another; "
                + "these cannot both be true.")),
            "Set Part 1's width to 2'-6\"",
            sketch,
            id => design.LabelFor(id) ?? id.ToString(),
            AtSixteenths);

        Assert.Equal(EditSeverity.Problem, message.Severity);
        Assert.Contains("would not hold", message.Text, StringComparison.Ordinal);

        // The conflict report's own summary, with the parts named rather than identified.
        Assert.Contains("The X of Part 1 is 30\"", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(EditingBuilder.Id(0).ToString(), message.Text, StringComparison.Ordinal);

        // And what cannot all hold, in words.
        Assert.Contains("flush with", message.Text, StringComparison.Ordinal);
        Assert.Contains("pinned", message.Text, StringComparison.Ordinal);

        Assert.Equal([flushId, pinId], message.Highlight);
        Assert.Equal(flushId, message.OfferToRemove);
        Assert.Contains("Remove:", message.OfferText!, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void Rejected_explains_the_reason_in_words_and_never_as_an_enum()
    {
        foreach (RejectionReason reason in Enum.GetValues<RejectionReason>())
        {
            EditMessage message = EditMessages.For(
                new Rejected(reason),
                "Resized Part 1",
                Sketch.Empty,
                Name,
                AtSixteenths);

            Assert.Equal(EditSeverity.Problem, message.Severity);
            Assert.StartsWith("Resized Part 1 did not happen:", message.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(reason.ToString(), message.Text, StringComparison.Ordinal);
            Assert.EndsWith(".", message.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_drag_blocked_by_a_typed_dimension_points_at_the_dimension()
    {
        // §4.1: a drag never silently overrides a number the user typed, and the refusal has to
        // say what to do instead rather than simply refusing.
        string text = EditMessages.Refusal(RejectionReason.DrivenSize);

        Assert.Contains("typed dimension", text, StringComparison.Ordinal);
        Assert.Contains("Edit the dimension instead", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_relationship_kind_this_build_cannot_hold_says_so_plainly()
    {
        string text = EditMessages.Refusal(RejectionReason.UnsupportedRelationship);

        Assert.Contains("cannot hold that kind of relationship yet", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(Flush), "Part 2's left edge is flush with Part 1's right edge.")]
    [InlineData(typeof(Coincident), "Part 2's bottom-left corner is at Part 1's bottom-right corner.")]
    [InlineData(typeof(Anchored), "Part 1 is pinned where it is.")]
    [InlineData(typeof(ParamValue), "Part 1's width is 2'-0\".")]
    [InlineData(typeof(EqualParam), "Part 2's width is the same as Part 1's width.")]
    public void A_relationship_reads_as_a_sentence(Type kind, string expected)
    {
        Design design = EditingBuilder.Design(
            EditingBuilder.At(0, 0, 24, 12),
            EditingBuilder.At(24, 0, 24, 12));

        EntityId one = EditingBuilder.Id(0);
        EntityId two = EditingBuilder.Id(1);
        RelationshipId id = RelationshipId.New();

        Relationship relationship = kind switch
        {
            _ when kind == typeof(Flush) => new Flush(id, new BoxEdgeRef(one, BoxEdge.East), new BoxEdgeRef(two, BoxEdge.West)),
            _ when kind == typeof(Coincident) => new Coincident(id, new CornerRef(one, BoxCorner.SouthEast), new CornerRef(two, BoxCorner.SouthWest)),
            _ when kind == typeof(Anchored) => new Anchored(id, one),
            _ when kind == typeof(ParamValue) => new ParamValue(id, new BoxWidthRef(one), Length.Feet(2)),
            _ => new EqualParam(id, new BoxWidthRef(one), new BoxWidthRef(two)),
        };

        Assert.Equal(
            expected,
            RelationshipText.Describe(design.Sketch, relationship, id => design.LabelFor(id)!, AtSixteenths));
    }

    [Fact]
    public void A_kind_reserved_for_the_solver_is_named_rather_than_printed()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        Napkin.Core.Geometry.Parallel reserved = new(
            RelationshipId.New(),
            new BoxEdgeRef(EditingBuilder.Id(0), BoxEdge.South),
            new BoxEdgeRef(EditingBuilder.Id(0), BoxEdge.North));

        string text = RelationshipText.Describe(design.Sketch, reserved, Name, AtSixteenths);

        Assert.Contains("two edges running the same way", text, StringComparison.Ordinal);
        Assert.Contains("cannot hold", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_id_a_conflict_report_names_is_replaced_by_the_name_on_screen()
    {
        EntityId id = EditingBuilder.Id(0);
        string summary = $"The width of {id} is 30\" and {id} is 24\" wide.";

        Assert.Equal(
            "The width of Shelf is 30\" and Shelf is 24\" wide.",
            EditMessages.Rename(summary, [id], _ => "Shelf"));
    }

    static string Name(EntityId id) => "Part 1";
}
