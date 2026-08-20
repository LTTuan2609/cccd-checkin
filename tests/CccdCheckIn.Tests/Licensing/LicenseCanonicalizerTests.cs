using System.Text;
using CccdCheckIn.Core.Licensing;
using Xunit;

namespace CccdCheckIn.Tests.Licensing;

public class LicenseCanonicalizerTests
{
    private static LicensePayload Sample(DateTimeOffset? expiresAtUtc = null) => new()
    {
        Version = 1,
        Product = "CccdCheckIn",
        LicenseId = "LIC001",
        MachineKeyHash = "abc123",
        Plan = LicensePlan.Yearly,
        IssuedAtUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
        NotBeforeUtc = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero),
        ExpiresAtUtc = expiresAtUtc ?? new DateTimeOffset(2027, 8, 20, 9, 0, 0, TimeSpan.Zero),
        KeyId = "prod-2026-01",
    };

    [Fact]
    public void Canonicalize_Twice_ProducesSameBytes()
    {
        var c = new LicenseCanonicalizer();
        var first = c.Canonicalize(Sample());
        var second = c.Canonicalize(Sample());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Canonicalize_UsesFixedPropertyOrder_NoWhitespace()
    {
        var c = new LicenseCanonicalizer();
        var json = Encoding.UTF8.GetString(c.Canonicalize(Sample()));
        Assert.Contains("\"v\":1", json);
        Assert.DoesNotContain(" ", json);
        // Thứ tự canonical cố định: v đứng trước product, keyId đứng cuối.
        Assert.True(json.IndexOf("\"v\":", StringComparison.Ordinal) < json.IndexOf("\"product\":", StringComparison.Ordinal));
        Assert.True(json.IndexOf("\"expiresAt\":", StringComparison.Ordinal) < json.IndexOf("\"keyId\":", StringComparison.Ordinal));
    }

    [Fact]
    public void Canonicalize_ConvertsLocalOffsetToUtc()
    {
        var c = new LicenseCanonicalizer();
        var payload = Sample(expiresAtUtc: new DateTimeOffset(2027, 8, 20, 16, 0, 0, TimeSpan.FromHours(7)));
        var json = Encoding.UTF8.GetString(c.Canonicalize(payload));
        Assert.Contains("\"expiresAt\":\"2027-08-20T09:00:00Z\"", json);
    }

    [Fact]
    public void Parse_AfterCanonicalize_RoundTripsAllFields()
    {
        var c = new LicenseCanonicalizer();
        var original = Sample();
        var parsed = c.Parse(c.Canonicalize(original));

        Assert.NotNull(parsed);
        Assert.Equal(original.Version, parsed!.Version);
        Assert.Equal(original.Product, parsed.Product);
        Assert.Equal(original.LicenseId, parsed.LicenseId);
        Assert.Equal(original.MachineKeyHash, parsed.MachineKeyHash);
        Assert.Equal(original.Plan, parsed.Plan);
        Assert.Equal(original.IssuedAtUtc, parsed.IssuedAtUtc);
        Assert.Equal(original.NotBeforeUtc, parsed.NotBeforeUtc);
        Assert.Equal(original.ExpiresAtUtc, parsed.ExpiresAtUtc);
        Assert.Equal(original.KeyId, parsed.KeyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1}")] // thiếu trường bắt buộc
    public void Parse_MalformedInput_ReturnsNull(string input)
    {
        var c = new LicenseCanonicalizer();
        Assert.Null(c.Parse(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(LicensePlan.Monthly, "monthly")]
    [InlineData(LicensePlan.Yearly, "yearly")]
    [InlineData(LicensePlan.Custom, "custom")]
    public void Canonicalize_PlanSerializedAsLowercase(LicensePlan plan, string expected)
    {
        var c = new LicenseCanonicalizer();
        var basePayload = Sample();
        var payload = new LicensePayload
        {
            Version = basePayload.Version,
            Product = basePayload.Product,
            LicenseId = basePayload.LicenseId,
            MachineKeyHash = basePayload.MachineKeyHash,
            Plan = plan,
            IssuedAtUtc = basePayload.IssuedAtUtc,
            NotBeforeUtc = basePayload.NotBeforeUtc,
            ExpiresAtUtc = basePayload.ExpiresAtUtc,
            KeyId = basePayload.KeyId,
        };
        var json = Encoding.UTF8.GetString(c.Canonicalize(payload));
        Assert.Contains($"\"plan\":\"{expected}\"", json);
    }
}
