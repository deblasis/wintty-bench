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

    // Fields wired up in subsequent steps (Task 16: D3D11 device + DXGI bridge,
    // Task 17: GraphicsCaptureItem, Task 18: frame pool / capture session /
    // staging texture / FrameArrived token). CS0169 is tolerated for now.
#pragma warning disable CS0169 // The field is never used
    private nint _d3dDevice;
    private nint _d3dContext;
    private nint _stagingTexture;
    private nint _graphicsDevice;
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

#pragma warning disable CA1822 // Mark members as static - body filled in Task 16.
    private void InitializeNative(nint hwnd)
    {
        // Filled in by subsequent steps.
    }
#pragma warning restore CA1822

    public Task<CapturedFrame> NextFrameAsync(CancellationToken ct)
    {
        return _frames.Reader.ReadAsync(ct).AsTask();
    }

    public void Dispose()
    {
        // Filled in by subsequent steps; for now, harmless.
    }
}
