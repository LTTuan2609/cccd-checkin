namespace CccdCheckIn.Core.Contracts;

using CccdCheckIn.Core.Models;

/// <summary>
/// Máy đọc thẻ. Implement mới (chip/NFC, đồng bộ mạng, …) chỉ cần một class
/// mới implement interface này — không sửa Core.
/// </summary>
public interface ICccdReader : IAsyncDisposable
{
    /// <summary>Payload thô (chuỗi QR) khi scan thành công.</summary>
    event EventHandler<CardReadEventArgs>? CardScanned;

    /// <summary>Thay đổi trạng thái (kết nối/ngắt/reconnect).</summary>
    event EventHandler<ReaderStateChangedEventArgs>? StateChanged;

    ReaderState State { get; }

    void Start();
    void Stop();
}