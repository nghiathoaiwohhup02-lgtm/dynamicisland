using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DynamicIsland.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland");

    public static string FilePath { get; } = Path.Combine(Folder, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public static SettingsStore Load()
    {
        var store = new SettingsStore();
        try
        {
            if (File.Exists(FilePath))
                store.Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            // File hỏng thì giữ bản sao để người dùng sửa lại, chạy tiếp với cấu hình mặc định.
            Log.Error("Không đọc được settings.json", ex);
            try { File.Copy(FilePath, FilePath + ".broken", overwrite: true); } catch { }
        }
        store.Save();
        return store;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error("Không ghi được settings.json", ex);
        }
    }
}
