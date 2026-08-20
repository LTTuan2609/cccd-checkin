using System.Security.Cryptography;
using System.Text;
using CccdCheckIn.Core.Licensing;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class EcdsaLicenseVerifierTests
{
    private static readonly byte[] PayloadBytes =
        Encoding.UTF8.GetBytes("{\"v\":1,\"product\":\"CccdCheckIn\"}");

    private static (EcdsaLicenseVerifier verifier, ECDsa privateKey) CreatePair()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        return (new EcdsaLicenseVerifier(publicBase64), key);
    }

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsTrue()
    {
        var (verifier, key) = CreatePair();
        var signature = key.SignData(PayloadBytes, HashAlgorithmName.SHA256);
        Assert.True(verifier.VerifySignature(PayloadBytes, signature));
    }

    [Fact]
    public void VerifySignature_TamperedPayload_ReturnsFalse()
    {
        var (verifier, key) = CreatePair();
        var signature = key.SignData(PayloadBytes, HashAlgorithmName.SHA256);
        var tampered = (byte[])PayloadBytes.Clone();
        tampered[^1] ^= 0x01;
        Assert.False(verifier.VerifySignature(tampered, signature));
    }

    [Fact]
    public void VerifySignature_TamperedSignature_ReturnsFalse()
    {
        var (verifier, key) = CreatePair();
        var signature = key.SignData(PayloadBytes, HashAlgorithmName.SHA256);
        signature[^1] ^= 0x01;
        Assert.False(verifier.VerifySignature(PayloadBytes, signature));
    }

    [Fact]
    public void VerifySignature_SignedByDifferentKey_ReturnsFalse()
    {
        var (verifier, _) = CreatePair();
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = otherKey.SignData(PayloadBytes, HashAlgorithmName.SHA256);
        Assert.False(verifier.VerifySignature(PayloadBytes, signature));
    }
}
