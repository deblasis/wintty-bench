using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Input;

// EnumDisplaySettings against the primary monitor. Replaces the hardcoded
// 60 Hz in EnvProbe.Capture.
[SupportedOSPlatform("windows")]
public static class RefreshRateProbe
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettingsW(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    public static int? GetPrimaryRefreshHz()
    {
        var dm = default(DEVMODE);
        dm.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
        if (!EnumDisplaySettingsW(null, ENUM_CURRENT_SETTINGS, ref dm))
            return null;

        var hz = (int)dm.dmDisplayFrequency;
        // Some drivers report 0 or 1 Hz for "default"; treat as unknown.
        return hz <= 1 ? null : hz;
    }
}
