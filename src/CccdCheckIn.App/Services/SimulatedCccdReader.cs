using System.Diagnostics;
using CccdCheckIn.Core.Contracts;

namespace CccdCheckIn.App.Services;

/// <summary>
/// ICccdReader giả — gửi lần lượt các chuỗi QR mẫu (chạy 1 lần/giây) qua đúng
/// đường ống pipeline để demo/quay video mà không cần máy quét thật.
/// Dùng khi Reader.Mode = "Simulated". Không nối COM.
/// </summary>
public sealed class SimulatedCccdReader : ICccdReader
{
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    private static readonly string[] Samples =
    [
        // 1 chuẩn 7 trường + 1 Zalo 6 trường (ngày có dấu /) + 1 chuỗi rác để thấy cảnh báo.
        "094083123451|3099789364|Nguyễn Gia Mạnh|21031983|Nam|165/7, Nguyễn Thị Minh Khai, Phường 9, TP Sóc Trăng|06042021",
        "094083123452|Trần Thị Bích|15/08/1990|Nữ|12 Lê Lợi, Quận 1, TP Hồ Chí Minh|15/08/2016",
        "KHONG_PHAI_CCCD",
    ];

    public event EventHandler<CardReadEventArgs>? CardScanned;
    public event EventHandler<ReaderStateChangedEventArgs>? StateChanged;

    public ReaderState State { get; private set; } = ReaderState.Disconnected;

    public void Start()
    {
        lock (_lock)
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();
        }
        SetState(ReaderState.Connecting, "Simulator đang chạy…");
        _loop = Task.Run(() => SimulateLoop(_cts.Token));
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_lock) { cts = _cts; _cts = null; }
        cts?.Cancel();
        SetState(ReaderState.Disconnected);
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private async Task SimulateLoop(CancellationToken token)
    {
        try
        {
            SetState(ReaderState.Listening, "Simulator (không cần máy quét)");
            foreach (var sample in Samples)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(1), token);
                CardScanned?.Invoke(this, new CardReadEventArgs(sample));
            }
            Debug.WriteLine("Simulator đã phát hết mẫu.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetState(ReaderState.Error, "Simulator lỗi: " + ex.Message);
        }
        finally
        {
            lock (_lock)
            {
                // Loop kết thúc vì bị Stop() → trạng thái Disconnected đã được đặt trong Stop();
                // kết thúc tự nhiên (đã phát hết mẫu) → trả về Disconnected để không treo "đang nghe".
                if (_cts is null) SetState(ReaderState.Disconnected);
            }
        }
    }

    private void SetState(ReaderState state, string? message = null)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, new ReaderStateChangedEventArgs(state, message));
    }
}