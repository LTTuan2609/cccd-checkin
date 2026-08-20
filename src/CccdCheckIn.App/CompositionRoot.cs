using CccdCheckIn.App.Config;
using CccdCheckIn.App.Services;
using CccdCheckIn.Core;
using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Models;
using CccdCheckIn.Core.Parsing;
using CccdCheckIn.Reader.Serial;
using CccdCheckIn.Storage.Sqlite;

namespace CccdCheckIn.App;

/// <summary>
/// Composition root — nơi duy nhất "nối dây" reader/store/parser theo AppConfig.
/// Đổi cổng, store, parser … đều ở đây; thêm plugin mới = đổi 1 branch switch.
/// Không hardcode reader cụ thể trong Form.
/// </summary>
public sealed class CompositionRoot : IDisposable
{
    private readonly ICccdReader _reader;
    private readonly ICheckInStore _store;
    public AppConfig Config { get; }
    public CheckInPipeline Pipeline { get; }

    private CompositionRoot(AppConfig config, ICccdReader reader, ICheckInStore store,
                            CheckInPipeline pipeline)
    {
        _reader = reader;
        _store = store;
        Config = config;
        Pipeline = pipeline;
    }

    public static CompositionRoot Create(AppConfig config)
    {
        // Reader: theo Reader.Mode (Serial là mặc định; Simulated dành cho demo/test không thẻ).
        var core = CoreConfigFactory.From(config);
        var parser = new CccdQrParser();

        ICccdReader reader = config.Reader.Mode switch
        {
            "Simulated" => new SimulatedCccdReader(),
            _ => new SerialCccdReader(new ReaderPortConfig
            {
                Port = config.Reader.Port,
                BaudRate = config.Reader.BaudRate,
                DtrEnable = config.Reader.DtrEnable,
                AutoDetectPort = config.Reader.AutoDetectPort,
                ScannerVidPids = config.Reader.ScannerVidPids,
                ReconnectIntervalMs = config.Reader.ReconnectIntervalMs,
            }),
        };

        // Store: theo Store.Type — thêm store mới chỉ cần 1 nhánh ở đây (plugin assembly riêng).
        ICheckInStore store = new SqliteCheckInStore(config.Store.SqlitePath, config.Store.SaveFields);

        var guard = new DuplicateScanGuard(core.DuplicateWindow);
        // Store tích hợp sẵn rejected-log (bảng rejected) → truyền luôn cho pipeline.
        var rejectedLog = store as IRejectedLogStore;
        var pipeline = new CheckInPipeline(reader, parser, store, guard, core.LogRejectedScans, rejectedLog);

        return new CompositionRoot(config, reader, store, pipeline);
    }

    /// <summary>Giải phóng dependencies đã nối (reader đóng COM, store đóng dispose).</summary>
    public void Dispose()
    {
        Pipeline.Dispose();          // ngắt sự kiện + Stop reader
        if (_store is IDisposable d) d.Dispose();
    }
}