namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Serialize payload sang bytes canonical (deterministic) để ký/verify.
/// Không dùng JsonSerializer trực tiếp làm protocol contract — thứ tự property phải cố định.
/// </summary>
public interface ILicenseCanonicalizer
{
    byte[] Canonicalize(LicensePayload payload);

    /// <summary>Parse ngược từ canonical bytes; trả null nếu sai cấu trúc.</summary>
    LicensePayload? Parse(ReadOnlySpan<byte> canonical);
}
