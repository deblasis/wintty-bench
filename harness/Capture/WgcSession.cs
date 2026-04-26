using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Channels;

namespace WinttyBench.Capture;

// RAII wrapper around a per-cell-run WGC capture pipeline.
//
// Construction sequence:
//   1. RoInitialize(MULTITHREADED) - idempotent; OK if a prior call set ASTA.
//   2. D3D11CreateDevice with BGRA_SUPPORT.
//   3. Wrap the D3D11 device as IDirect3DDevice via CreateDirect3D11DeviceFromDXGIDevice.
//   4. RoGetActivationFactory(IGraphicsCaptureItemInterop) -> CreateForWindow(hwnd).
//   5. Get IDirect3D11CaptureFramePoolStatics; CreateFreeThreaded(device, BGRA8, 2, item.Size).
//   6. framePool.CreateCaptureSession(item); session.StartCapture().
//   7. Hook FrameArrived; readback into a pre-allocated staging texture.
//
// Each step releases its predecessor on failure so partial-init cleans up.
[SupportedOSPlatform("windows")]
public sealed class WgcSession : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    public int ClientWidthPx { get; }
    public int ClientHeightPx { get; }
    public long QpcFrequency { get; } = Stopwatch.Frequency;

    // D3D11 device + DXGI-bridged IDirect3DDevice (Task 16).
    private nint _d3dDevice;
    private nint _d3dContext;
    private nint _graphicsDevice;

    // Wired up in Task 17 (GraphicsCaptureItem) and Task 18 (frame pool /
    // capture session / staging texture / FrameArrived token). CS0169 is
    // tolerated until then.
#pragma warning disable CS0169 // The field is never used
    private nint _stagingTexture;
    private nint _captureItem;
    private nint _framePool;
    private nint _captureSession;
    private long _frameArrivedToken;
#pragma warning restore CS0169

    private readonly Channel<CapturedFrame> _frames = Channel.CreateBounded<CapturedFrame>(
        new BoundedChannelOptions(capacity: 1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

    private WgcSession(int width, int height)
    {
        ClientWidthPx = width;
        ClientHeightPx = height;
    }

    public static WgcSession Open(nint hwnd)
    {
        if (hwnd == nint.Zero) throw new ArgumentException("hwnd must be non-null", nameof(hwnd));
        if (!GetClientRect(hwnd, out var rc))
            throw new InvalidOperationException(
                $"GetClientRect failed: win32 error {Marshal.GetLastWin32Error()}");
        var w = rc.right - rc.left;
        var h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"Client area is empty: {w}x{h}");

        // CombaseInterop.RoInitialize: returns S_OK (0) on first call,
        // RPC_E_CHANGED_MODE (0x80010106) if a prior call selected a
        // different apartment - which is fine for our use case (we do not
        // pump messages on this thread). Treat both as success.
        var hr = CombaseInterop.RoInitialize(CombaseInterop.RO_INIT_MULTITHREADED);
        const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);
        if (hr != 0 && hr != RPC_E_CHANGED_MODE)
            throw new InvalidOperationException($"RoInitialize failed: hr=0x{hr:X8}");

        var session = new WgcSession(w, h);
        try
        {
            session.InitializeNative(hwnd);
        }
        catch
        {
            session.Dispose();
            throw;
        }
        return session;
    }

    private void InitializeNative(nint hwnd)
    {
        // Step 1: D3D11 device with BGRA support.
        var hr = D3D11Interop.D3D11CreateDevice(
            pAdapter: nint.Zero,
            DriverType: D3D11Interop.D3D_DRIVER_TYPE_HARDWARE,
            Software: nint.Zero,
            Flags: D3D11Interop.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            pFeatureLevels: nint.Zero,
            FeatureLevels: 0,
            SDKVersion: D3D11Interop.D3D11_SDK_VERSION,
            ppDevice: out _d3dDevice,
            pFeatureLevel: out _,
            ppImmediateContext: out _d3dContext);
        if (hr != 0)
        {
            // Fallback to WARP for VMs / headless boxes with no GPU driver.
            hr = D3D11Interop.D3D11CreateDevice(
                nint.Zero,
                D3D11Interop.D3D_DRIVER_TYPE_WARP,
                nint.Zero,
                D3D11Interop.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                nint.Zero, 0,
                D3D11Interop.D3D11_SDK_VERSION,
                out _d3dDevice, out _, out _d3dContext);
            if (hr != 0)
                throw new InvalidOperationException(
                    $"D3D11CreateDevice (HARDWARE+WARP) failed: hr=0x{hr:X8}");
        }

        // Step 2: bridge D3D11 -> IDirect3DDevice (WGC's input type).
        // _d3dDevice is an IUnknown*; QueryInterface for IDXGIDevice via the
        // standard COM ID, then hand to CreateDirect3D11DeviceFromDXGIDevice.
        var iidDxgiDevice = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
        var hrQi = ComQueryInterface(_d3dDevice, iidDxgiDevice, out var dxgiDevice);
        if (hrQi != 0)
            throw new InvalidOperationException($"QI(IDXGIDevice) failed: hr=0x{hrQi:X8}");
        try
        {
            hr = D3D11Interop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out _graphicsDevice);
            if (hr != 0)
                throw new InvalidOperationException(
                    $"CreateDirect3D11DeviceFromDXGIDevice failed: hr=0x{hr:X8}");
        }
        finally
        {
            ComRelease(dxgiDevice);
        }
    }

    // Helper: dispatch IUnknown::QueryInterface (vtable slot 0).
    private static unsafe int ComQueryInterface(nint pUnk, Guid iid, out nint ppOut)
    {
        ppOut = nint.Zero;
        var vtable = *(nint**)pUnk;
        var qiPtr = vtable[0];
        var qi = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)qiPtr;
        nint outPtr;
        int hr;
        hr = qi(pUnk, &iid, &outPtr);
        ppOut = outPtr;
        return hr;
    }

    // Helper: dispatch IUnknown::Release (vtable slot 2).
    private static unsafe uint ComRelease(nint pUnk)
    {
        if (pUnk == nint.Zero) return 0;
        var vtable = *(nint**)pUnk;
        var relPtr = vtable[2];
        var rel = (delegate* unmanaged[Stdcall]<nint, uint>)relPtr;
        return rel(pUnk);
    }

    public Task<CapturedFrame> NextFrameAsync(CancellationToken ct)
    {
        return _frames.Reader.ReadAsync(ct).AsTask();
    }

    public void Dispose()
    {
        if (_graphicsDevice != nint.Zero) { ComRelease(_graphicsDevice); _graphicsDevice = nint.Zero; }
        if (_d3dContext != nint.Zero) { ComRelease(_d3dContext); _d3dContext = nint.Zero; }
        if (_d3dDevice != nint.Zero) { ComRelease(_d3dDevice); _d3dDevice = nint.Zero; }
    }
}
