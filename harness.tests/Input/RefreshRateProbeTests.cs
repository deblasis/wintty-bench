using System.Runtime.Versioning;
using WinttyBench.Input;
using Xunit;

namespace WinttyBench.Tests.Input;

[SupportedOSPlatform("windows")]
public class RefreshRateProbeTests
{
    [Fact]
    [Trait("OS", "Windows")]
    public void GetPrimaryRefreshHz_Returns_Plausible_Value_OnWindowsHost()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "EnumDisplaySettings is Windows-only");
        var hz = RefreshRateProbe.GetPrimaryRefreshHz();
        if (hz is null) Assert.Skip("headless agent: no primary monitor");
        Assert.InRange(hz!.Value, 24, 600);
    }
}
