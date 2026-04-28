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

    // Optional JobObject that contains Process and all its descendants.
    // When set, disposing it is the canonical kill path: CloseHandle with
    // KILL_ON_JOB_CLOSE reaps everything in one shot, even descendants that
    // have already been re-parented away from the launched process. Falls
    // back to Process.Kill(entireProcessTree) on platforms / launchers that
    // cannot use a job (e.g. WtLauncher hands off to an MSIX app).
    public IDisposable? Job { get; init; }

    // Top-level window handle of the launched terminal, populated by the
    // launcher once the window has been identified. LatencyRunner uses
    // this for SendInput targeting + WGC capture instead of re-discovering
    // the HWND from process ID. Null if the launcher did not (or could not)
    // identify a window. Wintty + WT fill it; future launchers should too.
    public nint? WindowHandle { get; init; }

    public void Dispose()
    {
        // Dispose the job first: closing the handle with KILL_ON_JOB_CLOSE
        // drops the whole tree synchronously. Process.Kill() is then either
        // a no-op (already gone) or a belt-and-braces fallback.
        Job?.Dispose();
        Process.Kill();
        try
        {
            if (Directory.Exists(ConfigRoot)) Directory.Delete(ConfigRoot, recursive: true);
        }
        catch { /* best effort cleanup */ }
    }
}
