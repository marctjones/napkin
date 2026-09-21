namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Parsing and formatting goldens from docs/design/geometry-model.md &#xA7;7.1, and the round-trip
/// half of property P8 (&#xA7;7.2).
/// </summary>
public class LengthTextTests
{
    [Theory]
    // Every syntax listed in design §1.5.
    [InlineData("6'-3 5/16\"", 77120)]
    [InlineData("6' 3-5/16\"", 77120)]
    [InlineData("6ft 3in", 76800)]
    [InlineData("75 5/16", 77120)]
    [InlineData("75.3125", 77120)]
    [InlineData("3/4", 768)]
    [InlineData("-1/2", -512)]
    [InlineData("0", 0)]
    [InlineData("12\"", 12288)]
    [InlineData("1'-0\"", 12288)]
    [InlineData("6'", 73728)]
    [InlineData("-6'-3\"", -76800)]
    [InlineData("3 1/2 inches", 3584)]
    [InlineData("23/32 inch", 736)]
    [InlineData("  6'-3\"  ", 76800)]
    // The typographic marks a user may paste.
    [Trait("Feature", "GEO-004")]
    [InlineData("6′-3″", 76800)]
    [InlineData("6'–3\"", 76800)]
    public void ParsesEverySyntaxTheDesignLists(string text, long expectedUnits)
    {
        Assert.True(Length.TryParse(text, out Length value, out bool wasRounded), text);
        Assert.Equal(expectedUnits, value.Units);
        Assert.False(wasRounded);
        Assert.Equal(value, Length.Parse(text));
    }

    [Trait("Feature", "GEO-004")]
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("5mm")]
    [InlineData("5 cm")]
    [InlineData("5 metres")]
    [InlineData("6 3")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData("3 apples")]
    [InlineData("1/")]
    [InlineData("3.\"")]
    public void RejectsUnitsItDoesNotKnowAndMalformedText(string? text)
    {
        Assert.False(Length.TryParse(text, out Length value, out bool wasRounded));
        Assert.Equal(Length.Zero, value);
        Assert.False(wasRounded);
        Assert.Throws<FormatException>(() => Length.Parse(text!));
    }

    [Trait("Feature", "GEO-004")]
    [Theory]
    [InlineData("1/3", true, 341)]
    [InlineData("3.505", true, 3589)]
    [InlineData("3.5", false, 3584)]
    [InlineData("0.001", true, 1)]
    public void ReportsWhetherTheParsedValueWasOnTheGrid(string text, bool expectedRounded, long expectedUnits)
    {
        Assert.True(Length.TryParse(text, out Length value, out bool wasRounded));
        Assert.Equal(expectedUnits, value.Units);
        Assert.Equal(expectedRounded, wasRounded);
    }

    [Trait("Feature", "GEO-004")]
    [Theory]
    [InlineData(77120, 16, "6'-3 5/16\"")]
    [InlineData(12288, 16, "1'-0\"")]
    [InlineData(0, 16, "0\"")]
    [InlineData(-768, 16, "-3/4\"")]
    [InlineData(768, 16, "3/4\"")]
    [InlineData(512, 16, "1/2\"")]          // 8/16 reduces to 1/2
    [InlineData(3584, 16, "3 1/2\"")]
    [InlineData(73728, 16, "6'-0\"")]
    [InlineData(-77120, 16, "-6'-3 5/16\"")]
    [InlineData(76880, 64, "6'-3 5/64\"")]
    public void FormatsFeetInchesAndFractions(long units, int denominator, string expected)
    {
        FormattedLength formatted = new Length(units).Format(new FeetInchesFormat(denominator));

        Assert.Equal(expected, formatted.Text);
        Assert.True(formatted.IsExact);
    }

    [Theory]
    [InlineData(77120, "75 5/16\"")]
    [InlineData(12288, "12\"")]
    [InlineData(0, "0\"")]
    [InlineData(-768, "-3/4\"")]
    public void FormatsInchesOnlyWithoutCarryingIntoFeet(long units, string expected)
    {
        Assert.Equal(expected, new Length(units).Format(new InchesOnlyFormat(16)).Text);
    }

    [Fact]
    public void FormatsDecimalInches()
    {
        Assert.Equal("75.3125\"", new Length(77120).Format(new DecimalInchesFormat(4)).Text);
        Assert.True(new Length(77120).Format(new DecimalInchesFormat(4)).IsExact);

        Assert.Equal("0.3330\"", new Length(341).Format(new DecimalInchesFormat(4)).Text);
        Assert.False(new Length(341).Format(new DecimalInchesFormat(4)).IsExact);

        Assert.Equal("-3.5000\"", new Length(-3584).Format(new DecimalInchesFormat(4)).Text);
        Assert.Equal("4\"", new Length(3584).Format(new DecimalInchesFormat(0)).Text);
    }

    [Trait("Feature", "GEO-004")]
    [Fact]
    public void DisplayRoundingIsHalfAwayFromZeroAndSaysWhenItIsInexact()
    {
        // 1/32" shown at 1/16" precision reads as 1/16", not 0 — how a tape measure is read.
        Length oneThirtySecond = Length.Inches(0, 1, 32);
        FormattedLength formatted = oneThirtySecond.Format(new FeetInchesFormat(16));

        Assert.Equal("1/16\"", formatted.Text);
        Assert.False(formatted.IsExact);

        // And it is exact at a precision that can hold it.
        Assert.True(oneThirtySecond.Format(new FeetInchesFormat(32)).IsExact);
        Assert.Equal("1/32\"", oneThirtySecond.Format(new FeetInchesFormat(32)).Text);

        // Negatives round away from zero too.
        Assert.Equal("-1/16\"", (-oneThirtySecond).Format(new FeetInchesFormat(16)).Text);
    }

    [Fact]
    public void FormatRefusesAPrecisionThatIsNotOnTheGrid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FeetInchesFormat(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InchesOnlyFormat(2048));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FeetInchesFormat(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DecimalInchesFormat(-1));
    }

    [Fact]
    public void ToStringIsAlwaysExact()
    {
        Assert.Equal("75 323/1024\"", new Length(77123).ToString());
        Assert.Equal("0\"", Length.Zero.ToString());
    }

    [Trait("Feature", "GEO-015")]
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void P8_ParseOfFormatAtTheFullGridRoundTrips(int seed)
    {
        // Property P8's round-trip clause (§7.2). A seeded loop rather than a property-testing
        // package: Core.Geometry takes no third-party dependency, and the seed is printed on
        // failure so any counterexample is reproducible.
        Random random = new(seed);

        for (int iteration = 0; iteration < 500; iteration++)
        {
            Length original = new(random.NextInt64(-50L * Length.UnitsPerFoot, 50L * Length.UnitsPerFoot));

            foreach (LengthFormat format in new LengthFormat[]
                     {
                         new FeetInchesFormat((int)Length.UnitsPerInch),
                         new InchesOnlyFormat((int)Length.UnitsPerInch),
                     })
            {
                FormattedLength formatted = original.Format(format);
                string because = $"seed {seed}, iteration {iteration}, units {original.Units}, text '{formatted.Text}'";

                Assert.True(formatted.IsExact, because);
                Assert.True(Length.TryParse(formatted.Text, out Length parsed, out bool wasRounded), because);
                Assert.False(wasRounded, because);
                Assert.True(original == parsed, because);
            }
        }
    }
}
