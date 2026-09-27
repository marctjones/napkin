namespace Napkin.Core.Geometry.Tests;

/// <summary>A survey underlay's calibration (permit-set §5.4, #224): its rules, and SetSite refusing a bad one.</summary>
public class SurveyUnderlayTests
{
    const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    static SurveyUnderlay Good() => new(Hash, new Pixel(10, 20), new Pixel(410, 20), Point2.Origin, new Point2(Length.Feet(100), Length.Zero), Length.Feet(100), "survey.png");

    [Fact]
    public void A_good_calibration_is_kept_and_each_bad_one_is_refused()
    {
        Assert.Null(SurveyUnderlayRules.Refusal(Good()));
        Assert.Equal("an underlay names its image by the SHA-256 of its bytes: 64 lowercase hex digits", SurveyUnderlayRules.Refusal(Good() with { Asset = Hash.ToUpperInvariant() }));
        Assert.NotNull(SurveyUnderlayRules.Refusal(Good() with { Asset = Hash[..63] }));
        Assert.Equal("an underlay is calibrated from two different points", SurveyUnderlayRules.Refusal(Good() with { ImageB = new Pixel(10, 20) }));
        Assert.Equal("an underlay is calibrated from two different points", SurveyUnderlayRules.Refusal(Good() with { WorldB = Point2.Origin }));
        Assert.Equal("the distance between the calibration points is longer than zero", SurveyUnderlayRules.Refusal(Good() with { Distance = Length.Zero }));
        Assert.Equal("an underlay says which file it came from", SurveyUnderlayRules.Refusal(Good() with { Name = " " }));
        Assert.False(SurveyUnderlayRules.IsHash(null));
    }

    [Fact]
    public void SetSite_keeps_a_good_underlay_and_refuses_a_bad_one()
    {
        SketchBuilder builder = new();
        Solved set = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(builder.Sketch, new SetSite(SiteValues.NotEntered with { Underlay = Good() })));
        Assert.Equal(Good(), set.Sketch.Site.Underlay);
        Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(builder.Sketch, new SetSite(SiteValues.NotEntered with { Underlay = Good() with { Distance = Length.Zero } })));
        Assert.Null(SiteValues.NotEntered.Underlay);
    }
}
