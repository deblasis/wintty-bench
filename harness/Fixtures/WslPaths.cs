using System.Globalization;

namespace WinttyBench.Fixtures;

// Shared Windows <-> WSL path conversion. Previously inlined in
// ThroughputRunner, FixtureResolver, and a test-side WslHelpers; kept
// in one place so future-drive-letter or UNC handling only needs to
// change once.
public static class WslPaths
{
    // C:\foo\bar -> /mnt/c/foo/bar
    // Assumes an absolute drive-letter path; UNC paths (\\server\share\...)
    // are not supported. All current callers pass %TEMP%-derived or
    // repo-rooted paths, which are drive-letter absolute.
    public static string ToWslMountPath(string windowsPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowsPath);
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return string.Create(CultureInfo.InvariantCulture, $"/mnt/{drive}{rest}");
    }
}
