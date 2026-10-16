using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinCompare.UI.ViewModels;

/// <summary>
/// 独立的 Tab 数据模型，用于窗口之间完整迁移 Tab（保留页面实例与对比状态）
/// </summary>
public sealed class CompareTabViewModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Header { get; set; } = string.Empty;

    public string Glyph { get; set; } = "\uE8A5";

    public bool IsWelcome { get; set; }

    public bool IsClosable { get; set; } = true;

    /// <summary>
    /// 对比页面内容实例（TextDiffPage / ExcelDiffPage / WordDiffPage / FolderDiffPage / ThreeWayMergePage / WelcomePage）
    /// </summary>
    public UIElement? ContentPage { get; set; }

    /// <summary>
    /// 绑定的 TabViewItem 控件引用
    /// </summary>
    public TabViewItem? TabItem { get; set; }
}
