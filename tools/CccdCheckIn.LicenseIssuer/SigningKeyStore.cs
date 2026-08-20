using System.Security.Cryptography;
using System.Text.Json;

namespace CccdCheckIn.LicenseIssuer;

/// <summary>
/// Load/lưu private key ký license của nhà cung cấp.
/// Private key nằm ở %ProgramData%\LTTuan\LicenseIssuer\private-key.json —
/// KHÔNG bao giờ vào repo/git (đã chặn trong .gitignore). Raw PEM tạm được
/// zeroed sau khi dùng. Đây là kho private tối giản cho MVP; cân nhắc
/// certificate store/HSM khi quy mô lớn hơn.
/// </summary>
public static class SigningKeyStore
{
    public static string DefaultKeyFile
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LTTuan", "LicenseIssuer", "private-key.json");

    public static (ECDsa Key, string PublicKeyBase64) CreateOrLoad(string? keyFile = null)
    {
        var path = keyFile ?? DefaultKeyFile;
        if (File.Exists(path))
        {
            var dto = JsonSerializer.Deserialize<PemStore>(File.ReadAllText(path));
            if (dto is null || string.IsNullOrEmpty(dto.Pem)) ThrowInvalid();
            var key = ECDsa.Create();
            try
            {
                key.ImportFromPem(dto!.Pem!);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("Private key file bị hỏng hoặc sai định dạng: " + path);
            }
            return (key, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        }

        // Tạo mới.
        var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = created.ExportPkcs8PrivateKeyPem();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new PemStore { Pem = pem }, new JsonSerializerOptions { WriteIndented = true }));
        CryptographicOperations.ZeroMemory(System.Text.Encoding.UTF8.GetBytes(pem));

        return (created, Convert.ToBase64String(created.ExportSubjectPublicKeyInfo()));
    }

    private static void ThrowInvalid()
        => throw new InvalidOperationException("Private key file không hợp lệ: " + DefaultKeyFile);

    private sealed class PemStore
    {
        public string? Pem { get; init; }
    }
}