using System.Diagnostics;
using Xunit;

namespace WinttyBench.Tests.Fixtures;

[Trait("Category", "Integration")]
public class MakeC10Tests
{
    [Fact]
    public void MakeC10_Produces_Exact_Size()
    {
        var size = 1_048_576L;
        var wslHome = WslHelpers.RunWsl("printenv HOME").Trim();
        var target = $"{wslHome}/.cache/wintty-bench/c10-{size}.bin";

        WslHelpers.RunWsl($"rm -f '{target}'");
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var scriptWsl = WslHelpers.ToWslMountPath(Path.Combine(repoRoot, "scripts/fixtures/make-c10.sh"));
        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");

        var statOutput = WslHelpers.RunWsl($"stat -c '%s' '{target}'").Trim();
        Assert.Equal(size.ToString(), statOutput);
    }

    [Fact]
    public void MakeC10_Is_Deterministic()
    {
        var size = 131_072L;
        var wslHome = WslHelpers.RunWsl("printenv HOME").Trim();
        var target = $"{wslHome}/.cache/wintty-bench/c10-{size}.bin";

        WslHelpers.RunWsl($"rm -f '{target}'");
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var scriptWsl = WslHelpers.ToWslMountPath(Path.Combine(repoRoot, "scripts/fixtures/make-c10.sh"));

        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");
        var hash1 = WslHelpers.RunWsl($"sha256sum '{target}' | cut -d' ' -f1").Trim();
        WslHelpers.RunWsl($"rm '{target}'");
        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");
        var hash2 = WslHelpers.RunWsl($"sha256sum '{target}' | cut -d' ' -f1").Trim();

        Assert.Equal(hash1, hash2);
    }
}
