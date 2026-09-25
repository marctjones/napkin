using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// What typing into a dimension does: which request it becomes, and what it says when it cannot
/// become one.
/// </summary>
/// <remarks>
/// The reading itself belongs to <c>Core.Geometry</c>'s <see cref="Length.TryParse"/> and is
/// tested there. What is held down here is the canvas's half: one owner per number (design
/// &#xA7;3.3), so the same keystroke is <see cref="AddRelationship"/> on a size nothing drives and
/// <see cref="SetParameter"/> on one something does; and an explanation, when the text is not a
/// length, that says what could not be read and what would work (GUI-DRAW-03).
/// </remarks>
public class DimensionEntryTests
{
    [Theory]
    [InlineData("3' 4 1/2\"", 40.5)]
    [InlineData("2'-6 1/2\"", 30.5)]
    [InlineData("40 3/4", 40.75)]
    [InlineData(".75", 0.75)]
    [InlineData("6ft", 72)]
    [InlineData("3/4", 0.75)]
    public void Feet_inch_and_fraction_text_is_read_as_the_length_it_says(string text, double inches)
    {
        ReadableLength readable = Assert.IsType<ReadableLength>(DimensionEntry.Interpret(text));

        Assert.Equal((long)Math.Round(inches * Length.UnitsPerInch), readable.Value.Units);
        Assert.False(readable.WasRounded);
    }

    [Theory]
    [InlineData("250mm")]
    [InlineData("1.5cm")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("5.")]
    [InlineData("wide")]
    [InlineData("6 3")]
    [InlineData("-4\"")]
    [InlineData("0")]
    public void Text_that_is_not_a_length_is_explained_rather_than_refused(string text)
    {
        UnreadableText unreadable = Assert.IsType<UnreadableText>(DimensionEntry.Interpret(text));

        // An explanation that does not show what works is only an insult.
        Assert.Contains(DimensionEntry.Examples, unreadable.Message, StringComparison.Ordinal);
        Assert.EndsWith(".", unreadable.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void The_explanation_quotes_what_could_not_be_read()
    {
        UnreadableText unreadable = Assert.IsType<UnreadableText>(DimensionEntry.Interpret("250mm"));

        Assert.Contains("250mm", unreadable.Message, StringComparison.Ordinal);
        Assert.Contains("could not read", unreadable.Message, StringComparison.OrdinalIgnoreCase);

        // And says the one thing a person typing millimetres actually needs to hear.
        Assert.Contains("feet and inches", unreadable.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_length_that_does_not_land_on_the_grid_is_read_and_reported_as_rounded()
    {
        ReadableLength readable = Assert.IsType<ReadableLength>(DimensionEntry.Interpret("1/3"));

        Assert.True(readable.WasRounded);
        Assert.True(readable.Value > Length.Zero);
    }

    [Fact]
    [Trait("Feature", "CVS-005")]
    public void Typing_a_size_nothing_drives_states_it_as_a_new_relationship()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        BoxWidthRef width = new(EditingBuilder.Id(0));

        Request request = DimensionEntry.RequestFor(design.Sketch, width, Length.Inches(30));

        AddRelationship add = Assert.IsType<AddRelationship>(request);
        ParamValue stated = Assert.IsType<ParamValue>(add.Relationship);
        Assert.Equal(width, stated.Param);
        Assert.Equal(Length.Inches(30).Units, stated.Value.Units);
    }

    [Fact]
    [Trait("Feature", "CVS-005")]
    public void Typing_a_size_something_already_drives_sets_that_relationships_number()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        BoxWidthRef width = new(EditingBuilder.Id(0));
        RelationshipId driving = RelationshipId.New();
        Sketch sketch = design.Sketch.WithRelationship(new ParamValue(driving, width, Length.Inches(24)));

        Request request = DimensionEntry.RequestFor(sketch, width, Length.Inches(30));

        SetParameter set = Assert.IsType<SetParameter>(request);
        Assert.Equal(driving, set.Driving);
        Assert.Equal(Length.Inches(30).Units, set.Value.Units);
        Assert.Equal(driving, DimensionEntry.DrivingRelationship(sketch, width));
    }

    [Fact]
    public void A_ParamValue_on_another_size_is_not_this_size_s_owner()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        EntityId box = EditingBuilder.Id(0);
        Sketch sketch = design.Sketch.WithRelationship(
            new ParamValue(RelationshipId.New(), new BoxHeightRef(box), Length.Inches(12)));

        Assert.Null(DimensionEntry.DrivingRelationship(sketch, new BoxWidthRef(box)));
        Assert.IsType<AddRelationship>(
            DimensionEntry.RequestFor(sketch, new BoxWidthRef(box), Length.Inches(30)));
    }
}
