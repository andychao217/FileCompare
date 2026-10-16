using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinCompare.Core.Models;
using WinCompare.Core.Services;

namespace WinCompare.UI.Views;

public sealed partial class UpdateAvailableControl : UserControl
{
    public event Action? CloseRequested;

    private readonly string _newVersion;
    private readonly string _releaseNotes;
    private readonly string _releaseUrl;

    public UpdateAvailableControl() : this(UpdateCheckerService.CurrentVersion, "", "")
    {
    }

    public UpdateAvailableControl(string newVersion, string releaseNotes, string releaseUrl)
    {
        _newVersion = newVersion;
        _releaseNotes = releaseNotes;
        _releaseUrl = string.IsNullOrWhiteSpace(releaseUrl) ? "https://github.com/andychao217/FileCompare/releases" : releaseUrl;

        this.InitializeComponent();

        if (DragHeader != null)
        {
            DragHeader.PointerPressed += OnHeaderPointerPressed;
            DragHeader.PointerMoved += OnHeaderPointerMoved;
            DragHeader.PointerReleased += OnHeaderPointerReleased;
            DragHeader.PointerCaptureLost += OnHeaderPointerCaptureLost;
        }

        UpdateLocalization();
        LanguageManager.Instance.LanguageChanged += UpdateLocalization;
    }

    private Windows.Foundation.Point _startPoint;
    private bool _isDragging = false;

    private UIElement? GetDragReferenceElement()
    {
        return this.XamlRoot?.Content ?? App.GetWindowForElement(this)?.Content;
    }

    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var refElement = GetDragReferenceElement();
        if (refElement == null) return;

        var ptr = e.GetCurrentPoint(refElement);
        if (ptr.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _startPoint = ptr.Position;
            DragHeader.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            var refElement = GetDragReferenceElement();
            if (refElement == null) return;

            var curPoint = e.GetCurrentPoint(refElement).Position;
            double dx = curPoint.X - _startPoint.X;
            double dy = curPoint.Y - _startPoint.Y;

            DragTransform.X += dx;
            DragTransform.Y += dy;

            _startPoint = curPoint;
            e.Handled = true;
        }
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            DragHeader.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnHeaderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        UpdateCheckerService.Instance.SkipVersion(_newVersion);
        CloseRequested?.Invoke();
    }

    private void OnLaterClicked(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string targetUrl = !string.IsNullOrEmpty(UpdateCheckerService.Instance.DownloadAssetUrl)
                ? UpdateCheckerService.Instance.DownloadAssetUrl
                : _releaseUrl;

            await Launcher.LaunchUriAsync(new Uri(targetUrl));
        }
        catch { }
        finally
        {
            CloseRequested?.Invoke();
        }
    }

    private void UpdateLocalization()
    {
        if (HeaderTitleText != null)
        {
            HeaderTitleText.Text = LanguageManager.Instance.Text("softwareUpdateTitle");
        }
        if (CloseButton != null)
        {
            ToolTipService.SetToolTip(CloseButton, LanguageManager.Instance.Text("close"));
        }

        if (PromptHeadlineText != null)
        {
            PromptHeadlineText.Text = LanguageManager.Instance.Text("newVersionAvailableTitle");
        }
        if (PromptDetailText != null)
        {
            string format = LanguageManager.Instance.Text("newVersionPromptFormat");
            PromptDetailText.Text = string.Format(format, _newVersion, UpdateCheckerService.CurrentVersion);
        }

        string colon = LanguageManager.Instance.CurrentLanguage == AppLanguage.En ? ": " : "\uFF1A";
        if (ReleaseNotesLabel != null)
        {
            ReleaseNotesLabel.Text = LanguageManager.Instance.Text("releaseNotes") + colon;
        }
        if (ReleaseVersionBadge != null)
        {
            ReleaseVersionBadge.Text = "WinCompare v" + _newVersion;
        }

        if (ReleaseNotesBody != null)
        {
            string notes = UpdateCheckerService.Instance.LocalizedReleaseNotes(LanguageManager.Instance.CurrentLanguage);
            if (string.IsNullOrWhiteSpace(notes))
            {
                notes = string.IsNullOrWhiteSpace(_releaseNotes) ? LanguageManager.Instance.Text("noReleaseNotes") : _releaseNotes;
            }
            ReleaseNotesBody.Text = notes;
        }

        if (SkipVersionBtn != null)
        {
            SkipVersionBtn.Content = LanguageManager.Instance.Text("skipThisVersion");
        }
        if (LaterBtn != null)
        {
            LaterBtn.Content = LanguageManager.Instance.Text("later");
        }
        if (DownloadBtnText != null)
        {
            DownloadBtnText.Text = LanguageManager.Instance.Text("downloadUpdate");
        }
    }
}
