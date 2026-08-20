namespace CccdCheckIn.Core.Contracts;

using CccdCheckIn.Core.Models;

/// <summary>
/// Nơi ghi các lượt quét bị từ chối (QR không parse được) để debug.
/// Store chính nếu muốn ghi chung vào DB chỉ cần implement thêm interface này.
/// Payload lưu đã được sanitize (không chứa chuỗi PII hoàn chỉnh) — đủ để debug.
/// </summary>
public interface IRejectedLogStore
{
    /// <summary>Ghi 1 lượt quét bị từ chối. Không ném — lỗi ghi log không chặn reader.</summary>
    Task LogRejectedAsync(string payloadSanitized, string reason, DateTime scannedAt);
}