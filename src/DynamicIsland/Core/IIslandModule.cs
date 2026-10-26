using System.ComponentModel;
using System.Windows;

namespace DynamicIsland.Core;

/// <summary>
/// Một tiện ích trên đảo. Mỗi module tự lo dữ liệu của mình và cung cấp hai giao diện:
/// giao diện mở rộng (khi rê chuột vào đảo) và, nếu có hoạt động đang diễn ra, giao diện thu gọn.
/// Muốn thêm tiện ích mới chỉ cần viết một lớp kế thừa <see cref="IslandModule"/>
/// rồi đăng ký trong <see cref="ModuleCatalog"/>.
/// </summary>
public interface IIslandModule : INotifyPropertyChanged, IDisposable
{
    string Id { get; }
    string Title { get; }

    /// <summary>Ký tự biểu tượng trong font Segoe MDL2 Assets, dùng cho thanh tab.</summary>
    string Glyph { get; }

    /// <summary>Module đang có hoạt động cần hiện ở dạng thu gọn (nhạc đang phát, Pomodoro đang chạy...).</summary>
    bool IsLive { get; }

    /// <summary>Khi nhiều module cùng "live", module có độ ưu tiên cao hơn được hiện ở dạng thu gọn.</summary>
    int LivePriority { get; }

    /// <summary>Giao diện thu gọn vẫn có nghĩa khi không "live": bấm chọn tab thì thu gọn vẫn hiện tab này.</summary>
    bool CanPinCompact { get; }

    FrameworkElement ExpandedView { get; }
    FrameworkElement? CompactView { get; }

    event EventHandler<PeekRequest>? PeekRequested;

    Task InitializeAsync(IslandContext context);
}

/// <summary>
/// Yêu cầu đảo bung ra ngắn để báo một tin (hết giờ Pomodoro, pin yếu, đổi âm lượng...).
/// Có <paramref name="Activate"/> thì bấm vào tin sẽ chạy hành động đó (mở báo cáo, vào họp...).
/// </summary>
public sealed record PeekRequest(string Glyph, string Title, string Message, TimeSpan Duration, bool PlaySound = false, Action? Activate = null);
