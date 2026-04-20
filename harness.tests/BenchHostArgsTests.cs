using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class BenchHostArgsTests
{
    private static readonly string[] OneC1 = ["C1"];

    [Fact]
    public void Parse_Ci_Mode_Single_Cell_Target()
    {
        var args = new[] { "--mode=ci", "--cells=C1", "--target=C:\\wintty\\Wintty.exe" };

        var parsed = BenchHost.ParseArgs(args);

        Assert.Equal("ci", parsed.Mode);
        Assert.Equal(OneC1, parsed.Cells);
        Assert.Equal("C:\\wintty\\Wintty.exe", parsed.TargetExePath);
        Assert.Null(parsed.ReleaseTag);
    }

    [Fact]
    public void Parse_All_Cells_Expands_To_Starred()
    {
        var args = new[] { "--mode=ci", "--cells=all", "--target=C:\\wintty\\Wintty.exe" };

        var parsed = BenchHost.ParseArgs(args);

        Assert.Contains("C1", parsed.Cells);
        Assert.Contains("C2", parsed.Cells);
        Assert.Contains("C3", parsed.Cells);
        Assert.Contains("C4", parsed.Cells);
    }

    [Fact]
    public void Parse_Marketing_Requires_ReleaseTag()
    {
        var args = new[] { "--mode=marketing", "--cells=C1", "--target=C:\\x.exe" };

        var ex = Assert.Throws<ArgumentException>(() => BenchHost.ParseArgs(args));
        Assert.Contains("--release-tag", ex.Message);
    }

    [Fact]
    public void Parse_Marketing_With_ReleaseTag_Works()
    {
        var args = new[] { "--mode=marketing", "--cells=C1", "--target=C:\\x.exe", "--release-tag=v0.3.0" };

        var parsed = BenchHost.ParseArgs(args);

        Assert.Equal("v0.3.0", parsed.ReleaseTag);
    }

    [Fact]
    public void Parse_Unknown_Mode_Throws()
    {
        var args = new[] { "--mode=wat", "--cells=C1", "--target=C:\\x.exe" };

        var ex = Assert.Throws<ArgumentException>(() => BenchHost.ParseArgs(args));
        Assert.Contains("mode", ex.Message);
    }

    [Fact]
    public void Parse_Unknown_Cell_Throws()
    {
        var args = new[] { "--mode=ci", "--cells=C99", "--target=C:\\x.exe" };

        var ex = Assert.Throws<ArgumentException>(() => BenchHost.ParseArgs(args));
        Assert.Contains("C99", ex.Message);
    }
}
