namespace CccdCheckIn.App;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Lưới an toàn cuối: lỗi chưa xử lý không được làm app biến mất im lặng —
        // ghi vào Data\crash.log cạnh exe để chẩn đoán tại máy khách.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => CrashLog.Write("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog.Write("background thread",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "(không rõ)"));

        // Single-instance: chống 2 instance ghi trùng log (DB lock / số đếm nhầm).
        // "Global\" cho phép chặn instance ở mọi session (RDP/fast-user-switch) trên cùng máy.
        using var mutex = new Mutex(initiallyOwned: true, @"Global\CccdCheckIn.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Chương trình đang chạy ở cửa sổ khác. Hãy đóng cửa sổ cũ rồi mở lại.",
                "CccdCheckIn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(MainFormFactory.Create());
    }
}

/// <summary>
/// Tách factory khỏi Program để composition root + config một chỗ — MainForm không
/// tự tạo dependency (dễ test, dễ mở rộng).
/// </summary>
public static class MainFormFactory
{
    public static MainForm Create()
    {
        var configPath = Config.AppConfigFactory.AppSettingsPath(AppContext.BaseDirectory);
        var config = Config.AppConfigFactory.Load(configPath);
        var root = CompositionRoot.Create(config);
        return new MainForm(root, config);
    }
}

/// <summary>Ghi ngoại lệ chưa xử lý ra Data\crash.log cạnh exe — chẩn đoán tại máy khách.</summary>
internal static class CrashLog
{
    private static readonly object Lock = new();

    public static void Write(string source, Exception ex)
    {
        try
        {
            var dir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dir);
            lock (Lock)
            {
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Log crash mà gây crash thì vô nghĩa — bỏ qua.
        }
    }
}