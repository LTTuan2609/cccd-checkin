namespace CccdCheckIn.Storage.Sqlite;

using System.Text;
using CccdCheckIn.Core.Models;

/// <summary>
/// Xuất CSV log check-in có BOM UTF-8 để Excel mở tiếng Việt đúng.
/// Chọn cột theo Export.Fields (mặc định ít hơn SaveFields để tránh PII không cần thiết).
/// </summary>
public sealed class CheckInCsvExporter
{
    private readonly IReadOnlyCollection<string> _fields;

    /// <param name="fields">Tên cột (gồm "CheckedInAt" và "CccdNumber", xem README để biết danh sách).</param>
    public CheckInCsvExporter(IReadOnlyCollection<string> fields)
    {
        _fields = fields.Count == 0 ? DefaultFields : fields;
    }

    public static readonly string[] DefaultFields =
        ["CheckedInAt", "CccdNumber", "FullName", "Address"];

    /// <summary>Ghi CSV ra path, trả về path đã ghi (cùng tham số).</summary>
    public async Task<string> ExportAsync(string path, IEnumerable<CheckInRecord> records)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        // Header
        sb.Append(string.Join(',', _fields.Select(CsvEscape))).Append("\r\n");

        foreach (var r in records)
            sb.Append(string.Join(',', _fields.Select(f => CsvEscape(ValueOf(r, f))))).Append("\r\n");

        // BOM UTF-8 để Excel hiểu file là UTF-8 (tiếng Việt đúng dấu).
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static string ValueOf(CheckInRecord r, string field) => field switch
    {
        nameof(CheckInRecord.Id) => r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        nameof(CheckInRecord.CccdNumber) => r.CccdNumber,
        nameof(CheckInRecord.OldIdNumber) => r.OldIdNumber,
        nameof(CheckInRecord.FullName) => r.FullName,
        nameof(CheckInRecord.DateOfBirth) => FormatDate(r.DateOfBirth),
        nameof(CheckInRecord.Gender) => r.Gender,
        nameof(CheckInRecord.Address) => r.Address,
        nameof(CheckInRecord.IssueDate) => FormatDate(r.IssueDate),
        nameof(CheckInRecord.CheckedInAt) => FormatDate(r.CheckedInAt.ToLocalTime()),
        nameof(CheckInRecord.RawPayload) => r.RawPayload ?? "",
        _ => "", // field lạ trong config → bỏ qua, không crash
    };

    private static string FormatDate(DateTime dt) =>
        dt == default ? "" : dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Escape CSV: ký tự ", ',' và \r\n → wrap dấu nháy khi cần.</summary>
    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var v = value.Replace("\"", "\"\"");
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v + "\"";
        return v;
    }
}