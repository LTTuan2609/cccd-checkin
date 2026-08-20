using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Logic trial thuần (không IO): phát hiện clock rollback bằng lastSeenUtc + sequence.
/// now &lt; lastSeen - skew ⇒ Tampered (fail-closed); lastSeen luôn tiến về phía trước.
/// </summary>
public sealed class TrialClock
{
    private readonly TimeSpan _skewTolerance;

    private readonly int _trialDays;

    public TrialClock(TimeSpan? skewTolerance = null, int? trialDays = null)
    {
        _skewTolerance = skewTolerance ?? LicensingDefaults.ClockSkewTolerance;
        _trialDays = trialDays ?? LicensingDefaults.TrialDays;
    }

    public TrialEvaluation Evaluate(TrialState? state, DateTimeOffset nowUtc)
    {
        // Lần chạy đầu: khởi tạo trial.
        if (state is null)
        {
            var fresh = new TrialState { FirstSeenUtc = nowUtc, LastSeenUtc = nowUtc, Sequence = 1 };
            return new TrialEvaluation(fresh, LicenseStatus.Trial, DaysLeft(fresh, nowUtc));
        }

        // Đồng hồ quay lùi quá mức cho phép ⇒ nghi ngờ sửa ngày giờ.
        if (nowUtc < state.LastSeenUtc - _skewTolerance)
        {
            return new TrialEvaluation(state, LicenseStatus.Tampered, DaysLeft(state, state.LastSeenUtc));
        }

        var advanced = new TrialState
        {
            FirstSeenUtc = state.FirstSeenUtc,
            LastSeenUtc = nowUtc > state.LastSeenUtc ? nowUtc : state.LastSeenUtc,
            Sequence = state.Sequence + 1,
        };

        var endsUtc = advanced.FirstSeenUtc.AddDays(_trialDays);
        if (nowUtc >= endsUtc)
        {
            return new TrialEvaluation(advanced, LicenseStatus.TrialExpired, 0);
        }

        return new TrialEvaluation(advanced, LicenseStatus.Trial, DaysLeft(advanced, nowUtc));
    }

    private int DaysLeft(TrialState state, DateTimeOffset nowUtc)
    {
        var endsUtc = state.FirstSeenUtc.AddDays(_trialDays);
        var remaining = (int)Math.Ceiling((endsUtc - nowUtc).TotalDays);
        return Math.Max(0, remaining);
    }
}

/// <summary>Kết quả một lần đánh giá trial: state mới + trạng thái + số ngày còn lại.</summary>
public sealed record TrialEvaluation(TrialState NewState, LicenseStatus Status, int DaysRemaining);
