using System.Text;
using CccdCheckIn.Core.Models;
using CccdCheckIn.Storage.Sqlite;
using Xunit;

namespace CccdCheckIn.Tests;

public class CheckInCsvExporterTests : IDisposable
{
    private readonly string _dir;

    public CheckInCsvExporterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cccd-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static CheckInRecord Record(int id = 1, string cccd = "094083123451", string name = "Nguyễn Gia Mạnh") => new()
    {
        Id = id,
        CccdNumber = cccd,
        OldIdNumber = "3099789364",
        FullName = name,
        DateOfBirth = new DateTime(1983, 3, 21),
        Gender = "Nam",
        Address = "165/7, Nguyễn Thị Minh Khai",
        IssueDate = new DateTime(2021, 4, 6),
        CheckedInAt = new DateTime(2026, 8, 19, 9, 30, 0, DateTimeKind.Utc),   // exporter renders local
        RawPayload = "x|y",
    };

    [Fact]
    public void Export_WritesBomUtf8_HeadersEnglish_Values()
    {
        var path = Path.Combine(_dir, "a.csv");
        var exporter = new CheckInCsvExporter(["CheckedInAt", "CccdNumber", "FullName", "Address"]);
        exporter.ExportAsync(path, [Record()]).GetAwaiter().GetResult();

        var bytes = File.ReadAllBytes(path);
        // BOM EF BB BF
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));

        var text = File.ReadAllText(path, Encoding.UTF8);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("CheckedInAt,CccdNumber,FullName,Address", lines[0]);
        // Record.CheckedInAt is UTC; exporter renders local wall-clock.
        Assert.Contains(Record().CheckedInAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), lines[1]);
        Assert.Contains("094083123451", lines[1]);
        Assert.Contains("Nguyễn Gia Mạnh", lines[1]);   // tiếng Việt đúng dấu
    }

    [Fact]
    public void Export_QuotesFieldWithComma_CsvEscaped()
    {
        var path = Path.Combine(_dir, "b.csv");
        var r = Record();
        var exporter = new CheckInCsvExporter(["FullName", "Address"]);
        exporter.ExportAsync(path, [r]).GetAwaiter().GetResult();

        var text = File.ReadAllText(path, Encoding.UTF8);
        var line = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[1];
        // FullName không chứa phẩy/dấu nháy → không cần escape, giữ nguyên.
        Assert.Contains("Nguyễn Gia Mạnh", line);
        // Address chứa phẩy → được wrap trong dấu nháy kép.
        Assert.Contains("\"165/7, Nguyễn Thị Minh Khai\"", line);
    }

    [Fact]
    public void Export_UnknownField_SkippedNotCrash()
    {
        var path = Path.Combine(_dir, "c.csv");
        var exporter = new CheckInCsvExporter(["CheckedInAt", "KhôngTồnTạiX"]);
        exporter.ExportAsync(path, [Record()]).GetAwaiter().GetResult();
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("CheckedInAt", text);
    }

    [Fact]
    public void Export_EmptyFields_DefaultsToCheckedInAtCccdFullNameAddress()
    {
        var path = Path.Combine(_dir, "d.csv");
        var exporter = new CheckInCsvExporter([]);
        exporter.ExportAsync(path, [Record()]).GetAwaiter().GetResult();
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert.Contains("CheckedInAt,CccdNumber,FullName,Address", text);
    }
}