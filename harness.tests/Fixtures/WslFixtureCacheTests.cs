using WinttyBench.Fixtures;
using Xunit;

namespace WinttyBench.Tests.Fixtures;

[Trait("Category", "Integration")]
public class WslFixtureCacheTests
{
    [Fact]
    public async Task HomeAsync_Returns_NonEmpty_Path_Starting_With_Slash()
    {
        var cache = new WslFixtureCache();
        var home = await cache.HomeAsync();
        Assert.False(string.IsNullOrWhiteSpace(home));
        Assert.StartsWith("/", home);
    }

    [Fact]
    public async Task HomeAsync_Caches_Result()
    {
        var cache = new WslFixtureCache();
        var h1 = await cache.HomeAsync();
        var h2 = await cache.HomeAsync();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public async Task ToWindowsPath_Converts_WslHomePath_To_UncPath()
    {
        var cache = new WslFixtureCache();
        var home = await cache.HomeAsync();
        var wslPath = $"{home}/foo/bar.bin";
        var winPath = cache.ToWindowsPath(wslPath);
        Assert.StartsWith(@"\\wsl.localhost\Ubuntu-24.04\", winPath);
        Assert.EndsWith(@"\foo\bar.bin", winPath);
    }

    [Fact]
    public async Task ComputeSha256Async_Matches_CommandLine_SHA256()
    {
        var cache = new WslFixtureCache();
        var home = await cache.HomeAsync();
        var testFile = $"{home}/.cache/wintty-bench-test.bin";

        WslHelpers.RunWsl($"mkdir -p '{home}/.cache' && printf 'hello' > '{testFile}'");
        var expected = WslHelpers.RunWsl($"sha256sum '{testFile}' | cut -d' ' -f1").Trim();
        try
        {
            var actual = await cache.ComputeSha256Async(testFile);
            Assert.Equal(expected, actual);
        }
        finally
        {
            WslHelpers.RunWsl($"rm -f '{testFile}'");
        }
    }
}
