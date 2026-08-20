namespace CccdCheckIn.Core.Licensing;

/// <summary>Trạng thái dùng thử, chống clock rollback bằng lastSeenUtc + sequence.</summary>
public sealed class TrialState
{
    public DateTimeOffset FirstSeenUtc { get; init; }

    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>Tăng mỗi lần app chạy; dùng phát hiện sửa file state lùi thời gian.</summary>
    public long Sequence { get; init; }

    public int SchemaVersion { get; init; } = 1;

    public DateTimeOffset TrialEndsUtc => FirstSeenUtc.AddDays(LicensingDefaults.TrialDays);
}
