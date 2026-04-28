using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class BenchHostParseArgsTests
{
    private static readonly string[] WinttyDefaultArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--target=C:/wintty.exe",
    ];

    private static readonly string[] WinttyWtArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--target=C:/wintty.exe",
        "--terminals=wintty,wt",
    ];

    private static readonly string[] WtOnlyArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--target-wt=auto",
        "--terminals=wt",
    ];

    private static readonly string[] WinttyTerminalNoTargetArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--terminals=wintty",
    ];

    private static readonly string[] RequireVersionArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--target=C:/wintty.exe",
        "--terminals=wintty,wt",
        "--target-wt=auto",
        "--require-version=wt:1.18.3231.0",
    ];

    private static readonly string[] UnknownTerminalArgs =
    [
        "--mode=ci",
        "--cells=C1",
        "--target=C:/wintty.exe",
        "--terminals=wintty,nethack",
    ];

    [Fact]
    public void NoTerminalsFlag_DefaultsToWintty()
    {
        var parsed = BenchHost.ParseArgs(WinttyDefaultArgs);
        Assert.Single(parsed.Terminals);
        Assert.Equal("wintty", parsed.Terminals[0]);
    }

    [Fact]
    public void TerminalsWithWt_RequiresTargetWt()
    {
        var ex = Assert.Throws<System.ArgumentException>(() => BenchHost.ParseArgs(WinttyWtArgs));
        Assert.Contains("--target-wt", ex.Message);
    }

    [Fact]
    public void TerminalsWtOnly_DoesNotRequireTarget()
    {
        var parsed = BenchHost.ParseArgs(WtOnlyArgs);
        Assert.Single(parsed.Terminals);
        Assert.Equal("wt", parsed.Terminals[0]);
        Assert.Equal("auto", parsed.TargetWtPath);
    }

    [Fact]
    public void TerminalsWintty_StillRequiresTarget()
    {
        Assert.Throws<System.ArgumentException>(() => BenchHost.ParseArgs(WinttyTerminalNoTargetArgs));
    }

    [Fact]
    public void RequireVersion_Parses()
    {
        var parsed = BenchHost.ParseArgs(RequireVersionArgs);
        Assert.Equal("wt:1.18.3231.0", parsed.RequireVersion);
    }

    [Fact]
    public void UnknownTerminal_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => BenchHost.ParseArgs(UnknownTerminalArgs));
    }
}
