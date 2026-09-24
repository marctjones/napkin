using Xunit;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The edges of exact length arithmetic (#94): every value on the grid, the two ways rounding can
/// go, and text a person might type that should be refused, not guessed at.
/// </summary>
/// <remarks>
/// <see cref="LengthTextTests"/> and <see cref="LengthTests"/> hold the examples the design lists.
/// This file is the sweep: exhaustive where the space is small enough to be exhaustive, and stated
/// as a rule where it is not. Randomised cases use fixed seeds and no third-party package.
/// </remarks>
public class LengthExactnessTests
{
    const long EightFeet = 8 * Length.UnitsPerFoot;

    [Fact]
    public void Every_grid_value_from_minus_eight_to_plus_eight_feet_round_trips_exactly_in_both_full_precision_formats()
    {
        LengthFormat[] formats =
        [
            new FeetInchesFormat((int)Length.UnitsPerInch),
            new InchesOnlyFormat((int)Length.UnitsPerInch),
        ];

        for (long units = -EightFeet; units <= EightFeet; units++)
        {
            Length original = new(units);
            foreach (LengthFormat format in formats)
            {
                FormattedLength formatted = original.Format(format);
                Assert.True(formatted.IsExact, $"units {units} '{formatted.Text}' is on the grid, so it must format exactly.");
                Assert.True(Length.TryParse(formatted.Text, out Length parsed, out bool wasRounded), $"units {units} '{formatted.Text}' must parse.");
                Assert.False(wasRounded, $"units {units} '{formatted.Text}' was on the grid and must not report rounding.");
                Assert.Equal(original, parsed);
            }
        }
    }

    [Fact]
    public void Display_at_sixteenths_is_exact_only_on_a_sixteenth_and_otherwise_within_half_of_one()
    {
        const long Sixteenth = Length.UnitsPerInch / 16;
        FeetInchesFormat sixteenths = new(16);

        for (long units = -Length.UnitsPerFoot; units <= Length.UnitsPerFoot; units++)
        {
            FormattedLength shown = new Length(units).Format(sixteenths);

            Assert.Equal(units % Sixteenth == 0, shown.IsExact);

            // What is shown, read back, is the nearest sixteenth: never more than half a sixteenth off.
            Assert.True(Length.TryParse(shown.Text, out Length back, out _), shown.Text);
            Assert.True(Math.Abs(back.Units - units) <= Sixteenth / 2, $"units {units} showed '{shown.Text}', which is {back.Units - units} units away.");
            Assert.Equal(0, back.Units % Sixteenth);
        }
    }

    [Theory]
    [InlineData("3'-4 1/4\"", 3L * 12 * 1024 + 4 * 1024 + 256, false)]
    [InlineData("40.5", 40L * 1024 + 512, false)]
    [InlineData("0'6\"", 6L * 1024, false)]
    [InlineData("0'-6\"", 6L * 1024, false)]
    [InlineData(".5", 512L, false)]
    [InlineData("1 1/2\"", 1536L, false)]
    [InlineData("1/3\"", 341L, true)]
    public void Typed_text_gives_the_documented_value_and_says_when_it_had_to_round(string text, long expectedUnits, bool expectedRounded)
    {
        Assert.True(Length.TryParse(text, out Length value, out bool wasRounded), text);
        Assert.Equal(expectedUnits, value.Units);
        Assert.Equal(expectedRounded, wasRounded);
    }

    [Theory]
    [InlineData("1/0")]
    [InlineData("1/0\"")]
    [InlineData("1 1/2 1/2")]
    [InlineData("1..5")]
    [InlineData("1.5.5")]
    [InlineData("--3")]
    [InlineData("3'-'4")]
    [InlineData("99999999999999999999")]
    [InlineData("99999999999999999999'")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("½")]
    public void Hostile_or_ambiguous_text_is_refused_and_never_throws_or_guesses(string text)
    {
        Assert.False(Length.TryParse(text, out Length value, out bool wasRounded), $"'{text}' parsed as {value.Units}.");
        Assert.Equal(Length.Zero, value);
        Assert.False(wasRounded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Sums_and_differences_are_exact_commutative_and_associative(int seed)
    {
        Random random = new(seed);
        for (int i = 0; i < 2000; i++)
        {
            Length a = new(random.NextInt64(-EightFeet, EightFeet));
            Length b = new(random.NextInt64(-EightFeet, EightFeet));
            Length c = new(random.NextInt64(-EightFeet, EightFeet));
            string because = $"seed {seed}: {a.Units}, {b.Units}, {c.Units}";

            Assert.Equal(a + b, b + a);
            Assert.Equal((a + b) + c, a + (b + c));
            Assert.Equal(a, (a + b) - b);
            Assert.Equal(a.Units + b.Units, (a + b).Units);
            Assert.True((a < b) == (a.Units < b.Units), because);
        }
    }

    [Theory]
    [InlineData(1, 0, 1)]      // half of 1/1024" is a tie: to even → 0, away from zero → 1
    [InlineData(3, 2, 2)]      // 1.5 → 2 either way (2 is even and is away from zero)
    [InlineData(5, 2, 3)]      // 2.5 → even gives 2, away gives 3
    [InlineData(7, 4, 4)]      // 3.5 → 4 either way
    [InlineData(-1, 0, -1)]
    [InlineData(-5, -2, -3)]
    public void Halving_an_odd_number_of_units_follows_the_rounding_it_is_told(long units, long toEven, long awayFromZero)
    {
        Length length = new(units);

        Assert.Equal(toEven, length.Divide(2, Rounding.HalfToEven).Units);
        Assert.Equal(awayFromZero, length.Divide(2, Rounding.HalfAwayFromZero).Units);
        Assert.False(length.TryDivideExact(2, out _), "an odd count of units cannot be halved exactly.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Rounding_is_symmetric_about_zero_and_an_even_count_halves_exactly(int seed)
    {
        Random random = new(seed);
        for (int i = 0; i < 2000; i++)
        {
            long units = random.NextInt64(1, EightFeet);
            foreach (Rounding rounding in new[] { Rounding.HalfToEven, Rounding.HalfAwayFromZero })
            {
                Assert.Equal(-new Length(units).Divide(2, rounding), new Length(-units).Divide(2, rounding));
            }

            Length even = new(units * 2);
            Assert.True(even.TryDivideExact(2, out Length half));
            Assert.Equal(new Length(units), half);
            Assert.Equal(half, even.Divide(2, Rounding.HalfToEven));
        }
    }

    [Fact]
    public void Three_thirds_of_an_inch_are_one_unit_short_of_the_inch_and_that_is_the_stated_rule()
    {
        // 1024 does not divide by 3. Each third is rounded on its own, so three of them do not add
        // back to the whole: a person cutting a board in thirds is off by 1/1024" and can be told so.
        Length third = Length.Inches(1).Divide(3, Rounding.HalfAwayFromZero);

        Assert.Equal(341, third.Units);
        Assert.Equal(Length.Inches(1) - (third * 3), new Length(1));
        Assert.False(Length.Inches(1).TryDivideExact(3, out _));
    }
}
