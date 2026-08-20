using System.Text;
using CccdCheckIn.Core.Licensing;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class ActivationCodeCodecTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("{\"v\":1}");
    private static readonly byte[] Signature = [0x30, 0x45, 0x02, 0x21, 0xFF];

    [Fact]
    public void Encode_ThenTryDecode_RoundTrips()
    {
        var code = ActivationCodeCodec.Encode(Payload, Signature);
        Assert.True(ActivationCodeCodec.TryDecode(code, out var payload, out var signature));
        Assert.Equal(Payload, payload);
        Assert.Equal(Signature, signature);
    }

    [Fact]
    public void Encode_UsesBase64Url_NoPaddingNoSlashPlus()
    {
        var code = ActivationCodeCodec.Encode(Payload, Signature);
        Assert.DoesNotContain("=", code);
        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
    }

    [Fact]
    public void Encode_ResultCopyPasteFriendly_WhitespaceTrimmedOnDecode()
    {
        var code = ActivationCodeCodec.Encode(Payload, Signature);
        Assert.True(ActivationCodeCodec.TryDecode($"  {code}\n", out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-dot-separator")]
    [InlineData(".missing-payload")]
    [InlineData("missing-signature.")]
    [InlineData("!!!.###")] // không phải base64
    public void TryDecode_MalformedInput_ReturnsFalse(string input)
    {
        Assert.False(ActivationCodeCodec.TryDecode(input, out _, out _));
    }

    [Fact]
    public void Base64UrlTryDecode_SingleCharPaddingLength_ReturnsFalse()
    {
        // Độ dài %4 == 1 không hợp lệ với base64.
        Assert.False(ActivationCodeCodec.Base64UrlTryDecode("abcde", out _));
    }
}
