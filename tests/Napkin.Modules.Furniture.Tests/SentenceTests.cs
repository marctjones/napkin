using Napkin.Core.Geometry;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The fixed sentences the app shows, pinned here word for word (#179), so the GUI workflows can
/// assert that the app shows them by calling the same formatter instead of copying the words.
/// </summary>
public sealed class SentenceTests
{
    [Fact]
    public void A_joint_refused_or_apart_says_why_in_words()
    {
        Assert.Equal("A rabbet needs a depth greater than zero, like 1/4\".", JointTooltip.DepthRefusal(JointType.Rabbet));
        Assert.Equal("A groove needs a depth greater than zero, like 1/4\".", JointTooltip.DepthRefusal(JointType.Groove));
        Assert.Equal("Part 1 and Part 3 don't touch.", JointTooltip.DontTouch("Part 1", "Part 3"));
        Assert.Equal(" (parts no longer touch)", JointTooltip.PartsNoLongerTouch);
    }

    [Fact]
    public void The_count_box_suggests_the_recipe_and_refuses_a_count_below_one()
    {
        Assert.Equal("recipe: 3", Recipes.Placeholder(3));
        Assert.Equal("A count is a whole number of at least 1, or blank for the recipe's.", Recipes.CountRefusal);
    }

    [Fact]
    public void A_pack_size_that_is_not_a_whole_number_is_refused_with_what_was_typed()
    {
        Assert.Equal("A pack size is a whole number of at least 1, or blank; \"many\" is not.", SuppliesList.PackSizeRefusal("many"));
    }

    [Theory]
    [InlineData(0, 0, "nothing to cut")]
    [InlineData(1, 1, "1 row, 1 piece to cut")]
    [InlineData(4, 9, "4 rows, 9 pieces to cut")]
    public void The_headline_counts_rows_and_pieces_in_the_singular_and_plural(int rows, int pieces, string headline)
    {
        Assert.Equal(headline, CutList.Headline(rows, pieces));
    }
}
