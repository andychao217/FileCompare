using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class DiffLineDisplayItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private int _lineIndex;
    public int LineIndex
    {
        get => _lineIndex;
        set => SetField(ref _lineIndex, value);
    }

    private string _leftLineNumText = string.Empty;
    public string LeftLineNumText
    {
        get => _leftLineNumText;
        set
        {
            if (SetField(ref _leftLineNumText, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPhantomLeft)));
            }
        }
    }

    private string _leftSign = " ";
    public string LeftSign
    {
        get => _leftSign;
        set => SetField(ref _leftSign, value);
    }

    private string _leftText = string.Empty;
    public string LeftText
    {
        get => _leftText;
        set => SetField(ref _leftText, value);
    }

    private Brush _leftBgBrush = new SolidColorBrush(Colors.Transparent);
    public Brush LeftBgBrush
    {
        get => _leftBgBrush;
        set => SetField(ref _leftBgBrush, value);
    }

    private Brush _leftFgBrush = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush LeftFgBrush
    {
        get => _leftFgBrush;
        set => SetField(ref _leftFgBrush, value);
    }

    private string _rightLineNumText = string.Empty;
    public string RightLineNumText
    {
        get => _rightLineNumText;
        set
        {
            if (SetField(ref _rightLineNumText, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPhantomRight)));
            }
        }
    }

    private string _rightSign = " ";
    public string RightSign
    {
        get => _rightSign;
        set => SetField(ref _rightSign, value);
    }

    private string _rightText = string.Empty;
    public string RightText
    {
        get => _rightText;
        set => SetField(ref _rightText, value);
    }

    private Brush _rightBgBrush = new SolidColorBrush(Colors.Transparent);
    public Brush RightBgBrush
    {
        get => _rightBgBrush;
        set => SetField(ref _rightBgBrush, value);
    }

    private Brush _rightFgBrush = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush RightFgBrush
    {
        get => _rightFgBrush;
        set => SetField(ref _rightFgBrush, value);
    }

    private bool _isDifference;
    public bool IsDifference
    {
        get => _isDifference;
        set => SetField(ref _isDifference, value);
    }

    public bool IsPhantomLeft => string.IsNullOrEmpty(LeftLineNumText);
    public bool IsPhantomRight => string.IsNullOrEmpty(RightLineNumText);
}

public sealed class SingleTextLineDisplayItem
{
    private string? _lineNumText;
    public int LineNumber { get; set; }
    public string LineNumText
    {
        get { return _lineNumText ?? LineNumber.ToString(); }
        set { _lineNumText = value; }
    }
    public string Content { get; set; } = string.Empty;
}

public sealed class ThreeWayMergeLineDisplayItem
{
    public int LineIndex { get; set; }

    public string LocalLineNumText { get; set; } = string.Empty;
    public string LocalSign { get; set; } = " ";
    public string LocalText { get; set; } = string.Empty;
    public Brush LocalBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush LocalFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string BaseLineNumText { get; set; } = string.Empty;
    public string BaseSign { get; set; } = " ";
    public string BaseText { get; set; } = string.Empty;
    public Brush BaseBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush BaseFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string RemoteLineNumText { get; set; } = string.Empty;
    public string RemoteSign { get; set; } = " ";
    public string RemoteText { get; set; } = string.Empty;
    public Brush RemoteBgBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    public Brush RemoteFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");

    public string Status { get; set; } = "Unchanged";
    public bool IsConflict => Status == "Conflict";
}
