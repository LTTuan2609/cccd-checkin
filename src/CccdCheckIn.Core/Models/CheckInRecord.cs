namespace CccdCheckIn.Core.Models;

/// <summary>Bản ghi một lượt check-in đã lưu.</summary>
public sealed class CheckInRecord
{
    public long Id { get; init; }
    public string CccdNumber { get; init; } = "";
    public string OldIdNumber { get; init; } = "";   // có thể rỗng (biến thể Zalo)
    public string FullName { get; init; } = "";
    public DateTime DateOfBirth { get; init; }
    public string Gender { get; init; } = "";
    public string Address { get; init; } = "";
    public DateTime IssueDate { get; init; }
    public DateTime CheckedInAt { get; init; }        // giờ check-in local
    public string? RawPayload { get; init; }           // null khi không lưu payload thô
}