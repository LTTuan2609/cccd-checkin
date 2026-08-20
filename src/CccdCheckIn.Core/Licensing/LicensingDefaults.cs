namespace CccdCheckIn.Core.Licensing;

/// <summary>Hằng số dùng chung cho hệ thống licensing.</summary>
public static class LicensingDefaults
{
    public const string Product = "CccdCheckIn";

    public const int PayloadVersion = 1;

    public const int TrialDays = 14;

    /// <summary>Độ lệch đồng hồ cho phép trước khi coi là clock rollback.</summary>
    public static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromMinutes(5);

    /// <summary>Số ngày trước khi hết hạn bắt đầu cảnh báo (30/7/1).</summary>
    public static readonly int[] ExpiryWarningDays = [30, 7, 1];
}
