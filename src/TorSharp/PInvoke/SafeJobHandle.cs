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
        return WindowsApi.TerminateJobObject(handle, uExitCode: 0);
    }
}
