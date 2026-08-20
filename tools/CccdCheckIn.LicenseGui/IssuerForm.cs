using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;
using CccdCheckIn.LicenseIssuer;

namespace CccdCheckIn.LicenseGui;

/// <summary>
/// Tool nội bộ phát hành license cho khách — KHÔNG ship.
/// Dán Mã Máy (chuỗi khách copy từ màn hình Bản quyền) → chọn gói Tháng/Năm → bấm Tạo
/// → copy Mã Kích Hoạt (hoặc ghi file .license) để gửi lại khách.
/// </summary>
public sealed class IssuerForm : Form
{
    private readonly TextBox _txtMachine = null!;
    private readonly RadioButton _rbMonthly = null!;
    private readonly RadioButton _rbYearly = null!;
    private readonly TextBox _txtResult = null!;
    private readonly Button _btnCopy = null!;
    private readonly Button _btnSave = null!;
    private readonly Label _lblStatus = null!;

    private string _lastCode = "";

    public IssuerForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        Text = "Phát hành License — CccdCheckIn";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 420);
        Font = new Font("Segoe UI", 9.5f);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(16),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // intro
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nhãn Mã Máy
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // ô Mã Máy
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // gói
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nút Tạo
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nhãn kết quả
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // ô kết quả
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nút copy/save
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // status

        var intro = new Label
        {
            Text = "Dán Mã Máy của khách hàng (chuỗi dài khách copy từ màn hình Bản quyền), chọn gói, bấm Tạo Mã → gửi Mã Kích Hoạt lại cho khách.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            ForeColor = Color.FromArgb(90, 100, 110),
            Padding = new Padding(0, 0, 0, 8),
        };

        var machineLabel = new Label
        {
            Text = "Mã Máy khách hàng:",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Padding = new Padding(0, 4, 0, 2),
        };

        _txtMachine = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10.5f),
            Height = 32,
            PlaceholderText = "Dán Mã Máy (43 ký tự)…",
        };

        var planRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 6, 0, 2),
        };
        _rbMonthly = new RadioButton { Text = "Theo tháng (30 ngày)", AutoSize = true, Checked = true };
        _rbYearly = new RadioButton { Text = "Theo năm (365 ngày)", AutoSize = true };
        foreach (var r in new[] { _rbMonthly, _rbYearly }) r.Margin = new Padding(0, 0, 16, 0);
        planRow.Controls.Add(_rbMonthly);
        planRow.Controls.Add(_rbYearly);

        var btnIssue = new Button { Text = "Tạo Mã Kích Hoạt", AutoSize = false, Width = 200, Height = 36 };
        btnIssue.Click += OnIssueClicked;
        btnIssue.Margin = new Padding(0, 6, 0, 6);

        var resultLabel = new Label
        {
            Text = "Mã Kích Hoạt:",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Padding = new Padding(0, 4, 0, 2),
        };

        _txtResult = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f),
            Multiline = true,
            Height = 88,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
        };

        var actionRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
        };
        _btnCopy = new Button { Text = "Sao chép Mã Kích Hoạt", AutoSize = false, Width = 200, Height = 32, Enabled = false };
        _btnCopy.Click += (_, _) =>
        {
            if (_lastCode.Length == 0) return;
            Clipboard.SetText(_lastCode);
            _btnCopy.Text = "✓ Đã sao chép";
            var t = this;
            t.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(1500);
                if (IsDisposed) return;
                _btnCopy.Text = "Sao chép Mã Kích Hoạt";
            }));
        };

        _btnSave = new Button { Text = "Ghi ra file .license…", AutoSize = false, Width = 160, Height = 32, Enabled = false };
        _btnSave.Click += OnSaveClicked;

        _btnCopy.Margin = new Padding(0, 0, 8, 0);
        actionRow.Controls.Add(_btnCopy);
        actionRow.Controls.Add(_btnSave);

        _lblStatus = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 22,
            ForeColor = Color.FromArgb(90, 110, 100),
            Padding = new Padding(0, 4, 0, 0),
            Font = new Font("Segoe UI", 9f),
        };

        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(machineLabel, 0, 1);
        layout.Controls.Add(_txtMachine, 0, 2);
        layout.Controls.Add(planRow, 0, 3);
        layout.Controls.Add(btnIssue, 0, 4);
        layout.Controls.Add(resultLabel, 0, 5);
        layout.Controls.Add(_txtResult, 0, 6);
        layout.Controls.Add(actionRow, 0, 7);
        layout.Controls.Add(_lblStatus, 0, 8);

        Controls.Add(layout);
    }

    private void OnIssueClicked(object? sender, EventArgs e)
    {
        var machine = _txtMachine.Text?.Trim() ?? "";
        try
        {
            LicenseIssueService.ValidateMachineCode(machine);
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
            return;
        }

        var plan = _rbYearly.Checked ? LicensePlan.Yearly : LicensePlan.Monthly;
        var days = plan == LicensePlan.Yearly ? 365 : 30;

        try
        {
            using var key = SigningKeyStore.CreateOrLoad().Key;
            var result = LicenseIssueService.Issue(key, machine, plan, days,
                keyId: "prod-2026-01", issuedAtUtc: DateTimeOffset.UtcNow);

            _lastCode = result.ActivationCode;
            _txtResult.Text = result.ActivationCode;
            _btnCopy.Enabled = true;
            _btnSave.Enabled = true;

            _lblStatus.ForeColor = Color.ForestGreen;
            _lblStatus.Text = $"✓ Đã tạo: {result.LicenseId} · {DescribePlan(plan)} (" + days + " ngày) · hiệu lực đến " +
                              result.Payload.ExpiresAtUtc.ToString("dd/MM/yyyy HH:mm 'UTC'");
        }
        catch (Exception ex)
        {
            ShowError("Lỗi phát hành: " + ex.Message);
        }
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_lastCode.Length == 0) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Lưu file license gửi khách",
            Filter = "License files (*.license)|*.license|All files (*.*)|*.*",
            FileName = $"license-{DateTime.Now:yyyyMMdd-HHmmss}.license",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            System.IO.File.WriteAllText(dlg.FileName, _lastCode);
            _lblStatus.ForeColor = Color.ForestGreen;
            _lblStatus.Text = "✓ Đã ghi: " + dlg.FileName;
        }
        catch (Exception ex)
        {
            ShowError("Không ghi được file: " + ex.Message);
        }
    }

    private static string DescribePlan(LicensePlan plan) => plan switch
    {
        LicensePlan.Monthly => "theo tháng",
        LicensePlan.Yearly => "theo năm",
        _ => "tùy chỉnh",
    };

    private void ShowError(string message)
    {
        _lblStatus.ForeColor = Color.Firebrick;
        _lblStatus.Text = message;
        _btnCopy.Enabled = _btnSave.Enabled = false;
    }
}