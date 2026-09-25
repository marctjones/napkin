using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>The sheet's arrangement and shared scale (docs/design/standard-views.md §11.2, §11.4).</summary>
public class SheetLayoutTests
{
    static SheetPane Pane(IReadOnlyList<SheetPane> panes, StandardView? view) => Assert.Single(panes, pane => pane.View == view);

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void Top_is_above_Front_and_Right_beside_Front_with_3D_in_the_spare_corner()
    {
        // 608 × 408 with an 8 px gutter: four 300 × 200 panes.
        IReadOnlyList<SheetPane> panes = SheetLayout.Panes(608, 408);

        Assert.Equal<StandardView?>([StandardView.Top, null, StandardView.Front, StandardView.Right], panes.Select(pane => pane.View));
        Assert.Equal(new SheetPane(StandardView.Top, 0, 0, 300, 200), Pane(panes, StandardView.Top));
        Assert.Equal(new SheetPane(null, 308, 0, 300, 200), Pane(panes, null));
        Assert.Equal(new SheetPane(StandardView.Front, 0, 208, 300, 200), Pane(panes, StandardView.Front));
        Assert.Equal(new SheetPane(StandardView.Right, 308, 208, 300, 200), Pane(panes, StandardView.Right));
    }

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void Top_and_Front_share_their_columns_and_Front_and_Right_their_rows_and_nothing_overlaps()
    {
        IReadOnlyList<SheetPane> panes = SheetLayout.Panes(901, 577);
        SheetPane top = Pane(panes, StandardView.Top), front = Pane(panes, StandardView.Front), right = Pane(panes, StandardView.Right);

        Assert.Equal((top.Left, top.Right), (front.Left, front.Right));
        Assert.Equal((front.Top, front.Bottom), (right.Top, right.Bottom));
        Assert.Equal(SheetLayout.Gutter, front.Top - top.Bottom, 9);
        Assert.Equal(SheetLayout.Gutter, right.Left - front.Right, 9);
        Assert.Equal(901, right.Right, 9);
        Assert.Equal(577, right.Bottom, 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void A_sheet_smaller_than_its_gutters_has_empty_panes_rather_than_negative_ones()
    {
        Assert.All(SheetLayout.Panes(4, 4), pane => Assert.Equal((0.0, 0.0), (pane.Width, pane.Height)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetLayout.Panes(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetLayout.Panes(10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetLayout.Panes(10, 10, -1));
    }

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void The_shared_scale_is_the_one_the_tightest_drawing_fits_at()
    {
        // Panes 300 × 200 with a 10 % margin leave 240 × 160. The coffee table's 48 × 24 top in Top
        // fits at 5 px/in; Front, 48 across and 17 up, at 5 too (240/48); Right, 24 across and 17 up,
        // at 160/17 ≈ 9.41. The shared scale is 5.
        IReadOnlyList<SheetPane> panes = SheetLayout.Panes(608, 408);
        double? scale = SheetLayout.SharedScale(
            [
                (Pane(panes, StandardView.Top), 48, 24),
                (Pane(panes, StandardView.Front), 48, 17),
                (Pane(panes, StandardView.Right), 24, 17),
            ],
            0.1);

        Assert.Equal(5, scale!.Value, 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void A_drawing_with_no_extent_one_way_is_fitted_the_other_way_and_nothing_to_fit_has_no_scale()
    {
        SheetPane pane = new(StandardView.Front, 0, 0, 300, 200);

        // A part seen edge-on: 30 across, nothing up — fitted across alone, 240/30 = 8.
        Assert.Equal(8, SheetLayout.SharedScale([(pane, 30, 0)], 0.1)!.Value, 9);
        Assert.Null(SheetLayout.SharedScale([(pane, 0, 0)], 0.1));
        Assert.Null(SheetLayout.SharedScale([], 0.1));
    }
}
