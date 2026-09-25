using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// What the properties panel and the shape workshop read out of their fields, without a window:
/// the stock line under a stock name, and the hardware list a joint carries.
/// </summary>
public class PanelEntryTests
{
    static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_stock_typed_says_the_cut_list_uses_the_typed_size(string? typed) =>
        Assert.Equal("No stock chosen. The cut list shows the size you typed.", StockAssignment.Readout(typed, Library));

    [Fact]
    public void A_stock_the_library_carries_reads_as_its_hover_text()
    {
        StockItem item = Library.Items.First();

        Assert.Equal(item.HoverText, StockAssignment.Readout($"  {item.Name} ", Library));
    }

    [Fact]
    public void A_stock_the_library_does_not_carry_is_said_not_guessed() =>
        Assert.Equal(
            "\"2x5 unobtainium\" is not in this build's materials library. The cut list will say so rather than guess.",
            StockAssignment.Readout(" 2x5 unobtainium", Library));

    [Fact]
    public void Hardware_lines_read_as_name_and_count_with_x_or_times_and_blank_lines_skipped()
    {
        Assert.True(HardwareEntry.TryRead("pocket screw x 4\n\n dowel × 2 \nbolt\nwasher x3\r\n", out ImmutableList<HardwareItem> items, out string problem));

        Assert.Equal(string.Empty, problem);
        Assert.Equal(
            [new HardwareItem("pocket screw", 4), new HardwareItem("dowel", 2), new HardwareItem("bolt", 1), new HardwareItem("washer", 3)],
            items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n  \n")]
    public void No_hardware_is_an_empty_list(string? typed)
    {
        Assert.True(HardwareEntry.TryRead(typed, out ImmutableList<HardwareItem> items, out string problem));
        Assert.Empty(items);
        Assert.Equal(string.Empty, problem);
    }

    [Theory]
    [InlineData("hinge x 0", "hinge")]
    [InlineData("screw x 99999999999", "screw")]
    public void A_count_below_one_or_too_big_to_read_is_refused_by_name(string typed, string name)
    {
        Assert.False(HardwareEntry.TryRead("bolt\n" + typed, out ImmutableList<HardwareItem> items, out string problem));

        Assert.Empty(items);
        Assert.Equal($"Hardware \"{name}\" needs a count of at least 1.", problem);
    }

    [Fact]
    public void A_name_that_merely_ends_in_x_is_one_of_it() =>
        Assert.True(HardwareEntry.TryRead("box", out ImmutableList<HardwareItem> items, out _) && items.Single() == new HardwareItem("box", 1));
}
