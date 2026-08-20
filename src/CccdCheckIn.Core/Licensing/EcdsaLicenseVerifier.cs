using System.Security.Cryptography;

namespace CccdCheckIn.Core.Licensing;

/// <summary>Verify chữ ký ECDSA P-256 / SHA-256 bằng public key nhúng trong app.</summary>
public sealed class EcdsaLicenseVerifier : ILicenseVerifier
{
    private readonly ECDsa _publicKey;

    /// <param name="publicKeyBase64">Public key P-256 dạng SubjectPublicKeyInfo (base64), nhúng trong app.</param>
    public EcdsaLicenseVerifier(string publicKeyBase64)
    {
        _publicKey = ECDsa.Create();
        _publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
    }

    public bool VerifySignature(ReadOnlySpan<byte> canonicalPayload, ReadOnlySpan<byte> signature)
        => _publicKey.VerifyData(canonicalPayload, signature, HashAlgorithmName.SHA256);
}
