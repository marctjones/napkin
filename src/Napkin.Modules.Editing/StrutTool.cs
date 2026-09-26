using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// Draw → Angled part: two clicks, one per end (<c>docs/design/assembly-model.md</c> &#xA7;3a.7,
/// <c>docs/design/angled-parts.md</c> &#xA7;1.2). The first click is held; the second makes one
/// <see cref="AddEntity"/> of a <see cref="Strut"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The ends.</strong> In the 3D view each click is a point on what the pointer is over: the
/// floor, at Z = 0, cut <see cref="EndCut.Z"/> — the floor is the plane a foot meets — or a face of a
/// part, cut to that face's axis. In the plan both clicks are at Z = 0, where a strut cut to the floor
/// at both ends would lie in the plane it is cut to, which is no board at all
/// (<see cref="ValidationErrorKind.StrutCutAlongItself"/>); so the plan makes a flat brace with square
/// ends, and the panel types the top's height and turns its cuts to the floor and the seat.
/// <see cref="EndCut.Square"/> is otherwise chosen, never defaulted.
/// </para>
/// <para>
/// <strong>The reference.</strong> The highest-priority axis the ends are cut to, <c>Z</c> then
/// <c>Y</c> then <c>X</c>, and <c>Z</c> when both are square: a leg drawn floor to seat keeps its
/// wide face vertical and its ends plain mitres unless the panel says otherwise (angled-parts &#xA7;1.2).
/// </para>
/// </remarks>
public sealed class StrutTool
{
    /// <summary>The words the panel offers for each reference axis (angled-parts &#xA7;1.2).</summary>
    public static string ReferenceWords(Axis axis) => axis switch
    {
        Axis.Z => "keep the wide face vertical",
        Axis.X => "keep the wide face parallel to the long side",
        _ => "keep the wide face parallel to the short side",
    };

    /// <summary>The first end, once it has been clicked; null before.</summary>
    public (Point3 At, EndCut Cut)? First { get; private set; }

    /// <summary>Takes a click. The first is held; the second returns the strut the two make, or null when they coincide.</summary>
    /// <param name="at">Where the click landed, snapped.</param>
    /// <param name="cut">The plane that end is cut to there.</param>
    /// <param name="make">What to make of the two ends, once there are two.</param>
    public Strut? Click(Point3 at, EndCut cut, Func<Point3, EndCut, Point3, EndCut, Strut> make)
    {
        ArgumentNullException.ThrowIfNull(make);
        if (First is not { } first)
        {
            First = (at, cut);
            return null;
        }

        First = null;
        return first.At == at ? null : make(first.At, first.Cut, at, cut);
    }

    /// <summary>Forgets a held first click: Escape, or the tool put down.</summary>
    public void Cancel() => First = null;

    /// <summary>The reference axis a new strut takes from its cuts: the highest-priority cut axis, Z when both ends are square.</summary>
    public static Axis DefaultReference(EndCut fromCut, EndCut toCut)
        => fromCut == EndCut.Z || toCut == EndCut.Z ? Axis.Z
            : fromCut == EndCut.Y || toCut == EndCut.Y ? Axis.Y
            : fromCut == EndCut.X || toCut == EndCut.X ? Axis.X
            : Axis.Z;

    /// <summary>A new strut between two clicked ends, on a layer, with a cross-section and its default reference.</summary>
    public static Strut Make(EntityId id, LayerId layer, Point3 from, EndCut fromCut, Point3 to, EndCut toCut, Length height, Length depth)
        => new(id, layer, from, to, fromCut, toCut, DefaultReference(fromCut, toCut), height, depth);

    /// <summary>
    /// Why two clicks cannot be a strut, in words, or <see langword="null"/> when they can: ends along
    /// one axis are a box, and an end cut to a plane the strut runs along has no board.
    /// </summary>
    public static string? Refusal(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);
        return !Strut.LeansIn(strut.Direction)
            ? "Those two points line up along one axis: that is a straight part. Draw it with the rectangle tool."
            : Strut.CutAlongItself(strut) is { } along
                ? $"An end cut to the {along.ToString().ToUpperInvariant()} plane would run along the part. Put the ends at different heights."
                : null;
    }

    /// <summary>
    /// The panel's three derived readouts, each marked ≈ unless exact (assembly-model &#xA7;3a.7,
    /// angled-parts &#xA7;2.4): the long-point length, the tilt from the reference axis, and the
    /// azimuth in the plan from east, counter-clockwise.
    /// </summary>
    public static (string Length, string Tilt, string Azimuth) Readouts(Strut strut, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(strut);
        StrutBlank blank = strut.Blank();
        FormattedLength length = blank.Length.Value.Format(format);
        string lengthText = blank.Length.Exact && length.IsExact ? length.Text : "≈" + length.Text;

        Vector3 d = strut.Direction;
        double dx = d.Dx.ToInches(), dy = d.Dy.ToInches(), dz = d.Dz.ToInches();
        double along = strut.Reference switch { Axis.X => dx, Axis.Y => dy, _ => dz };
        double across = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz) - (along * along));
        double tilt = Math.Atan2(across, Math.Abs(along)) * 180 / Math.PI;
        double azimuth = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;

        return (lengthText, Degrees(tilt), Degrees(azimuth));
    }

    // Display only, to the nearest half degree; exact only when it lands on a whole quarter turn,
    // which a leaning strut's tilt never does and its azimuth does when it runs along one plan axis.
    static string Degrees(double degrees)
    {
        double halves = Math.Round(degrees * 2, MidpointRounding.AwayFromZero) / 2;
        string text = halves.ToString(halves == Math.Floor(halves) ? "0" : "0.0", CultureInfo.InvariantCulture) + "°";
        return Math.Abs(degrees - (Math.Round(degrees / 90) * 90)) < 1e-9 ? text : "≈" + text;
    }
}
