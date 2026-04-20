using System.Diagnostics;
using System.Globalization;
using WinttyBench.Cells;
using WinttyBench.Launchers;

namespace WinttyBench;

public static class MeasurementRunner
{
    public static IReadOnlyList<double> RunThroughput(Cell cell, string winttyExe, FairnessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(profile);

        if (!File.Exists(cell.FixturePath))
            throw new FileNotFoundException($"Fixture not found: {cell.FixturePath}");

        var launcher = new WinttyLauncher();
        var totalIters = profile.WarmupIters + profile.MeasuredIters;
        var times = new List<double>(profile.MeasuredIters);

        for (var i = 0; i < totalIters; i++)
        {
            var isWarmup = i < profile.WarmupIters;
            var shellCmd = BuildShellCommandForCell(cell);

            var launch = launcher.Launch(new LaunchRequest(
                TargetExePath: winttyExe,
                ShellCommand: shellCmd,
                ConfigOverrides: cell.WinttyConfigOverrides,
                Cols: 120,
                Rows: 32));

            var sw = Stopwatch.StartNew();
            try
            {
                // Wait for wintty process to exit on its own (shell -c finishes).
                var proc = Process.GetProcessById(launch.Process.ProcessId);
                proc.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds);
            }
            finally
            {
                sw.Stop();
                launch.Dispose();
            }

            if (!isWarmup)
            {
                times.Add(sw.Elapsed.TotalSeconds);
            }
        }

        return times;
    }

    private static string BuildShellCommandForCell(Cell cell)
    {
        // Shell runs inside wintty; the outer process exits when the shell exits.
        // pwsh: Get-Content then Write-Host NoNewline then exit.
        // WSL: cat then exit.
        var fixtureAbs = Path.GetFullPath(cell.FixturePath);
        return cell.Shell switch
        {
            "pwsh-7.4" => string.Create(CultureInfo.InvariantCulture,
                $"pwsh -NoLogo -Command \"Get-Content -Raw '{fixtureAbs}' | Write-Host -NoNewline; exit\""),
            "wsl-ubuntu-24.04" => string.Create(CultureInfo.InvariantCulture,
                $"wsl -d Ubuntu-24.04 -- bash -c \"cat '{ToWslPath(fixtureAbs)}'; exit\""),
            _ => throw new NotSupportedException($"Shell '{cell.Shell}' not supported in MVP"),
        };
    }

    private static string ToWslPath(string windowsPath)
    {
        // C:\foo\bar -> /mnt/c/foo/bar
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return string.Create(CultureInfo.InvariantCulture, $"/mnt/{drive}{rest}");
    }
}
