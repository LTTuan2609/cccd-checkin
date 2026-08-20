namespace CccdCheckIn.Reader.Serial;

using System.Management;
using System.Runtime.Versioning;

/// <summary>
/// Tự tìm cổng COM theo VID/PID (WMI Win32_PnPEntity). Máy quét CCCD của khách
/// hiện đang nằm ở COM3 (USB\VID_DA23&PID_1904) nhưng có thể đổi COM theo lúc cắm —
/// auto-detect giúp không phải sửa config mỗi lần. Trả rỗng nếu không tìm thấy.
/// Chỉ hỗ trợ Windows — đúng phạm vi app này.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ComPortAutoDetector
{
    private static readonly char[] ComSeparators = [',', ';'];

    /// <summary>
    /// Truyền danh sách VID/PID phân tách bằng phẩy: "VID_DA23&PID_1904,VID_1234&PID_5678".
    /// Chỉ gọi trên Windows (đã giới hạn bằng [SupportedOSPlatform]).
    /// </summary>
    public IReadOnlyList<string> DetectPorts(string vidPidList)
    {
        var targets = (vidPidList ?? "")
            .Split(ComSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (targets.Length == 0) return [];

        var found = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Name FROM Win32_PnPEntity");
            foreach (var obj in searcher.Get())
            {
                var deviceId = obj["DeviceID"] as string ?? "";
                var name = obj["Name"] as string ?? "";

                foreach (var target in targets)
                {
                    // Ưu tiên deviceId (đầy đủ VID&PID); fallback so khớp tên hiển thị.
                    bool match = deviceId.Contains(target, StringComparison.OrdinalIgnoreCase)
                        || name.Contains(target, StringComparison.OrdinalIgnoreCase);
                    if (match)
                    {
                        var com = ExtractCom(deviceId) ?? ExtractCom(name);
                        if (com is not null && !found.Contains(com)) found.Add(com);
                    }
                }
            }
        }
        catch
        {
            // WMI hỏng / không có quyền → trả rỗng, caller chọn tay.
        }
        return found;
    }

    private static string? ExtractCom(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var idx = text.IndexOf("COM", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var sb = new System.Text.StringBuilder();
        var i = idx + 3;
        while (i < text.Length && char.IsDigit(text[i])) sb.Append(text[i++]);
        return sb.Length > 0 ? "COM" + sb : null;
    }
}