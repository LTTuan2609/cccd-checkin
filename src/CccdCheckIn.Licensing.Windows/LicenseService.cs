using System.Runtime.Versioning;
using CccdCheckIn.Core.Licensing;

namespace CccdCheckIn.Licensing.Windows;

/// <summary>
/// Orchestrate đánh giá license: có license đã ký ⇒ verify chữ ký + máy + ngày;
/// không có ⇒ trial 14 ngày theo TrialClock. Activate lưu atomic rồi re-evaluate.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LicenseService : ILicenseService
{
    private readonly WindowsLicenseStore _store;

    private readonly IMachineIdentityProvider _identityProvider;

    private readonly ILicenseVerifier _verifier;

    private readonly ILicenseCanonicalizer _canonicalizer;

    private readonly TrialClock _trialClock;

    public LicenseService(
        WindowsLicenseStore store,
        IMachineIdentityProvider identityProvider,
        ILicenseVerifier verifier,
        ILicenseCanonicalizer canonicalizer,
        TrialClock? trialClock = null)
    {
        _store = store;
        _identityProvider = identityProvider;
        _verifier = verifier;
        _canonicalizer = canonicalizer;
        _trialClock = trialClock ?? new TrialClock();
    }

    public LicenseValidationResult Evaluate(DateTimeOffset nowUtc)
    {
        var identity = _identityProvider.GetOrCreateAsync().GetAwaiter().GetResult();
        var stored = _store.LoadLicense();

        if (stored is null)
        {
            return EvaluateTrial(nowUtc);
        }

        if (!ActivationCodeCodec.TryDecode(stored.ActivationCode, out var canonical, out var signature))
        {
            return Fail(LicenseStatus.Malformed, "License không đọc được. Vui lòng kích hoạt lại.");
        }

        if (!_verifier.VerifySignature(canonical, signature))
        {
            return Fail(LicenseStatus.InvalidSignature, "License không hợp lệ (chữ ký sai). Vui lòng liên hệ nhà cung cấp.");
        }

        var payload = _canonicalizer.Parse(canonical);
        if (payload is null)
        {
            return Fail(LicenseStatus.Malformed, "License sai cấu trúc.");
        }

        if (payload.Product != LicensingDefaults.Product || payload.Version != LicensingDefaults.PayloadVersion)
        {
            return Fail(LicenseStatus.Malformed, "License không dành cho sản phẩm này.");
        }

        if (payload.MachineKeyHash != identity.MachineKeyHash)
        {
            return Fail(LicenseStatus.MachineMismatch, "License không thuộc máy này. Vui lòng gửi Mã Máy để nhận license đúng.");
        }

        if (nowUtc < payload.NotBeforeUtc)
        {
            return new LicenseValidationResult
            {
                Status = LicenseStatus.NotYetValid,
                Mode = LicenseMode.ReadExportOnly,
                Payload = payload,
                Message = "License chưa đến ngày hiệu lực.",
            };
        }

        if (nowUtc >= payload.ExpiresAtUtc)
        {
            return new LicenseValidationResult
            {
                Status = LicenseStatus.Expired,
                Mode = LicenseMode.ReadExportOnly,
                Payload = payload,
                DaysRemaining = 0,
                Message = "Gói thuê bao đã hết hạn. Dữ liệu của bạn vẫn được giữ nguyên, có thể xem và xuất. Nhập license gia hạn để tiếp tục chấm công.",
            };
        }

        var daysLeft = (int)Math.Ceiling((payload.ExpiresAtUtc - nowUtc).TotalDays);
        return new LicenseValidationResult
        {
            Status = LicenseStatus.Valid,
            Mode = LicenseMode.Active,
            Payload = payload,
            DaysRemaining = daysLeft,
            Message = $"License hoạt động — còn {daysLeft} ngày.",
        };
    }

    public LicenseValidationResult Activate(string activationCode, DateTimeOffset nowUtc)
    {
        var identity = _identityProvider.GetOrCreateAsync().GetAwaiter().GetResult();

        if (!ActivationCodeCodec.TryDecode(activationCode, out var canonical, out var signature))
        {
            return Fail(LicenseStatus.Malformed, "Mã kích hoạt không đúng định dạng.");
        }

        if (!_verifier.VerifySignature(canonical, signature))
        {
            return Fail(LicenseStatus.InvalidSignature, "Mã kích hoạt không hợp lệ.");
        }

        var payload = _canonicalizer.Parse(canonical);
        if (payload is null || payload.Product != LicensingDefaults.Product ||
            payload.Version != LicensingDefaults.PayloadVersion)
        {
            return Fail(LicenseStatus.Malformed, "Mã kích hoạt không dành cho sản phẩm này.");
        }

        // Cùng bộ kiểm tra với Evaluate — không lưu mã chưa hiệu lực, vì lưu xong
        // Evaluate sẽ báo NotYetValid/Malformed mãi mãi dù mã đúng.
        if (nowUtc < payload.NotBeforeUtc)
        {
            return Fail(LicenseStatus.NotYetValid, "Mã kích hoạt chưa đến ngày hiệu lực.");
        }

        if (payload.MachineKeyHash != identity.MachineKeyHash)
        {
            return Fail(LicenseStatus.MachineMismatch, "Mã kích hoạt không thuộc máy này. Vui lòng gửi đúng Mã Máy hiển thị trên màn hình.");
        }

        if (nowUtc >= payload.ExpiresAtUtc)
        {
            // Không lưu license đã hết hạn — giữ nguyên license cũ nếu có.
            return Fail(LicenseStatus.Expired, "Mã kích hoạt đã hết hạn. Vui lòng liên hệ nhà cung cấp.");
        }

        _store.SaveLicense(new StoredLicense
        {
            ActivationCode = activationCode.Trim(),
            ActivatedAtUtc = nowUtc,
        });

        return Evaluate(nowUtc);
    }

    private LicenseValidationResult EvaluateTrial(DateTimeOffset nowUtc)
    {
        var state = _store.LoadTrialState();

        // Đối chiếu marker plaintext với state DPAPI — lệch firstSeen ⇒ state bị sửa.
        var marker = _store.LoadTrialMarker();
        if (state is not null && marker is not null &&
            Math.Abs((marker.Value - state.FirstSeenUtc).TotalSeconds) > 60)
        {
            return Fail(LicenseStatus.Tampered, "Dữ liệu dùng thử không hợp lệ. Vui lòng liên hệ nhà cung cấp.");
        }

        var evaluation = _trialClock.Evaluate(state, nowUtc);

        if (evaluation.Status == LicenseStatus.Tampered)
        {
            // Không cập nhật state khi phát hiện gian lận — giữ nguyên bằng chứng.
            return Fail(LicenseStatus.Tampered, "Phát hiện đồng hồ hệ thống bị chỉnh lùi. Vui lòng đặt lại ngày giờ đúng.");
        }

        _store.SaveTrialState(evaluation.NewState);

        if (evaluation.Status == LicenseStatus.TrialExpired)
        {
            return new LicenseValidationResult
            {
                Status = LicenseStatus.TrialExpired,
                Mode = LicenseMode.ReadExportOnly,
                DaysRemaining = 0,
                Message = "Thời gian dùng thử đã kết thúc. Dữ liệu của bạn vẫn được giữ nguyên, có thể xem và xuất. Nhập mã kích hoạt để tiếp tục chấm công.",
            };
        }

        return new LicenseValidationResult
        {
            Status = LicenseStatus.Trial,
            Mode = LicenseMode.Trial,
            DaysRemaining = evaluation.DaysRemaining,
            Message = $"Dùng thử — còn {evaluation.DaysRemaining} ngày.",
        };
    }

    private static LicenseValidationResult Fail(LicenseStatus status, string message) => new()
    {
        Status = status,
        Mode = LicenseMode.ReadExportOnly,
        Message = message,
    };
}
