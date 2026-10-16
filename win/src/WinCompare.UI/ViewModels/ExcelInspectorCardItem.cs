using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class ExcelInspectorCardItem
{
    public int ColumnIndex { get; set; }
    public string ColumnLetter { get; set; } = string.Empty;
    public string HeaderName { get; set; } = string.Empty;
    public string LeftDisplayValue { get; set; } = string.Empty;
    public string RightDisplayValue { get; set; } = string.Empty;
    public Brush HeaderNameFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush ColLetterFgBrush { get; set; } = App.GetThemeBrush("TextFillColorTertiaryBrush");
    public Brush LabelFgBrush { get; set; } = App.GetThemeBrush("TextFillColorSecondaryBrush");
    public Brush LeftFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush RightFgBrush { get; set; } = App.GetThemeBrush("TextFillColorPrimaryBrush");
    public Brush BorderBrush { get; set; } = App.GetThemeBrush("DividerStrokeColorDefaultBrush");
    public Visibility DiffDotVisibility { get; set; } = Visibility.Collapsed;
    public bool IsDiff { get; set; }
}
