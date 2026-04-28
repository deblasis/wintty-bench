using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace WinttyBench.Launchers;

// Polling deadlines use Stopwatch.GetElapsedTime (monotonic, sub-ms
// precision) rather than DateTime.UtcNow (wall-clock, ~16 ms tick,
// can jump on NTP sync). See LatencyRunner.cs:117 for the precedent.
[SupportedOSPlatform("windows")]
public static class WtHwndLocator
{
    // Window class for WT's main window. Stable since the v1.x line.
    // Verified empirically via Spy++ / FindWindow probes; documented in
    // microsoft/terminal source tree.
    public const string ExpectedWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    // Polls EnumWindows until a top-level window whose class matches
    // ExpectedWindowClass and whose owning process ID matches `pid`
    // appears, or `timeout` elapses. Returns the HWND or throws
    // TimeoutException.
    public static nint WaitForWtHwnd(int pid, TimeSpan timeout)
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < timeout)
        {
            nint found = 0;
            // EnumWindows is synchronous: the lambda's lifetime is bounded by this
            // call, so a captured closure is safe. For async callbacks
            // (SetWinEventHook, IDirect3D11* sample-grabbers), root the delegate in
            // a field and add GC.KeepAlive at the end of the registration scope to
            // avoid CallbackOnCollectedDelegateException.
            EnumWindows((hwnd, _lParam) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                _ = GetWindowThreadProcessId(hwnd, out var hwndPid);
                if (hwndPid != (uint)pid) return true;

                var buf = new char[64];
                var len = GetClassName(hwnd, buf, buf.Length);
                if (len == 0) return true;
                if (new string(buf, 0, len) == ExpectedWindowClass)
                {
                    found = hwnd;
                    return false;  // stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            if (found != 0) return found;
            Thread.Sleep(50);
        }
        throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
            $"WT HWND with class '{ExpectedWindowClass}' for PID {pid} did not appear within {timeout.TotalSeconds:N1}s"));
    }

    // Snapshot of process IDs that currently own a top-level visible
    // window with class CASCADIA_HOSTING_WINDOW_CLASS. WtLauncher uses
    // this to identify which WT host processes already exist BEFORE
    // spawning wt.exe so the post-spawn poll can pick the NEW one
    // (avoiding both user-opened WT windows and the transient
    // wt.exe -> broker -> host handoff where the broker exits quickly).
    public static System.Collections.Generic.HashSet<int> SnapshotCascadiaPids()
    {
        var pids = new System.Collections.Generic.HashSet<int>();
        EnumWindows((hwnd, _lParam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            _ = GetWindowThreadProcessId(hwnd, out var hwndPid);
            var buf = new char[64];
            var len = GetClassName(hwnd, buf, buf.Length);
            if (len == 0) return true;
            if (new string(buf, 0, len) == ExpectedWindowClass)
            {
                pids.Add((int)hwndPid);
            }
            return true;
        }, IntPtr.Zero);
        return pids;
    }

    // Polls EnumWindows until a top-level window with class
    // ExpectedWindowClass appears whose owning PID is NOT in
    // `excludePids`, or `timeout` elapses. Returns (hwnd, pid) or
    // throws TimeoutException. WtLauncher uses this to identify the
    // NEW WT host process after spawning wt.exe.
    public static (nint hwnd, int pid) WaitForNewWtHwnd(
        System.Collections.Generic.IReadOnlySet<int> excludePids,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(excludePids);
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < timeout)
        {
            nint foundHwnd = 0;
            int foundPid = 0;
            EnumWindows((hwnd, _lParam) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                _ = GetWindowThreadProcessId(hwnd, out var hwndPid);
                if (excludePids.Contains((int)hwndPid)) return true;

                var buf = new char[64];
                var len = GetClassName(hwnd, buf, buf.Length);
                if (len == 0) return true;
                if (new string(buf, 0, len) == ExpectedWindowClass)
                {
                    foundHwnd = hwnd;
                    foundPid = (int)hwndPid;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (foundHwnd != 0) return (foundHwnd, foundPid);
            Thread.Sleep(50);
        }
        throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
            $"New WT HWND with class '{ExpectedWindowClass}' did not appear within {timeout.TotalSeconds:N1}s"));
    }

    // Polls until GetForegroundWindow() returns `expectedHwnd` or `timeout`
    // elapses. Throws TimeoutException on timeout. C13 needs the WT window
    // to be foreground so SendInput keystrokes hit it; MSIX apps can have
    // focus quirks on launch.
    public static void WaitForForeground(nint expectedHwnd, TimeSpan timeout)
    {
        var start = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(start) < timeout)
        {
            if (GetForegroundWindow() == expectedHwnd) return;
            Thread.Sleep(50);
        }
        throw new TimeoutException(string.Create(CultureInfo.InvariantCulture,
            $"WT HWND 0x{expectedHwnd:X} did not become foreground within {timeout.TotalSeconds:N1}s"));
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    // CA1838: avoid StringBuilder for P/Invoke. Char[] (pinned by the
    // marshaller for [In, Out]) avoids the StringBuilder marshal overhead and
    // is AOT-friendly; the buffer is locale-independent ASCII anyway.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
