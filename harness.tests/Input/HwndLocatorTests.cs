using System.Diagnostics;
using System.Runtime.Versioning;
using WinttyBench.Input;
using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Input;

[SupportedOSPlatform("windows")]
public class HwndLocatorTests
{
    [Fact]
    [Trait("OS", "Windows")]
    public void WaitForWinttyHwnd_Finds_Gui_Window()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "EnumWindows is Windows-only");

        // winver.exe is a classic Win32 app where the PID Process.Start
        // returns owns the top-level window. notepad.exe and mspaint.exe
        // would seem like cleaner choices, but on Windows 11 they are
        // AppX-shimmed: Process.Start returns a launcher PID that has no
        // window, while a child process owns the actual MainWindowHandle.
        // winver lives at %SYSTEMROOT%\System32\winver.exe on every modern
        // Windows host and stays plain Win32.
        var psi = new ProcessStartInfo("winver.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        using var p = Process.Start(psi)!;
        using var job = new WinttyJobObject();
        job.AssignProcess(p);

        try
        {
            var hwnd = HwndLocator.WaitForWinttyHwnd(p.Id, TimeSpan.FromSeconds(5));
            Assert.NotEqual(nint.Zero, hwnd);

            // Verify the HWND we got back belongs to the winver PID.
            _ = SendInputInterop.GetWindowThreadProcessId(hwnd, out var pid);
            Assert.Equal((uint)p.Id, pid);
        }
        finally
        {
            // Job dispose kills the process tree.
        }
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void WaitForWinttyHwnd_Throws_On_Timeout_For_NonGui_Process()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "EnumWindows is Windows-only");

        // Pick a PID that exists but has no top-level window: ourselves
        // (test runner is console-mode in xUnit + Microsoft.Testing.Platform).
        // We give it 250 ms which is plenty for the locator's poll loop.
        var ourPid = Environment.ProcessId;
        Assert.Throws<TimeoutException>(
            () => HwndLocator.WaitForWinttyHwnd(ourPid, TimeSpan.FromMilliseconds(250)));
    }
}
