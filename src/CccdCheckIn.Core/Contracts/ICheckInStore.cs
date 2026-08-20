namespace CccdCheckIn.Core.Contracts;

using CccdCheckIn.Core.Models;

/// <summary>
/// Nơi lưu log check-in. Implement mới (CSV, SQL Server, HTTP API, …) chỉ cần
/// một class mới implement interface này cho store hiện tại.
/// </summary>
public interface ICheckInStore
{
    Task<CheckInRecord> AddCheckInAsync(CitizenInfo citizen, DateTime checkedInAt);

    /// <summary>N bản ghi gần nhất, giảm dần theo thời gian check-in.</summary>
    Task<IReadOnlyList<CheckInRecord>> GetRecentAsync(int count);

    Task<int> GetCountAsync();

    /// <summary>Số lượt check-in từ mốc thời gian (dùng cho dashboard "hôm nay").</summary>
    Task<int> GetCountAsync(DateTime from);

    /// <summary>Tất cả bản ghi check-in từ mốc thời gian, giảm dần (dùng cho xuất CSV "hôm nay").</summary>
    Task<IReadOnlyList<CheckInRecord>> GetWhereAtLeastAsync(DateTime from);
}