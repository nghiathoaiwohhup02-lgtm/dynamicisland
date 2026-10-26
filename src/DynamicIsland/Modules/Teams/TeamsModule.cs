using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Teams;

/// <summary>Bật/tắt mic, camera, giơ tay, rời họp Teams ngay trên đảo khi đang họp.</summary>
public sealed partial class TeamsModule : IslandModule
{
    private readonly TeamsClient _client = new();

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isInMeeting;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isVideoOn;
    [ObservableProperty] private bool _isHandRaised;
    [ObservableProperty] private string _status = "";

    public override string Id => "teams";
    public override string Title => "Teams";
    public override string Glyph => "\uE720";
    public override int LivePriority => 50;

    // Tắt mic/camera thì nền đỏ, đang bật thì nền xám (giống nút trong Teams).
    private static readonly Brush OffBrush = Frozen(Color.FromRgb(0xFF, 0x45, 0x3A));
    private static readonly Brush OnBrush = Frozen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    public Brush MicBackground => IsMuted ? OffBrush : OnBrush;
    public Brush CameraBackground => IsVideoOn ? OnBrush : OffBrush;

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    protected override Task OnInitializeAsync()
    {
        _client.Token = Context.Settings.Current.TeamsToken;
        _client.TokenReceived += token => OnUi(() =>
        {
            Context.Settings.Current.TeamsToken = token;
            Context.Settings.Save();
        });
        _client.ConnectionChanged += connected => OnUi(() =>
        {
            IsConnected = connected;
            if (!connected) Apply(new TeamsMeetingState(false, false, false, false, false, false, false));
            UpdateStatus();
        });
        _client.StateChanged += state => OnUi(() => Apply(state));
        UpdateStatus();
        _ = _client.RunAsync();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new TeamsView();

    protected override FrameworkElement CreateCompactView() => new TeamsCompactView();

    private void Apply(TeamsMeetingState state)
    {
        IsInMeeting = state.IsInMeeting;
        IsMuted = state.IsMuted;
        IsVideoOn = state.IsVideoOn;
        IsHandRaised = state.IsHandRaised;
        OnPropertyChanged(nameof(MicBackground));
        OnPropertyChanged(nameof(CameraBackground));
        IsLive = state.IsInMeeting;
        UpdateStatus();
    }

    private void UpdateStatus() => Status =
        !IsConnected ? "Chưa kết nối Teams. Trong Teams: Cài đặt → Quyền riêng tư → Quản lý API → Bật API."
        : !IsInMeeting ? "Đã kết nối Teams. Các nút sẽ dùng được khi anh vào họp."
        : string.IsNullOrEmpty(Context.Settings.Current.TeamsToken) ? "Bấm một nút, rồi bấm Cho phép trong Teams để ghép nối lần đầu."
        : "Đang họp";

    [RelayCommand] private Task ToggleMuteAsync() => _client.SendAsync("toggle-mute");
    [RelayCommand] private Task ToggleVideoAsync() => _client.SendAsync("toggle-video");
    [RelayCommand] private Task ToggleHandAsync() => _client.SendAsync("toggle-hand");
    [RelayCommand] private Task LeaveAsync() => _client.SendAsync("leave-call");

    public override void Dispose() => _client.Dispose();
}
