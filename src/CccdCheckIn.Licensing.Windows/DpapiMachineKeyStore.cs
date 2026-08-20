using System.Runtime.Versioning;
using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Sinh/lưu keypair P-256 của máy. Private key bảo vệ bằng DPAPI (CurrentUser) —
/// copy file sang máy khác không giải mã được. License bind vào hash của public key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiMachineKeyStore : IMachineIdentityProvider
{
    private readonly string _keyFile;

    public DpapiMachineKeyStore(string? rootOverride = null)
    {
        _keyFile = LicensingPaths.MachineKeyFile(LicensingPaths.GetRoot(rootOverride));
    }

    public Task<MachineIdentity> GetOrCreateAsync(CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            using var key = LoadOrCreateKey();
            var publicKey = key.ExportSubjectPublicKeyInfo();
            var publicKeyBase64Url = ActivationCodeCodec.Base64UrlEncode(publicKey);
            var hash = SHA256.HashData(publicKey);

            return new MachineIdentity
            {
                PublicKeyBase64Url = publicKeyBase64Url,
                MachineKeyHash = ActivationCodeCodec.Base64UrlEncode(hash),
                MachineCode = FormatMachineCode(hash),
            };
        }, cancellationToken);

    /// <summary>Mã Máy hiển thị cho khách: LT-XXXX-XXXX-XXXX-XXXX (16 hex đầu của hash).</summary>
    internal static string FormatMachineCode(byte[] publicKeyHash)
    {
        var hex = Convert.ToHexString(publicKeyHash)[..16];
        return $"LT-{hex[0..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    private ECDsa LoadOrCreateKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(_keyFile))
        {
            var encrypted = File.ReadAllBytes(_keyFile);
            var pkcs8 = ProtectedData.Unprotect(encrypted, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            key.ImportPkcs8PrivateKey(pkcs8, out _);
            CryptographicOperations.ZeroMemory(pkcs8);
            return key;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_keyFile)!);
        var newPkcs8 = key.ExportPkcs8PrivateKey();
        var newEncrypted = ProtectedData.Protect(newPkcs8, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(newPkcs8);
        WriteAtomic(_keyFile, newEncrypted);
        return key;
    }

    internal static void WriteAtomic(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }
}
