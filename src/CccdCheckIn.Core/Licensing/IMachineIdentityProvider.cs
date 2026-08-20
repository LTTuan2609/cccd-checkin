namespace CccdCheckIn.Core.Licensing;

/// <summary>Lấy (hoặc tạo lần đầu) danh tính máy — keypair P-256 bảo vệ bằng DPAPI.</summary>
public interface IMachineIdentityProvider
{
    Task<MachineIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default);
}
