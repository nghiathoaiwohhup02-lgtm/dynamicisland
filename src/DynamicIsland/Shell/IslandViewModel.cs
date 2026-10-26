using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicIsland.Core;

namespace DynamicIsland.Shell;

public enum IslandState
{
    Collapsed,
    Peek,
    Expanded,
}

/// <summary>Trạng thái của đảo: đang thu gọn, đang báo tin (peek) hay đang mở rộng; module nào đang "live".</summary>
public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan ExpandDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(450);

    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _peekTimer;
    private bool _pointerInside;
    private bool _dragging;
    private bool _holdOpen;

    [ObservableProperty]
    private IslandState _state = IslandState.Collapsed;

    [ObservableProperty]
    private IIslandModule? _selectedModule;

    [ObservableProperty]
    private IIslandModule? _liveModule;

    [ObservableProperty]
    private PeekRequest? _peek;

    [ObservableProperty]
    private string _clockText = "";

    [ObservableProperty]
    private string _dateText = "";

    public IslandViewModel(IReadOnlyList<IIslandModule> modules)
    {
        Modules = new ObservableCollection<IIslandModule>(modules);
        SelectedModule = Modules.FirstOrDefault();

        foreach (var module in Modules)
        {
            module.PropertyChanged += OnModulePropertyChanged;
            module.PeekRequested += OnPeekRequested;
        }

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();
        _hoverTimer = new DispatcherTimer();
        _hoverTimer.Tick += OnHoverTimer;
        _peekTimer = new DispatcherTimer();
        _peekTimer.Tick += (_, _) => EndPeek();
    }

    public ObservableCollection<IIslandModule> Modules { get; }

    public async Task InitializeAsync(IslandContext context)
    {
        UpdateClock();
        _clock.Start();

        foreach (var module in Modules)
        {
            try
            {
                await module.InitializeAsync(context);
            }
            catch (Exception ex)
            {
                // Một module lỗi (vd. máy không có API nhạc) không được làm hỏng cả đảo.
                Log.Error($"Module {module.Id} khởi tạo lỗi", ex);
            }
        }
        UpdateLiveModule();
    }

    public void PointerEntered()
    {
        _pointerInside = true;
        if (_dragging) return;
        if (State == IslandState.Expanded) _hoverTimer.Stop();
        else RestartHoverTimer(ExpandDelay);
    }

    public void PointerLeft()
    {
        _pointerInside = false;
        if (_holdOpen) return;
        if (State == IslandState.Expanded) RestartHoverTimer(CollapseDelay);
        else _hoverTimer.Stop();
    }

    /// <summary>Giữ đảo mở dù chuột đã ra ngoài (đang gõ ghi chú, đang kéo file vào khay).</summary>
    public void SetHoldOpen(bool hold)
    {
        _holdOpen = hold;
        if (!hold && !_pointerInside && State == IslandState.Expanded) RestartHoverTimer(CollapseDelay);
    }

    /// <summary>Mở đảo ở tab của module có Id cho trước.</summary>
    public void ShowModule(string id)
    {
        var module = Modules.FirstOrDefault(m => m.Id == id);
        if (module is null) return;
        _hoverTimer.Stop();
        SelectedModule = module;
        State = IslandState.Expanded;
    }

    public void DropFiles(IEnumerable<string> paths)
    {
        if (Modules.OfType<IFileDropTarget>().FirstOrDefault() is { } target) target.AcceptFiles(paths);
    }

    /// <summary>Bấm vào tin đang hiện: chạy hành động gắn với tin. Trả về false nếu tin không có hành động.</summary>
    public bool ActivatePeek()
    {
        if (State != IslandState.Peek || Peek?.Activate is not Action activate) return false;
        EndPeek();
        try { activate(); }
        catch (Exception ex) { Log.Error("Hành động của thông báo lỗi", ex); }
        return true;
    }

    /// <summary>Đang kéo đảo đi chỗ khác: thu gọn và không tự mở ra.</summary>
    public void BeginDrag()
    {
        _dragging = true;
        _hoverTimer.Stop();
        if (State == IslandState.Expanded) State = IslandState.Collapsed;
    }

    public void EndDrag() => _dragging = false;

    public void ToggleExpanded()
    {
        _hoverTimer.Stop();
        if (State == IslandState.Expanded) Collapse();
        else Expand();
    }

    public void Collapse()
    {
        _hoverTimer.Stop();
        State = Peek is not null ? IslandState.Peek : IslandState.Collapsed;
    }

    private void Expand()
    {
        // Mở đảo khi đang có hoạt động thì nhảy thẳng tới tab của hoạt động đó.
        if (State == IslandState.Collapsed && LiveModule is not null) SelectedModule = LiveModule;
        State = IslandState.Expanded;
    }

    private void RestartHoverTimer(TimeSpan delay)
    {
        _hoverTimer.Stop();
        _hoverTimer.Interval = delay;
        _hoverTimer.Start();
    }

    private void OnHoverTimer(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_pointerInside) Expand();
        else Collapse();
    }

    private void OnPeekRequested(object? sender, PeekRequest request)
    {
        if (request.PlaySound) SystemSounds.Asterisk.Play();

        // Đang mở rộng thì người dùng đã nhìn thấy, không cần bung tin.
        if (State == IslandState.Expanded) return;

        Peek = request;
        State = IslandState.Peek;
        _peekTimer.Stop();
        _peekTimer.Interval = request.Duration;
        _peekTimer.Start();
    }

    private void EndPeek()
    {
        _peekTimer.Stop();
        Peek = null;
        if (State == IslandState.Peek) State = IslandState.Collapsed;
    }

    private void OnModulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IIslandModule.IsLive)) UpdateLiveModule();
    }

    private void UpdateLiveModule() =>
        LiveModule = Modules.Where(m => m.IsLive && m.CompactView is not null)
                            .OrderByDescending(m => m.LivePriority)
                            .FirstOrDefault();

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("HH:mm");
        DateText = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM", Vietnamese));
    }

    public void Dispose()
    {
        _clock.Stop();
        _hoverTimer.Stop();
        _peekTimer.Stop();
        foreach (var module in Modules)
        {
            module.PropertyChanged -= OnModulePropertyChanged;
            module.PeekRequested -= OnPeekRequested;
            module.Dispose();
        }
    }
}
