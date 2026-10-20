using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WinCompare.UI.ViewModels;

public sealed class FolderSingleDisplayItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string RelativePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public string ModifiedFormatted { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE8A5";
    public Brush? IconColorBrush { get; set; }

    // Tree hierarchy & Collapse state
    public int Depth { get; set; }
    public Thickness IndentMargin => new Thickness(Depth * 16, 0, 0, 0);

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChevronAngle));
            }
        }
    }

    public double ChevronAngle => IsExpanded ? 90.0 : 0.0;
    public Visibility ChevronVisibility => IsDirectory ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChevronPlaceholderVisibility => !IsDirectory ? Visibility.Visible : Visibility.Collapsed;
    public string ParentPath { get; set; } = string.Empty;
    public List<string> AncestorPaths { get; set; } = new();
}

