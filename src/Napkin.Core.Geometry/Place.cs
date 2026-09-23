using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// What a <see cref="PlaceRef"/> fixes: a coordinate on each world axis it speaks about, and nothing
/// on the others (<c>docs/design/assembly-model.md</c> &#xA7;2.1, &#xA7;2.2). A face fixes one axis,
/// an edge two, a vertex or a centre three; a node fixes X and Y; an axis-aligned segment one.
/// </summary>
/// <remarks>
/// <para>
/// Three optional exact lengths rather than a dictionary of axes, so that two places are equal by
/// value — the trap <see cref="Box.Equals(Box?)"/> had to hand-fix for its cuts — and so that nothing
/// here can hold a <see cref="double"/>. A coordinate is exactly what <see cref="Sketch.PlaceOf"/>
/// read off the geometry: for a box on one of the 24 orientations, an anchor component plus or minus
/// a stored size.
/// </para>
/// <para>
/// <c>default(Place)</c> fixes nothing — a diagonal segment's place.
/// </para>
/// </remarks>
/// <param name="X">The coordinate along world X, or <see langword="null"/> when X is not fixed.</param>
/// <param name="Y">The coordinate along world Y, or <see langword="null"/> when Y is not fixed.</param>
/// <param name="Z">The coordinate along world Z, or <see langword="null"/> when Z is not fixed.</param>
public readonly record struct Place(Length? X, Length? Y, Length? Z)
{
    private static readonly Axis[] AllAxes = [Axis.X, Axis.Y, Axis.Z];

    /// <summary>The world axes this place fixes, in X, Y, Z order.</summary>
    public ImmutableArray<Axis> Axes => [.. AllAxes.Where(Fixes)];

    /// <summary>How many axes this place fixes: 0 to 3.</summary>
    public int Count => (X is null ? 0 : 1) + (Y is null ? 0 : 1) + (Z is null ? 0 : 1);

    /// <summary>Whether this place fixes a coordinate on <paramref name="axis"/>.</summary>
    public bool Fixes(Axis axis) => Coordinate(axis) is not null;

    /// <summary>The coordinate on <paramref name="axis"/>, or <see langword="null"/> when this place does not fix it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is not an axis.</exception>
    public Length? Coordinate(Axis axis) => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The coordinate on <paramref name="axis"/>, which this place must fix.</summary>
    /// <exception cref="InvalidOperationException">This place does not fix <paramref name="axis"/>.</exception>
    public Length this[Axis axis]
        => Coordinate(axis) ?? throw new InvalidOperationException($"{this} does not fix {axis}.");

    /// <summary>The axes both places fix, in X, Y, Z order: what a <see cref="Coincident"/> holds equal.</summary>
    public static ImmutableArray<Axis> Common(Place a, Place b) => [.. AllAxes.Where(axis => a.Fixes(axis) && b.Fixes(axis))];

    /// <summary>A place fixing one axis.</summary>
    public static Place On(Axis axis, Length coordinate) => default(Place).With(axis, coordinate);

    /// <summary>This place, also fixing <paramref name="axis"/> at <paramref name="coordinate"/>.</summary>
    public Place With(Axis axis, Length coordinate) => axis switch
    {
        Axis.X => this with { X = coordinate },
        Axis.Y => this with { Y = coordinate },
        Axis.Z => this with { Z = coordinate },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>
    /// The axes in words, for a refusal message: "Z", "X and Y", "X, Y and Z", or "nothing".
    /// </summary>
    public string AxesInWords() => Axes switch
    {
        [] => "nothing",
        [var only] => only.ToString(),
        [var first, var second] => $"{first} and {second}",
        var all => $"{string.Join(", ", all.Take(all.Length - 1))} and {all[^1]}",
    };

    /// <inheritdoc/>
    public override string ToString()
    {
        Place self = this;
        return Count == 0
            ? "Place(nothing)"
            : $"Place({string.Join(", ", Axes.Select(axis => $"{axis} = {self[axis]}"))})";
    }
}
