using CccdCheckIn.Core;

namespace CccdCheckIn.App.Config;

/// <summary>
/// Cấu hình toàn bộ app (đọc từ appsettings.json, mọi trường có sẵn default để
/// app chạy được ngay cả khi thiếu file — tính "mở" cho khách tùy chỉnh).
/// PropertyGrid hiển thị trực tiếp các trường public này trong Cài đặt.
/// </summary>
public sealed class AppConfig
{
    public ReaderSettings Reader { get; set; } = new();
    public ScanSettings Scan { get; set; } = new();
    public StoreSettings Store { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public LicensingSettings Licensing { get; set; } = new();
}

/// <summary>Section "Licensing" — bật/tắt và thời lượng dùng thử.</summary>
public sealed class LicensingSettings
{
    public bool Enabled { get; set; } = true;
    public int TrialDays { get; set; } = 14;
}

/// <summary>Section "Reader" — cách nối với máy quét.</summary>
public sealed class ReaderSettings
{
    public string Mode { get; set; } = "Serial";        // "Serial" | "Simulated"

    public string Port { get; set; } = "";              // "" = tự dò theo ScannerVidPids
    public int BaudRate { get; set; } = 14400;   // máy quét khách (VID_DA23) xuất 14400
    public bool DtrEnable { get; set; } = true;
    public bool AutoDetectPort { get; set; } = true;
    public string ScannerVidPids { get; set; } = "VID_DA23&PID_1904";
    public int ReconnectIntervalMs { get; set; } = 3000;
}

/// <summary>Section "Scan" — ghi log check-in.</summary>
public sealed class ScanSettings
{
    public int DuplicateIgnoreSeconds { get; set; } = 5;
    public bool LogRejectedScans { get; set; } = true;
}

/// <summary>Section "Store" — nơi lưu.</summary>
public sealed class StoreSettings
{
    public string Type { get; set; } = "Sqlite";
    public string SqlitePath { get; set; } = "Data/checkin.db";
    public List<string> SaveFields { get; set; } = [.. Storage.Sqlite.SqliteCheckInStore.DefaultSaveFields];
}

/// <summary>Section "Ui" — hành vi dashboard.</summary>
public sealed class UiSettings
{
    public bool AutoStartListening { get; set; } = true;
    public int RecentRows { get; set; } = 20;
    public bool MaskCccdDigits { get; set; } = true;
    public string StatusFilter { get; set; } = "Today"; // "Today" | "All" — thống kê mặc định
}

/// <summary>Section "Export" — xuất CSV.</summary>
public sealed class ExportSettings
{
    public List<string> Fields { get; set; } = [.. Storage.Sqlite.CheckInCsvExporter.DefaultFields];
    public string CsvPath { get; set; } = "Data/export";
}

/// <summary>Giá trị core cần để dựng pipeline (duplicate-window, log-rejected).</summary>
public sealed class CoreConfig
{
    public TimeSpan DuplicateWindow { get; init; }
    public bool LogRejectedScans { get; init; }
}

public static class CoreConfigFactory
{
    public static CoreConfig From(AppConfig cfg) => new()
    {
        DuplicateWindow = TimeSpan.FromSeconds(Math.Max(0, cfg.Scan.DuplicateIgnoreSeconds)),
        LogRejectedScans = cfg.Scan.LogRejectedScans,
    };
}