using System.Text;

namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Mã Kích Hoạt dạng: base64url(canonical payload) + "." + base64url(chữ ký).
/// Base64Url thay vì Base64 thường để mã gọn, an toàn khi copy/paste.
/// </summary>
public static class ActivationCodeCodec
{
    public static string Encode(byte[] canonicalPayload, byte[] signature)
        => $"{Base64UrlEncode(canonicalPayload)}.{Base64UrlEncode(signature)}";

    /// <summary>Giải mã; trả false nếu sai format (không throw).</summary>
    public static bool TryDecode(string activationCode, out byte[] canonicalPayload, out byte[] signature)
    {
        canonicalPayload = [];
        signature = [];
        if (string.IsNullOrWhiteSpace(activationCode)) return false;

        var trimmed = activationCode.Trim();
        var idx = trimmed.IndexOf('.');
        if (idx <= 0 || idx >= trimmed.Length - 1) return false;

        if (!Base64UrlTryDecode(trimmed[..idx], out canonicalPayload)) return false;
        if (!Base64UrlTryDecode(trimmed[(idx + 1)..], out signature)) return false;
        return canonicalPayload.Length > 0 && signature.Length > 0;
    }

    public static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool Base64UrlTryDecode(string value, out byte[] data)
    {
        data = [];
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
                case 1: return false;
            }
            data = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
