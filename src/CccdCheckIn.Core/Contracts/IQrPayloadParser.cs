namespace CccdCheckIn.Core.Contracts;

using CccdCheckIn.Core.Models;

/// <summary>Kết quả parse một chuỗi QR CCCD.</summary>
public sealed class CccdParseResult
{
    public bool IsValid { get; init; }
    public CitizenInfo? Citizen { get; init; }        // null khi invalid
    public string? ErrorMessage { get; init; }         // đã lọc, hiện được lên UI
    public List<string> Warnings { get; init; } = [];  // ví dụ: ngày sinh dự đoán, giới tính lạ
}

/// <summary>Parser chuỗi QR CCCD → CitizenInfo. Xử lý cả 2 biến thể (7/6 trường).</summary>
public interface IQrPayloadParser
{
    CccdParseResult Parse(string rawPayload);
}