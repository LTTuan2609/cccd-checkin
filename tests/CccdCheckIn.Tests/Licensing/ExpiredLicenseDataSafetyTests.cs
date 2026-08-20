using CccdCheckIn.App.Services;
using CccdCheckIn.Core;
using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Licensing;
using CccdCheckIn.Core.Models;
using CccdCheckIn.Core.Parsing;
using CccdCheckIn.Storage.Sqlite;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

/// <summary>
/// INVARIANT QUAN TRỌNG NHẤT (chạy CI):
/// license hết hạn KHÔNG được: xóa/khoá đọc/khoá export/thay đổi dữ liệu khách hàng.
/// Chỉ chặn ghi check-in mới; mọi dữ liệu cũ vẫn đọc + xuất được nguyên vẹn.
/// </summary>
public class ExpiredLicenseDataSafetyTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "cccd-tests-" + Guid.NewGuid().ToString("N"));

    private readonly string _dbPath;

    private readonly SqliteCheckInStore _store;

    private readonly ManualCccdReader _reader;

    private readonly CheckInPipeline _pipeline;

    private readonly LicenseGate _gate;

    private const int CheckInCount = 50;

    public ExpiredLicenseDataSafetyTests()
    {
        _dbPath = Path.Combine(_dir, "checkin.db");
        _store = new SqliteCheckInStore(_dbPath);

        // License đã hết hạn (expiresAt quá khứ) → gate ở chế độ chỉ đọc/xuất.
        var expiredResult = new LicenseValidationResult
        {
            Status = LicenseStatus.Expired,
            Mode = LicenseMode.ReadExportOnly,
            Message = "Gói thuê bao đã hết hạn.",
        };
        _gate = new LicenseGate(expiredResult);

        _reader = new ManualCccdReader();
        _pipeline = new CheckInPipeline(
            reader: _reader,
            parser: new CccdQrParser(),
            store: _store,
            guard: new DuplicateScanGuard(TimeSpan.FromSeconds(5)),
            logRejectedScans: true,
            rejectedLog: _store,
            licenseGate: _gate);

        SeedData();
    }

    public void Dispose()
    {
        _pipeline.Dispose();
        _store.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private void SeedData()
    {
        for (var i = 0; i < CheckInCount; i++)
        {
            var cccd = $"0940831{i:D3}000";
            var citizen = new CitizenInfo
            {
                CccdNumber = cccd,
                FullName = "NV " + i,
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = "Nam",
                Address = "Địa chỉ " + i,
                IssueDate = new DateTime(2021, 1, 1),
            };
            _store.AddCheckInAsync(citizen, new DateTime(2026, 8, 19, 8, i % 24, 0)).GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void Expired_ReadStillWorks_AllDataIntact()
    {
        var count = _store.GetCountAsync().GetAwaiter().GetResult();
        var recent = _store.GetRecentAsync(10).GetAwaiter().GetResult();

        Assert.Equal(CheckInCount, count);
        Assert.True(recent.Count == Math.Min(10, CheckInCount));
        Assert.All(recent, r => Assert.StartsWith("NV ", r.FullName));
    }

    [Fact]
    public void Expired_ExportStillWorks()
    {
        var records = _store.GetRecentAsync(int.MaxValue).GetAwaiter().GetResult();
        var exporter = new CheckInCsvExporter(CheckInCsvExporter.DefaultFields);
        var csvPath = Path.Combine(_dir, "export.csv");

        var wrote = exporter.ExportAsync(csvPath, records).GetAwaiter().GetResult();

        Assert.NotNull(wrote);
        Assert.True(File.Exists(csvPath));
        var text = File.ReadAllText(csvPath);
        var lines = text.TrimEnd('\r', '\n')
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        Assert.Equal(CheckInCount + 1, lines.Length);   // header + data
        Assert.Contains("FullName", lines[0]);
        // Mọi record vẫn xuất đủ — kiểm tra số dòng + vài dữ liệu có mặt (không phụ thuộc thứ tự).
        Assert.Contains("NV ", text);
        Assert.Equal(CheckInCount, lines.Skip(1).Count());
    }

    [Fact]
    public void Expired_CheckInBlocked_DataCountUnchanged()
    {
        var before = _store.GetCountAsync().GetAwaiter().GetResult();

        // Quét 1 thẻ hợp lệ nhưng license hết hạn → rejected, không ghi.
        var citizen = new CitizenInfo
        {
            CccdNumber = "099999999999",
            FullName = "Người mới",
            DateOfBirth = new DateTime(2000, 1, 1),
            Gender = "Nữ",
            Address = "Hà Nội",
            IssueDate = new DateTime(2023, 1, 1),
        };
        var payload = "099999999999|1000|Người mới|01012000|Nữ|Hà Nội|01012023";
        _reader.Emit(payload);

        var count = _store.GetCountAsync().GetAwaiter().GetResult();
        Assert.Equal(before, count);       // không ghi thêm
        Assert.Equal(CheckInCount, count); // dữ liệu nguyên vẹn
    }

    [Fact]
    public void Expired_PipelineRejectsScanOutcome_WithLicenseMessage()
    {
        var payload = "099999999999|1000|Người mới|01012000|Nữ|Hà Nội|01012023";

        ScanOutcome? captured = null;
        _pipeline.ScanCompleted += (_, outcome) => captured = outcome;
        _reader.Emit(payload);

        Assert.NotNull(captured);
        Assert.False(captured!.Accepted);
        Assert.Contains("hết hạn", captured.Message);
    }

    private int RecordsCount => CheckInCount;

    /// <summary>ICccdReader fake: emit payload theo ý muốn (không Start/Stop, không thread).</summary>
    private sealed class ManualCccdReader : ICccdReader, IDisposable
    {
        public event EventHandler<CardReadEventArgs>? CardScanned;
        public event EventHandler<ReaderStateChangedEventArgs>? StateChanged;

        public ReaderState State { get; private set; } = ReaderState.Disconnected;

        public void Start() { }

        public void Stop() { }

        public void Emit(string payload)
            => CardScanned?.Invoke(this, new CardReadEventArgs(payload));

        public void Dispose() => Stop();

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}