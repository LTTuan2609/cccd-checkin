namespace CccdCheckIn.Storage.Sqlite;

using System.Data;
using Microsoft.Data.Sqlite;

/// <summary>
/// Tự tạo/đồng bộ schema SQLite (idempotent — chạy nhiều lần không lỗi, không tạo trùng).
/// CREATE TABLE IF NOT EXISTS → lần đầu tự tạo, lần sau bỏ qua.
/// </summary>
public static class CheckInSchema
{
    /// <summary>Mở (hoặc tạo) DB, tạo thư mục cha nếu chưa có, đảm bảo schema tồn tại.</summary>
    public static void EnsureCreated(string connectionString)
    {
        var cs = new SqliteConnectionStringBuilder(connectionString) { Pooling = false };
        if (!string.IsNullOrEmpty(cs.DataSource))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(cs.DataSource));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        connectionString = cs.ToString();

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS checkins (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                cccd_number  TEXT    NOT NULL,
                old_id_number TEXT   NOT NULL DEFAULT '',
                full_name    TEXT    NOT NULL,
                date_of_birth TEXT   NOT NULL,
                gender       TEXT    NOT NULL,
                address      TEXT    NOT NULL,
                issue_date   TEXT    NOT NULL,
                checked_in_at TEXT   NOT NULL,
                raw_payload  TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS ix_checkins_checked_in_at ON checkins(checked_in_at);
            CREATE INDEX IF NOT EXISTS ix_checkins_cccd_number ON checkins(cccd_number);

            CREATE TABLE IF NOT EXISTS rejected (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                scanned_at  TEXT NOT NULL,
                reason      TEXT NOT NULL,
                payload     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_rejected_scanned_at ON rejected(scanned_at);
            """;
        cmd.ExecuteNonQuery();
    }
}

/// <summary>Kích thước chuỗi ISO cho mốc thời gian (UTC "O"), round-trip chính xác giữa máy/DB.</summary>
public static class SqliteFormat
{
    /// <summary>Round-trip "O" (Kind=Utc): "2026-08-19T09:15:30.0000000Z".</summary>
    public static string Iso(this DateTime utc) => utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    public static DateTime FromIso(string s) => DateTime.Parse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);

    /// <summary>Số giờ offset (mặc định 0 = UTC; để máy khác timezone vẫn nhất quán).</summary>
    public static int UtcOffsetHours { get; set; } = 0;
}