namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Đường dẫn lưu state licensing: %ProgramData%\LTTuan\CccdCheckIn\
/// (License\license.dat, Machine\machine-key.dpapi, State\trial.dat + trial.marker).
/// Tách khỏi Data\ cạnh exe — Data\ là dữ liệu của khách hàng.
/// </summary>
public static class LicensingPaths
{
    public const string Vendor = "LTTuan";

    public const string Product = "CccdCheckIn";

    /// <summary>Cho phép test inject thư mục gốc khác thay vì ProgramData.</summary>
    public static string GetRoot(string? rootOverride = null)
        => rootOverride
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
               Vendor, Product);

    public static string MachineKeyFile(string root) => Path.Combine(root, "Machine", "machine-key.dpapi");

    public static string LicenseFile(string root) => Path.Combine(root, "License", "license.dat");

    public static string TrialStateFile(string root) => Path.Combine(root, "State", "trial.dat");

    /// <summary>Marker plaintext để đối chiếu với trial.dat (DPAPI) — lệch nhau ⇒ state bị sửa.</summary>
    public static string TrialMarkerFile(string root) => Path.Combine(root, "State", "trial.marker");
}
