using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static WinttyBench.Input.SendInputInterop;

namespace WinttyBench.Input;

// Polls EnumWindows until a top-level visible HWND owned by processId
// appears. WaitForInputIdle is intentionally NOT used: it requires a
// Process handle (not just a PID) and EnumWindows already gives us the
// "window exists and is visible" signal we want. Using the PID directly
// also avoids opening a Process handle that would need disposal.
[SupportedOSPlatform("windows")]
public static class HwndLocator
{
    private const uint GW_OWNER = 4;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    // DllImport (not LibraryImport) to match the rest of the harness; the
    // project does not enable AllowUnsafeBlocks.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hWnd, uint uCmd);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    public static nint WaitForWinttyHwnd(int processId, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var found = FindOnce(processId);
            if (found != nint.Zero) return found;
            Thread.Sleep(PollInterval);
        }
        throw new TimeoutException(
            $"No top-level visible window found for PID {processId} within {timeout}");
    }

    private static nint FindOnce(int processId)
    {
        var match = nint.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindow(hwnd, GW_OWNER) != nint.Zero) return true;
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid != (uint)processId) return true;
            match = hwnd;
            return false;
        }, nint.Zero);
        return match;
    }
}
