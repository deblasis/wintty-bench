using System.Diagnostics;

namespace WinttyBench.Tests.Fixtures;

internal static class WslHelpers
{
    public static string RunWsl(string bashCommand)
    {
        var psi = new ProcessStartInfo("wsl.exe",
            $"-d Ubuntu-24.04 -- bash -c \"{bashCommand.Replace("\"", "\\\"")}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"wsl bash exit {p.ExitCode} for '{bashCommand}'; stderr: {p.StandardError.ReadToEnd()}");
        return stdout;
    }

    public static string ToWslMountPath(string windowsPath)
    {
        var drive = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath[2..].Replace('\\', '/');
        return $"/mnt/{drive}{rest}";
    }
}
