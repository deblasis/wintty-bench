using System.Diagnostics;

namespace WinttyBench;

public sealed class MeasurableProcess
{
    private readonly Process _process;

    private MeasurableProcess(Process process)
    {
        _process = process;
    }

    public int ProcessId => _process.Id;
    public string ProcessName => _process.ProcessName;
    public bool HasExited => _process.HasExited;

    public static MeasurableProcess FromProcess(Process process, string expectedName)
    {
        if (!process.ProcessName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Process name mismatch: got '{process.ProcessName}', expected '{expectedName}'. " +
                "This is likely a Launcher bug (wrapping wt.exe instead of WindowsTerminal.exe).");
        }
        return new MeasurableProcess(process);
    }

    public static MeasurableProcess? WaitForProcessByName(string name, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var found = Process.GetProcessesByName(name).FirstOrDefault();
            if (found is not null)
            {
                return new MeasurableProcess(found);
            }
            Thread.Sleep(50);
        }
        return null;
    }

    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (InvalidOperationException) { }
    }
}
