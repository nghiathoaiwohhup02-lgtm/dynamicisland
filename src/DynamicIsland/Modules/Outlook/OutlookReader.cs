using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DynamicIsland.Modules.Outlook;

public sealed record MeetingInfo(string EntryId, string Subject, DateTime Start, DateTime End, string Location, string? TeamsUrl);

public sealed record MailInfo(string EntryId, string Sender, string Subject, DateTime Received);

public sealed record OutlookSnapshot(IReadOnlyList<MeetingInfo> Meetings, int UnreadCount, IReadOnlyList<MailInfo> Unread);

/// <summary>
/// Đọc lịch và hộp thư từ Outlook (bản cổ điển) đang chạy trên máy qua COM, không cần quyền IT hay đăng nhập thêm.
/// Chỉ đọc; không gửi, không xóa, không đánh dấu đã đọc. Mọi hàm phải gọi trên luồng STA (xem StaTask).
/// </summary>
public static partial class OutlookReader
{
    private const int FolderInbox = 6;
    private const int FolderCalendar = 9;

    [GeneratedRegex(@"https://teams\.microsoft\.com/l/meetup-join/[^\s""<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsLink();

    /// <summary>Chỉ nối vào Outlook đang mở; không tự bật Outlook lên.</summary>
    public static bool IsOutlookRunning()
    {
        var processes = Process.GetProcessesByName("OUTLOOK");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    public static OutlookSnapshot Read(int mailCount)
    {
        dynamic? app = null;
        try
        {
            app = CreateApp();
            dynamic ns = app.GetNamespace("MAPI");
            return new OutlookSnapshot(ReadMeetings(ns), ReadUnreadCount(ns), ReadUnread(ns, mailCount));
        }
        finally
        {
            if (app is not null) Marshal.FinalReleaseComObject(app);
        }
    }

    public static void Display(string entryId)
    {
        dynamic app = CreateApp();
        try
        {
            dynamic item = app.GetNamespace("MAPI").GetItemFromID(entryId);
            item.Display();
        }
        finally
        {
            Marshal.FinalReleaseComObject(app);
        }
    }

    private static dynamic CreateApp()
    {
        var type = Type.GetTypeFromProgID("Outlook.Application")
                   ?? throw new InvalidOperationException("Máy chưa cài Outlook bản cổ điển");
        return Activator.CreateInstance(type)!;
    }

    private static List<MeetingInfo> ReadMeetings(dynamic ns)
    {
        var now = DateTime.Now;
        dynamic items = ns.GetDefaultFolder(FolderCalendar).Items;
        items.IncludeRecurrences = true;
        items.Sort("[Start]");

        // Outlook so sánh ngày theo định dạng ngày giờ của Windows.
        var culture = CultureInfo.CurrentCulture;
        var filter = $"[End] >= '{now.ToString("g", culture)}' AND [Start] <= '{now.Date.AddDays(1).ToString("g", culture)}'";
        dynamic found = items.Restrict(filter);

        // Duyệt bằng GetFirst/GetNext: khi IncludeRecurrences bật, Count không đáng tin.
        var result = new List<MeetingInfo>();
        for (dynamic? item = found.GetFirst(); item is not null; item = found.GetNext())
        {
            try
            {
                // Bỏ sự kiện cả ngày (nghỉ phép, sinh nhật...).
                if ((bool)item.AllDayEvent) continue;
                string body = "";
                try { body = (string)(item.Body ?? ""); } catch { }
                var link = TeamsLink().Match(body);
                result.Add(new MeetingInfo(
                    (string)item.EntryID,
                    (string)(item.Subject ?? "(không tiêu đề)"),
                    (DateTime)item.Start,
                    (DateTime)item.End,
                    (string)(item.Location ?? ""),
                    link.Success ? link.Value : null));
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
            if (result.Count >= 5) break;
        }
        return result;
    }

    private static int ReadUnreadCount(dynamic ns) => (int)ns.GetDefaultFolder(FolderInbox).UnReadItemCount;

    private static List<MailInfo> ReadUnread(dynamic ns, int count)
    {
        dynamic items = ns.GetDefaultFolder(FolderInbox).Items.Restrict("[UnRead] = True");
        items.Sort("[ReceivedTime]", true);

        var result = new List<MailInfo>();
        for (dynamic? item = items.GetFirst(); item is not null; item = items.GetNext())
        {
            try
            {
                // Hộp thư có thể chứa lời mời họp, báo nhận...; chỉ lấy thư thường (MailItem = 43).
                if ((int)item.Class != 43) continue;
                result.Add(new MailInfo(
                    (string)item.EntryID,
                    (string)(item.SenderName ?? ""),
                    (string)(item.Subject ?? "(không tiêu đề)"),
                    (DateTime)item.ReceivedTime));
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
            if (result.Count >= count) break;
        }
        return result;
    }
}
