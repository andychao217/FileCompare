namespace WinCompare.UI.ViewModels;

public sealed class OutlineHeadingItem
{
    public string Title { get; set; } = string.Empty;
    public int Index { get; set; }
    public override string ToString() => Title;
}


