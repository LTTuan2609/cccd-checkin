using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CccdCheckIn.App.Config;

/// <summary>
/// Đọc appsettings.json thành AppConfig. AppConfigFactory dùng riêng cho App
/// (file JSON cạnh exe), tách biệt khỏi plugin reader/store — mỗi plugin tự
/// đọc section của mình, không phụ thuộc Microsoft.Extensions.*.
/// </summary>
public static class AppConfigFactory
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load(string jsonPath)
    {
        AppConfig config;
        if (File.Exists(jsonPath))
        {
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(jsonPath), JsonOpts) ?? new AppConfig();
            }
            catch (JsonException ex)
            {
                // File hỏng JSON — không crash, báo rõ + dùng default (khách sửa lại file).
                throw new InvalidOperationException(
                    $"{Path.GetFileName(jsonPath)} không đọc được (JSON lỗi: {ex.Message}). " +
                    "App dùng cấu hình mặc định — kiểm tra lại file cấu hình.", ex);
            }
        }
        else
        {
            // Không có file → mặc định toàn bộ (app chạy ngay, tự dò COM).
            config = new AppConfig();
        }

        // String List có thể bị null từ JSON → ép về mặc định.
        ApplyDefaults(config);
        return config;
    }

    public static void ApplyDefaults(AppConfig cfg)
    {
        if (cfg.Store.SaveFields is not { Count: > 0 })
            cfg.Store.SaveFields = [.. Storage.Sqlite.SqliteCheckInStore.DefaultSaveFields];
        if (cfg.Export.Fields is not { Count: > 0 })
            cfg.Export.Fields = [.. Storage.Sqlite.CheckInCsvExporter.DefaultFields];
    }

    /// <summary>
    /// Vị trí appsettings.json: ưu tiên file cạnh exe thật (nơi khách hàng chỉnh sửa).
    /// Với publish 1 file, AppContext.BaseDirectory trỏ tới thư mục giải nén tạm —
    /// đọc ở đó thì mọi sửa đổi appsettings.json cạnh exe đều bị bỏ qua.
    /// </summary>
    public static string AppSettingsPath(string baseDir)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exeDir))
        {
            var besideExe = Path.Combine(exeDir, "appsettings.json");
            if (File.Exists(besideExe)) return besideExe;
        }

        var local = Path.Combine(baseDir, "appsettings.json");
        if (File.Exists(local)) return local;

        // Chưa có file nào → trả về vị trí cạnh exe để SettingsForm.Save ghi ra đó.
        return string.IsNullOrEmpty(exeDir) ? local : Path.Combine(exeDir, "appsettings.json");
    }

    /// <summary>Ghi AppConfig đã sửa vào appsettings.json (SettingsForm sau khi bấm Lưu).</summary>
    public static void Save(AppConfig config, string jsonPath)
    {
        // Merge với file cũ để không nuốt key không nằm trong model (DataBits/Parity/StopBits, v.v.).
        var existing = new Dictionary<string, JsonElement>();
        if (File.Exists(jsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                foreach (var p in doc.RootElement.EnumerateObject())
                    existing[p.Name] = p.Value.Clone();
            }
            catch (JsonException)
            {
                // File cũ hỏng — ghi đè bằng model (không nuốt thêm gì).
            }
        }

        var json = JsonSerializer.Serialize(new
        {
            config.Reader, config.Scan, config.Store, config.Ui, config.Export,
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });

        if (existing.Count > 0)
        {
            var merged = JsonNode.Parse(json)!.AsObject();
            foreach (var (name, el) in existing)
            {
                if (!merged.ContainsKey(name))
                    merged[name] = JsonNode.Parse(el.GetRawText());
            }
            json = merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        File.WriteAllText(jsonPath, json);
    }
}