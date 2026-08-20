# CCCD Check-in — Ứng dụng chấm công bằng mã QR CCCD

Ứng dụng Windows **check-in bằng máy quét QR CCCD**: nhân viên đưa Căn cước công dân vào máy
quét → app tự đọc dữ liệu từ QR → ghi log check-in và hiển thị trên một dashboard theo dõi
số lượt đã quét trong ngày.

- **Một dashboard duy nhất** để theo dõi: số đã check-in hôm nay, tổng lượt, log gần nhất.
- **Chạy được ngay** trên máy Windows bình thường (bản publish self-contained, không cần cài .NET).
- **Mở – không hardcode**: khách hàng tự cấu hình cổng máy quét, trường nào lưu, trường nào xuất CSV.

---

## 1. Yêu cầu phần cứng

| Thành phần | Ghi chú |
|---|---|
| Máy quét QR CCCD | Máy quét mã vạch/QR **passive** (tự gửi chuỗi đã giải mã + phím Enter qua cổng COM/Serial USB). Máy đang dùng: `VID_DA23&PID_1904` (COM3) |
| CCCD | Căn cước công dân gắn chip có mã QR in trên mặt sau |
| Máy tính | Windows 10/11 — bản publish chạy độc lập, không cần cài .NET Runtime |

> Mã QR CCCD gồm 7 trường: **Số CCCD | Số CMND cũ | Họ tên | Ngày sinh | Giới tính | Địa chỉ | Ngày cấp**.
> App cũng nhận biến thể QR của Zalo (6 trường, ngày có dấu `/`).

---

## 2. Cài đặt & chạy

### Nếu nhận từ nhà phát triển (bản phân phối)

1. Giải nén toàn bộ thư mục `publish\win-x64\` (có `CccdCheckIn.App.exe` + `appsettings.json`) vào máy cần dùng.
2. Cắm máy quét QR vào cổng USB.
3. Chạy `CccdCheckIn.App.exe`.

Lần đầu chạy: app tự tạo thư mục `Data\` (DB `Data\checkin.db`, thư mục xuất CSV `Data\export\`) ngay cạnh exe.
Máy quét sẽ được **tự dò theo VID/PID** — thường không cần cấu hình gì thêm.

### Nếu tự build

Cần [.NET SDK 10 LTS](https://dotnet.microsoft.com/download/dotnet/10.0) trên máy phát triển.

```bash
# Build + chạy
dotnet build CccdCheckIn.slnx
dotnet run --project src/CccdCheckIn.App

# Đóng gói 1 file exe self-contained (không cần cài .NET trên máy khách)
powershell -ExecutionPolicy Bypass -File publish.ps1
# → publish\win-x64\CccdCheckIn.App.exe (+ appsettings.json)
```

---

## 3. Dashboard

```
┌──────────────────────────────────────────────────────────────┐
│ ● Đang lắng nghe — COM3 @14400     [Kết nối lại][Cài đặt][Xuất CSV][Thoát]
├─────────────────────────────┬────────────────────────────────┤
│  ĐÃ CHECK-IN HÔM NAY  : 128 │   LOG GẦN NHẤT (20 dòng)       │
│  Tổng lượt            : 204 │   Giờ      Họ tên   Số CCCD     │
│  ─────────────────────      │   08:32:14 Mạnh     ****3451    │
│  Vừa quét ✓ 08:32:14        │   08:31:05 Huyền    ****0485    │
│    Nguyễn Gia Mạnh          │   ...                            │
│    ****3451 · Nam · 21/03/1983                                │
├─────────────────────────────┴────────────────────────────────┤
│ ⚠ (chỉ hiện khi có quét lỗi / cảnh báo)                     │
└──────────────────────────────────────────────────────────────┘
```

- **Thanh trạng thái** đổi màu theo máy quét: xanh lá = đang nghe · cam = đang kết nối/kết nối lại · đỏ = lỗi.
- Quét hợp lệ: dòng mới hiện lên đầu log, **số CCCD được che bớt** (chỉ hiện 4 số cuối — bật/tắt trong Cài đặt).
- Quét **trùng thẻ** trong cửa sổ chống trùng (mặc định 5 giây): bỏ qua, báo "thẻ đã quét".
- QR không đọc được: báo lỗi, ghi vào bảng `rejected` trong DB để kiểm tra, app vẫn tiếp tục nghe.
- **Xuất CSV**: xuất tất cả hoặc chỉ hôm nay (theo `Ui.StatusFilter`) → file `.csv` có BOM UTF-8
  (mở bằng Excel thấy tiếng Việt đúng) → hỏi có mở thư mục không.

**Nút Cài đặt**: mở bảng thuộc tính — dò cổng COM (tự nhận máy quét), sửa baud, che/không che số CCCD,
số dòng hiển thị, tắt/bật tự động nghe… Thay đổi cấu hình được ghi vào `appsettings.json`.

---

## 4. Cấu hình (`appsettings.json` cạnh exe)

Toàn bộ hành vi app nằm trong 1 file JSON — không hardcode. Sửa bằng Notepad, lưu lại, chạy lại app.

```jsonc
{
  "Reader": {
    "Mode": "Serial",          // "Serial" | "Simulated" (demo không cần máy quét)
    "Port": "",                // "" = tự dò theo ScannerVidPids; hoặc ghi thẳng "COM3"
    "BaudRate": 14400,     // máy quét của khách (VID_DA23) xuất 14400, KHÔNG phải 115200
    "DtrEnable": true,
    "Encoding": "Auto",  // (không dùng nữa — decode luôn UTF-8, fallback Windows-1252)
    "AutoDetectPort": true,
    "ScannerVidPids": "VID_DA23&PID_1904",   // máy quét khác → đổi VID/PID
    "ReconnectIntervalMs": 3000              // rút USB giữa chừng → tự kết nối lại
  },
  "Scan": {
    "DuplicateIgnoreSeconds": 5,             // bỏ qua quét trùng trong N giây
    "LogRejectedScans": true                 // ghi QR lỗi vào bảng rejected
  },
  "Store": {
    "Type": "Sqlite",
    "SqlitePath": "Data/checkin.db",
    "SaveFields": [ "CccdNumber", "FullName", "DateOfBirth", "Gender", "Address", "IssueDate" ]
  },
  "Ui": {
    "AutoStartListening": true,
    "RecentRows": 20,
    "MaskCccdDigits": true,    // UI chỉ hiện 4 số cuối của CCCD
    "StatusFilter": "Today"    // dashboard: "Today" | "All"
  },
  "Export": {
    "Fields": [ "CheckedInAt", "CccdNumber", "FullName", "Address" ],
    "CsvPath": "Data/export"
  },
  "Licensing": {
    "Enabled": true,        // false = tắt licensing (dùng nội bộ/kiểm thử)
    "TrialDays": 14         // số ngày dùng thử
  }
}
```

### Licensing — bản quyền & thuê bao

Ứng dụng chạy theo mô hình **dùng thử → thuê bao tháng/năm**, hoạt động 100% offline:

- **Lần đầu chạy**: tự bật **dùng thử 14 ngày** (banner "🎯 Dùng thử — còn N ngày").
- **Hết dùng thử / hết thuê bao**: app chuyển sang **chế độ chỉ đọc + xuất** —
  vẫn xem lịch sử, xuất CSV, backup DB; **chỉ chặn ghi check-in mới**. **Dữ liệu luôn là của bạn**,
  không bao giờ bị xóa/khoá/mã hóa vì license.
- **Nút "Bản quyền"** trên toolbar: hiện **Mã Máy**, nhập/dán **Mã Kích Hoạt** hoặc import file `.license`.

**Luồng mua/gia hạn (offline):**

1. Khách cài app, mở **Bản quyền** → **Sao chép Mã Máy**.
2. Gửi Mã Máy + thông tin công ty cho nhà cung cấp → chuyển khoản gói tháng/năm.
3. Nhà cung cấp chạy tool nội bộ `CccdCheckIn.LicenseIssuer` tạo **Mã Kích Hoạt** (chữ ký số) → gửi lại.
4. Khách **Dán** Mã Kích Hoạt (hoặc Mở file `.license`) → **Kích hoạt** → app switch sang **Active**.

> Mã Kích Hoạt là chữ ký **ECDSA P-256** trên (Mã Máy + gói + hạn), chỉ nhà cung cấp
> (giữ private key) ký được. App chỉ nhúng public key để verify — khách không thể tự chế mã.
> Chống gian lận trial là *best-effort* trên máy khách (Administrator vẫn có thể xóa state),
> đây là giới hạn chung của phần mềm desktop offline.

---

## 5. Bảo mật & lưu ý vận hành

| Khóa | Mô tả |
|---|---|
| `CccdNumber` | Số CCCD 12 số |
| `OldIdNumber` | Số CMND cũ (QR một số thẻ có thể trống) |
| `FullName` | Họ và tên |
| `DateOfBirth` | Ngày sinh |
| `Gender` | Giới tính (Nam/Nữ) |
| `Address` | Địa chỉ |
| `IssueDate` | Ngày cấp thẻ |
| `CheckedInAt` | Thời điểm check-in (chỉ dùng cho xuất CSV) |
| `RawPayload` | Chuỗi QR thô (dùng cho gỡ lỗi) |

- **`Store.SaveFields`** = trường nào được **ghi vào DB**. Mặc định đầy đủ 6 trường nhân thân
  (đúng yêu cầu). Muốn bớt lưu → bỏ trường khỏi mảng.
- **`Export.Fields`** = cột nào xuất ra **CSV**. Mặc định chỉ 4 cột để tránh đưa PII không cần
  thiết ra file Excel. Muốn đầy đủ → thêm `DateOfBirth`, `Gender`, `IssueDate`, `RawPayload`…

> **Quan trọng**: `SaveFields` áp dụng khi app khởi động (dữ liệu cũ trong DB không đổi).
> Đổi cấu hình xong → đóng app, mở lại.

---

## 5. Bảo mật & lưu ý vận hành

Ứng dụng lưu dữ liệu **nhân thân** (họ tên, ngày sinh, địa chỉ, số CCCD) trong file
`Data\checkin.db` theo cấu hình. Khuyến nghị:

- **Bật BitLocker** trên ổ chứa DB (khuyến nghị mạnh) — dữ liệu nhân thân nhạy cảm.
- Dùng **tài khoản Windows riêng** cho máy chấm công, hạn chế quyền quản trị.
- Không đặt `Data\` trên share mạng công khai; nếu cần chia sẻ → dùng CSV **đã lọc trường**.
- Có thể chọn **không lưu** trường nhạy cảm bằng cách bỏ khỏi `Store.SaveFields`.

### Xử lý sự cố thường gặp

| Vấn đề | Cách xử lý |
|---|---|
| Không nhận máy quét | Cắm máy quét; bấm **Cài đặt → Dò cổng COM…**, chọn cổng có dấu "✓ máy quét" |
| Tên có dấu sai (mất dấu) | Máy quét đang xuất ASCII. Chỉnh máy quét sang **UTF-8** (xem tài liệu máy) — app luôn giải mã UTF-8, fallback Windows-1252 |
| Rút USB giữa chừng | App tự kết nối lại sau vài giây (thanh trạng thái cam → xanh) |
| Quét thử bị "trùng thẻ" | Là quét trúng thẻ đúng trong 5 giây — mặc định chống trùng; chỉnh thời gian ở `Scan.DuplicateIgnoreSeconds` |
| Số đếm không tăng, DB bị khóa | Đảm bảo chỉ **một** app đang chạy (app đã chặn mở 2 cửa sổ), không mở DB bằng công cụ khác khi đang chạy |
| Xuất CSV mở Excel bị vỡ dấu | File đã có BOM UTF-8; nếu vẫn lỗi → import vào Excel bằng "Dữ liệu → Từ văn bản/CSV", chọn UTF-8 |

---

## 6. Kiến trúc (cho nhà phát triển)

```
appsettings.json            ← cấu hình ngoài (khách hàng sửa được)
src\
  CccdCheckIn.Core\         ← hợp đồng (interface) + parser, models — thuần, không phụ thuộc UI/DI
    Contracts\ICccdReader.cs, ICheckInStore.cs, IRejectedLogStore.cs, IQrPayloadParser.cs
    Parsing\CccdQrParser.cs, CccdDateParser.cs
    Models\CitizenInfo.cs, CheckInRecord.cs
  CccdCheckIn.Reader.Serial\ ← plugin máy quét: SerialPort + tự dò COM + tự kết nối lại
    SerialCccdReader.cs, ScanBuffer.cs, ComPortAutoDetector.cs, ReaderPortConfig.cs
  CccdCheckIn.Storage.Sqlite\ ← plugin lưu trữ: SQLite + xuất CSV (BOM) + log rejected
    SqliteCheckInStore.cs, CheckInSchema.cs, CheckInCsvExporter.cs
  CccdCheckIn.Licensing.Windows\ ← plugin licensing: DPAPI machine key + trial + verify ECDSA
    DpapiMachineKeyStore.cs, LicenseService.cs, WindowsLicenseStore.cs, EmbeddedKeys.cs
  CccdCheckIn.App\          ← WinForms dashboard + composition root (nối dây theo config)
    Program.cs, CompositionRoot.cs, MainForm.cs, SettingsForm.cs, Forms\LicenseForm.cs
    Config\AppConfig.cs, AppConfigFactory.cs
    Services\CheckInPipeline.cs, DuplicateScanGuard.cs, SimulatedCccdReader.cs
tests\CccdCheckIn.Tests\     ← xUnit: parser, store, exporter, chống trùng, licensing, invariant
tools\CccdCheckIn.Simulator\ ← gửi chuỗi mẫu ra COM để test e2e
tools\CccdCheckIn.LicenseIssuer\ ← tool nội bộ (KHÔNG ship): create-keys + issue Mã Kích Hoạt
publish.ps1                ← đóng gói self-contained 1 file
publish\win-x64\           ← sản phẩm bàn giao (exe + appsettings.json)
```

Dữ liệu chạy: SQLite `Data\checkin.db` — 1 file, ghi atomic, query được; mỗi lệnh mở kết nối
riêng nên an toàn với reader (nhiều thread) và không khóa file khi sao lưu.

### Thêm máy đọc / nơi lưu mới (mở rộng)

- **Máy đọc mới** (NFC, Bluetooth, webcam QR…): viết 1 class implement `ICccdReader`
  trong assembly riêng (`Reader.X`), nối tại `CompositionRoot.Create` — không sửa Core.
- **Nơi lưu mới** (CSV file, SQL Server, HTTP API…): viết 1 class implement `ICheckInStore`
  (tuỳ chọn `IRejectedLogStore`) trong assembly riêng (`Storage.Y`) — không sửa Core.

### Kiểm tra

```bash
dotnet test tests/CccdCheckIn.Tests/CccdCheckIn.Tests.csproj
```

- Demo không cần thẻ: đổi `Reader.Mode = "Simulated"` → app tự đẩy 1 mẫu QR mỗi giây qua đúng pipeline.
- Test thật trên cổng COM: cắm máy quét, mở Cài đặt, chọn cổng, quét CCCD thật.
- Licensing: lần đầu chạy tạo `%ProgramData%\LTTuan\CccdCheckIn\` (machine key + trial state).
  Nếu mã hóa DB chưa làm, đây là nơi lưu identity/trial — chạy bản Simulated để xem banner trial.

---

## 7. Changelog

- **v1.1 (licensing)** — Trial 14 ngày → thuê bao tháng/năm, hoạt động offline:
  - Mã Máy + Mã Kích Hoạt (ECDSA P-256), machine identity gắn DPAPI
  - Hết hạn = Read/Export-only — dữ liệu khách hàng không bao giờ bị khóa/xóa
  - Màn hình Bản quyền (copy Mã Máy, nhập/import Mã Kích Hoạt)
  - Tool nội bộ `CccdCheckIn.LicenseIssuer` (create-keys / issue)
  - Integration test `ExpiredLicenseDataSafety` bảo vệ invariant dữ liệu
- **v1.0** — Dashboard check-in hoàn chỉnh: đọc QR qua COM (tự dò VID/PID, tự kết nối lại),
  lưu SQLite theo cấu hình, xuất CSV có BOM, chống quét trùng, log QR lỗi, single-instance.