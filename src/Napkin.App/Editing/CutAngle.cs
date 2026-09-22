using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.App.Editing;

/// <summary>
/// Angle as an <em>entry mode</em> for a corner cut, with exactly one explicit rounding
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;1.3, &#xA7;7.2).
/// </summary>
/// <remarks>
/// <para>
/// A cut has two descriptions — the angle it is cut at and the two marks it is laid out from — and
/// on a fixed grid only one of them can be exact. The stored form is the setbacks, so an angle
/// typed into the workshop is converted to the setback it implies, once, here; and a setback is
/// read back as the angle it comes out at, marked <c>&#x2248;</c> unless it is exact. That is
/// <see cref="Cut"/>'s own note that "an angle is an entry mode with one explicit rounding, which
/// belongs to the editor", made real.
/// </para>
/// <para>
/// The formula is the cut list's: the angle off square is <c>atan(setback / whole)</c>, to the
/// nearest half degree, where <em>whole</em> is the edge the cut runs the whole of. It is written
/// out again here rather than shared because
/// <c>Napkin.Modules.Furniture.CutDescription</c> owns a <em>sentence</em> and this owns a
/// <em>field</em>; what matters is that they agree about the number, which their tests check.
/// Equal setbacks are the only exact case: by Niven's theorem the tangent of any other whole or
/// half degree is irrational, so no other pair of exact lengths lands on one.
/// </para>
/// </remarks>
public static class CutAngle
{
    /// <summary>The marker a length or an angle carries when it is not the stored value.</summary>
    public const string Approximately = "≈";

    /// <summary>The angle off square a setback makes against the edge the cut runs the whole of.</summary>
    /// <param name="setback">The short setback: the mark on the edge that survives.</param>
    /// <param name="whole">The edge the cut runs the whole of.</param>
    /// <returns>Degrees off square, to the nearest half.</returns>
    public static double DegreesOffSquare(Length setback, Length whole)
    {
        if (whole <= Length.Zero)
        {
            return 0;
        }

        double degrees = Math.Atan2(setback.Units, whole.Units) * 180 / Math.PI;
        return Math.Round(degrees * 2, MidpointRounding.AwayFromZero) / 2;
    }

    /// <summary>
    /// Whether the angle a setback reads as is exactly the angle it is: only when the two lengths
    /// are equal, which is 45&#xB0;.
    /// </summary>
    public static bool IsExact(Length setback, Length whole) => setback == whole;

    /// <summary>The angle a field shows for a setback, with the <c>&#x2248;</c> when it is derived.</summary>
    public static string Text(Length setback, Length whole)
    {
        double halves = DegreesOffSquare(setback, whole);
        string degrees = halves.ToString(
            halves == Math.Floor(halves) ? "0" : "0.0",
            CultureInfo.InvariantCulture) + "°";

        return IsExact(setback, whole) ? degrees : Approximately + degrees;
    }

    /// <summary>
    /// The setback an angle asks for: the one explicit rounding onto the grid.
    /// </summary>
    /// <param name="whole">The edge the cut runs the whole of.</param>
    /// <param name="degrees">The angle off square, as it was typed.</param>
    /// <returns>
    /// The setback, or <see langword="null"/> when the angle is not one a cut can be made at —
    /// nothing at or below zero, and nothing at or past a quarter turn, which is a cut parallel to
    /// the edge it is measured from.
    /// </returns>
    public static Length? SetbackFor(Length whole, double degrees)
    {
        if (whole <= Length.Zero || !double.IsFinite(degrees) || degrees <= 0 || degrees >= 90)
        {
            return null;
        }

        Length setback = Length.FromInches(
            whole.ToInches() * Math.Tan(degrees * Math.PI / 180),
            Rounding.HalfAwayFromZero);

        return setback > Length.Zero ? setback : null;
    }

    /// <summary>
    /// Reads an angle a person typed: a number, with or without a degree sign.
    /// </summary>
    /// <remarks>
    /// Lengths have <see cref="LengthParser"/> to read feet and inches; an angle is a plain number
    /// of degrees, so this is the whole of it. Anything else is refused, and the field says so,
    /// rather than being taken for a number it does not say.
    /// </remarks>
    public static bool TryParseDegrees(string? text, out double degrees)
    {
        degrees = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string cleaned = text.Trim().TrimEnd('°', 'd', 'D').Trim();
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees);
    }

    /// <summary>
    /// The full mitre &#xA7;7.2 asks for: both setbacks at the blank's width, in one
    /// <see cref="SetCut"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "The blank's width" is the rail's <em>narrow</em> dimension — the width of the stick the
    /// moulding is cut from — so a full mitre is a 45&#xB0; cut that runs the whole of the narrow
    /// edge. <see cref="Length.Min"/> of the two plan sizes is the only reading of that which
    /// always satisfies invariant 7 (<c>AlongX &#x2264; Width</c> and
    /// <c>AlongY &#x2264; Height</c>) whichever way round the rail was drawn.
    /// </para>
    /// <para>
    /// It is <strong>one</strong> assignment and nothing more: the two setbacks are set together
    /// and are not related afterwards, because nothing in this beta keeps cuts related to each
    /// other (&#xA7;2.5's stated gap, issue #67). Changing the rail's width later does not move
    /// them.
    /// </para>
    /// </remarks>
    public static CornerCut FullMitre(Box blank, BoxCorner corner)
    {
        ArgumentNullException.ThrowIfNull(blank);

        Length across = Length.Min(blank.Width, blank.Height);
        return new CornerCut(corner, across, across);
    }
}
