namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Public key ECDSA P-256 (SubjectPublicKeyInfo, base64) nhúng trong app để verify license.
/// Private key ký license CHỈ nằm ở tool issuer phía nhà cung cấp
/// (%ProgramData%\LTTuan\LicenseIssuer\private-key.json) — không bao giờ vào repo/app.
/// </summary>
public static class EmbeddedKeys
{
    /// <summary>Key ID hiện tại — issuer ghi keyId này vào payload.</summary>
    public const string CurrentKeyId = "prod-2026-01";

    /// <summary>Public key base64 (SPKI). Sinh bởi lệnh create-keys của LicenseIssuer.</summary>
    public const string LicensePublicKeyBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaJ1XXjaJ7Ug5ZJwszySqlW1+0Hd9LhZiFF4D7QFSCNIF/4kmzelO1haX6ILWB30mL31FZxkKxNSZGm6UkDHoFA==";
}
