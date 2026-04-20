using System.Diagnostics;

namespace WinttyBench;

public static class EnvProbe
{
    public static EnvCapture Capture(string targetExePath)
    {
        var winVersion = Environment.OSVersion.Version.ToString();
        var cpu = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "unknown";
        var ramGb = (int)(GetTotalRamBytes() / (1024L * 1024 * 1024));
        var (winttySha, winttyVersion) = ProbeWinttyVersion(targetExePath);
        var wtVersion = ProbeWtVersion();

        return new EnvCapture(
            WinttySha: winttySha,
            WinttyVersion: winttyVersion,
            WtVersion: wtVersion,
            WezTermVersion: null,  // Plan 2
            WindowsBuild: winVersion,
            Cpu: cpu,
            Gpu: "unknown",  // deferred: DXGI enum, needs P/Invoke
            RamGb: ramGb,
            Display: new DisplayCapture(1920, 1080, 60, 1.0));  // deferred: GetDeviceCaps, monitor enum
    }

    private static (string sha, string version) ProbeWinttyVersion(string exePath)
    {
        try
        {
#pragma warning disable CA1416 // FileVersionInfo is cross-plat but some props are Windows-flavored; this harness is Windows-only.
            var info = FileVersionInfo.GetVersionInfo(exePath);
            return (info.ProductVersion ?? "unknown", info.FileVersion ?? "unknown");
#pragma warning restore CA1416
        }
        catch (FileNotFoundException)
        {
            return ("unknown", "unknown");
        }
        catch (IOException)
        {
            return ("unknown", "unknown");
        }
    }

    private static string ProbeWtVersion()
    {
        try
        {
            var psi = new ProcessStartInfo("wt.exe", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return "not-installed";
            p.WaitForExit(2000);
            return p.StandardOutput.ReadToEnd().Trim();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "not-installed";
        }
        catch (InvalidOperationException)
        {
            return "not-installed";
        }
    }

    private static string? ReadRegistry(string keyPath, string valueName)
    {
        try
        {
#pragma warning disable CA1416 // Registry is Windows-only; this whole harness is Windows-only.
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
#pragma warning restore CA1416
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static long GetTotalRamBytes()
    {
        // GC heap is a poor proxy; use GetPhysicallyInstalledSystemMemory via P/Invoke.
        // Deferred to Plan 2; returning a conservative default.
        return 16L * 1024 * 1024 * 1024;
    }
}
