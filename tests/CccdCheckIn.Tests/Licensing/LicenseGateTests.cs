using CccdCheckIn.Core.Licensing;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class LicenseGateTests
{
    private static LicenseValidationResult Result(LicenseStatus status, LicenseMode mode) => new()
    {
        Status = status,
        Mode = mode,
    };

    [Theory]
    [InlineData(LicenseStatus.Valid, LicenseMode.Active)]
    [InlineData(LicenseStatus.Trial, LicenseMode.Trial)]
    public void Gate_ActiveOrTrial_AllowsAllOperations(LicenseStatus status, LicenseMode mode)
    {
        var gate = new LicenseGate(Result(status, mode));
        Assert.True(gate.CanRead);
        Assert.True(gate.CanExport);
        Assert.True(gate.CanWrite);
        Assert.True(gate.CanCheckIn);
    }

    [Theory]
    [InlineData(LicenseStatus.Expired)]
    [InlineData(LicenseStatus.TrialExpired)]
    [InlineData(LicenseStatus.Missing)]
    [InlineData(LicenseStatus.MachineMismatch)]
    [InlineData(LicenseStatus.InvalidSignature)]
    [InlineData(LicenseStatus.Malformed)]
    [InlineData(LicenseStatus.Tampered)]
    [InlineData(LicenseStatus.NotYetValid)]
    public void Gate_AnyNonUsableStatus_ReadExportOnly_DataNeverLocked(LicenseStatus status)
    {
        // Invariant quan trọng nhất: hết hạn/không hợp lệ vẫn đọc + export được,
        // chỉ chặn ghi — dữ liệu khách hàng không bao giờ bị khóa.
        var gate = new LicenseGate(Result(status, LicenseMode.ReadExportOnly));
        Assert.True(gate.CanRead);
        Assert.True(gate.CanExport);
        Assert.False(gate.CanWrite);
        Assert.False(gate.CanCheckIn);
    }

    [Fact]
    public void Update_AfterActivation_EnablesWrite()
    {
        var gate = new LicenseGate(Result(LicenseStatus.Missing, LicenseMode.ReadExportOnly));
        Assert.False(gate.CanCheckIn);

        gate.Update(Result(LicenseStatus.Valid, LicenseMode.Active));

        Assert.Equal(LicenseMode.Active, gate.Mode);
        Assert.True(gate.CanWrite);
        Assert.True(gate.CanCheckIn);
    }
}
