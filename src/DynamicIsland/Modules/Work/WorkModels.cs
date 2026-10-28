using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DynamicIsland.Modules.Work;

/// <summary>Một đầu việc. Group rỗng = nhóm "Chung" của danh sách.</summary>
public sealed partial class WorkItem : ObservableObject
{
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _group = "";
    [ObservableProperty] private bool _important;
    [ObservableProperty] private bool _done;
    [ObservableProperty] private bool _waiting;

    public DateTime Created { get; set; } = DateTime.Now;
}

/// <summary>Một danh sách việc (vd. WH-Tool, Staircase Model), chia nhóm theo thứ tự trong Groups.</summary>
public sealed class WorkList
{
    public string Name { get; set; } = "";
    public List<string> Groups { get; set; } = [""];
    public List<WorkItem> Items { get; set; } = [];

    [JsonIgnore]
    public int OpenCount => Items.Count(i => !i.Done);
}

public sealed class WorkData
{
    public List<WorkList> Lists { get; set; } = [];
}

public sealed record WorkGroupHeader(string Title, int OpenCount);

/// <summary>Nút chọn danh sách trên đầu tab.</summary>
public sealed partial class WorkListChip(WorkList list) : ObservableObject
{
    public WorkList List { get; } = list;
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private bool _isSelected;
}
