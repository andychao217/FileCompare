using System.Windows.Media;

namespace WinCompare.Setup.Models;

public sealed class ThemeColorItem
{
    public string Name { get; init; } = string.Empty;
    public Color Color { get; init; }
    public Brush Brush => new SolidColorBrush(Color);

    public ThemeColorItem(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}
