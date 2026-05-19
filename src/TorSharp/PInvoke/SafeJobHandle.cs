using System;
using Microsoft.Win32.SafeHandles;

namespace Knapcode.TorSharp.PInvoke;

internal class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeJobHandle(IntPtr handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        // ReleaseHandle must never throw: it is called from a finalizer / CER context.
        // Terminate all processes in the job, then close the native handle.
        var terminated = WindowsApi.TerminateJobObject(handle, uExitCode: 0);
        var closed = WindowsApi.CloseHandle(handle);
        return terminated && closed;
    }
}
