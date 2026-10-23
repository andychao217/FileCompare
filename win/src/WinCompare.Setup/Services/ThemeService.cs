using System;
using System.Collections.Generic;
using System.Windows.Media;
using WinCompare.Setup.Models;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinCompare.Setup.Services;

public sealed class ThemeService
{
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
    public static ThemeService Instance => _instance.Value;

    public IReadOnlyList<ThemeColorItem> AccentColors { get; } = new List<ThemeColorItem>
    {
        new("Windows Blue", Color.FromRgb(0x00, 0x78, 0xD4)),
        new("Iris Purple",  Color.FromRgb(0x78, 0x52, 0xFF)),
        new("Mint Green",   Color.FromRgb(0x10, 0x7C, 0x41)),
        new("Sunset Orange",Color.FromRgb(0xF7, 0x63, 0x0C)),
        new("Rose Pink",    Color.FromRgb(0xE3, 0x00, 0x8C))
    };

    public void SetTheme(string themeMode)
    {
        switch (themeMode.ToLowerInvariant())
        {
            case "light":
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Acrylic);
                break;
            case "dark":
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Acrylic);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }

        // Apply system accent color according to user preference
        try
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        catch
        {
            ApplicationAccentColorManager.Apply(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }

        // Ensure text on accent background is always pure white
        System.Windows.Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(Colors.White);
    }

    public void SetAccentColor(Color color)
    {
        ApplicationAccentColorManager.Apply(color);
    }
}
