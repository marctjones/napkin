namespace Napkin.Core.RulesEngine.Tests;

public class SmokeTests
{
    [Fact]
    public void Base_fixture_loads()
    {
        LoadedPack pack = Fx.Load("us-zz-base");
        Assert.Single(pack.Tables);
        Assert.Equal(20, pack.Tables[0].Rows.Count);
    }
}
