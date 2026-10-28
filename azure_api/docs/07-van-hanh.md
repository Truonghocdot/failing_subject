# 07 – Vận hành: phát hành, chạy 1 tiến trình, Windows Service

Bản phát hành chỉ cần **MySQL** và **.NET 10 runtime (ASP.NET Core)**: một tiến trình `backend.exe` phục vụ cả API lẫn giao diện ở
`http://localhost:5090`. Không dùng Docker để đóng gói (Docker trên máy dev chỉ giữ MySQL ở cổng 3306).

## 1. Tạo bản publish (máy dev – cần .NET SDK + Node)

```powershell
cd azure_api\backend
dotnet publish -c Release -o publish      # tự chạy `npm ci` (nếu chưa có node_modules) + `npm run build` ở ../fe
# đã build FE sẵn / máy không có Node:  dotnet publish -c Release -o publish -p:SkipFrontend=true
```

Kết quả `backend\publish\` (thư mục bị `.gitignore`):

```
publish/
├── backend.exe, *.dll             # API (.NET 10, framework-dependent)
├── appsettings*.json              # mặc định không bí mật; Production: log ra file + tự migrate
├── wwwroot/                       # fe/dist – index.html + assets/<tên-có-hash>.js|css
├── .env                           # ← TỰ TẠO trên máy chạy (không có trong bản publish)
└── logs/backend-YYYYMMDD.log      # sinh ra khi chạy (giữ 14 ngày)
```

Có thể chép nguyên thư mục `publish` sang máy khác (VD `D:\SoChiTieu`).

## 2. File `.env` cạnh `backend.exe`

Copy `backend\.env.example` → `publish\.env`, sửa:

```dotenv
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5090
ConnectionStrings__Default=Server=localhost;Port=3306;Database=expense;User=expense;Password=<mật khẩu>;CharSet=utf8mb4
Jwt__Secret=<chuỗi ngẫu nhiên ≥ 32 ký tự – KHÁC bản dev>
# true nếu có HTTPS phía trước; truy cập http://localhost vẫn được (trình duyệt coi localhost là an toàn),
# truy cập qua IP LAN bằng http:// thì phải để false, nếu không cookie phiên không được lưu → F5 bị đăng xuất
Jwt__RefreshCookieSecure=false
```

- Backend tìm `.env` bằng cách đi ngược lên từ thư mục của `backend.exe` (rồi tới thư mục hiện tại) – service vẫn tìm thấy
  dù thư mục hiện tại của nó là `C:\Windows\System32`.
- **Biến môi trường thật luôn thắng** giá trị trong `.env` (VD service đặt sẵn `ASPNETCORE_ENVIRONMENT=Production`).
- Lưu ý trên máy dev: `publish\` nằm trong `backend\` → thiếu `publish\.env` thì nó dùng `backend\.env` (Development).

## 3. Chạy tay

```powershell
D:\SoChiTieu\backend.exe            # chạy từ thư mục nào cũng được – wwwroot / logs lấy theo thư mục của exe
# hoặc:  dotnet D:\SoChiTieu\backend.dll
curl http://localhost:5090/health   # {"status":"ok","database":"up"}
```

Khi khởi động ở Production: tự áp migration (`Database:MigrateOnStartup` trong `appsettings.Production.json` – máy chạy không cần
`dotnet-ef`), tắt `/openapi`, ghi log ra `logs\`. MySQL chưa lên → app vẫn chạy, `/health` báo `database: down`, có DB là dùng được ngay.

## 4. Windows Service

PowerShell **Run as Administrator**, tại thư mục `azure_api`:

```powershell
.\scripts\install-service.ps1                         # mặc định backend\publish, service "SoChiTieu"
.\scripts\install-service.ps1 -PublishDir D:\SoChiTieu
.\scripts\uninstall-service.ps1                       # dừng + gỡ (giữ nguyên publish, .env, log)
```

Script `install-service.ps1`:

| Bước | Chi tiết |
|---|---|
| Kiểm tra | Có `backend.exe` và `.env` trong thư mục publish; service chưa tồn tại |
| Tài khoản | `NT AUTHORITY\LocalService` (ít quyền): đọc thư mục publish, ghi `logs\`; `.env` chỉ Administrators / SYSTEM / LocalService đọc được |
| Khởi động | `Automatic (Delayed Start)` – chờ mạng và Docker Desktop (MySQL) lên trước |
| Lỗi | Tự khởi động lại sau 5 giây, 5 giây, 1 phút (`sc.exe failure`) |
| Môi trường | Biến `ASPNETCORE_ENVIRONMENT=Production` riêng cho service (registry `Services\SoChiTieu\Environment`) |
| Xác nhận | Start service rồi gọi `/health` |

Thao tác hằng ngày:

```powershell
Get-Service SoChiTieu
Restart-Service SoChiTieu
Get-Content D:\SoChiTieu\logs\backend-$(Get-Date -f yyyyMMdd).log -Tail 50 -Wait
```

Code: `builder.Host.UseWindowsService()` (chạy tay thì không ảnh hưởng gì); service dừng → app nhận tín hiệu tắt như Ctrl+C.

## 5. Cập nhật phiên bản mới

```powershell
.\scripts\uninstall-service.ps1         # hoặc: Stop-Service SoChiTieu
# chép đè bản publish mới (GIỮ LẠI .env và logs\)
.\scripts\install-service.ps1           # hoặc: Start-Service SoChiTieu
```

- Migration mới tự áp khi khởi động. Nên **sao lưu DB trước** ([06 §8](06-database.md#8-sao-lưu)).
- `index.html` gửi `Cache-Control: no-cache`, file trong `assets/` có hash + cache 1 năm → người dùng nhận bản mới ngay lần tải tiếp.
  Tab đang mở từ bản cũ bấm sang trang khác (chunk cũ đã bị xoá) → màn hình "Ứng dụng vừa được cập nhật – Tải lại trang".

## 6. Bảo mật khi chạy production

| Hạng mục | Cách làm |
|---|---|
| Header | Mọi response: `Content-Security-Policy` (chỉ script / kết nối cùng origin, `frame-ancestors 'none'`), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy` |
| OpenAPI | Chỉ bật ở Development |
| Bí mật | Chỉ trong `.env` (không commit, quyền đọc hạn chế); `Jwt__Secret` của production khác bản dev |
| Cookie phiên | `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`; `Secure` khi có HTTPS (`Jwt__RefreshCookieSecure=true`) |
| HTTPS | Ngoài phạm vi máy cá nhân; mở ra Internet thì đặt reverse proxy có TLS (IIS / Caddy) phía trước, bật `Jwt__RefreshCookieSecure=true` |
| Mạng | Chỉ dùng trên máy này → đổi `ASPNETCORE_URLS=http://localhost:5090` để không nghe trên LAN |

## 7. Sự cố thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Service không lên, Event Viewer báo lỗi cấu hình | `.env` thiếu `Jwt__Secret` / `ConnectionStrings__Default` hoặc `App__TimeZone` sai – xem `logs\` |
| `/health` → `database: down` | MySQL chưa chạy (Docker Desktop chưa mở) – app tự dùng được khi DB lên |
| Đăng nhập được nhưng F5 bị đăng xuất | `Jwt__RefreshCookieSecure=true` mà truy cập bằng `http://<IP LAN>` → để `false` hoặc dùng HTTPS |
| Cổng 5090 đã bị dùng | Đang có bản `dotnet run` dev chạy song song – tắt nó hoặc đổi `ASPNETCORE_URLS` |
| Trang trắng, console báo CSP | Có script inline / tài nguyên ngoài origin – mọi thứ phải đi qua Vite build (không nhúng CDN) |
