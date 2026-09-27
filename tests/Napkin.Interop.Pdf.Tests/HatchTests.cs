namespace Napkin.Interop.Pdf.Tests;

/// <summary>The hatch, clipped by hand: 45° lines a step apart, inside a rectangle, around its holes.</summary>
public class HatchTests
{
    static PagePoint P(double x, double y) => new(x, y);

    [Fact]
    public void A_square_is_crossed_by_lines_rising_at_45_degrees_clipped_to_it()
    {
        // A 10 × 10 square at the origin, step 5: lines y = x − c for c = −10, −5, 0, 5 (c = 10 only
        // touches the corner). c = −10 touches the top-left corner only; c = −5 runs (0,5)–(5,10);
        // c = 0 the diagonal; c = 5 (5,0)–(10,5).
        IReadOnlyList<PageSegment> lines = Hatch.Lines(new PageRect(0, 0, 10, 10), [], 5);
        Assert.Equal(
            [new PageSegment(P(0, 5), P(5, 10)), new PageSegment(P(0, 0), P(10, 10)), new PageSegment(P(5, 0), P(10, 5))],
            lines);
    }

    [Fact]
    public void A_hole_is_left_clear()
    {
        // The diagonal of a 10 × 10 square passes through a hole at 4–6 × 4–6: kept 0–4 and 6–10.
        IReadOnlyList<PageSegment> lines = Hatch.Lines(new PageRect(0, 0, 10, 10), [new PageRect(4, 4, 2, 2)], 10);
        Assert.Equal([new PageSegment(P(0, 0), P(4, 4)), new PageSegment(P(6, 6), P(10, 10))], lines);

        // A hole the line misses, one covering its start, and one covering it all.
        Assert.Equal([new PageSegment(P(0, 0), P(10, 10))], Hatch.Lines(new PageRect(0, 0, 10, 10), [new PageRect(8, 0, 2, 2)], 10));
        Assert.Equal([new PageSegment(P(3, 3), P(10, 10))], Hatch.Lines(new PageRect(0, 0, 10, 10), [new PageRect(-1, -1, 4, 4)], 10));
        Assert.Empty(Hatch.Lines(new PageRect(0, 0, 10, 10), [new PageRect(0, 0, 10, 10)], 10));
    }

    [Fact]
    public void An_empty_area_has_no_hatch_and_a_step_must_be_positive()
    {
        Assert.Empty(Hatch.Lines(new PageRect(0, 0, 0, 10), [], 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Hatch.Lines(new PageRect(0, 0, 10, 10), [], 0));
        Assert.Throws<ArgumentNullException>(() => Hatch.Lines(new PageRect(0, 0, 10, 10), null!, 5));
    }
}
