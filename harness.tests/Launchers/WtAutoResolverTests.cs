using System;
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
    public void Resolve_Auto_ReturnsAnExistingWtExe()
    {
        // Tolerant of either resolution path: portable cache (preferred) or
        // Store install. Both end in `wt.exe` and must point at a real file.
        // The portable cache wins if present — see Resolve_Auto_PrefersPortableCacheOverStore
        // for the precedence assertion against an isolated USERPROFILE.
        var resolved = WtAutoResolver.Resolve("auto");
        Assert.True(File.Exists(resolved), $"Expected wt.exe at {resolved} but file not found");
        Assert.EndsWith("wt.exe", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_Auto_PrefersPortableCacheOverStore()
    {
        // Setup a fake portable WT cache in a temp profile, repoint USERPROFILE
        // at it, and verify Resolve picks the portable path even when a Store
        // install is also present on the host. Mutates global env state — the
        // existing Resolve_Auto_ReturnsAnExistingWtExe test already does the
        // same, so this is consistent with the fixture's pattern.
        var tempProfile = Path.Combine(Path.GetTempPath(), "wt-resolver-test-" + Guid.NewGuid());
        var portableDir = Path.Combine(tempProfile, ".cache", "wintty-bench", "wt", "1.21.3231.0");
        Directory.CreateDirectory(portableDir);
        var portableWt = Path.Combine(portableDir, "wt.exe");
        File.WriteAllText(portableWt, "fake wt.exe");

        var oldUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        try
        {
            Environment.SetEnvironmentVariable("USERPROFILE", tempProfile);
            var resolved = WtAutoResolver.Resolve("auto");
            Assert.Equal(portableWt, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("USERPROFILE", oldUserProfile);
            if (Directory.Exists(tempProfile)) Directory.Delete(tempProfile, recursive: true);
        }
    }
}
