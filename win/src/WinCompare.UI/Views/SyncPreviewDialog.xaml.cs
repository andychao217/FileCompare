using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinCompare.Core.Services;
using WinCompare.UI.ViewModels;

namespace WinCompare.UI.Views;

public sealed partial class SyncPreviewDialog : UserControl
{
    public event Action? Confirmed;
    public event Action? Cancelled;

    public SyncPreviewDialog(IReadOnlyList<SyncPlanItemDisplay> plan)
    {
        this.InitializeComponent();

        int totalCount = plan.Count;
        int previewCount = Math.Min(totalCount, 1000);

        if (totalCount == 0)
        {
            EmptyPanel.Visibility = Visibility.Visible;
            ListPanel.Visibility = Visibility.Collapsed;
            ExecuteBtn.IsEnabled = false;
        }
        else
        {
            EmptyPanel.Visibility = Visibility.Collapsed;
            ListPanel.Visibility = Visibility.Visible;
            ExecuteBtn.IsEnabled = true;

            OperationsListView.ItemsSource = totalCount > 1000 ? plan.Take(1000).ToList() : plan;
        }

        UpdateLocalization(totalCount, previewCount);

        this.Unloaded += (s, e) =>
        {
            OperationsListView.ItemsSource = null;
        };
    }

    private void UpdateLocalization(int totalCount, int previewCount)
    {
        TitleBlock.Text = LanguageManager.Instance.Text("dryRunTitle");
        SubtitleBlock.Text = LanguageManager.Instance.Text("dryRunSubtitle");
        EmptyTextBlock.Text = LanguageManager.Instance.Text("completelyInSync");
        CancelBtn.Content = LanguageManager.Instance.Text("cancelAction");
        ExecuteBtn.Content = LanguageManager.Instance.Text("executeSync");

        string pendingOperations = LanguageManager.Instance.Text("pendingOperations");
        string itemsCount = LanguageManager.Instance.Text("itemsCount");
        CountHeaderBlock.Text = $"{pendingOperations} ({totalCount} {itemsCount}):";

        if (totalCount > previewCount)
        {
            string limitFormat = LanguageManager.Instance.Text("previewLimitNote");
            LimitNoteBlock.Text = string.Format(limitFormat, previewCount, totalCount - previewCount);
            LimitNoteBlock.Visibility = Visibility.Visible;
        }
        else
        {
            LimitNoteBlock.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            Cancelled?.Invoke();
        });
    }

    private void OnExecuteClicked(object sender, RoutedEventArgs e)
    {
        ExecuteBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        ExecuteBtn.Content = LanguageManager.Instance.Text("executing");
        this.DispatcherQueue.TryEnqueue(() =>
        {
            Confirmed?.Invoke();
        });
    }
}
