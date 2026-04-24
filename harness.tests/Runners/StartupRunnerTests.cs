using System.Diagnostics;
using WinttyBench;
using WinttyBench.Cells;
using WinttyBench.Fixtures;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

public class StartupRunnerTests
{
    // This test is NOT marked Integration and is NOT Skip'd: it is the
    // cheap end-to-end verification that the pwsh command we build works.
    // Bypasses Wintty / Ghostty config entirely - spawns pwsh directly via
    // Process.Start with the exact command StartupRunner would issue.
    //
    // Catches (fast, every `dotnet test`):
    // - Missing `-NoExit` regressions: pwsh would exit without ever hitting
    //   the prompt override, sentinel never fires, test times out.
    // - Command-quoting / encoding regressions (e.g. switching away from
    //   -EncodedCommand back to raw -Command and tripping a quoting layer).
    // - pwsh availability on PATH (PowerShell 7.x is required).
    //
    // Requires: pwsh on PATH. Windows-only: pwsh is not guaranteed elsewhere
    // and Wintty itself is Windows-only, so this test class is Windows-only.
    [Fact]
    [Trait("OS", "Windows")]
    public void BuildPwshStartupCommand_FiresSentinelInPwsh_DirectProcess()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(),
            "pwsh-7 + Wintty is Windows-only for this bench");

        var sentinelPath = Path.Combine(
            Path.GetTempPath(),
            $"wintty-bench-test-{Guid.NewGuid():N}.marker");
        if (File.Exists(sentinelPath)) File.Delete(sentinelPath);

        // Same command StartupRunner would hand to Wintty's config.command.
        var command = StartupRunner.BuildPwshStartupCommand(sentinelPath);

        // Split "pwsh <args...>" into exe + args for ProcessStartInfo.
        var firstSpace = command.IndexOf(' ', StringComparison.Ordinal);
        var exe = command[..firstSpace];
        var args = command[(firstSpace + 1)..];

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Explicit empty stdin: avoids inheriting test-runner stdin which
            // would deadlock pwsh on first readline after the override fires.
            RedirectStandardInput = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pwsh (not on PATH?)");

        // Close stdin immediately so pwsh's readline sees EOF if the prompt
        // override somehow fails to call exit - prevents a hang past exit.
        proc.StandardInput.Close();

        // Sentinel should appear well under 5s on any developer machine;
        // pwsh 7.x cold start is typically ~200-500ms.
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(10);
        while (!File.Exists(sentinelPath) && sw.Elapsed < timeout)
        {
            Thread.Sleep(25);
        }

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(TimeSpan.FromSeconds(5));

        try
        {
            Assert.True(
                File.Exists(sentinelPath),
                $"Sentinel file was not created within {timeout.TotalSeconds}s.\n" +
                $"Command: {command}\n" +
                $"pwsh stdout: {stdout}\n" +
                $"pwsh stderr: {stderr}");
            Assert.True(proc.HasExited, "pwsh did not exit after firing the sentinel.");
        }
        finally
        {
            try { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); }
            catch (IOException) { /* best effort */ }
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    // Real Wintty end-to-end. Still Skip'd by default because it requires a
    // built Wintty.exe; unskip locally when changing StartupRunner internals.
    [Trait("Category", "Integration")]
    [Fact(Skip = "Integration: requires real Wintty.exe. Unskip locally and set WINTTY_EXE.")]
    public async Task StartupRunner_EmitsPositiveSeconds_ForPwsh()
    {
        var exe = Environment.GetEnvironmentVariable("WINTTY_EXE")
            ?? throw new InvalidOperationException("Set WINTTY_EXE to the Wintty.exe path");

        var cell = new Cell(
            Id: "C8",
            Shell: "pwsh-7.4",
            Workload: "shell_startup",
            Kpi: "startup_seconds",
            FixturePath: null,
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>());

        var profile = FairnessProfile.Ci() with { WarmupIters = 0, MeasuredIters = 1 };
        var resolver = new FixtureResolver(new WslFixtureCache());
        var runner = new StartupRunner();

        var samples = await runner.RunAsync(cell, exe, profile, resolver);

        Assert.Single(samples);
        Assert.False(samples[0].Hung);
        Assert.NotNull(samples[0].Value);
        Assert.InRange(samples[0].Value!.Value, 0.1, 30.0);
    }
}
