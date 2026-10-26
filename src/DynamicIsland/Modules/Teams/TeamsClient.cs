using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicIsland.Modules.Teams;

public sealed record TeamsMeetingState(bool IsInMeeting, bool IsMuted, bool IsVideoOn, bool IsHandRaised, bool CanToggleMute, bool CanToggleVideo, bool CanLeave);

/// <summary>
/// Nói chuyện với Teams qua "Third-party app API" mà Teams mở sẵn trên máy (ws://127.0.0.1:8124).
/// Cần bật trong Teams: Cài đặt → Quyền riêng tư → Quản lý API → Bật API. Lần đầu bấm một nút khi đang họp,
/// Teams sẽ hỏi cho phép ghép nối; sau đó Teams gửi lại mã (token) để lần sau tự kết nối.
/// </summary>
public sealed class TeamsClient : IDisposable
{
    private const string Endpoint = "ws://127.0.0.1:8124";
    private readonly CancellationTokenSource _stop = new();
    private ClientWebSocket? _socket;
    private int _requestId;

    public event Action<TeamsMeetingState>? StateChanged;
    public event Action<string>? TokenReceived;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public string? Token { get; set; }

    /// <summary>Vòng kết nối lại: Teams tắt/mở hay mất kết nối thì thử lại sau vài giây.</summary>
    public async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(_stop.Token);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Teams chưa chạy hoặc chưa bật API: chờ rồi thử lại.
            }
            ConnectionChanged?.Invoke(false);
            try { await Task.Delay(TimeSpan.FromSeconds(10), _stop.Token); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ConnectAndListenAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        var query = $"token={Uri.EscapeDataString(Token ?? "")}&protocol-version=2.0.0&manufacturer=DynamicIsland" +
                    "&device=Windows&app=Dynamic%20Island&app-version=0.3";
        await socket.ConnectAsync(new Uri($"{Endpoint}?{query}"), ct);
        _socket = socket;
        ConnectionChanged?.Invoke(true);

        var buffer = new byte[16 * 1024];
        var message = new StringBuilder();
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;
            message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage) continue;
            Handle(message.ToString());
            message.Clear();
        }
        _socket = null;
    }

    private void Handle(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return; }
        if (root is null) return;

        if (root["tokenRefresh"]?.GetValue<string>() is { Length: > 0 } token)
        {
            Token = token;
            TokenReceived?.Invoke(token);
        }

        if (root["meetingUpdate"] is JsonNode update)
        {
            var state = update["meetingState"];
            var perms = update["meetingPermissions"];
            StateChanged?.Invoke(new TeamsMeetingState(
                Bool(state, "isInMeeting"),
                Bool(state, "isMuted"),
                Bool(state, "isVideoOn"),
                Bool(state, "isHandRaised"),
                Bool(perms, "canToggleMute"),
                Bool(perms, "canToggleVideo"),
                Bool(perms, "canLeave")));
        }
    }

    private static bool Bool(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<bool>(out var b) && b;

    /// <summary>Gửi lệnh: toggle-mute, toggle-video, toggle-hand, leave-call...</summary>
    public async Task SendAsync(string action)
    {
        if (_socket is not { State: WebSocketState.Open } socket) return;
        var payload = JsonSerializer.Serialize(new { action, parameters = new { }, requestId = Interlocked.Increment(ref _requestId) });
        await socket.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, _stop.Token);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _socket?.Abort();
    }
}
