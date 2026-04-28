using System;
using System.IO;
using System.Linq;

namespace WinttyBench.Launchers;

public static class WtAutoResolver
{
    // Resolves --target-wt input to an actual wt.exe path. "auto" tries
    // standard install locations in order; an explicit path is verified
    // for existence and returned. Hard-fails (throws) on miss; the harness
    // surfaces this before launching the first cell.
    //
    // Resolution order for "auto":
    //   1. Portable WT cache (~/.cache/wintty-bench/wt/<version>/wt.exe), highest version wins.
    //      Preferred because Store WT is single-instance per AppX identity — new
    //      `wt.exe` spawns open as tabs in the user's daily-driver window, which
    //      breaks process isolation for the bench.
    //   2. Microsoft Store install via App Execution Aliases (with stderr warning).
    //   3. PATH lookup (rare; for portable installs added to PATH manually).
    public static string Resolve(string spec)
    {
        ArgumentException.ThrowIfNullOrEmpty(spec);

        if (!string.Equals(spec, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(spec))
                throw new FileNotFoundException($"wt.exe not found at '{spec}'.", spec);
            return spec;
        }

        // 1. Portable WT cache — preferred to avoid Monarch single-instance behavior.
        // Read $USERPROFILE from env first (matches the setup-wt-portable.ps1 path
        // exactly and lets tests redirect the cache by setting the env var); fall
        // back to SpecialFolder.UserProfile if the env var is unset.
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(userProfile))
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var portableRoot = Path.Combine(userProfile, ".cache", "wintty-bench", "wt");
        if (Directory.Exists(portableRoot))
        {
            var versionDirs = Directory.GetDirectories(portableRoot)
                .Where(d => File.Exists(Path.Combine(d, "wt.exe")))
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (versionDirs.Count > 0)
            {
                return Path.Combine(versionDirs[0], "wt.exe");
            }
        }

        // 2. Microsoft Store install via App Execution Aliases — works but warns
        //    because the Store install is single-instance per user (Monarch).
        var localApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(localApps))
        {
            Console.Error.WriteLine(
                $"warning: WT auto-resolved to Store install at {localApps}. " +
                "Single-instance behavior may interfere with your daily-driver WT. " +
                "Run scripts/setup-wt-portable.ps1 to install a portable WT for clean comparison.");
            return localApps;
        }

        // 3. PATH lookup
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "wt.exe");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            "wt.exe could not be auto-resolved. Tried portable cache (~/.cache/wintty-bench/wt/), " +
            "%LocalAppData%\\Microsoft\\WindowsApps\\wt.exe, and PATH. " +
            "Run scripts/setup-wt-portable.ps1 or pass --target-wt=<explicit path>.");
    }
}
