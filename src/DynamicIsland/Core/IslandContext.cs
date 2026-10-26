using System.Net.Http;
using System.Windows.Threading;

namespace DynamicIsland.Core;

/// <summary>Những thứ dùng chung mà module cần: cấu hình, HTTP, luồng giao diện.</summary>
public sealed class IslandContext
{
    public IslandContext(SettingsStore settings, HttpClient http, Dispatcher dispatcher)
    {
        Settings = settings;
        Http = http;
        Dispatcher = dispatcher;
    }

    public SettingsStore Settings { get; }
    public HttpClient Http { get; }
    public Dispatcher Dispatcher { get; }
}
