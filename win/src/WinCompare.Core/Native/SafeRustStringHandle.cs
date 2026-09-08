using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinCompare.Core.Native;

/// <summary>
/// SafeHandle wrapping a C-string allocated by Rust that must be freed via `free_rust_string`.
/// Guarantees no memory leaks across the native P/Invoke boundary.
/// </summary>
public sealed class SafeRustStringHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeRustStringHandle() : base(ownsHandle: true)
    {
    }

    public SafeRustStringHandle(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.free_rust_string(handle);
            handle = IntPtr.Zero;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Converts UTF-8 null-terminated bytes to a managed string and releases the native memory.
    /// </summary>
    public string ToManagedStringAndDispose()
    {
        try
        {
            if (IsInvalid) return string.Empty;
            return Marshal.PtrToStringUTF8(handle) ?? string.Empty;
        }
        finally
        {
            Dispose();
        }
    }
}
