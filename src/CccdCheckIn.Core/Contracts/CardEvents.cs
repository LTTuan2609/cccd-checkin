namespace CccdCheckIn.Core.Contracts;

/// <summary>Trạng thái máy đọc thẻ.</summary>
public enum ReaderState
{
    Disconnected,
    Connecting,
    Listening,
    Reconnecting,
    Error
}

/// <summary>Sự kiện một thẻ/quét được (payload thô chưa parse).</summary>
public sealed class CardReadEventArgs(string payload) : EventArgs
{
    public string Payload { get; } = payload;
}

/// <summary>Sự kiện thay đổi trạng thái máy đọc.</summary>
public sealed class ReaderStateChangedEventArgs(ReaderState state, string? message = null) : EventArgs
{
    public ReaderState State { get; } = state;
    public string? Message { get; } = message;
}