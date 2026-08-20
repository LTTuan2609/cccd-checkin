namespace CccdCheckIn.Storage.Sqlite;

using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Models;
using Microsoft.Data.Sqlite;

/// <summary>
/// Lưu đầy đủ các field của CitizenInfo + raw payload theo cấu hình SaveFields.
/// SQLite: 1 file DB, ghi atomic, query được. Dùng UTC cho checked_in_at; display local.
/// Thread-Safe: mỗi lệnh mở kết nối riêng → an toàn từ nhiều thread (serial reader).
/// </summary>
public sealed class SqliteCheckInStore : ICheckInStore, IRejectedLogStore, IDisposable, IAsyncDisposable
{
    private readonly string _connectionString;

    public SqliteCheckInStore(string dbPath, IReadOnlyCollection<string>? saveFields = null)
    {
        SaveFields = saveFields ?? DefaultSaveFields;

        var full = Path.GetFullPath(dbPath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5,     // tránh chờ vô hạn khi DB bị khóa ngoài
            Pooling = false,        // không giữ file handle → DB không bị khóa, dễ sao lưu/copy
        };
        _connectionString = builder.ToString();
        CheckInSchema.EnsureCreated(_connectionString);
    }

    public IReadOnlyCollection<string> SaveFields { get; }

    public static readonly string[] DefaultSaveFields =
        ["CccdNumber", "FullName", "DateOfBirth", "Gender", "Address", "IssueDate"];

    /// <summary>
    /// Timezone hiển thị khi đọc lại DB. Mặc định 0 (UTC); có thể cấu hình
    /// TimeZoneInfo ở App để dashboard hiển thị giờ local đúng.
    /// </summary>
    public static TimeZoneInfo DisplayZone { get; set; } = TimeZoneInfo.Utc;

    public async Task<CheckInRecord> AddCheckInAsync(CitizenInfo citizen, DateTime checkedInAt)
    {
        var fields = SaveFields;
        var ccdd = fields.Contains("CccdNumber") ? citizen.CccdNumber : "";
        var oldId = fields.Contains("OldIdNumber") ? citizen.OldIdNumber : "";
        var name = fields.Contains("FullName") ? citizen.FullName : "";
        var dob = fields.Contains("DateOfBirth") ? citizen.DateOfBirth : default;
        var gender = fields.Contains("Gender") ? citizen.Gender : "";
        var address = fields.Contains("Address") ? citizen.Address : "";
        var issue = fields.Contains("IssueDate") ? citizen.IssueDate : default;
        var raw = fields.Contains("RawPayload") ? citizen.RawPayload : null;

        var sql = """
            INSERT INTO checkins
                (cccd_number, old_id_number, full_name, date_of_birth, gender,
                 address, issue_date, checked_in_at, raw_payload)
            VALUES ($cccd, $old, $name, $dob, $gender, $address, $issue, $at, $raw);
            SELECT last_insert_rowid();
            """;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$cccd", ccdd);
        cmd.Parameters.AddWithValue("$old", oldId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$dob", SqliteFormat.Iso(dob));
        cmd.Parameters.AddWithValue("$gender", gender ?? "");
        cmd.Parameters.AddWithValue("$address", address ?? "");
        cmd.Parameters.AddWithValue("$issue", SqliteFormat.Iso(issue));
        cmd.Parameters.AddWithValue("$at", SqliteFormat.Iso(checkedInAt.ToUniversalTime()));
        cmd.Parameters.AddWithValue("$raw", (object?)raw ?? DBNull.Value);

        var id = (long)(await cmd.ExecuteScalarAsync())!;
        // Lưu UTC → trả UTC nhất quán; UI hiển thị local theo TimeZoneInfo của app.
        return new CheckInRecord
        {
            Id = id,
            CccdNumber = ccdd,
            OldIdNumber = oldId,
            FullName = name,
            DateOfBirth = dob,
            Gender = gender ?? "",
            Address = address ?? "",
            IssueDate = issue,
            CheckedInAt = checkedInAt.ToUniversalTime(),
            RawPayload = raw,
        };
    }

    public async Task LogRejectedAsync(string payloadSanitized, string reason, DateTime scannedAt)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO rejected (scanned_at, reason, payload) VALUES ($at, $reason, $payload);";
            cmd.Parameters.AddWithValue("$at", SqliteFormat.Iso(scannedAt.ToUniversalTime()));
            cmd.Parameters.AddWithValue("$reason", (object?)reason ?? "");
            cmd.Parameters.AddWithValue("$payload", (object?)payloadSanitized ?? "");
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Log phụ — hỏng không chặn app (reader vẫn tiếp tục lắng nghe).
        }
    }

    public async Task<IReadOnlyList<CheckInRecord>> GetRecentAsync(int count)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, cccd_number, old_id_number, full_name, date_of_birth,
                   gender, address, issue_date, checked_in_at, raw_payload
            FROM checkins
            ORDER BY checked_in_at DESC, id DESC
            LIMIT $count;
            """;
        cmd.Parameters.AddWithValue("$count", Math.Max(0, count));

        var list = new List<CheckInRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(ReadRecord(reader));
        return list;
    }

    public async Task<int> GetCountAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM checkins;";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> GetCountAsync(DateTime from)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM checkins WHERE checked_in_at >= $from;";
        cmd.Parameters.AddWithValue("$from", SqliteFormat.Iso(from.ToUniversalTime()));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<CheckInRecord>> GetWhereAtLeastAsync(DateTime from)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, cccd_number, old_id_number, full_name, date_of_birth,
                   gender, address, issue_date, checked_in_at, raw_payload
            FROM checkins
            WHERE checked_in_at >= $from
            ORDER BY checked_in_at DESC, id DESC;
            """;
        cmd.Parameters.AddWithValue("$from", SqliteFormat.Iso(from.ToUniversalTime()));

        var list = new List<CheckInRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(ReadRecord(reader));
        return list;
    }

    private static CheckInRecord ReadRecord(SqliteDataReader reader)
    {
        DateTime ParseDate(string column) =>
            SqliteFormat.FromIso(reader.GetString(reader.GetOrdinal(column)));

        var checkedIn = ParseDate("checked_in_at");
        return new CheckInRecord
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            CccdNumber = reader.GetString(reader.GetOrdinal("cccd_number")),
            OldIdNumber = reader.GetString(reader.GetOrdinal("old_id_number")),
            FullName = reader.GetString(reader.GetOrdinal("full_name")),
            DateOfBirth = ParseDate("date_of_birth"),
            Gender = reader.GetString(reader.GetOrdinal("gender")),
            Address = reader.GetString(reader.GetOrdinal("address")),
            IssueDate = ParseDate("issue_date"),
            CheckedInAt = checkedIn,
            RawPayload = reader.IsDBNull(reader.GetOrdinal("raw_payload"))
                ? null : reader.GetString(reader.GetOrdinal("raw_payload")),
        };
    }

    // Không giữ kết nối dài hạn — SQLite mở/đóng per-call, nên Dispose rỗng (giữ IDisposable để
    // composition root gọi; không có tài nguyên nào cần giải phóng).
    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}