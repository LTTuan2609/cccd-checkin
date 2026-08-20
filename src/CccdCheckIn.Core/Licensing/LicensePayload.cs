namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Payload license được nhà cung cấp ký bằng ECDSA P-256.
/// MachineKeyHash = base64url(SHA-256(machine public key)) — binding license với máy.
/// </summary>
public sealed class LicensePayload
{
    public int Version { get; init; } = LicensingDefaults.PayloadVersion;

    public string Product { get; init; } = LicensingDefaults.Product;

    public string LicenseId { get; init; } = string.Empty;

    public string MachineKeyHash { get; init; } = string.Empty;

    public LicensePlan Plan { get; init; } = LicensePlan.Monthly;

    public DateTimeOffset IssuedAtUtc { get; init; }

    public DateTimeOffset NotBeforeUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>Định danh khóa ký (hỗ trợ rotate key về sau).</summary>
    public string KeyId { get; init; } = string.Empty;
}
