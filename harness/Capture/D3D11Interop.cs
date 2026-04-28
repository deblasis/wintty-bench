using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Capture;

// Direct-COM surface for D3D11 device creation, DXGI device adapter, and
// the staging-texture readback path. We declare exactly the entries we
// call; everything else is the WGC frame pool's job.
//
// Calling-convention note: D3D11CreateDevice and the COM vtable entries
// are __stdcall on x86 and the platform-default on x64 (which P/Invoke
// matches automatically via CallingConvention = Winapi).
//
// DllImport (not LibraryImport) to match WinttyJobObject's style: the
// harness project does not enable AllowUnsafeBlocks, and the source-gen
// stubs LibraryImport produces require it.
[SupportedOSPlatform("windows")]
internal static class D3D11Interop
{
    public const uint D3D11_SDK_VERSION = 7;
    public const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;

    public const uint D3D_DRIVER_TYPE_HARDWARE = 1;
    public const uint D3D_DRIVER_TYPE_WARP = 5;

    public const uint D3D11_USAGE_STAGING = 3;
    public const uint D3D11_CPU_ACCESS_READ = 0x20000;

    // DXGI_FORMAT_B8G8R8A8_UNORM
    public const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87;

    public const uint D3D11_MAP_READ = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_TEXTURE2D_DESC
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D3D11_MAPPED_SUBRESOURCE
    {
        public nint pData;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [DllImport("d3d11.dll")]
    public static extern int D3D11CreateDevice(
        nint pAdapter,
        uint DriverType,
        nint Software,
        uint Flags,
        nint pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out nint ppDevice,
        out uint pFeatureLevel,
        out nint ppImmediateContext);

    // Bridges a D3D11 device to a Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice
    // (the type WGC's frame pool consumes). Lives in d3d11.dll on modern
    // Windows but exported only via headers; we declare it manually.
    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
    public static extern int CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);
}

[SupportedOSPlatform("windows")]
internal static class CombaseInterop
{
    public const uint RO_INIT_MULTITHREADED = 1;

    [DllImport("combase.dll")]
    public static extern int RoInitialize(uint initType);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    public static extern int WindowsCreateString(
        string sourceString,
        uint length,
        out nint hstring);

    [DllImport("combase.dll")]
    public static extern int WindowsDeleteString(nint hstring);

    [DllImport("combase.dll")]
    public static extern int RoActivateInstance(nint activatableClassId, out nint instance);

    [DllImport("combase.dll")]
    public static extern int RoGetActivationFactory(
        nint activatableClassId,
        in Guid iid,
        out nint factory);
}
