using System.Diagnostics;
using System.Text;
using WinttyBench.Input;

namespace WinttyBench.Launchers;

public sealed class WinttyLauncher : ILauncher
{
    public string Name => "wintty";
    public string ExpectedProcessName => "Wintty";

    public LaunchHandle Launch(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configRoot = Path.Combine(Path.GetTempPath(), "wintty-bench-" + Guid.NewGuid());

        // Pass the shell command through the config file's `command` key
        // rather than -e argv. Wintty's WinUI entry on Windows does not
        // forward -e to libghostty reliably (it falls back to the default
        // shell, cmd.exe), but config.command is read by the surface
        // initializer before termio spawns the child.
        var overrides = new Dictionary<string, string>(request.ConfigOverrides, StringComparer.Ordinal)
        {
            ["command"] = request.ShellCommand,
        };
        WriteConfig(configRoot, overrides, templatePath: null);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.TargetExePath,
            Arguments = string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        // ProcessStartInfo.Environment is pre-populated with the parent's env;
        // these entries override-or-add, they do not replace.
        foreach (var (k, v) in BuildEnv(configRoot))
        {
            startInfo.Environment[k] = v;
        }

        var proc = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start wintty at {request.TargetExePath}");

        // Attach to a JobObject with KILL_ON_JOB_CLOSE BEFORE Wintty has had
        // time to spawn its child shell. Process.Start returns synchronously
        // after CreateProcess, so there is an inherent race; in practice
        // Wintty's libghostty surface init happens tens of ms later, which
        // is enough. Children spawned after assignment inherit the job on
        // Win8+ (nested-jobs is default). The job is what guarantees
        // descendants die when we dispose the handle - Process.Kill
        // (entireProcessTree: true) cannot reach re-parented orphans.
        WinttyJobObject? job = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                job = new WinttyJobObject();
                job.AssignProcess(proc);
            }
            catch
            {
                // Inner guard required: CA1416 does not flow the outer
                // OperatingSystem.IsWindows() branch into this catch block.
                if (OperatingSystem.IsWindows())
                {
                    job?.Dispose();
                }
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                throw;
            }
        }

        // Discover the top-level HWND so LatencyRunner can target SendInput
        // and open a WGC capture session without having to re-walk the
        // process tree itself. Non-C13 cells (Startup, MemoryRss) don't
        // need a window handle and finish before the timeout matters; if
        // the lookup times out we leave WindowHandle null and let
        // LatencyRunner throw separately when (and only if) it tries to
        // use it. CA1416 does not flow the outer OS check into the inner
        // call site, so the IsWindows() guard is required around the
        // HwndLocator call even though the surrounding launcher is
        // Windows-only in practice.
        nint? hwnd = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                hwnd = HwndLocator.WaitForWinttyHwnd(proc.Id, TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                // Swallowed: see comment above. Only LatencyRunner cares.
            }
        }

        // Wintty is self-hosted: the spawned exe IS the measurable process.
        return new LaunchHandle
        {
            Process = MeasurableProcess.FromProcess(proc, ExpectedProcessName),
            ConfigRoot = configRoot,
            Job = job,
            WindowHandle = hwnd,
        };
    }

    // Baseline keys every bench run needs, regardless of cell. Without
    // quit-after-last-window-closed=true, Wintty stays alive after the
    // shell exits on Windows (Linux-only default upstream) and the bench
    // hangs on WaitForExit.
    public static IReadOnlyDictionary<string, string> BaselineOverrides { get; } =
        new Dictionary<string, string>
        {
            ["quit-after-last-window-closed"] = "true",
            ["wait-after-command"] = "false",
        };

    public static void WriteConfig(string configRoot, IReadOnlyDictionary<string, string> overrides, string? templatePath)
    {
        var ghosttyDir = Path.Combine(configRoot, "ghostty");
        Directory.CreateDirectory(ghosttyDir);

        var sb = new StringBuilder();
        if (templatePath is not null && File.Exists(templatePath))
        {
            sb.Append(File.ReadAllText(templatePath));
            sb.AppendLine();
        }
        foreach (var (k, v) in BaselineOverrides)
        {
            sb.Append(k).Append(" = ").AppendLine(v);
        }
        foreach (var (k, v) in overrides)
        {
            // Plain Append calls dodge CA1305 (no interpolated handler, no
            // IFormatProvider needed) and ghostty config keys/values are
            // already locale-invariant identifiers.
            sb.Append(k).Append(" = ").AppendLine(v);
        }

        File.WriteAllText(Path.Combine(ghosttyDir, "config"), sb.ToString());
    }

    public static Dictionary<string, string> BuildEnv(string configRoot) => new()
    {
        ["XDG_CONFIG_HOME"] = configRoot,
    };
}
