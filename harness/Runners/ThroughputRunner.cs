using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Kpis;
using WinttyBench.Launchers;

namespace WinttyBench.Runners;

public sealed class ThroughputRunner : IKpiRunner
{
    public async Task<IReadOnlyList<IterationSample>> RunAsync(
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

    private static string BuildShellCommandForCell(Cell cell, string fixtureShellPath, string sentinelPath)
    {
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
        return string.Create(CultureInfo.InvariantCulture,
            $"pwsh -NoProfile -NonInteractive -NoLogo -File {scriptPath}");
    }

    private static string BuildWslCommand(Cell cell, string fixtureWslPath, string scriptsDir, string sentinelPath)
    {
        var scriptPath = Path.Combine(scriptsDir, $"{cell.Id}.sh");
        var sentinelWsl = ToWslMountPath(sentinelPath);
        var body = string.Create(CultureInfo.InvariantCulture,
            $"cat '{fixtureWslPath}'\ntouch '{sentinelWsl}'\nexit\n");
        File.WriteAllText(scriptPath, body.Replace("\r\n", "\n", StringComparison.Ordinal));
        var scriptWsl = ToWslMountPath(scriptPath);
        return string.Create(CultureInfo.InvariantCulture,
            $"wsl -d Ubuntu-24.04 bash {scriptWsl}");
    }

    private static string ToWslMountPath(string windowsPath)
    {
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return string.Create(CultureInfo.InvariantCulture, $"/mnt/{drive}{rest}");
    }
}
