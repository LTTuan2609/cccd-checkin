using System.Globalization;
using System.Text;

namespace CccdCheckIn.Core.Licensing;

/// <summary>
/// Canonical JSON với thứ tự property cố định:
/// v, product, licenseId, machineKeyHash, plan, issuedAt, notBefore, expiresAt, keyId.
/// UTF-8, ngày UTC dạng yyyy-MM-dd'T'HH:mm:ss'Z', không whitespace thừa.
/// </summary>
public sealed class LicenseCanonicalizer : ILicenseCanonicalizer
{
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public byte[] Canonicalize(LicensePayload payload)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        // "v" là số nguyên, không bọc trong quotes; các trường còn lại là chuỗi.
        sb.Append("\"v\":").Append(payload.Version.ToString(CultureInfo.InvariantCulture));
        AppendField(sb, "product", payload.Product);
        AppendField(sb, "licenseId", payload.LicenseId);
        AppendField(sb, "machineKeyHash", payload.MachineKeyHash);
        AppendField(sb, "plan", PlanToString(payload.Plan));
        AppendField(sb, "issuedAt", FormatDate(payload.IssuedAtUtc));
        AppendField(sb, "notBefore", FormatDate(payload.NotBeforeUtc));
        AppendField(sb, "expiresAt", FormatDate(payload.ExpiresAtUtc));
        AppendField(sb, "keyId", payload.KeyId);
        sb.Append('}');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public LicensePayload? Parse(ReadOnlySpan<byte> canonical)
    {
        // Parse tay JSON đơn giản (không whitespace, property cố định) để tránh phụ thuộc
        // vào hành vi JsonSerializer; payload đã được verify chữ ký trước khi dùng giá trị.
        var text = Encoding.UTF8.GetString(canonical);
        if (!text.StartsWith('{') || !text.EndsWith('}'))
            return null;

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var inner = text[1..^1];
        foreach (var pair in SplitTopLevel(inner))
        {
            var idx = pair.IndexOf(':');
            if (idx <= 0) return null;
            var key = pair[..idx].Trim();
            var value = pair[(idx + 1)..].Trim();
            if (!key.StartsWith('"') || !key.EndsWith('"')) return null;
            fields[key[1..^1]] = value.StartsWith('"') ? value[1..^1] : value;
        }

        if (!fields.TryGetValue("v", out var v) ||
            !fields.TryGetValue("product", out var product) ||
            !fields.TryGetValue("licenseId", out var licenseId) ||
            !fields.TryGetValue("machineKeyHash", out var machineKeyHash) ||
            !fields.TryGetValue("plan", out var plan) ||
            !fields.TryGetValue("issuedAt", out var issuedAt) ||
            !fields.TryGetValue("notBefore", out var notBefore) ||
            !fields.TryGetValue("expiresAt", out var expiresAt) ||
            !fields.TryGetValue("keyId", out var keyId))
            return null;

        if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var version)) return null;
        if (!ParseDate(issuedAt, out var issuedAtUtc)) return null;
        if (!ParseDate(notBefore, out var notBeforeUtc)) return null;
        if (!ParseDate(expiresAt, out var expiresAtUtc)) return null;

        return new LicensePayload
        {
            Version = version,
            Product = product,
            LicenseId = licenseId,
            MachineKeyHash = machineKeyHash,
            Plan = StringToPlan(plan),
            IssuedAtUtc = issuedAtUtc,
            NotBeforeUtc = notBeforeUtc,
            ExpiresAtUtc = expiresAtUtc,
            KeyId = keyId,
        };
    }

    private static void AppendField(StringBuilder sb, string name, string value, bool first = false)
    {
        if (!first) sb.Append(',');
        sb.Append('"').Append(name).Append("\":\"").Append(value).Append('"');
    }

    private static string FormatDate(DateTimeOffset value)
        => value.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static bool ParseDate(string value, out DateTimeOffset result)
    {
        if (DateTimeOffset.TryParseExact(
                value, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
        {
            return true;
        }
        result = default;
        return false;
    }

    private static string PlanToString(LicensePlan plan) => plan switch
    {
        LicensePlan.Monthly => "monthly",
        LicensePlan.Yearly => "yearly",
        LicensePlan.Custom => "custom",
        _ => "custom",
    };

    private static LicensePlan StringToPlan(string plan) => plan switch
    {
        "monthly" => LicensePlan.Monthly,
        "yearly" => LicensePlan.Yearly,
        _ => LicensePlan.Custom,
    };

    /// <summary>Tách cặp key:value ở top level, tôn trọng quote (value không chứa ',' ngoài quote theo canonical form nhưng vẫn parse an toàn).</summary>
    private static List<string> SplitTopLevel(string inner)
    {
        var result = new List<string>();
        var inQuotes = false;
        var start = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(inner[start..i]);
                start = i + 1;
            }
        }
        if (start < inner.Length) result.Add(inner[start..]);
        return result;
    }
}
