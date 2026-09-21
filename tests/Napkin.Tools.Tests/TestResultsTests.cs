using Napkin.Tools.Features;

namespace Napkin.Tools.Tests;

public class TestResultsTests
{
    [Fact]
    public void ReadsOutcomesFromARealTrxFile()
    {
        var results = TestResults.Load([Fixture.Path("probe.trx")]);

        Assert.Equal(TestOutcome.Passed, results.OutcomeOf("TraitProbe.Probe.Passes"));
        Assert.Equal(TestOutcome.Passed, results.OutcomeOf("TraitProbe.Probe+Nested.Inner"));
    }

    [Fact]
    public void ASkippedFactReadsAsSkipped() =>
        // xunit's Skip comes through TRX as outcome="NotExecuted".
        Assert.Equal(
            TestOutcome.Skipped,
            TestResults.Load([Fixture.Path("probe.trx")]).OutcomeOf("TraitProbe.Probe.Skipped"));

    [Fact]
    public void ATheorysCasesFoldIntoOneMethodOutcome()
    {
        var results = TestResults.Load([Fixture.Path("probe.trx")]);

        // Two <UnitTest> rows, `TheoryCase(i: 1)` and `(i: 2)`, one method name.
        Assert.Equal(TestOutcome.Passed, results.OutcomeOf("TraitProbe.Probe.TheoryCase"));
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void ATestTheRunNeverMentionedCountsAsSkipped() =>
        Assert.Equal(
            TestOutcome.Skipped,
            TestResults.Load([Fixture.Path("probe.trx")]).OutcomeOf("Napkin.Nothing.Here"));

    [Theory]
    [InlineData("Failed", TestOutcome.Failed)]
    [InlineData("Timeout", TestOutcome.Failed)]
    [InlineData("Aborted", TestOutcome.Failed)]
    [InlineData("Error", TestOutcome.Failed)]
    [InlineData("NotExecuted", TestOutcome.Skipped)]
    [InlineData("Passed", TestOutcome.Passed)]
    public void EveryOutcomeThatIsNotPassedOrNotExecutedIsAFailure(string outcome, TestOutcome expected)
    {
        using var scratch = Fixture.NewDirectory();
        var path = scratch.Write("run.trx", Trx(outcome));

        Assert.Equal(expected, TestResults.Load([path]).OutcomeOf("Napkin.Sample.Thing.Works"));
    }

    [Fact]
    public void AFailingCaseOutranksAPassingOneInTheSameMethod()
    {
        using var scratch = Fixture.NewDirectory();
        var pass = scratch.Write("a.trx", Trx("Passed"));
        var fail = scratch.Write("b.trx", Trx("Failed"));

        Assert.Equal(
            TestOutcome.Failed,
            TestResults.Load([pass, fail]).OutcomeOf("Napkin.Sample.Thing.Works"));
    }

    [Fact]
    public void AMalformedTrxIsAnInputError()
    {
        using var scratch = Fixture.NewDirectory();
        var path = scratch.Write("run.trx", "not xml at all");

        Assert.Throws<InputException>(() => TestResults.Load([path]));
    }

    private static string Trx(string outcome) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testId="11111111-1111-1111-1111-111111111111"
                            testName="Napkin.Sample.Thing.Works" outcome="{outcome}" />
          </Results>
          <TestDefinitions>
            <UnitTest name="Napkin.Sample.Thing.Works" id="11111111-1111-1111-1111-111111111111">
              <TestMethod className="Napkin.Sample.Thing" name="Works" />
            </UnitTest>
          </TestDefinitions>
        </TestRun>
        """;
}
