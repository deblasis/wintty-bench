using System.Diagnostics;

namespace WinttyBench.Launchers;

public sealed class WtLauncher : ILauncher
{
    public string Name => "windows-terminal";

    // wt.exe is a launcher stub; the window-hosting process is WindowsTerminal.exe.
    // Timing wt.exe would miss the actual window startup.
    public string ExpectedProcessName => "WindowsTerminal";

    public LaunchHandle Launch(LaunchRequest request)
    {
        var configRoot = Path.Combine(Path.GetTempPath(), "wt-bench-" + Guid.NewGuid());
        WriteSettings(configRoot, request.ShellCommand);

        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = "-p WinttyBenchProfile",
            UseShellExecute = false,
        };
        foreach (var (k, v) in BuildEnv(configRoot))
        {
            startInfo.Environment[k] = v;
        }

        Process.Start(startInfo);

        // wt.exe returns before WindowsTerminal.exe finishes starting.
        // Poll for WindowsTerminal.exe with a generous timeout.
        var measurable = MeasurableProcess.WaitForProcessByName("WindowsTerminal", TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException(
                "WindowsTerminal.exe did not appear within 10s after wt.exe invocation");

        return new LaunchHandle
        {
            Process = measurable,
            ConfigRoot = configRoot,
        };
    }

    public static void WriteSettings(string settingsRoot, string shellCommand)
    {
        Directory.CreateDirectory(settingsRoot);
        var json = $$"""
{
  "profiles": {
    "list": [
      {
        "name": "WinttyBenchProfile",
        "commandline": "{{shellCommand}}",
        "hidden": false,
        "startingDirectory": "%USERPROFILE%"
      }
    ]
  },
  "defaultProfile": "WinttyBenchProfile"
}
""";
        File.WriteAllText(Path.Combine(settingsRoot, "settings.json"), json);
    }

    public static Dictionary<string, string> BuildEnv(string settingsRoot) => new()
    {
        ["WT_SETTINGS_PATH"] = settingsRoot,
    };
}
