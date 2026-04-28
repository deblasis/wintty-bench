using System.IO;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Launchers;

public class WtAutoResolverTests
{
    [Fact]
    public void Resolve_ExplicitPath_ReturnsPathWhenExists()
    {
        var existing = System.Environment.ProcessPath!;  // any real exe
        var resolved = WtAutoResolver.Resolve(existing);
        Assert.Equal(existing, resolved);
    }

    [Fact]
    public void Resolve_ExplicitPath_ThrowsWhenMissing()
    {
        var bogus = Path.Combine(Path.GetTempPath(), "definitely-does-not-exist-wt.exe");
        Assert.Throws<FileNotFoundException>(() => WtAutoResolver.Resolve(bogus));
    }

    [Fact]
    public void Resolve_Auto_ReturnsLocalAppDataPathWhenPresent()
    {
        var resolved = WtAutoResolver.Resolve("auto");
        Assert.True(File.Exists(resolved), $"Expected wt.exe at {resolved} but file not found");
        Assert.EndsWith("wt.exe", resolved, System.StringComparison.OrdinalIgnoreCase);
    }
}
