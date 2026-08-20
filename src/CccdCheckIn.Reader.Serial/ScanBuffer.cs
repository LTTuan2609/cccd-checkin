namespace CccdCheckIn.Reader.Serial;

using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Gộp byte từ SerialPort thành từng dòng hoàn chỉnh.
/// Máy quét CCCD thường kết thúc mỗi mã QR bằng Enter (\r hoặc \r\n); dữ liệu
/// giữa các lần DataReceived là byte rời → không đọc trực tiếp từng chunk.
/// Xử lý byte thô (chứ không ReadExisting+decode) để không hỏng tiếng Việt.
/// </summary>
public sealed class ScanBuffer
{
    static ScanBuffer()
    {
        // Fallback Windows-1252 cần provider này — .NET (Core) không có sẵn bảng mã đó.
        // Gọi nhiều lần vẫn an toàn (RegisterProvider idempotent).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly List<byte> _buffer = [];

    /// <summary>Số byte tối đa giữ lại khi không thấy dấu kết thúc (chống rò rỉ bộ nhớ trên payload rác).</summary>
    public int MaxBufferBytes { get; set; } = 4096;

    /// <summary>
    /// Máy quét của 1 số khách gửi payload QR KHÔNG kèm \r\n ở cuối → không bao giờ chốt dòng.
    /// Nhận diện payload CCCD hợp lệ: ≥ 6 dấu '|' (7 trường) và kết thúc bằng 8 chữ số (ngày cấp DDMMYYYY).
    /// Khi khớp → chốt dòng ngay (không chờ terminator).
    /// </summary>
    public static bool IsCompleteCccdPayload(ReadOnlySpan<byte> b)
    {
        int bars = 0;
        if (b.Length < 20) return false;
        for (int i = 0; i < b.Length; i++)
            if (b[i] == (byte)'|') bars++;
        if (bars < 6) return false;

        int digits = 0;
        for (int i = b.Length - 1; i >= 0 && digits < 8; i--)
        {
            if (b[i] >= (byte)'0' && b[i] <= (byte)'9') digits++;
            else break;
        }
        return digits >= 8;
    }

    /// <summary>Đẩy một khối byte vào buffer; trả các dòng hoàn chỉnh (không kèm \r\n).</summary>
    public IReadOnlyList<string> Append(ReadOnlySpan<byte> chunk)
    {
        var results = new List<string>();
        foreach (var b in chunk)
        {
            // Cả \r lẫn \n đều kết thúc dòng: máy quét có thể gửi "\r\n", "\r" hoặc "\n"
            // sau chuỗi QR. Chỉ chờ \n (như trước) sẽ treo buffer nếu máy gửi "\r" đơn.
            if (b == (byte)'\n' || b == (byte)'\r')
            {
                var line = PopLine();
                if (line is not null) results.Add(line);
                continue;
            }

            _buffer.Add(b);

            // Máy quét 1 số khách gửi payload KHÔNG kèm \r\n → buffer đầy payload hợp lệ → chốt dòng ngay.
            if (IsCompleteCccdPayload(CollectionsMarshal.AsSpan(_buffer)))
            {
                var line = PopLine();
                if (line is not null) results.Add(line);
                continue;
            }

            if (_buffer.Count > MaxBufferBytes)
            {
                _buffer.RemoveRange(0, _buffer.Count - MaxBufferBytes);
            }
        }
        return results;
    }

    private string? PopLine()
    {
        if (_buffer.Count == 0) return null;
        var data = _buffer.ToArray();
        _buffer.Clear();
        return Decode(data);
    }

    private static string Decode(byte[] data)
    {
        // Bỏ BOM EF BB BF nếu máy quét/cổng chèn vào (một số máy gửi kèm ký tự đầu).
        var span = data.AsSpan();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding("Windows-1252").GetString(span);
        }
    }
}