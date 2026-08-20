using CccdCheckIn.Core.Licensing;
using CccdCheckIn.Licensing.Windows;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class TrialClockTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

    private static readonly TrialClock Clock = new();

    [Fact]
    public void Evaluate_NoState_InitializesTrialWithFullDays()
    {
        var result = Clock.Evaluate(null, Start);

        Assert.Equal(LicenseStatus.Trial, result.Status);
        Assert.Equal(14, result.DaysRemaining);
        Assert.Equal(Start, result.NewState.FirstSeenUtc);
        Assert.Equal(1, result.NewState.Sequence);
    }

    [Fact]
    public void Evaluate_Day10_StillTrialWithDaysLeft()
    {
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start, Sequence = 5 };
        var now = Start.AddDays(10);

        var result = Clock.Evaluate(state, now);

        Assert.Equal(LicenseStatus.Trial, result.Status);
        Assert.Equal(4, result.DaysRemaining);
        Assert.Equal(now, result.NewState.LastSeenUtc);
        Assert.Equal(6, result.NewState.Sequence);
    }

    [Fact]
    public void Evaluate_After14Days_TrialExpired()
    {
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start, Sequence = 5 };

        var result = Clock.Evaluate(state, Start.AddDays(14));

        Assert.Equal(LicenseStatus.TrialExpired, result.Status);
        Assert.Equal(0, result.DaysRemaining);
    }

    [Fact]
    public void Evaluate_ClockRollbackBeyondSkew_Tampered()
    {
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start.AddDays(5), Sequence = 9 };

        // Quay lùi 1 ngày (quá mức skew 5 phút) ⇒ nghi ngờ chỉnh đồng hồ.
        var result = Clock.Evaluate(state, Start.AddDays(4));

        Assert.Equal(LicenseStatus.Tampered, result.Status);
    }

    [Fact]
    public void Evaluate_SmallSkewWithinTolerance_NotTampered()
    {
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start.AddDays(5), Sequence = 9 };

        // Lệch 2 phút — trong mức cho phép.
        var result = Clock.Evaluate(state, Start.AddDays(5).AddMinutes(-2));

        Assert.Equal(LicenseStatus.Trial, result.Status);
    }

    [Fact]
    public void Evaluate_LastSeenNeverMovesBackward()
    {
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start.AddDays(5), Sequence = 9 };

        var result = Clock.Evaluate(state, Start.AddDays(3).AddHours(1));

        Assert.Equal(Start.AddDays(5), result.NewState.LastSeenUtc);
    }

    [Fact]
    public void Evaluate_CustomTrialDays_Respected()
    {
        var shortClock = new TrialClock(trialDays: 3);
        var state = new TrialState { FirstSeenUtc = Start, LastSeenUtc = Start, Sequence = 1 };

        var result = shortClock.Evaluate(state, Start.AddDays(3));

        Assert.Equal(LicenseStatus.TrialExpired, result.Status);
    }
}
