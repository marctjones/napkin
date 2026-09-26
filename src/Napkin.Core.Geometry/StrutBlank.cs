using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>A length derived from exact inputs and rounded onto the grid once, with whether it was proven to be exact.</summary>
/// <param name="Value">The value on the grid.</param>
/// <param name="Exact">Whether the true value was proven, in integers, to be <paramref name="Value"/>; otherwise it is shown with <c>≈</c>.</param>
public readonly record struct DerivedLength(Length Value, bool Exact);

/// <summary>A cut derived from exact inputs, its setback rounded once, with whether it was proven exact.</summary>
/// <param name="Cut">The cut, as a box would carry it.</param>
/// <param name="Exact">Whether its setback was proven to be on the grid.</param>
public readonly record struct DerivedCut(Cut Cut, bool Exact);

/// <summary>
/// An angle on a cut-list row: derived, display only, never stored and never an <see cref="Angle"/>
/// in the sketch (<c>docs/design/angled-parts.md</c> §2.4).
/// </summary>
/// <param name="Degrees">The derived value.</param>
/// <param name="Exact">Whether it was proven exact in integers — a mitre at 0° or 45°, a bevel at 30°.</param>
public readonly record struct DerivedAngle(double Degrees, bool Exact)
{
    /// <summary>The value to show: the nearest half degree, a half rounded away from zero.</summary>
    public double Shown => Math.Round(Degrees * 2, MidpointRounding.AwayFromZero) / 2;
}

/// <summary>An end of a strut's blank, in the blank's own frame: never "from" or "to", which the derivation may have swapped.</summary>
public enum BlankEnd
{
    /// <summary>The end at local −X.</summary>
    West,

    /// <summary>The end at local +X.</summary>
    East,
}

/// <summary>
/// Where the long point of a compound end is: the sign of its local Y (−1 south, +1 north) and local
/// Z (−1 bottom, +1 top). Zero when the long point is a whole edge along that axis rather than a corner.
/// </summary>
/// <param name="Y">South (−1), north (+1), or the whole width (0).</param>
/// <param name="Z">Bottom (−1), top (+1), or the whole depth (0).</param>
public readonly record struct StrutCorner(int Y, int Z);

/// <summary>
/// An end whose cut is not perpendicular to the blank's wide face: a mitre and a bevel
/// (<c>docs/design/angled-parts.md</c> §2.2). A description of a derived board, never stored.
/// </summary>
/// <param name="End">Which end of the blank.</param>
/// <param name="Mitre">The swing across the wide face, from square: <c>tan α = |n_y| / |n_d|</c>.</param>
/// <param name="Bevel">The tilt from perpendicular to the wide face: <c>sin β = |n_z|</c>.</param>
/// <param name="LongPoint">The corner of the end that reaches farthest.</param>
public sealed record DerivedCompoundEnd(BlankEnd End, DerivedAngle Mitre, DerivedAngle Bevel, StrutCorner LongPoint);

/// <summary>
/// The board a strut is cut from: derived, never stored (<c>docs/design/assembly-model.md</c> §3a.4,
/// <c>docs/design/angled-parts.md</c> §2.1–§2.2). Its long-point length is rounded once from exact
/// integers, and each value says whether it was proven exact.
/// </summary>
/// <param name="Length">The long-point length <c>L</c>: its length along local X, to the farthest corner at each end.</param>
/// <param name="Cuts">The plain mitres as <see cref="CornerCut"/>s on the wide face, in site order like <see cref="Box.Cuts"/>.</param>
/// <param name="CompoundEnds">The compound ends, west first.</param>
public sealed record StrutBlank(DerivedLength Length, ImmutableList<DerivedCut> Cuts, ImmutableList<DerivedCompoundEnd> CompoundEnds)
{
    /// <summary>
    /// Derives a strut's blank. The operation order is fixed so that equal inputs give bit-identical
    /// doubles on every machine: the exact integer sums of squares, one square root each of
    /// <c>|d|²</c> and <c>|z|²</c>, then §2.1's formula as written, each end in turn, rounded once.
    /// </summary>
    /// <exception cref="ArgumentException">The strut fails invariant 14 or cuts an end along itself: there is no blank.</exception>
    public static StrutBlank Derive(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);
        StrutFrame frame = strut.Frame();
        if (Strut.CutAlongItself(strut) is { } along)
        {
            throw new ArgumentException($"Strut {strut.Id}'s {along} cut runs along it; there is no blank.", nameof(strut));
        }

        Int128 d2 = frame.D.SquaredLength;
        Int128 z2 = frame.Z.SquaredLength;
        double c = Math.Sqrt((double)d2);
        double zn = Math.Sqrt((double)z2);
        Int128? zRoot = ExactRoots.SquareRoot(z2);

        EndTerms west = Terms(strut, frame, frame.Reversed ? strut.ToCut : strut.FromCut, BlankEnd.West, c, zn, zRoot, z2);
        EndTerms east = Terms(strut, frame, frame.Reversed ? strut.FromCut : strut.ToCut, BlankEnd.East, c, zn, zRoot, z2);

        double units = c;
        units += west.Reach;
        units += east.Reach;
        Length value = Geometry.Length.FromInches(units / Geometry.Length.UnitsPerInch, Rounding.HalfToEven);

        // L = c + (s₁ + s₂)/2 is exact iff c is, both setbacks are, and their sum is even; a compound
        // term is never proven (angled-parts §11 decision 5).
        bool exact = ExactRoots.SquareRoot(d2) is not null
                     && west.Compound is null && east.Compound is null
                     && west.SetbackExact && east.SetbackExact
                     && ((west.Setback + east.Setback) % 2) == 0;

        ImmutableList<DerivedCut> cuts =
        [
            .. new[] { west.Cut, east.Cut }
                .Where(cut => cut is not null)
                .Select(cut => cut!.Value)
                .OrderBy(cut => cut.Cut.Site),
        ];
        ImmutableList<DerivedCompoundEnd> compound = [.. new[] { west.Compound, east.Compound }.OfType<DerivedCompoundEnd>()];
        return new StrutBlank(new DerivedLength(value, exact), cuts, compound);
    }

    /// <summary>Equality by value, the lists compared as sequences — what lets mirror-image legs group into one row.</summary>
    public bool Equals(StrutBlank? other)
        => other is not null
           && Length == other.Length
           && Cuts.SequenceEqual(other.Cuts)
           && CompoundEnds.SequenceEqual(other.CompoundEnds);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Length);
        foreach (DerivedCut cut in Cuts)
        {
            hash.Add(cut);
        }

        foreach (DerivedCompoundEnd end in CompoundEnds)
        {
            hash.Add(end);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Invariant 17: the blank is a board that can be cut — shaped-parts invariants 7–9 on its rounded
    /// values, judged by the same code as a box's cuts when every end is plain, and on the four long
    /// edges when an end is compound. <see langword="null"/> when it holds.
    /// </summary>
    internal static ValidationError? TooShort(Strut strut, StrutBlank blank)
    {
        bool fits;
        if (blank.CompoundEnds.IsEmpty)
        {
            Box asBox = new(strut.Id, strut.Layer, Point3.Origin, blank.Length.Value, strut.Height, strut.Depth, BoxFace.Top, Angle.Zero)
            {
                Cuts = [.. blank.Cuts.Select(cut => cut.Cut)],
            };
            fits = CutRules.Errors(asBox).IsEmpty;
        }
        else
        {
            fits = EdgesArePositive(strut);
        }

        return fits ? null : new ValidationError(
            ValidationErrorKind.StrutTooShortForItsCuts,
            $"Strut {strut.Id} is too short for its end cuts: they would meet or cross along a {strut.Height} by {strut.Depth} board.");
    }

    /// <summary>
    /// One end's contribution: how far its farthest corner reaches past the centreline point, and the
    /// cut or compound end it is.
    /// </summary>
    private static EndTerms Terms(Strut strut, StrutFrame frame, EndCut cut, BlankEnd end, double c, double zn, Int128? zRoot, Int128 z2)
    {
        if (cut == EndCut.Square)
        {
            return new EndTerms(0, null, 0, true, null);
        }

        Axis k = AxisOf(cut);
        Int128 dk = Int128.Abs(frame.D.Component(k));
        Int128 yk = frame.Y.Component(k);
        Int128 zk = frame.Z.Component(k);
        int sigma = Int128.Sign(frame.D.Component(k));

        // n oriented along the strut: n_d > 0, and n_y, n_z carry σ.
        double nd = (double)dk / c;
        double ny = (double)Int128.Abs(yk) / (zn * c);
        double nz = (double)Int128.Abs(zk) / zn;
        double reach = ((0.5 * strut.Height.Units * ny) + (0.5 * strut.Depth.Units * nz)) / nd;

        if (zk == 0)
        {
            // A plain mitre in the wide face (§3a.3 steps 5–6): setback s = Height · tan α.
            double setback = strut.Height.Units * ny / nd;
            long s = Geometry.Length.FromInches(setback / Geometry.Length.UnitsPerInch, Rounding.HalfToEven).Units;
            bool exact = zRoot is { } root
                         && ExactRoots.Product(strut.Height.Units, Int128.Abs(yk)) is { } numerator
                         && ExactRoots.Product(root, dk) is { } denominator
                         && numerator % denominator == 0;
            bool southLong = sigma * Int128.Sign(yk) > 0;
            BoxCorner corner = end == BlankEnd.West
                ? (southLong ? BoxCorner.SouthWest : BoxCorner.NorthWest)
                : (southLong ? BoxCorner.NorthEast : BoxCorner.SouthEast);

            // A setback that rounds to nothing is no cut: the end is square on the grid.
            DerivedCut? derived = s > 0 ? new DerivedCut(new CornerCut(corner, new Length(s), strut.Height), exact) : null;
            return new EndTerms(reach, derived, s, exact, null);
        }

        const double degrees = 180 / Math.PI;
        DerivedAngle mitre = new(
            Math.Atan(ny / nd) * degrees,
            yk == 0 || (ExactRoots.Product(yk, yk) is { } y2 && ExactRoots.Product(dk, dk, z2) is { } dz2 && y2 == dz2));
        // A bevel is never exact. §2.4 names 30° (4(n·z)² = |z|²), but with an axis reference |z|² is
        // a sum of two integer squares a² + b², and 4a² = a² + b² asks for b = a√3.
        DerivedAngle bevel = new(Math.Asin(nz) * degrees, false);
        int flip = end == BlankEnd.West ? 1 : -1;
        StrutCorner longPoint = new(flip * sigma * Int128.Sign(yk), flip * sigma * Int128.Sign(zk));
        return new EndTerms(reach, null, 0, false, new DerivedCompoundEnd(end, mitre, bevel, longPoint));
    }

    /// <summary>Whether each of the four long edges of a blank with a compound end has positive length.</summary>
    private static bool EdgesArePositive(Strut strut)
    {
        StrutFrame frame = strut.Frame();
        double c = Math.Sqrt((double)frame.D.SquaredLength);
        double zn = Math.Sqrt((double)frame.Z.SquaredLength);

        // Extension past the centreline point, along the edge at (y, z) half-sizes, at each end.
        double Extension(EndCut cut, double y, double z)
        {
            if (cut == EndCut.Square)
            {
                return 0;
            }

            Axis k = AxisOf(cut);
            double sigma = Int128.Sign(frame.D.Component(k));
            double nd = (double)Int128.Abs(frame.D.Component(k)) / c;
            double ny = sigma * (double)frame.Y.Component(k) / (zn * c);
            double nz = sigma * (double)frame.Z.Component(k) / zn;
            return ((y * ny) + (z * nz)) / nd;
        }

        EndCut west = frame.Reversed ? strut.ToCut : strut.FromCut;
        EndCut east = frame.Reversed ? strut.FromCut : strut.ToCut;
        foreach (int ySign in new[] { -1, 1 })
        {
            foreach (int zSign in new[] { -1, 1 })
            {
                double y = ySign * 0.5 * strut.Height.Units;
                double z = zSign * 0.5 * strut.Depth.Units;
                if (c + Extension(west, y, z) - Extension(east, y, z) <= 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Every caller has already set a square end aside.
    private static Axis AxisOf(EndCut cut) => cut switch
    {
        EndCut.X => Axis.X,
        EndCut.Y => Axis.Y,
        _ => Axis.Z,
    };

    private readonly record struct EndTerms(double Reach, DerivedCut? Cut, long Setback, bool SetbackExact, DerivedCompoundEnd? Compound);
}
