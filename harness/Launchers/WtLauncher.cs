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

        // Use request.TargetExePath, not "wt.exe" -- the latter resolves
        // via PATH to the Store-install wt.exe (App Execution Aliases),
        // which would defeat the whole point of --target-wt=auto picking
        // a portable WT. The portable path is resolved by WtAutoResolver
        // before we get here.
        var startInfo = new ProcessStartInfo
        {
            FileName = request.TargetExePath,
            Arguments = "-p WinttyBenchProfile",
            UseShellExecute = false,
        };
        // ProcessStartInfo.Environment is pre-populated with the parent's env;
        // these entries override-or-add, they do not replace.
        foreach (var (k, v) in BuildEnv(configRoot))
        {
            startInfo.Environment[k] = v;
        }

        // Snapshot which CASCADIA-owning PIDs already exist before we
        // spawn. Reasons it must happen pre-spawn:
        //   1. The user may already have WT open as a daily driver.
        //      Without exclusion, Process.GetProcessesByName returns
        //      whichever WindowsTerminal.exe matches first; that could
        //      be the user's WT, and our JobObject.AssignProcess +
        //      KILL_ON_JOB_CLOSE would kill it on Dispose.
        //   2. WT 1.24 has a wt.exe -> broker -> host process chain.
        //      The broker is transient; if we polled by name we'd
        //      sometimes attach to a PID that exits between
        //      Process.GetProcessesByName and Process.GetProcessById,
        //      throwing ArgumentException("Process with an Id of N is
        //      not running") and aborting the whole run.
        // The fix: identify our host process by its CASCADIA window's
        // owning PID and exclude pre-existing CASCADIA owners.
        var existingHostPids = OperatingSystem.IsWindows()
            ? WtHwndLocator.SnapshotCascadiaPids()
            : new HashSet<int>();

        Process.Start(startInfo);

        // Poll for the NEW WT host: a CASCADIA window whose owning PID
        // is not in the pre-spawn snapshot. The HWND identifies the
        // host visually; its PID is what we attach JobObject to.
        nint hwnd = 0;
        int hostPid = 0;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var found = WtHwndLocator.WaitForNewWtHwnd(existingHostPids, TimeSpan.FromSeconds(10));
                hwnd = found.hwnd;
                hostPid = found.pid;
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    "Windows Terminal host (CASCADIA window owner) did not appear within 10s after wt.exe invocation",
                    ex);
            }
        }

        Process winttermProc;
        try
        {
            winttermProc = Process.GetProcessById(hostPid);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture,
                    $"WT host PID {hostPid} exited between HWND identification and process attach"), ex);
        }

        var measurable = MeasurableProcess.FromProcess(winttermProc, ExpectedProcessName);

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
                jobCandidate.AssignProcess(winttermProc);
                job = jobCandidate;
            }
            catch (InvalidOperationException) { job = null; }
            catch (System.ComponentModel.Win32Exception) { job = null; }
            catch (ArgumentException) { job = null; }
        }

        // Best-effort foreground verification. MSIX apps can lag on
        // becoming foreground after Process.Start; LatencyRunner re-
        // checks foreground per iteration, so swallowing the launch-
        // time race here is safe.
        if (OperatingSystem.IsWindows() && hwnd != 0)
        {
            try { WtHwndLocator.WaitForForeground(hwnd, TimeSpan.FromSeconds(2)); }
            catch (TimeoutException) { /* foreground best-effort */ }
        }

        return new LaunchHandle
        {
            Process = measurable,
            ConfigRoot = configRoot,
            WindowHandle = hwnd != 0 ? hwnd : null,
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
        // JSON-encode shellCommand so internal " or \ don't malform the
        // settings.json. JsonSerializer.Serialize on a string returns the
        // value WITH surrounding quotes, e.g. "bash -c \"x\"" -- embed it
        // raw into the template (no extra outer quotes). Use the source-
        // generated string TypeInfo for AOT/trim safety (IL2026/IL3050).
        var commandlineJson = System.Text.Json.JsonSerializer.Serialize(
            shellCommand, WinttyBench.ResultSchemaContext.Default.String);
        var json = $$"""
{
  "profiles": {
    "list": [
      {
        "name": "WinttyBenchProfile",
        "commandline": {{commandlineJson}},
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
