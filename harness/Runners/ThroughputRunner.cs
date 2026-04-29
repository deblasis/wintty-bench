using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

// Wintty's WinUI 3 shell does not quit on last-window-closed on Windows
// even with quit-after-last-window-closed=true in config, so the harness
// drives the lifecycle itself: wait for a sentinel file that the shell
// script writes on its last line, then stop the clock and kill Wintty.
public sealed class ThroughputRunner : IKpiRunner
{
    public IReadOnlyList<string> SupportedTerminals { get; } = ["wintty", "wt"];

    public async Task<IReadOnlyList<IterationSample>> RunAsync(
        Cell cell,
        string terminalName,
        string targetExePath,
        FairnessProfile profile,
        FixtureResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentException.ThrowIfNullOrEmpty(terminalName);
        ArgumentException.ThrowIfNullOrEmpty(targetExePath);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(resolver);

        var handle = await resolver.ResolveAsync(cell, profile);

        var launcher = LauncherFactory.For(terminalName);
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var samples = new List<IterationSample>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var sentinelPath = Path.Combine(Path.GetTempPath(),
                $"wintty-bench-done-{Guid.NewGuid():N}.marker");
            if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

            var (shellCmd, scriptPath) = BuildShellCommandForCell(cell, handle.ShellPath, sentinelPath);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: targetExePath,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            var hung = false;
            try
            {
                // Only TimeoutException maps to a hung-sample; any other throw
                // (e.g. OperationCanceledException from a future cancellation
                // caller) bubbles out. The finally block still disposes launch
                // and stops the stopwatch, so resources are clean on abort;
                // dropping partial samples is the right contract because a
                // cancelled run should fail loud, not return a truncated list.
                SentinelWaiter.WaitForSentinel(sentinelPath, SentinelWaiter.DefaultExitTimeout);
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
                // Per-iter script cleanup matches the per-iter script naming
                // (see BuildShellCommandForCell). Leftover scripts would
                // accumulate slowly but more importantly an orphan wsl-bash
                // from a still-running prior iter could no longer race the
                // next iter's overwrite -- each iter has its own file.
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); }
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
                    var bytesPerSec = handle.SizeBytes / sw.Elapsed.TotalSeconds;
                    samples.Add(new IterationSample(Value: bytesPerSec, Hung: false));
                }
            }
        }

        return samples;
    }

    internal static (string Command, string ScriptPath) BuildShellCommandForCell(
        Cell cell, string fixtureShellPath, string sentinelPath)
    {
        // PER-ITER UNIQUE script name (Guid suffix): each iter has its own
        // file, so a still-running prior wsl-bash never observes the next
        // iter's overwritten content. A stable name was racy because vmcompute-
        // managed wsl processes survive Job.KILL_ON_JOB_CLOSE on the WT host;
        // iter N's bash, on its next read, would see iter N+1's content and
        // `touch` iter N+1's sentinel before iter N+1 even started its
        // stopwatch.
        //
        // The script-on-disk approach (rather than inlining into commandline)
        // exists because Ghostty's Windows termio wraps any command containing
        // cmd.exe metacharacters ('|', '&', '<', '>', '(', ')', '^', '%', '!')
        // in `cmd.exe /c ...`, which mangles nested quotes. Putting the shell
        // body in a temp script file keeps the `command = ...` value in the
        // config a plain argv with no shell metacharacters.
        var scriptsDir = Path.Combine(Path.GetTempPath(), "wintty-bench-scripts");
        Directory.CreateDirectory(scriptsDir);
        // Guid:N is hex-only and locale-invariant by definition; no IFormat-
        // Provider needed.
        var scriptStem = $"{cell.Id}-{Guid.NewGuid():N}";

        return cell.Shell switch
        {
            "pwsh-7.4" => BuildPwshCommand(fixtureShellPath, scriptsDir, scriptStem, sentinelPath),
            "wsl-ubuntu-24.04" => BuildWslCommand(fixtureShellPath, scriptsDir, scriptStem, sentinelPath),
            _ => throw new NotSupportedException($"Shell '{cell.Shell}' not supported"),
        };
    }

    private static (string Command, string ScriptPath) BuildPwshCommand(
        string fixtureAbs, string scriptsDir, string scriptStem, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, scriptStem + ".ps1");
        // Single-quoted pwsh literals assume no apostrophes in fixtureAbs or
        // sentinelPath. Current callers pass repo-rooted fixture paths and
        // %TEMP%-derived sentinel paths; both are apostrophe-free on Windows.
        // If a future cell points at a path containing `'`, this needs `''`
        // doubling to stay literal.
        var body = string.Create(CultureInfo.InvariantCulture,
            $"Get-Content -Raw -LiteralPath '{fixtureAbs}' | Write-Host -NoNewline\nNew-Item -ItemType File -Force -Path '{sentinelPath}' | Out-Null\nexit\n");
        File.WriteAllText(scriptPath, body);
        // -NoProfile skips ~1-2s of profile loading per launch. -NonInteractive
        // guarantees no hidden prompt can wedge the measurement.
        var command = string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoProfile -NonInteractive -NoLogo -File {scriptPath}");
        return (command, scriptPath);
    }

    private static (string Command, string ScriptPath) BuildWslCommand(
        string fixtureWslPath, string scriptsDir, string scriptStem, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, scriptStem + ".sh");
        var sentinelWsl = WslPaths.ToWslMountPath(sentinelPath);
        // LF line endings: bash under WSL rejects CRLF script lines. The
        // Replace is defensive — string.Create with \n literals never inserts
        // CRLF today, but a future edit that switches to a template file or
        // verbatim string could, and this keeps the invariant loud.
        var body = string.Create(CultureInfo.InvariantCulture,
            $"cat '{fixtureWslPath}'\ntouch '{sentinelWsl}'\nexit\n");
        File.WriteAllText(scriptPath, body.Replace("\r\n", "\n", StringComparison.Ordinal));
        var scriptWsl = WslPaths.ToWslMountPath(scriptPath);
        var command = string.Create(CultureInfo.InvariantCulture,
            $"wsl -d Ubuntu-24.04 bash {scriptWsl}");
        return (command, scriptPath);
    }
}
