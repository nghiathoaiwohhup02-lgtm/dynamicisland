using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Overview;

public sealed record ForecastItem(string Day, string Icon, string Range);

/// <summary>Tab đầu tiên: đồng hồ lớn, ngày, thời tiết hiện tại và 3 ngày tới.</summary>
public sealed partial class OverviewModule : IslandModule
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(20);
    private readonly DispatcherTimer _refresh = new() { Interval = RefreshInterval };
    private WeatherService _weather = null!;
    private int _failures;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _weatherIcon = "";
    [ObservableProperty] private string _temperature = "--°";
    [ObservableProperty] private string _condition = "Đang tải thời tiết...";
    [ObservableProperty] private string _details = "";

    public override string Id => "overview";
    public override string Title => "Tổng quan";
    public override string Glyph => "\uE80F";

    public ObservableCollection<ForecastItem> Forecast { get; } = [];

    protected override Task OnInitializeAsync()
    {
        _weather = new WeatherService(Context.Http);
        _clock.Tick += (_, _) => UpdateClock();
        _refresh.Tick += async (_, _) => await RefreshAsync();
        UpdateClock();
        _clock.Start();
        _refresh.Start();
        // Không chờ mạng khi khởi động.
        _ = RefreshAsync();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new OverviewView();

    private void UpdateClock()
    {
        var now = DateTime.Now;
        Time = now.ToString("HH:mm");
        Date = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd MMMM yyyy", Vietnamese));
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var settings = Context.Settings;
        var s = settings.Current;
        try
        {
            if (!string.Equals(s.City, s.ResolvedCity, StringComparison.OrdinalIgnoreCase))
            {
                var place = await _weather.GeocodeAsync(s.City);
                if (place is null)
                {
                    Condition = $"Không tìm thấy \"{s.City}\"";
                    return;
                }
                (s.ResolvedCity, s.Latitude, s.Longitude) = (s.City, place.Value.Lat, place.Value.Lon);
                settings.Save();
            }

            var report = await _weather.GetAsync(s.Latitude, s.Longitude);
            var (icon, text) = WeatherService.Describe(report.Code);
            City = s.City;
            WeatherIcon = icon;
            Temperature = $"{Math.Round(report.Temperature)}°";
            Condition = text;
            Details = $"Cảm giác {Math.Round(report.FeelsLike)}° · Ẩm {report.Humidity}% · Gió {Math.Round(report.WindKmh)} km/h";

            Forecast.Clear();
            foreach (var day in report.Days.Skip(1).Take(3))
            {
                var label = Vietnamese.TextInfo.ToTitleCase(day.Date.ToString("ddd", Vietnamese));
                Forecast.Add(new ForecastItem(label, WeatherService.Describe(day.Code).Icon, $"{Math.Round(day.Min)}/{Math.Round(day.Max)}°"));
            }
            _failures = 0;
            _refresh.Interval = RefreshInterval;
        }
        catch (Exception ex)
        {
            Log.Error("Lấy thời tiết lỗi", ex);
            Condition = "Không lấy được thời tiết";
            // Khởi động cùng Windows thường chạy trước khi có mạng: thử lại sau 1, 2, 4... phút thay vì chờ 20 phút.
            _failures++;
            _refresh.Interval = TimeSpan.FromMinutes(Math.Min(RefreshInterval.TotalMinutes, Math.Pow(2, _failures - 1)));
        }
    }

    public override void Dispose()
    {
        _clock.Stop();
        _refresh.Stop();
    }
}
