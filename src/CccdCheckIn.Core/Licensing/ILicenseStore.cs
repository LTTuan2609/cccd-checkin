namespace CccdCheckIn.Core.Licensing;

/// <summary>Đọc/ghi license + trial state trên máy khách.</summary>
public interface ILicenseStore
{
    StoredLicense? LoadLicense();

    /// <summary>Lưu atomic (write tmp → replace); activation lỗi không được overwrite license cũ.</summary>
    void SaveLicense(StoredLicense license);

    TrialState? LoadTrialState();

    void SaveTrialState(TrialState state);
}
