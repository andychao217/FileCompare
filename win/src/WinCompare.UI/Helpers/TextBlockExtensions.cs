using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Helpers;

public static class TextBlockExtensions
{
    public static readonly DependencyProperty FormattedTokensProperty =
        DependencyProperty.RegisterAttached(
            "FormattedTokens",
            typeof(IEnumerable<WordDiffTokenItem>),
            typeof(TextBlockExtensions),
            new PropertyMetadata(null, OnFormattedTokensChanged));

    public static IEnumerable<WordDiffTokenItem>? GetFormattedTokens(DependencyObject obj)
    {
        return (IEnumerable<WordDiffTokenItem>?)obj.GetValue(FormattedTokensProperty);
    }

    public static void SetFormattedTokens(DependencyObject obj, IEnumerable<WordDiffTokenItem>? value)
    {
        obj.SetValue(FormattedTokensProperty, value);
    }

    private static void OnFormattedTokensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (d is TextBlock textBlock && e.NewValue is IEnumerable<WordDiffTokenItem> tokens)
            {
                textBlock.Text = string.Concat(System.Linq.Enumerable.Select(tokens, t => t.Text));
            }
        }
        catch
        {
        }
    }
}
