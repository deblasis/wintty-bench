using System;
using System.IO;

namespace WinttyBench.Launchers;

public static class WtAutoResolver
{
    // Resolves --target-wt input to an actual wt.exe path. "auto" tries
    // standard install locations in order; an explicit path is verified
    // for existence and returned. Hard-fails (throws) on miss; the harness
    // surfaces this before launching the first cell.
    public static string Resolve(string spec)
    {
        ArgumentException.ThrowIfNullOrEmpty(spec);

        if (!string.Equals(spec, "auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(spec))
                throw new FileNotFoundException($"wt.exe not found at '{spec}'.", spec);
            return spec;
        }

        // 1. Microsoft Store install via App Execution Aliases
        var localApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(localApps)) return localApps;

        // 2. PATH lookup
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "wt.exe");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException(
            "wt.exe could not be auto-resolved. Tried %LocalAppData%\\Microsoft\\WindowsApps\\wt.exe and PATH. " +
            "Pass --target-wt=<explicit path> or install Windows Terminal from the Microsoft Store.");
    }
}
