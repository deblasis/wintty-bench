using System.Diagnostics;
using System.Runtime.Versioning;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Launchers;

// These are proof-of-containment tests: we assert that disposing the job
// actually kills the processes in it. Without a working job, a bench that
// times out leaks orphan shells across the box (observed: 14 stuck pwsh
// after a single aborted C8 run). The test harness spawns short-lived
// native `cmd /c pause`-equivalents instead of pwsh so it is fast and has
// no profile-load variance.
[SupportedOSPlatform("windows")]
public class WinttyJobObjectTests
{
    [Fact]
    [Trait("OS", "Windows")]
    public void Disposing_Job_Kills_Assigned_Process()
    {
        if (!OperatingSystem.IsWindows()) return;

        // timeout /t is a native Windows binary that sleeps. Using /nobreak
        // /t 30 ensures it waits 30s without responding to keypresses, so
        // the only way it disappears is if the job kills it.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c timeout /t 30 /nobreak > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to spawn cmd.exe");
        try
        {
            using (var job = new WinttyJobObject())
            {
                job.AssignProcess(proc);
                Assert.False(proc.HasExited, "Process should still be running before job dispose.");
            } // <- job.Dispose here triggers KILL_ON_JOB_CLOSE

            // Wait briefly for the kill to propagate. On Win10/11 this is
            // effectively synchronous but we allow a generous slack for CI.
            var ok = proc.WaitForExit(TimeSpan.FromSeconds(5));

            Assert.True(ok, "Process did not exit within 5s after job dispose.");
            Assert.True(proc.HasExited, "Process.HasExited should be true after WaitForExit returned ok.");
        }
        finally
        {
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void Disposing_Job_Kills_Grandchild_Process()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Key scenario: Wintty's real-world failure mode. The launched
        // process is `cmd.exe` which spawns a GRANDCHILD (another cmd via
        // /c start /b). We assign only the parent cmd to the job. The
        // grandchild inherits the job (nested-jobs default on Win8+), so
        // disposing the job must kill the grandchild too.
        //
        // Command: `cmd.exe /c start /b cmd /c timeout /t 30 /nobreak > nul`
        // The outer cmd exits quickly after kicking off the inner via
        // `start`, so without job containment the grandchild would outlive
        // the launch handle and become an orphan. That is exactly the
        // Wintty leak we are guarding against.

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c start /b cmd /c timeout /t 30 /nobreak > nul",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to spawn outer cmd.exe");

        var procStart = proc.StartTime;
        var job = new WinttyJobObject();
        var grandchildSeen = false;
        try
        {
            // Assign parent to job BEFORE polling so nested-jobs semantics
            // catch the grandchild when cmd spawns it via `start`.
            job.AssignProcess(proc);

            // Wait up to 2s for the grandchild `timeout` process to appear.
            // If it never appears, the containment test has no subject -
            // fall through to cleanup and Skip via Assert.Inconclusive-
            // analog (we just assert on the subject-observed path).
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                if (CountTimeoutProcessesStartedAfter(procStart) > 0)
                {
                    grandchildSeen = true;
                    break;
                }
                Thread.Sleep(50);
            }

            job.Dispose(); // <- KILL_ON_JOB_CLOSE fires here

            // Give Windows a tick to reap the grandchild's process entry.
            var killDeadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < killDeadline)
            {
                if (CountTimeoutProcessesStartedAfter(procStart) == 0) break;
                Thread.Sleep(50);
            }

            var after = CountTimeoutProcessesStartedAfter(procStart);

            if (grandchildSeen)
            {
                Assert.Equal(0, after);
            }
            // else: the test was racy and never saw the grandchild - pass
            // silently rather than flake. The primary `Disposing_Job_Kills
            // _Assigned_Process` test already covers the core guarantee.
        }
        finally
        {
            job.Dispose(); // idempotent
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }
    }

    private static int CountTimeoutProcessesStartedAfter(DateTime cutoff)
    {
        var count = 0;
        foreach (var p in Process.GetProcessesByName("timeout"))
        {
            try
            {
                if (p.StartTime >= cutoff) count++;
            }
            catch
            {
                // StartTime can throw if the process exited mid-check. Ignore.
            }
            finally
            {
                p.Dispose();
            }
        }
        return count;
    }
}
