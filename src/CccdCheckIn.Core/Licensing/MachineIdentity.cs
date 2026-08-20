namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Danh tính máy: keypair P-256 sinh lần đầu, private key giữ trên máy (DPAPI).
/// License bind vào MachineKeyHash chứ không phải hardware fingerprint.
/// </summary>
public sealed class MachineIdentity
{
    public string PublicKeyBase64Url { get; init; } = string.Empty;

    /// <summary>base64url(SHA-256(public key bytes)) — giá trị binding trong payload license.</summary>
    public string MachineKeyHash { get; init; } = string.Empty;

    /// <summary>Mã Máy hiển thị cho khách hàng (vd: LT-XXXX-XXXX-XXXX-XXXX).</summary>
    public string MachineCode { get; init; } = string.Empty;
}
