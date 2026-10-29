# 07 – Vận hành: phát hành, chạy 1 tiến trình, Windows Service

Bản phát hành chỉ cần **MySQL** và **.NET 10 runtime (ASP.NET Core)**. Một tiến trình `backend.exe` phục vụ cả API lẫn giao diện ở
`http://localhost:5100`. Không dùng Docker để đóng gói (Docker trên máy dev chỉ giữ MySQL ở cổng 3306).

## 1. Tạo bản publish (máy dev – cần .NET SDK + Node)

```powershell
cd pr_term3\backend
dotnet publish -c Release -o publish      # tự chạy `npm ci` (nếu chưa có node_modules) + `npm run build` ở ../fe
# đã build FE sẵn / máy không có Node:  dotnet publish -c Release -o publish -p:SkipFrontend=true
```

Kết quả `backend\publish\` (thư mục bị `.gitignore`):

```
publish/
├── backend.exe, *.dll              # API (.NET 10, framework-dependent)
├── appsettings*.json               # không có bí mật; Production: log ra file, tự migrate, link email = :5100
├── wwwroot/                        # fe/dist – index.html + assets/<tên-có-hash>.js|css (tách chunk theo trang)
├── .env                            # ← TỰ TẠO trên máy chạy (không có trong bản publish)
├── storage/uploads/, storage/mail/ # sinh ra khi chạy: ảnh homestay, thư .eml (khi chưa cấu hình SMTP)
└── logs/backend-YYYYMMDD.log       # sinh ra khi chạy (giữ 14 ngày)
```

Có thể chép nguyên thư mục `publish` sang máy khác (VD `D:\HomestayBooking`).

## 2. File `.env` cạnh `backend.exe`

Copy `backend\.env.example` → `publish\.env`, sửa:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5100
ConnectionStrings__Default=Server=localhost;Port=3306;Database=homestay;User=homestay;Password=<mật khẩu>;CharSet=utf8mb4
Jwt__Secret=<chuỗi ngẫu nhiên ≥ 32 ký tự – KHÁC bản dev>
# true nếu có HTTPS phía trước; truy cập http://localhost vẫn được (trình duyệt coi localhost là an toàn),
# truy cập qua IP LAN bằng http:// thì phải để false, nếu không cookie phiên không được lưu → F5 bị đăng xuất
Jwt__RefreshCookieSecure=false
Seed__AdminEmail=<email admin đầu tiên>
Seed__AdminPassword=<mật khẩu ≥ 8 ký tự – đổi ngay sau lần đăng nhập đầu>
App__PublicBaseUrl=http://localhost:5100          # link trong email gửi khách
# Tài khoản nhận cọc – thiếu thì trang đặt phòng KHÔNG hiện hướng dẫn chuyển khoản (Development có tài khoản demo)
Payment__BankName=<ngân hàng>
Payment__AccountNumber=<số tài khoản>
Payment__AccountName=<chủ tài khoản>
# Gửi email thật (bỏ trống Smtp__Host → ghi .eml vào storage\mail để xem)
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__User=<tài khoản>
Smtp__Password=<app password>
Smtp__From=Homestay Booking <no-reply@ten-mien.vn>
```

- Backend tìm `.env` bằng cách đi ngược lên từ thư mục của `backend.exe` (rồi tới thư mục hiện tại), nên service vẫn tìm thấy
  dù thư mục hiện tại của nó là `C:\Windows\System32`.
- **Biến môi trường thật luôn thắng** giá trị trong `.env` (VD service đặt sẵn `ASPNETCORE_ENVIRONMENT=Production`).
- Lưu ý trên máy dev: `publish\` nằm trong `backend\`, nên thiếu `publish\.env` thì nó dùng `backend\.env` (Development).
- Production không nạp dữ liệu mẫu (`Seed__SampleData` mặc định `false`) – chỉ tạo admin đầu tiên khi DB chưa có admin.

## 3. Chạy tay

```powershell
D:\HomestayBooking\backend.exe      # chạy từ thư mục nào cũng được – wwwroot / storage / logs lấy theo thư mục của exe
# hoặc:  dotnet D:\HomestayBooking\backend.dll
curl http://localhost:5100/health   # {"status":"ok","database":"up"}
```

Khi khởi động ở Production:
- Tự áp migration (`Database:MigrateOnStartup` trong `appsettings.Production.json`), nên máy chạy không cần `dotnet-ef`.
  Đã thử trên DB trống: 0 → 16 bảng + admin đầu tiên.
- Tắt `/openapi`, ghi log ra `logs\`.
- Bật 2 dịch vụ nền: hết hạn giữ chỗ (mỗi phút) và hàng đợi email (mỗi 30 giây).
- MySQL chưa lên thì app vẫn chạy, `/health` báo `database: down`; khi có DB là dùng được ngay.

## 4. Windows Service

PowerShell **Run as Administrator**, tại thư mục `pr_term3`:

```powershell
.\scripts\install-service.ps1                              # mặc định backend\publish, service "HomestayBooking"
.\scripts\install-service.ps1 -PublishDir D:\HomestayBooking
.\scripts\uninstall-service.ps1                            # dừng + gỡ (giữ nguyên publish, .env, log, ảnh, .eml)
```

Script `install-service.ps1`:

| Bước | Chi tiết |
|---|---|
| Kiểm tra | Có `backend.exe` và `.env` trong thư mục publish; service chưa tồn tại |
| Tài khoản | `NT AUTHORITY\LocalService` (ít quyền): đọc thư mục publish, ghi `logs\` + `storage\` (ảnh upload, `.eml`); `.env` chỉ Administrators / SYSTEM / LocalService đọc được |
| Khởi động | `Automatic (Delayed Start)` – chờ mạng và Docker Desktop (MySQL) lên trước |
| Lỗi | Tự khởi động lại sau 5 giây, 5 giây, 1 phút (`sc.exe failure`) |
| Môi trường | Biến `ASPNETCORE_ENVIRONMENT=Production` riêng cho service (registry `Services\HomestayBooking\Environment`) |
| Xác nhận | Start service rồi gọi `/health` |

Thao tác hằng ngày:

```powershell
Get-Service HomestayBooking
Restart-Service HomestayBooking
Get-Content D:\HomestayBooking\logs\backend-$(Get-Date -f yyyyMMdd).log -Tail 50 -Wait
```

Code: `builder.Host.UseWindowsService()` – chạy tay thì không ảnh hưởng gì; khi service dừng, app nhận tín hiệu tắt như Ctrl+C
(dịch vụ nền dừng gọn, thư đang gửi dở được gửi lại lần sau).

## 5. Cập nhật phiên bản mới

```powershell
.\scripts\uninstall-service.ps1         # hoặc: Stop-Service HomestayBooking
# chép đè bản publish mới – GIỮ LẠI .env, logs\ và storage\ (ảnh homestay!)
.\scripts\install-service.ps1           # hoặc: Start-Service HomestayBooking
```

- Migration mới tự áp khi khởi động. Nên **sao lưu DB + `storage\uploads` trước** ([06 §6](06-database.md#6-sao-lưu)).
- `index.html` gửi `Cache-Control: no-cache`; file trong `assets/` có hash trong tên và cache 1 năm, nên người dùng nhận bản mới
  ngay lần tải tiếp. Nếu tab đang mở từ bản cũ bấm sang trang khác (chunk cũ đã bị xoá), màn hình báo "Ứng dụng vừa được cập nhật – Tải lại trang".

## 6. Bảo mật khi chạy production

| Hạng mục | Cách làm |
|---|---|
| Header | Mọi response: `Content-Security-Policy` (chỉ script / kết nối / ảnh cùng origin, `frame-ancestors 'none'`), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy` |
| OpenAPI | Chỉ bật ở Development (production `/openapi/v1.json` → 404) |
| Bí mật | Chỉ trong `.env` (không commit, quyền đọc hạn chế); `Jwt__Secret` và mật khẩu admin của production khác bản dev |
| Cookie phiên | `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`; `Secure` khi có HTTPS (`Jwt__RefreshCookieSecure=true`) |
| Ảnh upload | Kiểm tra định dạng thật (magic bytes), tên ngẫu nhiên, chỉ phục vụ `.jpg/.png/.webp` kèm `nosniff` |
| Chống dò | Giới hạn đăng nhập / đăng ký / tra cứu `RateLimit__AuthPermitPerMinute` (10 / phút / IP) |
| HTTPS | Ngoài phạm vi máy cá nhân; mở ra Internet thì đặt reverse proxy có TLS (IIS / Caddy) phía trước, bật `Jwt__RefreshCookieSecure=true` |
| Mạng | Chỉ dùng trên máy này → đổi `ASPNETCORE_URLS=http://localhost:5100` để không nghe trên LAN |

## 7. Trợ năng (đã rà soát ở Phase 7)

- axe-core (WCAG 2.1 A + AA) trên bản publish: **0 vi phạm** ở 24 trang (khách vãng lai, khách, admin) × sáng / tối × 1280 / 360 px.
- Bàn phím:
  - Tab đầu tiên là link "Bỏ qua tới nội dung chính".
  - Mọi phần tử nhận focus đều có viền focus.
  - Hộp thoại: focus vào trong khi mở; Esc đóng và trả focus về nút / ô đã mở nó.
  - Ô lịch phòng và thanh đặt phòng mở được bằng Enter.
  - Bảng rộng cuộn được bằng phím mũi tên.
- Chế độ tối theo cài đặt hệ điều hành (`prefers-color-scheme`); màu trạng thái luôn kèm chữ.

## 8. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Service không lên, Event Viewer báo lỗi cấu hình | `.env` thiếu `Jwt__Secret` / `ConnectionStrings__Default` hoặc `App__TimeZone` sai – xem `logs\` |
| `/health` → `database: down` | MySQL chưa chạy (Docker Desktop chưa mở) – app tự dùng được khi DB lên |
| Đăng nhập được nhưng F5 bị đăng xuất | `Jwt__RefreshCookieSecure=true` mà truy cập bằng `http://<IP LAN>` → để `false` hoặc dùng HTTPS |
| Trang đặt phòng không có hướng dẫn chuyển khoản | Chưa điền `Payment__BankName` / `Payment__AccountNumber` trong `.env` |
| Khách không nhận được email | `Smtp__*` sai → bảng `email_outbox` cột `last_error`, thử lại 5 lần rồi `failed`; chưa cấu hình SMTP thì thư nằm ở `storage\mail\*.eml` |
| Ảnh homestay mất sau khi cập nhật | Chép đè đã xoá `storage\uploads` – khôi phục từ bản sao lưu; lần sau giữ lại thư mục `storage\` |
| Cổng 5100 đã bị dùng | Đang có bản `dotnet run` dev chạy song song – tắt nó hoặc đổi `ASPNETCORE_URLS` |
| Trang trắng, console báo CSP | Có script inline / tài nguyên ngoài origin – mọi thứ phải đi qua Vite build (không nhúng CDN) |
