using System.Reflection;
using System.Runtime.InteropServices;

namespace WinCompare.Core.Native;

/// <summary>
/// Direct P/Invoke bindings to the shared Rust core library (maccompare_ffi.dll / libmaccompare_ffi.dylib).
/// </summary>
public static class NativeMethods
{
    public const string LibraryName = "maccompare_ffi";

    static NativeMethods()
    {
        // Custom DllImport resolver to search adjacent bin folders or system paths
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveDll);
    }

    private static IntPtr ResolveDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == LibraryName)
        {
            string appDir = AppContext.BaseDirectory;
            string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" :
                               RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" : ".so";

            string prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";
            string fileName = $"{prefix}{LibraryName}{extension}";

            // Check adjacent directory
            string directPath = Path.Combine(appDir, fileName);
            if (File.Exists(directPath) && NativeLibrary.TryLoad(directPath, out IntPtr handle))
            {
                return handle;
            }

            // Check root ../../core/target/release/
            string relativeRustPath = Path.Combine(appDir, "..", "..", "..", "..", "..", "core", "target", "release", fileName);
            if (File.Exists(relativeRustPath) && NativeLibrary.TryLoad(relativeRustPath, out handle))
            {
                return handle;
            }

            // Fallback to standard system resolution
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out handle))
            {
                return handle;
            }
        }
        return IntPtr.Zero;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool maccompare_is_available();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_compare_text_json(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string left,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string right,
        [MarshalAs(UnmanagedType.I1)] bool ignoreWhitespace,
        [MarshalAs(UnmanagedType.I1)] bool ignoreCase
    );

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_merge_three_way_json(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string local,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string @base,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string remote
    );

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_compare_folders_json(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string leftPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string rightPath,
        int modeInt,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? excludePatternsJson
    );

    // MARK: - Big-Data Excel Diff Session

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_excel_create_session(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string leftPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string rightPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? optionsJson
    );

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_excel_get_summary(IntPtr sessionPtr);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr maccompare_excel_get_viewport_json(
        IntPtr sessionPtr,
        nuint sheetIndex,
        nuint startRow,
        nuint count
    );

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long maccompare_excel_find_next_diff_row(
        IntPtr sessionPtr,
        nuint sheetIndex,
        nuint currentRow,
        [MarshalAs(UnmanagedType.I1)] bool backwards
    );

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void maccompare_excel_free_session(IntPtr sessionPtr);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_rust_string(IntPtr ptr);
}
