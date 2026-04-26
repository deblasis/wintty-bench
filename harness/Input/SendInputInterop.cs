using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Input;

// User32 SendInput + window helpers. Direct P/Invoke; no managed projection
// layer. Uses DllImport (not LibraryImport) to match WinttyJobObject's style:
// the harness project does not enable AllowUnsafeBlocks, and the source-gen
// stubs LibraryImport produces require it.
[SupportedOSPlatform("windows")]
internal static class SendInputInterop
{
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const uint MAPVK_VK_TO_VSC = 0;

    public const byte VK_SPACE = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        // The full InputUnion in Win32 also has MOUSEINPUT and HARDWAREINPUT.
        // KEYBDINPUT is the largest at x86 and matches at x64 layout, so we
        // declare only it; the size of InputUnion still matches Win32's
        // because all variants are <= the size we declare here at x64.
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKeyW(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
