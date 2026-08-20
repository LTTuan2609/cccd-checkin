namespace CccdCheckIn.Core.Licensing;

/// <summary>Orchestrate đánh giá license/trial và kích hoạt.</summary>
public interface ILicenseService
{
    /// <summary>Đánh giá trạng thái license tại thời điểm hiện tại (trial hoặc license đã ký).</summary>
    LicenseValidationResult Evaluate(DateTimeOffset nowUtc);

    /// <summary>
    /// Kích hoạt bằng Mã Kích Hoạt. Verify chữ ký + máy + ngày; lưu atomic rồi trả kết quả mới.
    /// Lỗi kích hoạt KHÔNG overwrite license hiện có.
    /// </summary>
    LicenseValidationResult Activate(string activationCode, DateTimeOffset nowUtc);
}
