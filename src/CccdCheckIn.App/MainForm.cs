using CccdCheckIn.App.Config;
using CccdCheckIn.App.Services;
using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Models;
using CccdCheckIn.Storage.Sqlite;

namespace CccdCheckIn.App;

/// <summary>
/// Dashboard check-in. Status bar theo ReaderState, 2 thẻ số lớn (hôm nay / tổng),
/// grid log gần nhất (mask số CCCD), flash xanh khi quét hợp lệ, export CSV, cài đặt.
/// Mọi cập nhật control từ thread khác đều qua BeginInvoke — an toàn.
/// </summary>
public partial class MainForm : Form
{
    private readonly CompositionRoot _root;
    private readonly CheckInPipeline _pipeline;
    private readonly UiSettings _ui;
    private bool _autoStarted;

    // --- UI controls (khởi tạo trong InitializeComponent) ---
    private Label _lblStatus = null!;
    private Button _btnReconnect = null!;
    private Button _btnSettings = null!;
    private Button _btnExport = null!;
    private Button _btnExit = null!;
    private Label _lblTodayValue = null!;
    private Label _lblTotalValue = null!;
    private Label _lblLastScan = null!;
    private DataGridView _grid = null!;
    private Label _lblAlert = null!;

    public MainForm(CompositionRoot root, AppConfig config)
    {
        _root = root;
        _pipeline = root.Pipeline;
        _ui = config.Ui;

        InitializeComponent();
        WireEvents();
        RefreshCounters();
    }

    /// <summary>
    /// Bắt đầu lắng nghe sau khi form đã hiển thị (handle tồn tại). Gọi vào lúc này
    /// vì Start() có thể bắn ReaderStateChanged/ScanCompleted đồng bộ ngay — nếu gọi
    /// trong ctor, BeginInvoke sẽ ném "window handle has not yet been created".
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_autoStarted) return;
        _autoStarted = true;
        LoadRecentLog();
        if (_ui.AutoStartListening) _pipeline.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pipeline.Dispose();
            _root.Dispose();
        }
        base.Dispose(disposing);
    }

    private void WireEvents()
    {
        _pipeline.ScanCompleted += OnScanCompleted;
        _pipeline.ReaderStateChanged += OnReaderStateChanged;
        _pipeline.PipelineError += OnPipelineError;
    }

    // ---------------- Sự kiện pipeline (đến từ thread của reader) ----------------

    private void OnScanCompleted(object? sender, ScanOutcome outcome)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => ApplyScanOutcome(outcome));
    }

    private void OnReaderStateChanged(object? sender, ReaderStateChangedEventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => ApplyReaderState(e));
    }

    private void OnPipelineError(object? sender, Exception ex)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() => ShowAlert("Lỗi hệ thống: " + ex.Message, AlertKind.Error));
    }

    // ---------------- Xử lý kết quả quét ----------------

    private void ApplyScanOutcome(ScanOutcome outcome)
    {
        if (outcome.IsDuplicate)
        {
            PlaySound(false);
            ShowAlert("Thẻ đã quét — bỏ qua (chống trùng).", AlertKind.Warning);
            return;
        }
        if (!outcome.Accepted)
        {
            PlaySound(false);
            ShowAlert("QR không hợp lệ: " + outcome.Message, AlertKind.Error);
            return;
        }

        PlaySound(true);
        ShowAlert("", AlertKind.None);
        if (outcome.Record is not null)
            InsertGridRow(outcome.Record);

        RefreshCounters();
        UpdateLastScan(outcome);

        if (outcome.Warnings.Count > 0)
            ShowAlert(string.Join(" · ", outcome.Warnings), AlertKind.Warning);
    }

    private void InsertGridRow(CheckInRecord record)
    {
        var local = record.CheckedInAt.ToLocalTime();
        var row = new object?[]
        {
            local.ToString("HH:mm:ss"),
            local.ToString("dd/MM/yyyy"),
            record.FullName,
            _ui.MaskCccdDigits ? MaskCccd(record.CccdNumber) : record.CccdNumber,
            record.Gender,
            record.Address,
        };

        if (_grid.Rows.Count >= _ui.RecentRows)
            _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
        _grid.Rows.Insert(0, row!);

        // Chỉ highlight dòng mới nhất — không lặp toàn bảng mỗi lần quét.
        if (_grid.Rows.Count > 1) _grid.Rows[1].DefaultCellStyle.BackColor = Color.White;
        _grid.Rows[0].DefaultCellStyle.BackColor = Color.LightGreen;
    }

    private static string MaskCccd(string cccd)
    {
        if (string.IsNullOrEmpty(cccd) || cccd.Length <= 4) return cccd;
        return new string('*', cccd.Length - 4) + cccd[^4..];
    }

    /// <summary>Phản hồi âm thanh tại quầy — bọc try/catch vì máy có thể không có sound card.</summary>
    private static void PlaySound(bool success)
    {
        try { (success ? System.Media.SystemSounds.Asterisk : System.Media.SystemSounds.Exclamation).Play(); }
        catch { }
    }

    private async void RefreshCounters()
    {
        try
        {
            var todayCount = await _pipeline.Store.GetCountAsync(DateTime.Today);
            var totalCount = await _pipeline.Store.GetCountAsync();
            _lblTodayValue.Text = todayCount.ToString("N0");
            _lblTotalValue.Text = totalCount.ToString("N0");
        }
        catch (Exception ex)
        {
            ShowAlert("Không đọc được số liệu: " + ex.Message, AlertKind.Error);
        }
    }

    /// <summary>Nạp log theo bộ lọc hiện tại (Today → chỉ hôm nay; All → N dòng gần nhất).</summary>
    private async void LoadRecentLog()
    {
        try
        {
            IReadOnlyList<CheckInRecord> recent = string.Equals(_ui.StatusFilter, "All", StringComparison.OrdinalIgnoreCase)
                ? await _pipeline.Store.GetRecentAsync(_ui.RecentRows)
                : await _pipeline.Store.GetWhereAtLeastAsync(DateTime.Today);
            _grid.Rows.Clear();
            foreach (var record in recent)
                InsertGridRow(record);
        }
        catch (Exception ex)
        {
            ShowAlert("Không đọc được log gần nhất: " + ex.Message, AlertKind.Error);
        }
    }

    private void UpdateLastScan(ScanOutcome outcome)
    {
        if (outcome.Citizen is null) return;
        var c = outcome.Citizen;
        var dob = c.DateOfBirth == default ? "?" : c.DateOfBirth.ToString("dd/MM/yyyy");
        var line = $"{c.FullName} · {(_ui.MaskCccdDigits ? MaskCccd(c.CccdNumber) : c.CccdNumber)} · {c.Gender} · {dob}";
        _lblLastScan.Text = $"{DateTime.Now:HH:mm:ss}  ✓  {line}";
        _lblLastScan.ForeColor = Color.ForestGreen;
    }

    // ---------------- Trạng thái máy đọc ----------------

    private void ApplyReaderState(ReaderStateChangedEventArgs e)
    {
        (string text, Color color) = e.State switch
        {
            ReaderState.Listening => ("● " + (e.Message ?? "Đang lắng nghe…"), Color.ForestGreen),
            ReaderState.Reconnecting => ("⟳ " + (e.Message ?? "Đang kết nối lại…"), Color.OrangeRed),
            ReaderState.Error => ("✕ " + (e.Message ?? "Lỗi máy đọc."), Color.Firebrick),
            ReaderState.Connecting => ("⟳ " + (e.Message ?? "Đang kết nối…"), Color.DarkOrange),
            _ => ("○ " + (e.Message ?? "Máy đọc chưa kết nối."), Color.Gray),
        };
        _lblStatus.Text = text;
        _lblStatus.ForeColor = color;
        _btnReconnect.Enabled = e.State is ReaderState.Reconnecting or ReaderState.Error;
    }

    private enum AlertKind { None, Warning, Error }

    private void ShowAlert(string message, AlertKind kind)
    {
        _lblAlert.Text = message;
        _lblAlert.ForeColor = kind switch
        {
            AlertKind.Error => Color.Firebrick,
            AlertKind.Warning => Color.DarkOrange,
            _ => Color.FromArgb(90, 110, 100),
        };
        _lblAlert.BackColor = kind switch
        {
            AlertKind.Error => Color.FromArgb(255, 235, 235),
            AlertKind.Warning => Color.FromArgb(255, 248, 220),
            _ => Color.Transparent,
        };
    }

    // ---------------- Nút thao tác ----------------

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        _btnExport.Enabled = false;
        ShowAlert("Đang xuất CSV…", AlertKind.None);
        try
        {
            var from = string.Equals(_ui.StatusFilter, "All", StringComparison.OrdinalIgnoreCase) ? (DateTime?)null : DateTime.Today;
            IReadOnlyList<CheckInRecord> records = from is null
                ? await _pipeline.Store.GetRecentAsync(int.MaxValue)
                : await _pipeline.Store.GetWhereAtLeastAsync(from.Value);

            var csvDir = Path.GetFullPath(_root.Config.Export.CsvPath);
            Directory.CreateDirectory(csvDir);
            var path = Path.Combine(csvDir, $"checkin_{DateTime.Now:yyyyMMdd_HHmmss_fff}.csv");

            // Dựng exporter theo cấu hình HIỆN TẠI — cột xuất đổi trong Cài đặt phải ăn ngay,
            // không chờ khởi động lại.
            var exporter = new CheckInCsvExporter(_root.Config.Export.Fields);
            await exporter.ExportAsync(path, records);

            var result = MessageBox.Show(
                $"Đã xuất {records.Count:N0} lượt ra:\n{path}\n\nMở thư mục?", "Xuất CSV",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
                OpenFolder(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không xuất được CSV: " + ex.Message, "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnExport.Enabled = true;
            ShowAlert("", AlertKind.None);
        }
    }

    private static void OpenFolder(string path)
    {
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
    }

    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        try
        {
            _pipeline.Reader.Start();
            ShowAlert("Đang kết nối lại…", AlertKind.None);
        }
        catch (Exception ex)
        {
            ShowAlert("Không kết nối lại được: " + ex.Message, AlertKind.Error);
        }
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        using var form = new SettingsForm(_root, _pipeline);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // SettingsForm đã ghi trực tiếp vào _root.Config (cùng tham chiếu với _ui) — chỉ cần nạp lại hiển thị.
            _grid.Rows.Clear();
            LoadRecentLog();
            RefreshCounters();
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        // Tránh tắt nhầm ứng dụng khi đang vận hành tại quầy.
        if (MessageBox.Show("Thoát ứng dụng check-in?", "Xác nhận thoát",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            Close();
    }

    // ---------------- Layout ----------------

    /// <summary>Thẻ số thống kê: tiêu đề AutoSize trên, số lớn Fill dưới — cấu trúc
    /// TableLayoutPanel đảm bảo hai dòng không bao giờ đè lên nhau.</summary>
    private static Panel StatCard(string title, Label value, Color cardBack, Color titleColor)
    {
        var eyebrow = new Label
        {
            Text = title, Dock = DockStyle.Fill, AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), ForeColor = titleColor,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(6, 12, 6, 8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(eyebrow, 0, 0);
        layout.Controls.Add(value, 0, 1);

        return new Panel
        {
            Dock = DockStyle.Top, Height = 118, BackColor = cardBack,
            Margin = new Padding(0, 0, 0, 8),
            Controls = { layout },
        };
    }

    private void InitializeComponent()
    {
        // Scale theo DPI — máy khách laptop hay chạy 125/150%; thiếu 2 dòng này
        // chữ phình to nhưng khung giữ nguyên kích thước cũ → chèn/đè chữ.
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom, Height = 26,
            BackColor = Color.FromArgb(235, 240, 245),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.Gray,
            Padding = new Padding(6, 3, 0, 0),
        };

        _btnReconnect = new Button { Text = "Kết nối lại", AutoSize = true, Enabled = false };
        _btnSettings = new Button { Text = "Cài đặt", AutoSize = true };
        _btnExport = new Button { Text = "Xuất CSV", AutoSize = true };
        _btnExit = new Button { Text = "Thoát", AutoSize = true };

        _lblTodayValue = new Label
        {
            Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Light", 30, FontStyle.Regular), ForeColor = Color.FromArgb(27, 122, 67),
            AutoEllipsis = true,
        };
        _lblTotalValue = new Label
        {
            Text = "0", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Light", 30, FontStyle.Regular), ForeColor = Color.FromArgb(40, 90, 150),
            AutoEllipsis = true,
        };
        _lblLastScan = new Label
        {
            Text = "Chưa có lượt quét nào.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5f), ForeColor = Color.Gray,
            AutoEllipsis = true,
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
        };
        _grid.Columns.Add("Time", "Giờ");
        _grid.Columns.Add("Date", "Ngày");
        _grid.Columns.Add("Name", "Họ tên");
        _grid.Columns.Add("Cccd", "Số CCCD");
        _grid.Columns.Add("Gender", "GT");
        _grid.Columns.Add("Address", "Địa chỉ");
        // Chia bề rộng theo nội dung: địa chỉ rộng nhất, GT hẹp nhất.
        _grid.Columns["Time"]!.FillWeight = 70;
        _grid.Columns["Date"]!.FillWeight = 80;
        _grid.Columns["Name"]!.FillWeight = 140;
        _grid.Columns["Cccd"]!.FillWeight = 110;
        _grid.Columns["Gender"]!.FillWeight = 45;
        _grid.Columns["Address"]!.FillWeight = 230;

        _lblAlert = new Label
        {
            Text = "", Dock = DockStyle.Bottom, Height = 26,
            ForeColor = Color.FromArgb(90, 110, 100), Padding = new Padding(6, 2, 0, 0),
            Font = new Font("Segoe UI", 9.5f), AutoEllipsis = true,
        };

        // Toolbar trên cùng
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 4),
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
        };
        // Khoảng cách đều giữa các nút (FlowLayoutPanel mặc định margin 3px — dính quá).
        foreach (var b in new[] { _btnReconnect, _btnSettings, _btnExport, _btnExit })
            b.Margin = new Padding(0, 0, 10, 0);
        toolbar.Controls.Add(_btnReconnect);
        toolbar.Controls.Add(_btnSettings);
        toolbar.Controls.Add(_btnExport);
        toolbar.Controls.Add(_btnExit);

        // Cột thống kê — mỗi thẻ là TableLayoutPanel (tiêu đề AutoSize + số Fill)
        // nên không bao giờ chèn chữ dù DPI hay cỡ chữ thay đổi.
        // Phân bậc bằng màu: xanh lục = hôm nay (hero), xanh dương = tổng, trắng = vừa quét.
        var todayCell = StatCard("ĐÃ CHECK-IN HÔM NAY", _lblTodayValue,
            cardBack: Color.FromArgb(237, 245, 239), titleColor: Color.FromArgb(74, 122, 92));
        var totalCell = StatCard("TỔNG LƯỢT", _lblTotalValue,
            cardBack: Color.FromArgb(239, 243, 247), titleColor: Color.FromArgb(90, 115, 137));

        var lastEyebrow = new Label
        {
            Text = "VỪA QUÉT", Dock = DockStyle.Fill, AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(122, 130, 138),
        };
        var lastLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(14, 12, 14, 10),
        };
        lastLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        lastLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lastLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        lastLayout.Controls.Add(lastEyebrow, 0, 0);
        lastLayout.Controls.Add(_lblLastScan, 0, 1);
        var lastCell = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        lastCell.Controls.Add(lastLayout);

        var leftCol = new Panel
        {
            Dock = DockStyle.Left, Width = 260,
            BackColor = Color.FromArgb(240, 242, 245), Padding = new Padding(8),
        };
        leftCol.Controls.Add(lastCell);
        leftCol.Controls.Add(totalCell);
        leftCol.Controls.Add(todayCell);

        var gridWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        gridWrap.Controls.Add(_grid);

        // Không dùng SplitContainer ở đây: FixedPanel.Panel1 + scale DPI làm panel trái
        // giữ nguyên bề rộng trong khi form co lại → bảng log đè lên cột thống kê.
        // Docking trực tiếp (Left rồi Fill) luôn giữ đúng khoảng cách 260px.
        Controls.Add(gridWrap);
        Controls.Add(leftCol);
        Controls.Add(_lblAlert);
        Controls.Add(toolbar);
        Controls.Add(_lblStatus);

        Text = "CCCD — Dashboard check-in";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 560);
        ClientSize = new Size(1100, 660);
        Font = new Font("Segoe UI", 9.5f);

        _btnExport.Click += OnExportClicked;
        _btnReconnect.Click += OnReconnectClicked;
        _btnSettings.Click += OnSettingsClicked;
        _btnExit.Click += OnExitClicked;
    }
}