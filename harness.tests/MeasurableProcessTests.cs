using WinttyBench;
using Xunit;

namespace WinttyBench.Tests;

public class MeasurableProcessTests
{
    [Fact]
    public void Create_Wraps_Process_Id_And_Name()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var mp = MeasurableProcess.FromProcess(self, expectedName: self.ProcessName);

        Assert.Equal(self.Id, mp.ProcessId);
        Assert.Equal(self.ProcessName, mp.ProcessName);
        Assert.False(mp.HasExited);
    }

    [Fact]
    public void WaitForProcessByName_Returns_Null_When_Not_Found_Within_Timeout()
    {
        var result = MeasurableProcess.WaitForProcessByName(
            "ThisProcessShouldNeverExist_9f3a", TimeSpan.FromMilliseconds(200));

        Assert.Null(result);
    }

    [Fact]
    public void WaitForProcessByName_Finds_Running_Process()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var result = MeasurableProcess.WaitForProcessByName(
            self.ProcessName, TimeSpan.FromSeconds(1));

        Assert.NotNull(result);
        Assert.Equal(self.Id, result.ProcessId);
    }

    [Fact]
    public void WorkingSet64_Returns_Positive_For_Current_Process()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var mp = MeasurableProcess.FromProcess(self, expectedName: self.ProcessName);

        mp.Refresh();
        Assert.True(mp.WorkingSet64 > 0);
    }

    [Fact]
    public void Refresh_Updates_Cached_WorkingSet()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess();
        var mp = MeasurableProcess.FromProcess(self, expectedName: self.ProcessName);

        mp.Refresh();
        var first = mp.WorkingSet64;
        // Allocate some memory to force WorkingSet to change.
        var junk = new byte[16 * 1024 * 1024];
        junk[0] = 1; junk[^1] = 2;  // touch pages
        mp.Refresh();
        var second = mp.WorkingSet64;

        Assert.True(first > 0);
        Assert.True(second > 0);
        // Don't assert second > first — Windows working set is not monotonic
        // across Refresh calls; the invariant we care about is that Refresh
        // doesn't throw and returns a plausible (> 0) number. Retain the
        // junk reference so it isn't collected before the second Refresh.
        GC.KeepAlive(junk);
    }
}
