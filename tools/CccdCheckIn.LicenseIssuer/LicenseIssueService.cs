using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.LicenseIssuer;

/// <summary>Kết quả một lần phát hành license (dùng chung CLI + GUI).</summary>
public sealed record LicenseIssueResult(LicensePayload Payload, string ActivationCode, string LicenseId);

/// <summary>
/// Logic phát hành license dùng chung cho CLI (Program.cs) và GUI.
/// Nhận Mã Máy + gói → ký bằng private key → trả LicenseIssueResult.
/// </summary>
public static class LicenseIssueService
{
    public static void ValidateMachineCode(string machineId)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new ArgumentException("Mã Máy không được để trống.", nameof(machineId));
        // Base64url SHA-256 = 43 ký tự; rút gọn LT-XXXX-... sai → báo sớm.
        if (machineId.Length != 43)
            throw new ArgumentException(
                "Mã Máy phải đúng 43 ký tự (base64url SHA-256). Hãy dùng chuỗi khách copy từ màn hình Bản quyền — không phải mã rút gọn LT-….",
                nameof(machineId));
    }

    public static LicenseIssueResult Issue(
        ECDsa signingKey,
        string machineId,
        LicensePlan plan,
        int days,
        string keyId,
        DateTimeOffset issuedAtUtc)
    {
        ValidateMachineCode(machineId);

        var payload = new LicensePayload
        {
            Version = LicensingDefaults.PayloadVersion,
            Product = LicensingDefaults.Product,
            LicenseId = "LIC-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            MachineKeyHash = machineId.Trim(),
            Plan = plan,
            IssuedAtUtc = issuedAtUtc,
            NotBeforeUtc = issuedAtUtc,
            ExpiresAtUtc = issuedAtUtc.AddDays(days),
            KeyId = keyId,
        };

        var canonical = new LicenseCanonicalizer().Canonicalize(payload);
        var signature = signingKey.SignData(canonical, HashAlgorithmName.SHA256);
        var code = ActivationCodeCodec.Encode(canonical, signature);

        return new LicenseIssueResult(payload, code, payload.LicenseId);
    }
}