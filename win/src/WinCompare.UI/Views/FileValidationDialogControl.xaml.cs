using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views
{
    public sealed partial class FileValidationDialogControl : UserControl
    {
        private Action? _onPrimaryAction;
        private Action? _onCancelAction;

        public FileValidationDialogControl()
        {
            this.InitializeComponent();
            PrimaryBtn.Click += (s, e) => _onPrimaryAction?.Invoke();
            CancelBtn.Click += (s, e) => _onCancelAction?.Invoke();
        }

        public void Setup(
            string title, 
            string message, 
            string? primaryBtnText, 
            Action onPrimary, 
            Action onCancel,
            bool isDestructive = false)
        {
            DialogTitleText.Text = title;
            DialogMessageText.Text = message;
            _onPrimaryAction = onPrimary;
            _onCancelAction = onCancel;

            if (isDestructive)
            {
                DialogIcon.Glyph = "\uE74D";
                DialogIcon.Foreground = App.GetThemeBrush("StatusDangerBrush");
                CancelBtn.Content = LanguageManager.Instance.Text("cancel");
                CancelBtn.Visibility = Visibility.Visible;
                PrimaryBtn.Content = !string.IsNullOrEmpty(primaryBtnText) ? primaryBtnText : LanguageManager.Instance.Text("clear");
                PrimaryBtn.Background = App.GetThemeBrush("DangerButtonBackground");
                PrimaryBtn.Foreground = App.GetThemeBrush("TextOnAccentBrush");
                PrimaryBtn.BorderBrush = App.GetThemeBrush("DangerButtonBorder");
            }
            else if (!string.IsNullOrEmpty(primaryBtnText))
            {
                DialogIcon.Glyph = "\uE8AB";
                DialogIcon.ClearValue(FontIcon.ForegroundProperty);
                CancelBtn.Content = LanguageManager.Instance.Text("cancel");
                CancelBtn.Visibility = Visibility.Visible;
                PrimaryBtn.Content = primaryBtnText;
                PrimaryBtn.ClearValue(Button.BackgroundProperty);
                PrimaryBtn.ClearValue(Button.ForegroundProperty);
                PrimaryBtn.ClearValue(Button.BorderBrushProperty);
            }
            else
            {
                DialogIcon.Glyph = "\uE7BA";
                DialogIcon.Foreground = App.GetThemeBrush("StatusWarningBrush");
                CancelBtn.Visibility = Visibility.Collapsed;
                PrimaryBtn.Content = LanguageManager.Instance.Text("btnGotIt");
                PrimaryBtn.ClearValue(Button.BackgroundProperty);
                PrimaryBtn.ClearValue(Button.ForegroundProperty);
                PrimaryBtn.ClearValue(Button.BorderBrushProperty);
            }
        }
    }
}
