using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static WinttyBench.Input.SendInputInterop;

namespace WinttyBench.Input;

// SendInput keystroke injection plus the AttachThreadInput foreground-window
// hand-off. Synthesizes a single keydown+keyup at the Win32 input-queue
// level; ConPTY relays the resulting byte to wintty's child shell.
[SupportedOSPlatform("windows")]
public static class SendInputProbe
{
    // Brings hwnd to the foreground using the documented AttachThreadInput
    // workaround. Required because SetForegroundWindow alone is rate-limited
    // when the calling process is not the current foreground; attaching to
    // the foreground thread's input queue lifts that restriction for one
    // call. Detaches in finally so the bench process does not leave its
    // input queue tangled with whatever was foreground.
    public static void EnsureForeground(nint hwnd)
    {
        if (hwnd == nint.Zero)
            throw new ArgumentException("hwnd must be non-null", nameof(hwnd));

        var fg = GetForegroundWindow();
        if (fg == hwnd) return;

        var fgTid = fg == nint.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
        var ourTid = GetCurrentThreadId();
        var attached = false;
        try
        {
            if (fgTid != 0 && fgTid != ourTid)
                attached = AttachThreadInput(ourTid, fgTid, true);

            // Best-effort: SetForegroundWindow may still fail if the user
            // is interacting with another app. The runner treats subsequent
            // ROI-no-change as Hung, which is the correct behavior.
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
                AttachThreadInput(ourTid, fgTid, false);
        }
    }

    // Injects a single keydown+keyup for vkCode; returns QPC ticks captured
    // immediately before the SendInput call. The QPC is the runner's t=0:
    // "system saw the input" lower-bound.
    public static long Inject(byte vkCode)
    {
        var scan = (ushort)MapVirtualKeyW(vkCode, MAPVK_VK_TO_VSC);

        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].U.ki.wVk = vkCode;
        inputs[0].U.ki.wScan = scan;
        inputs[0].U.ki.dwFlags = 0; // keydown
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].U.ki.wVk = vkCode;
        inputs[1].U.ki.wScan = scan;
        inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

        var qpc = Stopwatch.GetTimestamp();
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput sent {sent} of {inputs.Length}, win32 error {Marshal.GetLastWin32Error()}");
        }
        return qpc;
    }
}
