using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Launchers;

public class WtLauncherTests
{
    [Fact]
    public void WriteFragment_PinsInitialColsRowsAndCloseOnExit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wt-launcher-test-" + System.Guid.NewGuid());
        try
        {
            WtLauncher.WriteFragment(dir, "pwsh -NoLogo", cols: 120, rows: 32);

            // Filename matches the dir's leaf name (see WriteFragment comment).
            var fragmentName = Path.GetFileName(dir);
            var fragmentPath = Path.Combine(dir, fragmentName + ".json");
            Assert.True(File.Exists(fragmentPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(fragmentPath));
            // Top-level "profiles" is an array in the fragment shape -- not
            // nested under "list" the way the main settings.json schema is.
            var profile = doc.RootElement.GetProperty("profiles")[0];

            Assert.Equal("pwsh -NoLogo", profile.GetProperty("commandline").GetString());
            Assert.Equal(120, profile.GetProperty("initialCols").GetInt32());
            Assert.Equal(32, profile.GetProperty("initialRows").GetInt32());
            Assert.Equal("always", profile.GetProperty("closeOnExit").GetString());
            Assert.Equal("WinttyBenchProfile", profile.GetProperty("name").GetString());

            // Fragments cannot set defaultProfile; the bench selects via `-p`.
            Assert.False(doc.RootElement.TryGetProperty("defaultProfile", out _));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteFragment_EscapesEmbeddedQuotes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wt-launcher-test-" + System.Guid.NewGuid());
        try
        {
            WtLauncher.WriteFragment(dir, "bash -c \"echo hi\"", cols: 80, rows: 24);

            var fragmentName = Path.GetFileName(dir);
            var fragmentPath = Path.Combine(dir, fragmentName + ".json");
            var content = File.ReadAllText(fragmentPath);
            // The JSON must parse without error and the commandline must round-trip.
            using var doc = JsonDocument.Parse(content);
            var profile = doc.RootElement.GetProperty("profiles")[0];
            Assert.Equal("bash -c \"echo hi\"", profile.GetProperty("commandline").GetString());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetFragmentRoot_PointsAtCanonicalLocalAppDataFragmentsDir()
    {
        var root = WtLauncher.GetFragmentRoot();
        var expected = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows Terminal", "Fragments");
        Assert.Equal(expected, root);
    }

    [Fact]
    public void WriteFragment_TrailingSlashOnDir_FilenameUsesLeafName()
    {
        // Locks down the leaf-name extraction in WriteFragment so a future
        // refactor that drops the TrimEnd doesn't silently put the JSON at
        // <dir>\.json (empty leaf), which WT would not load.
        var bare = Path.Combine(Path.GetTempPath(), "wt-launcher-test-" + System.Guid.NewGuid());
        var dirWithSlash = bare + Path.DirectorySeparatorChar;
        try
        {
            WtLauncher.WriteFragment(dirWithSlash, "pwsh", cols: 80, rows: 24);
            var leaf = Path.GetFileName(bare);
            Assert.True(File.Exists(Path.Combine(bare, leaf + ".json")));
        }
        finally
        {
            if (Directory.Exists(bare)) Directory.Delete(bare, recursive: true);
        }
    }

    [Fact]
    public void SweepLegacyFragments_DeletesHyphenSuffixedDirs_LeavesPlainDir()
    {
        // Pins the regex literal: pattern wintty-bench-* must match the
        // legacy GUID-suffixed dirs and NOT the live "wintty-bench" stable
        // dir. A future refactor that broadens the pattern would silently
        // delete the live profile mid-run.
        var fragRoot = Path.Combine(Path.GetTempPath(), "wt-sweep-test-" + System.Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(fragRoot, "wintty-bench"));
            Directory.CreateDirectory(Path.Combine(fragRoot, "wintty-bench-abc123"));
            Directory.CreateDirectory(Path.Combine(fragRoot, "wintty-bench-def456"));
            Directory.CreateDirectory(Path.Combine(fragRoot, "Microsoft.WSL"));

            WtLauncher.SweepLegacyFragments(fragRoot);

            Assert.True(Directory.Exists(Path.Combine(fragRoot, "wintty-bench")), "stable dir must survive sweep");
            Assert.False(Directory.Exists(Path.Combine(fragRoot, "wintty-bench-abc123")), "legacy dir must be swept");
            Assert.False(Directory.Exists(Path.Combine(fragRoot, "wintty-bench-def456")), "legacy dir must be swept");
            Assert.True(Directory.Exists(Path.Combine(fragRoot, "Microsoft.WSL")), "unrelated dirs must survive");
        }
        finally
        {
            if (Directory.Exists(fragRoot)) Directory.Delete(fragRoot, recursive: true);
        }
    }

    [Fact]
    public void SweepLegacyFragments_NoFragmentRoot_NoThrow()
    {
        // Pins the !Directory.Exists short-circuit: callers (Launch) invoke
        // sweep before any iter has a chance to create the Fragments root.
        WtLauncher.SweepLegacyFragments(Path.Combine(Path.GetTempPath(),
            "wt-sweep-nonexistent-" + System.Guid.NewGuid()));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WtClassName_MatchesCascadiaHostingWindowClass()
    {
        Assert.Equal("CASCADIA_HOSTING_WINDOW_CLASS", WtHwndLocator.ExpectedWindowClass);
    }

    [Fact(Skip = "Integration: requires WT installed. Unskip manually for smoke.")]
    [SupportedOSPlatform("windows")]
    public void Launch_FillsWindowHandle_Smoke()
    {
        var launcher = new WtLauncher();
        using var handle = launcher.Launch(new LaunchRequest(
            TargetExePath: WtAutoResolver.Resolve("auto"),
            ShellCommand: "pwsh -NoLogo -NoProfile -Command \"exit 0\"",
            ConfigOverrides: new System.Collections.Generic.Dictionary<string, string>(),
            Cols: 80,
            Rows: 24));

        Assert.NotNull(handle.WindowHandle);
        Assert.NotEqual(0, handle.WindowHandle!.Value);
    }
}
