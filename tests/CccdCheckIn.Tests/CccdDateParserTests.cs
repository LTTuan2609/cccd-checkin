using CccdCheckIn.Core.Parsing;
using Xunit;

namespace CccdCheckIn.Tests;

public class CccdDateParserTests
{
    [Theory]
    [InlineData("21031983", 1983, 3, 21)]    // ddMMyyyy (chuẩn)
    [InlineData("21/03/1983", 1983, 3, 21)] // dd/MM/yyyy (Zalo)
    [InlineData("21-03-1983", 1983, 3, 21)] // dd-MM-yyyy (dự phòng)
    public void Parse_ValidFormats_ReturnsDateTime(string input, int y, int m, int d)
    {
        var result = CccdDateParser.Parse(input);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(y, m, d), result!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ReturnsNull(string? input)
        => Assert.Null(CccdDateParser.Parse(input));

    [Theory]
    [InlineData("99999999")]   // không hợp lệ
    [InlineData("32/13/2021")] // 32/13 — không tồn tại
    [InlineData("1983-03-21")] // sai thứ tự
    [InlineData("abc")]        // không phải số
    [InlineData("2103198")]    // thiếu số
    public void Parse_Invalid_ReturnsNull(string input)
        => Assert.Null(CccdDateParser.Parse(input));
}