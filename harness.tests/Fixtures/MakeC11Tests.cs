using Xunit;

namespace WinttyBench.Tests.Fixtures;

[Trait("Category", "Integration")]
public class MakeC11Tests
{
    private static string RunScriptAndGetHash(long size)
    {
        var wslHome = WslHelpers.RunWsl("printenv HOME").Trim();
        var target = $"{wslHome}/.cache/wintty-bench/c11-{size}.bin";
        WslHelpers.RunWsl($"rm -f '{target}'");

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var scriptWsl = WslHelpers.ToWslMountPath(Path.Combine(repoRoot, "scripts/fixtures/make-c11.sh"));
        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");

        return WslHelpers.RunWsl($"sha256sum '{target}' | cut -d' ' -f1").Trim();
    }

    [Fact]
    public void MakeC11_Is_Deterministic()
    {
        var size = 131_072L;
        var hash1 = RunScriptAndGetHash(size);
        var hash2 = RunScriptAndGetHash(size);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void MakeC11_Produces_Exact_Size()
    {
        var size = 131_072L;
        var wslHome = WslHelpers.RunWsl("printenv HOME").Trim();
        var target = $"{wslHome}/.cache/wintty-bench/c11-{size}.bin";
        WslHelpers.RunWsl($"rm -f '{target}'");

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var scriptWsl = WslHelpers.ToWslMountPath(Path.Combine(repoRoot, "scripts/fixtures/make-c11.sh"));
        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");

        var statOut = WslHelpers.RunWsl($"stat -c '%s' '{target}'").Trim();
        Assert.Equal(size.ToString(), statOut);
    }

    [Fact]
    public void MakeC11_No_Forbidden_Bytes()
    {
        var size = 131_072L;
        var wslHome = WslHelpers.RunWsl("printenv HOME").Trim();
        var target = $"{wslHome}/.cache/wintty-bench/c11-{size}.bin";
        WslHelpers.RunWsl($"rm -f '{target}'");

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var scriptWsl = WslHelpers.ToWslMountPath(Path.Combine(repoRoot, "scripts/fixtures/make-c11.sh"));
        WslHelpers.RunWsl($"bash '{scriptWsl}' {size}");

        var hexdump = WslHelpers.RunWsl(
            $"xxd -p -c 1 '{target}' | sort -u | tr -d '\\n' | sed 's/../&,/g'").Trim();
        var seenHex = hexdump.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => Convert.ToByte(h, 16))
            .ToHashSet();

        var allowed = new HashSet<byte>();
        for (byte b = 0x20; b <= 0x7E; b++) allowed.Add(b);
        allowed.Add(0x09); // tab
        allowed.Add(0x0A); // newline
        allowed.Add(0x1B); // ESC (validated as part of SGR/CUP sequences separately)

        var disallowed = seenHex.Where(b => !allowed.Contains(b)).ToList();
        Assert.True(disallowed.Count == 0,
            $"Forbidden bytes present: {string.Join(",", disallowed.Select(b => $"0x{b:X2}"))}");
    }
}
