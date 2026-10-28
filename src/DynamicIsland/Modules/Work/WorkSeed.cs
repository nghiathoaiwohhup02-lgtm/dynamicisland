namespace DynamicIsland.Modules.Work;

/// <summary>Dữ liệu ban đầu chép từ Sticky Notes của anh (việc đã gạch = đã làm). Chỉ dùng khi chưa có tasks.json.</summary>
internal static class WorkSeed
{
    public static WorkData Create()
    {
        var created = DateTime.Now;
        WorkItem Item(string text, string group = "", bool done = false, bool waiting = false) =>
            new() { Text = text, Group = group, Done = done, Waiting = waiting, Created = created = created.AddSeconds(1) };

        var whTool = new WorkList
        {
            Name = "WH-Tool",
            Groups = [""],
            Items =
            [
                Item("Nền tảng người dùng"),
                Item("Cấu trúc chuyển đổi C#", done: true),
                Item("Hệ thống chuyển đổi", done: true),
                Item("Sync trực tiếp - Không cần tắt", done: true),
                Item("Theme - Style - Follow a Luận", done: true),
                Item("User - Đăng nhập (Same với mail được cấp quyền vào Onedrive)", done: true),
                Item("Admin - Quản lý"),
                Item("Add link shortcut tương tự pyrevit", done: true),
                Item("Tool: auto check & chuyển đổi về đúng format WCP", done: true),
                Item("Phân tích cài đặt của Kinship", done: true),
                Item("Library", done: true),
                Item("Cài đặt cho người mới nhanh gọn", done: true),
                Item("Nút favorite tool"),
                Item("Thay logo cho toàn bộ tool"),
                Item("File cài đặt gồm cả toolkit .SDK10", done: true),
                Item("Nền tảng share cho teamleader"),
                Item("Setup full team (15)", done: true),
                Item("Sign-in"),
                Item("Set role"),
                Item("Extension thêm"),
                Item(".exe 1 click"),
            ],
        };

        var staircase = new WorkList
        {
            Name = "Staircase Model",
            Groups = ["", "ST-A4", "ST-A2", "ST-A3"],
            Items =
            [
                Item("RL2 - RL3: railing corner to be rounded (Level 1)"),
                Item("Linework Door (Auto)"),
                Item("Railing base on ramp (Level 1)", "ST-A4"),
                Item("EXIT Sign: Family bị lỗi", "ST-A4"),
                Item("Roof level. Update 3D following 2D", "ST-A2"),
                Item("aHuy check Ramma diện tích lỗ mở --> thêm tường bỏ railing", "ST-A2", done: true),
                Item("Family thang máy (dùng chung ST-A3)", "ST-A2"),
                Item("Update thang (TM74) - Update lần 2 --> đợi Siva phản hồi", "ST-A2", waiting: true),
                Item("Đợi ME dời doghouse", "ST-A2", waiting: true),
                Item("Dời mương né chân cột KC", "ST-A2", done: true),
                Item("URF: vị trí mương pending, chờ aĐ coor lại với Rama", "ST-A3", waiting: true),
                Item("Cột thép KC", "ST-A3"),
                Item("Sửa theo markup của a.Huy 22/06", "ST-A3"),
                Item("Model thang bộ trên mái theo markup của Rama", "ST-A3"),
            ],
        };

        return new WorkData { Lists = [whTool, staircase] };
    }
}
