using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;
using Windows.Media.Control;
using MediaSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace DynamicIsland.Modules.Media;

/// <summary>
/// Điều khiển nhạc qua System Media Transport Controls của Windows: dùng được với Spotify,
/// YouTube trên Edge/Chrome, Groove, VLC... (ứng dụng nào hiện trong bảng âm lượng của Windows).
/// Thu gọn chỉ hiện sóng nhạc cho kín đáo; tên bài, ca sĩ, ảnh bìa chỉ hiện khi mở ra.
/// </summary>
public sealed partial class MediaModule : IslandModule
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private MediaSession? _session;

    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _trackTitle = "Không có nhạc đang phát";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private BitmapImage? _artwork;

    public override string Id => "media";
    public override string Title => "Nhạc";
    public override string Glyph => "\uE8D6";
    public override int LivePriority => 10;

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseGlyph));
        IsLive = value;
    }

    protected override async Task OnInitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => OnUi(AttachCurrentSession);
        // Đóng/mở ứng dụng phát nhạc không phải lúc nào cũng báo CurrentSessionChanged.
        _manager.SessionsChanged += (_, _) => OnUi(AttachCurrentSession);
        AttachCurrentSession();
    }

    protected override FrameworkElement CreateExpandedView() => new MediaView();

    protected override FrameworkElement CreateCompactView() => new MediaCompactView();

    private void AttachCurrentSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        _session = _manager?.GetCurrentSession();
        HasSession = _session is not null;

        if (_session is null)
        {
            IsPlaying = false;
            TrackTitle = "Không có nhạc đang phát";
            Artist = Source = "";
            Artwork = null;
            return;
        }

        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        Source = FriendlySource(_session.SourceAppUserModelId);
        UpdatePlayback();
        _ = UpdatePropertiesAsync();
    }

    private void OnMediaPropertiesChanged(MediaSession sender, MediaPropertiesChangedEventArgs args) =>
        OnUi(() => _ = UpdatePropertiesAsync());

    private void OnPlaybackInfoChanged(MediaSession sender, PlaybackInfoChangedEventArgs args) =>
        OnUi(UpdatePlayback);

    private void UpdatePlayback()
    {
        var status = _session?.GetPlaybackInfo()?.PlaybackStatus;
        IsPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    private async Task UpdatePropertiesAsync()
    {
        var session = _session;
        if (session is null) return;
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (props is null || session != _session) return;

            TrackTitle = string.IsNullOrWhiteSpace(props.Title) ? "Không rõ tên bài" : props.Title;
            Artist = props.Artist ?? "";
            Artwork = props.Thumbnail is null ? null : await LoadThumbnailAsync(props.Thumbnail);

            // Không báo khi đổi bài: thông báo hiện ở chế độ thu gọn sẽ lộ tên bài.
        }
        catch (Exception ex)
        {
            Log.Error("Đọc thông tin bài hát lỗi", ex);
        }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference reference)
    {
        try
        {
            using var winStream = await reference.OpenReadAsync();
            using var stream = winStream.AsStreamForRead();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 160;
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string FriendlySource(string appId)
    {
        var id = appId.ToLowerInvariant();
        if (id.Contains("spotify")) return "Spotify";
        if (id.Contains("msedge")) return "Microsoft Edge";
        if (id.Contains("chrome")) return "Google Chrome";
        if (id.Contains("firefox")) return "Firefox";
        if (id.Contains("zunemusic") || id.Contains("groove")) return "Groove Music";
        if (id.Contains("vlc")) return "VLC";
        return Path.GetFileNameWithoutExtension(appId);
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (_session is not null) await _session.TryTogglePlayPauseAsync();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (_session is not null) await _session.TrySkipNextAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        if (_session is not null) await _session.TrySkipPreviousAsync();
    }
}
