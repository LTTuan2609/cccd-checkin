namespace CccdCheckIn.Core;

/// <summary>
/// Chống quét trùng: cùng số CCCD trong khoảng thời gian ngắn (mặc định 5s)
/// thường là máy quét đọc lại cùng một thẻ — bỏ qua lượt sau.
/// Cửa sổ cố định tính từ LẦN QUÉT ĐẦU TIÊN (không gia hạn khi quét trùng) —
/// reset lại mốc chỉ khi hết cửa sổ. Thread-Safe, tự prune entry cũ.
/// </summary>
public sealed class DuplicateScanGuard
{
    private TimeSpan _window;
    private readonly Dictionary<string, DateTime> _lastScan = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public DuplicateScanGuard(TimeSpan window) => _window = window;

    /// <summary>
    /// Kiểm tra CCCD có phải quét trùng trong cửa sổ không.
    /// </summary>
    /// <returns>true nếu BỊ trùng (nên bỏ qua); false nếu là lượt mới hợp lệ.</returns>
    public bool IsDuplicate(string cccdNumber)
    {
        if (string.IsNullOrEmpty(cccdNumber)) return false;

        var now = DateTime.UtcNow;
        lock (_lock)
        {
            if (_lastScan.TryGetValue(cccdNumber, out var last)
                && now - last < _window)
            {
                return true;   // cửa sổ cố định từ lần đầu — không gia hạn
            }

            _lastScan[cccdNumber] = now;
            PruneLocked(now);
            return false;
        }
    }

    private void PruneLocked(DateTime now)
    {
        if (_lastScan.Count < 256) return;
        var cutoff = now - _window;
        foreach (var kv in _lastScan)
            if (kv.Value < cutoff)
                _lastScan.Remove(kv.Key);
    }

    public void Clear()
    {
        lock (_lock) { _lastScan.Clear(); }
    }

    /// <summary>Đổi cửa sổ chống trùng lúc chạy (khách đổi cài đặt — không cần khởi động lại).</summary>
    public void SetWindow(TimeSpan window)
    {
        lock (_lock) { _window = window; }
    }
}