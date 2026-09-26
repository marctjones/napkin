namespace Napkin.Core.Geometry.Tests;

/// <summary>The integer square root a strut's exactness proofs rest on (docs/design/angled-parts.md).</summary>
public class ExactRootsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(625, 25)]
    [InlineData(169, 13)]
    [InlineData(26214400, 5120)]
    public void APerfectSquareHasItsRoot(long value, long root) =>
        Assert.Equal((Int128)root, ExactRoots.SquareRoot(value));

    [Theory]
    [InlineData(2)]
    [InlineData(624)]
    [InlineData(626)]
    [InlineData(887095296)]
    [InlineData(-4)]
    public void AnythingElseHasNone(long value) => Assert.Null(ExactRoots.SquareRoot(value));

    [Fact]
    public void ALargeSquareIsFoundWhereADoubleAloneWouldMissIt()
    {
        // 2⁶² − 1 squared is not representable in a double; the correction steps find it anyway.
        Int128 root = (Int128.One << 62) - 1;
        Assert.Equal(root, ExactRoots.SquareRoot(root * root));
        Assert.Null(ExactRoots.SquareRoot((root * root) + 1));
        Assert.Null(ExactRoots.SquareRoot((root * root) - 1));
    }

    [Fact]
    public void ASquareWhoseDoubleRootLandsShortIsStillFound()
    {
        // Math.Sqrt of this square, as a double, truncates to one below its root.
        Int128 root = Int128.Parse("11223131235081369", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(root, ExactRoots.SquareRoot(root * root));
        Assert.Null(ExactRoots.SquareRoot(Int128.Parse("125958674719859055259655874658176", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void TheLimitItselfIsProvedAndPastItTheProofDeclines()
    {
        Assert.Equal(Int128.One << 62, ExactRoots.SquareRoot(ExactRoots.Limit));
        Assert.Null(ExactRoots.SquareRoot(ExactRoots.Limit + 1));
        Assert.Null(ExactRoots.SquareRoot(Int128.MaxValue));
    }

    [Fact]
    public void AProductThatWouldOverflowDeclines()
    {
        Assert.Equal((Int128)60, ExactRoots.Product(3, 4, 5));
        Assert.Equal(Int128.One, ExactRoots.Product());
        Assert.Null(ExactRoots.Product(Int128.One << 64, Int128.One << 64));
    }
}
