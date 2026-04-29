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
}
