namespace Napkin.Core.Geometry;

/// <summary>
/// How a box is turned in space: which of its local faces points up, then a spin about world Z
/// (docs/design/assembly-model.md &#xA7;1.3). With <see cref="Rotation"/> a quarter turn, the six
/// face-up choices times four spins are the 24 axis-aligned orientations, each with exactly one
/// spelling.
/// </summary>
/// <remarks>
/// <para>
/// A local vector <c>v</c> lands in the world at <c>Rz(Rotation) · Tip(FaceUp) · v</c>.
/// <c>Tip</c> is the fixed table of &#xA7;1.3 (<see cref="TipOf"/>); every row is a signed
/// permutation with determinant +1 — a rotation, never a mirror. <c>Rz</c> is
/// <see cref="Vector2.Rotate"/> applied to X and Y with Z untouched, so for a quarter turn every
/// component of the result is a component of the input, possibly negated: exact, with no
/// <see cref="double"/> anywhere. A <see cref="Rotation"/> that is not a quarter turn — reachable
/// only from the solver (&#xA7;3.2) — makes <see cref="IsExact"/> false; <see cref="Apply"/> and
/// <see cref="Unapply"/> then round X and Y exactly as <see cref="Vector2.Rotate"/> does, and the
/// axis-valued members (<see cref="Image"/>, <see cref="Normal"/>, <see cref="TurnedAbout"/>) throw,
/// because such an orientation sends a local axis to no world axis.
/// </para>
/// <para>
/// A positive quarter turn about a world axis follows the right-hand rule, the direction
/// <see cref="Vector2.Rotate"/> already turns about Z: about Z, X goes to Y; about X, Y goes to Z;
/// about Y, Z goes to X. With that convention <c>Tip(North)</c> is one turn about X ("tipped back"),
/// <c>Tip(West)</c> one turn about Y.
/// </para>
/// <para>
/// <c>default(Orientation)</c> is <c>(South, 0°)</c>, a real orientation but not the box as drawn;
/// that is <see cref="AsDrawn"/>.
/// </para>
/// </remarks>
/// <param name="FaceUp">The local face whose outward normal points to world +Z. <see cref="BoxFace.Top"/> is the box as drawn.</param>
/// <param name="Rotation">The spin about world Z, applied after the tip; the plan view's rotation.</param>
public readonly record struct Orientation(BoxFace FaceUp, Angle Rotation)
{
    private static readonly Axis[] Axes = [Axis.X, Axis.Y, Axis.Z];

    /// <summary>The box as drawn: top up, no spin. Every box before assembly-model is this.</summary>
    public static readonly Orientation AsDrawn = new(BoxFace.Top, Angle.Zero);

    /// <summary>
    /// Whether this is one of the 24 axis-aligned orientations — whether <see cref="Rotation"/> is a
    /// right-angle multiple. Every orientation the direct updater touches is.
    /// </summary>
    public bool IsExact => Rotation.IsRightAngleMultiple;

    /// <summary>
    /// The images of local +X, +Y and +Z under <c>Tip(faceUp)</c>: the table of
    /// docs/design/assembly-model.md &#xA7;1.3, pinned there and here, and nowhere else.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="faceUp"/> is not a box face.</exception>
    internal static ((Axis Axis, bool Positive) X, (Axis Axis, bool Positive) Y, (Axis Axis, bool Positive) Z) TipOf(BoxFace faceUp)
        => faceUp switch
        {
            //                  local +X →         local +Y →         local +Z →
            BoxFace.Top => ((Axis.X, true), (Axis.Y, true), (Axis.Z, true)),
            BoxFace.Bottom => ((Axis.X, true), (Axis.Y, false), (Axis.Z, false)),
            BoxFace.North => ((Axis.X, true), (Axis.Z, true), (Axis.Y, false)),
            BoxFace.South => ((Axis.X, true), (Axis.Z, false), (Axis.Y, true)),
            BoxFace.East => ((Axis.Z, true), (Axis.Y, true), (Axis.X, false)),
            BoxFace.West => ((Axis.Z, false), (Axis.Y, true), (Axis.X, true)),
            _ => throw new ArgumentOutOfRangeException(nameof(faceUp), faceUp, "Not a box face."),
        };

    /// <summary>Where a local axis points in the world, and which way. Exact.</summary>
    /// <exception cref="InvalidOperationException">This orientation is not <see cref="IsExact"/>.</exception>
    public (Axis Axis, bool Positive) Image(Axis local)
    {
        RequireExact();
        (Axis Axis, bool Positive) image = Tip(FaceUp, local);
        for (int turn = 0; turn < Rotation.QuarterTurns; turn++)
        {
            image = QuarterTurn(image, Axis.Z);
        }

        return image;
    }

    /// <summary>
    /// The world axis a local face is perpendicular to, and whether its outward normal points the
    /// positive way along it. Exact.
    /// </summary>
    /// <remarks>
    /// The outward normal of <see cref="BoxFace.East"/>, <see cref="BoxFace.North"/> and
    /// <see cref="BoxFace.Top"/> is local +X, +Y and +Z; of <see cref="BoxFace.West"/>,
    /// <see cref="BoxFace.South"/> and <see cref="BoxFace.Bottom"/>, local −X, −Y and −Z. So
    /// <c>Normal(FaceUp)</c> is always <c>(Z, true)</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">This orientation is not <see cref="IsExact"/>.</exception>
    public (Axis Axis, bool Positive) Normal(BoxFace face)
    {
        (Axis local, bool outwardPositive) = face switch
        {
            BoxFace.South => (Axis.Y, false),
            BoxFace.East => (Axis.X, true),
            BoxFace.North => (Axis.Y, true),
            BoxFace.West => (Axis.X, false),
            BoxFace.Bottom => (Axis.Z, false),
            BoxFace.Top => (Axis.Z, true),
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, "Not a box face."),
        };

        (Axis axis, bool positive) = Image(local);
        return (axis, positive == outwardPositive);
    }

    /// <summary>
    /// A local displacement in world terms: <c>Rz(Rotation) · Tip(FaceUp) · local</c>. Exact when
    /// <see cref="IsExact"/>; otherwise X and Y round as <see cref="Vector2.Rotate"/> does, and Z is exact.
    /// </summary>
    public Vector3 Apply(Vector3 local)
    {
        Vector3 tipped = ApplyTip(FaceUp, local);
        Vector2 spun = tipped.XY.Rotate(Rotation);
        return new Vector3(spun.Dx, spun.Dy, tipped.Dz);
    }

    /// <summary>
    /// A world displacement in local terms: the inverse of <see cref="Apply"/>. Exact when
    /// <see cref="IsExact"/>; otherwise X and Y round as <see cref="Vector2.Rotate"/> does.
    /// </summary>
    public Vector3 Unapply(Vector3 world)
    {
        Vector2 unspun = world.XY.Rotate(-Rotation);
        return UnapplyTip(FaceUp, new Vector3(unspun.Dx, unspun.Dy, world.Dz));
    }

    /// <summary>
    /// This orientation after <paramref name="quarterTurns"/> right-hand quarter turns about a world
    /// axis (negative turns the other way). Closed: the result is one of the 24, in its one spelling.
    /// Turning about Z is the only case that leaves <see cref="FaceUp"/> alone; it adds to
    /// <see cref="Rotation"/>.
    /// </summary>
    /// <remarks>
    /// Composes signed permutations on the world side — <c>Rot(axis, q) · Rz(Rotation) · Tip(FaceUp)</c>
    /// — and decomposes the product back into <c>(FaceUp, Rotation)</c>: the face whose normal
    /// lands on +Z, then the spin that remains once that face's tip is factored out, read off from
    /// where it sends world +X. Integer and enum arithmetic only.
    /// </remarks>
    /// <exception cref="InvalidOperationException">This orientation is not <see cref="IsExact"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is not an axis.</exception>
    public Orientation TurnedAbout(Axis axis, int quarterTurns)
    {
        RequireExact();
        if (axis is not (Axis.X or Axis.Y or Axis.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis.");
        }

        int turns = ((quarterTurns % 4) + 4) % 4;
        var images = new (Axis Axis, bool Positive)[3];
        for (int i = 0; i < 3; i++)
        {
            images[i] = Image(Axes[i]);
            for (int turn = 0; turn < turns; turn++)
            {
                images[i] = QuarterTurn(images[i], axis);
            }
        }

        // FaceUp: the local face whose outward normal now points to +Z.
        int up = Array.FindIndex(images, image => image.Axis == Axis.Z);
        BoxFace faceUp = (Axes[up], images[up].Positive) switch
        {
            (Axis.X, true) => BoxFace.East,
            (Axis.X, false) => BoxFace.West,
            (Axis.Y, true) => BoxFace.North,
            (Axis.Y, false) => BoxFace.South,
            (_, true) => BoxFace.Top,
            (_, false) => BoxFace.Bottom,
        };

        // What is left is a spin about Z, R = M · Tip(faceUp)⁻¹. It is fixed by where it sends
        // world +X: Tip(faceUp) sends some local ±k to +X, and M sends that same ±k to R(+X).
        (Axis Axis, bool Positive)[] tip = TipArray(faceUp);
        int k = Array.FindIndex(tip, image => image.Axis == Axis.X);
        (Axis Axis, bool Positive) spunX = (images[k].Axis, images[k].Positive == tip[k].Positive);

        int spin = spunX switch
        {
            (Axis.X, true) => 0,
            (Axis.Y, true) => 1,
            (Axis.X, false) => 2,
            (Axis.Y, false) => 3,
            _ => throw new InvalidOperationException($"Turning {this} about {axis} did not leave a spin about Z; the tip table is not a set of rotations."),
        };

        return new Orientation(faceUp, Angle.Right * spin);
    }

    /// <inheritdoc/>
    public override string ToString() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{FaceUp} up, {Rotation.ToDegrees()}°");

    // One right-hand quarter turn about `about`, on a signed axis. Cyclic order X → Y → Z → X: the
    // axis after `about` goes to the one after that, which goes to minus the first.
    private static (Axis Axis, bool Positive) QuarterTurn((Axis Axis, bool Positive) v, Axis about)
    {
        if (v.Axis == about)
        {
            return v;
        }

        Axis next = Next(about);
        return v.Axis == next ? (Next(next), v.Positive) : (next, !v.Positive);
    }

    private static Axis Next(Axis axis) => axis switch
    {
        Axis.X => Axis.Y,
        Axis.Y => Axis.Z,
        _ => Axis.X,
    };

    private static (Axis Axis, bool Positive) Tip(BoxFace faceUp, Axis local)
    {
        var tip = TipOf(faceUp);
        return local switch
        {
            Axis.X => tip.X,
            Axis.Y => tip.Y,
            Axis.Z => tip.Z,
            _ => throw new ArgumentOutOfRangeException(nameof(local), local, "Not an axis."),
        };
    }

    private static (Axis Axis, bool Positive)[] TipArray(BoxFace faceUp)
    {
        var tip = TipOf(faceUp);
        return [tip.X, tip.Y, tip.Z];
    }

    // Tip(faceUp) · v: each local component moved to its image axis, negated when the image is.
    private static Vector3 ApplyTip(BoxFace faceUp, Vector3 local)
    {
        (Axis Axis, bool Positive)[] tip = TipArray(faceUp);
        Vector3 world = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            Length component = local.Component(Axes[i]);
            world = world.WithComponent(tip[i].Axis, tip[i].Positive ? component : -component);
        }

        return world;
    }

    // Tip(faceUp)⁻¹ · v: the transpose, since a signed permutation is orthogonal.
    private static Vector3 UnapplyTip(BoxFace faceUp, Vector3 world)
    {
        (Axis Axis, bool Positive)[] tip = TipArray(faceUp);
        Vector3 local = Vector3.Zero;
        for (int i = 0; i < 3; i++)
        {
            Length component = world.Component(tip[i].Axis);
            local = local.WithComponent(Axes[i], tip[i].Positive ? component : -component);
        }

        return local;
    }

    private void RequireExact()
    {
        if (!IsExact)
        {
            throw new InvalidOperationException(
                $"{this} is not one of the 24 axis-aligned orientations: its rotation is not a quarter turn, so a local axis lands on no world axis.");
        }
    }
}
