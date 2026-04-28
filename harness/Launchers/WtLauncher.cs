using System.Diagnostics;
using System.Globalization;

namespace WinttyBench.Launchers;

public sealed class WtLauncher : ILauncher
{
    public string Name => "windows-terminal";

    // wt.exe is a launcher stub; the window-hosting process is WindowsTerminal.exe.
    // Timing wt.exe would miss the actual window startup.
    public string ExpectedProcessName => "WindowsTerminal";

    public LaunchHandle Launch(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configRoot = Path.Combine(Path.GetTempPath(), "wt-bench-" + Guid.NewGuid());
        WriteSettings(configRoot, request.ShellCommand, request.Cols, request.Rows);

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

        // Best-effort JobObject. MSIX-imposed restrictions may block
        // AssignProcessToJobObject for packaged apps in some Windows
        // versions; on failure, leave Job=null and rely on
        // LaunchHandle.Dispose's Process.Kill(entireProcessTree) fallback.
        WinttyJobObject? job = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var jobCandidate = new WinttyJobObject();
                var winttermProc = Process.GetProcessById(measurable.ProcessId);
                jobCandidate.AssignProcess(winttermProc);
                job = jobCandidate;
            }
            catch (InvalidOperationException) { job = null; }
            catch (System.ComponentModel.Win32Exception) { job = null; }
        }

        // HWND lookup, then foreground verification. Both are best-effort:
        // non-C13 cells don't need a window handle, and MSIX apps can lag
        // on becoming foreground after Process.Start. LatencyRunner re-
        // checks foreground per iteration, so swallowing the launch-time
        // race here is safe.
        nint? hwnd = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                hwnd = WtHwndLocator.WaitForWtHwnd(measurable.ProcessId, TimeSpan.FromSeconds(5));
                try { WtHwndLocator.WaitForForeground(hwnd.Value, TimeSpan.FromSeconds(2)); }
                catch (TimeoutException) { /* foreground best-effort */ }
            }
            catch (TimeoutException) { /* HWND best-effort */ }
        }

        return new LaunchHandle
        {
            Process = measurable,
            ConfigRoot = configRoot,
            WindowHandle = hwnd,
            Job = job,
        };
    }

    public static void WriteSettings(string settingsRoot, string shellCommand, int cols, int rows)
    {
        Directory.CreateDirectory(settingsRoot);
        // Numbers are formatted via InvariantCulture to keep settings.json
        // locale-independent (CA1305). The interpolated handler would
        // otherwise pick up CurrentCulture and produce e.g. "1 024" on
        // some locales.
        var colsStr = cols.ToString(CultureInfo.InvariantCulture);
        var rowsStr = rows.ToString(CultureInfo.InvariantCulture);
        var json = $$"""
{
  "profiles": {
    "list": [
      {
        "name": "WinttyBenchProfile",
        "commandline": "{{shellCommand}}",
        "initialCols": {{colsStr}},
        "initialRows": {{rowsStr}},
        "closeOnExit": "always",
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
