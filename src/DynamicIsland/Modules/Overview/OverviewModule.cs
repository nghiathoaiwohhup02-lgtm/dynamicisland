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
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMinutes(20) };
    private WeatherService _weather = null!;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _weatherIcon = "";
    [ObservableProperty] private string _temperature = "--°";
    [ObservableProperty] private string _condition = "Đang tải thời tiết...";
    [ObservableProperty] private string _details = "";

    public override string Id => "overview";
    public override string Title => "Tổng quan";
    public override string Glyph => "";

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
                Forecast.Add(new ForecastItem(label, WeatherService.Describe(day.Code).Icon, $"{Math.Round(day.Min)}° / {Math.Round(day.Max)}°"));
            }
        }
        catch (Exception ex)
        {
            Log.Error("Lấy thời tiết lỗi", ex);
            Condition = "Không lấy được thời tiết";
        }
    }

    public override void Dispose()
    {
        _clock.Stop();
        _refresh.Stop();
    }
}
