using CccdCheckIn.Core.Licensing;
using CccdCheckIn.Licensing.Windows;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class WindowsLicenseStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "cccd-tests-" + Guid.NewGuid().ToString("N"), "licensing");

    private readonly WindowsLicenseStore _store;

    public WindowsLicenseStoreTests() => _store = new WindowsLicenseStore(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LoadLicense_NoFile_ReturnsNull()
    {
        Assert.Null(_store.LoadLicense());
    }

    [Fact]
    public void SaveLicense_ThenLoad_RoundTrips()
    {
        var license = new StoredLicense
        {
            ActivationCode = "payload.signature",
            ActivatedAtUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
        };

        _store.SaveLicense(license);
        var loaded = _store.LoadLicense();

        Assert.NotNull(loaded);
        Assert.Equal(license.ActivationCode, loaded!.ActivationCode);
        Assert.Equal(license.ActivatedAtUtc, loaded.ActivatedAtUtc);
    }

    [Fact]
    public void LoadLicense_CorruptFile_ReturnsNullInsteadOfThrowing()
    {
        _store.SaveLicense(new StoredLicense { ActivationCode = "x.y" });
        File.WriteAllText(LicensingPaths.LicenseFile(_root), "not json {{{");

        Assert.Null(_store.LoadLicense());
    }

    [Fact]
    public void SaveTrialState_ThenLoad_RoundTripsThroughDpapi()
    {
        var state = new TrialState
        {
            FirstSeenUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
            LastSeenUtc = new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero),
            Sequence = 7,
        };

        _store.SaveTrialState(state);
        var loaded = _store.LoadTrialState();

        Assert.NotNull(loaded);
        Assert.Equal(state.FirstSeenUtc, loaded!.FirstSeenUtc);
        Assert.Equal(state.LastSeenUtc, loaded.LastSeenUtc);
        Assert.Equal(state.Sequence, loaded.Sequence);
    }

    [Fact]
    public void TrialStateFile_IsNotPlaintext_DpapiApplied()
    {
        var state = new TrialState
        {
            FirstSeenUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
            LastSeenUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
            Sequence = 1,
        };

        _store.SaveTrialState(state);

        var raw = File.ReadAllText(LicensingPaths.TrialStateFile(_root));
        Assert.DoesNotContain("FirstSeenUtc", raw); // đã mã hóa, không đọc được plaintext
    }

    [Fact]
    public void SaveTrialState_WritesPlaintextMarker_WithFirstSeen()
    {
        var firstSeen = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        _store.SaveTrialState(new TrialState
        {
            FirstSeenUtc = firstSeen,
            LastSeenUtc = firstSeen,
            Sequence = 1,
        });

        var marker = _store.LoadTrialMarker();
        Assert.NotNull(marker);
        Assert.Equal(firstSeen, marker!.Value);
    }

    [Fact]
    public void SaveLicense_IsAtomic_NoTmpFileLeftBehind()
    {
        _store.SaveLicense(new StoredLicense { ActivationCode = "a.b" });

        Assert.False(File.Exists(LicensingPaths.LicenseFile(_root) + ".tmp"));
    }
}
