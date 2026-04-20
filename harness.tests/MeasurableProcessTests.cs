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
}
