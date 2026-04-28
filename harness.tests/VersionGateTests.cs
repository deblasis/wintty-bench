using System.Collections.Generic;
using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class VersionGateTests
{
    [Fact]
    public void Parse_SinglePair_OK()
    {
        var pins = VersionGate.Parse("wt:1.18.3231.0");
        Assert.Single(pins);
        Assert.Equal("1.18.3231.0", pins["wt"]);
    }

    [Fact]
    public void Parse_MultiplePairs_OK()
    {
        var pins = VersionGate.Parse("wt:1.18.3231.0,wezterm:20240829-051119");
        Assert.Equal(2, pins.Count);
        Assert.Equal("1.18.3231.0", pins["wt"]);
        Assert.Equal("20240829-051119", pins["wezterm"]);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(VersionGate.Parse(""));
        Assert.Empty(VersionGate.Parse(null!));
    }

    [Fact]
    public void Parse_MalformedPair_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => VersionGate.Parse("wt"));
        Assert.Throws<System.ArgumentException>(() => VersionGate.Parse("wt:"));
        Assert.Throws<System.ArgumentException>(() => VersionGate.Parse(":1.0"));
    }

    [Fact]
    public void Verify_AllMatch_NoThrow()
    {
        var pins = new Dictionary<string, string> { ["wt"] = "1.18.3231.0" };
        var detected = new Dictionary<string, string> { ["wt"] = "1.18.3231.0" };
        VersionGate.Verify(pins, detected);  // no throw
    }

    [Fact]
    public void Verify_OneMismatch_Throws()
    {
        var pins = new Dictionary<string, string> { ["wt"] = "1.18.3231.0" };
        var detected = new Dictionary<string, string> { ["wt"] = "1.19.0.0" };
        var ex = Assert.Throws<VersionMismatchException>(() => VersionGate.Verify(pins, detected));
        Assert.Contains("wt", ex.Message);
        Assert.Contains("1.18.3231.0", ex.Message);
        Assert.Contains("1.19.0.0", ex.Message);
    }

    [Fact]
    public void Verify_MissingDetected_Throws()
    {
        var pins = new Dictionary<string, string> { ["wt"] = "1.18.3231.0" };
        var detected = new Dictionary<string, string>();
        Assert.Throws<VersionMismatchException>(() => VersionGate.Verify(pins, detected));
    }
}
