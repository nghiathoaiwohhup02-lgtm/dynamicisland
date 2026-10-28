using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Work;

/// <summary>
/// Theo dõi đầu việc: nhiều danh sách, mỗi danh sách chia nhóm (ST-A2, ST-A3...).
/// Đánh dấu đã làm, quan trọng, đang chờ người khác; thêm/sửa/xóa/chuyển nhóm.
/// Lưu ở %AppData%\DynamicIsland\tasks.json. Thu gọn chỉ hiện số việc, không hiện nội dung.
/// </summary>
public sealed partial class WorkModule : IslandModule
{
    private const string NewGroupTarget = "+ Nhóm mới";
    private const string NewListTarget = "+ Danh sách mới";
    private static readonly string FilePath = Path.Combine(SettingsStore.Folder, "tasks.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private WorkData _data = new();
    private WorkList? _current;
    private bool _rebuildQueued;
    private List<string> _targets = [];
    private int _targetIndex;

    [ObservableProperty] private string _newText = "";
    [ObservableProperty] private string _targetLabel = "Chung";
    [ObservableProperty] private string _filter = "open";
    [ObservableProperty] private int _openTotal;
    [ObservableProperty] private int _importantTotal;
    [ObservableProperty] private bool _hasImportant;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _canDeleteList;

    public override string Id => "tasks";
    public override string Title => "Việc";
    public override string Glyph => "";
    public override bool CanPinCompact => true;
    public override double ExpandedHeight => 320;

    public ObservableCollection<WorkListChip> Chips { get; } = [];

    /// <summary>Các dòng đang hiện: tiêu đề nhóm (<see cref="WorkGroupHeader"/>) xen với việc (<see cref="WorkItem"/>).</summary>
    public ObservableCollection<object> Rows { get; } = [];

    public bool FilterOpen => Filter == "open";
    public bool FilterImportant => Filter == "important";
    public bool FilterAll => Filter == "all";

    partial void OnFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilterOpen));
        OnPropertyChanged(nameof(FilterImportant));
        OnPropertyChanged(nameof(FilterAll));
        Rebuild();
    }

    protected override Task OnInitializeAsync()
    {
        _data = Load();
        foreach (var item in _data.Lists.SelectMany(l => l.Items)) Track(item);
        _current = _data.Lists.FirstOrDefault();
        Rebuild();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new WorkView();

    protected override FrameworkElement CreateCompactView() => new WorkCompactView();

    [RelayCommand]
    private void SelectList(WorkListChip? chip)
    {
        if (chip is null || chip.List == _current) return;
        _current = chip.List;
        _targetIndex = 0;
        Rebuild();
    }

    [RelayCommand]
    private void SetFilter(string? filter) => Filter = filter ?? "open";

    /// <summary>Đổi chỗ sẽ thêm việc: lần lượt các nhóm, rồi "nhóm mới", "danh sách mới".</summary>
    [RelayCommand]
    private void CycleTarget()
    {
        if (_targets.Count == 0) return;
        _targetIndex = (_targetIndex + 1) % _targets.Count;
        TargetLabel = _targets[_targetIndex];
    }

    [RelayCommand]
    private void Add()
    {
        var text = NewText.Trim();
        if (text.Length == 0) return;
        var target = _targets.ElementAtOrDefault(_targetIndex) ?? "Chung";

        if (target == NewListTarget)
        {
            if (_data.Lists.All(l => !l.Name.Equals(text, StringComparison.OrdinalIgnoreCase)))
                _data.Lists.Add(new WorkList { Name = text });
            _current = _data.Lists.First(l => l.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
            _targetIndex = 0;
        }
        else if (_current is null)
        {
            return;
        }
        else if (target == NewGroupTarget)
        {
            if (!_current.Groups.Contains(text)) _current.Groups.Add(text);
            _targetIndex = _current.Groups.IndexOf(text);
        }
        else
        {
            var item = new WorkItem { Text = text, Group = _current.Groups[_targetIndex] };
            Track(item);
            _current.Items.Add(item);
        }

        NewText = "";
        Save();
        Rebuild();
    }

    [RelayCommand]
    private void Delete(WorkItem? item)
    {
        if (item is null || _current is null) return;
        item.PropertyChanged -= OnItemChanged;
        _current.Items.Remove(item);
        Save();
        Rebuild();
    }

    /// <summary>Chuyển việc sang nhóm kế tiếp trong danh sách.</summary>
    [RelayCommand]
    private void MoveGroup(WorkItem? item)
    {
        if (item is null || _current is null || _current.Groups.Count < 2) return;
        var index = _current.Groups.IndexOf(item.Group);
        item.Group = _current.Groups[(index + 1) % _current.Groups.Count];
    }

    [RelayCommand]
    private void ToggleWaiting(WorkItem? item)
    {
        if (item is not null) item.Waiting = !item.Waiting;
    }

    [RelayCommand]
    private void DeleteList()
    {
        if (_current is null || _current.Items.Count > 0) return;
        _data.Lists.Remove(_current);
        _current = _data.Lists.FirstOrDefault();
        _targetIndex = 0;
        Save();
        Rebuild();
    }

    private void Track(WorkItem item) => item.PropertyChanged += OnItemChanged;

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
        // Sửa chữ thì không dựng lại danh sách (sẽ mất ô đang gõ); đổi trạng thái thì sắp xếp lại.
        if (e.PropertyName == nameof(WorkItem.Text) || _rebuildQueued) return;
        _rebuildQueued = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _rebuildQueued = false;
            Rebuild();
        });
    }

    private void Rebuild()
    {
        Chips.Clear();
        foreach (var list in _data.Lists)
            Chips.Add(new WorkListChip(list) { Label = $"{list.Name} · {list.OpenCount}", IsSelected = list == _current });

        Rows.Clear();
        if (_current is not null)
        {
            // Nhóm có việc mà chưa có trong Groups (sửa tay file json) thì thêm vào cuối.
            foreach (var group in _current.Items.Select(i => i.Group).Distinct())
                if (!_current.Groups.Contains(group)) _current.Groups.Add(group);

            foreach (var group in _current.Groups)
            {
                var items = _current.Items
                    .Where(i => i.Group == group && Filter switch
                    {
                        "important" => i.Important && !i.Done,
                        "all" => true,
                        _ => !i.Done,
                    })
                    .OrderBy(i => i.Done)
                    .ThenByDescending(i => i.Important)
                    .ThenBy(i => i.Created)
                    .ToList();
                if (items.Count == 0) continue;
                Rows.Add(new WorkGroupHeader(group.Length == 0 ? "CHUNG" : group.ToUpperInvariant(),
                                             _current.Items.Count(i => i.Group == group && !i.Done)));
                foreach (var item in items) Rows.Add(item);
            }
        }

        IsEmpty = Rows.Count == 0;
        CanDeleteList = _current is not null && _current.Items.Count == 0;
        OpenTotal = _data.Lists.Sum(l => l.OpenCount);
        ImportantTotal = _data.Lists.Sum(l => l.Items.Count(i => i.Important && !i.Done));
        HasImportant = ImportantTotal > 0;

        _targets = (_current?.Groups.Select(g => g.Length == 0 ? "Chung" : g) ?? []).ToList();
        if (_current is not null) _targets.Add(NewGroupTarget);
        _targets.Add(NewListTarget);
        _targetIndex = Math.Clamp(_targetIndex, 0, _targets.Count - 1);
        TargetLabel = _targets[_targetIndex];
    }

    private static WorkData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<WorkData>(File.ReadAllText(FilePath)) ?? new WorkData();
        }
        catch (Exception ex)
        {
            Log.Error("Đọc tasks.json lỗi", ex);
            return new WorkData();
        }

        var seed = WorkSeed.Create();
        Save(seed);
        return seed;
    }

    private void Save() => Save(_data);

    private static void Save(WorkData data)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error("Không ghi được tasks.json", ex);
        }
    }
}
