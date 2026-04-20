using System.Diagnostics;
using System.Text;

namespace WinttyBench.Launchers;

public sealed class WinttyLauncher : ILauncher
{
    public string Name => "wintty";
    public string ExpectedProcessName => "Wintty";

    public LaunchHandle Launch(LaunchRequest request)
    {
        var configRoot = Path.Combine(Path.GetTempPath(), "wintty-bench-" + Guid.NewGuid());
        WriteConfig(configRoot, request.ConfigOverrides, templatePath: null);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.TargetExePath,
            Arguments = $"-e {request.ShellCommand}",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
        foreach (var (k, v) in BuildEnv(configRoot))
        {
            startInfo.Environment[k] = v;
        }

        var proc = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start wintty at {request.TargetExePath}");

        // Wintty is self-hosted: the spawned exe IS the measurable process.
        return new LaunchHandle
        {
            Process = MeasurableProcess.FromProcess(proc, ExpectedProcessName),
            ConfigRoot = configRoot,
        };
    }

    public static void WriteConfig(string configRoot, IReadOnlyDictionary<string, string> overrides, string? templatePath)
    {
        var ghosttyDir = Path.Combine(configRoot, "ghostty");
        Directory.CreateDirectory(ghosttyDir);

        var sb = new StringBuilder();
        if (templatePath is not null && File.Exists(templatePath))
        {
            sb.Append(File.ReadAllText(templatePath));
            sb.AppendLine();
        }
        foreach (var (k, v) in overrides)
        {
            // Plain Append calls dodge CA1305 (no interpolated handler, no
            // IFormatProvider needed) and ghostty config keys/values are
            // already locale-invariant identifiers.
            sb.Append(k).Append(" = ").AppendLine(v);
        }

        File.WriteAllText(Path.Combine(ghosttyDir, "config"), sb.ToString());
    }

    public static Dictionary<string, string> BuildEnv(string configRoot) => new()
    {
        ["XDG_CONFIG_HOME"] = configRoot,
    };
}
