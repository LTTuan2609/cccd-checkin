using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

/// <summary>
/// Round-trip: issuer dụng private key ký → app verify bằng public key (EmbeddedKeys).
/// </summary>
public class LicenseIssuerRoundTripTests
{
    [Fact]
    public void SignedLicense_ByIssuerKey_VerifiesWithAppPublicKey()
    {
        using var appKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Giả lập app nhúng public key của issuer (EmbeddedKeys).
        var verifier = new EcdsaLicenseVerifier(
            Convert.ToBase64String(issuerKey.ExportSubjectPublicKeyInfo()));

        var nowUtc = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var payload = new LicensePayload
        {
            Version = 1,
            Product = "CccdCheckIn",
            LicenseId = "LIC-TEST",
            MachineKeyHash = "machine-1",
            Plan = LicensePlan.Yearly,
            IssuedAtUtc = nowUtc,
            NotBeforeUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddDays(365),
            KeyId = "prod-2026-01",
        };
        var canonical = new LicenseCanonicalizer().Canonicalize(payload);
        var signature = issuerKey.SignData(canonical, HashAlgorithmName.SHA256);
        var code = ActivationCodeCodec.Encode(canonical, signature);

        Assert.True(ActivationCodeCodec.TryDecode(code, out var payloadBytes, out var sigBytes));
        Assert.True(verifier.VerifySignature(payloadBytes, sigBytes));
        var parsed = new LicenseCanonicalizer().Parse(payloadBytes);
        Assert.NotNull(parsed);
        Assert.Equal("machine-1", parsed!.MachineKeyHash);
        Assert.Equal(LicensePlan.Yearly, parsed.Plan);
    }
}