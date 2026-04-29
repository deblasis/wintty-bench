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

        // Stable fragment dir (NOT per-iter). WT auto-derives a profile
        // GUID from (source, name); per-iter source dirs caused every
        // iteration to register a NEW WinttyBenchProfile entry in the
        // user's settings.json. Old entries persisted as orphans (empty
        // commandline) and `wt -p WinttyBenchProfile` resolved to one of
        // them, never to the freshly-written fragment. A single stable
        // source means exactly one profile entry whose commandline gets
        // overwritten each iter.
        var fragmentRoot = GetFragmentRoot();
        SweepLegacyFragmentsOnce(fragmentRoot);

        var fragmentDir = Path.Combine(fragmentRoot, "wintty-bench");
        WriteFragment(fragmentDir, request.ShellCommand, request.Cols, request.Rows);

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
            ConfigRoot = fragmentDir,
            WindowHandle = hwnd != 0 ? hwnd : null,
            Job = job,
        };
    }

    // Fragments root: %LOCALAPPDATA%\Microsoft\Windows Terminal\Fragments\.
    // Empirically, both Store and unpackaged Windows Terminal load fragments
    // from this canonical path. WT_SETTINGS_PATH is honored only by Store WT
    // (and only for the main settings.json, not fragments), which is why an
    // earlier WT_SETTINGS_PATH-based handoff silently fell through to the
    // user's existing settings on portable WT.
    public static string GetFragmentRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows Terminal", "Fragments");

    private static int s_swept;

    private static void SweepLegacyFragmentsOnce(string fragmentRoot)
    {
        // Interlocked guard so concurrent first-launches in the same process
        // only sweep once. The bench is single-threaded today, but the static
        // is defense-in-depth for future parallel callers.
        //
        // Pattern wintty-bench-* (with trailing hyphen) targets only the
        // legacy GUID-suffixed dirs from an earlier broken iteration; the
        // current stable dir is plain "wintty-bench" (no hyphen) so it's
        // not matched.
        if (Interlocked.Exchange(ref s_swept, 1) != 0) return;
        try
        {
            if (!Directory.Exists(fragmentRoot)) return;
            foreach (var d in Directory.EnumerateDirectories(fragmentRoot, "wintty-bench-*"))
            {
                try { Directory.Delete(d, recursive: true); }
                catch (IOException) { /* best effort */ }
                catch (UnauthorizedAccessException) { /* best effort */ }
            }
        }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    public static void WriteFragment(string fragmentDir, string shellCommand, int cols, int rows)
    {
        Directory.CreateDirectory(fragmentDir);
        // Numbers formatted via InvariantCulture to keep the fragment JSON
        // locale-independent (CA1305). The interpolated handler would
        // otherwise pick up CurrentCulture and produce e.g. "1 024" on
        // some locales.
        var colsStr = cols.ToString(CultureInfo.InvariantCulture);
        var rowsStr = rows.ToString(CultureInfo.InvariantCulture);
        // JSON-encode shellCommand so internal " or \ don't malform the
        // fragment. JsonSerializer.Serialize on a string returns the value
        // WITH surrounding quotes, e.g. "bash -c \"x\"" -- embed it raw
        // into the template (no extra outer quotes). Use the source-
        // generated string TypeInfo for AOT/trim safety (IL2026/IL3050).
        var commandlineJson = System.Text.Json.JsonSerializer.Serialize(
            shellCommand, WinttyBench.ResultSchemaContext.Default.String);
        // Fragment shape differs from settings.json: top-level "profiles"
        // is an array, not nested under "list", and defaultProfile cannot
        // be set from a fragment. The bench selects this profile explicitly
        // via `wt -p WinttyBenchProfile`, so additive merge is sufficient
        // and non-destructive to the user's existing WT settings.
        var json = $$"""
{
  "profiles": [
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
}
""";
        // Filename inside the fragment dir is arbitrary; WT loads any
        // *.json under Fragments\<source>\. Match the dir name so a stray
        // fragment is easy to attribute during debugging.
        var fragmentName = Path.GetFileName(fragmentDir
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        File.WriteAllText(Path.Combine(fragmentDir, fragmentName + ".json"), json);
    }
}
