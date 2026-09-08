using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI.Xaml;

namespace WinCompare.UI.Helpers;

public static class PickerHelper
{
    public static async Task<StorageFile?> PickSingleFileAsync(string[] fileTypeFilters, Window? parentWindow = null)
    {
        var window = parentWindow ?? App.MainWindowInstance;
        var picker = new FileOpenPicker();

        if (window != null)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        if (fileTypeFilters == null || fileTypeFilters.Length == 0)
        {
            picker.FileTypeFilter.Add("*");
        }
        else
        {
            foreach (var filter in fileTypeFilters)
            {
                picker.FileTypeFilter.Add(filter);
            }
        }

        return await picker.PickSingleFileAsync();
    }

    public static async Task<StorageFolder?> PickSingleFolderAsync(Window? parentWindow = null)
    {
        var window = parentWindow ?? App.MainWindowInstance;
        var picker = new FolderPicker();

        if (window != null)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        picker.FileTypeFilter.Add("*");
        return await picker.PickSingleFolderAsync();
    }

    public static async Task<StorageFile?> PickSaveFileAsync(string defaultExtension, Dictionary<string, IList<string>> choices, string suggestedFileName, Window? parentWindow = null)
    {
        var window = parentWindow ?? App.MainWindowInstance;
        var picker = new FileSavePicker();

        if (window != null)
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        picker.DefaultFileExtension = defaultExtension;
        picker.SuggestedFileName = suggestedFileName;
        foreach (var kvp in choices)
        {
            picker.FileTypeChoices.Add(kvp.Key, kvp.Value);
        }

        return await picker.PickSaveFileAsync();
    }
}
