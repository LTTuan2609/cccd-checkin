using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Đọc/ghi license + trial state dưới ProgramData (hoặc root override cho test).
/// license.dat: JSON plaintext (đã ký nên không cần DPAPI). Ghi atomic — activation lỗi
/// không bao giờ overwrite license cũ.
/// trial.dat: JSON bảo vệ bằng DPAPI CurrentUser; trial.marker: plaintext để đối chiếu.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsLicenseStore : ILicenseStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _root;

    public WindowsLicenseStore(string? rootOverride = null)
        => _root = LicensingPaths.GetRoot(rootOverride);

    public StoredLicense? LoadLicense()
    {
        var path = LicensingPaths.LicenseFile(_root);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<StoredLicenseDto>(File.ReadAllText(path), JsonOptions)?.ToModel();
        }
        catch (JsonException)
        {
            return null; // file hỏng — coi như chưa có license; không crash app.
        }
    }

    public void SaveLicense(StoredLicense license)
    {
        var dto = new StoredLicenseDto
        {
            ActivationCode = license.ActivationCode,
            ActivatedAtUtc = license.ActivatedAtUtc,
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        WriteAtomic(LicensingPaths.LicenseFile(_root), Encoding.UTF8.GetBytes(json));
    }

    public TrialState? LoadTrialState()
    {
        var path = LicensingPaths.TrialStateFile(_root);
        if (!File.Exists(path)) return null;
        try
        {
            var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(path), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            var dto = JsonSerializer.Deserialize<TrialStateDto>(decrypted, JsonOptions);
            return dto?.ToModel();
        }
        catch (CryptographicException)
        {
            return null; // DPAPI không giải mã được (copy từ máy khác) — coi như chưa có.
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void SaveTrialState(TrialState state)
    {
        var dto = TrialStateDto.FromModel(state);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        WriteAtomic(LicensingPaths.TrialStateFile(_root), encrypted);

        // Marker plaintext: ngày bắt đầu trial — để đối chiếu phát hiện sửa trial.dat.
        WriteAtomic(LicensingPaths.TrialMarkerFile(_root),
            Encoding.UTF8.GetBytes(state.FirstSeenUtc.UtcDateTime.ToString("O")));
    }

    /// <summary>Đọc firstSeen từ marker plaintext; null nếu thiếu/hỏng.</summary>
    public DateTimeOffset? LoadTrialMarker()
    {
        var path = LicensingPaths.TrialMarkerFile(_root);
        if (!File.Exists(path)) return null;
        return DateTimeOffset.TryParse(File.ReadAllText(path).Trim(),
            out var value) ? value : null;
    }

    private static void WriteAtomic(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed class StoredLicenseDto
    {
        public string ActivationCode { get; init; } = string.Empty;

        public DateTimeOffset ActivatedAtUtc { get; init; }

        public StoredLicense ToModel() => new()
        {
            ActivationCode = ActivationCode,
            ActivatedAtUtc = ActivatedAtUtc,
        };
    }

    private sealed class TrialStateDto
    {
        public DateTimeOffset FirstSeenUtc { get; init; }

        public DateTimeOffset LastSeenUtc { get; init; }

        public long Sequence { get; init; }

        public int SchemaVersion { get; init; } = 1;

        public TrialState ToModel() => new()
        {
            FirstSeenUtc = FirstSeenUtc,
            LastSeenUtc = LastSeenUtc,
            Sequence = Sequence,
            SchemaVersion = SchemaVersion,
        };

        public static TrialStateDto FromModel(TrialState state) => new()
        {
            FirstSeenUtc = state.FirstSeenUtc,
            LastSeenUtc = state.LastSeenUtc,
            Sequence = state.Sequence,
            SchemaVersion = state.SchemaVersion,
        };
    }
}
