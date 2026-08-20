namespace CccdCheckIn.Reader.Serial;

/// <summary>
/// Cấu hình cổng COM cho máy quét CCCD (section "Reader" trong appsettings.json).
/// Model giàu dữ liệu, không phụ thuộc Microsoft.Extensions.* — do AppConfigFactory đọc.
/// - Port = "" → tự dò AUTO theo ScannerVidPids; gán cổng tay trong Cài đặt sẽ ghi đè.
/// - Encoding "Auto" = thử UTF-8 trước, sai byte → fallback Windows-1252 (ScanBuffer xử lý).
/// </summary>
public sealed class ReaderPortConfig
{
    public const string SectionName = "Reader";

    /// <summary>Cổng COM cố định ("" = autodetect theo VID/PID).</summary>
    public string Port { get; set; } = "";

    /// <summary>Tốc độ baud của máy quét — máy của khách (VID_DA23) xuất 14400.</summary>
    public int BaudRate { get; set; } = 14400;

    /// <summary>Kích hoạt DTR — một số máy quét cần để "thức dậy".</summary>
    public bool DtrEnable { get; set; } = true;

    /// <summary>Tự tìm cổng theo ScannerVidPids khi Port rỗng.</summary>
    public bool AutoDetectPort { get; set; } = true;

    /// <summary>Danh sách VID/PID máy quét (phân tách phẩy), chuẩn WMI device id.</summary>
    public string ScannerVidPids { get; set; } = "VID_DA23&PID_1904";

    /// <summary>Thời gian chờ giữa các lần thử kết nối lại khi rút/cắm USB.</summary>
    public int ReconnectIntervalMs { get; set; } = 3000;
}