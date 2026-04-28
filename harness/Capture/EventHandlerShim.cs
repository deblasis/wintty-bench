using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Capture;

// Hand-rolled IUnknown implementation that forwards a single method (Invoke
// at vtable slot 3) to a delegate function pointer. Used to satisfy WinRT's
// ITypedEventHandler<,> for the FrameArrived event without dragging in
// CsWinRT's projected handlers (which would require the Windows TFM).
//
// Layout: a single allocated block holds [vtable*, refcount, invokeFn].
// The QI/AddRef/Release function pointers are statically allocated and
// shared across all shims; the per-instance vtable copies them and adds
// the caller's invoke pointer at slot 3.
[SupportedOSPlatform("windows")]
internal static unsafe class EventHandlerShim
{
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private static readonly Guid IID_IUnknown =
        new("00000000-0000-0000-C000-000000000046");
    // IAgileObject is required because the FreeThreaded frame pool will QI
    // event handlers for it to confirm the handler is callable from any
    // thread. The shim's QI/AddRef/Release/Invoke have no thread affinity,
    // so claiming agility is correct.
    private static readonly Guid IID_IAgileObject =
        new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceFn(nint self, Guid* iid, nint* ppv);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint AddRefFn(nint self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(nint self);

    // Hold the delegate instances in static fields so the GC keeps them alive
    // for the lifetime of the AppDomain (matching the lifetime of the native
    // function pointers we hand to WGC via the per-instance vtable).
    private static readonly QueryInterfaceFn s_qi = QI;
    private static readonly AddRefFn s_addRef = AddRef;
    private static readonly ReleaseFn s_release = Release;

    private static readonly nint s_qiPtr = Marshal.GetFunctionPointerForDelegate(s_qi);
    private static readonly nint s_addRefPtr = Marshal.GetFunctionPointerForDelegate(s_addRef);
    private static readonly nint s_releasePtr = Marshal.GetFunctionPointerForDelegate(s_release);

    [StructLayout(LayoutKind.Sequential)]
    private struct Instance
    {
        public nint Vtable;
        public int RefCount;
        public nint InvokePtr;
    }

    public static nint Create(nint invokePtr)
    {
        // Allocate per-instance vtable so slot 3 can carry the caller's fn ptr.
        var vt = (nint*)Marshal.AllocHGlobal(sizeof(nint) * 4);
        vt[0] = s_qiPtr;
        vt[1] = s_addRefPtr;
        vt[2] = s_releasePtr;
        vt[3] = invokePtr;

        var p = (Instance*)Marshal.AllocHGlobal(sizeof(Instance));
        p->Vtable = (nint)vt;
        p->RefCount = 1;
        p->InvokePtr = invokePtr;
        return (nint)p;
    }

    private static int QI(nint self, Guid* iid, nint* ppv)
    {
        // We strictly implement IUnknown (slots 0-2) plus a single Invoke
        // method at slot 3 that matches ITypedEventHandler<,>::Invoke. We
        // are also agile (no thread affinity, no STA marshaling).
        //
        // Returning E_NOINTERFACE for IIDs we don't support is the COM
        // contract. The runtime caveat: WGC's FrameArrivedAdd internally
        // QIs handlers for the parameterized typed-event-handler IID
        // (a hash of TypedEventHandler<,>'s base IID with Direct3D11-
        // CaptureFramePool and IInspectable). We can't enumerate that
        // IID at compile time, so we accept any IID that isn't on a
        // known-bad list. Today the only known-bad list is empty; if
        // future smoke runs surface an IID we should reject (e.g. a
        // debugger probing IDispatch and then dispatching through our
        // 4-slot vtable), add it here. The IUnknown / IAgileObject
        // checks below short-circuit the common probe path; everything
        // else falls through to the same accept behavior with the same
        // refcount bump.
        var i = *iid;
        var p = (Instance*)self;
        if (i == IID_IUnknown || i == IID_IAgileObject)
        {
            Interlocked.Increment(ref p->RefCount);
            *ppv = self;
            return 0; // S_OK
        }
        // Default-accept: see contract caveat above.
        Interlocked.Increment(ref p->RefCount);
        *ppv = self;
        return 0;
    }

    private static uint AddRef(nint self)
    {
        var p = (Instance*)self;
        return (uint)Interlocked.Increment(ref p->RefCount);
    }

    private static uint Release(nint self)
    {
        var p = (Instance*)self;
        var c = Interlocked.Decrement(ref p->RefCount);
        if (c == 0)
        {
            Marshal.FreeHGlobal(p->Vtable);
            Marshal.FreeHGlobal(self);
        }
        return (uint)c;
    }
}
