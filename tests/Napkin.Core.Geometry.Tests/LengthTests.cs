namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The golden cases for <see cref="Length"/> from docs/design/geometry-model.md &#xA7;7.1.
/// </summary>
public class LengthTests
{
    [Theory]
    // The table from issue #4 that ruled out integer millimetres (design §1.1).
    [InlineData(0, 1, 16, 64)]              // 1/16"
    [InlineData(3, 1, 2, 3584)]             // 3 1/2" — a 2x4's width
    [InlineData(0, 23, 32, 736)]            // 23/32" — 3/4" nominal plywood
    [InlineData(0, 15, 32, 480)]            // 15/32" — 1/2" nominal plywood
    [InlineData(1, 0, 1, 1024)]             // 5/4 decking is 1" actual
    [InlineData(1, 1, 2, 1536)]             // a 2x4's thickness
    [InlineData(16, 0, 1, 16384)]           // 16" on centre
    [InlineData(24, 0, 1, 24576)]           // 24" on centre
    public void ExactInchValuesRoundTripThroughTheGrid(long whole, long numerator, long denominator, long expectedUnits)
    {
        Length value = Length.Inches(whole, numerator, denominator);

        Assert.Equal(expectedUnits, value.Units);
        Assert.Equal(value, Length.Inches(whole, numerator, denominator));
        Assert.Equal(whole + (double)numerator / denominator, value.ToInches(), 12);
    }

    [Fact]
    public void SixFeetIsExactAndComparesAtTheHeaderSpanThreshold()
    {
        Length sixFeet = Length.FeetInches(6, 0);

        // The row integer millimetres broke: 6'-0" becomes 1829 mm = 72.008".
        Assert.Equal(73728, sixFeet.Units);
        Assert.Equal(72.0, sixFeet.ToInches());

        // "<= 6 ft" in a header table must be true for a part that is exactly 6 ft.
        Assert.True(sixFeet <= Length.FeetInches(6, 0));
        Assert.True(sixFeet >= Length.FeetInches(6, 0));
        Assert.False(sixFeet < Length.FeetInches(6, 0));
        Assert.Equal(sixFeet, Length.Feet(6));
        Assert.Equal(0, sixFeet.CompareTo(Length.Inches(72)));
    }

    [Fact]
    public void FeetInchesSumsItsComponents()
    {
        Assert.Equal(Length.Inches(75, 5, 16), Length.FeetInches(6, 3, 5, 16));
        Assert.Equal(-Length.Inches(75, 5, 16), Length.FeetInches(-6, -3, -5, 16));
    }

    [Fact]
    public void InchesRefusesAFractionThatIsNotOnTheGrid()
    {
        // A third of an inch is the honest limitation of any fixed grid (design §1.1). The exact
        // constructor never rounds silently; FromInches and the parser are where rounding enters.
        Assert.Throws<ArgumentException>(() => Length.Inches(0, 1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.Inches(0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.Inches(0, 1, -2));
    }

    [Fact]
    public void AdditionAndSubtractionAreExact()
    {
        Length a = Length.Inches(3, 1, 2);
        Length b = Length.Inches(0, 23, 32);

        Assert.Equal(Length.Inches(4, 7, 32), a + b);
        Assert.Equal(a, a + b - b);
        Assert.Equal(Length.Inches(-3, -1, 2), -a);
        Assert.Equal(Length.Inches(7), a * 2);
        Assert.Equal(Length.Inches(7), 2 * a);
        Assert.Equal(a, Length.Abs(-a));
        Assert.Equal(a, Length.Max(a, b));
        Assert.Equal(b, Length.Min(a, b));
    }

    [Fact]
    public void DivideRoundsAndTryDivideExactRefuses()
    {
        Length oneInch = Length.Inches(1);

        // Three equal shelves in an inch: 1024/3 = 341.33 units.
        Assert.Equal(341, oneInch.Divide(3, Rounding.HalfToEven).Units);
        Assert.False(oneInch.TryDivideExact(3, out Length notExact));
        Assert.Equal(Length.Zero, notExact);

        Assert.True(oneInch.TryDivideExact(4, out Length quarter));
        Assert.Equal(Length.Inches(0, 1, 4), quarter);

        Assert.False(oneInch.TryDivideExact(0, out _));
        Assert.Throws<DivideByZeroException>(() => oneInch.Divide(0, Rounding.HalfToEven));
    }

    [Theory]
    // value, divisor, HalfToEven result, HalfAwayFromZero result. Both tie directions, both signs.
    [InlineData(3, 2, 2, 2)]        // 1.5 -> 2 either way (2 is even)
    [InlineData(5, 2, 2, 3)]        // 2.5 -> 2 (even) or 3 (away)
    [InlineData(-3, 2, -2, -2)]     // -1.5 -> -2 either way
    [InlineData(-5, 2, -2, -3)]     // -2.5 -> -2 (even) or -3 (away)
    [InlineData(7, 2, 4, 4)]        // 3.5 -> 4 either way
    [InlineData(5, -2, -2, -3)]     // a negative divisor behaves the same
    [InlineData(7, 4, 2, 2)]        // 1.75 -> 2, not a tie
    [InlineData(1, 4, 0, 0)]        // 0.25 -> 0, not a tie
    public void DivideUsesTheRoundingModeItIsGiven(long units, long divisor, long toEven, long awayFromZero)
    {
        Assert.Equal(toEven, new Length(units).Divide(divisor, Rounding.HalfToEven).Units);
        Assert.Equal(awayFromZero, new Length(units).Divide(divisor, Rounding.HalfAwayFromZero).Units);
    }

    [Fact]
    public void ScaleAppliesARatio()
    {
        Length ten = Length.Inches(10);

        Assert.Equal(Length.Inches(15), ten.Scale(3, 2, Rounding.HalfToEven));
        // 10" / 3 = 3.3333" = 3413.33 units.
        Assert.Equal(3413, ten.Scale(1, 3, Rounding.HalfToEven).Units);
        Assert.Throws<DivideByZeroException>(() => ten.Scale(1, 0, Rounding.HalfToEven));
    }

    [Fact]
    public void FromInchesIsTheOnlyEntryFromDouble()
    {
        Assert.Equal(Length.Inches(3, 1, 2), Length.FromInches(3.5, Rounding.HalfToEven));
        Assert.Equal(Length.Inches(0, 3, 4), Length.FromInches(0.75, Rounding.HalfAwayFromZero));

        // 3.505" is 3589.12 units: not on the grid, so it lands on the nearest unit.
        Assert.Equal(3589, Length.FromInches(3.505, Rounding.HalfToEven).Units);

        Assert.Throws<OverflowException>(() => Length.FromInches(double.NaN, Rounding.HalfToEven));
        Assert.Throws<OverflowException>(() => Length.FromInches(double.PositiveInfinity, Rounding.HalfToEven));
    }

    [Fact]
    public void EveryStoredValueConvertsToDoubleExactly()
    {
        // What makes the fixed-point/double boundary lossless on the way in (design §1.1).
        foreach (long units in new long[] { 0, 1, 64, 736, 3584, 73728, -73728, 1L << 40 })
        {
            Length value = new(units);
            Assert.Equal(value, Length.FromInches(value.ToInches(), Rounding.HalfToEven));
        }
    }

    [Fact]
    public void RatioOfTwoLengthsIsForDisplayOnly()
    {
        Assert.Equal(2.0, Length.Inches(7) / Length.Inches(3, 1, 2));
    }

    [Fact]
    public void CheckedOverflowThrows()
    {
        Length max = new(long.MaxValue);

        Assert.Throws<OverflowException>(() => max + new Length(1));
        Assert.Throws<OverflowException>(() => max * 2);
        Assert.Throws<OverflowException>(() => -new Length(long.MinValue));
        Assert.Throws<OverflowException>(() => Length.Abs(new Length(long.MinValue)));
        Assert.Throws<OverflowException>(() => Length.Feet(long.MaxValue));
    }
}
