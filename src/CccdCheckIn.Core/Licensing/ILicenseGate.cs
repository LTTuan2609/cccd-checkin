namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Cổng quyền thao tác theo license. Invariant: mọi trạng thái hết hạn
/// ⇒ CanRead + CanExport = true, CanWrite + CanCheckIn = false (không khóa dữ liệu khách).
/// </summary>
public interface ILicenseGate
{
    LicenseMode Mode { get; }

    LicenseValidationResult LastResult { get; }

    bool CanRead { get; }

    bool CanExport { get; }

    bool CanWrite { get; }

    bool CanCheckIn { get; }

    /// <summary>Cập nhật sau khi re-evaluate/activate.</summary>
    void Update(LicenseValidationResult result);
}
