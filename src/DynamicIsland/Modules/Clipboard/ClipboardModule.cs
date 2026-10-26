using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Clipboard;

public sealed record ClipEntry(string Text, string Preview, DateTime Time)
{
    public string TimeText => Time.ToString("HH:mm");
}

/// <summary>
/// Lịch sử 20 đoạn chữ đã sao chép gần nhất; bấm để chép lại. Chỉ giữ trong bộ nhớ,
/// tắt ứng dụng là mất, không ghi ra đĩa (tránh lưu lại mật khẩu đã copy).
/// </summary>
public sealed partial class ClipboardModule : IslandModule
{
    private const int MaxEntries = 20;
    private const int MaxLength = 20_000;
    private string? _settingText;

    public override string Id => "clipboard";
    public override string Title => "Lịch sử clipboard";
    public override string Glyph => "";

    public ObservableCollection<ClipEntry> Entries { get; } = [];

    protected override Task OnInitializeAsync()
    {
        ClipboardMonitor.Changed += OnClipboardChanged;
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new ClipboardView();

    private void OnClipboardChanged()
    {
        string text;
        try
        {
            if (!System.Windows.Clipboard.ContainsText()) return;
            text = System.Windows.Clipboard.GetText();
        }
        catch
        {
            // Ứng dụng khác đang giữ clipboard; bỏ qua lần này.
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxLength) return;
        if (text == _settingText) { _settingText = null; return; }

        var existing = Entries.FirstOrDefault(e => e.Text == text);
        if (existing is not null) Entries.Remove(existing);
        Entries.Insert(0, new ClipEntry(text, MakePreview(text), DateTime.Now));
        while (Entries.Count > MaxEntries) Entries.RemoveAt(Entries.Count - 1);
    }

    private static string MakePreview(string text)
    {
        var oneLine = string.Join(" ", text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return oneLine.Length > 120 ? oneLine[..120] + "…" : oneLine;
    }

    [RelayCommand]
    private void Copy(ClipEntry? entry)
    {
        if (entry is null) return;
        try
        {
            _settingText = entry.Text;
            System.Windows.Clipboard.SetText(entry.Text);
            Entries.Remove(entry);
            Entries.Insert(0, entry with { Time = DateTime.Now });
            Peek("", "Đã chép lại", entry.Preview, seconds: 1.5);
        }
        catch (Exception ex)
        {
            Log.Error("Chép vào clipboard lỗi", ex);
        }
    }

    [RelayCommand]
    private void ClearAll() => Entries.Clear();

    public override void Dispose() => ClipboardMonitor.Changed -= OnClipboardChanged;
}
