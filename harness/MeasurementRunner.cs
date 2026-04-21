using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
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

    public static async Task<ThroughputRunResult> RunThroughputAsync(
        Cell cell,
        string winttyExe,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var handle = await resolver.ResolveAsync(cell, profile);

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var sentinelPath = Path.Combine(Path.GetTempPath(),
                $"wintty-bench-done-{Guid.NewGuid():N}.marker");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            var shellCmd = BuildShellCommandForCell(cell, handle.ShellPath, sentinelPath);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            var hung = false;
            try
            {
                WaitForSentinel(sentinelPath, ShellExitTimeout);
            }
            catch (TimeoutException)
            {
                hung = true;
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
                if (hung)
                {
                    samples.Add(new IterationSample(Value: null, Hung: true));
                }
                else
                {
                    // Convert wall-seconds to bytes/sec at sample-emit time so
                    // the stats layer downstream stays unit-agnostic.
                    var bytesPerSec = handle.SizeBytes / sw.Elapsed.TotalSeconds;
                    samples.Add(new IterationSample(Value: bytesPerSec, Hung: false));
                }
            }
        }

        return new ThroughputRunResult(samples);
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

    private static string BuildShellCommandForCell(Cell cell, string fixtureShellPath, string sentinelPath)
    {
        // Ghostty's Windows termio wraps any command containing cmd.exe
        // metacharacters ('|', '&', '<', '>', '(', ')', '^', '%', '!') in
        // `cmd.exe /c ...`, which mangles nested quotes. Put the shell body
        // in a temp script file instead so the `command = ...` value in
        // the config is a plain argv with no shell metacharacters.
        var scriptsDir = Path.Combine(Path.GetTempPath(), "wintty-bench-scripts");
        Directory.CreateDirectory(scriptsDir);

        return cell.Shell switch
        {
            "pwsh-7.4" => BuildPwshCommand(cell, fixtureShellPath, scriptsDir, sentinelPath),
            "wsl-ubuntu-24.04" => BuildWslCommand(cell, fixtureShellPath, scriptsDir, sentinelPath),
            _ => throw new NotSupportedException($"Shell '{cell.Shell}' not supported"),
        };
    }

    private static string BuildPwshCommand(Cell cell, string fixtureAbs, string scriptsDir, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, $"{cell.Id}.ps1");
        var body = string.Create(CultureInfo.InvariantCulture,
            $"Get-Content -Raw -LiteralPath '{fixtureAbs}' | Write-Host -NoNewline\nNew-Item -ItemType File -Force -Path '{sentinelPath}' | Out-Null\nexit\n");
        File.WriteAllText(scriptPath, body);
        // -NoProfile skips ~1-2s of profile loading per launch. -NonInteractive
        // guarantees no hidden prompt can wedge the measurement.
        return string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoProfile -NonInteractive -NoLogo -File {scriptPath}");
    }

    private static string BuildWslCommand(Cell cell, string fixtureWslPath, string scriptsDir, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, $"{cell.Id}.sh");
        var sentinelWsl = ToWslMountPath(sentinelPath);
        // Note: LF line endings; bash under WSL rejects CRLF script lines.
        var body = string.Create(CultureInfo.InvariantCulture,
            $"cat '{fixtureWslPath}'\ntouch '{sentinelWsl}'\nexit\n");
        File.WriteAllText(scriptPath, body.Replace("\r\n", "\n", StringComparison.Ordinal));
        var scriptWsl = ToWslMountPath(scriptPath);
        return string.Create(CultureInfo.InvariantCulture,
            $"wsl -d Ubuntu-24.04 bash {scriptWsl}");
    }

    private static string ToWslMountPath(string windowsPath)
    {
        // C:\foo\bar -> /mnt/c/foo/bar
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return string.Create(CultureInfo.InvariantCulture, $"/mnt/{drive}{rest}");
    }
}

public sealed record ThroughputRunResult(IReadOnlyList<IterationSample> Samples);
