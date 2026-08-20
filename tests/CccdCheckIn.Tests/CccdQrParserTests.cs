using CccdCheckIn.Core.Parsing;
using Xunit;

namespace CccdCheckIn.Tests;

public class CccdQrParserTests
{
    private readonly CccdQrParser _parser = new();

    // 2 chuỗi mẫu QR CCCD THẬT đã xác nhận (forum giaiphapexcel + repo hocbanchat).
    private const string Standard7 = "094083123451|3099789364|Nguyễn Gia Mạnh|21031983|Nam|165/7, Nguyễn Thị Minh Khai, Khóm 2, Phường 9, TP Sóc Trăng|06042021";
    private const string Zalo6 = "094083123451|Nguyễn Gia Mạnh|21/03/1983|Nam|165/7, Nguyễn Thị Minh Khai, Khóm 2, Phường 9, TP Sóc Trăng|06/04/2021";

    [Fact]
    public void Parse_Standard7Fields_AllFieldsCorrect()
    {
        var r = _parser.Parse(Standard7);

        Assert.True(r.IsValid);
        Assert.Null(r.ErrorMessage);
        var c = r.Citizen!;
        Assert.Equal("094083123451", c.CccdNumber);
        Assert.Equal("3099789364", c.OldIdNumber);
        Assert.Equal("Nguyễn Gia Mạnh", c.FullName);
        Assert.Equal(new DateTime(1983, 3, 21), c.DateOfBirth);
        Assert.Equal("Nam", c.Gender);
        Assert.Contains("165/7, Nguyễn Thị Minh Khai", c.Address);
        Assert.Equal(new DateTime(2021, 4, 6), c.IssueDate);
    }

    [Fact]
    public void Parse_Zalo6Fields_AllFieldsCorrect()
    {
        var r = _parser.Parse(Zalo6);

        Assert.True(r.IsValid);
        var c = r.Citizen!;
        Assert.Equal("094083123451", c.CccdNumber);
        Assert.Equal("", c.OldIdNumber);
        Assert.Equal("Nguyễn Gia Mạnh", c.FullName);
        Assert.Equal(new DateTime(1983, 3, 21), c.DateOfBirth);   // dd/MM/yyyy
        Assert.Equal("Nam", c.Gender);
        Assert.Equal(new DateTime(2021, 4, 6), c.IssueDate);       // dd/MM/yyyy
        Assert.Contains(r.Warnings, w => w.Contains("Zalo", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("001158024485|110419092|Đinh Thị Huyền|08101958|Nữ|Khánh Vân, Khánh Hà, Thường Tín, Hà Nội|24042021")]
    [InlineData("033123009876|0123456789|Trần Thị Bích|15081990|Nữ|Hà Nội|15082016")]
    public void Parse_StandardVariants_Valid(string payload)
    {
        var r = _parser.Parse(payload);
        Assert.True(r.IsValid, r.ErrorMessage);
    }

    [Fact]
    public void Parse_EmptyOldIdNumber_KeptAsEmpty()
    {
        // "||" giữa CCCD và HọTên → CMND cũ trống.
        var payload = "094083123451||Nguyễn Gia Mạnh|21031983|Nam|Địa chỉ|06042021";
        var r = _parser.Parse(payload);
        Assert.True(r.IsValid);
        Assert.Equal("", r.Citizen!.OldIdNumber);
    }

    [Fact]
    public void Parse_WithBomAndTrailingNewline_Strips()
    {
        var payload = "﻿" + Standard7 + "\r\n";
        var r = _parser.Parse(payload);
        Assert.True(r.IsValid);
        Assert.Equal("094083123451", r.Citizen!.CccdNumber);
    }

    [Fact]
    public void Parse_CccdNumberTooShort_InvalidWithMessage()
    {
        var r = _parser.Parse("09408312345|3099789364|Nguyễn|21031983|Nam|Địa chỉ|06042021");   // 11 số
        Assert.False(r.IsValid);
        Assert.Contains("12 chữ số", r.ErrorMessage);
    }

    [Fact]
    public void Parse_CccdContainsLetters_Invalid()
    {
        var r = _parser.Parse("0940831234A|3099789364|Nguyễn|21031983|Nam|Địa chỉ|06042021");
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Parse_InvalidDate_ValidButWarning()
    {
        var r = _parser.Parse("094083123451|3099789364|Nguyễn Gia Mạnh|99999999|Nam|Địa chỉ|06042021");
        Assert.True(r.IsValid);                              // không chặn, chỉ cảnh báo
        Assert.Contains(r.Warnings, w => w.Contains("Ngày sinh", StringComparison.Ordinal));
        Assert.Equal(default, r.Citizen!.DateOfBirth);
    }

    [Fact]
    public void Parse_InvalidIssueDate_ValidButWarning()
    {
        var r = _parser.Parse("094083123451|3099789364|Nguyễn|21031983|Nam|Địa chỉ|31/02/2021");
        Assert.True(r.IsValid);
        Assert.Contains(r.Warnings, w => w.Contains("Ngày cấp", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_FutureDateOfBirth_Warnings()
    {
        var r = _parser.Parse("094083123451|3099789364|Nguyễn|30082099|Nam|Địa chỉ|06042021");
        Assert.True(r.IsValid);
        Assert.Contains(r.Warnings, w => w.Contains("lớn hơn hôm nay", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_UnusualGender_WarningKeepsValue()
    {
        var r = _parser.Parse("094083123451|3099789364|Nguyễn|21031983|Khác|Địa chỉ|06042021");
        Assert.True(r.IsValid);
        Assert.Equal("Khác", r.Citizen!.Gender);
        Assert.Contains(r.Warnings, w => w.Contains("Giới tính", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_TooManyFields_Invalid()
    {
        var r = _parser.Parse("a|b|c|d|e|f|g|h");
        Assert.False(r.IsValid);
        Assert.Contains("7 trường", r.ErrorMessage);
    }

    [Fact]
    public void Parse_Garbage_Invalid()
    {
        var r = _parser.Parse("abc def ghi");
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Parse_Empty_Invalid()
    {
        Assert.False(_parser.Parse("").IsValid);
        Assert.False(_parser.Parse("   ").IsValid);
        Assert.False(_parser.Parse("\r\n").IsValid);
    }

    [Fact]
    public void Parse_EmptyName_ValidButWarns()
    {
        var r = _parser.Parse("094083123451|3099789364||21031983|Nam|Địa chỉ|06042021");
        Assert.True(r.IsValid);
        Assert.Contains(r.Warnings, w => w.Contains("họ tên", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_CccdNumberWithSpacesAround_Trims()
    {
        var r = _parser.Parse(" 094083123451 |3099789364|Nguyễn|21031983|Nam|Địa chỉ|06042021");
        Assert.True(r.IsValid);
        Assert.Equal("094083123451", r.Citizen!.CccdNumber);
    }
}