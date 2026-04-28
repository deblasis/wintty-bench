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

    // Wired across Tasks 17-18: CaptureItem, frame pool, capture session,
    // staging texture, and FrameArrived event token.
    private nint _captureItem;
    private nint _stagingTexture;
    private nint _framePool;
    private nint _captureSession;
    private long _frameArrivedToken;

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

        // CombaseInterop.RoInitialize returns:
        //   S_OK (0)                    - first init on this thread
        //   S_FALSE (1)                 - already initialized, same apartment
        //   RPC_E_CHANGED_MODE          - already initialized, different apartment
        // All three are usable for our purposes (we do not pump messages here).
        // .NET's CLR typically pre-initializes COM on the main thread, so
        // S_FALSE is the common path under `dotnet run`.
        var hr = CombaseInterop.RoInitialize(CombaseInterop.RO_INIT_MULTITHREADED);
        const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);
        if (hr < 0 && hr != RPC_E_CHANGED_MODE)
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

        // Step 3: GraphicsCaptureItem.CreateForWindow via IGraphicsCaptureItemInterop.
        // RoGetActivationFactory expects the runtime-class name as an HSTRING.
        var classNameHr = CombaseInterop.WindowsCreateString(
            WgcInterop.GraphicsCaptureItem_ClassName,
            (uint)WgcInterop.GraphicsCaptureItem_ClassName.Length,
            out var classNameH);
        if (classNameHr != 0)
            throw new InvalidOperationException(
                $"WindowsCreateString(GraphicsCaptureItem) failed: hr=0x{classNameHr:X8}");
        try
        {
            var iid = WgcInterop.IID_IGraphicsCaptureItemInterop;
            var hrFac = CombaseInterop.RoGetActivationFactory(classNameH, in iid, out var interopFactory);
            if (hrFac != 0)
                throw new InvalidOperationException(
                    $"RoGetActivationFactory(IGraphicsCaptureItemInterop) failed: hr=0x{hrFac:X8}");
            try
            {
                // IGraphicsCaptureItemInterop::CreateForWindow is vtable slot 3
                // (slots 0-2 are IUnknown). Signature:
                //   HRESULT CreateForWindow(HWND, REFIID, void**)
                _captureItem = InvokeCreateForWindow(interopFactory, hwnd);
                if (_captureItem == nint.Zero)
                    throw new InvalidOperationException("CreateForWindow returned null IGraphicsCaptureItem");
            }
            finally
            {
                ComRelease(interopFactory);
            }
        }
        finally
        {
            // HSTRING release; HRESULT discarded - failure here is benign on a
            // dispose path and there is nothing actionable we could do with it.
            _ = CombaseInterop.WindowsDeleteString(classNameH);
        }

        // Step 4: get IDirect3D11CaptureFramePoolStatics, call CreateFreeThreaded.
        var poolClassHr = CombaseInterop.WindowsCreateString(
            WgcInterop.Direct3D11CaptureFramePool_ClassName,
            (uint)WgcInterop.Direct3D11CaptureFramePool_ClassName.Length,
            out var poolClassH);
        if (poolClassHr != 0)
            throw new InvalidOperationException(
                $"WindowsCreateString(FramePool) failed: hr=0x{poolClassHr:X8}");
        try
        {
            // CreateFreeThreaded lives on Statics2, NOT Statics (v1). v1 only
            // has Create, which we cannot call from a non-STA thread.
            var poolStaticsIid = WgcInterop.IID_IDirect3D11CaptureFramePoolStatics2;
            var hrPoolFac = CombaseInterop.RoGetActivationFactory(
                poolClassH, in poolStaticsIid, out var poolStatics);
            if (hrPoolFac != 0)
                throw new InvalidOperationException(
                    $"RoGetActivationFactory(IDirect3D11CaptureFramePoolStatics2) failed: hr=0x{hrPoolFac:X8}");
            try
            {
                _framePool = InvokeCreateFreeThreaded(
                    poolStatics,
                    _graphicsDevice,
                    WgcInterop.DirectXPixelFormat_B8G8R8A8UIntNormalized,
                    numberOfBuffers: 2,
                    new WgcInterop.SizeInt32 { Width = ClientWidthPx, Height = ClientHeightPx });
            }
            finally
            {
                ComRelease(poolStatics);
            }
        }
        finally
        {
            _ = CombaseInterop.WindowsDeleteString(poolClassH);
        }

        // Step 5: framePool.CreateCaptureSession(item).
        _captureSession = InvokeCreateCaptureSession(_framePool, _captureItem);

        // Step 6: pre-allocate the staging texture.
        var desc = default(D3D11Interop.D3D11_TEXTURE2D_DESC);
        desc.Width = (uint)ClientWidthPx;
        desc.Height = (uint)ClientHeightPx;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = D3D11Interop.DXGI_FORMAT_B8G8R8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11Interop.D3D11_USAGE_STAGING;
        desc.CPUAccessFlags = D3D11Interop.D3D11_CPU_ACCESS_READ;

        // ID3D11Device::CreateTexture2D is vtable slot 5 (after IUnknown's 3).
        // Signature: HRESULT CreateTexture2D(D3D11_TEXTURE2D_DESC*, D3D11_SUBRESOURCE_DATA*, ID3D11Texture2D**)
        _stagingTexture = InvokeCreateTexture2D(_d3dDevice, ref desc);

        // Step 7: hook FrameArrived. The handler does the readback synchronously.
        _frameArrivedToken = InvokeAddFrameArrived(_framePool);

        // Step 8: start capture.
        InvokeStartCapture(_captureSession);
    }

    private static unsafe nint InvokeCreateForWindow(nint interopFactory, nint hwnd)
    {
        var vtable = *(nint**)interopFactory;
        var createForWindowPtr = vtable[3]; // slot 3 after IUnknown's 0..2
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)createForWindowPtr;
        var iid = WgcInterop.IID_IGraphicsCaptureItem;
        nint outItem;
        var hr = fn(interopFactory, hwnd, &iid, &outItem);
        if (hr != 0)
            throw new InvalidOperationException($"CreateForWindow failed: hr=0x{hr:X8}");
        return outItem;
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

    private static unsafe nint InvokeCreateFreeThreaded(
        nint poolStatics, nint device, uint pixelFormat, int numberOfBuffers, WgcInterop.SizeInt32 size)
    {
        // IDirect3D11CaptureFramePoolStatics2 layout:
        //   slots 0-2: IUnknown
        //   slots 3-5: IInspectable
        //   slot   6 : CreateFreeThreaded (the only method on Statics2).
        var vtable = *(nint**)poolStatics;
        var createPtr = vtable[6];
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, uint, int, WgcInterop.SizeInt32, nint*, int>)createPtr;
        nint outPool;
        var hr = fn(poolStatics, device, pixelFormat, numberOfBuffers, size, &outPool);
        if (hr != 0)
            throw new InvalidOperationException($"CreateFreeThreaded failed: hr=0x{hr:X8}");
        if (outPool == nint.Zero)
            throw new InvalidOperationException(
                "CreateFreeThreaded returned S_OK but null outPool (wrong vtable slot or IID).");
        return outPool;
    }

    private static unsafe nint InvokeCreateCaptureSession(nint framePool, nint item)
    {
        // IDirect3D11CaptureFramePool::CreateCaptureSession is at slot 10
        // (IUnknown 0-2, IInspectable 3-5, FramePool methods 6+: Recreate,
        // TryGetNextFrame, FrameArrivedAdd, FrameArrivedRemove, CreateCaptureSession).
        var vtable = *(nint**)framePool;
        var createPtr = vtable[10];
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)createPtr;
        nint outSession;
        var hr = fn(framePool, item, &outSession);
        if (hr != 0)
            throw new InvalidOperationException($"CreateCaptureSession failed: hr=0x{hr:X8}");
        return outSession;
    }

    private static unsafe nint InvokeCreateTexture2D(nint device, ref D3D11Interop.D3D11_TEXTURE2D_DESC desc)
    {
        // ID3D11Device::CreateTexture2D is at slot 5.
        var vtable = *(nint**)device;
        var createPtr = vtable[5];
        var fn = (delegate* unmanaged[Stdcall]<nint, ref D3D11Interop.D3D11_TEXTURE2D_DESC, nint, nint*, int>)createPtr;
        nint outTex;
        var hr = fn(device, ref desc, nint.Zero, &outTex);
        if (hr != 0)
            throw new InvalidOperationException($"CreateTexture2D failed: hr=0x{hr:X8}");
        return outTex;
    }

    private unsafe long InvokeAddFrameArrived(nint framePool)
    {
        // FrameArrivedAdd is at slot 8. Signature:
        //   HRESULT add_FrameArrived(ITypedEventHandler*, EventRegistrationToken*)
        var vtable = *(nint**)framePool;
        var addPtr = vtable[8];

        // Build a managed delegate that the runtime exposes as a function
        // pointer matching ITypedEventHandler::Invoke. We store the delegate
        // in a class field so it is not collected before Dispose().
        _frameArrivedDelegate = OnFrameArrivedNative;
        var handlerPtr = Marshal.GetFunctionPointerForDelegate(_frameArrivedDelegate);

        // Wrap in a hand-rolled IUnknown shim that exposes Invoke at slot 3
        // (the typed-event-handler signature WGC expects).
        var shim = EventHandlerShim.Create(handlerPtr);
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, long*, int>)addPtr;
        long token;
        var hr = fn(framePool, shim, &token);
        if (hr != 0)
            throw new InvalidOperationException($"add_FrameArrived failed: hr=0x{hr:X8}");
        // shim's refcount is 1 from Create; FrameArrivedAdd took its own ref.
        // We release our ref so removing the handler later destroys the shim.
        ComRelease(shim);
        return token;
    }

    private static unsafe void InvokeStartCapture(nint session)
    {
        // IGraphicsCaptureSession layout:
        //   slots 0-2: IUnknown
        //   slots 3-5: IInspectable
        //   slot   6 : StartCapture (the only method on the interface).
        // IClosable is a separate interface (IID 30D5A829-...) reachable via
        // QI; it does NOT appear in this vtable. The plan's slot-9 was past
        // the vtable end and produced a STATUS_ACCESS_VIOLATION.
        var vtable = *(nint**)session;
        var startPtr = vtable[6];
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)startPtr;
        var hr = fn(session);
        if (hr != 0)
            throw new InvalidOperationException($"StartCapture failed: hr=0x{hr:X8}");
    }

    // Hold the delegate alive for the session lifetime.
    private FrameArrivedNativeDelegate? _frameArrivedDelegate;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int FrameArrivedNativeDelegate(nint sender, nint args);

    private int OnFrameArrivedNative(nint sender, nint args)
    {
        try
        {
            // sender is the frame pool; call TryGetNextFrame (slot 7).
            var frame = InvokeTryGetNextFrame(sender);
            if (frame == nint.Zero) return 0;
            try
            {
                var captured = ReadbackFrame(frame);
                if (captured is not null)
                    _frames.Writer.TryWrite(captured);
            }
            finally
            {
                ComRelease(frame);
            }
        }
        catch
        {
            // Swallow: a single bad frame should not tear down the session.
        }
        return 0; // S_OK
    }

    private static unsafe nint InvokeTryGetNextFrame(nint framePool)
    {
        var vtable = *(nint**)framePool;
        var fnPtr = vtable[7];
        var fn = (delegate* unmanaged[Stdcall]<nint, nint*, int>)fnPtr;
        nint outFrame;
        var hr = fn(framePool, &outFrame);
        return hr == 0 ? outFrame : nint.Zero;
    }

    private unsafe CapturedFrame? ReadbackFrame(nint framePtr)
    {
        // Direct3D11CaptureFrame: SystemRelativeTime at slot 8 (after IUnknown 0-2 + IInspectable 3-5 + Surface 6 + ContentSize 7).
        // Returns 100-ns ticks (TimeSpan-shape).
        var fVtable = *(nint**)framePtr;
        var srtPtr = fVtable[8];
        var fnSrt = (delegate* unmanaged[Stdcall]<nint, long*, int>)srtPtr;
        long srt100ns;
        if (fnSrt(framePtr, &srt100ns) != 0) return null;

        // Surface at slot 6.
        var surfPtr = fVtable[6];
        var fnSurf = (delegate* unmanaged[Stdcall]<nint, nint*, int>)surfPtr;
        nint surface;
        if (fnSurf(framePtr, &surface) != 0) return null;
        try
        {
            // Surface implements IDirect3DDxgiInterfaceAccess; QI to get ID3D11Texture2D.
            var hrQi = ComQueryInterface(surface, WgcInterop.IID_IDirect3DDxgiInterfaceAccess, out var access);
            if (hrQi != 0) return null;
            try
            {
                // IDirect3DDxgiInterfaceAccess::GetInterface at slot 3.
                var aVtable = *(nint**)access;
                var giPtr = aVtable[3];
                var giFn = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)giPtr;
                var iidTex = new Guid("6F15AAF2-D208-4E89-9AB4-489535D34F9C"); // ID3D11Texture2D
                nint sourceTex;
                if (giFn(access, &iidTex, &sourceTex) != 0) return null;
                try
                {
                    // ID3D11DeviceContext::CopyResource at slot 47.
                    var ctxV = *(nint**)_d3dContext;
                    var copyPtr = ctxV[47];
                    var copyFn = (delegate* unmanaged[Stdcall]<nint, nint, nint, void>)copyPtr;
                    copyFn(_d3dContext, _stagingTexture, sourceTex);

                    // Map at slot 14: HRESULT Map(ID3D11Resource*, UINT, D3D11_MAP, UINT, D3D11_MAPPED_SUBRESOURCE*)
                    var mapPtr = ctxV[14];
                    var mapFn = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, D3D11Interop.D3D11_MAPPED_SUBRESOURCE*, int>)mapPtr;
                    D3D11Interop.D3D11_MAPPED_SUBRESOURCE mapped;
                    if (mapFn(_d3dContext, _stagingTexture, 0, D3D11Interop.D3D11_MAP_READ, 0, &mapped) != 0) return null;
                    try
                    {
                        var bytes = new byte[ClientWidthPx * ClientHeightPx * 4];
                        var rowBytes = ClientWidthPx * 4;
                        for (var y = 0; y < ClientHeightPx; y++)
                        {
                            Marshal.Copy(
                                mapped.pData + y * (int)mapped.RowPitch,
                                bytes,
                                y * rowBytes,
                                rowBytes);
                        }

                        // Convert SystemRelativeTime (100-ns ticks) to QPC ticks.
                        // TimeSpan ticks tick at 10MHz; QPC at QpcFrequency.
                        var qpc = checked((long)(srt100ns * (decimal)QpcFrequency / 10_000_000m));

                        // Unmap at slot 15.
                        var unmapPtr = ctxV[15];
                        var unmapFn = (delegate* unmanaged[Stdcall]<nint, nint, uint, void>)unmapPtr;
                        unmapFn(_d3dContext, _stagingTexture, 0);

                        return new CapturedFrame(bytes, ClientWidthPx, ClientHeightPx, qpc);
                    }
                    catch
                    {
                        // Best-effort unmap on error.
                        var unmapPtr2 = ctxV[15];
                        var unmapFn2 = (delegate* unmanaged[Stdcall]<nint, nint, uint, void>)unmapPtr2;
                        unmapFn2(_d3dContext, _stagingTexture, 0);
                        throw;
                    }
                }
                finally { ComRelease(sourceTex); }
            }
            finally { ComRelease(access); }
        }
        finally { ComRelease(surface); }
    }

    public void Dispose()
    {
        // Unhook FrameArrived first so no callback fires after Release runs,
        // then drop refs in reverse order of acquisition. We do NOT call
        // IClosable.Close on the session or framepool - IClosable is a
        // separate interface (IID 30D5A829-...) reachable only via QI; it
        // does not appear in either object's primary vtable. Relying on
        // ComRelease to drive refcount->0 is sufficient: the WGC objects'
        // destructors stop capture and free resources internally.
        if (_framePool != nint.Zero && _frameArrivedToken != 0)
        {
            unsafe
            {
                var v = *(nint**)_framePool;
                var rmPtr = v[9]; // FrameArrivedRemove on IDirect3D11CaptureFramePool
                var fn = (delegate* unmanaged[Stdcall]<nint, long, int>)rmPtr;
                _ = fn(_framePool, _frameArrivedToken);
            }
            _frameArrivedToken = 0;
        }
        if (_captureSession != nint.Zero) { ComRelease(_captureSession); _captureSession = nint.Zero; }
        if (_framePool != nint.Zero) { ComRelease(_framePool); _framePool = nint.Zero; }
        if (_stagingTexture != nint.Zero) { ComRelease(_stagingTexture); _stagingTexture = nint.Zero; }
        if (_captureItem != nint.Zero) { ComRelease(_captureItem); _captureItem = nint.Zero; }
        if (_graphicsDevice != nint.Zero) { ComRelease(_graphicsDevice); _graphicsDevice = nint.Zero; }
        if (_d3dContext != nint.Zero) { ComRelease(_d3dContext); _d3dContext = nint.Zero; }
        if (_d3dDevice != nint.Zero) { ComRelease(_d3dDevice); _d3dDevice = nint.Zero; }
        _frameArrivedDelegate = null;
        _frames.Writer.TryComplete();
    }
}
