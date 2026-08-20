namespace CccdCheckIn.Reader.Serial;

using System.IO.Ports;
using System.Runtime.Versioning;
using CccdCheckIn.Core.Contracts;

/// <summary>
/// ICccdReader cho máy quét CCCD passive kết nối qua cổng COM (USB CDC/serial).
/// - Chọn cổng: cấu hình sẵn → autodetect theo VID/PID → lỗi rõ ràng.
/// - Đọc byte trong DataReceived → ScanBuffer gộp thành dòng + decode UTF-8 (fallback 1252).
/// - Mất cổng (rút USB) → tự reconnect bằng timer sau ReconnectIntervalMs.
/// - Mọi trạng thái phát qua StateChanged; không bao giờ ném ra ngoài vô cớ.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SerialCccdReader : ICccdReader
{
    private readonly ReaderPortConfig _config;
    private readonly ComPortAutoDetector _detector;
    private readonly ScanBuffer _scanBuffer = new();
    private readonly object _lock = new();

    private SerialPort? _port;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private int _connectRunning;

    public SerialCccdReader(ReaderPortConfig? config = null, ComPortAutoDetector? detector = null)
    {
        _config = config ?? new ReaderPortConfig();
        _detector = detector ?? new ComPortAutoDetector();
    }

    public event EventHandler<CardReadEventArgs>? CardScanned;
    public event EventHandler<ReaderStateChangedEventArgs>? StateChanged;

    public ReaderState State { get; private set; } = ReaderState.Disconnected;

    public void Start() => ThreadPool.QueueUserWorkItem(_ => ConnectAsyncSafe());

    public void Stop()
    {
        CancelReconnect();
        SerialPort? port;
        lock (_lock) { port = _port; _port = null; }
        if (port is not null)
        {
            try { port.DataReceived -= OnDataReceived; } catch { }
            TryClosePort(port);
        }
        SetState(ReaderState.Disconnected);
    }

    /// <summary>Chọn cổng ở chế độ admin (SettingsForm) — ghi đè config, reconnect ngay.</summary>
    public void OverridePort(string portName)
    {
        _config.Port = portName;
        CancelReconnect();
        SerialPort? old;
        lock (_lock) { old = _port; _port = null; }
        if (old is not null) TryClosePort(old);
        ConnectAsyncSafe();
    }

    /// <summary>Áp cấu hình reader mới (Cài đặt → Lưu) — có hiệu lực ở lần Stop/Start kế tiếp.</summary>
    public void ApplyConfig(ReaderPortConfig config)
    {
        _config.Port = config.Port;
        _config.BaudRate = config.BaudRate;
        _config.DtrEnable = config.DtrEnable;
        _config.AutoDetectPort = config.AutoDetectPort;
        _config.ScannerVidPids = config.ScannerVidPids;
        _config.ReconnectIntervalMs = config.ReconnectIntervalMs;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }

    private void ConnectAsyncSafe()
    {
        // Chống double-connect (Start được gọi nhiều lần, ví dụ Save → Start khi chưa có lần trước xong).
        if (Interlocked.CompareExchange(ref _connectRunning, 1, 0) != 0) return;
        try
        {
            ConnectCore();
        }
        catch (Exception ex)
        {
            // Bất ngờ ngoài dự kiến không được giết app — log, báo lỗi, tự kết nối lại.
            DiagnosticLog.Write("connect FAIL: " + ex);
            SetState(ReaderState.Error, "Lỗi kết nối máy quét: " + ex.Message);
            ScheduleReconnect();
        }
        finally
        {
            Interlocked.Exchange(ref _connectRunning, 0);
        }
    }

    private void ConnectCore()
    {
        var portName = ResolvePortName();
        DiagnosticLog.Write($"connect: port={portName ?? "(không có)"} baud={_config.BaudRate}");
        if (portName is null)
        {
            if (!_config.AutoDetectPort && string.IsNullOrWhiteSpace(_config.Port))
            {
                // Tạm dừng (không dò, không chỉ định) → đứng yên, không nối lại.
                SetState(ReaderState.Disconnected, "Đã tạm dừng — không kết nối máy quét.");
                return;
            }
            SetState(ReaderState.Error, "Không tìm thấy máy quét (chưa có cổng COM khớp VID/PID). Mở Cài đặt → chọn tay.");
            ScheduleReconnect();
            return;
        }

        // Không mở cổng mới nếu đã có cổng đang mở (tránh mở cùng COM → khóa cổng).
        lock (_lock)
        {
            if (_port is not null && _port.IsOpen)
            {
                DiagnosticLog.Write("connect: đã có cổng mở, bỏ qua");
                SetState(ReaderState.Listening, $"Đang lắng nghe {_port.PortName} @ {_config.BaudRate}");
                return;
            }
        }

        var sp = new SerialPort(portName, _config.BaudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = _config.DtrEnable,
            ReadTimeout = 300,
        };
        sp.DataReceived += OnDataReceived;

        try
        {
            sp.Open();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            DiagnosticLog.Write($"open FAIL {portName}: {ex.Message}");
            SetState(ReaderState.Error, $"Không mở được {portName}: {ex.Message}");
            TryClosePort(sp);
            ScheduleReconnect();
            return;
        }

        SerialPort? old;
        lock (_lock) { old = _port; _port = sp; }
        if (old is not null) TryClosePort(old);
        DiagnosticLog.Write($"open OK {portName} @ {_config.BaudRate}");
        SetState(ReaderState.Listening, $"Đang lắng nghe {portName} @ {_config.BaudRate}");
    }

    private string? ResolvePortName()
    {
        if (!string.IsNullOrWhiteSpace(_config.Port)) return _config.Port.Trim();
        if (_config.AutoDetectPort)
        {
            var ports = _detector.DetectPorts(_config.ScannerVidPids);
            // Nhiều port khớp → lấy port đầu; SettingsForm cho cả danh sách để chọn tay.
            return ports.FirstOrDefault();
        }
        // Không chỉ định cổng và không tự dò → tạm dừng: không kết nối, không schedule.
        return null;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var port = (SerialPort)sender;
        try
        {
            while (port.BytesToRead > 0)
            {
                var buffer = new byte[Math.Min(port.BytesToRead, 1024)];
                var read = port.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                var tail = read > 20 ? 20 : read;
                var hex = Convert.ToHexString(buffer.AsSpan(read - tail, tail));
                DiagnosticLog.Write($"rx {read} byte | {hex}");

                var lines = _scanBuffer.Append(buffer.AsSpan(0, read));
                foreach (var line in lines) RaiseScan(line);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            HandlePortLost(port);
        }
    }

    private void RaiseScan(string line)
    {
        var trimmed = line.Trim(); // dòng đã bỏ BOM ở ScanBuffer; Trim cho chắc
        if (trimmed.Length == 0) return;
        DiagnosticLog.Write($"phát 1 dòng ({trimmed.Length} ký tự)");
        CardScanned?.Invoke(this, new CardReadEventArgs(trimmed));
    }

    private void HandlePortLost(SerialPort port)
    {
        DiagnosticLog.Write("port lost — chuyển sang tự kết nối lại");
        SerialPort? current;
        lock (_lock) { current = _port; if (ReferenceEquals(current, port)) _port = null; }
        TryClosePort(port);
        SetState(ReaderState.Reconnecting, "Cổng COM bị ngắt — chờ tự kết nối lại…");
        ScheduleReconnect();
    }

    private void ScheduleReconnect()
    {
        lock (_lock)
        {
            if (_reconnectCts is not null) return;
            _reconnectCts = new CancellationTokenSource();
        }
        var cts = _reconnectCts;
        _reconnectTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_config.ReconnectIntervalMs, cts.Token);
                lock (_lock)
                {
                    if (ReferenceEquals(_reconnectCts, cts)) _reconnectCts = null;
                    else return;                // đã có reconnect mới hơn — bỏ qua
                }
                if (!cts.IsCancellationRequested) ConnectAsyncSafe();
            }
            catch (OperationCanceledException) { }
        });
    }

    private void CancelReconnect()
    {
        CancellationTokenSource? cts;
        lock (_lock) { cts = _reconnectCts; _reconnectCts = null; }
        if (cts is not null)
        {
            try { cts.Cancel(); } catch { }
        }
    }

    private void SetState(ReaderState state, string? message = null)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, new ReaderStateChangedEventArgs(state, message));
    }

    private static bool TryClosePort(SerialPort port)
    {
        try
        {
            if (port.IsOpen) port.Close();
            port.Dispose();
            return true;
        }
        catch
        {
            try { port.Dispose(); } catch { }
            return false;
        }
    }
}