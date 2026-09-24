namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// <see cref="Area.Volume"/>: the three-factor product a board-foot takeoff needs, exact in
/// <see cref="Int128"/> (geometry model §1.3).
/// </summary>
public class AreaVolumeTests
{
    [Fact]
    public void A_nominal_2x4_foot_is_the_exact_cubic_units_of_2_by_4_by_12_inches()
    {
        // 2" x 4" x 12" = 96 in³, and one in³ is 1024³ cubic units.
        Int128 volume = Area.Volume(new Length(2 * 1024), new Length(4 * 1024), new Length(12 * 1024));

        Assert.Equal((Int128)96 * 1024 * 1024 * 1024, volume);
    }

    [Fact]
    public void Three_lengths_too_large_for_Int128_throw_rather_than_wrap()
    {
        Length huge = new(long.MaxValue);

        Assert.Throws<OverflowException>(() => Area.Volume(huge, huge, huge));
    }
}
