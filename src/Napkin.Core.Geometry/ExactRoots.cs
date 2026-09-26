namespace Napkin.Core.Geometry;

/// <summary>
/// Integer square roots over <see cref="Int128"/>, for proving a derived length exact rather than
/// inferring it from a double (docs/design/assembly-model.md §3a.4). The BCL has none.
/// </summary>
/// <remarks>
/// A proof that would not fit declines: the answer is "not proven", never an exception, so that
/// <c>≈</c> is the conservative answer everywhere. Only a part far beyond anything a person builds —
/// a thousand miles long — gets near the limit.
/// </remarks>
public static class ExactRoots
{
    /// <summary>The largest value whose root is sought; above it the proof declines. 2¹²⁴, leaving the correction steps room.</summary>
    public static readonly Int128 Limit = Int128.One << 124;

    /// <summary>
    /// The exact square root of <paramref name="value"/> when it is a perfect square, or
    /// <see langword="null"/> when it is not, is negative, or is past <see cref="Limit"/>.
    /// </summary>
    public static Int128? SquareRoot(Int128 value)
    {
        if (value < Int128.Zero || value > Limit)
        {
            return null;
        }

        Int128 root = Floor(value);
        return root * root == value ? root : null;
    }

    /// <summary>The largest integer whose square does not exceed <paramref name="value"/>, which is at most <see cref="Limit"/>.</summary>
    static Int128 Floor(Int128 value)
    {
        // A double's square root lands within a few units of the truth at this size; step onto it.
        Int128 root = (Int128)Math.Sqrt((double)value);
        while (root * root > value)
        {
            root--;
        }

        while ((root + 1) * (root + 1) <= value)
        {
            root++;
        }

        return root;
    }

    /// <summary>
    /// A product of integers, or <see langword="null"/> when it would overflow — what a proof uses so
    /// that an out-of-range case declines instead of throwing.
    /// </summary>
    public static Int128? Product(params Int128[] factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        try
        {
            Int128 product = Int128.One;
            foreach (Int128 factor in factors)
            {
                product = checked(product * factor);
            }

            return product;
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
