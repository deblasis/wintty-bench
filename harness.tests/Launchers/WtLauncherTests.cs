using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Launchers;

public class WtLauncherTests
{
    [Fact]
    public void WriteSettings_PinsInitialColsRowsAndCloseOnExit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wt-launcher-test-" + System.Guid.NewGuid());
        try
        {
            WtLauncher.WriteSettings(dir, "pwsh -NoLogo", cols: 120, rows: 32);

            var settingsPath = Path.Combine(dir, "settings.json");
            Assert.True(File.Exists(settingsPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var profile = doc.RootElement.GetProperty("profiles").GetProperty("list")[0];

            Assert.Equal("pwsh -NoLogo", profile.GetProperty("commandline").GetString());
            Assert.Equal(120, profile.GetProperty("initialCols").GetInt32());
            Assert.Equal(32, profile.GetProperty("initialRows").GetInt32());
            Assert.Equal("always", profile.GetProperty("closeOnExit").GetString());
            Assert.Equal("WinttyBenchProfile", profile.GetProperty("name").GetString());

            var defaultProfile = doc.RootElement.GetProperty("defaultProfile").GetString();
            Assert.Equal("WinttyBenchProfile", defaultProfile);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteSettings_EscapesEmbeddedQuotes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wt-launcher-test-" + System.Guid.NewGuid());
        try
        {
            WtLauncher.WriteSettings(dir, "bash -c \"echo hi\"", cols: 80, rows: 24);
            var settingsPath = Path.Combine(dir, "settings.json");
            var content = File.ReadAllText(settingsPath);
            // The JSON must parse without error and the commandline must round-trip.
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var profile = doc.RootElement.GetProperty("profiles").GetProperty("list")[0];
            Assert.Equal("bash -c \"echo hi\"", profile.GetProperty("commandline").GetString());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void BuildEnv_SetsWtSettingsPath()
    {
        var env = WtLauncher.BuildEnv("C:/foo/bar");
        Assert.Single(env);
        Assert.Equal("C:/foo/bar", env["WT_SETTINGS_PATH"]);
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
