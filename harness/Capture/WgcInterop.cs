using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Capture;

// Hand-rolled COM vtables for the WGC interfaces we touch. We use ComWrappers-free
// raw IntPtr + function-pointer dispatch, mirroring WinttyJobObject's style.
// Each interface struct has the methods we actually call, no more. Method
// indices come from the interface's IDL + WinRT reference projections; off-by-
// one errors show up as access violations, so the interface tables are
// regression-tested via the smoke test in Task 16.
[SupportedOSPlatform("windows")]
internal static class WgcInterop
{
    // RuntimeClass strings for RoActivateInstance / RoGetActivationFactory.
    public const string GraphicsCaptureItem_ClassName =
        "Windows.Graphics.Capture.GraphicsCaptureItem";
    public const string Direct3D11CaptureFramePool_ClassName =
        "Windows.Graphics.Capture.Direct3D11CaptureFramePool";

    // IIDs (lowercase hex strings; converted to Guids at usage sites).
    public static readonly Guid IID_IGraphicsCaptureItemInterop =
        new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    public static readonly Guid IID_IGraphicsCaptureItem =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    public static readonly Guid IID_IDirect3D11CaptureFramePoolStatics =
        new("7784056A-67AA-4D53-AE54-1088D5A8CA21");
    // v2 hosts CreateFreeThreaded; v1 only has Create. Use v2 to avoid
    // having to QI a separate factory.
    public static readonly Guid IID_IDirect3D11CaptureFramePoolStatics2 =
        new("589B103F-6BBC-5DF5-A991-02E28B3B66D5");
    public static readonly Guid IID_IDirect3D11CaptureFramePool =
        new("24EB6D22-1975-422E-82E7-780DBD8DDF24");
    public static readonly Guid IID_IGraphicsCaptureSession =
        new("814E42A9-F70F-4AD7-939B-FDDCC6EB880D");
    public static readonly Guid IID_IDirect3D11CaptureFrame =
        new("FA50C623-38DA-4B32-ACF3-FA9734AD800E");
    public static readonly Guid IID_IDirect3DSurface =
        new("0BF4A146-13C1-4694-BEE3-7ABF15EAF586");
    public static readonly Guid IID_IDirect3DDxgiInterfaceAccess =
        new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");

    // BGRA8 format for the frame pool.
    public const uint DirectXPixelFormat_B8G8R8A8UIntNormalized = 87;

    [StructLayout(LayoutKind.Sequential)]
    public struct SizeInt32
    {
        public int Width;
        public int Height;
    }
}
