namespace CccdCheckIn.Core.Licensing;

/// <summary>Map kết quả license → quyền thao tác UI/pipeline.</summary>
public sealed class LicenseGate : ILicenseGate
{
    public LicenseGate(LicenseValidationResult result) => LastResult = result;

    public LicenseMode Mode => LastResult.Mode;

    public LicenseValidationResult LastResult { get; private set; }

    public bool CanRead => true;

    public bool CanExport => true;

    public bool CanWrite => LastResult.Mode is LicenseMode.Active or LicenseMode.Trial;

    public bool CanCheckIn => LastResult.Mode is LicenseMode.Active or LicenseMode.Trial;

    public void Update(LicenseValidationResult result) => LastResult = result;
}
