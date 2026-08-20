using System.Security.Cryptography;
using CccdCheckIn.Core.Licensing;
using CccdCheckIn.LicenseIssuer;
using CccdCheckIn.Licensing.Windows;

var help = """
    CccdCheckIn LicenseIssuer — tool nội bộ phát hành license (KHÔNG ship cho khách).

    create-keys
        Tạo (hoặc load) private key ký license, lưu tại:
        %ProgramData%\LTTuan\LicenseIssuer\private-key.json
        In PUBLIC KEY (base64 SPKI) — paste vào EmbeddedKeys.LicensePublicKeyBase64 trong app.

    show-machine
        In Mã Máy ĐẦY ĐỦ (machineKeyHash base64url) của MÁY NÀY — chính là chuỗi
        app CccdCheckIn hiển thị/copy trong màn hình Bản quyền. Dùng để đối chiếu
        khi khách gửi Mã Máy, hoặc kiểm tra license sinh ra khớp đúng máy.

    issue --key <path|default> --machine <MãMáy> --plan monthly|yearly [--days N] [--out file]
        Ký license cho Mã Máy. Mã Máy = machineKeyHash đầy đủ (base64url SHA-256 của
        public key máy) được app hiển thị trong màn hình Bản quyền — khách copy nguyên
        chuỗi này gửi cho nhà cung cấp. In Mã Kích Hoạt (base64url payload.signature)
        và ghi file .license nếu --out được chỉ định.
        --days: số ngày hiệu lực (monthly≈30, yearly≈365; mặc nhiên theo --plan nếu bỏ qua).

    Ví dụ:
      LicenseIssuer create-keys
      LicenseIssuer issue --machine Aa1b2C3dE4fG5hI6jK7lM8nO9pQ0rS1tU2vW3xY4z5 --plan yearly
      LicenseIssuer issue --machine Aa1b2C3dE4fG5hI6jK7lM8nO9pQ0rS1tU2vW3xY4z5 --days 30 --out D:\\LIC.license
    """;

if (args.Length == 0)
{
    Console.WriteLine(help);
    return 1;
}

switch (args[0].ToLowerInvariant())
{
    case "create-keys":
    {
        var (_, publicKey) = SigningKeyStore.CreateOrLoad();
        Console.WriteLine("Public key (paste vào EmbeddedKeys.LicensePublicKeyBase64):");
        Console.WriteLine(publicKey);
        Console.WriteLine();
        Console.WriteLine("Private key: " + SigningKeyStore.DefaultKeyFile);
        return 0;
    }

    case "show-machine":
    {
        var identity = new DpapiMachineKeyStore().GetOrCreateAsync().GetAwaiter().GetResult();
        Console.WriteLine("Mã Máy (đầy đủ — gửi chuỗi này đi):");
        Console.WriteLine(identity.MachineKeyHash);
        Console.WriteLine();
        Console.WriteLine("Mã Máy rút gọn (chỉ để đối chiếu bằng mắt):");
        Console.WriteLine(identity.MachineCode);
        return 0;
    }

    case "issue":
    {
        var opts = ParseOptions(args[1..]);
        if (!opts.TryGetValue("machine", out var machine) || string.IsNullOrWhiteSpace(machine))
        {
            Console.Error.WriteLine("Thiếu --machine <MãMáy>.");
            return 2;
        }

        var plan = opts.GetValueOrDefault("plan")?.ToLowerInvariant() switch
        {
            "monthly" => LicensePlan.Monthly,
            "yearly" => LicensePlan.Yearly,
            _ => LicensePlan.Custom,
        };

        var days = plan switch
        {
            LicensePlan.Monthly => 30,
            LicensePlan.Yearly => 365,
            _ => 0,
        };
        if (opts.TryGetValue("days", out var daysRaw) &&
            int.TryParse(daysRaw, out var daysParsed) && daysParsed > 0)
        {
            days = daysParsed;
        }

        if (days <= 0)
        {
            Console.Error.WriteLine("Phải chỉ định --plan monthly|yearly hoặc --days N.");
            return 2;
        }

        var (key, _) = SigningKeyStore.CreateOrLoad(opts.GetValueOrDefault("key"));
        using (key)
        {
            // Mã Máy chính là machineKeyHash (base64url) — binding trực tiếp, không hashing lại.
            var machineKeyHash = machine.Trim();

            var nowUtc = DateTimeOffset.UtcNow;
            var payload = new LicensePayload
            {
                Version = LicensingDefaults.PayloadVersion,
                Product = LicensingDefaults.Product,
                LicenseId = "LIC-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                MachineKeyHash = machineKeyHash,
                Plan = plan,
                IssuedAtUtc = nowUtc,
                NotBeforeUtc = nowUtc,
                ExpiresAtUtc = nowUtc.AddDays(days),
                KeyId = "prod-2026-01",
            };

            var canonical = new LicenseCanonicalizer().Canonicalize(payload);
            var signature = key.SignData(canonical, HashAlgorithmName.SHA256);
            var code = ActivationCodeCodec.Encode(canonical, signature);

            Console.WriteLine("--- THÔNG TIN LICENSE ---");
            Console.WriteLine("LicenseId:     " + payload.LicenseId);
            Console.WriteLine("Product:       " + payload.Product);
            Console.WriteLine("Mã Máy:        " + machine);
            Console.WriteLine("Gói:           " + plan);
            Console.WriteLine("Hiệu lực:      " + payload.NotBeforeUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
            Console.WriteLine("Hết hạn:       " + payload.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
            Console.WriteLine();
            Console.WriteLine("--- MÃ KÍCH HOẠT (gửi cho khách) ---");
            Console.WriteLine(code);
            Console.WriteLine();

            if (opts.TryGetValue("out", out var outFile))
            {
                File.WriteAllText(outFile, code);
                Console.WriteLine("Đã ghi file:   " + Path.GetFullPath(outFile));
            }
        }

        return 0;
    }

    default:
        Console.WriteLine("Lệnh không biết: " + args[0]);
        Console.WriteLine(help);
        return 2;
    }

static Dictionary<string, string?> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--")) continue;
        var name = arg[2..];
        var value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : null;
        result[name] = value;
    }
    return result;
}