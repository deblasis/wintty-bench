using System.Collections.Frozen;
using System.IO;
using WinttyBench.Cells;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

public class ThroughputRunnerTests
{
    private static readonly Cell PwshCell = new(
        Id: "TEST",
        Shell: "pwsh-7.4",
        Workload: "test_workload",
        Kpi: "throughput_bytes_per_sec",
        FixturePath: "fixtures/test.txt",
        FixtureKey: null,
        WinttyConfigOverrides: FrozenDictionary<string, string>.Empty);

    private static readonly Cell WslCell = new(
        Id: "TEST",
        Shell: "wsl-ubuntu-24.04",
        Workload: "test_workload",
        Kpi: "throughput_bytes_per_sec",
        FixturePath: "fixtures/test.txt",
        FixtureKey: null,
        WinttyConfigOverrides: FrozenDictionary<string, string>.Empty);

    [Fact]
    public void BuildShellCommandForCell_Pwsh_GeneratesUniqueScriptPathPerCall()
    {
        // Two consecutive calls for the same cell must return distinct script
        // paths so a still-running prior iter cannot race the next iter's
        // overwrite of a stable filename. See the BuildShellCommandForCell
        // comment for the wsl-bash leftover-process race that motivated this.
        var (_, path1) = ThroughputRunner.BuildShellCommandForCell(
            PwshCell, "C:\\fixtures\\test.txt", "C:\\Temp\\sentinel-1.marker");
        var (_, path2) = ThroughputRunner.BuildShellCommandForCell(
            PwshCell, "C:\\fixtures\\test.txt", "C:\\Temp\\sentinel-2.marker");
        try
        {
            Assert.NotEqual(path1, path2);
            Assert.EndsWith(".ps1", path1);
            Assert.EndsWith(".ps1", path2);
            Assert.True(File.Exists(path1));
            Assert.True(File.Exists(path2));
        }
        finally
        {
            try { if (File.Exists(path1)) File.Delete(path1); } catch (IOException) { }
            try { if (File.Exists(path2)) File.Delete(path2); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Wsl_GeneratesUniqueScriptPathPerCall()
    {
        var (_, path1) = ThroughputRunner.BuildShellCommandForCell(
            WslCell, "/mnt/c/fixtures/test.txt", "C:\\Temp\\sentinel-1.marker");
        var (_, path2) = ThroughputRunner.BuildShellCommandForCell(
            WslCell, "/mnt/c/fixtures/test.txt", "C:\\Temp\\sentinel-2.marker");
        try
        {
            Assert.NotEqual(path1, path2);
            Assert.EndsWith(".sh", path1);
            Assert.EndsWith(".sh", path2);
            Assert.True(File.Exists(path1));
            Assert.True(File.Exists(path2));
        }
        finally
        {
            try { if (File.Exists(path1)) File.Delete(path1); } catch (IOException) { }
            try { if (File.Exists(path2)) File.Delete(path2); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_ScriptStemIncludesCellId()
    {
        // Cell.Id is part of the stem so that the wintty-bench-scripts dir
        // remains debuggable when multiple cells run in the same harness.
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            PwshCell, "C:\\fixtures\\test.txt", "C:\\Temp\\sentinel.marker");
        try
        {
            Assert.Contains("TEST-", Path.GetFileName(path));
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Pwsh_ScriptBodyTouchesSentinel()
    {
        // Lock down the script-body contract the whole fix depends on:
        // bash/pwsh actually touches the iter's specific sentinel, not the
        // dir or some derived path.
        var sentinel = "C:\\Temp\\unique-pwsh-sentinel.marker";
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            PwshCell, "C:\\fixtures\\test.txt", sentinel);
        try
        {
            var body = File.ReadAllText(path);
            Assert.Contains(sentinel, body);
            Assert.Contains("New-Item -ItemType File -Force -Path", body);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Wsl_ScriptBodyTouchesSentinelInWslPath()
    {
        // The wsl variant must translate the Windows sentinel path to its
        // /mnt/c/... form before emitting the touch; otherwise bash would
        // try (and fail) to create a file at a path with a drive letter.
        var sentinel = "C:\\Temp\\unique-wsl-sentinel.marker";
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            WslCell, "/mnt/c/fixtures/test.txt", sentinel);
        try
        {
            var body = File.ReadAllText(path);
            Assert.Contains("/mnt/c/Temp/unique-wsl-sentinel.marker", body);
            Assert.Contains("touch '", body);
            Assert.DoesNotContain("C:\\", body);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Pwsh_QueriesCursorPositionBeforeSentinel()
    {
        // Without the cursor-position query the done sentinel can fire before
        // the terminal has actually rendered the workload (WT does not back-
        // pressure stdout the way ConPTY does for wintty). Writing CSI 6 n
        // and reading the reply forces the terminal to parse and render every
        // preceding byte before responding, so the touch correlates with
        // "render complete" rather than "stdout buffered."
        var sentinel = "C:\\Temp\\cursor-query-pwsh.marker";
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            PwshCell, "C:\\fixtures\\test.txt", sentinel);
        try
        {
            var body = File.ReadAllText(path);
            var workloadIdx = body.IndexOf("Get-Content", StringComparison.Ordinal);
            var queryIdx = body.IndexOf("[6n", StringComparison.Ordinal);
            // ReadKey, not Read, because Read() blocks under cooked-mode
            // CONIN$ which is what pwsh sees when launched by a real
            // terminal. See BuildPwshCommand for the rationale.
            var readIdx = body.IndexOf("[Console]::ReadKey", StringComparison.Ordinal);
            var sentinelIdx = body.IndexOf("New-Item", StringComparison.Ordinal);

            Assert.True(workloadIdx >= 0, "Get-Content workload must be present");
            Assert.True(queryIdx >= 0, "CSI 6 n cursor-position query must be present");
            Assert.True(readIdx >= 0, "stdin read of cursor reply must be present");
            Assert.True(sentinelIdx >= 0, "sentinel touch must be present");
            Assert.True(workloadIdx < queryIdx, "workload must precede cursor query");
            Assert.True(queryIdx < readIdx, "cursor query must precede stdin read");
            Assert.True(readIdx < sentinelIdx, "stdin read must precede sentinel touch");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Wsl_QueriesCursorPositionBeforeSentinel()
    {
        var sentinel = "C:\\Temp\\cursor-query-wsl.marker";
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            WslCell, "/mnt/c/fixtures/test.txt", sentinel);
        try
        {
            var body = File.ReadAllText(path);
            var workloadIdx = body.IndexOf("cat '", StringComparison.Ordinal);
            // \033[6n is the bash-printf escape form; the literal "[6n" is
            // what survives in the script source.
            var queryIdx = body.IndexOf("[6n", StringComparison.Ordinal);
            // Pin the exact line that consumes the reply, not just any
            // "read " token, so a future reformat can't slide an unrelated
            // command into this assertion's path.
            var readIdx = body.IndexOf("IFS= read", StringComparison.Ordinal);
            var sentinelIdx = body.IndexOf("touch '", StringComparison.Ordinal);

            Assert.True(workloadIdx >= 0, "cat workload must be present");
            Assert.True(queryIdx >= 0, "CSI 6 n cursor-position query must be present");
            Assert.True(readIdx >= 0, "bash read of cursor reply must be present");
            Assert.True(sentinelIdx >= 0, "sentinel touch must be present");
            Assert.True(workloadIdx < queryIdx, "workload must precede cursor query");
            Assert.True(queryIdx < readIdx, "cursor query must precede stdin read");
            Assert.True(readIdx < sentinelIdx, "stdin read must precede sentinel touch");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }

    [Fact]
    public void BuildShellCommandForCell_Wsl_CapturesDiagnosticsToSiblingLog()
    {
        // The wsl path is suspected of returning fast under WT because `read
        // -d R` hits EOF (stdin closed by ConPTY chain) and bash silently
        // continues to touch the sentinel. The script captures stty exit,
        // read exit, and the byte-length of the response into a sibling .log
        // file so a real WT smoke produces concrete evidence per iter. The
        // log file shares the script's basename and lives next to it; the
        // runner deletes the .sh on exit but does not touch the .log, so it
        // persists for post-run inspection.
        var sentinel = "C:\\Temp\\diag-wsl-sentinel.marker";
        var (_, path) = ThroughputRunner.BuildShellCommandForCell(
            WslCell, "/mnt/c/fixtures/test.txt", sentinel);
        try
        {
            var body = File.ReadAllText(path);
            var expectedLogStem = Path.GetFileNameWithoutExtension(path);

            // The log path inside the script body is in WSL form, but it
            // must mention the same stem as the .sh on disk so the user can
            // map "which iter is this log".
            Assert.Contains(expectedLogStem + ".log", body);
            // stty exit code captured. The 2>/dev/null hides the stderr
            // message but not the exit status; record it.
            Assert.Contains("stty_exit=", body);
            // read exit code is the smoking gun for the EOF hypothesis.
            // Bash's read returns >0 on EOF without a delimiter; if WT
            // closes stdin pre-emptively this prints 1 and the script
            // falls through.
            Assert.Contains("read_exit=", body);
            // Length of what was actually consumed from stdin. 0 = nothing
            // arrived; non-zero with read_exit=0 = reply parsed correctly.
            Assert.Contains("resp_len=", body);
            // Sentinel touch is still the last functional step. Diagnostic
            // capture must not gate it; an unconditional touch keeps the
            // runner contract intact.
            var readIdx = body.IndexOf("IFS= read", StringComparison.Ordinal);
            var touchIdx = body.IndexOf("touch '", StringComparison.Ordinal);
            Assert.True(readIdx < touchIdx, "diagnostics must not move the sentinel touch");
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        }
    }
}
