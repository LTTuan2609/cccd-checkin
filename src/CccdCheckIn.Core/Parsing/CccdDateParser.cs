using System.Globalization;

namespace CccdCheckIn.Core.Parsing;

/// <summary>
/// Parse ngày ở các định dạng QR CCCD quan sát được:
///   - "DDMMYYYY"  (biến thể chuẩn: 21031983)
///   - "dd/MM/yyyy"(biến thể Zalo:  21/03/1983)
///   - "dd-MM-yyyy"(dự phòng, an toàn khi có dấu gạch)
/// Trả về null nếu không khớp — không bao giờ ném.
/// </summary>
public static class CccdDateParser
{
    private static readonly string[] Formats = ["ddMMyyyy", "dd/MM/yyyy", "dd-MM-yyyy"];

    /// <summary>Parse ngày hoặc trả về null. Input rỗng/null → null (người gọi quyết định xử lý).</summary>
    public static DateTime? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        foreach (var format in Formats)
        {
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var parsed))
                return parsed;
        }
        return null;
    }
}