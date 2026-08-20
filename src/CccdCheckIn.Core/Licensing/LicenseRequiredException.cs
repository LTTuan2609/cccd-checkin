namespace CccdCheckIn.Core.Licensing;

/// <summary>Ném khi thao tác ghi bị chặn do license hết hạn/chưa kích hoạt.</summary>
public sealed class LicenseRequiredException : Exception
{
    public LicenseRequiredException()
        : base("License không hợp lệ hoặc đã hết hạn. Vui lòng gia hạn để tiếp tục.")
    {
    }

    public LicenseRequiredException(string message) : base(message)
    {
    }
}
