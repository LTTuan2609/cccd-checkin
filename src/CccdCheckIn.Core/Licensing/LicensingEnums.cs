namespace CccdCheckIn.Core.Licensing;

/// <summary>Kết quả xác thực license/trial.</summary>
public enum LicenseStatus
{
    Valid,
    Trial,
    NotYetValid,
    Expired,
    TrialExpired,
    MachineMismatch,
    InvalidSignature,
    Malformed,
    Missing,
    Tampered,
}

/// <summary>Chế độ hoạt động của ứng dụng theo license.</summary>
public enum LicenseMode
{
    Trial,
    Active,
    ReadExportOnly,
}

/// <summary>Gói thuê bao.</summary>
public enum LicensePlan
{
    Monthly,
    Yearly,
    Custom,
}
