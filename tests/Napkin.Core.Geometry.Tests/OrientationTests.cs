namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Golden cases 1 to 4 of docs/design/assembly-model.md &#xA7;9.1 and property P14 of &#xA7;9.2:
/// the tip table, the 24 orientations, closure under quarter turns, and exact round trips.
/// </summary>
/// <remarks>
/// Every expectation here is written independently of <see cref="Orientation"/>'s own code: the
/// table is typed out from &#xA7;1.3, and the world-axis quarter turn <see cref="Rot"/> is plain
/// coordinate formulas, not the implementation's signed-axis arithmetic.
/// </remarks>
public class OrientationTests
{
    private static readonly Axis[] Axes = [Axis.X, Axis.Y, Axis.Z];

    private static readonly BoxFace[] Faces = Enum.GetValues<BoxFace>();

    /// <summary>All 24 orientations, (face-up, quarter turns).</summary>
    public static IEnumerable<Orientation> All
        => Faces.SelectMany(face => Enumerable.Range(0, 4).Select(q => new Orientation(face, Angle.Right * q)));

    // §1.3's table, character for character: FaceUp → images of local +X, +Y, +Z.
    public static TheoryData<BoxFace, string, string, string> TipTable => new()
    {
        { BoxFace.Top, "+X", "+Y", "+Z" },
        { BoxFace.Bottom, "+X", "-Y", "-Z" },
        { BoxFace.North, "+X", "+Z", "-Y" },
        { BoxFace.South, "+X", "-Z", "+Y" },
        { BoxFace.East, "+Z", "+Y", "-X" },
        { BoxFace.West, "-Z", "+Y", "+X" },
    };

    // ---- Case 1: the tip table --------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TipTable))]
    public void Case1_ImageOfEachFaceUpIsTheTable(BoxFace faceUp, string x, string y, string z)
    {
        Orientation o = new(faceUp, Angle.Zero);

        Assert.Equal(Parse(x), o.Image(Axis.X));
        Assert.Equal(Parse(y), o.Image(Axis.Y));
        Assert.Equal(Parse(z), o.Image(Axis.Z));

        // The internal table the implementation reads is the same table.
        var tip = Orientation.TipOf(faceUp);
        Assert.Equal(Parse(x), tip.X);
        Assert.Equal(Parse(y), tip.Y);
        Assert.Equal(Parse(z), tip.Z);
    }

    [Theory]
    [MemberData(nameof(TipTable))]
    public void Case1_EveryRowIsARotationNeverAMirror(BoxFace faceUp, string x, string y, string z)
    {
        _ = (x, y, z);
        Orientation o = new(faceUp, Angle.Zero);

        int[,] m = MatrixOf(o);
        Assert.Equal(1, Determinant(m));

        // And it is a permutation: each world axis is hit exactly once.
        Assert.Equal(3, Axes.Select(a => o.Image(a).Axis).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(TipTable))]
    public void Case1_TheFaceUpPointsUp(BoxFace faceUp, string x, string y, string z)
    {
        _ = (x, y, z);
        foreach (int q in Enumerable.Range(0, 4))
        {
            Orientation o = new(faceUp, Angle.Right * q);
            Assert.Equal((Axis.Z, true), o.Normal(faceUp));
            Assert.Equal((Axis.Z, false), o.Normal(BoxFeature.Opposite(faceUp)));
            Assert.Equal(1, Determinant(MatrixOf(o)));
        }
    }

    [Fact]
    public void Case1_TheTableSaysWhatTheHandDoes()
    {
        // "tipped back: the north face is up, the drawn top faces south"
        Orientation north = new(BoxFace.North, Angle.Zero);
        Assert.Equal((Axis.Y, false), north.Normal(BoxFace.Top));

        // "tipped forward: the south face is up, the drawn top faces north"
        Assert.Equal((Axis.Y, true), new Orientation(BoxFace.South, Angle.Zero).Normal(BoxFace.Top));

        // "tipped over westward: the east face is up, the drawn top faces west"
        Assert.Equal((Axis.X, false), new Orientation(BoxFace.East, Angle.Zero).Normal(BoxFace.Top));

        // "tipped over eastward: the west face is up, the drawn top faces east"
        Assert.Equal((Axis.X, true), new Orientation(BoxFace.West, Angle.Zero).Normal(BoxFace.Top));

        // "turned over, end for end, east and west kept"
        Orientation bottom = new(BoxFace.Bottom, Angle.Zero);
        Assert.Equal((Axis.X, true), bottom.Normal(BoxFace.East));
        Assert.Equal((Axis.Z, false), bottom.Normal(BoxFace.Top));

        // As drawn, every face points where its name says.
        Orientation drawn = Orientation.AsDrawn;
        Assert.Equal((Axis.Y, false), drawn.Normal(BoxFace.South));
        Assert.Equal((Axis.X, true), drawn.Normal(BoxFace.East));
        Assert.Equal((Axis.Y, true), drawn.Normal(BoxFace.North));
        Assert.Equal((Axis.X, false), drawn.Normal(BoxFace.West));
        Assert.Equal((Axis.Z, false), drawn.Normal(BoxFace.Bottom));
        Assert.Equal((Axis.Z, true), drawn.Normal(BoxFace.Top));
    }

    [Fact]
    public void ImageIncludesTheSpinAsVector2RotateTurns()
    {
        // Rz(90°) is Vector2.Rotate's (x, y) → (−y, x): +X lands on +Y, +Y on −X.
        Orientation spun = new(BoxFace.Top, Angle.Right);
        Assert.Equal((Axis.Y, true), spun.Image(Axis.X));
        Assert.Equal((Axis.X, false), spun.Image(Axis.Y));
        Assert.Equal((Axis.Z, true), spun.Image(Axis.Z));

        // East up, then a half turn: local +Z went to −X, and the spin sends −X to +X.
        Assert.Equal((Axis.X, true), new Orientation(BoxFace.East, Angle.Straight).Image(Axis.Z));
    }

    [Fact]
    public void NormalAgreesWithApplyOnEveryFaceOfEveryOrientation()
    {
        (BoxFace Face, Vector3 Outward)[] outward =
        [
            (BoxFace.South, new Vector3(Length.Zero, new Length(-1), Length.Zero)),
            (BoxFace.East, new Vector3(new Length(1), Length.Zero, Length.Zero)),
            (BoxFace.North, new Vector3(Length.Zero, new Length(1), Length.Zero)),
            (BoxFace.West, new Vector3(new Length(-1), Length.Zero, Length.Zero)),
            (BoxFace.Bottom, new Vector3(Length.Zero, Length.Zero, new Length(-1))),
            (BoxFace.Top, new Vector3(Length.Zero, Length.Zero, new Length(1))),
        ];

        foreach (Orientation o in All)
        {
            foreach ((BoxFace face, Vector3 normal) in outward)
            {
                (Axis axis, bool positive) = o.Normal(face);
                Assert.Equal(Vector3.Along(axis, new Length(positive ? 1 : -1)), o.Apply(normal));
            }
        }
    }

    // ---- Case 2: 24 distinct orientations ----------------------------------------------------

    [Fact]
    public void Case2_The24OrientationsAreDistinct()
    {
        Orientation[] all = [.. All];
        Assert.Equal(24, all.Length);
        Assert.Equal(24, all.Distinct().Count());

        HashSet<(Vector3, Vector3, Vector3)> images = [.. all.Select(o => (o.Apply(Unit(Axis.X)), o.Apply(Unit(Axis.Y)), o.Apply(Unit(Axis.Z))))];
        Assert.Equal(24, images.Count);

        // And there are exactly 24 proper signed permutations, so these are all of them.
        Assert.All(all, o => Assert.Equal(1, Determinant(MatrixOf(o))));
    }

    // ---- Case 3: closure --------------------------------------------------------------------

    [Fact]
    public void Case3_TurnedAboutIsClosedAndMatchesTheWorldTurn()
    {
        HashSet<Orientation> all = [.. All];

        foreach (Orientation o in All)
        {
            foreach (Axis axis in Axes)
            {
                foreach (int q in new[] { 1, 2, 3 })
                {
                    Orientation turned = o.TurnedAbout(axis, q);
                    string because = $"{o} turned {q} about {axis} gave {turned}";

                    // One of the 24, in its one spelling.
                    Assert.True(all.Contains(turned), because);
                    Assert.True(turned.IsExact, because);

                    // Applying it equals turning the original's images.
                    foreach (Axis local in Axes)
                    {
                        Assert.True(Rot(axis, q, o.Apply(Unit(local))) == turned.Apply(Unit(local)), because);
                    }
                }

                // Four turns about any axis is the identity, in one call and in four.
                Assert.Equal(o, o.TurnedAbout(axis, 4));
                Assert.Equal(o, o.TurnedAbout(axis, 0));
                Assert.Equal(o, o.TurnedAbout(axis, 1).TurnedAbout(axis, 1).TurnedAbout(axis, 1).TurnedAbout(axis, 1));
            }
        }
    }

    [Fact]
    public void Case3_TurningAboutZLeavesFaceUpAloneAndAddsToRotation()
    {
        foreach (Orientation o in All)
        {
            foreach (int q in new[] { 1, 2, 3 })
            {
                Orientation turned = o.TurnedAbout(Axis.Z, q);
                Assert.Equal(o.FaceUp, turned.FaceUp);
                Assert.Equal(o.Rotation + Angle.Right * q, turned.Rotation);
            }
        }
    }

    [Fact]
    public void Case3_OnlyZLeavesFaceUpAlone()
    {
        foreach (Orientation o in All)
        {
            foreach (Axis axis in new[] { Axis.X, Axis.Y })
            {
                // A single quarter turn about a horizontal axis always changes which face is up.
                Assert.NotEqual(o.FaceUp, o.TurnedAbout(axis, 1).FaceUp);
            }
        }
    }

    [Fact]
    public void Case3_TheTipsAreTurnsOfTheBoxAsDrawn()
    {
        // With the right-hand convention (the direction Vector2.Rotate turns about Z), each row of
        // the table is a turn of the box as drawn about a horizontal world axis.
        Orientation drawn = Orientation.AsDrawn;
        Assert.Equal(new Orientation(BoxFace.North, Angle.Zero), drawn.TurnedAbout(Axis.X, 1));
        Assert.Equal(new Orientation(BoxFace.Bottom, Angle.Zero), drawn.TurnedAbout(Axis.X, 2));
        Assert.Equal(new Orientation(BoxFace.South, Angle.Zero), drawn.TurnedAbout(Axis.X, 3));
        Assert.Equal(new Orientation(BoxFace.West, Angle.Zero), drawn.TurnedAbout(Axis.Y, 1));
        Assert.Equal(new Orientation(BoxFace.East, Angle.Zero), drawn.TurnedAbout(Axis.Y, 3));

        // A half turn about Y also turns the box over, but end for end the other way: that is
        // Bottom-up spun a half turn, not Bottom-up as the table spells it.
        Assert.Equal(new Orientation(BoxFace.Bottom, Angle.Straight), drawn.TurnedAbout(Axis.Y, 2));

        // Negative turns are the other direction.
        Assert.Equal(new Orientation(BoxFace.South, Angle.Zero), drawn.TurnedAbout(Axis.X, -1));
        Assert.Equal(drawn.TurnedAbout(Axis.Z, 3), drawn.TurnedAbout(Axis.Z, -1));
        Assert.Equal(new Orientation(BoxFace.Top, Angle.Degrees(270)), drawn.TurnedAbout(Axis.Z, -5));
    }

    // ---- Case 4: exact round trips ----------------------------------------------------------

    [Fact]
    public void Case4_ApplyThenUnapplyIsTheIdentity()
    {
        Vector3[] vectors =
        [
            // (W, H, D) of a 48″ × 24″ × ¾″ top.
            new(Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4)),
            // (−1, 2, −3) in units.
            new(new Length(-1), new Length(2), new Length(-3)),
        ];

        foreach (Orientation o in All)
        {
            foreach (Vector3 v in vectors)
            {
                Assert.Equal(v, o.Unapply(o.Apply(v)));
                Assert.Equal(v, o.Apply(o.Unapply(v)));

                // A signed permutation: the world vector is the local one's components, moved and
                // possibly negated — never a new magnitude.
                Vector3 w = o.Apply(v);
                long[] local = [.. Axes.Select(a => Math.Abs(v.Component(a).Units))];
                long[] world = [.. Axes.Select(a => Math.Abs(w.Component(a).Units))];
                Assert.Equal(local.Order(), world.Order());
            }
        }
    }

    [Fact]
    public void Case4_TheExactPathNeverTouchesDouble()
    {
        // The sentinel for "no Length is built through FromInches on the exact path": a component
        // of 2^53 + 1 units is not representable as a double, whether in units or in inches, so any
        // detour through double — Length.ToInches, FromInches, Math — would lose the odd unit. The
        // implementation reaches double only through Vector2.Rotate's non-right-angle branch, which
        // a quarter turn never takes; this proves it at runtime for all 24.
        long big = (1L << 53) + 1;
        Assert.NotEqual(big, (long)(double)big);

        Vector3 v = new(new Length(big), new Length(-big + 2), new Length(big + 2));
        foreach (Orientation o in All)
        {
            Vector3 w = o.Apply(v);
            long[] expected = [.. Axes.Select(a => Math.Abs(v.Component(a).Units)).Order()];
            Assert.Equal(expected, Axes.Select(a => Math.Abs(w.Component(a).Units)).Order());
            Assert.Equal(v, o.Unapply(w));
        }
    }

    // ---- The non-exact path (§3.2) ----------------------------------------------------------

    [Fact]
    public void ANonQuarterTurnRotationIsNotExactAndRoundsOnlyXAndY()
    {
        Orientation tilted = new(BoxFace.North, Angle.Degrees(45));
        Assert.False(tilted.IsExact);
        Assert.True(Orientation.AsDrawn.IsExact);

        // Tip first (exact), then Vector2.Rotate on X and Y, Z carried through untouched.
        Vector3 local = new(Length.Inches(10), Length.Inches(3), Length.Inches(2));
        Vector3 tipped = new Orientation(BoxFace.North, Angle.Zero).Apply(local);
        Vector2 spun = tipped.XY.Rotate(Angle.Degrees(45));
        Assert.Equal(new Vector3(spun.Dx, spun.Dy, tipped.Dz), tilted.Apply(local));

        // Unapply undoes the spin through the same rounding, then the tip exactly.
        Vector3 world = new(Length.Inches(4), Length.Inches(4), Length.Inches(7));
        Vector2 unspun = world.XY.Rotate(-Angle.Degrees(45));
        Assert.Equal(
            new Orientation(BoxFace.North, Angle.Zero).Unapply(new Vector3(unspun.Dx, unspun.Dy, world.Dz)),
            tilted.Unapply(world));

        // No local axis lands on a world axis, so the axis-valued members refuse.
        Assert.Throws<InvalidOperationException>(() => tilted.Image(Axis.X));
        Assert.Throws<InvalidOperationException>(() => tilted.Normal(BoxFace.Top));
        Assert.Throws<InvalidOperationException>(() => tilted.TurnedAbout(Axis.Z, 1));
    }

    [Fact]
    public void UndefinedValuesAreRefused()
    {
        Orientation drawn = Orientation.AsDrawn;
        Assert.Throws<ArgumentOutOfRangeException>(() => drawn.Image((Axis)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => drawn.Normal((BoxFace)6));
        Assert.Throws<ArgumentOutOfRangeException>(() => drawn.TurnedAbout((Axis)3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Orientation((BoxFace)6, Angle.Zero).Apply(Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Orientation.TipOf((BoxFace)(-1)));
    }

    [Fact]
    public void DefaultAndToString()
    {
        Assert.Equal(new Orientation(BoxFace.Top, Angle.Zero), Orientation.AsDrawn);
        Assert.Equal(new Orientation(BoxFace.South, Angle.Zero), default(Orientation));
        Assert.Equal("East up, 90°", new Orientation(BoxFace.East, Angle.Right).ToString());
    }

    // ---- P14: orientation is a group action -------------------------------------------------

    /// <summary>The seeds P14 runs against. A failure names the one to rerun.</summary>
    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 8).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P14_OrientationIsAGroupAction(int seed)
    {
        // Seeded loops in the style of PropertyTests: exhaustive over the finite part (24
        // orientations, 3 axes, turn counts −5 to 5), random over the vectors.
        Random random = new(seed);
        Orientation[] all = [.. All];

        foreach (Orientation o in all)
        {
            foreach (Axis axis in Axes)
            {
                for (int q = -5; q <= 5; q++)
                {
                    Vector3 v = new(NextLength(random), NextLength(random), NextLength(random));
                    Orientation turned = o.TurnedAbout(axis, q);
                    string because = $"seed {seed}: {o} turned {q} about {axis}, v = {v}";

                    // Turning the orientation is turning its image in the world.
                    Assert.True(turned.Apply(v) == Rot(axis, q, o.Apply(v)), because);

                    // Invertible: turning back recovers it exactly.
                    Assert.True(turned.TurnedAbout(axis, -q) == o, because);

                    // Composition: two turns about one axis are one turn by the sum.
                    int r = random.Next(-4, 5);
                    Assert.True(turned.TurnedAbout(axis, r) == o.TurnedAbout(axis, q + r), because);
                }
            }
        }
    }

    // ---- Helpers, independent of the implementation -----------------------------------------

    private static (Axis Axis, bool Positive) Parse(string signedAxis)
        => (Enum.Parse<Axis>(signedAxis[1..]), signedAxis[0] == '+');

    private static Vector3 Unit(Axis axis) => Vector3.Along(axis, new Length(1));

    // The world quarter turn by the right-hand rule, as coordinate formulas:
    //   about X: (x, y, z) → (x, −z, y);  about Y: (x, y, z) → (z, y, −x);  about Z: (x, y, z) → (−y, x, z).
    private static Vector3 Rot(Axis axis, int quarterTurns, Vector3 v)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;
        for (int i = 0; i < turns; i++)
        {
            v = axis switch
            {
                Axis.X => new Vector3(v.Dx, -v.Dz, v.Dy),
                Axis.Y => new Vector3(v.Dz, v.Dy, -v.Dx),
                _ => new Vector3(-v.Dy, v.Dx, v.Dz),
            };
        }

        return v;
    }

    // The matrix whose column j is the image of local axis j, from Image alone.
    private static int[,] MatrixOf(Orientation o)
    {
        int[,] m = new int[3, 3];
        for (int j = 0; j < 3; j++)
        {
            (Axis axis, bool positive) = o.Image(Axes[j]);
            m[(int)axis, j] = positive ? 1 : -1;
        }

        return m;
    }

    private static int Determinant(int[,] m)
        => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
         - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
         + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

    private static Length NextLength(Random random) => new(random.NextInt64(-1L << 40, 1L << 40));
}
