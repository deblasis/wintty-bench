namespace WinttyBench.Launchers;

public interface ILauncher
{
    string Name { get; }
    string ExpectedProcessName { get; }

    LaunchHandle Launch(LaunchRequest request);
}

public sealed record LaunchRequest(
    string TargetExePath,
    string ShellCommand,
    IReadOnlyDictionary<string, string> ConfigOverrides,
    int Cols,
    int Rows);

public sealed class LaunchHandle : IDisposable
{
    public required MeasurableProcess Process { get; init; }
    public required string ConfigRoot { get; init; }

    public void Dispose()
    {
        Process.Kill();
        try
        {
            if (Directory.Exists(ConfigRoot)) Directory.Delete(ConfigRoot, recursive: true);
        }
        catch { /* best effort cleanup */ }
    }
}
