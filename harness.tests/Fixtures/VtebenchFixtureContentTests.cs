using System.IO;
using Xunit;

namespace WinttyBench.Tests.Fixtures;

// Guards against a repeat of the Plan 1/2A vendoring bug where the vtebench
// "fixtures" were actually the upstream shell-script wrappers (70-765 bytes
// each) rather than the byte streams those wrappers would produce. The fix
// replaces them with deterministic ~138KB-2.5MB byte streams. This test
// asserts the post-fix shape so any future regression is caught immediately.
public class VtebenchFixtureContentTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "fixtures", "vtebench")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate repo root from " + AppContext.BaseDirectory);
        }
    }

    [Theory]
    [InlineData("dense_cells.txt", 2_000_000)]
    [InlineData("scrolling.txt", 100_000)]
    [InlineData("unicode.txt", 100_000)]
    public void VtebenchFixture_IsByteStreamNotShellScript(string name, long minSize)
    {
        var path = Path.Combine(RepoRoot, "fixtures", "vtebench", name);
        Assert.True(File.Exists(path), $"Fixture missing: {path}");

        var info = new FileInfo(path);
        Assert.True(info.Length >= minSize,
            $"{name} is {info.Length} bytes, expected >= {minSize}. " +
            "Did someone re-vendor the upstream shell-script wrapper instead of the byte stream?");

        using var fs = File.OpenRead(path);
        var head = new byte[9];
        var read = fs.Read(head, 0, head.Length);
        Assert.True(read == 9, $"{name} is too small to inspect header");

        var shebang = System.Text.Encoding.ASCII.GetString(head);
        Assert.False(shebang.StartsWith("#!/bin/sh", System.StringComparison.Ordinal),
            $"{name} starts with '#!/bin/sh' - looks like a shell-script wrapper, not a byte stream.");
    }
}
