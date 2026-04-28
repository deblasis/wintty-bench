using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace WinttyBench.Launchers;

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
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            nint found = 0;
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

    // Polls until GetForegroundWindow() returns `expectedHwnd` or `timeout`
    // elapses. Throws TimeoutException on timeout. C13 needs the WT window
    // to be foreground so SendInput keystrokes hit it; MSIX apps can have
    // focus quirks on launch.
    public static void WaitForForeground(nint expectedHwnd, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
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
