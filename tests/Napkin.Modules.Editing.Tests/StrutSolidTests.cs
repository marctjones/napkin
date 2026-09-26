using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>A strut drawn (#192, assembly-model §3a.7, angled-parts §4): six outward faces whose ends lie in the planes they are cut to.</summary>
public class StrutSolidTests
{
    private static Point3 At(double x, double y, double z) => new(Length.FromInches(x, Rounding.HalfToEven), Length.FromInches(y, Rounding.HalfToEven), Length.FromInches(z, Rounding.HalfToEven));

    private static Strut Leg(Point3 from, Point3 to, Axis reference = Axis.Z, EndCut fromCut = EndCut.Z, EndCut toCut = EndCut.Z)
        => new(EntityId.New(), LayerId.Default, from, to, fromCut, toCut, reference, Length.Inches(1, 1, 2), Length.Inches(1, 1, 2));

    // The splayed bench's south-west leg (angled-parts §9.1): foot (4, −4, 0), top (4, 3, 24).
    private static readonly Strut BenchLeg = Leg(At(4, -4, 0), At(4, 3, 24));

    [Fact]
    public void ALegsFootSitsFlatOnTheFloorAndItsTopUnderTheSeat()
    {
        ImmutableArray<StrutSolidPolygon> faces = StrutSolid.Of(BenchLeg);

        Assert.Equal(6, faces.Length);
        Assert.All(faces.Single(face => face.Face == StrutSolidFace.WestEnd).Corners, corner => Assert.Equal(0, corner.Z, 9));
        Assert.All(faces.Single(face => face.Face == StrutSolidFace.EastEnd).Corners, corner => Assert.Equal(24, corner.Z, 9));
    }

    [Theory]
    [InlineData(Axis.Z)]
    [InlineData(Axis.X)]
    [InlineData(Axis.Y)]
    public void EveryFaceIsPlanarAndFacesOut(Axis reference)
    {
        // The footstool's two-way leg, every way it can be made (§9.2): compound ends included.
        Strut leg = Leg(At(0, -1, 0), At(3, 3, 12), reference);
        ImmutableArray<StrutSolidPolygon> faces = StrutSolid.Of(leg);
        (double X, double Y, double Z) centre = (1.5, 1, 6);

        foreach (StrutSolidPolygon face in faces)
        {
            (double X, double Y, double Z) n = Newell(face.Corners);
            (double X, double Y, double Z) mid = (face.Corners.Average(c => c.X), face.Corners.Average(c => c.Y), face.Corners.Average(c => c.Z));
            Assert.True(Dot(n, (mid.X - centre.X, mid.Y - centre.Y, mid.Z - centre.Z)) > 0, $"{face.Face} faces in.");

            // Planar: every corner is on the plane through the first with that normal.
            Assert.All(face.Corners, c => Assert.Equal(0, Dot(n, (c.X - face.Corners[0].X, c.Y - face.Corners[0].Y, c.Z - face.Corners[0].Z)), 9));
        }

        Assert.All(faces.Single(face => face.Face == StrutSolidFace.WestEnd).Corners, corner => Assert.Equal(0, corner.Z, 9));
    }

    [Fact]
    public void ASquareEndIsNormalToTheStrut()
    {
        // A 3-4-5 brace fixed by hardware: its end faces are square to its run.
        Strut brace = Leg(At(0, 0, 0), At(3, 0, 4), fromCut: EndCut.Square, toCut: EndCut.Square);
        StrutSolidPolygon west = StrutSolid.Of(brace).Single(face => face.Face == StrutSolidFace.WestEnd);

        (double X, double Y, double Z) n = Newell(west.Corners);
        double length = Math.Sqrt(Dot(n, n));
        Assert.Equal(-0.6, n.X / length, 9);
        Assert.Equal(-0.8, n.Z / length, 9);
    }

    [Fact]
    public void AReversedStrutIsTheSameSolid()
    {
        Strut swapped = BenchLeg with { From = BenchLeg.To, To = BenchLeg.From };

        Assert.Equal(
            StrutSolid.Of(BenchLeg).SelectMany(face => face.Corners).Select(Rounded).Order(),
            StrutSolid.Of(swapped).SelectMany(face => face.Corners).Select(Rounded).Order());
    }

    [Fact]
    public void FromAboveTheBenchLegIsAStripAndPicksWithinReach()
    {
        // Seen from above the leg is a strip 1 1/2″ wide about x = 4, running out along Y.
        ImmutableArray<(double X, double Y)> outline = StrutSolid.PlanOutline(BenchLeg);

        Assert.Equal(3.25, outline.Min(point => point.X), 9);
        Assert.Equal(4.75, outline.Max(point => point.X), 9);
        Assert.True(StrutSolid.PlanContains(BenchLeg, 4, 0, reach: 0));
        Assert.False(StrutSolid.PlanContains(BenchLeg, 5, 0, reach: 0));
        Assert.True(StrutSolid.PlanContains(BenchLeg, 5, 0, reach: 0.5));
    }

    private static string Rounded((double X, double Y, double Z) c) => FormattableString.Invariant($"{c.X:F6},{c.Y:F6},{c.Z:F6}");

    private static (double X, double Y, double Z) Newell(ImmutableArray<(double X, double Y, double Z)> corners)
    {
        (double X, double Y, double Z) n = (0, 0, 0);
        for (int i = 0; i < corners.Length; i++)
        {
            (double X, double Y, double Z) a = corners[i];
            (double X, double Y, double Z) b = corners[(i + 1) % corners.Length];
            n = (n.X + ((a.Y - b.Y) * (a.Z + b.Z)), n.Y + ((a.Z - b.Z) * (a.X + b.X)), n.Z + ((a.X - b.X) * (a.Y + b.Y)));
        }

        return n;
    }

    private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
}
