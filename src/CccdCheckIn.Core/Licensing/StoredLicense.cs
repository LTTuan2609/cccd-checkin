namespace CccdCheckIn.Core.Licensing;

/// <summary>License đã kích hoạt, lưu trên máy khách.</summary>
public sealed class StoredLicense
{
    /// <summary>Mã Kích Hoạt: base64url(canonical payload) + "." + base64url(chữ ký).</summary>
    public string ActivationCode { get; init; } = string.Empty;

    public DateTimeOffset ActivatedAtUtc { get; init; }
}
