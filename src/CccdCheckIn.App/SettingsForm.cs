using CccdCheckIn.App.Config;
using CccdCheckIn.App.Services;
using CccdCheckIn.Reader.Serial;
using CccdCheckIn.Storage.Sqlite;

namespace CccdCheckIn.App;

/// <summary>
/// Cửa sổ cài đặt thân thiện người dùng — nhóm theo tab, nhãn tiếng Việt,
/// chỉ để lộ các mục khách thật sự cần. Trường kỹ thuật (Mode, ScannerVidPids…)
/// giữ mặc định và ẩn. Sau khi Lưu: ghi appsettings.json + áp dụng ngay những gì áp dụng được.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly CompositionRoot _root;
    private readonly CheckInPipeline _pipeline;
    private readonly AppConfig _config;
    private readonly string _jsonPath;

    // Máy quét
    private readonly RadioButton _rbAutoCom = new() { Text = "Tự dò máy quét (khuyên dùng)", AutoSize = true };
    private readonly RadioButton _rbManualCom = new() { Text = "Chọn cổng cụ thể:", AutoSize = true };
    private readonly RadioButton _rbDisableCom = new() { Text = "Không kết nối (tạm dừng)", AutoSize = true };
    private readonly ComboBox _cmbPort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly List<string> _portNames = new();   // tên cổng thô, cùng thứ tự với item của _cmbPort
    private readonly NumericUpDown _numBaud = new() { Width = 120, Minimum = 300, Maximum = 115200, Increment = 600 };
    private readonly CheckBox _chkReconnect = new() { Text = "Tự kết nối lại khi mất máy quét", AutoSize = true };

    // Quét
    private readonly NumericUpDown _numDupSeconds = new() { Width = 80, Minimum = 0, Maximum = 3600, Increment = 1 };

    // Hiển thị
    private readonly CheckBox _chkMask = new() { Text = "Che số CCCD (chỉ hiện 4 số cuối)", AutoSize = true };
    private readonly NumericUpDown _numRecentRows = new() { Width = 80, Minimum = 5, Maximum = 500, Increment = 1 };
    private readonly ComboBox _cmbStatusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };

    // Xuất CSV
    private readonly TextBox _txtCsvPath = new() { Width = 200 };
    private readonly CheckedListBox _chkExportFields = new() { Width = 220, Height = 120, CheckOnClick = true };
    private static readonly string[] _availableExportFields =
        ["CheckedInAt", "CccdNumber", "FullName", "Address", "Gender", "DateOfBirth", "IssueDate", "OldIdNumber", "RawPayload"];

    public SettingsForm(CompositionRoot root, CheckInPipeline pipeline)
    {
        _root = root;
        _pipeline = pipeline;
        _config = Clone(root.Config);
        _jsonPath = Config.AppConfigFactory.AppSettingsPath(AppContext.BaseDirectory);

        Text = "Cài đặt — CCCD Check-in";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 520);
        Font = new Font("Segoe UI", 9.5f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildReaderTab());
        tabs.TabPages.Add(BuildScanTab());
        tabs.TabPages.Add(BuildDisplayTab());
        tabs.TabPages.Add(BuildExportTab());

        var btnSave = new Button { Text = "Lưu", AutoSize = true, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(10, 8, 10, 6),
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
        };
        bottom.Controls.Add(btnCancel);
        bottom.Controls.Add(btnSave);

        Controls.Add(tabs);
        Controls.Add(bottom);

        btnSave.Click += (_, _) => SaveAndApply();
    }

    private TabPage BuildReaderTab()
    {
        // Set radio TRƯỚC khi bind để _cmbPort.Enabled đúng ngay.
        _rbAutoCom.Checked = string.IsNullOrWhiteSpace(_config.Reader.Port);
        _rbManualCom.Checked = !_rbAutoCom.Checked && _config.Reader.Port.Length > 0;
        _rbDisableCom.Checked = !_rbAutoCom.Checked && !_rbManualCom.Checked;
        _cmbPort.Enabled = _rbManualCom.Checked;

        LoadPortList(_cmbPort, _config.Reader.Port);

        _numBaud.Value = Clamp(_config.Reader.BaudRate, 300, 115200);
        _chkReconnect.Checked = _config.Reader.ReconnectIntervalMs > 0;

        _rbAutoCom.CheckedChanged += (_, _) => _cmbPort.Enabled = _rbManualCom.Checked;
        _rbManualCom.CheckedChanged += (_, _) => _cmbPort.Enabled = _rbManualCom.Checked;

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.Controls.Add(LabelOf("Kết nối máy quét"), 0, 0);
        p.Controls.Add(Flow(_rbAutoCom), 1, 0);
        p.Controls.Add(LabelOf("Cổng COM"), 0, 1);
        p.Controls.Add(Flow(_rbManualCom, _cmbPort), 1, 1);
        p.Controls.Add(LabelOf("Tạm dừng"), 0, 2);
        p.Controls.Add(_rbDisableCom, 1, 2);
        p.Controls.Add(LabelOf("Tốc độ (baud)"), 0, 3);
        p.Controls.Add(_numBaud, 1, 3);
        p.Controls.Add(LabelOf("Tự động nối lại"), 0, 4);
        p.Controls.Add(_chkReconnect, 1, 4);
        p.Controls.Add(LabelOf(""), 0, 5);

        var tab = new TabPage("Máy quét") { Controls = { p } };
        return tab;
    }

    private TabPage BuildScanTab()
    {
        _numDupSeconds.Value = Clamp(_config.Scan.DuplicateIgnoreSeconds, 0, 3600);

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.Controls.Add(LabelOf("Chống quét trùng (giây)"), 0, 0);
        p.Controls.Add(_numDupSeconds, 1, 0);

        var tab = new TabPage("Quét") { Controls = { p } };
        return tab;
    }

    private TabPage BuildDisplayTab()
    {
        _chkMask.Checked = _config.Ui.MaskCccdDigits;
        _numRecentRows.Value = Clamp(_config.Ui.RecentRows, 5, 500);
        _cmbStatusFilter.Items.AddRange(new object[] { "Today", "All" });
        _cmbStatusFilter.SelectedItem = _config.Ui.StatusFilter == "All" ? "All" : "Today";

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.Controls.Add(LabelOf("Hiển thị CCCD"), 0, 0);
        p.Controls.Add(_chkMask, 1, 0);
        p.Controls.Add(LabelOf("Số dòng log hiển thị"), 0, 1);
        p.Controls.Add(_numRecentRows, 1, 1);
        p.Controls.Add(LabelOf("Phạm vi log & xuất CSV"), 0, 2);
        p.Controls.Add(_cmbStatusFilter, 1, 2);

        var tab = new TabPage("Hiển thị") { Controls = { p } };
        return tab;
    }

    private TabPage BuildExportTab()
    {
        _txtCsvPath.Text = _config.Export.CsvPath;

        foreach (var f in _availableExportFields)
            _chkExportFields.Items.Add(f, _config.Export.Fields.Contains(f));

        var btnBrowse = new Button { Text = "…", AutoSize = true };
        btnBrowse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { SelectedPath = _txtCsvPath.Text };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _txtCsvPath.Text = dlg.SelectedPath;
        };

        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(14), AutoSize = true };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.Controls.Add(LabelOf("Thư mục CSV"), 0, 0);
        p.Controls.Add(Flow(_txtCsvPath, btnBrowse), 1, 0);
        p.Controls.Add(LabelOf("Các cột xuất ra"), 0, 1);
        p.Controls.Add(_chkExportFields, 1, 1);

        var tab = new TabPage("Xuất CSV") { Controls = { p } };
        return tab;
    }

    private static Label LabelOf(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 5, 12, 0) };

    private static FlowLayoutPanel Flow(params Control[] c)
    {
        var f = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        foreach (var x in c) f.Controls.Add(x);
        return f;
    }

    private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

    private void LoadPortList(ComboBox cmb, string currentPort)
    {
        try
        {
            var auto = new ComPortAutoDetector();
            var detected = auto.DetectPorts(string.Empty).ToList();
            var all = System.IO.Ports.SerialPort.GetPortNames().OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var ports = detected.Concat(all).Distinct().ToList();
            if (ports.Count == 0) ports.Add("COM1");

            // Giữ tên cổng thô riêng — text hiển thị có hậu tố "✓ máy quét",
            // lưu nguyên text đó vào config sẽ làm hỏng cổng (không mở được).
            _portNames.Clear();
            cmb.Items.Clear();
            foreach (var p in ports)
            {
                _portNames.Add(p);
                cmb.Items.Add(p + (detected.Contains(p) ? "  ✓ máy quét" : ""));
            }

            // Chọn đúng cổng đang dùng (không để mặc định item đầu).
            var idx = _portNames.FindIndex(p => string.Equals(p, currentPort, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) cmb.SelectedIndex = idx;
        }
        catch
        {
            // Không dò được → để trống, khách chọn tay.
        }
    }

    private static AppConfig Clone(AppConfig src) => new()
    {
        Reader = new ReaderSettings
        {
            Mode = src.Reader.Mode,
            Port = src.Reader.Port,
            BaudRate = src.Reader.BaudRate,
            DtrEnable = src.Reader.DtrEnable,
            AutoDetectPort = src.Reader.AutoDetectPort,
            ScannerVidPids = src.Reader.ScannerVidPids,
            ReconnectIntervalMs = src.Reader.ReconnectIntervalMs,
        },
        Scan = new ScanSettings
        {
            DuplicateIgnoreSeconds = src.Scan.DuplicateIgnoreSeconds,
            LogRejectedScans = src.Scan.LogRejectedScans,
        },
        Store = new StoreSettings
        {
            Type = src.Store.Type,
            SqlitePath = src.Store.SqlitePath,
            SaveFields = [.. src.Store.SaveFields],
        },
        Ui = new UiSettings
        {
            AutoStartListening = src.Ui.AutoStartListening,
            RecentRows = src.Ui.RecentRows,
            MaskCccdDigits = src.Ui.MaskCccdDigits,
            StatusFilter = src.Ui.StatusFilter,
        },
        Export = new ExportSettings
        {
            Fields = [.. src.Export.Fields],
            CsvPath = src.Export.CsvPath,
        },
    };

    private void SaveAndApply()
    {
        try
        {
            // Máy quét
            _config.Reader.Port = _rbManualCom.Checked && _cmbPort.SelectedIndex >= 0
                ? _portNames[_cmbPort.SelectedIndex]
                : "";
            _config.Reader.BaudRate = (int)_numBaud.Value;
            _config.Reader.ReconnectIntervalMs = _chkReconnect.Checked ? 3000 : 0;
            _config.Reader.AutoDetectPort = _rbAutoCom.Checked;

            // Quét
            _config.Scan.DuplicateIgnoreSeconds = (int)_numDupSeconds.Value;

            // Hiển thị
            _config.Ui.MaskCccdDigits = _chkMask.Checked;
            _config.Ui.RecentRows = (int)_numRecentRows.Value;
            _config.Ui.StatusFilter = _cmbStatusFilter.SelectedItem?.ToString() ?? "Today";

            // Xuất CSV
            _config.Export.CsvPath = string.IsNullOrWhiteSpace(_txtCsvPath.Text) ? "Data/export" : _txtCsvPath.Text.Trim();
            _config.Export.Fields = _chkExportFields.CheckedItems.Cast<string>().ToList();
            if (_config.Export.Fields.Count == 0)
                _config.Export.Fields = [.. CheckInCsvExporter.DefaultFields];   // không cho danh sách trống

            // Áp dụng
            Config.AppConfigFactory.ApplyDefaults(_config);
            Config.AppConfigFactory.Save(_config, _jsonPath);

            // Cập nhật toàn bộ live config (không chỉ vài trường) để UI/export dùng đúng giá trị mới.
            _root.Config.Reader.Port = _config.Reader.Port;
            _root.Config.Reader.BaudRate = _config.Reader.BaudRate;
            _root.Config.Reader.ReconnectIntervalMs = _config.Reader.ReconnectIntervalMs;
            _root.Config.Reader.AutoDetectPort = _config.Reader.AutoDetectPort;
            _root.Config.Scan.DuplicateIgnoreSeconds = _config.Scan.DuplicateIgnoreSeconds;
            _root.Config.Ui.MaskCccdDigits = _config.Ui.MaskCccdDigits;
            _root.Config.Ui.RecentRows = _config.Ui.RecentRows;
            _root.Config.Ui.StatusFilter = _config.Ui.StatusFilter;
            _root.Config.Export.CsvPath = _config.Export.CsvPath;
            _root.Config.Export.Fields = [.. _config.Export.Fields];

            // Đồng bộ cài đặt mới vào các đối tượng đang chạy — chúng giữ BẢN SAO tham số
            // lúc khởi động (reader giữ ReaderPortConfig riêng, guard giữ cửa sổ trùng riêng);
            // nếu không đồng bộ thì Lưu xong vẫn chạy theo giá trị cũ cho tới khi khởi động lại.
            if (_pipeline.Reader is SerialCccdReader serial)
                serial.ApplyConfig(new ReaderPortConfig
                {
                    Port = _config.Reader.Port,
                    BaudRate = _config.Reader.BaudRate,
                    DtrEnable = _config.Reader.DtrEnable,
                    AutoDetectPort = _config.Reader.AutoDetectPort,
                    ScannerVidPids = _config.Reader.ScannerVidPids,
                    ReconnectIntervalMs = _config.Reader.ReconnectIntervalMs,
                });
            _pipeline.SetDuplicateWindow(TimeSpan.FromSeconds(Math.Max(0, _config.Scan.DuplicateIgnoreSeconds)));

            // Nối lại theo lựa chọn của khách.
            _pipeline.Reader.Stop();                         // đóng cổng cũ trước (tránh khóa COM)
            if (!_rbDisableCom.Checked)
            {
                try { _pipeline.Reader.Start(); }
                catch (Exception startEx)
                {
                    // Cấu hình đã lưu nhưng máy quét không mở được — phải báo, không im lặng.
                    MessageBox.Show("Đã lưu cấu hình nhưng chưa mở được máy quét: " + startEx.Message,
                        "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            DialogResult = DialogResult.None;                // lưu thất bại → không đóng cửa sổ
            MessageBox.Show("Không lưu được cấu hình: " + ex.Message, "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}