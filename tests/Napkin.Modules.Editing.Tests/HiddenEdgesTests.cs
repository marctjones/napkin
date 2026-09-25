using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Hidden edges (docs/design/standard-views.md §2.3) on faces drawn by hand on the view plane: which
/// piece of which edge is seen, which is behind a nearer face, and which vanishes under a solid edge.
/// Every expected length is worked out on paper from the squares below.
/// </summary>
public class HiddenEdgesTests
{
    /// <summary>An axis-aligned rectangle at one nearness, every edge real.</summary>
    static FlatFace Rect(double u0, double v0, double u1, double v1, double nearness) => new(
        [new(u0, v0, nearness), new(u1, v0, nearness), new(u1, v1, nearness), new(u0, v1, nearness)],
        [true, true, true, true]);

    static double Total(IEnumerable<FlatSegment> segments, int face) =>
        segments.Where(segment => segment.Face == face).Sum(segment => segment.Length);

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void Two_flush_squares_side_by_side_hide_nothing_of_each_other()
    {
        EdgeSplit split = HiddenEdges.Split([Rect(0, 0, 1, 1, 0), Rect(1, 0, 2, 1, 0)]);
        Assert.Empty(split.Hidden);
        Assert.Equal(4, Total(split.Visible, 0), 9);
        Assert.Equal(4, Total(split.Visible, 1), 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void Equal_depth_never_hides_and_a_hair_nearer_is_still_equal()
    {
        // The small square lies inside the big one, nearer by half the tolerance: still flush.
        EdgeSplit flush = HiddenEdges.Split([Rect(1, 1, 2, 2, 0), Rect(0, 0, 3, 3, HiddenEdges.DepthTolerance / 2)]);
        Assert.Empty(flush.Hidden);
        Assert.Equal(4, Total(flush.Visible, 0), 9);

        // Twice the tolerance nearer, the big square covers the small one's whole outline.
        EdgeSplit behind = HiddenEdges.Split([Rect(1, 1, 2, 2, 0), Rect(0, 0, 3, 3, HiddenEdges.DepthTolerance * 2)]);
        Assert.Equal(4, Total(behind.Hidden, 0), 9);
        Assert.Equal(0, Total(behind.Visible, 0), 9);
        Assert.Equal(12, Total(behind.Visible, 1), 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_nearer_square_over_a_corner_hides_the_quarter_it_covers()
    {
        // Far 0–2 square, near 1–3 square: the far square's top edge is hidden from u 1 to 2, its right
        // edge from v 1 to 2; the rest of its outline — 6 of 8 — is seen. The near square is all seen.
        EdgeSplit split = HiddenEdges.Split([Rect(0, 0, 2, 2, 0), Rect(1, 1, 3, 3, 1)]);
        Assert.Equal(2, Total(split.Hidden, 0), 9);
        Assert.Equal(6, Total(split.Visible, 0), 9);
        Assert.Equal(8, Total(split.Visible, 1), 9);
        Assert.All(split.Hidden, piece => Assert.True(piece.From.U >= 1 - 1e-9 && piece.From.V >= 1 - 1e-9));
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_notch_in_a_nearer_outline_lets_the_edge_behind_show_through()
    {
        // A U-shaped near face: 0–3 wide, 0–2 tall, with a notch u 1–2 down to v 1. A far line across
        // at v 1.5 is hidden, seen in the notch, hidden again: 1 + 1 hidden, 1 seen.
        FlatFace u = new(
            [new(0, 0, 1), new(3, 0, 1), new(3, 2, 1), new(2, 2, 1), new(2, 1, 1), new(1, 1, 1), new(1, 2, 1), new(0, 2, 1)],
            [true, true, true, true, true, true, true, true]);
        FlatFace line = new([new(0.5, 1.5, 0), new(2.5, 1.5, 0), new(2.5, 1.6, 0)], [true, false, false]);
        EdgeSplit split = HiddenEdges.Split([line, u]);

        Assert.Equal(1, Total(split.Hidden, 0), 9);
        Assert.Equal(1, Total(split.Visible, 0), 9);
        FlatSegment seen = Assert.Single(split.Visible, piece => piece.Face == 0);
        Assert.Equal((1.0, 2.0), (seen.From.U, seen.To.U));
        Assert.Equal(2, split.Hidden.Count(piece => piece.Face == 0));
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_part_exactly_behind_another_shows_no_dash_under_the_solid_outline()
    {
        // The coffee table's far leg: the same outline, farther away. Every one of its edges lies along a
        // seen edge of the near leg, so nothing is dashed.
        EdgeSplit split = HiddenEdges.Split([Rect(0, 0, 2.5, 16.25, 0), Rect(0, 0, 2.5, 16.25, 21)]);
        Assert.Empty(split.Hidden);
        Assert.Equal(0, Total(split.Visible, 0), 9);
        Assert.Equal(2 * (2.5 + 16.25), Total(split.Visible, 1), 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_dash_is_cut_back_only_where_a_solid_edge_lies_along_it()
    {
        // Far square 0–4 behind two near strips, u 1–3 and u 3–5, both taller than it. Its bottom and
        // top edges are hidden from u 1 to 4 (3 each) and its right edge (u 4) all the way (4); no near
        // edge lies along any of them, so all 10 are dashed.
        EdgeSplit split = HiddenEdges.Split([Rect(0, 0, 4, 4, 0), Rect(1, -1, 3, 5, 1), Rect(3, -1, 5, 5, 1)]);
        Assert.Equal(3 + 4 + 3, Total(split.Hidden, 0), 9);

        // Bring the first strip's top down to v 4: now the far top edge's hidden u 1–3 lies under the
        // strip's own solid top edge, so it is not dashed; only the bottom edge's u 1–3 is (2).
        EdgeSplit cut = HiddenEdges.Split([Rect(0, 0, 4, 4, 0), Rect(1, -1, 3, 4, 1)]);
        FlatSegment[] top = [.. cut.Hidden.Where(piece => piece.Face == 0 && Math.Abs(piece.From.V - 4) < 1e-9 && Math.Abs(piece.To.V - 4) < 1e-9)];
        Assert.Empty(top);
        Assert.Equal(2, Total(cut.Hidden, 0), 9);
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_ruling_and_a_zero_length_edge_get_no_line()
    {
        FlatFace face = new([new(0, 0, 0), new(1, 0, 0), new(1, 0, 0), new(1, 1, 0)], [false, true, true, true]);
        EdgeSplit split = HiddenEdges.Split([face]);
        Assert.Empty(split.Hidden);
        Assert.Equal(1 + Math.Sqrt(2), Total(split.Visible, 0), 9);
        Assert.DoesNotContain(split.Visible, piece => piece.Edge == 0 || piece.Edge == 1);
    }

    [Fact]
    [Trait("Feature", "VIEW-007")]
    public void A_tilted_face_hides_only_what_it_is_nearer_than_everywhere()
    {
        // A near face sloping from nearness 1 to 3 over a far square at 0 hides it; the same face
        // sloping from −1 to 3 is not nearer everywhere and hides nothing.
        FlatFace far = Rect(1, 1, 2, 2, 0);
        FlatFace sloping = new([new(0, 0, 1), new(3, 0, 3), new(3, 3, 3), new(0, 3, 1)], [true, true, true, true]);
        FlatFace crossing = sloping with { Corners = [new(0, 0, -1), new(3, 0, 3), new(3, 3, 3), new(0, 3, -1)] };
        Assert.Equal(4, Total(HiddenEdges.Split([far, sloping]).Hidden, 0), 9);
        Assert.Empty(HiddenEdges.Split([far, crossing]).Hidden);
    }
}
