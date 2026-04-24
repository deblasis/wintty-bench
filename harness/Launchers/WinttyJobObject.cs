using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinttyBench.Launchers;

// Windows JobObject wrapper with KILL_ON_JOB_CLOSE. Any process assigned to
// the job, and every descendant it spawns, dies when the job handle is
// closed. This is the only reliable way to reap the full process tree on
// Windows: .NET's Process.Kill(entireProcessTree: true) walks parent-PID
// links at kill time, and Wintty's children (pwsh, cmd, wsl) are re-parented
// to PID 0 when Wintty exits, which orphans them out of reach.
//
// We observed this directly during Plan 2B: an aborted StartupRunner left
// 14 pwsh.exe processes alive across the box. JobObject prevents that.
[SupportedOSPlatform("windows")]
public sealed class WinttyJobObject : IDisposable
{
    private nint _handle;

    public WinttyJobObject()
    {
        _handle = CreateJobObjectW(lpJobAttributes: nint.Zero, lpName: null);
        if (_handle == 0)
        {
            throw new InvalidOperationException(
                $"CreateJobObjectW failed: win32 error {Marshal.GetLastWin32Error()}");
        }

        // KILL_ON_JOB_CLOSE: when the last handle to the job closes, Windows
        // terminates every process in the job. This is what protects us
        // against controller crashes and Ctrl+C aborts, not just graceful
        // Dispose().
        var info = default(JOBOBJECT_EXTENDED_LIMIT_INFORMATION);
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        var size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, buffer, fDeleteOld: false);
            if (!SetInformationJobObject(
                _handle,
                JobObjectInfoClass.ExtendedLimitInformation,
                buffer,
                (uint)size))
            {
                var err = Marshal.GetLastWin32Error();
                CloseHandle(_handle);
                _handle = 0;
                throw new InvalidOperationException(
                    $"SetInformationJobObject failed: win32 error {err}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // Assign a process to the job. Any descendants the process spawns after
    // this call are also in the job (nested-jobs is the default on Win8+).
    // Call this as soon as possible after Process.Start to minimize the
    // window in which early children could escape.
    public void AssignProcess(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        ObjectDisposedException.ThrowIf(_handle == 0, this);

        // Uses process.Handle (raw IntPtr) rather than SafeProcessHandle.
        // Callers must keep `process` alive until after WinttyJobObject is
        // Disposed; WinttyLauncher holds both refs via LaunchHandle and
        // respects that ordering. If that invariant ever changes, swap to
        // process.SafeHandle + DangerousAddRef/Release.
        if (!AssignProcessToJobObject(_handle, process.Handle))
        {
            throw new InvalidOperationException(
                $"AssignProcessToJobObject failed: win32 error {Marshal.GetLastWin32Error()}");
        }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            // CloseHandle triggers KILL_ON_JOB_CLOSE: every process in the
            // job dies synchronously before CloseHandle returns.
            CloseHandle(_handle);
            _handle = 0;
        }
    }

    // P/Invoke surface. Kept tight: no CsWin32 dependency, no wrapper crate.
    // Values match winnt.h / winbase.h as of current Windows SDK.

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    // Plain DllImport (not LibraryImport) to avoid pulling partial-class +
    // AllowUnsafeBlocks project-level changes into the harness just for four
    // functions. MarshalAs(UnmanagedType.Bool) adds a tiny runtime-marshal
    // shim for the BOOL returns; it is AOT/trim-safe, just not truly
    // "blittable" in the strict sense. If the surface grows beyond four
    // functions, migrate to LibraryImport for zero-overhead source-gen stubs.
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateJobObjectW(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        nint hJob,
        JobObjectInfoClass infoClass,
        nint lpJobObjectInformation,
        uint cbJobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);
}
