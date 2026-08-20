using CccdCheckIn.Core;
using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Models;
using CccdCheckIn.Core.Parsing;

namespace CccdCheckIn.App.Services;

/// <summary>Một lượt quét đã xử lý xong, sẵn cho UI render.</summary>
public sealed class ScanOutcome
{
    public bool Accepted { get; init; }                 // true = đã lưu 1 lượt check-in
    public bool IsDuplicate { get; init; }              // bỏ qua vì trùng thẻ trong cửa sổ
    public string Message { get; init; } = "";          // để lên UI (đã lọc, không có PII thô khi mask)
    public CitizenInfo? Citizen { get; init; }          // thông tin đã lưu (để hiện "vừa quét")
    public CheckInRecord? Record { get; init; }         // bản ghi đã lưu (để thêm vào grid)
    public List<string> Warnings { get; init; } = [];
}

/// <summary>
/// Nối reader → parser → duplicate-guard → store thành 1 đường ống. UI chỉ gọi
/// Start/Stop và lắng nghe sự kiện; không cần biết reader là Serial hay Simulated.
/// Threading: sự kiện CardScanned đến từ thread của reader → vẫn nối xuống store
/// (thread-safe), rồi nâng ScanOutcome lên UI thread qua sự kiện (MainForm dùng BeginInvoke).
/// Không bao giờ ném ra ngoài — mọi lỗi đều thành ScanOutcome/event.
/// </summary>
public sealed class CheckInPipeline : IDisposable, IAsyncDisposable
{
    private readonly ICccdReader _reader;
    private readonly IQrPayloadParser _parser;
    private readonly ICheckInStore _store;
    private readonly IRejectedLogStore? _rejectedLog;
    private readonly DuplicateScanGuard _guard;
    private readonly bool _logRejectedScans;
    private bool _disposed;

    public CheckInPipeline(ICccdReader reader, IQrPayloadParser parser, ICheckInStore store,
                           DuplicateScanGuard guard, bool logRejectedScans,
                           IRejectedLogStore? rejectedLog = null)
    {
        _reader = reader;
        _parser = parser;
        _store = store;
        _guard = guard;
        _logRejectedScans = logRejectedScans;
        _rejectedLog = rejectedLog;

        _reader.CardScanned += OnCardScanned;
        _reader.StateChanged += OnStateChanged;
    }

    public event EventHandler<ScanOutcome>? ScanCompleted;
    public event EventHandler<ReaderStateChangedEventArgs>? ReaderStateChanged;
    public event EventHandler<Exception>? PipelineError;   // lỗi nền (đã log, UI thông báo mềm)

    public ICheckInStore Store => _store;
    public ICccdReader Reader => _reader;

    public void Start() => _reader.Start();

    public void Stop() => _reader.Stop();

    /// <summary>Cập nhật cửa sổ chống trùng theo cài đặt mới (guard chốt tham số lúc khởi động).</summary>
    public void SetDuplicateWindow(TimeSpan window) => _guard.SetWindow(window);

    private void OnCardScanned(object? sender, CardReadEventArgs e)
    {
        var outcome = HandlePayload(e.Payload);
        ScanCompleted?.Invoke(this, outcome);
    }

    private ScanOutcome HandlePayload(string rawPayload)
    {
        try
        {
            var result = _parser.Parse(rawPayload);
            if (!result.IsValid || result.Citizen is null)
            {
                if (_logRejectedScans)
                    LogRejected(rawPayload, result.ErrorMessage ?? "không hợp lệ");
                return new ScanOutcome { Accepted = false, Message = result.ErrorMessage ?? "QR không hợp lệ." };
            }

            var citizen = result.Citizen;

            // Chống quét trùng: cũng là CCCD trong cửa sổ — bỏ qua, không ghi log.
            if (_guard.IsDuplicate(citizen.CccdNumber))
                return new ScanOutcome
                {
                    Accepted = false,
                    IsDuplicate = true,
                    Message = $"Đã quét thẻ {citizen.CccdNumber[^4..]} trong cửa sổ chống trùng — bỏ qua.",
                };

            var record = _store.AddCheckInAsync(citizen, DateTime.Now).GetAwaiter().GetResult();
            return new ScanOutcome
            {
                Accepted = true,
                Citizen = citizen,
                Record = record,
                Warnings = result.Warnings,
                Message = $"Đã ghi nhận check-in: {citizen.FullName}",
            };
        }
        catch (Exception ex)
        {
            // Store lỗi (DB khóa, ổ đầy, …) không được làm chết reader.
            PipelineError?.Invoke(this, ex);
            if (_logRejectedScans)
                LogRejected(rawPayload, "lỗi lưu: " + ex.Message);   // giữ lại dấu vết thay vì mất scan
            return new ScanOutcome { Accepted = false, Message = "Lỗi khi ghi nhận check-in: " + ex.Message };
        }
    }

    private void OnStateChanged(object? sender, ReaderStateChangedEventArgs e)
        => ReaderStateChanged?.Invoke(this, e);

    private void LogRejected(string rawPayload, string reason)
    {
        // Chỉ ghi payload đã sanitize (rút gọn ≤100 ký tự, không có chuỗi PII hoàn chỉnh) — đủ để debug.
        if (_rejectedLog is not null)
        {
            try
            {
                _rejectedLog.LogRejectedAsync(CccdQrParser.SanitizeForDisplay(rawPayload), reason, DateTime.Now)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // Log phụ — hỏng cũng không chặn (reader vẫn tiếp tục).
            }
        }
        else
        {
            // Không có rejected-store (store plugin khác) → ghi file text fallback.
            try
            {
                var file = Path.Combine(AppContext.BaseDirectory, "Data", "rejected.log");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.AppendAllText(file,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{reason}\t{CccdQrParser.SanitizeForDisplay(rawPayload)}{Environment.NewLine}");
            }
            catch
            {
                // Log phụ — hỏng cũng không chặn.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.CardScanned -= OnCardScanned;
        _reader.StateChanged -= OnStateChanged;
        Stop();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}