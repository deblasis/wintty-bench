using System.Diagnostics;
using System.Text;

namespace WinttyBench.Launchers;

public sealed class WinttyLauncher : ILauncher
{
    public string Name => "wintty";
    public string ExpectedProcessName => "Wintty";

    public LaunchHandle Launch(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configRoot = Path.Combine(Path.GetTempPath(), "wintty-bench-" + Guid.NewGuid());

        // Pass the shell command through the config file's `command` key
        // rather than -e argv. Wintty's WinUI entry on Windows does not
        // forward -e to libghostty reliably (it falls back to the default
        // shell, cmd.exe), but config.command is read by the surface
        // initializer before termio spawns the child.
        var overrides = new Dictionary<string, string>(request.ConfigOverrides, StringComparer.Ordinal)
        {
            ["command"] = request.ShellCommand,
        };
        WriteConfig(configRoot, overrides, templatePath: null);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.TargetExePath,
            Arguments = string.Empty,
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

    // Baseline keys every bench run needs, regardless of cell. Without
    // quit-after-last-window-closed=true, Wintty stays alive after the
    // shell exits on Windows (Linux-only default upstream) and the bench
    // hangs on WaitForExit.
    public static IReadOnlyDictionary<string, string> BaselineOverrides { get; } =
        new Dictionary<string, string>
        {
            ["quit-after-last-window-closed"] = "true",
            ["wait-after-command"] = "false",
        };

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
        foreach (var (k, v) in BaselineOverrides)
        {
            sb.Append(k).Append(" = ").AppendLine(v);
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
