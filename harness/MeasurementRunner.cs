using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Launchers;

namespace WinttyBench;

public static class MeasurementRunner
{
    // Wintty's WinUI 3 shell does not quit on last-window-closed on Windows
    // even with quit-after-last-window-closed=true in config, so the harness
    // drives the lifecycle itself: wait for a sentinel file that the shell
    // script writes on its last line, then stop the clock and kill Wintty.
    private static readonly TimeSpan ShellExitTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SentinelPollInterval = TimeSpan.FromMilliseconds(25);

    public static IReadOnlyList<double> RunThroughput(Cell cell, string winttyExe, FairnessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);

        // TODO(Plan 2A Task 5): resolve via FixtureResolver when FixtureKey is set
        if (!File.Exists(cell.FixturePath!))
            throw new FileNotFoundException($"Fixture not found: {cell.FixturePath!}");

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var times = new List<double>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var sentinelPath = Path.Combine(Path.GetTempPath(),
                $"wintty-bench-done-{Guid.NewGuid():N}.marker");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            var shellCmd = BuildShellCommandForCell(cell, sentinelPath);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            try
            {
                WaitForSentinel(sentinelPath, ShellExitTimeout);
            }
            finally
            {
                sw.Stop();
                launch.Dispose();
                try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); }
                catch (IOException) { /* best effort */ }
            }

            if (!isWarmup)
            {
                times.Add(sw.Elapsed.TotalSeconds);
            }
        }

        return times;
    }

    private static void WaitForSentinel(string sentinelPath, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(sentinelPath)) return;
            Thread.Sleep(SentinelPollInterval);
        }
        throw new TimeoutException($"Sentinel '{sentinelPath}' not observed in {timeout}; shell did not finish.");
    }

    private static string BuildShellCommandForCell(Cell cell, string sentinelPath)
    {
        // Ghostty's Windows termio wraps any command containing cmd.exe
        // metacharacters ('|', '&', '<', '>', '(', ')', '^', '%', '!') in
        // `cmd.exe /c ...`, which mangles nested quotes from a `pwsh
        // -Command "..."` form. Put the shell body in a temp script file
        // instead so the `command = ...` value in the config is a plain
        // argv with no shell metacharacters.
        // TODO(Plan 2A Task 5): resolve via FixtureResolver when FixtureKey is set
        var fixtureAbs = Path.GetFullPath(cell.FixturePath!);
        var scriptsDir = Path.Combine(Path.GetTempPath(), "wintty-bench-scripts");
        Directory.CreateDirectory(scriptsDir);

        return cell.Shell switch
        {
            "pwsh-7.4" => BuildPwshCommand(cell, fixtureAbs, scriptsDir, sentinelPath),
            "wsl-ubuntu-24.04" => BuildWslCommand(cell, fixtureAbs, scriptsDir, sentinelPath),
            _ => throw new NotSupportedException($"Shell '{cell.Shell}' not supported in MVP"),
        };
    }

    private static string BuildPwshCommand(Cell cell, string fixtureAbs, string scriptsDir, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, $"{cell.Id}.ps1");
        // Sentinel is written after Write-Host completes but before `exit`:
        // the stopwatch includes the render, not pwsh teardown.
        var body = string.Create(CultureInfo.InvariantCulture,
            $"Get-Content -Raw -LiteralPath '{fixtureAbs}' | Write-Host -NoNewline\nNew-Item -ItemType File -Force -Path '{sentinelPath}' | Out-Null\nexit\n");
        File.WriteAllText(scriptPath, body);
        // -NoProfile skips ~1-2s of profile loading per launch. -NonInteractive
        // guarantees no hidden prompt can wedge the measurement.
        return string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoProfile -NonInteractive -NoLogo -File {scriptPath}");
    }

    private static string BuildWslCommand(Cell cell, string fixtureAbs, string scriptsDir, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, $"{cell.Id}.sh");
        var fixtureWsl = ToWslPath(fixtureAbs);
        var sentinelWsl = ToWslPath(sentinelPath);
        // Note: LF line endings; bash under WSL rejects CRLF script lines.
        var body = string.Create(CultureInfo.InvariantCulture,
            $"cat '{fixtureWsl}'\ntouch '{sentinelWsl}'\nexit\n");
        File.WriteAllText(scriptPath, body.Replace("\r\n", "\n", StringComparison.Ordinal));
        var scriptWsl = ToWslPath(scriptPath);
        return string.Create(CultureInfo.InvariantCulture,
            $"wsl -d Ubuntu-24.04 bash {scriptWsl}");
    }

    private static string ToWslPath(string windowsPath)
    {
        // C:\foo\bar -> /mnt/c/foo/bar
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return string.Create(CultureInfo.InvariantCulture, $"/mnt/{drive}{rest}");
    }
}
