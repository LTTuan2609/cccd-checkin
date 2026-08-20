using CccdCheckIn.Licensing.Windows;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class DpapiMachineKeyStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "cccd-tests-" + Guid.NewGuid().ToString("N"), "licensing");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void GetOrCreate_FirstRun_CreatesKeyFileAndIdentity()
    {
        var store = new DpapiMachineKeyStore(_root);

        var identity = store.GetOrCreateAsync().GetAwaiter().GetResult();

        Assert.False(string.IsNullOrEmpty(identity.MachineKeyHash));
        Assert.False(string.IsNullOrEmpty(identity.MachineCode));
        Assert.StartsWith("LT-", identity.MachineCode);
        Assert.True(File.Exists(LicensingPaths.MachineKeyFile(_root)));
    }

    [Fact]
    public void GetOrCreate_SecondRun_ReturnsSameIdentity()
    {
        var store = new DpapiMachineKeyStore(_root);
        var first = store.GetOrCreateAsync().GetAwaiter().GetResult();

        var second = new DpapiMachineKeyStore(_root).GetOrCreateAsync().GetAwaiter().GetResult();

        Assert.Equal(first.MachineKeyHash, second.MachineKeyHash);
        Assert.Equal(first.MachineCode, second.MachineCode);
    }

    [Fact]
    public void MachineCode_Format_IsLtDash4x4Hex()
    {
        var store = new DpapiMachineKeyStore(_root);
        var identity = store.GetOrCreateAsync().GetAwaiter().GetResult();

        Assert.Matches("^LT-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$", identity.MachineCode);
    }

    [Fact]
    public void KeyFile_IsDpapiBlob_NotPlaintextKey()
    {
        var store = new DpapiMachineKeyStore(_root);
        store.GetOrCreateAsync().GetAwaiter().GetResult();

        // DPAPI blob bắt đầu bằng 0x01 0x00 0x00 0x00 (DPAPI header magic).
        var blob = File.ReadAllBytes(LicensingPaths.MachineKeyFile(_root));
        Assert.True(blob.Length > 16);
        Assert.Equal(0x01, blob[0]);
    }
}
