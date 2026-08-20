using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;
using CccdCheckIn.Licensing.Windows;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class LicenseServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "cccd-tests-" + Guid.NewGuid().ToString("N"), "licensing");

    private readonly WindowsLicenseStore _store;

    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly FakeMachineIdentity _machine = new();

    private readonly LicenseService _service;

    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    public LicenseServiceTests()
    {
        _store = new WindowsLicenseStore(_root);
        var verifier = new EcdsaLicenseVerifier(
            Convert.ToBase64String(_signingKey.ExportSubjectPublicKeyInfo()));
        _service = new LicenseService(
            _store, _machine, verifier, new LicenseCanonicalizer());
    }

    public void Dispose()
    {
        _signingKey.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Evaluate_NoLicenseNoTrialState_Trial14Days()
    {
        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.Trial, result.Status);
        Assert.Equal(LicenseMode.Trial, result.Mode);
        Assert.Equal(14, result.DaysRemaining);
    }

    [Fact]
    public void Evaluate_ValidLicense_Active()
    {
        StoreSignedLicense(expiresAt: Now.AddDays(30));

        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(LicenseMode.Active, result.Mode);
        Assert.True(result.DaysRemaining >= 29);
    }

    [Fact]
    public void Evaluate_ExpiredLicense_ReadExportOnly()
    {
        StoreSignedLicense(expiresAt: Now.AddDays(-1));

        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.Expired, result.Status);
        Assert.Equal(LicenseMode.ReadExportOnly, result.Mode);
    }

    [Fact]
    public void Evaluate_NotYetValidLicense_ReadExportOnly()
    {
        StoreSignedLicense(notBefore: Now.AddDays(1));

        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.NotYetValid, result.Status);
        Assert.Equal(LicenseMode.ReadExportOnly, result.Mode);
    }

    [Fact]
    public void Evaluate_LicenseSignedForDifferentMachine_MachineMismatch()
    {
        StoreSignedLicense(machineKeyHash: "hash-of-another-machine");

        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
        Assert.Equal(LicenseMode.ReadExportOnly, result.Mode);
    }

    [Fact]
    public void Evaluate_TamperedActivationCode_InvalidSignature()
    {
        StoreSignedLicense();
        var stored = _store.LoadLicense()!;
        // Sửa payload phần đầu (base64url) — chữ ký không còn khớp.
        var tampered = stored.ActivationCode[..^4] + "AAAA";
        _store.SaveLicense(new StoredLicense { ActivationCode = tampered, ActivatedAtUtc = Now });

        var result = _service.Evaluate(Now);

        Assert.True(result.Status is LicenseStatus.InvalidSignature or LicenseStatus.Malformed);
        Assert.Equal(LicenseMode.ReadExportOnly, result.Mode);
    }

    [Fact]
    public void Activate_ValidCode_ActivatesAndPersists()
    {
        var code = SignCode(expiresAt: Now.AddDays(365));

        var result = _service.Activate(code, Now);

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.NotNull(_store.LoadLicense());
    }

    [Fact]
    public void Activate_CodeForWrongMachine_DoesNotOverwriteExistingLicense()
    {
        StoreSignedLicense(expiresAt: Now.AddDays(30));
        var original = _store.LoadLicense()!.ActivationCode;
        var wrongMachineCode = SignCode(machineKeyHash: "other-machine");

        var result = _service.Activate(wrongMachineCode, Now);

        Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
        Assert.Equal(original, _store.LoadLicense()!.ActivationCode);
    }

    [Fact]
    public void Activate_AlreadyExpiredCode_Rejected()
    {
        var code = SignCode(expiresAt: Now.AddDays(-1));

        var result = _service.Activate(code, Now);

        Assert.Equal(LicenseStatus.Expired, result.Status);
        Assert.Null(_store.LoadLicense());
    }

    [Fact]
    public void Activate_MalformedCode_ReturnsMalformed()
    {
        var result = _service.Activate("garbage-no-dot", Now);

        Assert.Equal(LicenseStatus.Malformed, result.Status);
    }

    [Fact]
    public void Activate_FutureDatedCode_RejectedAndNotSaved()
    {
        var code = SignCode(notBefore: Now.AddDays(1));

        var result = _service.Activate(code, Now);

        Assert.Equal(LicenseStatus.NotYetValid, result.Status);
        Assert.Null(_store.LoadLicense());
    }

    [Fact]
    public void Activate_WrongVersionCode_RejectedAndNotSaved()
    {
        var code = SignCode(version: 99);

        var result = _service.Activate(code, Now);

        Assert.Equal(LicenseStatus.Malformed, result.Status);
        Assert.Null(_store.LoadLicense());
    }

    [Fact]
    public void Evaluate_TrialMarkerMismatch_Tampered()
    {
        // Tạo trial hợp lệ trước.
        _service.Evaluate(Now);

        // Giả lập sửa marker plaintext lùi ngày bắt đầu.
        File.WriteAllText(
            LicensingPaths.TrialMarkerFile(_root),
            Now.AddDays(-5).UtcDateTime.ToString("O"));

        var result = _service.Evaluate(Now);

        Assert.Equal(LicenseStatus.Tampered, result.Status);
    }

    private void StoreSignedLicense(
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? notBefore = null,
        string? machineKeyHash = null)
    {
        _store.SaveLicense(new StoredLicense
        {
            ActivationCode = SignCode(expiresAt, notBefore, machineKeyHash),
            ActivatedAtUtc = Now,
        });
    }

    private string SignCode(
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? notBefore = null,
        string? machineKeyHash = null,
        int? version = null)
    {
        var payload = new LicensePayload
        {
            Version = version ?? LicensingDefaults.PayloadVersion,
            Product = LicensingDefaults.Product,
            LicenseId = "TEST-001",
            MachineKeyHash = machineKeyHash ?? _machine.MachineKeyHash,
            Plan = LicensePlan.Yearly,
            IssuedAtUtc = Now,
            NotBeforeUtc = notBefore ?? Now,
            ExpiresAtUtc = expiresAt ?? Now.AddDays(365),
            KeyId = "test-key",
        };
        var canonical = new LicenseCanonicalizer().Canonicalize(payload);
        var signature = _signingKey.SignData(canonical, HashAlgorithmName.SHA256);
        return ActivationCodeCodec.Encode(canonical, signature);
    }

    private sealed class FakeMachineIdentity : IMachineIdentityProvider
    {
        public string MachineKeyHash { get; } = "test-machine-hash";

        public Task<MachineIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new MachineIdentity
            {
                PublicKeyBase64Url = "test-pub",
                MachineKeyHash = MachineKeyHash,
                MachineCode = "LT-0000-0000-0000-0000",
            });
    }
}
