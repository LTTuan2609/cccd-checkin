using CccdCheckIn.Core.Contracts;
using CccdCheckIn.Core.Models;

namespace CccdCheckIn.Core.Parsing;

/// <summary>
/// Parser chuỗi QR CCCD. Định dạng (xác nhận từ 2 nguồn độc lập — forum thực tế + repo GitHub):
///   Chuẩn (7 trường):  SốCCCD|SốCMNDcũ|HọTên|NgàySinh|GiớiTính|ĐịaChỉ|NgàyCấp
///   Zalo   (6 trường): SốCCCD|HọTên|NgàySinh|GiớiTính|ĐịaChỉ|NgàyCấp  (bỏ CMND cũ; ngày có dấu /)
/// Tiếng Việt UTF-8 có dấu ở trường tên/địa chỉ. Trường thiếu → trống.
/// Không bao giờ ném ngoại lệ — lỗi → IsValid=false kèm thông điệp hiện được lên UI.
/// </summary>
public sealed class CccdQrParser : IQrPayloadParser
{
    private const int StandardFieldCount = 7;
    private const int ZaloFieldCount = 6;

    public CccdParseResult Parse(string rawPayload)
    {
        // Trim + bỏ BOM (một số máy quét chèn U+FEFF đầu chuỗi) + bỏ đuôi \r\n.
        var cleaned = (rawPayload ?? "").Trim().TrimStart('﻿');
        if (cleaned.Length == 0)
            return Fail("Chuỗi QR trống (máy quét không trả dữ liệu).");

        string[] parts;
        try
        {
            parts = cleaned.Split('|');
        }
        catch (Exception ex)
        {
            return Fail($"Lỗi tách dữ liệu QR: {ex.Message}");
        }

        if (parts.Length != StandardFieldCount && parts.Length != ZaloFieldCount)
            return Fail(
                $"QR không đúng định dạng CCCD: có {parts.Length} trường (một số trống? máy quét quét nhầm thứ khác?). "
                + $"Dự kiến 7 trường (chuẩn) hoặc 6 trường (Zalo).");

        var isZalo = parts.Length == ZaloFieldCount;
        var warnings = new List<string>();

        // Ánh xạ vị trí theo biến thể.
        // Chuẩn 7 trường: CCCD|CMNDcũ|HọTên|NgàySinh|GiớiTính|ĐịaChỉ|NgàyCấp
        // Zalo 6 trường:  CCCD|HọTên|NgàySinh|GiớiTính|ĐịaChỉ|NgàyCấp (bỏ CMND cũ)
        int iCccd = 0,
            iOld = isZalo ? -1 : 1,
            iName = isZalo ? 1 : 2,
            iDob = isZalo ? 2 : 3,
            iGender = isZalo ? 3 : 4,
            iAddress = isZalo ? 4 : 5,
            iIssue = isZalo ? 5 : 6;

        var cccdNumber = parts[iCccd].Trim();
        var oldId = isZalo ? "" : parts[iOld].Trim();
        var fullName = parts[iName].Trim();
        var gender = parts[iGender].Trim();
        var address = parts[iAddress].Trim();
        var dobRaw = parts[iDob].Trim();
        var issueRaw = parts[iIssue].Trim();

        // Validate CCCD: phải 12 chữ số.
        if (!IsDigits(cccdNumber) || cccdNumber.Length != 12)
            return Fail(
                $"Số CCCD không hợp lệ: \"{cccdNumber}\" (cần đúng 12 chữ số). "
                + $"Kiểm tra máy quét có quét đúng mã QR CCCD không.");

        if (fullName.Length == 0)
            warnings.Add("Thiếu họ tên trong QR.");

        // Ngày sinh / ngày cấp: trống → cho phép (warning). Hợp lệ → parse. Không đúng → warning, không chặn.
        var dob = CccdDateParser.Parse(dobRaw);
        if (dobRaw.Length > 0 && dob is null)
            warnings.Add($"Ngày sinh \"{dobRaw}\" không đúng định dạng, để trống.");
        else if (dob is { } d && d > DateTime.Today)
            warnings.Add("Ngày sinh lớn hơn hôm nay — kiểm tra lại dữ liệu thẻ.");

        var issue = CccdDateParser.Parse(issueRaw);
        if (issueRaw.Length > 0 && issue is null)
            warnings.Add($"Ngày cấp \"{issueRaw}\" không đúng định dạng, để trống.");

        // Giới tính: chuẩn hóa Nam/Nữ; lạ → giữ nguyên + warning.
        if (gender is not ("Nam" or "Nữ"))
            warnings.Add($"Giới tính \"{gender}\" không phải Nam/Nữ.");

        if (isZalo)
            warnings.Add("QR dạng Zalo (6 trường): không có số CMND cũ.");

        var citizen = new CitizenInfo
        {
            CccdNumber = cccdNumber,
            OldIdNumber = oldId,
            FullName = fullName,
            DateOfBirth = dob ?? default,
            Gender = gender,
            Address = address,
            IssueDate = issue ?? default,
            RawPayload = cleaned
        };

        return new CccdParseResult { IsValid = true, Citizen = citizen, Warnings = warnings };
    }

    private static bool IsDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s)
            if (c is < '0' or > '9')
                return false;
        return true;
    }

    private static CccdParseResult Fail(string message)
        => new() { IsValid = false, ErrorMessage = message };

    /// <summary>
    /// Public cho tests debug. Rút gọn + che số CCCD để không ghi key cụ thể trong rejected log.
    /// </summary>
    public static string SanitizeForDisplay(string payload)
    {
        var s = (payload ?? "").Trim().TrimStart('﻿');
        s = MaskCccdInPayload(s);
        return s.Length <= 100 ? s : s[..100] + "…";
    }

    /// <summary>Che 12 chữ số CCCD (còn 4 số cuối) ở trường đầu của payload pipe-separated; giữ nguyên phần còn lại.</summary>
    public static string MaskCccdInPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return payload;
        var bars = payload.IndexOf('|');
        var head = bars < 0 ? payload : payload[..bars];
        var masked = head.Length <= 4 || !IsDigits(head) ? head : new string('*', head.Length - 4) + head[^4..];
        return bars < 0 ? masked : masked + payload[bars..];
    }
}