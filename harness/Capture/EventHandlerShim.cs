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
        // Accept any IID: WGC only QIs IUnknown and the typed-event-handler
        // IID, both of which we satisfy by returning self.
        var p = (Instance*)self;
        Interlocked.Increment(ref p->RefCount);
        *ppv = self;
        return 0; // S_OK
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
