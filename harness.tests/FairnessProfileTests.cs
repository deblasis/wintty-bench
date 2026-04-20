using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class FairnessProfileTests
{
    // Static readonly to satisfy CA1861 (no allocating an array per Fact run).
    private static readonly string[] CiDiscarded = ["first"];
    private static readonly string[] MarketingDiscarded = ["first", "last"];

    [Fact]
    public void Ci_Profile_Uses_Default_Power_And_One_Warmup()
    {
        var profile = FairnessProfile.Ci();

        Assert.Equal("default", profile.PowerPlan);
        Assert.False(profile.DefenderExcluded);
        Assert.Equal("Normal", profile.ProcessPriority);
        Assert.False(profile.VmReverted);
        Assert.Equal(1, profile.WarmupIters);
        Assert.Equal(10, profile.MeasuredIters);
        Assert.Equal(CiDiscarded, profile.Discarded);
    }

    [Fact]
    public void Marketing_Profile_Uses_High_Perf_And_Three_Warmups()
    {
        var profile = FairnessProfile.Marketing();

        Assert.Equal("SCHEME_MIN", profile.PowerPlan);
        Assert.True(profile.DefenderExcluded);
        Assert.Equal("High", profile.ProcessPriority);
        Assert.True(profile.VmReverted);
        Assert.Equal(3, profile.WarmupIters);
        Assert.Equal(30, profile.MeasuredIters);
        Assert.Equal(MarketingDiscarded, profile.Discarded);
    }

    [Fact]
    public void Profile_Serializes_To_FairnessCapture()
    {
        var profile = FairnessProfile.Ci();
        var capture = profile.ToCapture();

        Assert.Equal(profile.PowerPlan, capture.PowerPlan);
        Assert.Equal(profile.WarmupIters, capture.WarmupIters);
    }
}
