namespace CccdCheckIn.Core.Licensing;

/// <summary>Verify chữ ký ECDSA trên canonical payload (chưa kiểm tra ngày/máy).</summary>
public interface ILicenseVerifier
{
    bool VerifySignature(ReadOnlySpan<byte> canonicalPayload, ReadOnlySpan<byte> signature);
}
