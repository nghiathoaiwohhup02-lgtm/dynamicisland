using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Notes;

public sealed partial class TodoItem : ObservableObject
{
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _done;
}

/// <summary>Ghi chú nhanh (tự lưu notes.txt) và danh sách việc cần làm (todo.json).</summary>
public sealed partial class NotesModule : IslandModule
{
    private static readonly string NoteFile = Path.Combine(SettingsStore.Folder, "notes.txt");
    private static readonly string TodoFile = Path.Combine(SettingsStore.Folder, "todo.json");
    private readonly DispatcherTimer _saveNote = new() { Interval = TimeSpan.FromSeconds(1) };

    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _newTodo = "";
    [ObservableProperty] private string _summary = "";

    public override string Id => "notes";
    public override string Title => "Ghi chú và việc cần làm";
    public override string Glyph => "";

    public ObservableCollection<TodoItem> Todos { get; } = [];

    protected override Task OnInitializeAsync()
    {
        try
        {
            if (File.Exists(NoteFile)) Note = File.ReadAllText(NoteFile);
            if (File.Exists(TodoFile))
                foreach (var item in JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(TodoFile)) ?? [])
                    Track(item);
        }
        catch (Exception ex) { Log.Error("Đọc ghi chú lỗi", ex); }

        _saveNote.Tick += (_, _) =>
        {
            _saveNote.Stop();
            Write(NoteFile, Note);
        };
        UpdateSummary();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new NotesView();

    // Gõ tới đâu lưu tới đó (gom lại 1 giây cho đỡ ghi đĩa liên tục).
    partial void OnNoteChanged(string value)
    {
        _saveNote.Stop();
        _saveNote.Start();
    }

    [RelayCommand]
    private void AddTodo()
    {
        var text = NewTodo.Trim();
        if (text.Length == 0) return;
        Track(new TodoItem { Text = text });
        NewTodo = "";
        SaveTodos();
    }

    [RelayCommand]
    private void RemoveTodo(TodoItem? item)
    {
        if (item is null) return;
        Todos.Remove(item);
        SaveTodos();
    }

    [RelayCommand]
    private void ClearDone()
    {
        foreach (var item in Todos.Where(t => t.Done).ToList()) Todos.Remove(item);
        SaveTodos();
    }

    private void Track(TodoItem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TodoItem.Done)) SaveTodos();
        };
        Todos.Add(item);
    }

    private void SaveTodos()
    {
        Write(TodoFile, JsonSerializer.Serialize(Todos.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var open = Todos.Count(t => !t.Done);
        Summary = Todos.Count == 0 ? "Việc cần làm" : $"Việc cần làm · còn {open}/{Todos.Count}";
    }

    private static void Write(string path, string content)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(path, content);
        }
        catch (Exception ex) { Log.Error($"Lưu {Path.GetFileName(path)} lỗi", ex); }
    }

    public override void Dispose()
    {
        if (_saveNote.IsEnabled) Write(NoteFile, Note);
        _saveNote.Stop();
    }
}
