using CccdCheckIn.Core.Models;
using CccdCheckIn.Storage.Sqlite;
using Xunit;

namespace CccdCheckIn.Tests;

public class SqliteCheckInStoreTests : IDisposable
{
    private readonly string _path;
    private readonly string _cs;

    public SqliteCheckInStoreTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cccd-tests-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(dir, "test.db");
        _cs = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _path }.ToString();
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_path);
        if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static CitizenInfo Sample(string cccd = "094083123451") => new()
    {
        CccdNumber = cccd,
        OldIdNumber = "3099789364",
        FullName = "Nguyễn Gia Mạnh",
        DateOfBirth = new DateTime(1983, 3, 21),
        Gender = "Nam",
        Address = "165/7, Nguyễn Thị Minh Khai, Phường 9, TP Sóc Trăng",
        IssueDate = new DateTime(2021, 4, 6),
        RawPayload = "094083123451|3099789364|Nguyễn Gia Mạnh|21031983|Nam|165/7|06042021",
    };

    [Fact]
    public void Add_ThenGetRecent_ReturnsRecordWithAllDefaultFields()
    {
        using var store = new SqliteCheckInStore(_path);
        var t = new DateTime(2026, 8, 19, 9, 30, 0);
        var rec = store.AddCheckInAsync(Sample(), t).GetAwaiter().GetResult();

        Assert.True(rec.Id > 0);
        Assert.Equal(t.ToUniversalTime(), rec.CheckedInAt);
        Assert.Equal("094083123451", rec.CccdNumber);
        Assert.Equal("Nguyễn Gia Mạnh", rec.FullName);
        Assert.Equal("Nam", rec.Gender);
        Assert.Contains("Sóc Trăng", rec.Address);

        var recent = store.GetRecentAsync(10).GetAwaiter().GetResult();
        var one = Assert.Single(recent);
        Assert.Equal(rec.Id, one.Id);
        Assert.Equal(t.ToUniversalTime(), one.CheckedInAt);
        // Default SaveFields không lưu OldIdNumber/RawPayload
        Assert.Equal("", one.OldIdNumber);
        Assert.Null(one.RawPayload);
    }

    [Fact]
    public void AddLast_GivenCustomSaveFields_RoundTrips()
    {
        string[] fields = ["CccdNumber", "OldIdNumber", "FullName", "CheckInRecordEverything"];
        using var store = new SqliteCheckInStore(_path, SqliteCheckInStore.DefaultSaveFields.Concat(["OldIdNumber", "RawPayload"]).ToList());
        var t = new DateTime(2026, 8, 19, 9, 30, 0);
        var rec = store.AddCheckInAsync(Sample(), t).GetAwaiter().GetResult();
        var recent = store.GetRecentAsync(1).GetAwaiter().GetResult();
        var one = recent[0];
        Assert.Equal("3099789364", one.OldIdNumber);
        Assert.Equal(Sample().RawPayload, one.RawPayload);
    }

    [Fact]
    public void AddLast_Counts_TodayAndAll()
    {
        using var store = new SqliteCheckInStore(_path);
        var t0 = new DateTime(2026, 8, 19, 9, 0, 0);
        var t1 = new DateTime(2026, 8, 19, 10, 0, 0);
        var tOld = new DateTime(2026, 8, 18, 9, 0, 0);

        store.AddCheckInAsync(Sample("094083123451"), t0).GetAwaiter().GetResult();
        store.AddCheckInAsync(Sample("094083123452"), t1).GetAwaiter().GetResult();
        store.AddCheckInAsync(Sample("094083123453"), tOld).GetAwaiter().GetResult();

        Assert.Equal(3, store.GetCountAsync().GetAwaiter().GetResult());
        Assert.Equal(2, store.GetCountAsync(new DateTime(2026, 8, 19)).GetAwaiter().GetResult());
    }

    [Fact]
    public void GetRecent_OrdersByLatest_WithLimit()
    {
        using var store = new SqliteCheckInStore(_path);
        for (var i = 0; i < 5; i++)
            store.AddCheckInAsync(Sample($"09408312345{i}"),
                new DateTime(2026, 8, 19, 8, i, 0)).GetAwaiter().GetResult();

        var recent = store.GetRecentAsync(3).GetAwaiter().GetResult();
        Assert.Equal(3, recent.Count);
        Assert.Equal("094083123454", recent[0].CccdNumber);   // mới nhất trước
    }

    [Fact]
    public void RejectedLog_LogsSanitizedPayload_AndDoesNotThrow()
    {
        using var store = new SqliteCheckInStore(_path);
        var t = new DateTime(2026, 8, 19, 9, 30, 0);
        store.LogRejectedAsync("KHONG_PHAI_CCCD", "không đủ 7 trường", t).GetAwaiter().GetResult();

        // Đọc lại qua SQL trực tiếp để xác nhận có dòng trong bảng rejected.
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM rejected WHERE reason LIKE 'không%'";
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void EnsureCreated_IsIdempotent_AndCreatesDir()
    {
        CheckInSchema.EnsureCreated(_cs);
        CheckInSchema.EnsureCreated(_cs);   // chạy lần 2 không lỗi
        Assert.True(File.Exists(_path));
    }

    [Fact]
    public void SaveFields_DoesNotAffectUnselected()
    {
        string[] fields = ["CccdNumber"];   // chỉ lưu số CCCD
        using var store = new SqliteCheckInStore(_path, fields);
        var rec = store.AddCheckInAsync(Sample(), DateTime.Now).GetAwaiter().GetResult();
        Assert.Equal("", rec.FullName);
        Assert.Equal("", rec.Gender);

        var recent = store.GetRecentAsync(1).GetAwaiter().GetResult();
        Assert.Equal("", recent[0].FullName);
        Assert.Equal("094083123451", recent[0].CccdNumber);
    }
}