namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Golden case 7 of docs/design/assembly-model.md &#xA7;9.1, for the parts of it step 1 owns:
/// <see cref="BoxFeature"/>'s one spelling, its refusals, and the local uprights. (The
/// footprint half of case 7 is step 2's.)
/// </summary>
public class BoxFeatureTests
{
    private static readonly BoxFace[] AllFaces = Enum.GetValues<BoxFace>();

    private static readonly (BoxFace A, BoxFace B)[] OppositePairs =
    [
        (BoxFace.South, BoxFace.North),
        (BoxFace.East, BoxFace.West),
        (BoxFace.Bottom, BoxFace.Top),
    ];

    [Fact]
    public void BoxFaceOrderIsTheDocumentedOne()
    {
        // The declaration order is the file's spelling of a feature; pin it.
        Assert.Equal(
            [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top],
            AllFaces);
        Assert.Equal([BoxLevel.Bottom, BoxLevel.Top], Enum.GetValues<BoxLevel>());
    }

    [Fact]
    public void AFaceIsItselfWithDimensionTwo()
    {
        foreach (BoxFace face in AllFaces)
        {
            BoxFeature feature = BoxFeature.Face(face);
            Assert.Equal([face], feature.Faces);
            Assert.Equal(2, feature.Dimension);
        }
    }

    [Fact]
    public void EveryEdgeIsTwoAdjacentFacesInBoxFaceOrderWithOneSpelling()
    {
        int edges = 0;
        foreach (BoxFace a in AllFaces)
        {
            foreach (BoxFace b in AllFaces)
            {
                if (a == b || IsOppositePair(a, b))
                {
                    Assert.Throws<ArgumentException>(() => BoxFeature.Edge(a, b));
                    continue;
                }

                BoxFeature edge = BoxFeature.Edge(a, b);
                Assert.Equal(edge, BoxFeature.Edge(b, a));
                Assert.Equal(edge.GetHashCode(), BoxFeature.Edge(b, a).GetHashCode());
                Assert.Equal(1, edge.Dimension);
                Assert.Equal(a < b ? [a, b] : new[] { b, a }, edge.Faces);
                edges++;
            }
        }

        // Ordered pairs: 12 edges, each spelled twice.
        Assert.Equal(24, edges);
    }

    [Fact]
    public void EveryVertexIsThreeMutuallyAdjacentFacesInBoxFaceOrderWithOneSpelling()
    {
        HashSet<BoxFeature> vertices = [];
        int refused = 0;
        foreach (BoxFace a in AllFaces)
        {
            foreach (BoxFace b in AllFaces)
            {
                foreach (BoxFace c in AllFaces)
                {
                    bool legal = a != b && b != c && a != c
                        && !IsOppositePair(a, b) && !IsOppositePair(b, c) && !IsOppositePair(a, c);
                    if (!legal)
                    {
                        Assert.Throws<ArgumentException>(() => BoxFeature.Vertex(a, b, c));
                        refused++;
                        continue;
                    }

                    BoxFeature vertex = BoxFeature.Vertex(a, b, c);
                    Assert.Equal(0, vertex.Dimension);
                    Assert.Equal(new[] { a, b, c }.Order(), vertex.Faces);
                    vertices.Add(vertex);
                }
            }
        }

        Assert.Equal(8, vertices.Count);
        Assert.Equal(6 * 6 * 6 - 8 * 6, refused);
    }

    [Fact]
    public void NamedRefusalsFromTheDesign()
    {
        Assert.Throws<ArgumentException>(() => BoxFeature.Edge(BoxFace.South, BoxFace.North));
        Assert.Throws<ArgumentException>(() => BoxFeature.Edge(BoxFace.Top, BoxFace.Bottom));
        Assert.Throws<ArgumentException>(() => BoxFeature.Vertex(BoxFace.South, BoxFace.North, BoxFace.Top));
        Assert.Throws<ArgumentException>(() => BoxFeature.Vertex(BoxFace.East, BoxFace.South, BoxFace.West));
        Assert.Throws<ArgumentException>(() => BoxFeature.Vertex(BoxFace.Bottom, BoxFace.East, BoxFace.Top));
    }

    [Fact]
    public void LocalUprightsAreTheCornersOfTheBlankAsDrawn()
    {
        // BoxCorner's definitions: SouthWest (0, 0), SouthEast (width, 0), NorthEast (width,
        // height), NorthWest (0, height) — so the faces at each are the y-side and the x-side there.
        Assert.Equal(BoxFeature.Edge(BoxFace.South, BoxFace.West), BoxFeature.LocalUpright(BoxCorner.SouthWest));
        Assert.Equal(BoxFeature.Edge(BoxFace.South, BoxFace.East), BoxFeature.LocalUpright(BoxCorner.SouthEast));
        Assert.Equal(BoxFeature.Edge(BoxFace.North, BoxFace.East), BoxFeature.LocalUpright(BoxCorner.NorthEast));
        Assert.Equal(BoxFeature.Edge(BoxFace.North, BoxFace.West), BoxFeature.LocalUpright(BoxCorner.NorthWest));

        Assert.Equal([BoxFace.South, BoxFace.West], BoxFeature.LocalUpright(BoxCorner.SouthWest).Faces);
        Assert.Equal([BoxFace.South, BoxFace.East], BoxFeature.LocalUpright(BoxCorner.SouthEast).Faces);
        Assert.Equal([BoxFace.East, BoxFace.North], BoxFeature.LocalUpright(BoxCorner.NorthEast).Faces);
        Assert.Equal([BoxFace.North, BoxFace.West], BoxFeature.LocalUpright(BoxCorner.NorthWest).Faces);

        // A local upright is an edge along local Z: it names no cap.
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            BoxFeature upright = BoxFeature.LocalUpright(corner);
            Assert.DoesNotContain(BoxFace.Bottom, upright.Faces);
            Assert.DoesNotContain(BoxFace.Top, upright.Faces);
        }
    }

    [Fact]
    public void VertexAtACornerIsItsUprightPlusTheCap()
    {
        Assert.Equal(
            BoxFeature.Vertex(BoxFace.South, BoxFace.West, BoxFace.Bottom),
            BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom));
        Assert.Equal(
            BoxFeature.Vertex(BoxFace.North, BoxFace.East, BoxFace.Top),
            BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top));

        HashSet<BoxFeature> all = [];
        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
            {
                BoxFeature vertex = BoxFeature.Vertex(corner, level);
                BoxFace cap = level == BoxLevel.Bottom ? BoxFace.Bottom : BoxFace.Top;
                Assert.Equal(BoxFeature.LocalUpright(corner).Faces.Add(cap), vertex.Faces);
                all.Add(vertex);
            }
        }

        Assert.Equal(8, all.Count);
    }

    [Fact]
    public void UndefinedValuesAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Face((BoxFace)6));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Face((BoxFace)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Edge(BoxFace.South, (BoxFace)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Vertex(BoxFace.South, BoxFace.West, (BoxFace)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.LocalUpright((BoxCorner)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Vertex(BoxCorner.SouthWest, (BoxLevel)2));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoxFeature.Opposite((BoxFace)6));
    }

    [Fact]
    public void TheDefaultIsNotAFeature()
    {
        BoxFeature none = default;

        Assert.Empty(none.Faces);
        Assert.Throws<InvalidOperationException>(() => none.Dimension);
        Assert.NotEqual(none, BoxFeature.Face(BoxFace.South));
        Assert.Equal("BoxFeature(none)", none.ToString());
    }

    [Fact]
    public void ToStringSpellsTheFeature()
    {
        Assert.Equal("Face(Top)", BoxFeature.Face(BoxFace.Top).ToString());
        Assert.Equal("Edge(South, West)", BoxFeature.Edge(BoxFace.West, BoxFace.South).ToString());
        Assert.Equal("Vertex(East, North, Top)", BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top).ToString());
    }

    [Fact]
    public void OppositeIsTheDocumentedPairing()
    {
        foreach ((BoxFace a, BoxFace b) in OppositePairs)
        {
            Assert.Equal(b, BoxFeature.Opposite(a));
            Assert.Equal(a, BoxFeature.Opposite(b));
        }
    }

    private static bool IsOppositePair(BoxFace a, BoxFace b)
        => OppositePairs.Any(pair => (pair.A == a && pair.B == b) || (pair.A == b && pair.B == a));
}
