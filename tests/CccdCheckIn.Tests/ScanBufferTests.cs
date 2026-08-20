using System.Text;
using CccdCheckIn.Reader.Serial;
using Xunit;

namespace CccdCheckIn.Tests;

/// <summary>
/// ScanBuffer là điểm nhạy nhất của đường ống serial: máy quét CCCD có thể kết thúc
/// chuỗi bằng "\r\n", "\n" hoặc CHỈ "\r" (máy của khách trả về "\r" đơn — bản cũ chỉ
/// chờ "\n" nên treo buffer, dashboard không bao giờ hiện lượt quét). Bộ test này khóa
/// hành vi đó lại + đảm bảo tiếng Việt UTF-8 nguyên vẹn.
/// </summary>
public class ScanBufferTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Append_LfTerminated_EmitsLine()
    {
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes("ABC\n"));
        var one = Assert.Single(lines);
        Assert.Equal("ABC", one);
    }

    [Fact]
    public void Append_CrOnlyTerminated_EmitsLine()
    {
        // Regression: máy quét của khách kết thúc bằng "\r" đơn — không được treo buffer.
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes("079094012307|Lưu Thanh Tuấn|26091994\r"));
        var one = Assert.Single(lines);
        Assert.Equal("079094012307|Lưu Thanh Tuấn|26091994", one);
    }

    [Fact]
    public void Append_CrLfTerminated_EmitsSingleLine()
    {
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes("ABC\r\n"));
        var one = Assert.Single(lines);
        Assert.Equal("ABC", one);
    }

    [Fact]
    public void Append_BareCrLf_DoesNotEmitEmptyLine()
    {
        var buf = new ScanBuffer();
        var first = buf.Append(Bytes("ABC\r\n"));
        Assert.Equal("ABC", Assert.Single(first));
        // "\r\n" thừa sau dòng đã chốt không được sinh dòng rỗng.
        Assert.Empty(buf.Append(Bytes("\r\n")));
    }

    [Fact]
    public void Append_PayloadSplitAcrossChunks_EmitsWhenTerminatorArrives()
    {
        var buf = new ScanBuffer();
        Assert.Empty(buf.Append(Bytes("079094")));
        Assert.Empty(buf.Append(Bytes("012307|Nam")));
        var lines = buf.Append(Bytes("|Hà Nội\r"));
        var one = Assert.Single(lines);
        Assert.Equal("079094012307|Nam|Hà Nội", one);
    }

    [Fact]
    public void Append_Utf8Vietnamese_RoundTripsWithDiacritics()
    {
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes("Nguyễn Gia Mạnh|TP Sóc Trăng\r\n"));
        Assert.Equal("Nguyễn Gia Mạnh|TP Sóc Trăng", Assert.Single(lines));
    }

    [Fact]
    public void Append_MultipleLinesInOneChunk_EmitsAll()
    {
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes("ONE\rTWO\nTHREE\r\n"));
        Assert.Equal(new[] { "ONE", "TWO", "THREE" }, lines);
    }

    [Fact]
    public void Append_InvalidUtf8_FallsBackToWindows1252()
    {
        // 0xC0 = 'À' trong Windows-1252; đứng riêng là byte UTF-8 không hợp lệ.
        var buf = new ScanBuffer();
        var lines = buf.Append(new byte[] { 0x41, 0xC0, 0x0D });   // "AÀ" + CR
        Assert.Equal("AÀ", Assert.Single(lines));
    }

    [Fact]
    public void Append_CccdPayloadWithoutTerminator_EmitsLine()
    {
        // Regression: máy quét 1 số khách gửi payload QR không kèm \r\n — không được treo buffer.
        var payload = "094083123451|3099789364|Nguyễn Gia Mạnh|21031983|Nam|165/7 Nguyễn Thị Minh Khai, TP Hồ Chí Minh|29112021";
        var buf = new ScanBuffer();
        var lines = buf.Append(Bytes(payload));   // không có CR/LF
        var one = Assert.Single(lines);
        Assert.Equal(payload, one);
    }

    [Fact]
    public void Append_PartialCccd_NotEmitted_UntilComplete()
    {
        // Chuỗi chưa đủ 6 dấu '|' / chưa kết thúc bằng 8 chữ số → không chốt dòng.
        var buf = new ScanBuffer();
        Assert.Empty(buf.Append(Bytes("094083123451|3099789364|Nguyễn")));
        Assert.Empty(buf.Append(Bytes(" Gia Mạnh|21031983|Nam|Địa chỉ")));   // 5 '|', chưa đủ
        var lines = buf.Append(Bytes("|29112021"));    // đủ 6 '|' + 8 số
        Assert.Equal("094083123451|3099789364|Nguyễn Gia Mạnh|21031983|Nam|Địa chỉ|29112021", Assert.Single(lines));
    }

    [Fact]
    public void Append_OverlongGarbage_TrimsToMaxBufferBytes()
    {
        var buf = new ScanBuffer { MaxBufferBytes = 16 };
        buf.Append(Bytes(new string('X', 100)));   // không có ký tự kết thúc
        var lines = buf.Append(Bytes("\r"));
        Assert.Equal(16, Assert.Single(lines).Length);
    }
}