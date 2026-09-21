using Napkin.Tools.Coverage;

namespace Napkin.Tools.Tests;

public class CoverageMergerTests
{
    [Fact]
    public void ReadsLineAndBranchCountsFromRealCoverletOutput()
    {
        var merged = CoverageMerger.Merge([Fixture.Path("branchy.cobertura.xml")]);

        var assembly = Assert.Single(merged);
        Assert.Equal("Napkin.Probe.Branchy", assembly.Assembly);

        // The same totals coverlet itself put on the <coverage> element: lines-valid="19"
        // lines-covered="10" branches-valid="8" branches-covered="3".
        Assert.Equal(19, assembly.LinesCoverable);
        Assert.Equal(10, assembly.LinesCovered);
        Assert.Equal(8, assembly.BranchesTotal);
        Assert.Equal(3, assembly.BranchesCovered);
    }

    [Fact]
    public void PercentagesAreInPercentagePoints()
    {
        var assembly = Assert.Single(CoverageMerger.Merge([Fixture.Path("branchy.cobertura.xml")]));

        Assert.Equal(100.0 * 10 / 19, assembly.LinePercent!.Value, 6);
        Assert.Equal(37.5, assembly.BranchPercent!.Value, 6);
        Assert.False(assembly.IsNotApplicable);
    }

    [Fact]
    public void MergingTheSameReportTwiceDoesNotDoubleCount()
    {
        // The data collector copies each report into its own attachment directory, so a
        // solution-wide run really does present the same lines more than once.
        var once = Assert.Single(CoverageMerger.Merge([Fixture.Path("branchy.cobertura.xml")]));
        var twice = Assert.Single(CoverageMerger.Merge(
            [Fixture.Path("branchy.cobertura.xml"), Fixture.Path("branchy.cobertura.xml")]));

        Assert.Equal(once, twice);
    }

    [Fact]
    public void TwoReportsCoveringDifferentLinesUnionTogether()
    {
        using var scratch = Fixture.NewDirectory();
        var first = scratch.Write("a/coverage.cobertura.xml", Report(firstHit: 1, secondHit: 0));
        var second = scratch.Write("b/coverage.cobertura.xml", Report(firstHit: 0, secondHit: 3));

        var assembly = Assert.Single(CoverageMerger.Merge([first, second]));

        Assert.Equal(2, assembly.LinesCoverable);
        Assert.Equal(2, assembly.LinesCovered);
    }

    [Fact]
    public void AnAssemblyWithNoCoverableLinesIsNotApplicable()
    {
        var assembly = Assert.Single(
            CoverageMerger.Merge([Fixture.Path("placeholder.cobertura.xml")]));

        Assert.Equal("Napkin.Core.Geometry", assembly.Assembly);
        Assert.True(assembly.IsNotApplicable);
        Assert.Null(assembly.LinePercent);
        Assert.Null(assembly.BranchPercent);
    }

    [Fact]
    public void BranchlessCodeHasNoBranchPercentageRatherThanZero()
    {
        using var scratch = Fixture.NewDirectory();
        var path = scratch.Write("coverage.cobertura.xml", Report(firstHit: 1, secondHit: 1));

        var assembly = Assert.Single(CoverageMerger.Merge([path]));

        Assert.Equal(100.0, assembly.LinePercent);
        Assert.Null(assembly.BranchPercent);
    }

    [Fact]
    public void AMalformedReportIsAnInputError()
    {
        using var scratch = Fixture.NewDirectory();
        var path = scratch.Write("coverage.cobertura.xml", "<nonsense/>");

        var exception = Assert.Throws<InputException>(() => CoverageMerger.Merge([path]));
        Assert.Contains("coverage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindReportsReturnsNothingForADirectoryThatDoesNotExist() =>
        Assert.Empty(CoverageMerger.FindReports(Path.Combine(Path.GetTempPath(), "no-such-dir-xyz")));

    private static string Report(int firstHit, int secondHit) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0" branch-rate="0" version="1.9">
          <packages>
            <package name="Napkin.Test.Sample">
              <classes>
                <class name="Napkin.Test.Sample.Thing" filename="Thing.cs">
                  <lines>
                    <line number="10" hits="{firstHit}" branch="False" />
                    <line number="11" hits="{secondHit}" branch="False" />
                  </lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
