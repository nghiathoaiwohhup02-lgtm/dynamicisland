using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DynamicIsland.Core;

/// <summary>Lớp cơ sở cho module: tạo giao diện một lần, giữ ngữ cảnh, hỗ trợ báo "peek".</summary>
public abstract partial class IslandModule : ObservableObject, IIslandModule
{
    private FrameworkElement? _expandedView;
    private FrameworkElement? _compactView;
    private bool _compactCreated;

    [ObservableProperty]
    private bool _isLive;

    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Glyph { get; }
    public virtual int LivePriority => 0;

    public virtual bool CanPinCompact => false;

    public virtual double ExpandedHeight => 120;

    protected IslandContext Context { get; private set; } = null!;

    public FrameworkElement ExpandedView => _expandedView ??= Bind(CreateExpandedView());

    public FrameworkElement? CompactView
    {
        get
        {
            if (!_compactCreated)
            {
                _compactCreated = true;
                var view = CreateCompactView();
                _compactView = view is null ? null : Bind(view);
            }
            return _compactView;
        }
    }

    public event EventHandler<PeekRequest>? PeekRequested;

    public async Task InitializeAsync(IslandContext context)
    {
        Context = context;
        await OnInitializeAsync();
    }

    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    protected abstract FrameworkElement CreateExpandedView();

    protected virtual FrameworkElement? CreateCompactView() => null;

    protected void Peek(string glyph, string title, string message, double seconds = 3, bool playSound = false, Action? activate = null) =>
        PeekRequested?.Invoke(this, new PeekRequest(glyph, title, message, TimeSpan.FromSeconds(seconds), playSound, activate));

    /// <summary>Chạy trên luồng giao diện (sự kiện từ WinRT/COM thường đến từ luồng khác).</summary>
    protected void OnUi(Action action)
    {
        var dispatcher = Context.Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private FrameworkElement Bind(FrameworkElement view)
    {
        view.DataContext = this;
        return view;
    }

    public virtual void Dispose() { }
}
