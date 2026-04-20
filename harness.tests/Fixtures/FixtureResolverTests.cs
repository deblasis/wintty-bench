using WinttyBench;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using Xunit;

namespace WinttyBench.Tests.Fixtures;

[Trait("Category", "Integration")]
public class FixtureResolverTests
{
    private static Cell MakePlan1Cell(string shell, string fixturePath) => new(
        Id: "T1",
        Shell: shell,
        Workload: "x",
        Kpi: "throughput_bytes_per_sec",
        FixturePath: fixturePath,
        FixtureKey: null,
        WinttyConfigOverrides: new Dictionary<string, string>());

    private static Cell MakePlan2Cell(string key) => new(
        Id: "T2",
        Shell: "wsl-ubuntu-24.04",
        Workload: "x",
        Kpi: "throughput_bytes_per_sec",
        FixturePath: null,
        FixtureKey: key,
        WinttyConfigOverrides: new Dictionary<string, string>());

    [Fact]
    public async Task Plan1_Pwsh_Cell_Returns_Windows_Path()
    {
        var resolver = new FixtureResolver(new WslFixtureCache());
        var cell = MakePlan1Cell("pwsh-7.4", "fixtures/vtebench/dense_cells.txt");
        var handle = await resolver.ResolveAsync(cell, FairnessProfile.Ci());

        Assert.Equal(Path.GetFullPath("fixtures/vtebench/dense_cells.txt"), handle.ShellPath);
        Assert.True(handle.SizeBytes > 0);
    }

    [Fact]
    public async Task Plan1_Wsl_Cell_Returns_MountCPath()
    {
        var resolver = new FixtureResolver(new WslFixtureCache());
        var cell = MakePlan1Cell("wsl-ubuntu-24.04", "fixtures/vtebench/dense_cells.txt");
        var handle = await resolver.ResolveAsync(cell, FairnessProfile.Ci());

        Assert.StartsWith("/mnt/c/", handle.ShellPath);
        Assert.EndsWith("/fixtures/vtebench/dense_cells.txt", handle.ShellPath);
    }

    [Fact]
    public async Task Plan2A_C10_Cell_CacheMiss_InvokesGenerator()
    {
        var cache = new WslFixtureCache();
        var home = await cache.HomeAsync();
        var size = FairnessProfile.Ci().FixtureSizeBytesByKey["c10"];
        var target = $"{home}/.cache/wintty-bench/c10-{size}.bin";
        var sidecar = $"{target}.sha256";

        await cache.RunBashScriptAsync($"rm -f '{target}' '{sidecar}'");

        var resolver = new FixtureResolver(cache);
        var cell = MakePlan2Cell("c10");
        var handle = await resolver.ResolveAsync(cell, FairnessProfile.Ci());

        Assert.Equal(target, handle.ShellPath);
        Assert.Equal(size, handle.SizeBytes);
        Assert.True(File.Exists(cache.ToWindowsPath(target)));
        Assert.True(File.Exists(cache.ToWindowsPath(sidecar)));
    }

    [Fact]
    public async Task Plan2A_CacheHit_SkipsGenerator()
    {
        var cache = new WslFixtureCache();
        var resolver = new FixtureResolver(cache);
        var cell = MakePlan2Cell("c10");

        await resolver.ResolveAsync(cell, FairnessProfile.Ci());
        var home = await cache.HomeAsync();
        var size = FairnessProfile.Ci().FixtureSizeBytesByKey["c10"];
        var winPath = cache.ToWindowsPath($"{home}/.cache/wintty-bench/c10-{size}.bin");
        var mtime1 = File.GetLastWriteTimeUtc(winPath);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        await resolver.ResolveAsync(cell, FairnessProfile.Ci());
        var mtime2 = File.GetLastWriteTimeUtc(winPath);

        Assert.Equal(mtime1, mtime2);
    }

    [Fact]
    public async Task Plan2A_HashMismatch_Regenerates()
    {
        var cache = new WslFixtureCache();
        var resolver = new FixtureResolver(cache);
        var cell = MakePlan2Cell("c10");
        var home = await cache.HomeAsync();
        var size = FairnessProfile.Ci().FixtureSizeBytesByKey["c10"];
        var target = $"{home}/.cache/wintty-bench/c10-{size}.bin";
        var sidecar = $"{target}.sha256";

        await resolver.ResolveAsync(cell, FairnessProfile.Ci());

        await cache.RunBashScriptAsync($"printf 'deadbeef' > '{sidecar}'");
        var winPath = cache.ToWindowsPath(target);
        var mtime1 = File.GetLastWriteTimeUtc(winPath);
        await Task.Delay(1100, TestContext.Current.CancellationToken);

        await resolver.ResolveAsync(cell, FairnessProfile.Ci());
        var mtime2 = File.GetLastWriteTimeUtc(winPath);
        Assert.True(mtime2 > mtime1, "fixture should have been regenerated");
    }
}
