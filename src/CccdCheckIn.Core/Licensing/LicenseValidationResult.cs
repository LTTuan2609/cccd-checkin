namespace CccdCheckIn.Core.Licensing;

/// <summary>Kết quả đánh giá license/trial tại thời điểm hiện tại.</summary>
public sealed class LicenseValidationResult
{
    public LicenseStatus Status { get; init; }

    public LicenseMode Mode { get; init; }

    public LicensePayload? Payload { get; init; }

    /// <summary>Thông báo cho người dùng (tiếng Việt, tầng UI giữ nguyên).</summary>
    public string? Message { get; init; }

    /// <summary>Số ngày còn lại (trial hoặc trước khi hết hạn); null nếu không áp dụng.</summary>
    public int? DaysRemaining { get; init; }

    public static LicenseValidationResult Missing() => new()
    {
        Status = LicenseStatus.Missing,
        Mode = LicenseMode.ReadExportOnly,
        Message = "Chưa có license.",
    };
}
