namespace CccdCheckIn.Core.Models;

/// <summary>Thông tin công dân đọc được từ QR CCCD (pipe-separated).</summary>
public sealed class CitizenInfo
{
    public string CccdNumber { get; init; } = "";
    public string OldIdNumber { get; init; } = "";   // có thể rỗng (biến thể Zalo)
    public string FullName { get; init; } = "";
    public DateTime DateOfBirth { get; init; }        // default khi trống
    public string Gender { get; init; } = "";
    public string Address { get; init; } = "";
    public DateTime IssueDate { get; init; }          // default khi trống
    public string RawPayload { get; init; } = "";
}