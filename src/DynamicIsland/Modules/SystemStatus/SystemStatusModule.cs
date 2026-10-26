using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;
using NAudio.CoreAudioApi;
using Forms = System.Windows.Forms;

namespace DynamicIsland.Modules.SystemStatus;

/// <summary>Pin và âm lượng. Báo khi cắm/rút sạc, pin yếu, hoặc âm lượng đổi bằng phím.</summary>
public sealed partial class SystemStatusModule : IslandModule
{
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(20) };
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private bool _updatingFromDevice;
    private bool? _lastPlugged;
    private int _lastWarnedLevel = 101;

    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private int _batteryPercent;
    [ObservableProperty] private bool _isCharging;
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private bool _hasAudio;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private string _deviceName = "";

    public override string Id => "system";
    public override string Title => "Pin và âm lượng";
    public override string Glyph => "";

    public string BatteryGlyph => IsCharging ? "" : "";
    public string VolumeGlyph => IsMuted || Volume < 1 ? "" : Volume < 34 ? "" : Volume < 67 ? "" : "";

    protected override Task OnInitializeAsync()
    {
        UpdateBattery(announce: false);
        _poll.Tick += (_, _) =>
        {
            UpdateBattery(announce: true);
            EnsureAudioDevice();
        };
        _poll.Start();
        EnsureAudioDevice();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new SystemStatusView();

    private void UpdateBattery(bool announce)
    {
        var status = Forms.SystemInformation.PowerStatus;
        HasBattery = status.BatteryChargeStatus != Forms.BatteryChargeStatus.NoSystemBattery &&
                     status.BatteryChargeStatus != Forms.BatteryChargeStatus.Unknown;
        var plugged = status.PowerLineStatus == Forms.PowerLineStatus.Online;
        BatteryPercent = (int)Math.Round(status.BatteryLifePercent * 100);
        IsCharging = plugged;
        OnPropertyChanged(nameof(BatteryGlyph));

        if (!HasBattery)
        {
            BatteryText = "Máy bàn: không có pin";
            return;
        }

        BatteryText = plugged
            ? BatteryPercent >= 100 ? "Đã sạc đầy" : "Đang sạc"
            : status.BatteryLifeRemaining > 0 ? $"Còn khoảng {FormatDuration(status.BatteryLifeRemaining)}" : "Đang dùng pin";

        if (!announce) { _lastPlugged = plugged; return; }

        if (_lastPlugged is not null && plugged != _lastPlugged)
            Peek(plugged ? "" : "", plugged ? "Đã cắm sạc" : "Đã rút sạc", $"Pin {BatteryPercent}%");
        _lastPlugged = plugged;

        if (plugged) _lastWarnedLevel = 101;
        else if (BatteryPercent <= 10 && _lastWarnedLevel > 10) Warn(10);
        else if (BatteryPercent <= 20 && _lastWarnedLevel > 20) Warn(20);
    }

    private void Warn(int level)
    {
        _lastWarnedLevel = level;
        Peek("", "Pin yếu", $"Còn {BatteryPercent}%, nên cắm sạc", seconds: 5, playSound: true);
    }

    private static string FormatDuration(int seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} giờ {t.Minutes} phút" : $"{t.Minutes} phút";
    }

    // Lấy loa mặc định; gọi lại định kỳ để theo kịp khi đổi tai nghe/loa.
    private void EnsureAudioDevice()
    {
        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            {
                DetachDevice();
                HasAudio = false;
                DeviceName = "Không có thiết bị phát âm thanh";
                return;
            }

            var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_device is not null && device.ID == _device.ID)
            {
                device.Dispose();
                return;
            }

            DetachDevice();
            _device = device;
            _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
            HasAudio = true;
            DeviceName = _device.FriendlyName;
            ReadVolume(_device.AudioEndpointVolume.MasterVolumeLevelScalar, _device.AudioEndpointVolume.Mute);
        }
        catch (Exception ex)
        {
            Log.Error("Đọc thiết bị âm thanh lỗi", ex);
            HasAudio = false;
        }
    }

    private void DetachDevice()
    {
        if (_device is null) return;
        try { _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification; } catch { }
        _device.Dispose();
        _device = null;
    }

    private void OnVolumeNotification(AudioVolumeNotificationData data) =>
        OnUi(() =>
        {
            var changed = Math.Abs(data.MasterVolume * 100 - Volume) >= 1 || data.Muted != IsMuted;
            ReadVolume(data.MasterVolume, data.Muted);
            if (changed)
                Peek(VolumeGlyph, IsMuted ? "Tắt tiếng" : $"Âm lượng {Math.Round(Volume)}%", DeviceName, seconds: 1.5);
        });

    private void ReadVolume(float scalar, bool muted)
    {
        _updatingFromDevice = true;
        Volume = Math.Round(scalar * 100);
        IsMuted = muted;
        _updatingFromDevice = false;
        OnPropertyChanged(nameof(VolumeGlyph));
    }

    partial void OnVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(VolumeGlyph));
        if (_updatingFromDevice || _device is null) return;
        try { _device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(value / 100, 0, 1); }
        catch (Exception ex) { Log.Error("Đặt âm lượng lỗi", ex); }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (_device is null) return;
        try { _device.AudioEndpointVolume.Mute = !_device.AudioEndpointVolume.Mute; }
        catch (Exception ex) { Log.Error("Tắt tiếng lỗi", ex); }
    }

    public override void Dispose()
    {
        _poll.Stop();
        DetachDevice();
        _enumerator?.Dispose();
    }
}
