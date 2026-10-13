using System.Text.Json;
using WinCompare.Core.Models;
using WinCompare.Core.Native;

namespace WinCompare.Core.Services;

/// <summary>
/// Manages a big-data Excel comparison session backed by the high-performance Rust core.
/// </summary>
public sealed class ExcelDiffSession : IDisposable
{
    private IntPtr _sessionPtr;
    private bool _disposed;

    private ExcelDiffSession(IntPtr sessionPtr)
    {
        _sessionPtr = sessionPtr;
    }

    public static ExcelDiffSession? Create(string leftPath, string rightPath, string? optionsJson = null)
    {
        IntPtr ptr = NativeMethods.maccompare_excel_create_session(leftPath, rightPath, optionsJson);
        if (ptr == IntPtr.Zero)
        {
            return null;
        }
        return new ExcelDiffSession(ptr);
    }

    public ExcelSummary GetSummary()
    {
        ThrowIfDisposed();
        IntPtr jsonPtr = NativeMethods.maccompare_excel_get_summary(_sessionPtr);
        var handle = new SafeRustStringHandle(jsonPtr);
        string json = handle.ToManagedStringAndDispose();

        if (string.IsNullOrEmpty(json))
        {
            return new ExcelSummary();
        }

        return JsonSerializer.Deserialize<ExcelSummary>(json) ?? new ExcelSummary();
    }

    public ExcelViewportData GetViewport(int sheetIndex, int startRow, int count)
    {
        ThrowIfDisposed();
        IntPtr jsonPtr = NativeMethods.maccompare_excel_get_viewport_json(
            _sessionPtr,
            (nuint)Math.Max(0, sheetIndex),
            (nuint)Math.Max(0, startRow),
            (nuint)Math.Max(1, count)
        );
        var handle = new SafeRustStringHandle(jsonPtr);
        string json = handle.ToManagedStringAndDispose();

        var result = new ExcelViewportData { SheetIndex = sheetIndex, StartRow = startRow };
        if (string.IsNullOrEmpty(json))
        {
            return result;
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<ExcelRowDiff>>(json);
            if (rows != null)
            {
                result.Rows = rows;
                return result;
            }
        }
        catch { }

        try
        {
            var data = JsonSerializer.Deserialize<ExcelViewportData>(json);
            if (data?.Rows != null)
            {
                result = data;
            }
        }
        catch { }

        return result;
    }

    public long FindNextDiffRow(int sheetIndex, int currentRow, bool backwards = false)
    {
        ThrowIfDisposed();
        return NativeMethods.maccompare_excel_find_next_diff_row(
            _sessionPtr,
            (nuint)Math.Max(0, sheetIndex),
            (nuint)Math.Max(0, currentRow),
            backwards
        );
    }

    private void ThrowIfDisposed()
    {
        if (_disposed || _sessionPtr == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(ExcelDiffSession));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_sessionPtr != IntPtr.Zero)
            {
                NativeMethods.maccompare_excel_free_session(_sessionPtr);
                _sessionPtr = IntPtr.Zero;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~ExcelDiffSession()
    {
        Dispose();
    }
}
