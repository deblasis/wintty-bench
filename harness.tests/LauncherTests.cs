using WinttyBench;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests;

public class LauncherTests
{
    [Fact]
    public void WinttyLauncher_Writes_ConfigFile_With_Overrides()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "wintty-bench-test-" + Guid.NewGuid());
        try
        {
            var overrides = new Dictionary<string, string>
            {
                ["utf8-console"] = "force",
                ["font-family"] = "DejaVu Sans Mono",
            };

            WinttyLauncher.WriteConfig(tempRoot, overrides, templatePath: null);

            var configPath = Path.Combine(tempRoot, "ghostty", "config");
            Assert.True(File.Exists(configPath));
            var content = File.ReadAllText(configPath);
            Assert.Contains("utf8-console = force", content);
            Assert.Contains("font-family = DejaVu Sans Mono", content);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void WinttyLauncher_Env_Sets_XDG_CONFIG_HOME()
    {
        var tempRoot = "C:\\temp\\wintty-bench-x";
        var env = WinttyLauncher.BuildEnv(tempRoot);

        Assert.Equal(tempRoot, env["XDG_CONFIG_HOME"]);
    }

    [Fact]
    public void WtLauncher_ExpectedProcessName_Is_WindowsTerminal_Not_Wt()
    {
        var launcher = new WtLauncher();
        Assert.Equal("WindowsTerminal", launcher.ExpectedProcessName);
    }

    // WT settings handoff tests live in Launchers/WtLauncherTests.cs alongside
    // the rest of the WtLauncher coverage.
}
