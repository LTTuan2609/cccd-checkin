namespace CccdCheckIn.Storage.Sqlite;

/// <summary>
/// Model giàu dữ liệu của section "Store" (SaveFields + SqlitePath).
/// Là model thuần, không phụ thuộc Microsoft.Extensions.* — được build từ appsettings.json bởi AppConfigFactory.
/// </summary>
public sealed class SqliteStorageConfig
{
    public const string SectionName = "Store";

    public string Type { get; set; } = "Sqlite";
    public string SqlitePath { get; set; } = "Data/checkin.db";
    public List<string> SaveFields { get; set; } =
        [.. SqliteCheckInStore.DefaultSaveFields];
}