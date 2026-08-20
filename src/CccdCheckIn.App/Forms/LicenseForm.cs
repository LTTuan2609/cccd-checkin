using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.App.Forms;

/// <summary>
/// Màn hình Bản quyền: hiện Mã Máy cho khách (copy gửi nhà cung cấp), ô nhập
/// Mã Kích Hoạt + nút Kích hoạt (hoặc import file .license), hiển thị trạng thái/gói
/// hiệu lực/hết hạn. Sau khi kích hoạt thành công cập nhật gate + đóng trả OK.
/// </summary>
public sealed class LicenseForm : Form
{
    private readonly ILicenseService _licensing;
    private readonly ILicenseGate _gate;
    private readonly IMachineIdentityProvider _identity;
    private readonly Action _refreshMain;

    private TextBox _lblMachineCode = null!;
    private Label _lblMachineCaption = null!;
    private Label _lblStatus = null!;
    private TextBox _txtCode = null!;
    private Button _btnCopy = null!;
    private Button _btnPaste = null!;
    private Button _btnActivate = null!;
    private Button _btnImport = null!;
    private Button _btnDone = null!;

    public LicenseForm(ILicenseService licensing, ILicenseGate gate,
                       IMachineIdentityProvider identity, Action refreshMain)
    {
        _licensing = licensing;
        _gate = gate;
        _identity = identity;
        _refreshMain = refreshMain;

        InitializeComponent();
        RefreshView();
    }

    private void InitializeComponent()
    {
        Text = "Bản quyền — CccdCheckIn";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 460);
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(16),
            AutoScroll = true,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // hướng dẫn
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Mã Máy label
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Mã Máy + copy
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // trạng thái
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nhãn nhập mã
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // ô nhập
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nút hành động
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = new Label
        {
            Text = "Sao chép NGUYÊN chuỗi Mã Máy bên dưới (không rút gọn) gửi nhà cung cấp " +
                   "kèm thông tin đăng ký. Bạn sẽ nhận Mã Kích Hoạt, dán vào ô bên dưới rồi bấm Kích hoạt.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(90, 100, 110),
            Padding = new Padding(0, 0, 0, 8),
        };

        _lblMachineCaption = new Label
        {
            Text = "Mã Máy (gửi cho nhà cung cấp):",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Padding = new Padding(0, 6, 0, 2),
        };

        // TextBox ReadOnly thay cho Label → người dùng bôi đen/copy bằng chuột được luôn
        // (Label không cho text selection — trước đây chỉ có nút Sao chép mà lại không được
        //  add vào layout nên vô hình, user không copy được Mã Máy).
        _lblMachineCode = new TextBox
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ReadOnly = true,
            Font = new Font("Consolas", 11f),
            ForeColor = Color.FromArgb(20, 60, 130),
            BackColor = Color.FromArgb(245, 248, 252),
            BorderStyle = BorderStyle.FixedSingle,
        };
        _lblMachineCode.Click += (_, _) => _lblMachineCode.SelectAll();

        _btnCopy = new Button { Text = "Sao chép Mã Máy", AutoSize = false, Width = 160, Height = 32 };
        _btnCopy.Click += (_, _) =>
        {
            Clipboard.SetText(_lblMachineCode.Text.Trim());
            _btnCopy.Text = "✓ Đã sao chép";
            var t = this;
            t.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(1500);
                if (IsDisposed) return;
                _btnCopy.Text = "Sao chép Mã Máy";
            }));
        };

        var statusWrapper = new Panel { Dock = DockStyle.Fill, AutoSize = false, Height = 52, Padding = new Padding(0, 8, 0, 4) };
        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(2, 2, 2, 2),
        };
        statusWrapper.Controls.Add(_lblStatus);

        var codeLabel = new Label
        {
            Text = "Mã Kích Hoạt:",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Padding = new Padding(0, 6, 0, 2),
        };

        _txtCode = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10f),
            Multiline = true,
            Height = 76,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "Dán Mã Kích Hoạt vào đây…",
        };

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };

        _btnPaste = new Button { Text = "Dán", AutoSize = false, Width = 90, Height = 32 };
        _btnPaste.Click += (_, _) => _txtCode.Text = Clipboard.ContainsText() ? Clipboard.GetText() : "";

        _btnActivate = new Button { Text = "Kích hoạt", AutoSize = false, Width = 120, Height = 32 };
        _btnActivate.Click += OnActivateClicked;

        _btnImport = new Button { Text = "Mở file .license…", AutoSize = false, Width = 130, Height = 32 };
        _btnImport.Click += OnImportClicked;

        _btnDone = new Button { Text = "Đóng", AutoSize = false, Width = 100, Height = 32 };
        _btnDone.Click += (_, _) => Close();

        foreach (var b in new[] { _btnPaste, _btnActivate, _btnImport, _btnDone })
            b.Margin = new Padding(0, 0, 8, 0);
        actionRow.Controls.Add(_btnPaste);
        actionRow.Controls.Add(_btnActivate);
        actionRow.Controls.Add(_btnImport);
        actionRow.Controls.Add(_btnDone);

        // Nút Sao chép đặt cạnh ô Mã Máy. TableLayoutPanel chứ KHÔNG phải FlowLayoutPanel —
        // Flow bỏ qua Dock=Fill khiến ô mã chỉ render ~100px, cắt mất chuỗi 43 ký tự.
        var machineRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 36,
            ColumnCount = 2,
            Padding = new Padding(0, 2, 0, 2),
        };
        machineRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        machineRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _lblMachineCode.Height = 28;
        _btnCopy.Height = 28;
        _btnCopy.Margin = new Padding(8, 0, 0, 0);
        machineRow.Controls.Add(_lblMachineCode, 0, 0);
        machineRow.Controls.Add(_btnCopy, 1, 0);

        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(_lblMachineCaption, 0, 1);
        layout.Controls.Add(machineRow, 0, 2);
        layout.Controls.Add(statusWrapper, 0, 3);
        layout.Controls.Add(codeLabel, 0, 4);
        layout.Controls.Add(_txtCode, 0, 5);
        layout.Controls.Add(actionRow, 0, 6);

        Controls.Add(layout);
    }

    private void RefreshView()
    {
        try
        {
            var identity = _identity.GetOrCreateAsync().GetAwaiter().GetResult();
            // CRITICAL: phải hiển thị/copy MachineKeyHash ĐẦY ĐỦ (base64url SHA-256) —
            // issuer bind license vào đúng chuỗi này. MachineCode rút gọn (LT-XXXX-…)
            // chỉ dùng để đối chiếu bằng mắt, gửi nó đi thì kích hoạt luôn MachineMismatch.
            _lblMachineCode.Text = identity.MachineKeyHash;
            _lblMachineCaption.Text = $"Mã Máy ({identity.MachineCode}) — gửi NGUYÊN chuỗi bên dưới:";
        }
        catch (Exception ex)
        {
            _lblMachineCode.Text = "(không đọc được Mã Máy)";
            SetStatus("Lỗi đọc Mã Máy: " + ex.Message, Color.Firebrick);
            return;
        }

        var result = _gate.LastResult;
        SetStatus(BuildStatusMessage(result), StatusColor(result.Status));
    }

    private static string BuildStatusMessage(LicenseValidationResult r) => r.Status switch
    {
        LicenseStatus.Valid => "✓ License " + (
            r.Payload?.Plan == LicensePlan.Monthly ? "theo tháng" :
            r.Payload?.Plan == LicensePlan.Yearly ? "theo năm" : "tùy chỉnh") +
            " — còn " + (r.DaysRemaining ?? 0) + " ngày.",
        LicenseStatus.Trial => "Dùng thử — còn " + (r.DaysRemaining ?? 0) + " ngày.",
        LicenseStatus.Expired => "✕ " + r.Message,
        _ => r.Message ?? "Chưa kích hoạt.",
    };

    private static Color StatusColor(LicenseStatus s) => s switch
    {
        LicenseStatus.Valid or LicenseStatus.Trial => Color.ForestGreen,
        LicenseStatus.TrialExpired or LicenseStatus.Expired => Color.Maroon,
        _ => Color.FromArgb(150, 100, 0),
    };

    private void SetStatus(string text, Color color)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = color;
    }

    private void OnActivateClicked(object? sender, EventArgs e)
    {
        var code = _txtCode.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show("Vui lòng nhập Mã Kích Hoạt.", "Bản quyền",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            var result = _licensing.Activate(code, DateTimeOffset.UtcNow);
            Cursor = Cursors.Default;

            if (result.Status is LicenseStatus.Valid)
            {
                _gate.Update(result);
                _refreshMain();
                SetStatus("✓ Kích hoạt thành công — " + BuildStatusMessage(result), Color.ForestGreen);
                MessageBox.Show("Kích hoạt thành công!", "Bản quyền",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                SetStatus("✕ " + (result.Message ?? "Kích hoạt thất bại."), Color.Firebrick);
                MessageBox.Show(result.Message ?? "Kích hoạt thất bại.", "Bản quyền",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            SetStatus("✕ Lỗi kích hoạt: " + ex.Message, Color.Firebrick);
        }
    }

    private void OnImportClicked(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Chọn file license (.license)",
            Filter = "License files (*.license)|*.license|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _txtCode.Text = File.ReadAllText(dlg.FileName).Trim();
            OnActivateClicked(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Không đọc được file license: " + ex.Message, "Bản quyền",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}