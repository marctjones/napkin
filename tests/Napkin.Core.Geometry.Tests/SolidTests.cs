using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// <see cref="Box.Solid"/>, docs/design/assembly-model.md &#xA7;10 step 6: golden cases 14 and 18 of
/// &#xA7;9.1. Property P17 is in <see cref="PropertyTests"/>.
/// </summary>
/// <remarks>
/// Expected points are written independently of <see cref="Orientation"/>: &#xA7;1.3's tip table
/// typed out as coordinate formulas and the spin written as a swap-and-negate by hand, as
/// <see cref="BoxInSpaceTests"/> does.
/// </remarks>
public class SolidTests
{
    private static readonly Point3 CaseAnchor = new(new Length(1536), new Length(2048), new Length(4096));

    public static TheoryData<BoxFace, int> AllOrientations
    {
        get
        {
            TheoryData<BoxFace, int> data = [];
            foreach (BoxFace face in Enum.GetValues<BoxFace>())
            {
                for (int q = 0; q < 4; q++)
                {
                    data.Add(face, q);
                }
            }

            return data;
        }
    }

    // ---- Case 18: a plain box -------------------------------------------------------------------

    /// <summary>
    /// Case 18: a plain box's solid is six quads — the box's six faces, each carrying its
    /// <see cref="BoxFace"/>, each through the four <see cref="Box.Vertex"/> points of that face,
    /// winding outward — under all 24 orientations. So the one extrusion path gives, for a box
    /// with no cuts, exactly the six quads a renderer would build from the eight vertices without
    /// computing an outline.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case18_APlainBoxIsSixQuadsOneForEachFaceWindingOutward(BoxFace faceUp, int quarterTurns)
    {
        Box box = CaseBox(faceUp, quarterTurns);
        Point3 V(BoxCorner corner, BoxLevel level) => box.Vertex(corner, level);

        const BoxCorner SW = BoxCorner.SouthWest, SE = BoxCorner.SouthEast, NE = BoxCorner.NorthEast, NW = BoxCorner.NorthWest;
        const BoxLevel Lo = BoxLevel.Bottom, Hi = BoxLevel.Top;

        SolidFace[] expected =
        [
            Quad(BoxFace.Bottom, V(SW, Lo), V(NW, Lo), V(NE, Lo), V(SE, Lo)),
            Quad(BoxFace.Top, V(SW, Hi), V(SE, Hi), V(NE, Hi), V(NW, Hi)),
            Quad(BoxFace.South, V(SW, Lo), V(SE, Lo), V(SE, Hi), V(SW, Hi)),
            Quad(BoxFace.East, V(SE, Lo), V(NE, Lo), V(NE, Hi), V(SE, Hi)),
            Quad(BoxFace.North, V(NE, Lo), V(NW, Lo), V(NW, Hi), V(NE, Hi)),
            Quad(BoxFace.West, V(NW, Lo), V(SW, Lo), V(SW, Hi), V(NW, Hi)),
        ];

        Solid solid = box.Solid();
        Assert.Equal(new Solid([.. expected]), solid);

        // Outward: the right-hand normal of each quad is the face's outward normal turned by hand.
        foreach (SolidFace face in solid.Faces)
        {
            (long x, long y, long z) = OutwardLocal(face.Of!.Value);
            (long ex, long ey, long ez) = Turn(faceUp, quarterTurns, x, y, z);
            (Int128 nx, Int128 ny, Int128 nz) = NewellNormal(face);

            Assert.Equal(Math.Sign(ex), Int128.Sign(nx));
            Assert.Equal(Math.Sign(ey), Int128.Sign(ny));
            Assert.Equal(Math.Sign(ez), Int128.Sign(nz));
        }
    }

    // ---- Case 18: four rounded corners ----------------------------------------------------------

    /// <summary>
    /// Case 18 on the rounded-corner-table sample's top (samples/rounded-corner-table.scene.json):
    /// 48&#x2033; &#xD7; 24&#x2033; &#xD7; &#xBE;&#x2033;, its underside at 16640, each corner rounded to 1&#x2033;.
    /// </summary>
    /// <remarks>
    /// The outline has one straight run and one arc per edge — eight segments, four of each — so
    /// each cap has eight segments and there are eight sides, four quads and four curved patches.
    /// (&#xA7;9.1 case 18's text says "eight straight and four arc segments, twelve sides"; the
    /// extrusion rule of &#xA7;4.1, one side per outline segment, and <see cref="Box.Outline"/>
    /// give the counts asserted here.) Every X and Y is a multiple of 1024; Z is the underside
    /// 16640 (16&#xBC;&#x2033;, a multiple of 256) or the top 17408.
    /// </remarks>
    [Fact]
    public void Case18_TheRoundedCornerTableTopIsTwoCapsOfFourRunsAndFourArcsAndEightSides()
    {
        Box top = new(
            SketchBuilder.EntityIdAt(1), LayerId.Default, new Point3(Length.Zero, Length.Zero, new Length(16640)),
            new Length(49152), new Length(24576), new Length(768), BoxFace.Top, Angle.Zero)
        {
            Cuts =
            [
                new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                new RoundedCorner(BoxCorner.SouthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthWest, new Length(1024)),
            ],
        };

        const long Lo = 16640, Hi = 17408;
        static Point3 P(long x, long y, long z) => new(new Length(x), new Length(y), new Length(z));
        static StraightSegment3 Run(long x0, long y0, long x1, long y1, long z) => new(P(x0, y0, z), P(x1, y1, z));
        static ArcByCenter3 Arc(long x0, long y0, long x1, long y1, long cx, long cy, long z) => new(P(x0, y0, z), P(x1, y1, z), P(cx, cy, z));

        SolidSegment[] topCap =
        [
            Run(1024, 0, 48128, 0, Hi),
            Arc(48128, 0, 49152, 1024, 48128, 1024, Hi),
            Run(49152, 1024, 49152, 23552, Hi),
            Arc(49152, 23552, 48128, 24576, 48128, 23552, Hi),
            Run(48128, 24576, 1024, 24576, Hi),
            Arc(1024, 24576, 0, 23552, 1024, 23552, Hi),
            Run(0, 23552, 0, 1024, Hi),
            Arc(0, 1024, 1024, 0, 1024, 1024, Hi),
        ];

        SolidSegment[] bottomCap =
        [
            Arc(1024, 0, 0, 1024, 1024, 1024, Lo),
            Run(0, 1024, 0, 23552, Lo),
            Arc(0, 23552, 1024, 24576, 1024, 23552, Lo),
            Run(1024, 24576, 48128, 24576, Lo),
            Arc(48128, 24576, 49152, 23552, 48128, 23552, Lo),
            Run(49152, 23552, 49152, 1024, Lo),
            Arc(49152, 1024, 48128, 0, 48128, 1024, Lo),
            Run(48128, 0, 1024, 0, Lo),
        ];

        Solid solid = top.Solid();

        Assert.Equal(10, solid.Faces.Length);
        Assert.Equal(new SolidFace(BoxFace.Bottom, [.. bottomCap]), solid.Faces[0]);
        Assert.Equal(new SolidFace(BoxFace.Top, [.. topCap]), solid.Faces[1]);

        ImmutableArray<SolidFace> sides = solid.Faces[2..];
        Assert.Equal(
            new BoxFace?[] { BoxFace.South, null, BoxFace.East, null, BoxFace.North, null, BoxFace.West, null },
            sides.Select(side => side.Of));

        // The south-east corner's patch: its arc at the underside, a ruling up, the same arc back
        // at the top, a ruling down.
        Assert.Equal(
            new SolidFace(null,
            [
                Arc(48128, 0, 49152, 1024, 48128, 1024, Lo),
                new StraightSegment3(P(49152, 1024, Lo), P(49152, 1024, Hi)),
                Arc(49152, 1024, 48128, 0, 48128, 1024, Hi),
                new StraightSegment3(P(48128, 0, Hi), P(48128, 0, Lo)),
            ]),
            sides[1]);

        // The east side: a quad of the straight run between the two eastern arcs.
        Assert.Equal(
            Quad(BoxFace.East, P(49152, 1024, Lo), P(49152, 23552, Lo), P(49152, 23552, Hi), P(49152, 1024, Hi)),
            sides[2]);

        foreach (SolidFace side in sides)
        {
            Assert.Equal(4, side.Boundary.Length);
            Assert.Equal(side.Of is null ? 2 : 0, side.Boundary.Count(segment => segment is ArcByCenter3));
        }

        foreach (Point3 point in solid.Faces.SelectMany(face => face.Boundary).SelectMany(PointsOf))
        {
            Assert.Equal(0, point.X.Units % 1024);
            Assert.Equal(0, point.Y.Units % 1024);
            Assert.Contains(point.Z.Units, new[] { Lo, Hi });
        }
    }

    // ---- Case 18: a full mitre ------------------------------------------------------------------

    /// <summary>
    /// Case 18: a rail 36&#x2033; &#xD7; 2&#x2033; mitred across its whole east end — the cut runs from
    /// 2&#x2033; in along the south edge to the north-east corner. The mitre face has <c>Of</c>
    /// <see langword="null"/>, and there is no east face at all: the cut took the whole of it.
    /// </summary>
    [Fact]
    public void Case18_AFullMitreFaceIsOfNoBoxFaceAndLeavesNoEastSide()
    {
        Box rail = new(
            SketchBuilder.EntityIdAt(1), LayerId.Default, Point3.Origin,
            Length.Inches(36), Length.Inches(2), Length.Inches(1), BoxFace.Top, Angle.Zero)
        {
            Cuts = [new CornerCut(BoxCorner.SouthEast, Length.Inches(2), Length.Inches(2))],
        };

        Solid solid = rail.Solid();
        ImmutableArray<SolidFace> sides = solid.Faces[2..];

        Assert.Equal(new BoxFace?[] { BoxFace.South, null, BoxFace.North, BoxFace.West }, sides.Select(side => side.Of));
        Assert.Equal(
            Quad(null, Point3.Inches(34, 0, 0), Point3.Inches(36, 2, 0), Point3.Inches(36, 2, 1), Point3.Inches(34, 0, 1)),
            sides[1]);
    }

    // ---- Case 14 --------------------------------------------------------------------------------

    /// <summary>
    /// Case 14: <see cref="SetOrientation"/> on a box with cuts — a corner cut, a rounded corner and
    /// an inward curve across an odd-unit width, so the curve's middle is the outline's one
    /// half-unit rounding. The local outline is unchanged, and the solid's caps are that outline
    /// lifted to z = 0 and z = depth and turned, vertex for vertex, for all 24 orientations.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case14_TurningABoxWithCutsLeavesItsOutlineAndTurnsItsCaps(BoxFace faceUp, int quarterTurns)
    {
        Box drawn = new(
            SketchBuilder.EntityIdAt(1), LayerId.Default, CaseAnchor,
            new Length(49153), Length.Inches(24), new Length(768), BoxFace.Top, Angle.Zero)
        {
            Cuts =
            [
                new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(5)),
                new RoundedCorner(BoxCorner.SouthEast, Length.Inches(2)),
                new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(2)),
            ],
        };

        Assert.True(Sketch.Empty.WithEntity(drawn).Validate().IsValid);

        UpdateResult result = DirectUpdater.Instance.Apply(
            Sketch.Empty.WithEntity(drawn),
            new SetOrientation(drawn.Id, faceUp, Angle.Right * quarterTurns));
        Box turned = Assert.IsType<Box>(Assert.IsType<Solved>(result).Sketch.Find(drawn.Id));

        Assert.Equal(CaseAnchor, turned.Anchor);
        Assert.Equal(drawn.Outline(), turned.Outline());

        ImmutableArray<OutlineSegment> outline = turned.Outline().Segments;
        Point3 At(Point2 local, long z)
        {
            (long x, long y, long tz) = Turn(faceUp, quarterTurns, local.X.Units, local.Y.Units, z);
            return new Point3(
                new Length(CaseAnchor.X.Units + x),
                new Length(CaseAnchor.Y.Units + y),
                new Length(CaseAnchor.Z.Units + tz));
        }

        SolidSegment Lift(OutlineSegment segment, long z, bool reversed) => (segment, reversed) switch
        {
            (StraightSegment s, false) => new StraightSegment3(At(s.From, z), At(s.To, z)),
            (StraightSegment s, true) => new StraightSegment3(At(s.To, z), At(s.From, z)),
            (ArcByCenter a, false) => new ArcByCenter3(At(a.From, z), At(a.To, z), At(a.Center, z)),
            (ArcByCenter a, true) => new ArcByCenter3(At(a.To, z), At(a.From, z), At(a.Center, z)),
            (ArcThrough a, false) => new ArcThrough3(At(a.From, z), At(a.Through, z), At(a.To, z)),
            (ArcThrough a, true) => new ArcThrough3(At(a.To, z), At(a.Through, z), At(a.From, z)),
            _ => throw new InvalidOperationException(),
        };

        SolidFace bottom = new(BoxFace.Bottom, [.. outline.Reverse().Select(segment => Lift(segment, 0, reversed: true))]);
        SolidFace top = new(BoxFace.Top, [.. outline.Select(segment => Lift(segment, 768, reversed: false))]);

        Solid solid = turned.Solid();
        Assert.Equal(bottom, solid.Faces[0]);
        Assert.Equal(top, solid.Faces[1]);

        // All three cut kinds are in it, so every segment kind was placed.
        Assert.Contains(solid.Faces[1].Boundary, segment => segment is ArcByCenter3);
        Assert.Contains(solid.Faces[1].Boundary, segment => segment is ArcThrough3);
        Assert.Equal(outline.Length + 2, solid.Faces.Length);
    }

    /// <summary>
    /// A rotation that is not a quarter turn — only the solver reaches one — still gives a closed
    /// solid, each point rounded as <see cref="Box.Vertex"/> rounds it.
    /// </summary>
    [Fact]
    public void ABoxSpunOffTheRightAnglesGivesASolidThroughItsRoundedVertices()
    {
        Box box = Box.AsDrawn(
            SketchBuilder.EntityIdAt(1), LayerId.Default, Point2.Inches(10, 20),
            Length.Inches(30), Length.Inches(8), Length.Inches(2), Angle.Degrees(45));

        Solid solid = box.Solid();

        Assert.Equal(6, solid.Faces.Length);
        Assert.Equal(box.Vertex(BoxCorner.NorthEast, BoxLevel.Top), solid.Faces[1].Boundary[2].From);
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static Box CaseBox(BoxFace faceUp, int quarterTurns) => new(
        SketchBuilder.EntityIdAt(1),
        LayerId.Default,
        CaseAnchor,
        Length.Inches(48),
        Length.Inches(24),
        new Length(768),
        faceUp,
        Angle.Right * quarterTurns);

    private static SolidFace Quad(BoxFace? of, Point3 a, Point3 b, Point3 c, Point3 d)
        => new(of, [new StraightSegment3(a, b), new StraightSegment3(b, c), new StraightSegment3(c, d), new StraightSegment3(d, a)]);

    /// <summary>§1.3's Tip(FaceUp) then the spin, typed out by hand.</summary>
    private static (long X, long Y, long Z) Turn(BoxFace faceUp, int quarterTurns, long x, long y, long z)
    {
        (long tx, long ty, long tz) = faceUp switch
        {
            BoxFace.Top => (x, y, z),        // +X, +Y, +Z
            BoxFace.Bottom => (x, -y, -z),   // +X, -Y, -Z
            BoxFace.North => (x, -z, y),     // +X, +Z, -Y
            BoxFace.South => (x, z, -y),     // +X, -Z, +Y
            BoxFace.East => (-z, y, x),      // +Z, +Y, -X
            BoxFace.West => (z, y, -x),      // -Z, +Y, +X
            _ => throw new ArgumentOutOfRangeException(nameof(faceUp)),
        };

        (long sx, long sy) = quarterTurns switch
        {
            0 => (tx, ty),
            1 => (-ty, tx),
            2 => (-tx, -ty),
            _ => (ty, -tx),
        };

        return (sx, sy, tz);
    }

    /// <summary>A face's outward normal in the local frame.</summary>
    private static (long X, long Y, long Z) OutwardLocal(BoxFace face) => face switch
    {
        BoxFace.South => (0, -1, 0),
        BoxFace.East => (1, 0, 0),
        BoxFace.North => (0, 1, 0),
        BoxFace.West => (-1, 0, 0),
        BoxFace.Bottom => (0, 0, -1),
        _ => (0, 0, 1),
    };

    /// <summary>Every point a segment carries: its ends, and an arc's centre or third point.</summary>
    internal static IEnumerable<Point3> PointsOf(SolidSegment segment)
    {
        yield return segment.From;
        yield return segment.To;
        switch (segment)
        {
            case ArcByCenter3 arc:
                yield return arc.Center;
                break;
            case ArcThrough3 arc:
                yield return arc.Through;
                break;
        }
    }

    /// <summary>
    /// Newell's normal of a face's boundary polygon — each segment's start, and an arc through
    /// three points' middle point — which points the way the boundary winds by the right-hand
    /// rule. Twice the area, so exact in integers.
    /// </summary>
    internal static (Int128 X, Int128 Y, Int128 Z) NewellNormal(SolidFace face)
    {
        List<Point3> points = [];
        foreach (SolidSegment segment in face.Boundary)
        {
            points.Add(segment.From);
            if (segment is ArcThrough3 arc)
            {
                points.Add(arc.Through);
            }
        }

        Int128 nx = 0, ny = 0, nz = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Point3 a = points[i];
            Point3 b = points[(i + 1) % points.Count];
            nx += (Int128)(a.Y.Units - b.Y.Units) * (a.Z.Units + b.Z.Units);
            ny += (Int128)(a.Z.Units - b.Z.Units) * (a.X.Units + b.X.Units);
            nz += (Int128)(a.X.Units - b.X.Units) * (a.Y.Units + b.Y.Units);
        }

        return (nx, ny, nz);
    }
}
