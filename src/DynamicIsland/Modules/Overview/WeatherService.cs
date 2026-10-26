using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace DynamicIsland.Modules.Overview;

public sealed record WeatherDay(DateTime Date, int Code, double Min, double Max);

public sealed record WeatherReport(double Temperature, double FeelsLike, int Humidity, double WindKmh, int Code, IReadOnlyList<WeatherDay> Days);

/// <summary>Thời tiết từ Open-Meteo: miễn phí, không cần API key.</summary>
public sealed class WeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http) => _http = http;

    public async Task<(string Name, double Lat, double Lon)?> GeocodeAsync(string city, CancellationToken ct = default)
    {
        var url = $"https://geocoding-api.open-meteo.com/v1/search?count=1&language=vi&name={Uri.EscapeDataString(city)}";
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;
        var first = results[0];
        return (first.GetProperty("name").GetString() ?? city, first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
    }

    public async Task<WeatherReport> GetAsync(double lat, double lon, CancellationToken ct = default)
    {
        var url = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}") +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=4&timezone=auto";

        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var current = doc.RootElement.GetProperty("current");
        var daily = doc.RootElement.GetProperty("daily");

        var dates = daily.GetProperty("time").EnumerateArray().Select(d => DateTime.Parse(d.GetString()!, CultureInfo.InvariantCulture)).ToList();
        var codes = daily.GetProperty("weather_code").EnumerateArray().Select(c => c.GetInt32()).ToList();
        var max = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(t => t.GetDouble()).ToList();
        var min = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(t => t.GetDouble()).ToList();

        var days = dates.Select((d, i) => new WeatherDay(d, codes[i], min[i], max[i])).ToList();

        return new WeatherReport(
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("apparent_temperature").GetDouble(),
            current.GetProperty("relative_humidity_2m").GetInt32(),
            current.GetProperty("wind_speed_10m").GetDouble(),
            current.GetProperty("weather_code").GetInt32(),
            days);
    }

    /// <summary>Mã thời tiết WMO → (biểu tượng, mô tả tiếng Việt).</summary>
    public static (string Icon, string Text) Describe(int code) => code switch
    {
        0 => ("☀", "Trời quang"),
        1 => ("🌤", "Ít mây"),
        2 => ("⛅", "Có mây"),
        3 => ("☁", "Nhiều mây"),
        45 or 48 => ("🌫", "Sương mù"),
        51 or 53 or 55 or 56 or 57 => ("🌦", "Mưa phùn"),
        61 => ("🌧", "Mưa nhỏ"),
        63 or 66 => ("🌧", "Mưa vừa"),
        65 or 67 => ("🌧", "Mưa to"),
        71 or 73 or 75 or 77 or 85 or 86 => ("❄", "Tuyết"),
        80 or 81 => ("🌦", "Mưa rào"),
        82 => ("🌧", "Mưa rào to"),
        95 => ("⛈", "Dông"),
        96 or 99 => ("⛈", "Dông, mưa đá"),
        _ => ("🌡", "Không rõ"),
    };
}
