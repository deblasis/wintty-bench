using System.Diagnostics;
using System.Security.Cryptography;

namespace WinttyBench.Fixtures;

// Helper for resolving paths on WSL Ubuntu-24.04 and checking file hashes.
// One instance per harness run caches the resolved $HOME. Not thread-safe
// (the bench runs cells sequentially).
public sealed class WslFixtureCache
{
    private const string Distro = "Ubuntu-24.04";
    private string? _wslHome;

    public async Task<string> HomeAsync()
    {
        if (_wslHome is not null) return _wslHome;
        var result = await RunWslAsync("printenv HOME");
        _wslHome = result.Trim();
        if (string.IsNullOrEmpty(_wslHome) || !_wslHome.StartsWith('/'))
            throw new InvalidOperationException($"Unexpected WSL HOME: '{_wslHome}'");
        return _wslHome;
    }

    // /home/root/foo -> \\wsl.localhost\Ubuntu-24.04\home\root\foo
    // Used to File.Exists + hash-compare fixtures without round-tripping
    // every byte through wsl.exe.
    // CA1822: intentionally an instance method so callers hold one WslFixtureCache and call all methods on it.
#pragma warning disable CA1822
    public string ToWindowsPath(string wslPath)
#pragma warning restore CA1822
    {
        ArgumentException.ThrowIfNullOrEmpty(wslPath);
        if (!wslPath.StartsWith('/')) throw new ArgumentException("Not a WSL path", nameof(wslPath));
        var rest = wslPath[1..].Replace('/', '\\');
        return $@"\\wsl.localhost\{Distro}\{rest}";
    }

    public async Task<string> ComputeSha256Async(string wslPath)
    {
        var winPath = ToWindowsPath(wslPath);
        using var stream = File.OpenRead(winPath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // CA1822: intentionally instance for API symmetry with HomeAsync/ComputeSha256Async.
#pragma warning disable CA1822
    public async Task RunBashScriptAsync(string wslBashCommand)
#pragma warning restore CA1822
    {
        await RunWslAsync(wslBashCommand);
    }

    private static async Task<string> RunWslAsync(string bashCommand)
    {
        var psi = new ProcessStartInfo("wsl.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(Distro);
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("bash");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(bashCommand);

        using var p = Process.Start(psi)!;
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"wsl bash exit {p.ExitCode} for '{bashCommand}'; stderr: {stderr}");
        return stdout;
    }
}
